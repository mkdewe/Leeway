using Leeway.Creature.Domain;
using UnityEngine;

namespace Leeway.CreatureEditor
{
    /// <summary>
    /// Holds the creature up on its legs with a spring force instead of propping it on a rigid shape.
    /// </summary>
    /// <remarks>
    /// <para>The locomotion capsule wraps the torso alone and <b>floats</b> above the ground — the
    /// clearance under the belly is made by the suspension, not by an offset collider. That lets the
    /// body compress on landing, rise over a step and respond to uneven terrain, none of which a rigid
    /// offset gave.</para>
    ///
    /// <para><b>The force is applied on the prediction tick</b>, from inside the replicated movement,
    /// not in <c>FixedUpdate</c>. A step computed outside the replication loop would not be replayed
    /// during reconciliation and the client would drift away from the server on the first correction.</para>
    /// </remarks>
    [RequireComponent(typeof(Rigidbody))]
    public class CreatureSuspension : MonoBehaviour
    {
        [Header("Suspension")]
        [SerializeField, Min(0f)] private float _stiffness = 70f;

        [Tooltip("1 = critical damping: the fastest return with no bobbing. Below it bobs, above it settles sluggishly.")]
        [SerializeField, Range(0.2f, 2f)] private float _dampingRatio = 1f;

        [Tooltip("How far below the rest height a leg still reaches the ground.")]
        [SerializeField, Min(0f)] private float _maxDroop = 0.45f;

        [Tooltip("Layers treated as ground.")]
        [SerializeField] private LayerMask _groundMask = 1;

        private Rigidbody _rb;
        private CreatureBody _body;
        private CreatureLocomotion _locomotion;

        /// <summary>Whether the legs reach the ground. In the air there is nothing to push off.</summary>
        public bool IsGrounded { get; private set; }

        /// <summary>The distance to the ground measured on the last step.</summary>
        public float GroundDistance { get; private set; }

        /// <summary>
        /// How far the suspension is compressed. The tension in the legs — the visual layer presses the
        /// knees down by it, so a loaded leg bends visibly.
        /// </summary>
        public float Compression { get; private set; }

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _body = GetComponent<CreatureBody>();
            TryGetComponent(out _locomotion);
        }

        /// <summary>
        /// Applies the suspension acceleration. Called from the replicated movement so reconciliation
        /// replays it exactly like the rest of the step.
        /// </summary>
        public void Simulate(float deltaTime)
        {
            // The target is the root's height, not the leg reach alone — the root sits in the middle of
            // the carcass, so its radius has to be added in, otherwise the capsule rests on the ground
            // permanently.
            float rideHeight = _body != null ? GenomeStatRules.StandHeight(_body.Genome, _body.Rules) : 0f;

            // A legless creature has nothing to straighten — it lies belly-down on the capsule.
            if (rideHeight <= 0.001f)
            {
                IsGrounded = true;
                GroundDistance = 0f;
                return;
            }

            var spring = new Suspension(_stiffness, _dampingRatio, _maxDroop);
            float probe = rideHeight + _maxDroop;

            GroundDistance = ProbeUnderLegs(probe);
            IsGrounded = spring.IsGrounded(rideHeight, GroundDistance);

            if (!IsGrounded) return;

            float gravity = _rb.useGravity ? -Physics.gravity.y : 0f;
            float acceleration = spring.Acceleration(rideHeight, GroundDistance, _rb.linearVelocity.y, gravity);

            Compression = Mathf.Max(0f, rideHeight - GroundDistance);
            if (acceleration <= 0f) return;

            _rb.linearVelocity += Vector3.up * (acceleration * deltaTime);
        }

        /// <summary>
        /// The distance to the ground as seen by the <b>legs</b>, rather than by a single ray from the
        /// middle of the body.
        /// </summary>
        /// <remarks>
        /// <para>We take the nearest ground under any of the hips. That way a creature putting one leg
        /// onto a step rises immediately instead of waiting for the obstacle to be exactly beneath its
        /// centre — and coming down a slope it does not hang in the air because the centre lost contact
        /// before the legs did.</para>
        ///
        /// <para>The resulting support still acts vertically on the centre of mass. Distributing it
        /// across the individual feet would produce lean, but the body has its rotation frozen in X and
        /// Z — without that lock the predicted capsule would topple and fight reconciliation. Lean is
        /// therefore a job for the visual layer.</para>
        /// </remarks>
        private float ProbeUnderLegs(float maxDistance)
        {
            float closest = Probe(_rb.position, maxDistance);

            if (_locomotion == null) return closest;

            foreach (Vector3 hip in _locomotion.HipPositions)
            {
                // The hip is projected up to the root's height — what we care about is the ground under
                // the leg, not how high that leg is attached to the carcass.
                var origin = new Vector3(hip.x, _rb.position.y, hip.z);
                closest = Mathf.Min(closest, Probe(origin, maxDistance));
            }

            return closest;
        }

        /// <summary>The distance straight down from the given point. Infinity when nothing was hit.</summary>
        private float Probe(Vector3 from, float maxDistance)
        {
            // Start slightly above the point so the ray does not set off from inside our own capsule.
            const float lift = 0.05f;

            Vector3 origin = from + Vector3.up * lift;

            return Physics.Raycast(origin, Vector3.down, out RaycastHit hit, maxDistance + lift, _groundMask, QueryTriggerInteraction.Ignore)
                ? hit.distance - lift
                : float.PositiveInfinity;
        }
    }
}
