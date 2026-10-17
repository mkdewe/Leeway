using System.Collections.Generic;
using UnityEngine;

namespace Leeway.World.Editor
{
    /// <summary>
    /// Flat-shaded primitives for placeholder props, built per submesh so one prop can carry
    /// several materials (bark + foliage). Every triangle gets its own vertices.
    /// </summary>
    internal sealed class LowPolyMeshes
    {
        private readonly List<Vector3> _vertices = new();
        private readonly List<Vector3> _normals = new();
        private readonly List<List<int>> _submeshes = new();

        public LowPolyMeshes(int submeshCount)
        {
            for (int i = 0; i < submeshCount; i++) _submeshes.Add(new List<int>());
        }

        public void Triangle(int submesh, Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 cross = Vector3.Cross(b - a, c - a);
            if (cross.sqrMagnitude < 1e-10f) return; // degenerate - e.g. the apex of a dome
            Vector3 normal = cross.normalized;
            int start = _vertices.Count;
            _vertices.Add(a); _vertices.Add(b); _vertices.Add(c);
            _normals.Add(normal); _normals.Add(normal); _normals.Add(normal);
            _submeshes[submesh].Add(start);
            _submeshes[submesh].Add(start + 1);
            _submeshes[submesh].Add(start + 2);
        }

        /// <summary>Frustum (cone when topRadius is 0) along +Y, with caps.</summary>
        public void Frustum(int submesh, Vector3 basePos, int sides, float bottomRadius, float topRadius, float height,
            float angleOffset = 0f, Quaternion? rotation = null)
        {
            Quaternion rot = rotation ?? Quaternion.identity;
            Vector3 top = basePos + rot * (Vector3.up * height);
            for (int i = 0; i < sides; i++)
            {
                float a0 = angleOffset + i * Mathf.PI * 2f / sides;
                float a1 = angleOffset + (i + 1) * Mathf.PI * 2f / sides;
                Vector3 d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
                Vector3 d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                Vector3 b0 = basePos + rot * (d0 * bottomRadius);
                Vector3 b1 = basePos + rot * (d1 * bottomRadius);

                // Bottom cap, seen from below.
                Triangle(submesh, basePos, b0, b1);

                if (topRadius <= 0f)
                {
                    Triangle(submesh, b0, top, b1);
                    continue;
                }

                Vector3 t0 = top + rot * (d0 * topRadius);
                Vector3 t1 = top + rot * (d1 * topRadius);
                Triangle(submesh, b0, t0, t1);
                Triangle(submesh, b0, t1, b1);
                Triangle(submesh, top, t1, t0);
            }
        }

        /// <summary>Icosahedron with per-vertex radius noise: a rock or a bush blob.</summary>
        public void Blob(int submesh, Vector3 centre, Vector3 scale, float roughness, System.Random rng,
            int topSubmesh = -1, float topMinNormalY = 0.55f)
        {
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            var v = new List<Vector3>
            {
                new(-1, t, 0), new(1, t, 0), new(-1, -t, 0), new(1, -t, 0),
                new(0, -1, t), new(0, 1, t), new(0, -1, -t), new(0, 1, -t),
                new(t, 0, -1), new(t, 0, 1), new(-t, 0, -1), new(-t, 0, 1),
            };
            for (int i = 0; i < v.Count; i++)
            {
                float r = 1f + ((float)rng.NextDouble() * 2f - 1f) * roughness;
                Vector3 p = v[i].normalized * r;
                v[i] = centre + Vector3.Scale(p, scale);
            }

            int[] f =
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11,
                1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9,
                4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
            };
            // With this index order Cross(b - a, c - a) points outward, which is what Triangle expects.
            for (int i = 0; i < f.Length; i += 3)
            {
                Vector3 a = v[f[i]], b = v[f[i + 1]], c = v[f[i + 2]];
                bool top = topSubmesh >= 0 && Vector3.Cross(b - a, c - a).normalized.y > topMinNormalY;
                Triangle(top ? topSubmesh : submesh, a, b, c);
            }
        }

        /// <summary>Triangle wound so its face points away from <paramref name="inside"/>.</summary>
        public void TriangleOutward(int submesh, Vector3 a, Vector3 b, Vector3 c, Vector3 inside)
        {
            Vector3 normal = Vector3.Cross(b - a, c - a);
            if (Vector3.Dot(normal, (a + b + c) / 3f - inside) < 0f) (b, c) = (c, b);
            Triangle(submesh, a, b, c);
        }

        /// <summary>
        /// Single-surface rock dome open toward +Z: a cave shell. Render it with a double-sided
        /// material; a non-convex MeshCollider collides on both faces.
        /// </summary>
        public void CaveShell(int submesh, float radius, float height, int segments, int rings, float openingHalfAngle,
            int openRings, System.Random rng)
        {
            var grid = new Vector3[segments, rings + 1];
            for (int j = 0; j <= rings; j++)
            for (int i = 0; i < segments; i++)
            {
                float theta = i * Mathf.PI * 2f / segments;
                float phi = j / (float)rings * Mathf.PI * 0.5f;
                float jitter = j == rings ? 1f : 0.88f + 0.24f * (float)rng.NextDouble();
                float r = radius * Mathf.Cos(phi) * jitter;
                grid[i, j] = new Vector3(Mathf.Cos(theta) * r, -0.6f + height * Mathf.Sin(phi) * (j == rings ? 1f : 0.9f + 0.2f * (float)rng.NextDouble()), Mathf.Sin(theta) * r);
            }

            Vector3 inside = new Vector3(0f, height * 0.3f, 0f);
            for (int j = 0; j < rings; j++)
            for (int i = 0; i < segments; i++)
            {
                int n = (i + 1) % segments;
                float mid = (i + 0.5f) * 360f / segments;
                bool mouth = j < openRings && Mathf.Abs(Mathf.DeltaAngle(mid, 90f)) < openingHalfAngle;
                if (mouth) continue;

                TriangleOutward(submesh, grid[i, j], grid[n, j], grid[n, j + 1], inside);
                TriangleOutward(submesh, grid[i, j], grid[n, j + 1], grid[i, j + 1], inside);
            }
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(_vertices);
            mesh.SetNormals(_normals);
            mesh.subMeshCount = _submeshes.Count;
            for (int i = 0; i < _submeshes.Count; i++) mesh.SetTriangles(_submeshes[i], i);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
