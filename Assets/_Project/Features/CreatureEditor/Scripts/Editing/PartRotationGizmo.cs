using UnityEngine;

namespace Leeway.CreatureEditor
{
    /// <summary>An axis of the rotation gizmo.</summary>
    public enum GizmoAxis
    {
        X = 0,
        Y = 1,
        Z = 2,
    }

    /// <summary>
    /// Three XYZ rings for rotating the selected part.
    /// </summary>
    /// <remarks>
    /// <para><b>Hits are computed analytically</b> (ray against the ring's plane plus a distance check
    /// from the centre) rather than with colliders. A torus is concave, so as a <c>MeshCollider</c> it
    /// would need a non-convex version — and we would still want the hit tolerance in pixels, not in
    /// geometry. Twenty lines of maths come out cheaper and more predictable than physics.</para>
    ///
    /// <para>The gizmo is <b>not parented to a bone</b>, even though it moves with one. The preview is
    /// rebuilt on every edit and bones do not survive a rebuild — a gizmo parented to a bone would
    /// disappear along with it the first time the part twitched. Instead the controller feeds it the
    /// bone's position and rotation every frame through <see cref="Place"/>, while the object itself
    /// hangs at a stable place in the hierarchy.</para>
    /// </remarks>
    public class PartRotationGizmo : MonoBehaviour
    {
        private const int MajorSegments = 48;
        private const int MinorSegments = 8;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private static readonly Color[] AxisColors =
        {
            new(0.95f, 0.30f, 0.30f, 1f), // X
            new(0.35f, 0.90f, 0.40f, 1f), // Y
            new(0.35f, 0.55f, 1.00f, 1f), // Z
        };

        [Tooltip("The rings' material. Ideally the same unlit shader as the vertebra marker.")]
        [SerializeField] private Material _ringMaterial;

        [Tooltip("The ring radius in world units.")]
        [SerializeField] private float _radius = 0.35f;

        [Tooltip("The ring's thickness.")]
        [SerializeField] private float _thickness = 0.012f;

        [Tooltip("Hit tolerance on a ring, as a fraction of the radius.")]
        [SerializeField, Range(0.05f, 0.5f)] private float _hitTolerance = 0.15f;

        private readonly Renderer[] _rings = new Renderer[3];

        // Lazily, not in a field initialiser: a MaterialPropertyBlock would then be created in the
        // MonoBehaviour constructor, which Unity forbids.
        private MaterialPropertyBlock _block;

        private Transform _root;
        private Mesh _mesh;
        private GizmoAxis? _highlighted;

        public bool IsVisible => _root != null && _root.gameObject.activeSelf;

        /// <summary>
        /// Places the gizmo in the world and reveals it. The ring axes coincide with the axes of the
        /// rotation passed in — the controller feeds it the bone's rotation, so rotating around the "X"
        /// ring is a rotation around that bone's local X.
        /// </summary>
        public void Place(Vector3 worldPosition, Quaternion worldRotation)
        {
            EnsureBuilt();

            _root.SetPositionAndRotation(worldPosition, worldRotation);
            _root.gameObject.SetActive(true);
        }

        public void Hide()
        {
            if (_root != null) _root.gameObject.SetActive(false);
            SetHighlighted(null);
        }

        public void SetHighlighted(GizmoAxis? axis)
        {
            _highlighted = axis;
            if (_rings[0] == null) return;

            _block ??= new MaterialPropertyBlock();

            for (int i = 0; i < _rings.Length; i++)
            {
                if (_rings[i] == null) continue;

                Color color = AxisColors[i];
                if (_highlighted.HasValue && (int)_highlighted.Value == i) color = Color.Lerp(color, Color.white, 0.6f);

                _rings[i].GetPropertyBlock(_block);
                _block.SetColor(BaseColorId, color);
                _rings[i].SetPropertyBlock(_block);
            }
        }

        /// <summary>The axis direction in world space — the normal of the corresponding ring's plane.</summary>
        public Vector3 WorldAxis(GizmoAxis axis)
        {
            if (_root == null) return Vector3.zero;

            return axis switch
            {
                GizmoAxis.X => _root.right,
                GizmoAxis.Y => _root.up,
                _ => _root.forward,
            };
        }

        public Vector3 WorldCenter => _root != null ? _root.position : Vector3.zero;

        /// <summary>
        /// Which ring the ray hits. The one whose hit lies closest to the camera wins — otherwise a ring
        /// at the back would steal clicks from the one at the front.
        /// </summary>
        public bool TryPick(Ray ray, out GizmoAxis axis, out Vector3 hitPoint)
        {
            axis = default;
            hitPoint = default;

            if (!IsVisible) return false;

            float bestDistance = float.MaxValue;
            bool found = false;

            for (int i = 0; i < 3; i++)
            {
                var candidate = (GizmoAxis)i;
                if (!TryPickRing(ray, candidate, out Vector3 point, out float distance)) continue;
                if (distance >= bestDistance) continue;

                bestDistance = distance;
                axis = candidate;
                hitPoint = point;
                found = true;
            }

            return found;
        }

        /// <summary>Projects the ray onto a ring's plane and checks whether it hit its circumference.</summary>
        public bool TryProjectOntoRing(Ray ray, GizmoAxis axis, out Vector3 point)
            => TryIntersectPlane(ray, axis, out point);

        private bool TryPickRing(Ray ray, GizmoAxis axis, out Vector3 point, out float distance)
        {
            point = default;
            distance = 0f;

            if (!TryIntersectPlane(ray, axis, out point)) return false;

            distance = Vector3.Distance(ray.origin, point);

            float offset = Mathf.Abs(Vector3.Distance(point, WorldCenter) - _radius);
            return offset <= _radius * _hitTolerance;
        }

        private bool TryIntersectPlane(Ray ray, GizmoAxis axis, out Vector3 point)
        {
            point = default;

            var plane = new Plane(WorldAxis(axis), WorldCenter);

            // A ray almost parallel to the plane gives a hit at infinity — Plane.Raycast catches that,
            // but at a very flat angle it returns an absurd point.
            if (!plane.Raycast(ray, out float enter) || enter <= 0f) return false;

            point = ray.GetPoint(enter);
            return true;
        }

        private void EnsureBuilt()
        {
            if (_root != null) return;

            _mesh = BuildTorus(_radius, _thickness);

            // The parent is the component itself, never a bone — see the note in the class summary.
            _root = new GameObject("PartRotationGizmo").transform;
            _root.SetParent(transform, false);

            for (int i = 0; i < 3; i++)
            {
                var ring = new GameObject($"Ring_{(GizmoAxis)i}");
                ring.transform.SetParent(_root, false);

                // The torus lies in the XY plane with a +Z normal, so we rotate it until the normal
                // coincides with the ring's axis.
                ring.transform.localRotation = i switch
                {
                    0 => Quaternion.Euler(0f, 90f, 0f),  // +X normal
                    1 => Quaternion.Euler(90f, 0f, 0f),  // +Y normal
                    _ => Quaternion.identity,            // +Z normal
                };

                ring.AddComponent<MeshFilter>().sharedMesh = _mesh;

                var renderer = ring.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = _ringMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                _rings[i] = renderer;
            }

            SetHighlighted(null);
            _root.gameObject.SetActive(false);
        }

        /// <summary>A torus in the XY plane, with a +Z normal.</summary>
        private static Mesh BuildTorus(float major, float minor)
        {
            var vertices = new Vector3[MajorSegments * MinorSegments];
            var normals = new Vector3[vertices.Length];
            var triangles = new int[MajorSegments * MinorSegments * 6];

            for (int i = 0; i < MajorSegments; i++)
            {
                float u = i / (float)MajorSegments * Mathf.PI * 2f;
                var center = new Vector3(Mathf.Cos(u) * major, Mathf.Sin(u) * major, 0f);
                var outward = new Vector3(Mathf.Cos(u), Mathf.Sin(u), 0f);

                for (int j = 0; j < MinorSegments; j++)
                {
                    float v = j / (float)MinorSegments * Mathf.PI * 2f;
                    Vector3 normal = outward * Mathf.Cos(v) + Vector3.forward * Mathf.Sin(v);

                    int index = i * MinorSegments + j;
                    vertices[index] = center + normal * minor;
                    normals[index] = normal;

                    int nextI = (i + 1) % MajorSegments;
                    int nextJ = (j + 1) % MinorSegments;

                    int a = i * MinorSegments + j;
                    int b = nextI * MinorSegments + j;
                    int c = nextI * MinorSegments + nextJ;
                    int d = i * MinorSegments + nextJ;

                    int t = index * 6;
                    triangles[t++] = a; triangles[t++] = b; triangles[t++] = c;
                    triangles[t++] = a; triangles[t++] = c; triangles[t] = d;
                }
            }

            var mesh = new Mesh { name = "GizmoRing" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private void OnDestroy()
        {
            if (_root != null) Destroy(_root.gameObject);

            // The mesh is generated, not an asset — without this it would leak.
            if (_mesh != null) Destroy(_mesh);
        }
    }
}
