using System.Collections.Generic;
using UnityEngine;

namespace Leeway.CreatureEditor
{
    /// <summary>
    /// Low-poly bones: flat-shaded prisms and knobs, built straight into meshes in the local space of the
    /// transform they will hang on, so a skeleton piece rides its bone however the body is lying.
    /// </summary>
    public sealed class SkeletonMesh
    {
        private readonly List<Vector3> _vertices = new();
        private readonly List<Vector3> _normals = new();
        private readonly List<int> _triangles = new();

        public bool IsEmpty => _vertices.Count == 0;

        private void Triangle(Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 cross = Vector3.Cross(b - a, c - a);
            if (cross.sqrMagnitude < 1e-12f) return;

            Vector3 normal = cross.normalized;
            int start = _vertices.Count;
            _vertices.Add(a); _vertices.Add(b); _vertices.Add(c);
            _normals.Add(normal); _normals.Add(normal); _normals.Add(normal);
            _triangles.Add(start); _triangles.Add(start + 1); _triangles.Add(start + 2);
        }

        /// <summary>A bone: a prism from <paramref name="a"/> to <paramref name="b"/> that is thicker at the ends, with a knob on each.</summary>
        public void Bone(Vector3 a, Vector3 b, float radius, int sides = 6)
        {
            Vector3 axis = b - a;
            float length = axis.magnitude;
            if (length < 1e-4f) return;

            axis /= length;
            Vector3 side = Vector3.Cross(axis, Mathf.Abs(axis.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
            Vector3 up = Vector3.Cross(axis, side).normalized;

            // Thick ends, slim shaft: the silhouette of a bone.
            const int rings = 3;
            for (int r = 0; r < rings; r++)
            {
                float t0 = r / (float)rings, t1 = (r + 1) / (float)rings;
                float w0 = Mathf.Lerp(radius * 1.35f, radius * 0.7f, Mathf.Sin(t0 * Mathf.PI));
                float w1 = Mathf.Lerp(radius * 1.35f, radius * 0.7f, Mathf.Sin(t1 * Mathf.PI));
                for (int i = 0; i < sides; i++)
                {
                    float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides;
                    Vector3 d0 = side * Mathf.Cos(a0) + up * Mathf.Sin(a0);
                    Vector3 d1 = side * Mathf.Cos(a1) + up * Mathf.Sin(a1);
                    Vector3 p00 = Vector3.Lerp(a, b, t0) + d0 * w0, p01 = Vector3.Lerp(a, b, t0) + d1 * w0;
                    Vector3 p10 = Vector3.Lerp(a, b, t1) + d0 * w1, p11 = Vector3.Lerp(a, b, t1) + d1 * w1;
                    Triangle(p00, p10, p11);
                    Triangle(p00, p11, p01);
                }
            }

            Knob(a, radius * 1.45f);
            Knob(b, radius * 1.45f);
        }

        /// <summary>An icosphere, flat-shaded, optionally stretched along each axis.</summary>
        public void Knob(Vector3 centre, float radius, Vector3? scale = null, Quaternion? rotation = null)
        {
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            Vector3[] v =
            {
                new(-1, t, 0), new(1, t, 0), new(-1, -t, 0), new(1, -t, 0),
                new(0, -1, t), new(0, 1, t), new(0, -1, -t), new(0, 1, -t),
                new(t, 0, -1), new(t, 0, 1), new(-t, 0, -1), new(-t, 0, 1),
            };

            Vector3 s = scale ?? Vector3.one;
            Quaternion q = rotation ?? Quaternion.identity;
            for (int i = 0; i < v.Length; i++) v[i] = centre + q * Vector3.Scale(v[i].normalized * radius, s);

            int[] f =
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11,
                1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9,
                4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
            };

            for (int i = 0; i < f.Length; i += 3) Triangle(v[f[i]], v[f[i + 1]], v[f[i + 2]]);
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(_vertices);
            mesh.SetNormals(_normals);
            mesh.SetTriangles(_triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
