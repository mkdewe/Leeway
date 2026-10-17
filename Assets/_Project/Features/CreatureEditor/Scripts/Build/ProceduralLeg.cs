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
    /// <para><b>A prefab is one link — unless it says it is a whole limb.</b> Because the further links
    /// are copies of it, a prefab holding a complete authored leg — thigh, shank and foot in one model
    /// — would come out drawn once per link: the same leg twice over, with what reads as a knee exactly
    /// where the second copy starts. Such a part ticks <c>WholeLimb</c>, and then the chain does the
    /// opposite of copying: it <b>skins that one model across its links</b> (see
    /// <see cref="SkinnedLimb"/>), so the leg keeps a knee and there is only ever one of it. Parts
    /// authored as a single link of a longer limb — a tentacle joint, a fin ray — leave it off and are
    /// repeated as before.</para>
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

        /// <summary>Reused for carrying a renderer's tint to a cloned link — a block per clone would be pure garbage.</summary>
        private static MaterialPropertyBlock _propertyBlock;

        /// <summary>The fraction of the reach the foot may not escape past — beyond it the leg would only splay out.</summary>
        private const float MaxReachFactor = 0.995f;

        /// <summary>The shortest a link may be squashed to, as a fraction of its authored length.</summary>
        private const float MinStretch = 0.05f;

        private readonly Vector3[] _joints;
        private readonly Transform[] _segments;
        private readonly AdoptedVisual[] _adopted;
        private readonly LegSpec _spec;
        private readonly GaitProfile _gait;
        private readonly Transform _hip;
        private readonly float _phaseOffset;

        /// <summary>How far along <c>+Z</c> the part's authored visuals reach, in hip space.</summary>
        private readonly float _naturalLength;

        /// <summary>The length of each link the solver works with, in world metres. They need not be equal.</summary>
        private readonly float[] _lengths;

        /// <summary>An unscaled twin of each link, for physics to hold on to. See <see cref="MuscleTargets"/>.</summary>
        private readonly Transform[] _anchors;

        /// <summary>The authored length each link stands for, in hip space — what its stretch is measured against.</summary>
        private readonly float[] _naturals;

        /// <summary>The object holding the part's authored visuals. It is link 0 itself unless the limb is skinned.</summary>
        private readonly Transform _visuals;

        /// <summary>The skinned model, when the part is a whole limb. Undone when the leg is taken apart.</summary>
        private readonly LimbBinding _binding;

        /// <summary>
        /// Which way the knee bends, in the part's own space. Zero when the model does not say — the
        /// direction of travel decides then, as it always used to.
        /// </summary>
        private readonly Vector3 _bendHintLocal;

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

        /// <summary>The part instance the leg grows out of — the puppet hangs its leg muscles off the same bone.</summary>
        public Transform Hip => _hip;

        /// <summary>
        /// The links, hip to foot. These are the transforms the solver moves every frame.
        /// </summary>
        public IReadOnlyList<Transform> Segments => _segments;

        /// <summary>
        /// One unscaled transform per link, in the same order — what physics may be attached to.
        /// </summary>
        /// <remarks>
        /// <para><b>Never hang a muscle off a link itself.</b> A link is <b>stretched</b> along its own
        /// axis every frame, and a joint driven from a non-uniformly scaled transform is driven from a
        /// matrix that no longer describes a rotation. PuppetMaster pulls the joint's target rotation
        /// out of exactly that matrix; when the stretch approaches zero it comes out as
        /// <c>NaN</c>, Unity reports "Invalid quaternion rotation", and the joint then throws the whole
        /// ragdoll across the level — with the camera left watching where the creature used to be.</para>
        ///
        /// <para>So the anchors carry the link's <b>pose</b> and nothing else: same position, same
        /// rotation, scale always one. They cost one transform per link and they are the only thing the
        /// physical body is allowed to see.</para>
        /// </remarks>
        public IReadOnlyList<Transform> MuscleTargets => _anchors;

        /// <param name="wholeLimb">
        /// Whether the part's model is the complete leg. Then the chain skins that one model over its
        /// links instead of repeating it — see the class remarks.
        /// </param>
        public ProceduralLeg(LegSpec spec, Transform hip, float phaseOffset, int geneIndex = -1, bool wholeLimb = false)
        {
            _spec = spec;
            _gait = spec.Gait;
            _hip = hip;
            _phaseOffset = phaseOffset;
            GeneIndex = geneIndex;

            _joints = new Vector3[spec.JointCount];
            _segments = new Transform[spec.SegmentCount];
            _lengths = new float[spec.SegmentCount];
            _naturals = new float[spec.SegmentCount];

            bool skin = wholeLimb && spec.SegmentCount == 2;

            _adopted = AdoptVisuals(hip, skin ? VisualName : SegmentName(0), out _visuals);

            // A part with no mesh (or one reaching only sideways) cannot be measured — stretching is
            // then off and the link runs at its authored length.
            float measured = MeasureLength(_visuals);
            _naturalLength = measured > Epsilon ? measured : spec.SegmentLength;

            if (skin) BuildSkinnedChain(hip, measured, out _binding, out _bendHintLocal);
            else BuildRepeatedChain(hip);

            _anchors = new Transform[spec.SegmentCount];
            for (int i = 0; i < _anchors.Length; i++)
            {
                var anchor = new GameObject(AnchorName(i)).transform;
                anchor.SetParent(hip, false);

                _anchors[i] = anchor;
            }
        }

        /// <summary>
        /// The ordinary chain: link 0 <b>is</b> the part's model and every further link is a copy of it.
        /// Right for a part authored as one link of a limb, which is what most parts are.
        /// </summary>
        private void BuildRepeatedChain(Transform hip)
        {
            _segments[0] = _visuals;

            for (int i = 1; i < _segments.Length; i++)
                _segments[i] = CloneSegment(_visuals, hip, i);

            for (int i = 0; i < _segments.Length; i++)
            {
                _lengths[i] = _spec.SegmentLength;
                _naturals[i] = _naturalLength;
            }
        }

        /// <summary>
        /// The chain for a whole authored limb: two bare links, with the single model skinned across
        /// them and the joint placed where the model itself bends.
        /// </summary>
        /// <remarks>
        /// <para>The links are <b>not</b> equal: the thigh runs from the hip to the knee the artist
        /// drew, the shank from there to the foot. Splitting the chain anywhere else would crease the
        /// mesh in a place that is not a joint.</para>
        ///
        /// <para>The bones start at rest — strung along <c>+Z</c>, unrotated — because that pose is what
        /// <see cref="SkinnedLimb"/> binds the mesh against.</para>
        /// </remarks>
        private void BuildSkinnedChain(Transform hip, float measured, out LimbBinding binding, out Vector3 bendHint)
        {
            LimbKnee knee = SkinnedLimb.FindKnee(_visuals, measured);
            float fraction = Mathf.Clamp(knee.Fraction, MinKneeFraction, MaxKneeFraction);

            _naturals[0] = measured * fraction;
            _naturals[1] = measured * (1f - fraction);
            _lengths[0] = _spec.Reach * fraction;
            _lengths[1] = _spec.Reach * (1f - fraction);

            for (int i = 0; i < _segments.Length; i++)
            {
                var bone = new GameObject(SegmentName(i)).transform;
                bone.SetParent(hip, false);
                bone.localPosition = new Vector3(0f, 0f, i == 0 ? 0f : _naturals[0]);
                bone.localRotation = Quaternion.identity;

                _segments[i] = bone;
            }

            binding = SkinnedLimb.Skin(_visuals, _segments, _naturals[0], measured * SkinnedLimb.DefaultBlendBand);

            // A model that turned out to be unreadable cannot be skinned; the leg then stays rigid
            // rather than disappearing, and the links still hold it at the right length.
            if (!binding.IsBound) _visuals.SetParent(_segments[0], true);

            bendHint = knee.Found ? knee.BendDirection : Vector3.zero;
        }

        /// <summary>How far along the limb the knee may sit. A joint outside this range is a mismeasurement, not a leg.</summary>
        private const float MinKneeFraction = 0.25f;
        private const float MaxKneeFraction = 0.75f;

        private static string SegmentName(int index) => $"LegSegment_{index}";

        /// <summary>The name of a link's unscaled twin — see <see cref="MuscleTargets"/>.</summary>
        private static string AnchorName(int index) => $"LegAnchor_{index}";

        /// <summary>The object the part's own model hangs from once the chain has taken it over.</summary>
        private const string VisualName = "LegVisual";

        /// <summary>
        /// Moves the part's visuals under an object the chain owns. Deliberately a takeover rather than
        /// a copy — otherwise the attached leg would stay on top as a second, motionless shape beside
        /// the one the solver moves.
        /// </summary>
        private static AdoptedVisual[] AdoptVisuals(Transform hip, string name, out Transform holder)
        {
            int count = hip.childCount;
            var children = new Transform[count];
            for (int i = 0; i < count; i++) children[i] = hip.GetChild(i);

            holder = new GameObject(name).transform;
            holder.SetParent(hip, false);

            var adopted = new AdoptedVisual[count];
            for (int i = 0; i < count; i++)
            {
                adopted[i] = new AdoptedVisual(children[i]);
                children[i].SetParent(holder, false);
            }

            return adopted;
        }

        private static Transform CloneSegment(Transform template, Transform hip, int index)
        {
            GameObject clone = Object.Instantiate(template.gameObject, hip);
            clone.name = SegmentName(index);
            CopyPropertyBlocks(template, clone.transform);
            ShareSkin(template, clone.transform);

            return clone.transform;
        }

        /// <summary>
        /// Makes a cloned link wear the same skin as the link it was copied from.
        /// </summary>
        /// <remarks>
        /// <para>A painted part carries Paint in 3D components and a material of its own. Cloning them
        /// gives every link its own canvas and its own material — a leg with three links paints in three
        /// places and costs three render textures, and painting the leg leaves two of them untouched.</para>
        ///
        /// <para>So the copies are stripped of the painting machinery and pointed at the template's
        /// material instead. One leg, one skin: paint it once and the whole leg wears it.</para>
        /// </remarks>
        private static void ShareSkin(Transform template, Transform clone)
        {
            foreach (Component component in clone.GetComponentsInChildren<Component>(true))
            {
                if (component == null) continue;

                string type = component.GetType().Name;
                if (type == "P3dPaintable" || type == "P3dPaintableTexture" || type == "P3dMaterialCloner")
                    Object.Destroy(component);
            }

            Renderer[] from = template.GetComponentsInChildren<Renderer>(true);
            Renderer[] to = clone.GetComponentsInChildren<Renderer>(true);
            if (from.Length != to.Length) return;

            for (int i = 0; i < from.Length; i++)
                to[i].sharedMaterials = from[i].sharedMaterials;
        }

        /// <summary>
        /// Carries the per-renderer property blocks over to the copy.
        /// </summary>
        /// <remarks>
        /// <para>A part is painted in the creature's colour through a <c>MaterialPropertyBlock</c>
        /// (<c>CreaturePartInstantiator.Tint</c>), and <c>Instantiate</c> does not copy that block.
        /// Without this the first link — which <b>takes over</b> the original objects and keeps their
        /// block — would wear the creature's colour while every cloned link walked around in whatever
        /// colour the model was authored in.</para>
        ///
        /// <para>The clone is a copy of the template's hierarchy, so the two renderer lists come out in
        /// the same order and can be walked in step. Should they ever not, we leave the copy alone
        /// rather than paint the wrong renderer.</para>
        /// </remarks>
        private static void CopyPropertyBlocks(Transform template, Transform clone)
        {
            Renderer[] from = template.GetComponentsInChildren<Renderer>(true);
            Renderer[] to = clone.GetComponentsInChildren<Renderer>(true);
            if (from.Length != to.Length) return;

            _propertyBlock ??= new MaterialPropertyBlock();

            for (int i = 0; i < from.Length; i++)
            {
                from[i].GetPropertyBlock(_propertyBlock);
                to[i].SetPropertyBlock(_propertyBlock);
            }
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

            // We push the knee somewhere deliberate — otherwise FABRIK bends the chain in a random
            // direction.
            LimbIk.Solve(_joints, _lengths, _footTarget, BendHint(forward));

            ApplyToSegments();
        }

        /// <summary>
        /// Which way the knee is pushed.
        /// </summary>
        /// <remarks>
        /// A model drawn with a knee says for itself which way that knee goes — a hind leg bends
        /// backwards whichever way the creature happens to be running, and bending it towards the
        /// direction of travel would turn the mesh inside out. Only a model that says nothing falls
        /// back on the direction of travel.
        /// </remarks>
        private Vector3 BendHint(Vector3 forward)
            => _bendHintLocal.sqrMagnitude > Epsilon ? _hip.TransformDirection(_bendHintLocal) : forward;

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
            LimbIk.Solve(_joints, _lengths, _footTarget, BendHint(forward));

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
            float hipScale = Mathf.Abs(_hip.lossyScale.z);

            for (int i = 0; i < _segments.Length; i++)
            {
                Transform segment = _segments[i];
                if (segment == null) continue;

                // The authored length is in hip space while the joints are in world space — the part's
                // scale (the scale gene) is what links the two. Each link carries its own, because a
                // skinned limb's thigh and shank are not the same length.
                float natural = Mathf.Max(Epsilon, _naturals[i] * hipScale);

                Vector3 from = _joints[i];
                Vector3 to = _joints[i + 1];

                Vector3 axis = to - from;
                float length = axis.magnitude;
                if (length < Epsilon) continue;

                Vector3 direction = axis / length;

                Vector3 localPosition = _hip.InverseTransformPoint(from);
                Quaternion localRotation = inverseHip * Quaternion.LookRotation(direction, RollReference(direction));

                segment.localPosition = localPosition;
                segment.localRotation = localRotation;

                // Never let the stretch reach zero. A link squashed to nothing is a transform whose
                // matrix has no rotation left in it, and everything downstream that reads a rotation
                // from it — physics above all — gets a NaN for its trouble.
                segment.localScale = new Vector3(1f, 1f, Mathf.Max(MinStretch, length / natural));

                // The anchor takes the pose and leaves the stretch behind, so a muscle hanging off it
                // is driven by a rotation rather than by a squashed matrix.
                Transform anchor = _anchors != null && i < _anchors.Length ? _anchors[i] : null;
                if (anchor == null) continue;

                anchor.localPosition = localPosition;
                anchor.localRotation = localRotation;
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
            // The skinned copy goes first and the part's own renderers come back on: the model belongs
            // to the attached part, and the chain only borrowed it.
            _binding.Undo();

            // The visuals go back under the hip before the links are destroyed. If they went with them,
            // switching locomotion off would permanently delete the leg attached in the editor — these
            // are the same objects, not copies of them.
            if (_adopted != null)
            {
                for (int i = 0; i < _adopted.Length; i++)
                    _adopted[i].Restore(_hip);
            }

            for (int i = 0; i < _segments.Length; i++)
                Destroy(_segments[i]);

            if (_anchors != null)
            {
                for (int i = 0; i < _anchors.Length; i++) Destroy(_anchors[i]);
            }

            // The holder is one of the links in an ordinary chain, and its own object in a skinned one.
            if (_visuals != null && System.Array.IndexOf(_segments, _visuals) < 0) Destroy(_visuals);
        }

        private static void Destroy(Transform transform)
        {
            if (transform == null) return;

            if (Application.isPlaying) Object.Destroy(transform.gameObject);
            else Object.DestroyImmediate(transform.gameObject);
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
