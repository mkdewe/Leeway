using System.IO;
using Leeway.World.Domain;
using UnityEditor;
using UnityEngine;
using Random = System.Random;

namespace Leeway.World.Editor
{
    /// <summary>
    /// Builds the edible plants: low-poly models for a berry bush, a fruit tree, mushrooms, a healing herb
    /// and a poisonous cap, their prefabs, and the <see cref="FloraSet"/> that points at them.
    /// </summary>
    /// <remarks>
    /// Like the other island props these are placeholders in the same low-poly language, meant to be
    /// replaced by modelled ones: a prefab only has to keep its plant and put whatever is picked in a child
    /// called <c>Fruit</c>. Rerunning overwrites the meshes in place, so prefabs keep their references.
    /// </remarks>
    public static class FloraAssets
    {
        private const string ToonShaderPath = "Assets/_Project/Core/Rendering/Shaders/Toon.shader";
        private const string MeshDir = "Assets/_Project/Art/Models/Placeholder";
        private const string MaterialDir = "Assets/_Project/Art/Materials/World";
        private const string PrefabDir = "Assets/_Project/Features/World/Prefabs/Flora";
        private const string SetPath = "Assets/_Project/Features/World/Data/FloraSet.asset";

        [MenuItem("Leeway/World/Build Flora Assets")]
        public static void Build()
        {
            foreach (string dir in new[] { MeshDir, MaterialDir, PrefabDir, Path.GetDirectoryName(SetPath) })
                Directory.CreateDirectory(dir);

            Material leaf = Existing("Prop_Leaf");
            Material bark = Existing("Prop_Bark");
            Material bush = Existing("Prop_Bush");
            Material moss = Existing("Prop_Moss");

            Material berry = Toon("Flora_Berry", new Color32(0xC2, 0x2F, 0x4D, 0xFF), emission: new Color(0.9f, 0.1f, 0.2f));
            Material fruit = Toon("Flora_Fruit", new Color32(0xE8, 0x9A, 0x2B, 0xFF), emission: new Color(1.1f, 0.5f, 0.05f));
            Material stem = Toon("Flora_MushroomStem", new Color32(0xD9, 0xCF, 0xB8, 0xFF));
            Material cap = Toon("Flora_MushroomCap", new Color32(0x8C, 0x5A, 0x3A, 0xFF));
            Material deathCap = Toon("Flora_DeathCap", new Color32(0xC8, 0x2E, 0x2E, 0xFF), emission: new Color(0.8f, 0.08f, 0.08f));
            Material spot = Toon("Flora_DeathCapSpot", new Color32(0xF3, 0xEC, 0xDD, 0xFF));
            Material flower = Toon("Flora_HerbFlower", new Color32(0x6C, 0x8B, 0xE8, 0xFF), emission: new Color(0.2f, 0.4f, 1.1f));
            Material herbLeaf = Toon("Flora_HerbLeaf", new Color32(0x5A, 0x9A, 0x66, 0xFF), doubleSided: true);

            var prefabs = new GameObject[System.Enum.GetValues(typeof(FloraKind)).Length];

            prefabs[(int)FloraKind.BerryBush] = Plant("Flora_BerryBush", BerryBushBody(), new[] { bush }, BerryBushFruit(), new[] { berry }, null);
            prefabs[(int)FloraKind.FruitTree] = Plant("Flora_FruitTree", FruitTreeBody(), new[] { bark, leaf }, FruitTreeFruit(), new[] { fruit },
                go => { var c = go.AddComponent<CapsuleCollider>(); c.radius = 0.25f; c.height = 2.2f; c.center = new Vector3(0f, 1.1f, 0f); });
            prefabs[(int)FloraKind.Mushroom] = Plant("Flora_Mushroom", MossMound("Flora_MushroomMound"), new[] { moss }, MushroomFruit("Flora_Mushrooms", false), new[] { stem, cap }, null);
            prefabs[(int)FloraKind.Herb] = Plant("Flora_Herb", HerbBody(), new[] { herbLeaf }, HerbFruit(), new[] { herbLeaf, flower }, null);
            prefabs[(int)FloraKind.DeathCap] = Plant("Flora_DeathCap", MossMound("Flora_DeathCapMound"), new[] { moss }, MushroomFruit("Flora_DeathCaps", true), new[] { stem, deathCap, spot }, null);

            var set = AssetDatabase.LoadAssetAtPath<FloraSet>(SetPath);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<FloraSet>();
                AssetDatabase.CreateAsset(set, SetPath);
            }

            set.EditorSet(prefabs);
            EditorUtility.SetDirty(set);

            AssetDatabase.SaveAssets();
            foreach (GameObject prefab in prefabs) AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(prefab), ImportAssetOptions.ForceUpdate);
            AssetDatabase.Refresh();
            Debug.Log("Flora assets built: " + SetPath);
        }

        // ---------------------------------------------------------------- models

        private static Mesh BerryBushBody()
        {
            var rng = new Random(601);
            var mesh = new LowPolyMeshes(1);
            mesh.Blob(0, new Vector3(0f, 0.5f, 0f), new Vector3(0.95f, 0.65f, 0.95f), 0.2f, rng);
            mesh.Blob(0, new Vector3(0.6f, 0.32f, 0.25f), new Vector3(0.55f, 0.4f, 0.55f), 0.2f, rng);
            mesh.Blob(0, new Vector3(-0.5f, 0.3f, -0.3f), new Vector3(0.5f, 0.38f, 0.5f), 0.2f, rng);
            return Save(mesh.ToMesh("Flora_BerryBush"));
        }

        private static Mesh BerryBushFruit()
        {
            var rng = new Random(602);
            var mesh = new LowPolyMeshes(1);
            for (int i = 0; i < 9; i++)
            {
                float a = i * 2.399963f;
                float h = 0.35f + 0.5f * (float)rng.NextDouble();
                Vector3 p = new Vector3(Mathf.Cos(a) * 0.9f, h, Mathf.Sin(a) * 0.9f) * (0.92f + 0.12f * (float)rng.NextDouble());
                mesh.Blob(0, p, Vector3.one * 0.075f, 0.1f, rng);
            }

            return Save(mesh.ToMesh("Flora_BerryBushFruit"));
        }

        private static Mesh FruitTreeBody()
        {
            var rng = new Random(611);
            var mesh = new LowPolyMeshes(2);
            mesh.Frustum(0, Vector3.zero, 6, 0.26f, 0.15f, 2.1f);
            mesh.Blob(1, new Vector3(0f, 2.9f, 0f), new Vector3(1.6f, 1.15f, 1.6f), 0.18f, rng);
            mesh.Blob(1, new Vector3(0.8f, 2.4f, 0.3f), new Vector3(0.9f, 0.7f, 0.9f), 0.2f, rng);
            mesh.Blob(1, new Vector3(-0.7f, 2.5f, -0.4f), new Vector3(0.9f, 0.7f, 0.9f), 0.2f, rng);
            return Save(mesh.ToMesh("Flora_FruitTree"));
        }

        private static Mesh FruitTreeFruit()
        {
            var rng = new Random(612);
            var mesh = new LowPolyMeshes(1);
            for (int i = 0; i < 8; i++)
            {
                float a = i * 2.399963f;
                float r = 0.7f + 0.8f * (float)rng.NextDouble();
                mesh.Blob(0, new Vector3(Mathf.Cos(a) * r, 2.35f + 0.25f * (float)rng.NextDouble(), Mathf.Sin(a) * r), Vector3.one * 0.13f, 0.1f, rng);
            }

            return Save(mesh.ToMesh("Flora_FruitTreeFruit"));
        }

        private static Mesh MossMound(string name)
        {
            var mesh = new LowPolyMeshes(1);
            mesh.Blob(0, new Vector3(0f, 0.02f, 0f), new Vector3(0.55f, 0.12f, 0.55f), 0.15f, new Random(621));
            return Save(mesh.ToMesh(name));
        }

        private static Mesh MushroomFruit(string name, bool death)
        {
            var rng = new Random(death ? 632 : 631);
            int caps = death ? 4 : 5;
            var mesh = new LowPolyMeshes(death ? 3 : 2);
            for (int i = 0; i < caps; i++)
            {
                float a = i * 2.399963f;
                var root = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (0.08f + 0.28f * (float)rng.NextDouble());
                float h = death ? 0.22f + 0.2f * (float)rng.NextDouble() : 0.16f + 0.26f * (float)rng.NextDouble();
                float radius = death ? 0.13f + 0.07f * (float)rng.NextDouble() : 0.1f + 0.07f * (float)rng.NextDouble();
                mesh.Frustum(0, root, 6, 0.04f, 0.035f, h);
                mesh.Frustum(1, root + Vector3.up * h, 8, radius, 0f, radius * 0.65f, (float)rng.NextDouble() * 6f);

                // The poisonous cap is dotted white, so a careful eye can tell it from the edible ones.
                if (death)
                    for (int d = 0; d < 3; d++)
                    {
                        float da = d * 2.1f + i;
                        mesh.Blob(2, root + new Vector3(Mathf.Cos(da) * radius * 0.5f, h + radius * 0.42f, Mathf.Sin(da) * radius * 0.5f), Vector3.one * 0.022f, 0.05f, rng);
                    }
            }

            return Save(mesh.ToMesh(name));
        }

        private static Mesh HerbBody()
        {
            var rng = new Random(641);
            var mesh = new LowPolyMeshes(1);
            for (int i = 0; i < 9; i++)
            {
                float yaw = i * 40f + (float)rng.NextDouble() * 18f;
                Quaternion q = Quaternion.Euler(0f, yaw, 0f);
                float length = 0.35f + 0.25f * (float)rng.NextDouble();
                Vector3 mid = q * new Vector3(0f, 0.22f, length * 0.55f);
                Vector3 tip = q * new Vector3(0f, 0.12f, length);
                Vector3 side = q * new Vector3(0.07f, 0.02f, 0f);
                mesh.Triangle(0, Vector3.zero, mid + side, mid - side);
                mesh.Triangle(0, mid - side, mid + side, tip);
            }

            return Save(mesh.ToMesh("Flora_Herb"));
        }

        private static Mesh HerbFruit()
        {
            var rng = new Random(642);
            var mesh = new LowPolyMeshes(2);
            for (int i = 0; i < 4; i++)
            {
                float a = i * 1.7f;
                var root = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (0.05f + 0.15f * (float)rng.NextDouble());
                float h = 0.45f + 0.25f * (float)rng.NextDouble();
                var side = new Vector3(0.015f, 0f, 0f);
                mesh.Triangle(0, root - side, root + Vector3.up * h, root + side);
                mesh.Blob(1, root + Vector3.up * h, new Vector3(0.07f, 0.05f, 0.07f), 0.15f, rng);
            }

            return Save(mesh.ToMesh("Flora_HerbFlowers"));
        }

        // ---------------------------------------------------------------- assets

        private static GameObject Plant(string name, Mesh body, Material[] bodyMaterials, Mesh fruit, Material[] fruitMaterials,
            System.Action<GameObject> collider)
        {
            var root = new GameObject(name);
            root.AddComponent<MeshFilter>().sharedMesh = body;
            root.AddComponent<MeshRenderer>().sharedMaterials = bodyMaterials;
            collider?.Invoke(root);

            var fruitObject = new GameObject(FloraSet.FruitName);
            fruitObject.transform.SetParent(root.transform, false);
            fruitObject.AddComponent<MeshFilter>().sharedMesh = fruit;
            var renderer = fruitObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = fruitMaterials;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabDir}/{name}.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static Mesh Save(Mesh mesh)
        {
            string path = $"{MeshDir}/{mesh.name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }

            EditorUtility.CopySerialized(mesh, existing);
            Object.DestroyImmediate(mesh);
            return existing;
        }

        private static Material Existing(string name) => AssetDatabase.LoadAssetAtPath<Material>($"{MaterialDir}/{name}.mat");

        private static Material Toon(string name, Color color, bool doubleSided = false, Color? emission = null)
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
            material.SetFloat("_RimStrength", 0.3f);
            material.SetFloat("_Cull", doubleSided ? 0f : 2f);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
