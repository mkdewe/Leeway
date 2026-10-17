using System;
using System.Collections.Generic;
using System.IO;
using Leeway.World.Domain;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;
using Random = System.Random;

namespace Leeway.World.Editor
{
    /// <summary>
    /// Leeway -> World -> Build Placeholder Island Assets: generates low-poly placeholder props
    /// (trees, rocks, plants, a cave, the hub's standing stones), their Toon materials and the
    /// "Coast" biome, so the island generator has content before the artist's asset set exists.
    /// Rerunning overwrites the meshes, prefabs, materials and the biome's layers; the biome's
    /// island shape is kept.
    /// </summary>
    public static class IslandPlaceholderAssets
    {
        private const string ToonShaderPath = "Assets/_Project/Core/Rendering/Shaders/Toon.shader";
        private const string WaterShaderPath = "Assets/_Project/Core/Rendering/Shaders/ToonWater.shader";
        private const string MeshDir = "Assets/_Project/Art/Models/Placeholder";
        private const string MaterialDir = "Assets/_Project/Art/Materials/World";
        private const string PrefabDir = "Assets/_Project/Features/World/Prefabs/Props";
        private const string BiomePath = "Assets/_Project/Features/World/Data/Biome_Coast.asset";

        private sealed class Palette
        {
            public Material Ground, Water, Bark, Pine, Spruce, Leaf, Rock, Moss, CaveRock, Bush, Grass, Fern,
                ShroomStem, ShroomCap, Crystal;
        }

        [MenuItem("Leeway/World/Build Placeholder Island Assets")]
        public static void Build()
        {
            foreach (string dir in new[] { MeshDir, MaterialDir, PrefabDir, Path.GetDirectoryName(BiomePath) })
                Directory.CreateDirectory(dir);

            var m = new Palette
            {
                Ground = ToonMaterial("Island_Ground", Color.white, vertexColor: true),
                Water = WaterMaterial("Island_Water"),
                Bark = ToonMaterial("Prop_Bark", new Color32(0x3E, 0x3A, 0x36, 0xFF)),
                Pine = ToonMaterial("Prop_Pine", new Color32(0x24, 0x47, 0x3C, 0xFF)),
                Spruce = ToonMaterial("Prop_Spruce", new Color32(0x1F, 0x3D, 0x3A, 0xFF)),
                Leaf = ToonMaterial("Prop_Leaf", new Color32(0x4C, 0x7A, 0x55, 0xFF)),
                Rock = ToonMaterial("Prop_Rock", new Color32(0x6B, 0x77, 0x80, 0xFF)),
                Moss = ToonMaterial("Prop_Moss", new Color32(0x4F, 0x76, 0x58, 0xFF)),
                CaveRock = ToonMaterial("Prop_CaveRock", new Color32(0x4E, 0x58, 0x61, 0xFF), doubleSided: true),
                Bush = ToonMaterial("Prop_Bush", new Color32(0x3F, 0x6B, 0x55, 0xFF)),
                Grass = ToonMaterial("Prop_Grass", new Color32(0x6E, 0x9B, 0x74, 0xFF), doubleSided: true),
                Fern = ToonMaterial("Prop_Fern", new Color32(0x3B, 0x6E, 0x52, 0xFF), doubleSided: true),
                ShroomStem = ToonMaterial("Prop_ShroomStem", new Color32(0xC9, 0xBF, 0xA8, 0xFF)),
                // HDR emission: PaletteGrade keeps saturated, bright pixels as accents and bloom picks them up.
                ShroomCap = ToonMaterial("Prop_ShroomCap", new Color32(0xE0, 0x7A, 0x2E, 0xFF), emission: new Color(2.4f, 0.95f, 0.25f)),
                Crystal = ToonMaterial("Prop_Crystal", new Color32(0x5F, 0xE0, 0xC8, 0xFF), emission: new Color(0.35f, 1.9f, 1.6f)),
            };

            var layers = new List<IslandBiome.ScatterLayer>
            {
                Layer("Pines", Variants(3, i => Prefab($"Pine_{i}", PineMesh(i), new[] { m.Bark, m.Pine }, Trunk(0.3f, 5f))),
                    new ScatterRule(1.3f, 1.2f, 60f, 30f, 6f, 0.5f, 34f, 0.8f, 1.4f), 0f, 0.2f),
                Layer("Spruces", Variants(2, i => Prefab($"Spruce_{i}", SpruceMesh(i), new[] { m.Bark, m.Spruce }, Trunk(0.3f, 7f))),
                    new ScatterRule(0.6f, 8f, 60f, 32f, 6f, 0.45f, 30f, 0.9f, 1.3f), 0f, 0.2f),
                Layer("Broadleaf", Variants(2, i => Prefab($"Broadleaf_{i}", BroadleafMesh(i), new[] { m.Bark, m.Leaf }, Trunk(0.35f, 3f))),
                    new ScatterRule(0.45f, 0.9f, 10f, 25f, 4f, 0.45f, 26f, 0.8f, 1.3f), 0f, 0.15f),
                Layer("DeadTrees", Variants(2, i => Prefab($"DeadTree_{i}", DeadTreeMesh(i), new[] { m.Bark }, Trunk(0.25f, 4f))),
                    new ScatterRule(0.05f, 2f, 60f, 35f, 8f, 0f, 1f, 0.8f, 1.2f), 0f, 0.1f),
                Layer("Stumps", new[] { Prefab("Stump_0", StumpMesh(), new[] { m.Bark, m.Moss }, Trunk(0.4f, 0.8f)) },
                    new ScatterRule(0.08f, 1f, 40f, 30f, 3f, 0.45f, 34f, 0.8f, 1.3f), 0.3f, 0.05f),
                Layer("Boulders", Variants(3, i => Prefab($"Rock_{i}", RockMesh(i), new[] { m.Rock }, ConvexMesh)),
                    new ScatterRule(0.35f, -1f, 60f, 70f, 2f, 0.3f, 30f, 0.6f, 2.2f), 0.6f, 0.25f),
                Layer("MossyBoulders", Variants(2, i => Prefab($"MossyRock_{i}", MossyRockMesh(i), new[] { m.Rock, m.Moss }, ConvexMesh)),
                    new ScatterRule(0.3f, 1f, 40f, 40f, 2f, 0.5f, 34f, 0.8f, 2f), 0.5f, 0.25f),
                Layer("Spires", Variants(2, i => Prefab($"Spire_{i}", SpireMesh(i), new[] { m.Rock, m.Moss }, ConvexMesh)),
                    new ScatterRule(0.06f, 6f, 60f, 60f, 10f, 0.55f, 40f, 0.8f, 1.6f), 0.2f, 0.5f),
                Layer("ShoreSlabs", Variants(2, i => Prefab($"Slab_{i}", SlabMesh(i), new[] { m.Rock }, ConvexMesh)),
                    new ScatterRule(0.2f, -0.8f, 2f, 30f, 2f, 0.3f, 20f, 0.8f, 1.6f), 0.9f, 0.15f),
                Layer("Pebbles", new[] { Prefab("Pebbles_0", PebblesMesh(), new[] { m.Rock }, null) },
                    new ScatterRule(0.8f, -0.5f, 1.5f, 30f, 0f, 0.4f, 12f, 0.8f, 1.4f), 1f, 0.02f),
                Layer("Bushes", Variants(2, i => Prefab($"Bush_{i}", BushMesh(i), new[] { m.Bush }, null)),
                    new ScatterRule(1f, 0.9f, 40f, 35f, 1f, 0.42f, 18f, 0.7f, 1.4f), 0.3f, 0.15f),
                Layer("Ferns", Variants(2, i => Prefab($"Fern_{i}", FernMesh(i), new[] { m.Fern }, null, castShadows: false)),
                    new ScatterRule(1.6f, 1f, 40f, 35f, 1f, 0.5f, 34f, 0.7f, 1.3f), 0.6f, 0.02f),
                Layer("Grass", Variants(2, i => Prefab($"Grass_{i}", GrassMesh(i), new[] { m.Grass }, null, castShadows: false)),
                    new ScatterRule(5f, 0.8f, 40f, 35f, -12f, 0.4f, 10f, 0.8f, 1.4f), 0.8f, 0.02f),
                Layer("GlowShrooms", Variants(2, i => Prefab($"GlowShroom_{i}", ShroomMesh(i), new[] { m.ShroomStem, m.ShroomCap }, null, castShadows: false)),
                    new ScatterRule(0.12f, 1f, 40f, 25f, 6f, 0.62f, 34f, 0.8f, 1.5f), 0.7f, 0.02f),
                Layer("Logs", new[] { Prefab("Log_0", LogMesh(), new[] { m.Bark, m.Moss }, go =>
                    {
                        var capsule = go.AddComponent<CapsuleCollider>();
                        capsule.direction = 0;
                        capsule.center = new Vector3(0f, 0.3f, 0f);
                        capsule.radius = 0.3f;
                        capsule.height = 4f;
                    }) },
                    new ScatterRule(0.06f, 0.7f, 30f, 20f, 3f, 0f, 1f, 0.8f, 1.3f), 0.9f, 0.1f),
            };

            GameObject cave = CavePrefab(m);
            GameObject[] hubStones = Variants(2, i => Prefab($"StandingStone_{i}", StandingStoneMesh(i), new[] { m.Rock, m.Moss }, ConvexMesh));

            var biome = AssetDatabase.LoadAssetAtPath<IslandBiome>(BiomePath);
            if (biome == null)
            {
                biome = ScriptableObject.CreateInstance<IslandBiome>();
                AssetDatabase.CreateAsset(biome, BiomePath);
            }

            biome.EditorSetup(m.Ground, m.Water, layers, cave, hubStones);
            EditorUtility.SetDirty(biome);
            AssetDatabase.SaveAssets();
            Debug.Log($"[IslandPlaceholderAssets] Built {layers.Count} prop layers, the cave and {BiomePath}.");
        }

        // ---------------------------------------------------------------- trees

        private static Mesh PineMesh(int variant)
        {
            var rng = new Random(100 + variant);
            var mesh = new LowPolyMeshes(2);
            float height = 6.5f + variant * 0.6f;
            mesh.Frustum(0, Vector3.zero, 5, 0.24f, 0.12f, height * 0.75f);
            Tiers(mesh, rng, tiers: 3, firstY: 1.3f, lastY: height * 0.62f, baseRadius: 1.7f, topRadius: 0.75f, tierHeight: 2.6f);
            return SaveMesh(mesh.ToMesh($"Pine_{variant}"));
        }

        private static Mesh SpruceMesh(int variant)
        {
            var rng = new Random(150 + variant);
            var mesh = new LowPolyMeshes(2);
            float height = 9.5f + variant * 1.2f;
            mesh.Frustum(0, Vector3.zero, 5, 0.26f, 0.1f, height * 0.85f);
            Tiers(mesh, rng, tiers: 5, firstY: 1.6f, lastY: height * 0.72f, baseRadius: 1.35f, topRadius: 0.45f, tierHeight: 2.4f);
            return SaveMesh(mesh.ToMesh($"Spruce_{variant}"));
        }

        private static void Tiers(LowPolyMeshes mesh, Random rng, int tiers, float firstY, float lastY, float baseRadius, float topRadius, float tierHeight)
        {
            for (int t = 0; t < tiers; t++)
            {
                float k = tiers == 1 ? 0f : t / (float)(tiers - 1);
                float radius = Mathf.Lerp(baseRadius, topRadius, k) * (0.9f + 0.2f * (float)rng.NextDouble());
                mesh.Frustum(1, new Vector3(0f, Mathf.Lerp(firstY, lastY, k), 0f), 7, radius, 0f,
                    tierHeight * Mathf.Lerp(1f, 0.85f, k), (float)rng.NextDouble() * 6.28f);
            }
        }

        private static Mesh BroadleafMesh(int variant)
        {
            var rng = new Random(170 + variant);
            var mesh = new LowPolyMeshes(2);
            float trunk = 2.6f + variant * 0.5f;
            mesh.Frustum(0, Vector3.zero, 6, 0.32f, 0.18f, trunk + 0.6f);
            // A tilted branch under the crown keeps the silhouette from reading as a lollipop.
            mesh.Frustum(0, new Vector3(0f, trunk * 0.7f, 0f), 5, 0.14f, 0.07f, 1.5f, 0f, Quaternion.Euler(0f, 0f, -48f));
            mesh.Blob(1, new Vector3(0f, trunk + 1.4f, 0f), new Vector3(2.2f, 1.6f, 2.1f), 0.2f, rng);
            mesh.Blob(1, new Vector3(1.3f, trunk + 0.7f, 0.4f), new Vector3(1.4f, 1.1f, 1.3f), 0.2f, rng);
            mesh.Blob(1, new Vector3(-0.9f, trunk + 0.9f, -0.8f), new Vector3(1.5f, 1.2f, 1.4f), 0.2f, rng);
            return SaveMesh(mesh.ToMesh($"Broadleaf_{variant}"));
        }

        private static Mesh DeadTreeMesh(int variant)
        {
            var rng = new Random(190 + variant);
            var mesh = new LowPolyMeshes(1);
            float height = 5f + variant;
            mesh.Frustum(0, Vector3.zero, 5, 0.28f, 0.06f, height);
            for (int i = 0; i < 4; i++)
            {
                float y = height * (0.35f + 0.15f * i);
                float yaw = (float)rng.NextDouble() * 360f;
                float tilt = 40f + (float)rng.NextDouble() * 30f;
                mesh.Frustum(0, new Vector3(0f, y, 0f), 4, 0.11f, 0.02f, 1.2f + (float)rng.NextDouble() * 1.2f, 0f,
                    Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(0f, 0f, -tilt));
            }
            return SaveMesh(mesh.ToMesh($"DeadTree_{variant}"));
        }

        private static Mesh StumpMesh()
        {
            var mesh = new LowPolyMeshes(2);
            mesh.Frustum(0, Vector3.zero, 7, 0.55f, 0.42f, 0.7f, 0.3f);
            mesh.Frustum(1, new Vector3(0f, 0.7f, 0f), 7, 0.42f, 0.3f, 0.06f, 0.3f);
            return SaveMesh(mesh.ToMesh("Stump_0"));
        }

        private static Mesh LogMesh()
        {
            var mesh = new LowPolyMeshes(2);
            mesh.Frustum(0, new Vector3(-2f, 0.3f, 0f), 6, 0.32f, 0.27f, 4f, 0f, Quaternion.Euler(0f, 0f, -90f));
            mesh.Blob(1, new Vector3(-0.4f, 0.58f, 0f), new Vector3(0.9f, 0.12f, 0.28f), 0.2f, new Random(7));
            return SaveMesh(mesh.ToMesh("Log_0"));
        }

        // ---------------------------------------------------------------- rocks

        private static Mesh RockMesh(int variant)
        {
            var mesh = new LowPolyMeshes(1);
            mesh.Blob(0, new Vector3(0f, 0.35f, 0f), new Vector3(1f, 0.62f, 0.85f), 0.28f, new Random(200 + variant));
            return SaveMesh(mesh.ToMesh($"Rock_{variant}"));
        }

        private static Mesh MossyRockMesh(int variant)
        {
            var mesh = new LowPolyMeshes(2);
            mesh.Blob(0, new Vector3(0f, 0.5f, 0f), new Vector3(1.2f, 0.85f, 1f), 0.3f, new Random(220 + variant), topSubmesh: 1, topMinNormalY: 0.5f);
            return SaveMesh(mesh.ToMesh($"MossyRock_{variant}"));
        }

        private static Mesh SpireMesh(int variant)
        {
            var rng = new Random(240 + variant);
            var mesh = new LowPolyMeshes(2);
            float y = -0.5f;
            float radius = 1.4f + variant * 0.3f;
            Quaternion lean = Quaternion.identity;
            for (int i = 0; i < 3; i++)
            {
                float h = 2.6f + (float)rng.NextDouble() * 1.4f;
                float top = radius * (0.6f + 0.15f * (float)rng.NextDouble());
                lean *= Quaternion.Euler(((float)rng.NextDouble() - 0.5f) * 12f, 0f, ((float)rng.NextDouble() - 0.5f) * 12f);
                mesh.Frustum(0, new Vector3(0f, y, 0f), 6, radius, top, h, (float)rng.NextDouble() * 6.28f, lean);
                y += h * 0.95f;
                radius = top;
            }
            mesh.Blob(1, new Vector3(0f, y - 0.1f, 0f), new Vector3(radius * 1.15f, 0.35f, radius * 1.15f), 0.2f, rng);
            return SaveMesh(mesh.ToMesh($"Spire_{variant}"));
        }

        private static Mesh SlabMesh(int variant)
        {
            var mesh = new LowPolyMeshes(1);
            mesh.Blob(0, new Vector3(0f, 0.15f, 0f), new Vector3(1.7f, 0.38f, 1.15f), 0.22f, new Random(260 + variant));
            return SaveMesh(mesh.ToMesh($"Slab_{variant}"));
        }

        private static Mesh PebblesMesh()
        {
            var rng = new Random(280);
            var mesh = new LowPolyMeshes(1);
            for (int i = 0; i < 6; i++)
            {
                var c = new Vector3(((float)rng.NextDouble() - 0.5f) * 1.4f, 0.06f, ((float)rng.NextDouble() - 0.5f) * 1.4f);
                float s = 0.12f + (float)rng.NextDouble() * 0.16f;
                mesh.Blob(0, c, new Vector3(s, s * 0.6f, s * 0.85f), 0.25f, rng);
            }
            return SaveMesh(mesh.ToMesh("Pebbles_0"));
        }

        private static Mesh StandingStoneMesh(int variant)
        {
            var mesh = new LowPolyMeshes(2);
            mesh.Blob(0, new Vector3(0f, 1.2f, 0f), new Vector3(0.65f, 1.9f, 0.4f), 0.18f, new Random(300 + variant), topSubmesh: 1, topMinNormalY: 0.75f);
            return SaveMesh(mesh.ToMesh($"StandingStone_{variant}"));
        }

        // ---------------------------------------------------------------- plants

        private static Mesh BushMesh(int variant)
        {
            var rng = new Random(320 + variant);
            var mesh = new LowPolyMeshes(1);
            mesh.Blob(0, new Vector3(0f, 0.45f, 0f), new Vector3(0.9f, 0.6f, 0.9f), 0.2f, rng);
            mesh.Blob(0, new Vector3(0.55f, 0.3f, 0.2f), new Vector3(0.55f, 0.4f, 0.55f), 0.2f, rng);
            return SaveMesh(mesh.ToMesh($"Bush_{variant}"));
        }

        private static Mesh GrassMesh(int variant)
        {
            var rng = new Random(400 + variant);
            var mesh = new LowPolyMeshes(1);
            for (int i = 0; i < 6; i++)
            {
                float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                Vector3 root = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (0.15f * (float)rng.NextDouble());
                Vector3 side = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f) * Vector3.right * 0.06f;
                Vector3 lean = new Vector3((float)rng.NextDouble() - 0.5f, 0f, (float)rng.NextDouble() - 0.5f) * 0.35f;
                Vector3 tip = root + Vector3.up * (0.45f + 0.35f * (float)rng.NextDouble()) + lean;
                mesh.Triangle(0, root - side, tip, root + side);
            }
            return SaveMesh(mesh.ToMesh($"Grass_{variant}"));
        }

        private static Mesh FernMesh(int variant)
        {
            var rng = new Random(420 + variant);
            var mesh = new LowPolyMeshes(1);
            int fronds = 7 + variant * 2;
            for (int i = 0; i < fronds; i++)
            {
                float yaw = i * 360f / fronds + (float)rng.NextDouble() * 20f;
                Quaternion q = Quaternion.Euler(0f, yaw, 0f);
                float length = 0.8f + (float)rng.NextDouble() * 0.5f;
                Vector3 mid = q * new Vector3(0f, 0.45f, length * 0.5f);
                Vector3 tip = q * new Vector3(0f, 0.15f, length);
                Vector3 side = q * new Vector3(0.16f, 0.05f, 0f);
                mesh.Triangle(0, Vector3.zero, mid + side, mid - side);
                mesh.Triangle(0, mid - side, mid + side, tip);
            }
            return SaveMesh(mesh.ToMesh($"Fern_{variant}"));
        }

        private static Mesh ShroomMesh(int variant)
        {
            var rng = new Random(440 + variant);
            var mesh = new LowPolyMeshes(2);
            int count = 3 + variant;
            for (int i = 0; i < count; i++)
            {
                var root = new Vector3(((float)rng.NextDouble() - 0.5f) * 0.6f, 0f, ((float)rng.NextDouble() - 0.5f) * 0.6f);
                float h = 0.18f + (float)rng.NextDouble() * 0.3f;
                float cap = 0.08f + (float)rng.NextDouble() * 0.1f;
                mesh.Frustum(0, root, 5, 0.035f, 0.03f, h);
                mesh.Frustum(1, root + Vector3.up * h, 6, cap, 0f, cap * 0.6f, (float)rng.NextDouble() * 6f);
            }
            return SaveMesh(mesh.ToMesh($"GlowShroom_{variant}"));
        }

        // ---------------------------------------------------------------- landmarks

        private static GameObject CavePrefab(Palette m)
        {
            var rng = new Random(500);
            var shellMesh = new LowPolyMeshes(1);
            shellMesh.CaveShell(0, radius: 7f, height: 5.6f, segments: 14, rings: 5, openingHalfAngle: 38f, openRings: 3, rng);
            Mesh shell = SaveMesh(shellMesh.ToMesh("CaveShell_0"));

            var crystalMesh = new LowPolyMeshes(1);
            for (int i = 0; i < 9; i++)
            {
                var root = new Vector3(((float)rng.NextDouble() - 0.5f) * 5f, -0.2f, -2.5f - (float)rng.NextDouble() * 3f);
                Quaternion tilt = Quaternion.Euler(((float)rng.NextDouble() - 0.5f) * 50f, (float)rng.NextDouble() * 360f, ((float)rng.NextDouble() - 0.5f) * 50f);
                crystalMesh.Frustum(0, root, 4, 0.18f + (float)rng.NextDouble() * 0.15f, 0f, 0.8f + (float)rng.NextDouble() * 1.4f, 0f, tilt);
            }
            Mesh crystals = SaveMesh(crystalMesh.ToMesh("CaveCrystals_0"));

            var go = new GameObject("Cave_0");
            var shellGo = Child(go, "Shell", shell, new[] { m.CaveRock });
            shellGo.AddComponent<MeshCollider>().sharedMesh = shell;
            Child(go, "Crystals", crystals, new[] { m.Crystal }).GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

            var light = new GameObject("CrystalLight").AddComponent<Light>();
            light.transform.SetParent(go.transform, false);
            light.transform.localPosition = new Vector3(0f, 1.6f, -2.5f);
            light.type = LightType.Point;
            light.range = 10f;
            light.intensity = 4f;
            light.color = new Color(0.45f, 1f, 0.9f);
            light.shadows = LightShadows.None;

            return SavePrefab(go, "Cave_0");
        }

        // ---------------------------------------------------------------- helpers

        private static IslandBiome.ScatterLayer Layer(string name, GameObject[] prefabs, ScatterRule rule, float align, float sink)
            => new(name, prefabs, rule, align, sink);

        private static GameObject[] Variants(int count, Func<int, GameObject> make)
        {
            var result = new GameObject[count];
            for (int i = 0; i < count; i++) result[i] = make(i);
            return result;
        }

        private static Action<GameObject> Trunk(float radius, float height) => go =>
        {
            var capsule = go.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, height * 0.5f, 0f);
            capsule.radius = radius;
            capsule.height = Mathf.Max(height, radius * 2f);
        };

        private static void ConvexMesh(GameObject go) => go.AddComponent<MeshCollider>().convex = true;

        private static Mesh SaveMesh(Mesh mesh)
        {
            string path = $"{MeshDir}/{mesh.name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }
            // Overwrite in place so prefabs keep their reference.
            EditorUtility.CopySerialized(mesh, existing);
            Object.DestroyImmediate(mesh);
            return existing;
        }

        private static Material ToonMaterial(string name, Color color, bool vertexColor = false, bool doubleSided = false,
            float rim = 0.35f, Color? emission = null)
        {
            string path = $"{MaterialDir}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(AssetDatabase.LoadAssetAtPath<Shader>(ToonShaderPath));
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetColor("_BaseColor", color);
            material.SetColor("_EmissionColor", emission ?? Color.black);
            material.SetFloat("_RimStrength", rim);
            material.SetFloat("_UseVertexColor", vertexColor ? 1f : 0f);
            if (vertexColor) material.EnableKeyword("_VERTEX_COLOR");
            else material.DisableKeyword("_VERTEX_COLOR");
            material.SetFloat("_Cull", doubleSided ? 0f : 2f);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material WaterMaterial(string name)
        {
            string path = $"{MaterialDir}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(AssetDatabase.LoadAssetAtPath<Shader>(WaterShaderPath));
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = AssetDatabase.LoadAssetAtPath<Shader>(WaterShaderPath);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject Prefab(string name, Mesh mesh, Material[] materials, Action<GameObject> addCollider,
            bool castShadows = true)
        {
            var go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = materials;
            if (!castShadows) renderer.shadowCastingMode = ShadowCastingMode.Off;
            addCollider?.Invoke(go);
            return SavePrefab(go, name);
        }

        private static GameObject Child(GameObject parent, string name, Mesh mesh, Material[] materials)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = materials;
            return go;
        }

        private static GameObject SavePrefab(GameObject go, string name)
        {
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabDir}/{name}.prefab");
            Object.DestroyImmediate(go);
            return prefab;
        }
    }
}
