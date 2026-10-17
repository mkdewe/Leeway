using RootMotion.Dynamics;
using UnityEngine;

namespace Leeway.CreatureEditor
{
    /// <summary>
    /// Movement done by the muscles: the creature travels because its physical body is pushed, not
    /// because a capsule is told where to be.
    /// </summary>
    /// <remarks>
    /// <para><b>What this costs, and why it is a mode rather than the only way.</b> The capsule can be
    /// predicted: the owner runs the same step the server will run, and a mismatch is corrected. A
    /// jointed chain cannot — PhysX is not deterministic across machines, and a seven-body ragdoll
    /// diverges within a few ticks, so a client that predicted it would spend its life being snapped
    /// back. Muscle-driven movement is therefore <b>server-authoritative</b>: the server pushes the
    /// muscles and the result reaches everyone as the usual correction, at the cost of the owner's
    /// input arriving a round trip late.</para>
    ///
    /// <para><b>How it moves.</b> Three forces on the hips muscle, and nothing else: a pull towards the
    /// velocity the player asked for, a spring that holds the body at its standing height, and a torque
    /// that keeps it upright and facing where it is going. The legs are not "used" to walk — they are
    /// solved by IK as before and push off the world through their own muscles, which is what makes a
    /// creature stumble on a rock rather than glide over it.</para>
    ///
    /// <para><b>The capsule is still there</b>, kinematic, because the editor pad and the pickups are
    /// triggers that ask about it. It no longer moves anything.</para>
    ///
    /// <para><b>Off by default, and here is what it still needs.</b> Three configurations were tried
    /// and measured. With the pin on everything, the creature and its puppet chase each other and slide
    /// across the arena at 8 m/s with nobody touching the keyboard. With the hips alone unpinned and
    /// the capsule made kinematic, the position holds but the hips' velocity climbs without bound —
    /// the spine is still pinned to bones that are being carried by the hips. With the pin off
    /// entirely, everything is stable and the creature simply lies down: the joint drives hold its
    /// <b>shape</b> but nothing holds its <b>balance</b>, and it travels 0.01 m in two seconds of being
    /// pushed.</para>
    ///
    /// <para>That last measurement is the real answer. PuppetMaster stands a character up by pinning it
    /// to an animated pose; take the pin away and standing becomes a balance problem — centre of mass
    /// over a support polygon, feet placed to catch a fall — which is a controller this project does not
    /// have and which no part of the package provides. Until one exists, the working arrangement is the
    /// other one: the pose is held by the pin, and collisions move the creature through
    /// <see cref="CreaturePuppetPush"/>. Turn this on to carry on that work, not to play.</para>
    /// </remarks>
    [RequireComponent(typeof(CreaturePuppet))]
    public class CreatureMuscleLocomotion : MonoBehaviour
    {
        [Tooltip("Off: the creature moves on its predicted capsule and the puppet dresses it. On: the muscles " +
                 "move the creature, which needs a balance controller this project does not have yet — see the " +
                 "class summary for what was measured.")]
        [SerializeField] private bool _enabled;

        [Header("Drive")]
        [Tooltip("How hard the body is pulled towards the speed the player asked for.")]
        [SerializeField, Min(0f)] private float _acceleration = 26f;

        [Tooltip("How hard it is held at its standing height. This is what replaces the suspension.")]
        [SerializeField, Min(0f)] private float _supportSpring = 90f;

        [SerializeField, Min(0f)] private float _supportDamper = 12f;

        [Tooltip("How hard the body is kept upright and turned towards its heading.")]
        [SerializeField, Min(0f)] private float _uprightTorque = 40f;

        [Header("Limits")]
        [Tooltip("The most the drive may add per second, so a shove cannot be out-muscled instantly.")]
        [SerializeField, Min(0f)] private float _maxDriveSpeed = 8f;

        [Tooltip("The fastest the creature may follow its own body. Anything faster is a runaway, not a walk.")]
        [SerializeField, Min(0f)] private float _maxFollowSpeed = 12f;

        [Tooltip("Beyond this gap the creature was moved by something that is not physics, and the body is teleported to it.")]
        [SerializeField, Min(0.1f)] private float _teleportDistance = 1.5f;

        [Tooltip("How briskly the small drift between the creature and its body is worked off.")]
        [SerializeField, Min(0f)] private float _followStiffness = 6f;

        private CreaturePuppet _puppet;
        private Rigidbody _body;
        private CreatureSuspension _suspension;

        /// <summary>Where the hips sit relative to the creature root when nothing is wrong — the rest pose, not an error.</summary>
        private Vector3 _restOffset;
        private bool _restOffsetKnown;

        /// <summary>Whether the hips have been handed the lead — done once, not every step.</summary>
        private bool _leading;

        /// <summary>Whether movement is being done by the muscles rather than by the capsule.</summary>
        public bool IsActive => _enabled && _puppet != null && _puppet.PuppetMaster != null && !_puppet.IsDown;

        private void Awake()
        {
            _puppet = GetComponent<CreaturePuppet>();
            TryGetComponent(out _body);
            TryGetComponent(out _suspension);
        }

        /// <summary>
        /// One step of muscle-driven movement, on the server.
        /// </summary>
        /// <param name="direction">Where the player wants to go, in world space. Zero means stand still.</param>
        /// <param name="speed">How fast, in metres per second.</param>
        /// <param name="standHeight">How high above the ground the body belongs — from the legs' reach.</param>
        public void Drive(Vector3 direction, float speed, float standHeight, float deltaTime)
        {
            if (!IsActive) return;

            Muscle hips = _puppet.PuppetMaster.muscles[0];
            if (hips?.rigidbody == null) return;

            TakeTheLead(hips);

            Rigidbody body = hips.rigidbody;

            Accelerate(body, direction, speed, deltaTime);
            Support(body, standHeight);
            KeepUpright(body, direction);

            FollowWithTheCreature(body, deltaTime);
        }

        /// <summary>
        /// Makes the hips the thing that leads, once.
        /// </summary>
        /// <remarks>
        /// <para><b>The two of them cannot both follow.</b> PuppetMaster pins every muscle to its bone,
        /// and the bones are carried by the creature's own rigid body — so if the creature also follows
        /// the hips, each drags the other and the pair accelerates away. Measured before this fix: a
        /// creature with no input on the keyboard sliding across the arena at 8 m/s.</para>
        ///
        /// <para>So the hips stop being pinned — they are what the forces push — and the creature's body
        /// becomes kinematic, carried to wherever the hips ended up. Every other muscle keeps its pin,
        /// which is what still holds the spine and the legs in the pose the genome describes.</para>
        /// </remarks>
        private void TakeTheLead(Muscle hips)
        {
            if (_leading) return;

            _leading = true;

            hips.props.pinWeight = 0f;

            if (_body != null)
            {
                _body.linearVelocity = Vector3.zero;
                _body.isKinematic = true;
            }
        }

        /// <summary>
        /// Pulls the body towards the requested velocity.
        /// </summary>
        /// <remarks>
        /// Acceleration rather than a velocity assignment, so the creature has to overcome its own
        /// weight and whatever is pushing it — which is the whole point of moving this way. The
        /// vertical component is left alone: falling is gravity's business.
        /// </remarks>
        private void Accelerate(Rigidbody body, Vector3 direction, float speed, float deltaTime)
        {
            Vector3 wanted = direction.sqrMagnitude > 0.0001f ? direction.normalized * speed : Vector3.zero;

            Vector3 velocity = body.linearVelocity;
            Vector3 planar = new Vector3(velocity.x, 0f, velocity.z);

            Vector3 difference = wanted - planar;
            if (difference.magnitude > _maxDriveSpeed) difference = difference.normalized * _maxDriveSpeed;

            body.AddForce(difference * _acceleration * deltaTime, ForceMode.VelocityChange);
        }

        /// <summary>
        /// Holds the body up.
        /// </summary>
        /// <remarks>
        /// A spring against the ground under the creature, not a fixed height: it rides up a slope,
        /// settles into a dip, and stops pushing altogether when there is nothing underneath — so
        /// stepping off a ledge is a fall rather than a hover.
        /// </remarks>
        private void Support(Rigidbody body, float standHeight)
        {
            if (!Physics.Raycast(body.position, Vector3.down, out RaycastHit hit, standHeight * 2f + 0.5f,
                    ~0, QueryTriggerInteraction.Ignore))
                return;

            float error = standHeight - hit.distance;
            float damping = -body.linearVelocity.y * _supportDamper;

            body.AddForce(Vector3.up * (error * _supportSpring + damping), ForceMode.Acceleration);
        }

        /// <summary>Keeps the body level and turned the way it is travelling.</summary>
        private void KeepUpright(Rigidbody body, Vector3 direction)
        {
            Quaternion wanted = direction.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(new Vector3(direction.x, 0f, direction.z).normalized, Vector3.up)
                : Quaternion.LookRotation(Vector3.ProjectOnPlane(body.transform.forward, Vector3.up).normalized, Vector3.up);

            Quaternion difference = wanted * Quaternion.Inverse(body.rotation);
            difference.ToAngleAxis(out float angle, out Vector3 axis);
            if (angle > 180f) angle -= 360f;
            if (Mathf.Abs(angle) < 0.01f || float.IsInfinity(axis.x)) return;

            body.AddTorque(axis.normalized * (angle * Mathf.Deg2Rad * _uprightTorque), ForceMode.Acceleration);
        }

        /// <summary>
        /// Moves the creature to wherever its body ended up.
        /// </summary>
        /// <remarks>
        /// <para>The creature's own rigid body is what the network replicates, so this is the seam
        /// between the two worlds: the puppet is pushed by physics, and the capsule is told the result.
        /// Velocity rather than position, because the replication and the correction on the other
        /// clients are built around it — and because a teleported capsule would sweep through walls
        /// that the muscles had already stopped at.</para>
        ///
        /// <para>The suspension is switched off while this runs: two systems holding the same creature
        /// at the same height fight each other, and the spring here is the one that knows what the
        /// muscles are doing.</para>
        /// </remarks>
        private void FollowWithTheCreature(Rigidbody hips, float deltaTime)
        {
            if (_body == null) return;

            if (_suspension != null && _suspension.enabled) _suspension.enabled = false;

            // The hips sit some way above and in front of the creature's root — that gap is the rest
            // pose, not an error, and chasing it would have the creature accelerating forever towards
            // its own hips. So it is recorded once and subtracted from every comparison after.
            if (!_restOffsetKnown)
            {
                _restOffset = hips.position - _body.position;
                _restOffsetKnown = true;
            }

            Vector3 error = hips.position - _restOffset - _body.position;

            // A gap this wide is not movement, it is the creature having been put somewhere: spawned,
            // stood up by the server, corrected. Chasing it with velocity would fling the creature
            // across the level and the puppet after it, each pulling the other further — measured, it
            // reaches tens of metres per second inside a second. The body is told to jump instead.
            if (error.sqrMagnitude > _teleportDistance * _teleportDistance)
            {
                _puppet.Teleport();
                _restOffsetKnown = false;
                return;
            }

            // The creature is carried to where its body is. Position rather than velocity, because the
            // body is kinematic now: the muscles have already done the colliding, and integrating the
            // capsule as well would be a second, disagreeing simulation of the same creature.
            _body.MovePosition(hips.position - _restOffset);

            Vector3 forward = Vector3.ProjectOnPlane(hips.transform.forward, Vector3.up);
            if (forward.sqrMagnitude > 0.0001f)
                _body.MoveRotation(Quaternion.LookRotation(forward.normalized, Vector3.up));
        }
    }
}
