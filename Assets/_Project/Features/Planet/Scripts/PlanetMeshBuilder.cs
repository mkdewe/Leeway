using System.Collections.Generic;
using Leeway.World.Domain;
using UnityEngine;
using UnityEngine.Rendering;

namespace Leeway.Planet
{
    /// <summary>
    /// Turns a <see cref="PlanetModel"/> into one flat-shaded mesh: a subdivided icosahedron pushed out to
    /// the ground height, every facet coloured for what lies under it - deep sea, shallows, sand, grass,
    /// rock and snow.
    /// </summary>
    /// <remarks>
    /// The facets are not shared between neighbours, so each has its own normal and its own colour: the
    /// low-poly look of the rest of the game, and no gradients smearing a coast across a facet.
    /// </remarks>
    public static class PlanetMeshBuilder
    {
        private static readonly Color DeepSea = new(0.09f, 0.27f, 0.50f);
        private static readonly Color ShallowSea = new(0.28f, 0.70f, 0.76f);
        private static readonly Color Sand = new(0.88f, 0.81f, 0.56f);
        private static readonly Color LowGrass = new(0.47f, 0.73f, 0.34f);
        private static readonly Color HighGrass = new(0.28f, 0.55f, 0.27f);
        private static readonly Color Rock = new(0.55f, 0.51f, 0.47f);
        private static readonly Color Snow = new(0.95f, 0.96f, 0.98f);

        // The subdivided icosahedron depends only on the level, and a rebuild changes only the heights.
        private static readonly Dictionary<int, Icosphere> Cache = new();

        private sealed class Icosphere
        {
            public Vector3[] Directions;
            public int[] Triangles;
        }

        public static Mesh Build(PlanetModel model, int detail, Mesh reuse = null)
        {
            Icosphere sphere = Get(Mathf.Clamp(detail, 2, 7));

            // Heights once per distinct vertex; the facets share them.
            var corners = new Vector3[sphere.Directions.Length];
            for (int i = 0; i < corners.Length; i++)
            {
                Vector3 dir = sphere.Directions[i];
                corners[i] = dir * (model.Radius + model.Sample(dir).Height);
            }

            int faces = sphere.Triangles.Length / 3;
            var vertices = new Vector3[faces * 3];
            var normals = new Vector3[faces * 3];
            var colors = new Color32[faces * 3];
            var indices = new int[faces * 3];

            for (int f = 0; f < faces; f++)
            {
                int ia = sphere.Triangles[f * 3], ib = sphere.Triangles[f * 3 + 1], ic = sphere.Triangles[f * 3 + 2];
                Vector3 a = corners[ia], b = corners[ib], c = corners[ic];
                Vector3 centre = sphere.Directions[ia] + sphere.Directions[ib] + sphere.Directions[ic];

                Vector3 normal = Vector3.Cross(b - a, c - a);
                // Unity draws clockwise faces: if the winding gives an inward normal, swap two corners.
                if (Vector3.Dot(normal, centre) < 0f)
                {
                    (b, c) = (c, b);
                    normal = -normal;
                }
                normal.Normalize();

                Color32 colour = FacetColour(model, centre.normalized);
                int v = f * 3;
                vertices[v] = a; vertices[v + 1] = b; vertices[v + 2] = c;
                normals[v] = normals[v + 1] = normals[v + 2] = normal;
                colors[v] = colors[v + 1] = colors[v + 2] = colour;
                indices[v] = v; indices[v + 1] = v + 1; indices[v + 2] = v + 2;
            }

            Mesh mesh = reuse != null ? reuse : new Mesh { name = "Planet" };
            mesh.Clear();
            mesh.indexFormat = IndexFormat.UInt32;
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.colors32 = colors;
            mesh.SetIndices(indices, MeshTopology.Triangles, 0);

            // Bounds by hand: the planet is round and the automatic ones would need a pass over every vertex.
            float extent = (model.Radius + model.MaxHeight) * 2.05f;
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(extent, extent, extent));
            return mesh;
        }

        private static Color FacetColour(PlanetModel model, Vector3 direction)
        {
            PlanetModel.Point point = model.Sample(direction);

            if (!point.IsLand)
            {
                // Right at a coast the facet is shallow water; the colour deepens with the distance out.
                return Color.Lerp(ShallowSea, DeepSea, Mathf.Clamp01(point.Depth * 1.35f));
            }

            float high = model.MaxHeight > 0f ? Mathf.Clamp01(point.Height / model.MaxHeight) : 0f;
            if (point.Land < 0.07f || high < 0.045f) return Sand;
            if (high < 0.34f) return Color.Lerp(LowGrass, HighGrass, high / 0.34f);
            if (high < 0.66f) return HighGrass;
            if (high < 0.84f) return Rock;
            return Snow;
        }

        // ---------------------------------------------------------------- icosphere

        private static Icosphere Get(int level)
        {
            if (Cache.TryGetValue(level, out Icosphere cached)) return cached;

            float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
            var directions = new List<Vector3>
            {
                new(-1, t, 0), new(1, t, 0), new(-1, -t, 0), new(1, -t, 0),
                new(0, -1, t), new(0, 1, t), new(0, -1, -t), new(0, 1, -t),
                new(t, 0, -1), new(t, 0, 1), new(-t, 0, -1), new(-t, 0, 1),
            };
            for (int i = 0; i < directions.Count; i++) directions[i] = directions[i].normalized;

            var triangles = new List<int>
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11,
                1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9,
                4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
            };

            for (int step = 0; step < level; step++)
            {
                var midpoints = new Dictionary<long, int>();
                var next = new List<int>(triangles.Count * 4);

                int Middle(int p, int q)
                {
                    long key = p < q ? ((long)p << 32) | (uint)q : ((long)q << 32) | (uint)p;
                    if (midpoints.TryGetValue(key, out int existing)) return existing;
                    directions.Add(((directions[p] + directions[q]) * 0.5f).normalized);
                    midpoints[key] = directions.Count - 1;
                    return directions.Count - 1;
                }

                for (int i = 0; i < triangles.Count; i += 3)
                {
                    int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                    int ab = Middle(a, b), bc = Middle(b, c), ca = Middle(c, a);
                    next.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                triangles = next;
            }

            var sphere = new Icosphere { Directions = directions.ToArray(), Triangles = triangles.ToArray() };
            Cache[level] = sphere;
            return sphere;
        }
    }
}
