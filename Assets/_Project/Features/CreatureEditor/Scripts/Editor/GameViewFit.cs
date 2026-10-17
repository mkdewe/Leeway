#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace Leeway.CreatureEditor.Authoring
{
    /// <summary>Reset editor viewport zoom/pan, which can crop an otherwise correct canvas on every edge.</summary>
    [InitializeOnLoad]
    public static class GameViewFit
    {
        static GameViewFit(){EditorApplication.playModeStateChanged+=OnPlayMode;}
        private static void OnPlayMode(PlayModeStateChange state)
        {if(state==PlayModeStateChange.EnteredPlayMode)EditorApplication.delayCall+=Fit;}
        [MenuItem("Leeway/UI/Fit Game View")]
        public static void Fit()
        {
            foreach(var window in Resources.FindObjectsOfTypeAll<EditorWindow>().Where(w=>w.GetType().Name=="GameView"))
            {
                var so=new SerializedObject(window);
                var scale=so.FindProperty("m_ZoomArea.m_Scale");
                var translation=so.FindProperty("m_ZoomArea.m_Translation");
                var area=so.FindProperty("m_ZoomArea.m_DrawArea");
                if(scale==null||translation==null||area==null)continue;
                scale.vector2Value=Vector2.one;
                var r=area.rectValue;translation.vector2Value=new Vector2(r.width,r.height)*0.5f;
                so.ApplyModifiedPropertiesWithoutUndo();window.Repaint();
            }
        }
    }
}
#endif
