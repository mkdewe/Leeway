using Leeway.Creature.Domain;
using UnityEngine;

namespace Leeway.CreatureEditor
{
    /// <summary>
    /// The axis the camera should turn the creature around: the centre of its <b>visible
    /// silhouette</b>, rather than the genome's anchor point.
    /// </summary>
    /// <remarks>
    /// <para>These are not the same place, and the difference is visible to the naked eye.
    /// <c>SpineAnchor</c> centres the <b>bones</b> — it takes the box of vertebra positions and puts
    /// its centre at the root. What is on screen is something else: skin of varying radius, attached
    /// parts sticking out past the spine (mouth and eyes at the front, a fin at the back) and legs
    /// reaching down. The centre of all that lies somewhere other than the centre of the bones
    /// alone.</para>
    ///
    /// <para>A camera orbiting the root therefore carries the creature around the screen: its centre
    /// traces a circle with a radius equal to that discrepancy, escaping left and then right instead of
    /// standing still. The starter creature has 0.19 m of discrepancy along the body against a
    /// half-frame height of 1.82 m — so over a full turn it sways by ~11% of half the screen, and the
    /// creature in the game by ~26%, because the camera stands closer. The longer the tail or the
    /// heavier the mouth, the worse it gets, which is where the impression that the camera "spins
    /// unevenly" comes from.</para>
    ///
    /// <para>The axis is computed <b>once per rebuild</b> and kept as a fixed local offset. Chasing the
    /// bounds every frame would tie the framing to the step cycle and the camera would breathe along
    /// with the legs.</para>
    /// </remarks>
    [DisallowMultipleComponent]
    public class CreatureFocusPoint : MonoBehaviour
    {
        private const string PivotName = "CameraFocus";

        private Transform _pivot;
        private bool _dirty;

        /// <summary>Whether the body is currently on the ground, driven by physics rather than by its legs.</summary>
        private bool _sprawled;

        private CreatureBody _body;
        private CreatureBodyPreview _preview;

        /// <summary>The point the camera should aim at. It always exists.</summary>
        public Transform Pivot => _pivot != null ? _pivot : CreatePivot();

        /// <summary>
        /// How far the silhouette reaches from <see cref="Pivot"/> — the sphere the camera has to stay
        /// outside of.
        /// </summary>
        /// <remarks>
        /// The distance to the <b>corner</b> of the measured box, not its half-width: the box turns
        /// with the creature, and a camera cleared only for the flat side would end up inside a leg
        /// the moment the creature showed it its corner. Measured once per rebuild, like the axis
        /// itself, so the value does not breathe with the step cycle.
        /// </remarks>
        public float SilhouetteRadius { get; private set; }

        private void OnEnable()
        {
            // It watches its own rebuild source, so adding it to a creature is a one-liner and needs
            // nothing wired up in the scene.
            TryGetComponent(out _body);
            TryGetComponent(out _preview);

            if (_body != null) _body.BodyRebuilt += OnRebuilt;
            if (_preview != null) _preview.Rebuilt += OnRebuilt;

            Invalidate();
        }

        private void OnDisable()
        {
            if (_body != null) _body.BodyRebuilt -= OnRebuilt;
            if (_preview != null) _preview.Rebuilt -= OnRebuilt;
        }

        private void OnRebuilt(CreatureGenome genome, CreatureStats stats)
        {
            Invalidate();

            // Outside play mode there is no frame loop to consume the request.
            if (!Application.isPlaying) Refocus();
        }

        /// <summary>
        /// Reports that the silhouette has changed. The recomputation happens in <c>LateUpdate</c>,
        /// because at the moment of the rebuild event the legs are already built but not yet placed —
        /// a box computed now would not include them at all.
        /// </summary>
        public void Invalidate() => _dirty = true;

        private void LateUpdate()
        {
            if (_dirty)
            {
                _dirty = false;
                Refocus();
            }

            TrackSprawl();
        }

        /// <summary>
        /// Follows the body while it is being thrown about by physics instead of standing on its legs.
        /// </summary>
        /// <remarks>
        /// <para><b>The fixed offset is right for a creature that walks</b> — see the class remarks:
        /// chasing the silhouette every frame would tie the framing to the step cycle. It is wrong for
        /// a creature that has been knocked over. Then the ragdoll drives the bones, the carcass slides
        /// and rolls away from the root, and an offset measured relative to the root leaves the camera
        /// watching the patch of ground the creature fell from while the creature lies somewhere else.
        /// To the player that reads as the camera having come off their creature, which is exactly what
        /// it has done.</para>
        ///
        /// <para>While it is down there is no gait to breathe with, so following costs nothing that the
        /// fixed offset was protecting. The <b>bones</b> are enough to say where the body is, and they
        /// are a handful of transform reads rather than a walk over every vertex.</para>
        ///
        /// <para>Standing up restores the resting offset, so the framing the player is used to comes
        /// back the moment the creature is back on its feet.</para>
        /// </remarks>
        private void TrackSprawl()
        {
            bool down = _body != null && _body.IsKnockedDown;

            if (!down)
            {
                if (!_sprawled) return;

                _sprawled = false;
                Refocus();
                return;
            }

            _sprawled = true;

            Transform[] bones = _body.Built?.Bones;
            if (bones == null || bones.Length == 0) return;

            var bounds = new Bounds(bones[0].position, Vector3.zero);
            for (int i = 1; i < bones.Length; i++)
                if (bones[i] != null) bounds.Encapsulate(bones[i].position);

            Pivot.position = bounds.center;
        }

        /// <summary>Moves the axis to the centre of the visible silhouette.</summary>
        public void Refocus()
        {
            Transform pivot = Pivot;

            if (!TryMeasure(out Bounds bounds))
            {
                pivot.localPosition = Vector3.zero;
                SilhouetteRadius = 0f;
                return;
            }

            pivot.position = bounds.center;
            SilhouetteRadius = bounds.extents.magnitude;
        }

        /// <summary>
        /// The silhouette's bounds: the carcass plus the attached parts.
        /// </summary>
        /// <remarks>
        /// <para>We deliberately do <b>not</b> collect renderers from the hierarchy. Under the root there
        /// are also the vertebra handle markers, which the editor shows and hides — the camera axis
        /// would then jump the moment the spine was revealed. So we ask the built body directly.</para>
        ///
        /// <para>The carcass is measured from <b>vertices</b>, not from <c>Mesh.bounds</c> or
        /// <c>Renderer.bounds</c>. The generator adds padding to the mesh box for skinning deformation
        /// and culling — the starter creature has 2.13 × 2.13 × 2.69 m there against a real
        /// 0.63 × 0.63 × 1.19 m. The padding leaves the centre in place, but the box swallows every
        /// attached part and the total would stop depending on what the player built.</para>
        /// </remarks>
        private bool TryMeasure(out Bounds bounds)
        {
            bounds = default;
            bool any = false;

            BuiltCreatureBody built = _body != null ? _body.Built : _preview != null ? _preview.Body : null;
            if (built == null) return false;

            if (built.Renderer != null && built.GeneratedMesh != null && TryTightBounds(built.GeneratedMesh, out Bounds skin))
                Encapsulate(ref bounds, ref any, skin, built.Renderer.transform);

            if (built.PartInstances != null)
            {
                foreach (CreaturePartInstance part in built.PartInstances)
                {
                    if (part.Object == null) continue;

                    foreach (MeshFilter filter in part.Object.GetComponentsInChildren<MeshFilter>(true))
                        if (filter.sharedMesh != null)
                            Encapsulate(ref bounds, ref any, filter.sharedMesh.bounds, filter.transform);
                }
            }

            return any;
        }

        /// <summary>The mesh bounds computed from the vertices alone, without the generator's padding.</summary>
        private static bool TryTightBounds(Mesh mesh, out Bounds bounds)
        {
            bounds = default;

            Vector3[] vertices = mesh.vertices;
            if (vertices.Length == 0) return false;

            bounds = new Bounds(vertices[0], Vector3.zero);
            for (int i = 1; i < vertices.Length; i++) bounds.Encapsulate(vertices[i]);

            return true;
        }

        /// <summary>
        /// Adds a mesh box, transformed into world space, to the total.
        /// </summary>
        /// <remarks>
        /// Eight corners rather than the centre plus the size: a part rotated relative to the root has
        /// its bounds computed in its own frame, and a flat conversion would lie about its reach.
        /// </remarks>
        private static void Encapsulate(ref Bounds bounds, ref bool any, Bounds local, Transform space)
        {
            Vector3 min = local.min;
            Vector3 max = local.max;

            for (int corner = 0; corner < 8; corner++)
            {
                var point = new Vector3(
                    (corner & 1) == 0 ? min.x : max.x,
                    (corner & 2) == 0 ? min.y : max.y,
                    (corner & 4) == 0 ? min.z : max.z);

                Vector3 world = space.TransformPoint(point);

                if (!any)
                {
                    bounds = new Bounds(world, Vector3.zero);
                    any = true;
                    continue;
                }

                bounds.Encapsulate(world);
            }
        }

        private Transform CreatePivot()
        {
            _pivot = new GameObject(PivotName).transform;
            _pivot.SetParent(transform, false);

            return _pivot;
        }
    }
}
