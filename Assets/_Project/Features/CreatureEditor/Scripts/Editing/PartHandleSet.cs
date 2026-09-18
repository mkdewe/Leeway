using System.Collections.Generic;
using UnityEngine;

namespace Leeway.CreatureEditor
{
    /// <summary>
    /// Keeps the set of part grab handles in step with the current preview.
    /// </summary>
    /// <remarks>
    /// <para>The handles sit on the same layer as the vertebra handles, so the same raycast picks them
    /// up — what the player grabs is decided by distance from the camera. That is the right order: a
    /// part lies on top of a vertebra, so hovering an eye should grab the eye, not the vertebra
    /// underneath it.</para>
    ///
    /// <para>Unlike vertebrae, parts are grabbable <b>at all times</b> in sculpting mode, without
    /// exposing the spine — hovering a mouth should simply work.</para>
    ///
    /// <para>The collider is fitted to the renderers' actual bounds rather than a fixed radius: an eye
    /// and a leg differ in size by an order of magnitude, and the handle has to be comfortable for
    /// both.</para>
    /// </remarks>
    public class PartHandleSet : MonoBehaviour
    {
        [SerializeField] private CreatureBodyPreview _preview;

        [Tooltip("The handle layer - the same one the editor's raycast queries.")]
        [SerializeField] private int _handleLayer = 8;

        [Tooltip("Padding added to a part's bounds so small elements can still be hit comfortably.")]
        [SerializeField] private float _hitPadding = 0.02f;

        [SerializeField] private Color _highlightColor = new(1f, 0.8f, 0.15f, 1f);

        private readonly List<PartHandle> _handles = new();

        public IReadOnlyList<PartHandle> Handles => _handles;

        private void OnEnable()
        {
            if (_preview != null) _preview.Rebuilt += OnPreviewRebuilt;
        }

        private void OnDisable()
        {
            if (_preview != null) _preview.Rebuilt -= OnPreviewRebuilt;
        }

        private void OnPreviewRebuilt(Creature.Domain.CreatureGenome genome, Creature.Domain.CreatureStats stats) => Rebuild();

        public void Rebuild()
        {
            _handles.Clear();

            if (_preview == null || _preview.Body?.PartInstances == null) return;

            foreach (CreaturePartInstance instance in _preview.Body.PartInstances)
            {
                GameObject go = instance.Object;
                if (go == null) continue;

                if (!TryMeasure(go.transform, out Bounds local)) continue;

                go.layer = _handleLayer;

                var box = go.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.center = local.center;
                box.size = local.size + Vector3.one * _hitPadding;

                PartHandle handle = go.AddComponent<PartHandle>();
                handle.Bind(instance.GeneIndex, instance.Mirrored, _highlightColor);
                _handles.Add(handle);
            }
        }

        /// <summary>
        /// A part's bounds in its own space. We compute them from mesh corners transformed into the
        /// part root's space, because <c>Renderer.bounds</c> is in world space and would give a box far
        /// too large for a rotated bone.
        /// </summary>
        private static bool TryMeasure(Transform root, out Bounds bounds)
        {
            bounds = default;

            var filters = root.GetComponentsInChildren<MeshFilter>(true);
            bool any = false;

            foreach (MeshFilter filter in filters)
            {
                Mesh mesh = filter.sharedMesh;
                if (mesh == null) continue;

                Bounds meshBounds = mesh.bounds;
                Vector3 center = meshBounds.center;
                Vector3 extents = meshBounds.extents;

                for (int corner = 0; corner < 8; corner++)
                {
                    var offset = new Vector3(
                        (corner & 1) == 0 ? -extents.x : extents.x,
                        (corner & 2) == 0 ? -extents.y : extents.y,
                        (corner & 4) == 0 ? -extents.z : extents.z);

                    Vector3 world = filter.transform.TransformPoint(center + offset);
                    Vector3 local = root.InverseTransformPoint(world);

                    if (!any)
                    {
                        bounds = new Bounds(local, Vector3.zero);
                        any = true;
                    }
                    else
                    {
                        bounds.Encapsulate(local);
                    }
                }
            }

            return any;
        }
    }
}
