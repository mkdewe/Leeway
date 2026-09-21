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

        [Header("Lean")]
        [Tooltip("How many degrees per second the lean keeps up with the load. Lower = sluggish, higher = jerky.")]
        [SerializeField, Min(1f)] private float _leanResponsiveness = 220f;

        private readonly List<ProceduralLeg> _legs = new();
        private readonly List<Vector3> _hipPositions = new();

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

        private CreatureBody _body;
        private Vector3 _lastPosition;
        private float _lastYaw;
        private float _phase;

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
            armature.localRotation = Quaternion.Euler(0f, 0f, -_lean);
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

            Transform armature = _body.Built?.Armature;
            if (armature != null) armature.localRotation = Quaternion.identity;
        }

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

            // Vertical motion does not drive the step — a jump or a slide down a slope must not pace the legs.
            travel.y = 0f;

            float distance = travel.magnitude;
            Vector3 forward = distance > 1e-4f ? travel / distance : transform.forward;

            // The distance the hips actually travelled: the body's displacement plus the arc from the
            // turn. A hip close to the axis of rotation sweeps a shorter arc and rightly takes a smaller step.
            float stepDistance = distance + Mathf.Abs(yawDelta) * Mathf.Deg2Rad * MeanHipRadius(position);

            float speed = stepDistance / Mathf.Max(Time.deltaTime, 1e-4f);
            float stride = Stride(speed);

            // One shared phase for the whole creature; the legs are spread out by their own offsets.
            if (speed > _idleSpeed) _phase += stepDistance / stride;

            float responsiveness = Responsiveness(speed, stride);

            foreach (ProceduralLeg leg in _legs)
                leg.Tick(_phase, forward, stride, _groundMask, responsiveness, Time.deltaTime);
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
