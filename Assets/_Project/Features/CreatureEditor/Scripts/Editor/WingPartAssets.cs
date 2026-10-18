using System.Collections.Generic;
using Leeway.Creature.Domain;
using UnityEditor;
using UnityEngine;

namespace Leeway.CreatureEditor.Authoring
{
    /// <summary>
    /// Authors the wing part: a low-poly feathered wing, its prefab, its definition and its place in the
    /// catalog.
    /// </summary>
    /// <remarks>
    /// <para>The catalog's other models are the artist's, modelled in an external tool. There is no wing among
    /// them, so this one is built from code in the same low-poly language - flat facets, a bold silhouette -
    /// and goes in as an ordinary part, to be replaced by a hand-modelled one without touching a line of
    /// the game: the part is found by its category, not by anything in this file.</para>
    ///
    /// <para>The wing lies along the part's +Z (outward from the body, the direction every part is placed
    /// along), its leading edge towards +Y (the creature's front) and its feathers trailing towards -Y, so a mirrored pair is two
    /// wings spread to either side. Idempotent: run again to rebuild.</para>
    /// </remarks>
    public static class WingPartAssets
    {
        private const string Folder = "Assets/_Project/Features/CreatureEditor";
        private const string MeshPath = Folder + "/Data/Parts/Wing.asset";
        private const string PrefabPath = Folder + "/Prefabs/Parts/Part_wing.prefab";
        private const string DefinitionPath = Folder + "/Data/Parts/Part_wing.asset";
        private const string CatalogPath = Folder + "/Data/PartCatalog.asset";
        private const string SkinPath = Folder + "/Data/CreatureSkin.mat";

        [MenuItem("Leeway/Creature/Create Wing Part")]
        public static void Create()
        {
            Mesh mesh = BuildMesh();

            // An existing mesh is overwritten in place: deleting it would give it a new identity and every prefab
            // that points at it would be left pointing at nothing.
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if (existing != null) EditorUtility.CopySerialized(mesh, existing);
            else AssetDatabase.CreateAsset(mesh, MeshPath);

            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(MeshPath);

            GameObject prefab = BuildPrefab(AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath));
            CreaturePartDefinition definition = BuildDefinition(prefab);
            AddToCatalog(definition);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Wing part created: " + DefinitionPath);
        }

        /// <summary>Spar along +Z with the feathers fanning out behind it, as flat double-sided strips.</summary>
        private static Mesh BuildMesh()
        {
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            var normals = new List<Vector3>();

            // Every facet is added twice - once facing +X, once facing -X with its own normals - so the wing is
            // lit correctly from above and from below instead of showing a black back.
            void Add(Vector3[] corners, int[] tris)
            {
                int start = vertices.Count;
                vertices.AddRange(corners);
                foreach (Vector3 c in corners) { uvs.Add(Planar(c)); normals.Add(Vector3.right); }
                foreach (int t in tris) triangles.Add(start + t);

                int back = vertices.Count;
                vertices.AddRange(corners);
                foreach (Vector3 c in corners) { uvs.Add(Planar(c)); normals.Add(Vector3.left); }
                for (int i = tris.Length - 1; i >= 0; i--) triangles.Add(back + tris[i]);
            }

            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                Add(new[] { a, b, c, d }, new[] { 0, 1, 2, 0, 2, 3 });
            }

            // The arm: a slim tapering bar from the shoulder to the wrist, thickened a little in Y.
            const float span = 0.95f;
            Vector3 shoulder = new(0f, 0f, 0f);
            Vector3 elbow = new(0.04f, 0.05f, 0.32f);
            Vector3 wrist = new(0.02f, 0.02f, 0.66f);
            Vector3 tip = new(-0.02f, 0f, span);

            Quad(shoulder + new Vector3(0.07f, 0, 0), elbow + new Vector3(0.05f, 0, 0), elbow - new Vector3(0.05f, 0, 0), shoulder - new Vector3(0.07f, 0, 0));
            Quad(elbow + new Vector3(0.05f, 0, 0), wrist + new Vector3(0.035f, 0, 0), wrist - new Vector3(0.035f, 0, 0), elbow - new Vector3(0.05f, 0, 0));
            Quad(wrist + new Vector3(0.035f, 0, 0), tip + new Vector3(0.012f, 0, 0), tip - new Vector3(0.012f, 0, 0), wrist - new Vector3(0.035f, 0, 0));

            // The membrane behind the arm: a fan of broad feathers, longest near the body and shorter
            // towards the tip, each swept outward and slightly down at the end.
            float[] lengths = { 0.40f, 0.48f, 0.52f, 0.50f, 0.44f, 0.34f };
            for (int i = 0; i < lengths.Length; i++)
            {
                float t = i / (float)(lengths.Length - 1);
                float z = Mathf.Lerp(0.05f, span - 0.04f, t);
                float width = Mathf.Lerp(0.27f, 0.17f, t);
                float x = Mathf.Lerp(-0.04f, -0.02f, t);

                Vector3 rootNear = new(x, 0f, z - width * 0.5f);
                Vector3 rootFar = new(x, 0f, z + width * 0.5f);
                Vector3 tipFar = new(x - lengths[i], -0.045f, z + width * 0.5f + 0.07f);
                Vector3 tipNear = new(x - lengths[i] * 0.9f, -0.045f, z - width * 0.5f + 0.06f);

                Quad(rootNear, rootFar, tipFar, tipNear);

                // A rounded tip to each feather, so the trailing edge reads as feathers and not as teeth.
                Vector3 mid = (tipFar + tipNear) * 0.5f + new Vector3(-0.07f, -0.01f, 0.03f);
                Add(new[] { tipNear, tipFar, mid }, new[] { 0, 1, 2 });
            }

            // Laid out the way parts are placed: +Z outward, the chord along the part's +Y (that is the creature's
            // forward once the wing is on a flank) and the membrane flat, so the same mesh serves either side of a
            // mirrored pair.
            for (int i = 0; i < vertices.Count; i++)
            {
                Vector3 v = vertices[i];
                vertices[i] = new Vector3(0f, v.x, v.z);
                uvs[i] = new Vector2(Mathf.InverseLerp(-0.6f, 0.15f, v.x), Mathf.InverseLerp(0f, 1f, v.z));
            }

            var mesh = new Mesh { name = "Wing" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.SetNormals(normals);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Vector2 Planar(Vector3 point) => new(Mathf.InverseLerp(-0.6f, 0.15f, point.x), Mathf.InverseLerp(0f, 1f, point.z));

        private static GameObject BuildPrefab(Mesh mesh)
        {
            var root = new GameObject("Part_wing");
            var model = new GameObject("Model");
            model.transform.SetParent(root.transform, false);

            model.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = model.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(SkinPath);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static CreaturePartDefinition BuildDefinition(GameObject prefab)
        {
            var definition = AssetDatabase.LoadAssetAtPath<CreaturePartDefinition>(DefinitionPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<CreaturePartDefinition>();
                AssetDatabase.CreateAsset(definition, DefinitionPath);
            }

            var so = new SerializedObject(definition);
            so.FindProperty("<PartKey>k__BackingField").stringValue = "wing.feather";
            so.FindProperty("<DisplayName>k__BackingField").stringValue = "Wings";
            so.FindProperty("<Category>k__BackingField").intValue = (int)PartCategory.Wing;
            so.FindProperty("<AllowedSites>k__BackingField").intValue = (int)AttachmentSite.Torso;
            so.FindProperty("<MirrorCapable>k__BackingField").boolValue = true;
            so.FindProperty("<Prefab>k__BackingField").objectReferenceValue = prefab;
            so.FindProperty("<SkinOffset>k__BackingField").floatValue = -0.02f;
            so.FindProperty("<Cost>k__BackingField").intValue = 45;

            SerializedProperty stats = so.FindProperty("<Stats>k__BackingField");
            stats.FindPropertyRelative("HpBonus").floatValue = 2f;
            stats.FindPropertyRelative("SpeedBonus").floatValue = 0f;
            stats.FindPropertyRelative("DamageBonus").floatValue = 0f;
            stats.FindPropertyRelative("SenseRadiusBonus").floatValue = 0f;
            stats.FindPropertyRelative("MassKg").floatValue = 1.2f;

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static void AddToCatalog(CreaturePartDefinition definition)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CreaturePartCatalog>(CatalogPath);
            if (catalog == null) return;

            var so = new SerializedObject(catalog);
            SerializedProperty parts = so.FindProperty("_parts");

            for (int i = 0; i < parts.arraySize; i++)
                if (parts.GetArrayElementAtIndex(i).objectReferenceValue == definition) return;

            parts.arraySize++;
            parts.GetArrayElementAtIndex(parts.arraySize - 1).objectReferenceValue = definition;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
        }
    }
}
