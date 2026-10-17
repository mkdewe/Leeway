using System.Collections.Generic;
using Leeway.World.Domain;
using UnityEngine;
using UnityEngine.Rendering;

namespace Leeway.World
{
    /// <summary>
    /// Turns an <see cref="IslandHeightField"/> into meshes: flat-shaded terrain chunks (every
    /// triangle has its own vertices, so faces read as low-poly facets) coloured per triangle,
    /// lighter shared-vertex collider chunks and the water surface (sea, lakes, rivers).
    /// </summary>
    public static class IslandMeshBuilder
    {
        /// <summary>Terrain for the cells [x0, x1) x [z0, z1). Null when the chunk is all deep sea floor.</summary>
        public static Mesh BuildTerrainChunk(IslandHeightField field, IslandBiome biome, int x0, int z0, int x1, int z1)
        {
            IslandSettings s = field.Settings;
            var patches = new IslandNoise(field.Seed + 101);
            float grassMinNormalY = Mathf.Cos(biome.GrassMaxSlope * Mathf.Deg2Rad);
            float deepFloor = s.SeaFloorDepth + 0.25f;

            int cells = (x1 - x0) * (z1 - z0);
            var vertices = new List<Vector3>(cells * 6);
            var normals = new List<Vector3>(cells * 6);
            var colors = new List<Color>(cells * 6);

            for (int z = z0; z < z1; z++)
            for (int x = x0; x < x1; x++)
            {
                Vector3 v00 = field.VertexPosition(x, z);
                Vector3 v10 = field.VertexPosition(x + 1, z);
                Vector3 v01 = field.VertexPosition(x, z + 1);
                Vector3 v11 = field.VertexPosition(x + 1, z + 1);

                // Diagonal v00-v11 everywhere - IslandHeightField.SampleHeight assumes it.
                AddTriangle(v00, v11, v10);
                AddTriangle(v00, v01, v11);
            }

            if (vertices.Count == 0) return null;

            var mesh = new Mesh { name = $"Island_{field.Seed}_{x0}_{z0}", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetColors(colors);
            mesh.SetTriangles(Sequence(vertices.Count), 0);
            mesh.RecalculateBounds();
            return mesh;

            void AddTriangle(Vector3 a, Vector3 b, Vector3 c)
            {
                // The sea plane hides the deep floor; skipping it saves most of the outer ring.
                if (a.y < deepFloor && b.y < deepFloor && c.y < deepFloor) return;

                Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
                Vector3 centre = (a + b + c) / 3f;
                Color color = TriangleColor(centre, normal);

                vertices.Add(a); vertices.Add(b); vertices.Add(c);
                normals.Add(normal); normals.Add(normal); normals.Add(normal);
                colors.Add(color); colors.Add(color); colors.Add(color);
            }

            Color TriangleColor(Vector3 centre, Vector3 normal)
            {
                float fresh = field.FreshWaterLevel(centre.x, centre.z);
                Color color;
                if (centre.y < s.SeaLevel - 0.2f || centre.y < fresh - 0.3f) color = biome.SeaBed;
                else if (centre.y < s.BeachHeight + 0.45f || centre.y < fresh + 0.6f) color = biome.Sand;
                else if (normal.y < grassMinNormalY) color = biome.Rock;
                else
                {
                    float patch = patches.Fbm(centre.x / 22f, centre.z / 22f, 2);
                    color = Color.Lerp(biome.Grass, biome.GrassDark, Mathf.SmoothStep(0f, 1f, (patch - 0.35f) * 3f));
                }

                // Small per-facet brightness jitter keeps flat areas from reading as one plastic sheet.
                float jitter = 0.97f + 0.06f * Hash(centre);
                return new Color(color.r * jitter, color.g * jitter, color.b * jitter, 1f);
            }
        }

        /// <summary>Shared-vertex collider for the same cell range as <see cref="BuildTerrainChunk"/>.</summary>
        public static Mesh BuildColliderChunk(IslandHeightField field, int x0, int z0, int x1, int z1)
        {
            int w = x1 - x0 + 1;
            int h = z1 - z0 + 1;
            var vertices = new Vector3[w * h];
            for (int z = 0; z < h; z++)
            for (int x = 0; x < w; x++)
                vertices[z * w + x] = field.VertexPosition(x0 + x, z0 + z);

            var triangles = new int[(w - 1) * (h - 1) * 6];
            int t = 0;
            for (int z = 0; z < h - 1; z++)
            for (int x = 0; x < w - 1; x++)
            {
                int i00 = z * w + x;
                int i10 = i00 + 1;
                int i01 = i00 + w;
                int i11 = i01 + 1;
                triangles[t++] = i00; triangles[t++] = i11; triangles[t++] = i10;
                triangles[t++] = i00; triangles[t++] = i01; triangles[t++] = i11;
            }

            var mesh = new Mesh { name = $"IslandCollider_{field.Seed}_{x0}_{z0}", indexFormat = IndexFormat.UInt32 };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Water over the terrain grid: sea near the coast, lakes and rivers in one mesh. Each quad
        /// has its own vertices at the water level of its wet corners, so a drop in a river becomes
        /// a sloped quad - a waterfall. Vertex colour carries what <c>Leeway/ToonWater</c> needs:
        /// r = depth below the surface (0..1 over <paramref name="depthRange"/>), g = 1 for fresh water.
        /// </summary>
        public static Mesh BuildWaterSurface(IslandHeightField field, float depthRange = 3f)
        {
            IslandSettings s = field.Settings;
            int n = field.VertexCount;
            var level = new float[n * n];
            var fresh = new bool[n * n];
            for (int z = 0; z < n; z++)
            for (int x = 0; x < n; x++)
            {
                float freshLevel = field.FreshWaterAt(x, z);
                fresh[z * n + x] = freshLevel > s.SeaLevel;
                level[z * n + x] = Mathf.Max(s.SeaLevel, freshLevel);
            }

            var vertices = new List<Vector3>();
            var colors = new List<Color>();
            var triangles = new List<int>();
            var corner = new int[4];

            for (int z = 0; z < n - 1; z++)
            for (int x = 0; x < n - 1; x++)
            {
                corner[0] = z * n + x;
                corner[1] = z * n + x + 1;
                corner[2] = (z + 1) * n + x;
                corner[3] = (z + 1) * n + x + 1;

                // Dry corners take the highest wet corner level; the excess hides under the bank.
                float wetMax = float.NegativeInfinity;
                foreach (int i in corner)
                    if (field.HeightAt(i % n, i / n) < level[i]) wetMax = Mathf.Max(wetMax, level[i]);
                if (float.IsNegativeInfinity(wetMax)) continue;

                int start = vertices.Count;
                foreach (int i in corner)
                {
                    float ground = field.HeightAt(i % n, i / n);
                    float surface = ground < level[i] ? level[i] : wetMax;
                    Vector3 p = field.VertexPosition(i % n, i / n);
                    p.y = surface;
                    vertices.Add(p);
                    colors.Add(new Color(Mathf.Clamp01((surface - ground) / depthRange), fresh[i] ? 1f : 0f, 0f, 1f));
                }

                // Corners are 00, 10, 01, 11 - same diagonal as the terrain.
                triangles.Add(start); triangles.Add(start + 3); triangles.Add(start + 1);
                triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
            }

            if (vertices.Count == 0) return null;

            var mesh = new Mesh { name = $"IslandWater_{field.Seed}", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Open sea around the grid: a square ring, so it never overlaps <see cref="BuildWaterSurface"/>.</summary>
        public static Mesh BuildSeaRing(float outerSize, float innerSize, float height)
        {
            float o = outerSize * 0.5f;
            float i = innerSize * 0.5f;
            var mesh = new Mesh { name = "IslandOpenSea" };
            mesh.vertices = new[]
            {
                new Vector3(-o, height, -o), new Vector3(o, height, -o), new Vector3(o, height, o), new Vector3(-o, height, o),
                new Vector3(-i, height, -i), new Vector3(i, height, -i), new Vector3(i, height, i), new Vector3(-i, height, i),
            };
            var normals = new Vector3[8];
            var colors = new Color[8];
            for (int k = 0; k < 8; k++)
            {
                normals[k] = Vector3.up;
                colors[k] = new Color(1f, 0f, 0f, 1f); // deep salt water
            }
            mesh.normals = normals;
            mesh.colors = colors;
            mesh.triangles = new[]
            {
                0, 4, 1, 1, 4, 5,
                1, 5, 2, 2, 5, 6,
                2, 6, 3, 3, 6, 7,
                3, 7, 0, 0, 7, 4,
            };
            mesh.RecalculateBounds();
            return mesh;
        }

        private static int[] Sequence(int count)
        {
            var indices = new int[count];
            for (int i = 0; i < count; i++) indices[i] = i;
            return indices;
        }

        private static float Hash(Vector3 p)
        {
            float h = Mathf.Sin(p.x * 12.9898f + p.z * 78.233f + p.y * 37.719f) * 43758.5453f;
            return h - Mathf.Floor(h);
        }
    }
}
