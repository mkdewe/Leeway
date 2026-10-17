using UnityEditor;
using UnityEngine;

namespace Leeway.World.Editor
{
    [CustomEditor(typeof(IslandGenerator))]
    public sealed class IslandGeneratorEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var generator = (IslandGenerator)target;
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "Preview only: the generated island is not saved with the scene. In a match the seed comes from IslandSeed.",
                MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Generate preview"))
                    generator.Generate(generator.PreviewSeed);

                if (GUILayout.Button("Random seed"))
                {
                    var so = new SerializedObject(generator);
                    so.FindProperty("_previewSeed").intValue = Random.Range(1, 100000);
                    so.ApplyModifiedProperties();
                    generator.Generate(generator.PreviewSeed);
                }

                if (GUILayout.Button("Clear"))
                    generator.Clear();
            }

            if (generator.IsGenerated)
                EditorGUILayout.LabelField("Hub", generator.HubPosition.ToString("F1"));
        }
    }
}
