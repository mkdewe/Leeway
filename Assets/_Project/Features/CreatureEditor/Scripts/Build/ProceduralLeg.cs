using System.Collections.Generic;
using Leeway.Creature.Domain;
using UnityEngine;

namespace Leeway.CreatureEditor
{
    /// <summary>
    /// One leg: a chain of links solved by IK, with its own step phase.
    /// </summary>
    /// <remarks>
    /// <para><b>The leg is the very part the player attached in the editor</b> — not a separate shape
    /// fabricated at runtime. The links are made from the part prefab's authored visuals: the first
    /// link <b>takes over</b> the original objects, the rest are copies of them. That way a slender leg
    /// walks as a slender one, a hoofed one as hoofed, and what you see in the editor is what runs
    /// around the world. The number of links still follows from the bend points in the catalog — only a
    /// chain can actually bend.</para>
    ///
    /// <para><b>The links hang under the hip</b>, i.e. under the part instance attached to a bone. The
    /// solver computes the joints in world space but stores them in hip space, so the leg is welded to
    /// the carcass: a rotation of the torso carries it along even between solver steps, instead of
    /// leaving it behind in world space.</para>
    ///
    /// <para><b>A part's axis is <c>+Z</c></b>, just as in the rest of the body build —
    /// <c>PartOrientation.LookOutward</c> composes the rotation through <c>Quaternion.LookRotation</c>,
    /// so it is <c>+Z</c> that aims away from the body, and for a locomotion part, at the ground. A leg
    /// prefab modelled along a different axis will stick out sideways in the editor already, before this
    /// class touches it at all.</para>
    ///
    /// <para>This is <b>purely visual</b>. The creature's movement is done by the predicted locomotion
    /// capsule, and the legs only dress it — which is why none of this has to be deterministic or
    /// synchronised over the network.</para>
    /// </remarks>
    public class ProceduralLeg
    {
        private const float Epsilon = 1e-5f;

        /// <summary>The fraction of the reach the foot may not escape past — beyond it the leg would only splay out.</summary>
        private const float MaxReachFactor = 0.995f;

        private readonly Vector3[] _joints;
        private readonly Transform[] _segments;
        private readonly AdoptedVisual[] _adopted;
        private readonly LegSpec _spec;
        private readonly GaitProfile _gait;
        private readonly Transform _hip;
        private readonly float _phaseOffset;

        /// <summary>How far along <c>+Z</c> the part's authored visuals reach, in hip space.</summary>
        private readonly float _naturalLength;

        private Vector3 _footTarget;
        private bool _initialised;

        public LegSpec Spec => _spec;
        public GaitProfile Gait => _gait;
        public Vector3 FootPosition => _joints[^1];

        /// <summary>The gene of the part this leg grew from — the editor addresses reshaping by it.</summary>
        public int GeneIndex { get; }

        /// <summary>The joints in world space, from hip to foot. The editor's handles sit exactly on them.</summary>
        public IReadOnlyList<Vector3> Joints => _joints;

        /// <summary>The leg's attachment point in world space.</summary>
        public Vector3 HipPosition => _hip != null ? _hip.position : Vector3.zero;

        public ProceduralLeg(LegSpec spec, Transform hip, float phaseOffset, int geneIndex = -1)
        {
            _spec = spec;
            _gait = spec.Gait;
            _hip = hip;
            _phaseOffset = phaseOffset;
            GeneIndex = geneIndex;

            _joints = new Vector3[spec.JointCount];
            _segments = new Transform[spec.SegmentCount];

            _adopted = AdoptVisuals(hip, out Transform first);
            _segments[0] = first;

            // A part with no mesh (or one reaching only sideways) cannot be measured — stretching is
            // then off and the link runs at its authored length.
            float measured = MeasureLength(first);
            _naturalLength = measured > Epsilon ? measured : spec.SegmentLength;

            for (int i = 1; i < _segments.Length; i++)
                _segments[i] = CloneSegment(first, hip, i);
        }

        private static string SegmentName(int index) => $"LegSegment_{index}";

        /// <summary>
        /// Moves the part's visuals under the chain's first link. Deliberately a takeover rather than a
        /// copy — otherwise the attached leg would stay on top as a second, motionless shape beside the
        /// one the solver moves.
        /// </summary>
        private static AdoptedVisual[] AdoptVisuals(Transform hip, out Transform segment)
        {
            int count = hip.childCount;
            var children = new Transform[count];
            for (int i = 0; i < count; i++) children[i] = hip.GetChild(i);

            segment = new GameObject(SegmentName(0)).transform;
            segment.SetParent(hip, false);

            var adopted = new AdoptedVisual[count];
            for (int i = 0; i < count; i++)
            {
                adopted[i] = new AdoptedVisual(children[i]);
                children[i].SetParent(segment, false);
            }

            return adopted;
        }

        private static Transform CloneSegment(Transform template, Transform hip, int index)
        {
            GameObject clone = Object.Instantiate(template.gameObject, hip);
            clone.name = SegmentName(index);
            return clone.transform;
        }

        /// <summary>
        /// Measures how far along the part's axis its geometry reaches. This is the authored length the
        /// link's stretching is measured against.
        /// </summary>
        /// <remarks>
        /// <para>We measure along <b>+Z</b>, because that is the axis <c>PartOrientation.LookOutward</c>
        /// points away from the body — for a locomotion part, straight at the ground. The rest of the
        /// build holds the same convention, so the leg has to as well.</para>
        ///
        /// <para>We take all eight corners of each mesh rather than <c>bounds.max</c> alone: a part
        /// rotated relative to the link has its bounds computed in its own frame, and reading the
        /// extreme value flat would lie about its length.</para>
        /// </remarks>
        private static float MeasureLength(Transform segment)
        {
            float deepest = 0f;

            foreach (MeshFilter filter in segment.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh mesh = filter.sharedMesh;
                if (mesh == null) continue;

                Bounds bounds = mesh.bounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    var point = new Vector3(
                        (corner & 1) == 0 ? bounds.min.x : bounds.max.x,
                        (corner & 2) == 0 ? bounds.min.y : bounds.max.y,
                        (corner & 4) == 0 ? bounds.min.z : bounds.max.z);

                    Vector3 local = segment.InverseTransformPoint(filter.transform.TransformPoint(point));
                    deepest = Mathf.Max(deepest, local.z);
                }
            }

            return deepest;
        }

        /// <summary>
        /// Advances the leg by one simulation step.
        /// </summary>
        /// <param name="phase">The whole creature's shared gait phase.</param>
        /// <param name="forward">The direction of travel in world space.</param>
        /// <param name="stride">The stride length matched to the current speed.</param>
        /// <param name="groundMask">The layers the foot may stand on.</param>
        /// <param name="responsiveness">How fast the foot pulls towards its target.</param>
        public void Tick(float phase, Vector3 forward, float stride, LayerMask groundMask, float responsiveness, float deltaTime)
        {
            if (_hip == null) return;

            Vector3 hip = _hip.position;
            Vector2 offset = _gait.FootOffset(phase + _phaseOffset, stride);

            // The foot's rest point: under the hip, at the leg's working height.
            Vector3 neutral = hip + Vector3.down * _spec.RideHeight;
            Vector3 desired = neutral + forward * offset.x;

            desired = SnapToGround(desired, groundMask) + Vector3.up * offset.y;

            // Pulling rather than jumping: on a change of pace or a turn the foot moves to its new place
            // smoothly instead of teleporting.
            _footTarget = _initialised
                ? Vector3.Lerp(_footTarget, desired, 1f - Mathf.Exp(-responsiveness * deltaTime))
                : desired;

            _initialised = true;

            // A hard reach limit. Without it the pull falls behind the turning carcass and the leg splays
            // out into a straight line pointing at the foot's old place.
            _footTarget = ClampToReach(hip, _footTarget);

            _joints[0] = hip;

            // We push the knee forwards — otherwise FABRIK would bend the chain in a random direction.
            LimbIk.Solve(_joints, _spec.SegmentLength, _footTarget, forward);

            ApplyToSegments();
        }

        /// <summary>
        /// Puts the leg in a resting pose: the foot straight under the hip at its working height, the
        /// knee pushed in the given direction.
        /// </summary>
        /// <remarks>
        /// <para>It is the same pose <see cref="Tick"/> gives a standing creature, only computed
        /// <b>without</b> probing the ground and without the pull — the sculpted preview hangs above the
        /// editor pad rather than standing on it, and what matters is what the leg looks like, not what
        /// it happens to be touching.</para>
        ///
        /// <para>Without this the editor showed the part's authored link while the playground unfolded it
        /// into a chain: you attached a capsule and a bending leg ran around.</para>
        /// </remarks>
        /// <param name="forward">The direction we push the knees in — the creature's front.</param>
        public void Rest(Vector3 forward)
        {
            if (_hip == null) return;

            Vector3 hip = _hip.position;

            _footTarget = ClampToReach(hip, hip + Vector3.down * _spec.RideHeight);
            _initialised = true;

            _joints[0] = hip;
            LimbIk.Solve(_joints, _spec.SegmentLength, _footTarget, forward);

            ApplyToSegments();
        }

        private Vector3 ClampToReach(Vector3 hip, Vector3 foot)
        {
            Vector3 offset = foot - hip;
            float max = _spec.Reach * MaxReachFactor;

            return offset.sqrMagnitude > max * max ? hip + offset.normalized * max : foot;
        }

        private Vector3 SnapToGround(Vector3 position, LayerMask groundMask)
        {
            Vector3 origin = position + Vector3.up * _spec.Reach;
            float distance = _spec.Reach * 2f;

            return Physics.Raycast(origin, Vector3.down, out RaycastHit hit, distance, groundMask, QueryTriggerInteraction.Ignore)
                ? hit.point
                : position;
        }

        /// <summary>
        /// Lays the visible links out between the computed joints.
        /// </summary>
        /// <remarks>
        /// <para>The write happens in hip space, not world space — that is the entire "welding"
        /// mechanism: a link stays a child of the bone, so it is carried by every rotation of the
        /// carcass, including one that happens after this computation.</para>
        ///
        /// <para>A link aims along the chain with its local <c>+Z</c> and stretches <b>only</b> along it.
        /// That is not this class's choice: <c>PartOrientation.LookOutward</c> composes the rotation
        /// through <c>Quaternion.LookRotation</c>, so <c>+Z</c> is the "away from the body" axis for
        /// every part, and for a locomotion one the axis aimed at the ground. The cross-sectional
        /// dimensions stay as authored — otherwise a longer step would thicken the leg.</para>
        /// </remarks>
        private void ApplyToSegments()
        {
            Quaternion inverseHip = Quaternion.Inverse(_hip.rotation);

            // The authored length is in hip space while the joints are in world space — the part's scale
            // (the scale gene) is what links the two.
            float natural = Mathf.Max(Epsilon, _naturalLength * Mathf.Abs(_hip.lossyScale.z));

            for (int i = 0; i < _segments.Length; i++)
            {
                Transform segment = _segments[i];
                if (segment == null) continue;

                Vector3 from = _joints[i];
                Vector3 to = _joints[i + 1];

                Vector3 axis = to - from;
                float length = axis.magnitude;
                if (length < Epsilon) continue;

                Vector3 direction = axis / length;

                segment.localPosition = _hip.InverseTransformPoint(from);
                segment.localRotation = inverseHip * Quaternion.LookRotation(direction, RollReference(direction));
                segment.localScale = new Vector3(1f, 1f, length / natural);
            }
        }

        /// <summary>
        /// A link's "up", i.e. what fixes its roll about its own axis.
        /// </summary>
        /// <remarks>
        /// We take the hip's up so that a flat part (a fin, a hoof) keeps the same side along the whole
        /// chain instead of spinning randomly from frame to frame. When a link aims exactly along that
        /// axis, <c>LookRotation</c> has nothing to compose a rotation from — any other hip axis will do
        /// then.
        /// </remarks>
        private Vector3 RollReference(Vector3 direction)
        {
            Vector3 up = _hip.up;

            return Mathf.Abs(Vector3.Dot(direction, up)) > 0.99f ? _hip.right : up;
        }

        public void Dispose()
        {
            // The visuals go back under the hip before the links are destroyed. If they went with them,
            // switching locomotion off would permanently delete the leg attached in the editor — these
            // are the same objects, not copies of them.
            if (_adopted != null)
            {
                for (int i = 0; i < _adopted.Length; i++)
                    _adopted[i].Restore(_hip);
            }

            for (int i = 0; i < _segments.Length; i++)
            {
                if (_segments[i] == null) continue;

                if (Application.isPlaying) Object.Destroy(_segments[i].gameObject);
                else Object.DestroyImmediate(_segments[i].gameObject);
            }
        }

        /// <summary>A part's authored visuals together with the pose the chain found them in.</summary>
        private readonly struct AdoptedVisual
        {
            private readonly Transform _visual;
            private readonly Vector3 _localPosition;
            private readonly Quaternion _localRotation;
            private readonly Vector3 _localScale;

            public AdoptedVisual(Transform visual)
            {
                _visual = visual;
                _localPosition = visual.localPosition;
                _localRotation = visual.localRotation;
                _localScale = visual.localScale;
            }

            public void Restore(Transform hip)
            {
                if (_visual == null || hip == null) return;

                _visual.SetParent(hip, false);
                _visual.localPosition = _localPosition;
                _visual.localRotation = _localRotation;
                _visual.localScale = _localScale;
            }
        }
    }
}
