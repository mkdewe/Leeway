using UnityEngine;

namespace Leeway.CreatureEditor
{
    /// <summary>Which end of the spine a given arrow lengthens.</summary>
    public enum SpineEnd
    {
        /// <summary>The head side — the new vertebra becomes the new head.</summary>
        Head = 0,

        /// <summary>The tail side — the new vertebra is appended at the end.</summary>
        Tail = 1,
    }

    /// <summary>
    /// Two arrows at the ends of the spine. Dragging an arrow outwards adds vertebrae on that side,
    /// dragging it inwards removes them.
    /// </summary>
    /// <remarks>
    /// <para>The spine grows by <b>adding vertebrae</b>, not by stretching the existing ones — which is
    /// why lengthening has its own visible handle, separate from dragging a single vertebra.</para>
    ///
    /// <para>Hits are computed analytically, as in the rotation gizmo: an arrow is a segment from its
    /// base to its tip, so the distance from the ray to that segment is enough. Colliders here would
    /// just be one more thing to keep in step with the geometry.</para>
    ///
    /// <para>The arrows hang at a stable place in the hierarchy and are positioned in world space every
    /// frame. Parenting them to a bone would not survive a preview rebuild, which destroys and recreates
    /// the bones on every edit.</para>
    /// </remarks>
    public class SpineExtendGizmo : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private Material _material;

        [Tooltip("The arrow's length in world units.")]
        [SerializeField] private float _length = 0.42f;

        [Tooltip("The shaft's thickness.")]
        [SerializeField] private float _thickness = 0.05f;

        [Tooltip("How far from the end vertebra the arrow starts.")]
        [SerializeField] private float _gap = 0.12f;

        [Tooltip("Hit tolerance on the arrow, in world units.")]
        [SerializeField] private float _hitRadius = 0.13f;

        [SerializeField] private Color _normalColor = new(0.45f, 0.85f, 1f, 1f);
        [SerializeField] private Color _highlightColor = new(1f, 0.85f, 0.3f, 1f);

        private readonly Transform[] _shafts = new Transform[2];
        private readonly Transform[] _tips = new Transform[2];
        private readonly Vector3[] _origins = new Vector3[2];
        private readonly Vector3[] _directions = new Vector3[2];

        private Transform _root;
        private MaterialPropertyBlock _block;
        private SpineEnd? _highlighted;

        public bool IsVisible => _root != null && _root.gameObject.activeSelf;

        /// <summary>Places both arrows at the ends of the spine and reveals them.</summary>
        public void Place(Vector3 headPosition, Vector3 headDirection, Vector3 tailPosition, Vector3 tailDirection)
        {
            EnsureBuilt();

            Apply(SpineEnd.Head, headPosition, headDirection);
            Apply(SpineEnd.Tail, tailPosition, tailDirection);

            _root.gameObject.SetActive(true);
        }

        public void Hide()
        {
            if (_root != null) _root.gameObject.SetActive(false);
            SetHighlighted(null);
        }

        public void SetHighlighted(SpineEnd? end)
        {
            _highlighted = end;
            if (_shafts[0] == null) return;

            _block ??= new MaterialPropertyBlock();

            for (int i = 0; i < 2; i++)
            {
                Color color = _highlighted.HasValue && (int)_highlighted.Value == i ? _highlightColor : _normalColor;
                Tint(_shafts[i], color);
                Tint(_tips[i], color);
            }
        }

        /// <summary>Which arrow lies under the ray. The one closer to the camera wins.</summary>
        public bool TryPick(Ray ray, out SpineEnd end)
        {
            end = default;
            if (!IsVisible) return false;

            float best = float.MaxValue;
            bool found = false;

            for (int i = 0; i < 2; i++)
            {
                Vector3 from = _origins[i];
                Vector3 to = from + _directions[i] * _length;

                if (!IsNear(ray, from, to, _hitRadius, out float distance)) continue;
                if (distance >= best) continue;

                best = distance;
                end = (SpineEnd)i;
                found = true;
            }

            return found;
        }

        /// <summary>
        /// The axis a given arrow drags along: its base and the "away from the body" facing. The
        /// controller measures along it to work out how many vertebrae to add or remove.
        /// </summary>
        public bool TryGetAxis(SpineEnd end, out Vector3 origin, out Vector3 direction)
        {
            int i = (int)end;

            origin = _origins[i];
            direction = _directions[i];

            return IsVisible && direction.sqrMagnitude > 1e-6f;
        }

        private void Apply(SpineEnd end, Vector3 position, Vector3 direction)
        {
            int i = (int)end;

            Vector3 normalized = direction.sqrMagnitude > 1e-6f ? direction.normalized : Vector3.forward;
            Vector3 origin = position + normalized * _gap;

            _origins[i] = origin;
            _directions[i] = normalized;

            Quaternion rotation = Quaternion.FromToRotation(Vector3.up, normalized);

            // Shaft: Unity's capsule is 2 units tall at scale 1, hence half the length in Y.
            _shafts[i].SetPositionAndRotation(origin + normalized * (_length * 0.35f), rotation);
            _shafts[i].localScale = new Vector3(_thickness, _length * 0.35f, _thickness);

            _tips[i].SetPositionAndRotation(origin + normalized * (_length * 0.82f), rotation);
            _tips[i].localScale = new Vector3(_thickness * 2.6f, _thickness * 2.6f, _thickness * 2.6f);
        }

        private void Tint(Transform target, Color color)
        {
            if (target == null || !target.TryGetComponent(out Renderer renderer)) return;

            renderer.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, color);
            renderer.SetPropertyBlock(_block);
        }

        /// <summary>The distance from a ray to a segment. Simpler and cheaper than a collider on the arrow.</summary>
        private static bool IsNear(Ray ray, Vector3 from, Vector3 to, float radius, out float distance)
        {
            distance = 0f;

            Vector3 segment = to - from;
            float segmentLength = segment.magnitude;
            if (segmentLength < 1e-6f) return false;

            Vector3 axis = segment / segmentLength;

            // The closest approach of two skew lines.
            Vector3 w0 = ray.origin - from;
            float b = Vector3.Dot(ray.direction, axis);
            float d = Vector3.Dot(ray.direction, w0);
            float e = Vector3.Dot(axis, w0);
            float denominator = 1f - b * b;

            float alongRay = Mathf.Abs(denominator) < 1e-6f ? -d : (b * e - d) / denominator;
            float alongSegment = Mathf.Abs(denominator) < 1e-6f ? e : (e - b * d) / denominator;

            if (alongRay < 0f) return false;

            alongSegment = Mathf.Clamp(alongSegment, 0f, segmentLength);

            Vector3 pointOnRay = ray.origin + ray.direction * alongRay;
            Vector3 pointOnSegment = from + axis * alongSegment;

            if (Vector3.Distance(pointOnRay, pointOnSegment) > radius) return false;

            distance = alongRay;
            return true;
        }

        private void EnsureBuilt()
        {
            if (_root != null) return;

            _root = new GameObject("SpineExtendGizmo").transform;
            _root.SetParent(transform, false);

            for (int i = 0; i < 2; i++)
            {
                _shafts[i] = Build($"Shaft_{(SpineEnd)i}", PrimitiveType.Capsule);
                _tips[i] = Build($"Tip_{(SpineEnd)i}", PrimitiveType.Sphere);
            }

            SetHighlighted(null);
            _root.gameObject.SetActive(false);
        }

        private Transform Build(string name, PrimitiveType type)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;

            // Hits are computed analytically, so a collider would only get in the way of the editor's raycasts.
            Destroy(go.GetComponent<Collider>());

            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = _material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            go.transform.SetParent(_root, false);
            return go.transform;
        }

        private void OnDestroy()
        {
            if (_root != null) Destroy(_root.gameObject);
        }
    }
}
