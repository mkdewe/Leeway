using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Leeway.World.Editor
{
    /// <summary>
    /// Leeway -> Art -> Build Stylized FX: particle prefabs drawn as low-poly meshes in the Toon
    /// shader, so effects go through the same bands, outlines and palette as the world:
    /// FootDust (emitted by CreatureFootDust) and AmbientMotes (glowing spores drifting around
    /// the camera - the "vivid accents in a dark world" from GDD 4.2).
    /// </summary>
    public static class StylizedFxAssets
    {
        private const string ToonShaderPath = "Assets/_Project/Core/Rendering/Shaders/Toon.shader";
        private const string MeshDir = "Assets/_Project/Art/Models/Placeholder";
        private const string MaterialDir = "Assets/_Project/Art/Materials/Fx";
        private const string PrefabDir = "Assets/_Project/Features/World/Prefabs/Fx";

        public const string FootDustPath = PrefabDir + "/FootDust.prefab";
        public const string AmbientMotesPath = PrefabDir + "/AmbientMotes.prefab";

        [MenuItem("Leeway/Art/Build Stylized FX")]
        public static void Build()
        {
            foreach (string dir in new[] { MeshDir, MaterialDir, PrefabDir }) Directory.CreateDirectory(dir);

            Mesh puff = SaveMesh(BlobMesh("FxPuff", 0.5f, 0.22f, 11));
            Mesh mote = SaveMesh(BlobMesh("FxMote", 0.5f, 0.1f, 12));

            Material dust = Material("Fx_Dust", new Color(0.78f, 0.76f, 0.66f), Color.black);
            Material glow = Material("Fx_Mote", new Color(1f, 0.9f, 0.6f), new Color(2.2f, 1.7f, 0.6f));

            BuildFootDust(puff, dust);
            BuildAmbientMotes(mote, glow);
            AssetDatabase.SaveAssets();
            Debug.Log("[StylizedFxAssets] Built FootDust and AmbientMotes.");
        }

        private static void BuildFootDust(Mesh mesh, Material material)
        {
            var go = new GameObject("FootDust");
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = ps.main;
            main.loop = true; // keeps the system alive; CreatureFootDust emits by hand
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.3f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.16f, 0.32f);
            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = 0.15f;
            main.maxParticles = 400;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 0f;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.12f;

            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(0.25f, 1f), new Keyframe(1f, 0f)));

            ParticleSystem.LimitVelocityOverLifetimeModule drag = ps.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.drag = 3f;

            ConfigureRenderer(go, mesh, material);
            Save(go, FootDustPath);
        }

        private static void BuildAmbientMotes(Mesh mesh, Material material)
        {
            var go = new GameObject("AmbientMotes");
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.prewarm = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(8f, 13f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
            main.maxParticles = 500;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 35f;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(60f, 16f, 60f);

            ParticleSystem.NoiseModule noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.35f;
            noise.frequency = 0.15f;
            noise.scrollSpeed = 0.1f;

            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.15f, 1f), new Keyframe(0.85f, 1f), new Keyframe(1f, 0f)));

            ConfigureRenderer(go, mesh, material);
            Save(go, AmbientMotesPath);
        }

        private static void ConfigureRenderer(GameObject go, Mesh mesh, Material material)
        {
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = mesh;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.alignment = ParticleSystemRenderSpace.World;
        }

        private static Mesh BlobMesh(string name, float radius, float roughness, int seed)
        {
            var mesh = new LowPolyMeshes(1);
            mesh.Blob(0, Vector3.zero, Vector3.one * radius, roughness, new System.Random(seed));
            return mesh.ToMesh(name);
        }

        private static Mesh SaveMesh(Mesh mesh)
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

        private static Material Material(string name, Color color, Color emission)
        {
            string path = $"{MaterialDir}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(AssetDatabase.LoadAssetAtPath<Shader>(ToonShaderPath));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetColor("_EmissionColor", emission);
            material.SetFloat("_RimStrength", 0f);
            // Particle start colour arrives as vertex colour.
            material.SetFloat("_UseVertexColor", 1f);
            material.EnableKeyword("_VERTEX_COLOR");
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void Save(GameObject go, string path)
        {
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
        }
    }
}
