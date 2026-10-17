using System.IO;
using System.Text;
using Leeway.Creature.Domain;
using UnityEditor;
using UnityEngine;

namespace Leeway.CreatureEditor.Authoring
{
    /// <summary>
    /// Moves presets between the two places they live: assets in the project and JSON files on disk.
    /// </summary>
    /// <remarks>
    /// Both hold the same <see cref="GenomeDocument"/>, so this is copying rather than converting —
    /// which is the point. A creature a player built and liked can be brought into the project and
    /// shipped, and one authored here can be handed to someone as a single file.
    /// </remarks>
    public static class CreaturePresetMenu
    {
        [MenuItem("Leeway/Creature/Presets/Create asset from JSON…")]
        public static void ImportJson()
        {
            string source = EditorUtility.OpenFilePanel("Creature preset", CreaturePresetStore.Folder, "json");
            if (string.IsNullOrEmpty(source)) return;

            string json;
            try
            {
                json = File.ReadAllText(source, Encoding.UTF8);
            }
            catch (System.Exception exception)
            {
                EditorUtility.DisplayDialog("Creature preset", $"Could not read the file: {exception.Message}", "OK");
                return;
            }

            if (!GenomeDocument.TryFromJson(json, out GenomeDocument document, out string error))
            {
                EditorUtility.DisplayDialog("Creature preset", error, "OK");
                return;
            }

            string name = string.IsNullOrWhiteSpace(document.Name)
                ? Path.GetFileNameWithoutExtension(source)
                : document.Name;

            string target = EditorUtility.SaveFilePanelInProject("Creature preset asset", $"Preset_{name}", "asset",
                "Where should the preset asset go?");
            if (string.IsNullOrEmpty(target)) return;

            var preset = ScriptableObject.CreateInstance<CreaturePreset>();
            AssetDatabase.CreateAsset(preset, target);

            var serialized = new SerializedObject(preset);
            serialized.FindProperty("_displayName").stringValue = name;
            serialized.ApplyModifiedProperties();

            // Through the document rather than field by field: the asset then holds exactly what the
            // file held, including anything a future format version adds.
            JsonUtility.FromJsonOverwrite(json, preset.Document);

            EditorUtility.SetDirty(preset);
            AssetDatabase.SaveAssets();

            Selection.activeObject = preset;
            EditorGUIUtility.PingObject(preset);
        }

        [MenuItem("Leeway/Creature/Presets/Export selected asset to JSON…")]
        public static void ExportJson()
        {
            if (Selection.activeObject is not CreaturePreset preset) return;

            string target = EditorUtility.SaveFilePanel("Creature preset",
                CreaturePresetStore.Folder, CreaturePresetStore.Sanitise(preset.DisplayName), "json");
            if (string.IsNullOrEmpty(target)) return;

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(target) ?? CreaturePresetStore.Folder);
                File.WriteAllText(target, preset.Document.ToJson(), Encoding.UTF8);
            }
            catch (System.Exception exception)
            {
                EditorUtility.DisplayDialog("Creature preset", $"Could not write the file: {exception.Message}", "OK");
                return;
            }

            EditorUtility.RevealInFinder(target);
        }

        [MenuItem("Leeway/Creature/Presets/Export selected asset to JSON…", validate = true)]
        private static bool CanExportJson() => Selection.activeObject is CreaturePreset;

        /// <summary>Opens the folder the game itself saves the player's presets into.</summary>
        [MenuItem("Leeway/Creature/Presets/Open the player's preset folder")]
        public static void OpenPlayerFolder()
        {
            Directory.CreateDirectory(CreaturePresetStore.Folder);
            EditorUtility.RevealInFinder(CreaturePresetStore.Folder);
        }
    }
}
