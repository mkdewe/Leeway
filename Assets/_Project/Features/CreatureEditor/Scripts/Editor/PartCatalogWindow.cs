using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Leeway.Creature.Domain;
using UnityEditor;
using UnityEngine;

namespace Leeway.CreatureEditor.Authoring
{
    /// <summary>
    /// A window for creating body parts and checking that the catalog follows the conventions.
    /// </summary>
    /// <remarks>
    /// <para><b>Two things in one window, because it is one activity.</b> Adding a part means a prefab,
    /// a definition and a catalog entry — and then checking you have not fallen into one of the
    /// convention's traps. Split into two tools, it would mean the audit runs whenever somebody happens
    /// to remember it.</para>
    ///
    /// <para>The window does nothing that cannot be done by hand — it fills in the fields you would
    /// have to fill in anyway, and computes the ones you cannot judge by eye.</para>
    /// </remarks>
    public class PartCatalogWindow : EditorWindow
    {
        private const string DefaultFolder = "Assets/_Project/Features/CreatureEditor/Data/Parts";

        private static readonly Color ErrorTint = new(1f, 0.55f, 0.45f);
        private static readonly Color WarningTint = new(1f, 0.85f, 0.4f);

        private GameObject _newPrefab;
        private PartCategory _newCategory = PartCategory.Detail;

        private List<PartIssue> _issues;
        private Vector2 _scroll;

        [MenuItem("Leeway/Parts/Body part catalog")]
        public static void Open()
        {
            var window = GetWindow<PartCatalogWindow>();
            window.titleContent = new GUIContent("Body parts");
            window.minSize = new Vector2(460f, 320f);
            window.Refresh();
        }

        private void OnGUI()
        {
            CreaturePartCatalog catalog = PartAuthoringAudit.FindCatalog();

            DrawCreator(catalog);
            EditorGUILayout.Space(8f);
            DrawAudit(catalog);
        }

        // ---------- creating a part ----------

        private void DrawCreator(CreaturePartCatalog catalog)
        {
            EditorGUILayout.LabelField("New part", EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                _newPrefab = (GameObject)EditorGUILayout.ObjectField("Prefab", _newPrefab, typeof(GameObject), false);
                _newCategory = (PartCategory)EditorGUILayout.EnumPopup("Category", _newCategory);

                if (_newPrefab != null)
                {
                    PartGeometry geometry = PartGeometry.Measure(_newPrefab);

                    EditorGUILayout.LabelField(geometry.HasVisibleMesh
                        ? $"The model reaches {geometry.ReachAlongZ:F3} along +Z, seating {geometry.SeatDepth:F3}"
                        : "The prefab has no visible mesh — its tile will have no preview.",
                        EditorStyles.miniLabel);
                }

                using (new EditorGUI.DisabledScope(_newPrefab == null || catalog == null))
                {
                    if (GUILayout.Button("Create definition and add to catalog"))
                        Create(catalog, _newPrefab, _newCategory);
                }

                if (catalog == null)
                    EditorGUILayout.HelpBox("No part catalog found in the project.", MessageType.Error);
            }
        }

        /// <summary>
        /// Creates a definition filled in with whatever can be derived from the prefab.
        /// </summary>
        /// <remarks>
        /// <c>SegmentLength</c> is taken from measuring the model, because it is the only one of these
        /// numbers you cannot judge by eye and whose mismatch only shows in the playground. The rest is
        /// copying the name across — except without a typo in the key, which would cost you invalidated
        /// saved genomes when corrected.
        /// </remarks>
        private static void Create(CreaturePartCatalog catalog, GameObject prefab, PartCategory category)
        {
            string folder = ResolveFolder();
            string key = $"{Prefix(category)}.{Sanitise(prefab.name)}";
            string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/Part_{Sanitise(prefab.name)}.asset");

            var definition = CreateInstance<CreaturePartDefinition>();
            AssetDatabase.CreateAsset(definition, path);

            var serialized = new SerializedObject(definition);
            serialized.FindProperty("<PartKey>k__BackingField").stringValue = key;
            serialized.FindProperty("<DisplayName>k__BackingField").stringValue = ObjectNames.NicifyVariableName(Sanitise(prefab.name));
            serialized.FindProperty("<Category>k__BackingField").enumValueIndex = (int)category;
            serialized.FindProperty("<Prefab>k__BackingField").objectReferenceValue = prefab;

            PartGeometry geometry = PartGeometry.Measure(prefab);
            if (category == PartCategory.Locomotion && geometry.ReachAlongZ > LegLimits.MinSegmentLength)
            {
                serialized.FindProperty("<SegmentLength>k__BackingField").floatValue =
                    Mathf.Clamp(geometry.ReachAlongZ, LegLimits.MinSegmentLength, LegLimits.MaxSegmentLength);
            }

            serialized.ApplyModifiedProperties();

            PartAuthoringAudit.Register(catalog, definition);
            AssetDatabase.SaveAssets();

            Selection.activeObject = definition;
            EditorGUIUtility.PingObject(definition);
        }

        /// <summary>We create definitions where the existing ones live — not wherever the cursor happens to be.</summary>
        private static string ResolveFolder()
        {
            string[] existing = AssetDatabase.FindAssets($"t:{nameof(CreaturePartDefinition)}");
            if (existing.Length > 0)
            {
                string directory = Path.GetDirectoryName(AssetDatabase.GUIDToAssetPath(existing[0]));
                if (!string.IsNullOrEmpty(directory)) return directory.Replace('\\', '/');
            }

            if (!AssetDatabase.IsValidFolder(DefaultFolder))
                Directory.CreateDirectory(DefaultFolder);

            return DefaultFolder;
        }

        private static string Prefix(PartCategory category) => category switch
        {
            PartCategory.Locomotion => "loco",
            PartCategory.Mouth => "mouth",
            PartCategory.Sense => "sense",
            PartCategory.Grasper => "grasp",
            PartCategory.Weapon => "weapon",
            _ => "detail",
        };

        /// <summary>A prefab name turned into a key fragment: no "Part_" prefix, lower case, underscores.</summary>
        private static string Sanitise(string name)
        {
            string trimmed = name.StartsWith("Part_", System.StringComparison.OrdinalIgnoreCase)
                ? name.Substring("Part_".Length)
                : name;

            var builder = new StringBuilder(trimmed.Length);
            foreach (char character in trimmed)
            {
                if (char.IsLetterOrDigit(character)) builder.Append(char.ToLowerInvariant(character));
                else if (builder.Length > 0 && builder[^1] != '_') builder.Append('_');
            }

            return builder.ToString().Trim('_');
        }

        // ---------- audit ----------

        private void DrawAudit(CreaturePartCatalog catalog)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Catalog audit", EditorStyles.boldLabel);

                if (GUILayout.Button("Check", GUILayout.Width(90f))) Refresh();

                using (new EditorGUI.DisabledScope(_issues == null || !_issues.Any(issue => issue.CanFix)))
                {
                    if (GUILayout.Button("Fix what can be fixed", GUILayout.Width(160f))) FixAll();
                }
            }

            if (_issues == null)
            {
                EditorGUILayout.HelpBox("Press Check to go through the catalog.", MessageType.Info);
                return;
            }

            if (_issues.Count == 0)
            {
                int parts = catalog != null ? catalog.Parts.Count : 0;
                EditorGUILayout.HelpBox($"Nothing to report — {parts} parts follow the convention.", MessageType.Info);
                return;
            }

            int errors = _issues.Count(issue => issue.Kind == PartIssueKind.Error);
            EditorGUILayout.LabelField($"Errors: {errors}, warnings: {_issues.Count - errors}", EditorStyles.miniLabel);

            using var scroll = new EditorGUILayout.ScrollViewScope(_scroll);
            _scroll = scroll.scrollPosition;

            foreach (PartIssue issue in _issues) DrawIssue(issue);
        }

        private void DrawIssue(PartIssue issue)
        {
            Color previous = GUI.backgroundColor;
            GUI.backgroundColor = issue.Kind == PartIssueKind.Error ? ErrorTint : WarningTint;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUI.backgroundColor = previous;

                EditorGUILayout.LabelField(issue.Message, EditorStyles.wordWrappedLabel);

                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(issue.Context == null))
                    {
                        if (GUILayout.Button("Show", GUILayout.Width(70f)))
                        {
                            Selection.activeObject = issue.Context;
                            EditorGUIUtility.PingObject(issue.Context);
                        }
                    }

                    GUILayout.FlexibleSpace();

                    if (issue.CanFix && GUILayout.Button(issue.FixLabel, GUILayout.Width(140f)))
                    {
                        issue.Fix();
                        Refresh();
                    }
                }
            }

            GUI.backgroundColor = previous;
        }

        private void Refresh() => _issues = PartAuthoringAudit.Inspect(PartAuthoringAudit.FindCatalog());

        /// <summary>
        /// Fixes everything that can be fixed mechanically.
        /// </summary>
        /// <remarks>
        /// There is deliberately nothing here that changes a <c>PartKey</c>. The key is a part's address
        /// in saved genomes — correcting it with one button would invalidate every creature that uses
        /// that part.
        /// </remarks>
        private void FixAll()
        {
            foreach (PartIssue issue in _issues.Where(issue => issue.CanFix).ToList()) issue.Fix();

            AssetDatabase.SaveAssets();
            Refresh();
        }
    }
}
