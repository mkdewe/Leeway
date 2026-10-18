using System.Collections.Generic;
using Leeway.Creature.Domain;
using UnityEngine;

namespace Leeway.CreatureEditor
{
    /// <summary>
    /// The creature's procedural gait: it unfolds the locomotion parts attached in the editor into
    /// bending chains and drives their step cycle from the distance travelled.
    /// </summary>
    /// <remarks>
    /// <para><b>The legs are not fabricated</b> — the chain is made from the very part instance the
    /// player attached in the editor (see <see cref="ProceduralLeg"/>), and it is built by the same
    /// <see cref="CreatureLegFactory"/> the sculpting preview uses. This layer only drives their step
    /// cycle.</para>
    ///
    /// <para><b>The gait phase comes from distance, not from time.</b> That is what stops the creature
    /// "swimming" — the feet move by exactly as much as the body travelled, so during support they
    /// stand still no matter how fast it is going. A cycle driven by time would look right at one speed
    /// and slide at every other.</para>
    ///
    /// <para><b>Turning on the spot is travel too.</b> The steering deliberately turns the creature
    /// around without moving it (see <c>LocomotionSteering</c>), so the distance travelled by the body
    /// centre is then zero — the leg would not take a single step while the hip moved away regardless.
    /// Hence the added arc the hips sweep: when turning around, the creature paces its legs rather than
    /// swivelling its torso over feet rooted in the ground.</para>
    ///
    /// <para>The layer is purely visual and local — it touches neither prediction nor the network. The
    /// movement is still done by the locomotion capsule; the legs only show what it looks like.</para>
    /// </remarks>
    [RequireComponent(typeof(CreatureBody))]
    public class CreatureLocomotion : MonoBehaviour
    {
        [Header("Gait")]
        [Tooltip("The layers the feet step on.")]
        [SerializeField] private LayerMask _groundMask = 1;

        [Tooltip("How fast a foot pulls towards its computed target.")]
        [SerializeField] private float _footResponsiveness = 18f;

        [Tooltip("Below this speed the creature decides it is standing still and stops pacing its legs.")]
        [SerializeField] private float _idleSpeed = 0.05f;

        [Tooltip("How fast the step fades in when setting off and out when stopping (per second). On stopping, a raised foot comes down instead of freezing in mid-air.")]
        [SerializeField, Min(0.1f)] private float _gaitBlendSpeed = 5f;

        [Tooltip("How far beyond the legs' reach the ground may drop before the creature counts as airborne (m). In the air the legs hold their pose instead of stepping.")]
        [SerializeField, Min(0f)] private float _airborneMargin = 0.2f;

        [Header("Body tilt")]
        [Tooltip("How hard the legs push the body level with the ground they stand on (1/s^2). Higher = stiffer, lower = floppier.")]
        [SerializeField, Min(5f)] private float _tiltStiffness = 70f;

        [Tooltip("1 = settles without overshoot, below it the body sways before it settles.")]
        [SerializeField, Range(0.2f, 1.5f)] private float _tiltDamping = 0.65f;

        [Tooltip("How much speeding up or braking pitches the body.")]
        [SerializeField, Range(0f, 1.5f)] private float _inertiaCoupling = 0.35f;

        [Tooltip("How strongly the ground under the feet turns the body toward its slope (1/s^2). What pitches a creature with only one pair of legs.")]
        [SerializeField, Min(0f)] private float _soleStiffness = 45f;

        [SerializeField, Range(10f, 60f)] private float _maxPitch = 38f;
        [SerializeField, Range(10f, 45f)] private float _maxRoll = 28f;

        [Header("Lean")]
        [Tooltip("How many degrees per second the lean keeps up with the load. Lower = sluggish, higher = jerky.")]
        [SerializeField, Min(1f)] private float _leanResponsiveness = 220f;

        private readonly List<ProceduralLeg> _legs = new();
        private readonly List<Vector3> _hipPositions = new();
        private readonly List<Vector3> _footPositions = new();

        /// <summary>
        /// The legs' attachment points in world space. The suspension probes the ground beneath exactly
        /// these, so a step under one leg lifts the creature before it is under its centre.
        /// </summary>
        /// <summary>The creature's legs. The physical body reads them to put a muscle on every link.</summary>
        public IReadOnlyList<ProceduralLeg> Legs => _legs;

        public IReadOnlyList<Vector3> HipPositions
        {
            get
            {
                _hipPositions.Clear();
                foreach (ProceduralLeg leg in _legs) _hipPositions.Add(leg.HipPosition);
                return _hipPositions;
            }
        }

        /// <summary>Where each foot is now, in world space - for effects such as footstep dust.</summary>
        public IReadOnlyList<Vector3> FootPositions
        {
            get
            {
                _footPositions.Clear();
                foreach (ProceduralLeg leg in _legs) _footPositions.Add(leg.FootPosition);
                return _footPositions;
            }
        }

        private CreatureBody _body;
        private Vector3 _lastPosition;
        private float _lastYaw;
        private float _phase;
        private float _gaitWeight;
        private float _standHeight;
        private float _unsupportedSeconds;
        private Vector2 _tilt;
        private Vector2 _tiltRate;
        private Vector3 _tiltLastPosition;
        private Vector3 _tiltLastVelocity;
        private readonly List<Vector3> _supportBuffer = new();
        private readonly List<ProceduralLeg> _order = new();
        public float GaitPhase => _phase;

        /// <summary>Whether the body was travelling horizontally on the last frame.</summary>
        public bool IsMoving { get; private set; }

        /// <summary>
        /// Nothing under the creature within its legs' reach: mid-jump, falling, or floating over deep
        /// water. The gait holds still then - effects such as footstep dust read this too.
        /// </summary>
        public bool IsAirborne { get; private set; }

        /// <summary>Whether the gait is frozen for the duration of a ragdoll.</summary>
        private bool _suspended;

        /// <summary>The current, smoothed lean in degrees.</summary>
        private float _lean;

        private void Awake()
        {
            _body = GetComponent<CreatureBody>();
            _lastPosition = transform.position;
            _lastYaw = transform.eulerAngles.y;
        }

        private void OnEnable() => _body.BodyRebuilt += OnBodyRebuilt;

        private void OnDisable()
        {
            _body.BodyRebuilt -= OnBodyRebuilt;
            // The independent physics scene still owns muscles targeting these leg anchors.
            var puppet = GetComponent<CreaturePuppet>();
            if (puppet != null) puppet.ReleaseBody();
            ClearLegs();
        }

        private void OnBodyRebuilt(CreatureGenome genome, CreatureStats stats) => BuildLegs(genome);

        /// <summary>
        /// Recreates the legs from the genome. Called after every body rebuild — bones do not survive a
        /// rebuild, so the old hips would point at destroyed objects.
        /// </summary>
        private void BuildLegs(CreatureGenome genome)
        {
            ClearLegs();
            CreatureLegFactory.Build(genome, _body.Rules, _body.Built, _legs);
            _standHeight = GenomeStatRules.StandHeight(genome, _body.Rules);
        }

        /// <summary>
        /// Freezes the step cycle for the duration of a knockdown.
        /// </summary>
        /// <remarks>
        /// In ragdoll mode the bones are governed by physics and the hips travel with them. IK computed
        /// anyway would keep planting the feet on the ground throughout the fall, so a fallen creature
        /// would stand on straight legs beside its own carcass.
        /// </remarks>
        public void Suspend() => _suspended = true;

        /// <summary>
        /// Resumes the gait under a creature the server has just put back on its feet.
        /// </summary>
        /// <remarks>
        /// <para>The feet get a <b>fresh resting pose</b> under the current hips. Their previous target
        /// stayed in the world where the creature fell over — without resetting it, the leg would keep
        /// pulling towards it for the first half second of running and it would look like the splits.</para>
        ///
        /// <para>Resetting <c>_lastPosition</c> matters just as much: standing the creature up is a jump
        /// of several metres, and the step cycle runs on <b>distance travelled</b>. Without the reset
        /// that jump would enter the phase as one gigantic step and the legs would skip a dozen cycles
        /// in a single frame.</para>
        /// </remarks>
        public void Resume()
        {
            _suspended = false;
            _lastPosition = transform.position;
            _lastYaw = transform.eulerAngles.y;

            Vector3 forward = transform.forward;
            foreach (ProceduralLeg leg in _legs) leg.Rest(forward);
        }

        /// <summary>
        /// Leans the body into the corner according to how hard the legs are loaded.
        /// </summary>
        /// <remarks>
        /// <para>The lean goes into the <b>armature</b>, not into the locomotion body: that one has its
        /// rotation frozen in X and Z, because a predicted capsule that topples on its own would fight
        /// reconciliation. So we rotate what is seen, not what the network simulates.</para>
        ///
        /// <para>Applied <b>before</b> the step cycle in the same method rather than in a separate
        /// component: the order has to be certain. The hips travel with the armature, and the IK solver
        /// has to see them already leaning and plant the feet on the ground — that way the body lies
        /// over legs that stay rooted in the ground, instead of leaning along with them.</para>
        ///
        /// <para>The smoothing lives here rather than in the movement layer, because that one runs on
        /// network ticks (30 Hz) and this has to be smooth per frame.</para>
        /// </remarks>
        private void ApplyLean()
        {
            Transform armature = _body.Built?.Armature;
            if (armature == null) return;

            float target = _suspended ? 0f : _body.LeanDegrees;
            _lean = Mathf.MoveTowards(_lean, target, _leanResponsiveness * Time.deltaTime);

            // The sign is inverted: a rotation about +Z tips the top towards negative X, and we want a
            // positive lean to mean "top towards the creature's right". Without this the body leaned the
            // opposite way from the one it was about to fall.
            // The body also follows the ground: nose up on the way up a slope, nose down on the way down, and
            // banked along a side slope, so the legs reach the surface instead of dangling from a level torso.
            StepTilt(Time.deltaTime);

            armature.localRotation = Quaternion.Euler(-_tilt.x, 0f, -_lean + _tilt.y);
        }

        /// <summary>
        /// Zeroes the lean immediately, without smoothing.
        /// </summary>
        /// <remarks>
        /// Called while standing the creature up after a fall. The bones' rest pose is recorded
        /// <b>relative to the armature</b>, so a leaning armature would stand it up permanently crooked.
        /// </remarks>
        public void ResetLean()
        {
            _lean = 0f;
            _tilt = Vector2.zero;
            _tiltRate = Vector2.zero;

            Transform armature = _body.Built?.Armature;
            if (armature != null) armature.localRotation = Quaternion.identity;
        }

        /// <summary>
        /// Rotates the torso the way a body on springy legs rotates.
        /// </summary>
        /// <remarks>
        /// <para>Each planted leg is a spring between its hip and the ground under its foot. The more it is
        /// squeezed beyond what the others are, the harder it pushes that corner of the body up. Those
        /// pushes act at the hips, so unequal ones make a torque about the body's centre; the body then
        /// turns under it as a rigid body does, with its own inertia and damping, and settles when every
        /// leg is squeezed the same - which is exactly when the torso lies parallel to the ground the
        /// feet stand on. Nothing here says "tilt to the slope": a slope, a step under one foot or a leg
        /// swinging over a hollow all just change the compressions and the body reacts.</para>
        ///
        /// <para>A body accelerating also pitches: the legs push at the feet while the mass is higher up,
        /// so speeding up lifts the nose and braking dips it.</para>
        ///
        /// <para>Pitch is positive nose up, roll positive right side up, both in degrees.</para>
        /// </remarks>
        private void StepTilt(float deltaTime)
        {
            if (_suspended || _legs.Count == 0)
            {
                _tilt = Vector2.zero;
                _tiltRate = Vector2.zero;
                return;
            }

            deltaTime = Mathf.Min(deltaTime, 1f / 30f);
            if (deltaTime <= 0f) return;

            // Compression of every planted leg, and where its hip sits relative to the body's centre.
            float meanCompression = 0f;
            int supporting = 0;
            _supportBuffer.Clear();

            foreach (ProceduralLeg leg in _legs)
            {
                if (leg.IsStepping || !leg.HasGroundContact) continue;

                Vector3 local = transform.InverseTransformPoint(leg.HipPosition);
                float height = leg.HipPosition.y - leg.FootPosition.y;
                float compression = Mathf.Clamp(leg.Spec.RideHeight - height, -0.5f, 0.5f);

                _supportBuffer.Add(new Vector3(local.x, local.z, compression));
                meanCompression += compression;
                supporting++;
            }

            Vector2 acceleration = Vector2.zero;

            if (supporting > 0)
            {
                meanCompression /= supporting;

                float sumX = 0f, sumZ = 0f, torqueRoll = 0f, torquePitch = 0f;
                foreach (Vector3 support in _supportBuffer)
                {
                    float push = support.z - meanCompression; // the share beyond what the legs carry on average
                    torqueRoll += push * support.x;
                    torquePitch += push * support.y;
                    sumX += support.x * support.x;
                    sumZ += support.y * support.y;
                }

                // Normalised by the lever arms, so the natural frequency is the same for any number of legs.
                float stiffness = _tiltStiffness;
                float pitchAcceleration = stiffness * torquePitch / Mathf.Max(0.04f, sumZ);
                float rollAcceleration = stiffness * torqueRoll / Mathf.Max(0.04f, sumX);

                // Radians per second squared -> degrees.
                acceleration = new Vector2(pitchAcceleration, rollAcceleration) * Mathf.Rad2Deg;
            }

            // The soles: a foot is a patch, not a point, so the ground under it turns the body toward its own
            // slope. A creature whose legs all sit at one distance along its length (a single pair) has no lever
            // arm for the springs above in that direction, and this is what pitches it on a slope.
            Vector3 groundNormal = Vector3.zero;
            int soles = 0;
            foreach (ProceduralLeg leg in _legs)
            {
                if (leg.IsStepping || !leg.PlannerHasGround) continue;
                groundNormal += leg.GroundNormal;
                soles++;
            }

            if (soles > 0)
            {
                Vector3 local = transform.InverseTransformDirection(groundNormal.normalized);
                float wantedPitch = -Mathf.Atan2(local.z, local.y) * Mathf.Rad2Deg;
                float wantedRoll = Mathf.Asin(Mathf.Clamp(-local.x, -1f, 1f)) * Mathf.Rad2Deg;
                acceleration.x += _soleStiffness * (Mathf.Clamp(wantedPitch, -_maxPitch, _maxPitch) - _tilt.x);
                acceleration.y += _soleStiffness * (Mathf.Clamp(wantedRoll, -_maxRoll, _maxRoll) - _tilt.y);
            }

            // Accelerating pitches the body about its feet.
            Vector3 position = transform.position;
            Vector3 velocity = (position - _tiltLastPosition) / deltaTime;
            Vector3 bodyAcceleration = (velocity - _tiltLastVelocity) / deltaTime;
            _tiltLastPosition = position;
            _tiltLastVelocity = velocity;

            Vector3 localAcceleration = transform.InverseTransformDirection(bodyAcceleration);
            localAcceleration.y = 0f;
            float lever = Mathf.Max(0.2f, _standHeight) * _inertiaCoupling;
            acceleration.x += Mathf.Clamp(localAcceleration.z, -15f, 15f) * lever * Mathf.Rad2Deg;
            acceleration.y += Mathf.Clamp(-localAcceleration.x, -15f, 15f) * lever * Mathf.Rad2Deg;

            // Damping and a soft limit, so a freak contact cannot spin the body over.
            float omega = Mathf.Sqrt(_tiltStiffness);
            float damping = 2f * _tiltDamping * omega;
            acceleration -= _tiltRate * damping;
            acceleration.x -= SoftLimit(_tilt.x, _maxPitch) * _tiltStiffness * Mathf.Rad2Deg * 0.1f;
            acceleration.y -= SoftLimit(_tilt.y, _maxRoll) * _tiltStiffness * Mathf.Rad2Deg * 0.1f;

            _tiltRate += acceleration * deltaTime;
            _tilt += _tiltRate * deltaTime;
            _tilt.x = Mathf.Clamp(_tilt.x, -_maxPitch * 1.3f, _maxPitch * 1.3f);
            _tilt.y = Mathf.Clamp(_tilt.y, -_maxRoll * 1.3f, _maxRoll * 1.3f);
        }

        /// <summary>How far an angle is past its limit, in radians, 0 inside it.</summary>
        private static float SoftLimit(float degrees, float limit)
            => Mathf.Sign(degrees) * Mathf.Max(0f, Mathf.Abs(degrees) - limit) * Mathf.Deg2Rad;

        private void LateUpdate()
        {
            // The lean is applied even when there are no legs and when the gait is frozen — it then
            // simply returns to upright.
            ApplyLean();

            if (_suspended || _legs.Count == 0) return;

            Vector3 position = transform.position;
            Vector3 travel = position - _lastPosition;
            _lastPosition = position;

            float yaw = transform.eulerAngles.y;
            float yawDelta = Mathf.DeltaAngle(_lastYaw, yaw);
            _lastYaw = yaw;

            // In the air the legs keep their pose relative to the body: no stepping, no reaching for a
            // ground that is not there. The bookkeeping above still runs, so landing does not count the
            // whole flight as one giant step.
            bool supported = !CheckAirborne(position);
            _unsupportedSeconds = supported ? 0 : _unsupportedSeconds + Time.deltaTime;
            // A crest can put the torso over a hollow while the feet still support it.
            IsAirborne = !supported && _unsupportedSeconds > 0.12f;
            if (IsAirborne)
            {
                foreach (ProceduralLeg leg in _legs) leg.Hold();
                return;
            }

            // The step planner: each foot stays where it was planted and steps when the body has carried
            // it too far. Whatever moves the body - walking, a slide down a slope, a shove, a turn on the
            // spot - therefore makes the legs react. Nothing here is driven by a walking phase.
            Vector3 velocity = travel / Mathf.Max(Time.deltaTime, 1e-4f);
            float yawRate = yawDelta * Mathf.Deg2Rad / Mathf.Max(Time.deltaTime, 1e-4f);
            float flatSpeed = new Vector2(velocity.x, velocity.z).magnitude;
            IsMoving = flatSpeed > 0.4f;

            // The legs that need a step most go first, and only a share of them may be in the air at once -
            // that is what makes the feet alternate instead of all lifting together.
            _order.Clear();
            int airborneLegs = 0;
            foreach (ProceduralLeg leg in _legs)
            {
                _order.Add(leg);
                if (leg.IsStepping) airborneLegs++;
            }
            _order.Sort((a, b) => b.StepNeed.CompareTo(a.StepNeed));

            int allowed = Mathf.Max(1, Mathf.CeilToInt(_legs.Count * (flatSpeed > 4f ? 0.6f : 0.5f)));
            if (_legs.Count == 2 && flatSpeed > 6f) allowed = 2;

            foreach (ProceduralLeg leg in _order)
            {
                bool mayStep = leg.IsStepping || (airborneLegs < allowed && !OppositeStepping(leg));
                bool wasStepping = leg.IsStepping;
                leg.Plan(velocity, yawRate, position, transform.forward, _groundMask, mayStep, Time.deltaTime);
                if (!wasStepping && leg.IsStepping) airborneLegs++;
                else if (wasStepping && !leg.IsStepping) airborneLegs--;
            }

            _phase += flatSpeed * Time.deltaTime;
        }

        /// <summary>Whether a leg half a cycle away from this one is in the air - lifting both would leave the body unsupported on that side.</summary>
        private bool OppositeStepping(ProceduralLeg leg)
        {
            foreach (ProceduralLeg other in _legs)
            {
                if (other == leg || !other.IsStepping) continue;
                float apart = Mathf.Abs(Mathf.Repeat(leg.PhaseOffset - other.PhaseOffset + 0.5f, 1f) - 0.5f);
                if (apart > 0.3f) return true;
            }

            return false;
        }

        private bool CheckAirborne(Vector3 position)
        {
            if (_standHeight <= 0.001f) return false;

            float reach = _standHeight + _airborneMargin;
            const float lift = 0.05f;
            if (Physics.Raycast(position + Vector3.up * lift, Vector3.down, reach + lift, _groundMask, QueryTriggerInteraction.Ignore)) return false;

            // Over a hollow the torso can have nothing under it while the feet still stand on the crests either side.
            foreach (ProceduralLeg leg in _legs)
                if (leg.HasGroundWithinReach(_groundMask, _airborneMargin)) return false;

            return true;
        }

        /// <summary>
        /// The stride length matched to the current speed.
        /// </summary>
        /// <remarks>
        /// <para>Faster means a <b>longer step</b>, not a more frequent one. Previously the stride was
        /// fixed and followed purely from the leg's reach, so the frequency grew directly with speed — a
        /// stubby leg with a reach of 0.32 m at 6.3 m/s worked out to over forty cycles per second, and
        /// instead of steps there was a vibration on the spot.</para>
        ///
        /// <para>The upper bound is geometry, not taste: <see cref="LegSpec.MaxStride"/> is the longest
        /// stride at which the foot still reaches the ground. Beyond it the leg will not take a longer
        /// step — it will take more steps per second, and that is the signal that the creature is faster
        /// than its legs can carry it.</para>
        /// </remarks>
        private float Stride(float speed)
        {
            LegSpec spec = _legs[0].Spec;
            GaitProfile gait = _legs[0].Gait;

            float longest = spec.MaxStride > 0.001f ? spec.MaxStride : gait.StepLength;
            float shortest = Mathf.Min(gait.StepLength, longest);

            return Mathf.Clamp(speed / gait.TargetCadence, shortest, Mathf.Max(0.05f, longest));
        }

        /// <summary>
        /// How fast a foot pulls towards its target — never slower than the step itself lasts.
        /// </summary>
        /// <remarks>
        /// The pull is a low-pass filter, so a fixed value ate the step cycle as soon as the cadence
        /// exceeded its cutoff frequency: at a setting of 18/s anything above ~3 steps per second came
        /// out attenuated by more than a factor of ten, and a full stride was reduced to a centimetre of
        /// wobble. So the threshold follows the cadence, and the inspector slider stays as a lower bound.
        /// </remarks>
        private float Responsiveness(float speed, float stride)
        {
            float cadence = speed / Mathf.Max(0.01f, stride);

            return Mathf.Max(_footResponsiveness, cadence * SettleStepsPerCycle);
        }

        /// <summary>How many times faster than the cadence a foot should settle for the step to show at full amplitude.</summary>
        private const float SettleStepsPerCycle = 8f;

        /// <summary>The mean radius at which the hips sit around the creature's axis of rotation.</summary>
        private float MeanHipRadius(Vector3 center)
        {
            float total = 0f;

            foreach (ProceduralLeg leg in _legs)
            {
                Vector3 offset = leg.HipPosition - center;
                offset.y = 0f;
                total += offset.magnitude;
            }

            return total / _legs.Count;
        }

        private void ClearLegs() => CreatureLegFactory.Dispose(_legs);

        private void OnDestroy() => ClearLegs();
    }
}
