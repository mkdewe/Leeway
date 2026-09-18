using UnityEngine;

namespace Leeway.CreatureEditor
{
    /// <summary>
    /// The selection handle for one vertebra. Besides the index it carries a reference to the visual
    /// marker and knows how to highlight it on hover — the hits themselves are still collected by
    /// <see cref="VertebraHandleSet"/>.
    /// </summary>
    [RequireComponent(typeof(CapsuleCollider))]
    public class VertebraHandle : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private static MaterialPropertyBlock _sharedBlock;

        private Renderer _marker;
        private Color _normalColor;
        private Color _highlightColor;

        public int VertebraIndex { get; private set; }

        public void Bind(int vertebraIndex, Renderer marker, Color normalColor, Color highlightColor)
        {
            VertebraIndex = vertebraIndex;
            _marker = marker;
            _normalColor = normalColor;
            _highlightColor = highlightColor;
            SetHighlighted(false);
        }

        /// <summary>Changes the marker's colour through a <see cref="MaterialPropertyBlock"/> — without it every hover would spawn another material instance.</summary>
        public void SetHighlighted(bool highlighted)
        {
            if (_marker == null) return;

            _sharedBlock ??= new MaterialPropertyBlock();
            _marker.GetPropertyBlock(_sharedBlock);
            _sharedBlock.SetColor(BaseColorId, highlighted ? _highlightColor : _normalColor);
            _marker.SetPropertyBlock(_sharedBlock);
        }
    }

    /// <summary>
    /// Keeps the set of handles in step with the preview's current rig.
    /// </summary>
    /// <remarks>
    /// The handles sit on their own layer and are the only thing the selection raycast hits — that way
    /// a click catches neither the body mesh, nor the locomotion collider, nor the ground. The set is
    /// rebuilt together with the body: bones do not survive a rebuild, so a handle holding an old bone
    /// would point at a destroyed object.
    ///
    /// <para>The spine bones are hidden by default — <see cref="Visible"/> is only switched on by a
    /// click on the torso (see <c>CreatureEditorController</c>). The same flag controls both the
    /// marker's visibility and whether its collider catches the raycast at all: one <c>SetActive</c>
    /// on the handle object does both at once.</para>
    ///
    /// <para>Every vertebra is a <b>capsule</b> spanning from halfway to its predecessor to halfway to
    /// its successor. That tiling makes the spine read as one connected chain whatever the vertebra
    /// spacing, and the rounded caps close the joins even on bends. The collider has exactly the same
    /// shape as the marker — you click what you see.</para>
    /// </remarks>
    public class VertebraHandleSet : MonoBehaviour
    {
        [SerializeField] private CreatureBodyPreview _preview;

        [Tooltip("The handle layer - has to be the same one the selection raycast queries.")]
        [SerializeField] private int _handleLayer;

        [Header("Appearance")]
        [Tooltip("The vertebra's visual marker - a capsule, with no collider. Hit testing is done by the CapsuleCollider on the handle object.")]
        [SerializeField] private GameObject _markerPrefab;

        [Tooltip("The thickness of a vertebra capsule. A fixed size, independent of the flesh radius - a legible spine chain.")]
        [SerializeField] private float _thickness = 0.12f;

        [SerializeField] private Color _normalColor = new(0.2f, 0.9f, 1f, 0.9f);
        [SerializeField] private Color _highlightColor = new(1f, 0.8f, 0.15f, 1f);

        private readonly System.Collections.Generic.List<VertebraHandle> _handles = new();
        private bool _visible;

        public System.Collections.Generic.IReadOnlyList<VertebraHandle> Handles => _handles;

        /// <summary>Whether the spine vertebrae are currently exposed (after a click on the torso).</summary>
        public bool Visible => _visible;

        private void OnEnable()
        {
            if (_preview != null) _preview.Rebuilt += OnPreviewRebuilt;
        }

        private void OnDisable()
        {
            if (_preview != null) _preview.Rebuilt -= OnPreviewRebuilt;
        }

        private void OnPreviewRebuilt(Creature.Domain.CreatureGenome genome, Creature.Domain.CreatureStats stats) => Rebuild();

        /// <summary>Toggles the bones' visibility — called by the controller after a click on the torso or into empty space.</summary>
        public void SetVisible(bool visible)
        {
            _visible = visible;

            for (int i = 0; i < _handles.Count; i++)
            {
                if (_handles[i] == null) continue;

                _handles[i].gameObject.SetActive(_visible);
                if (!_visible) _handles[i].SetHighlighted(false);
            }
        }

        public void Rebuild()
        {
            Clear();

            if (_preview == null || _preview.Body == null || _preview.Genome == null) return;

            Transform[] bones = _preview.Body.Bones;
            for (int i = 0; i < bones.Length; i++)
            {
                if (bones[i] == null) continue;

                MeasureSpan(bones, i, out Vector3 axis, out float length, out float center);
                float height = length + _thickness;

                var go = new GameObject($"VertebraHandle_{i}") { layer = _handleLayer };
                go.transform.SetParent(bones[i], false);
                go.transform.localPosition = Vector3.zero;

                // Unity's capsule lies on the Y axis and the collider cannot take an arbitrary
                // rotation — so we rotate the handle itself until its Y lines up with the spine.
                go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, axis);

                var capsule = go.AddComponent<CapsuleCollider>();
                capsule.isTrigger = true;
                capsule.direction = 1;
                capsule.center = new Vector3(0f, center, 0f);
                capsule.radius = _thickness * 0.5f;
                capsule.height = height;

                Renderer marker = SpawnMarker(go.transform, center, height);

                VertebraHandle handle = go.AddComponent<VertebraHandle>();
                handle.Bind(i, marker, _normalColor, _highlightColor);
                _handles.Add(handle);

                // A rebuild creates the handles afresh — they have to inherit the current visibility
                // state, otherwise every edit (a scroll growing a bone, say) would briefly hide the
                // vertebrae.
                go.SetActive(_visible);
            }
        }

        /// <summary>
        /// A vertebra's span in its own space: from halfway to its predecessor to halfway to its
        /// successor. The end vertebrae have only one half.
        /// </summary>
        private static void MeasureSpan(Transform[] bones, int index, out Vector3 axis, out float length, out float center)
        {
            bool hasNext = index + 1 < bones.Length && bones[index + 1] != null;
            bool hasPrev = index > 0 && bones[index] != null;

            // The successor is a child, so its localPosition is already in this vertebra's space.
            Vector3 toNext = hasNext ? bones[index + 1].localPosition : Vector3.zero;

            // The predecessor is the parent: the vector to it has to be rotated into this vertebra's space.
            Vector3 toPrev = hasPrev
                ? Quaternion.Inverse(bones[index].localRotation) * -bones[index].localPosition
                : Vector3.zero;

            float forward = hasNext ? toNext.magnitude * 0.5f : 0f;
            float backward = hasPrev ? toPrev.magnitude * 0.5f : 0f;

            Vector3 forwardDir = hasNext && toNext.sqrMagnitude > 1e-8f ? toNext.normalized : Vector3.zero;
            Vector3 backwardDir = hasPrev && toPrev.sqrMagnitude > 1e-8f ? -toPrev.normalized : Vector3.zero;

            Vector3 blended = forwardDir + backwardDir;
            axis = blended.sqrMagnitude > 1e-8f ? blended.normalized : Vector3.forward;

            length = forward + backward;
            center = (forward - backward) * 0.5f;
        }

        private Renderer SpawnMarker(Transform parent, float center, float height)
        {
            if (_markerPrefab == null) return null;

            GameObject marker = Instantiate(_markerPrefab, parent);
            marker.transform.localPosition = new Vector3(0f, center, 0f);
            marker.transform.localRotation = Quaternion.identity;

            // The built-in capsule is 2 units tall and 1 across at unit scale, so the Y scale is half
            // the target height. The dimensions match the collider.
            marker.transform.localScale = new Vector3(_thickness, height * 0.5f, _thickness);

            return marker.GetComponentInChildren<Renderer>();
        }

        private void Clear()
        {
            for (int i = 0; i < _handles.Count; i++)
            {
                if (_handles[i] == null) continue;

                if (Application.isPlaying) Destroy(_handles[i].gameObject);
                else DestroyImmediate(_handles[i].gameObject);
            }

            _handles.Clear();
        }

        private void OnDestroy() => Clear();
    }
}
