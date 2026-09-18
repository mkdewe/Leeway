using System.Collections;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Leeway.Tests
{
    /// <summary>
    /// Loads the editor scene <b>once per PlayMode run</b>, shared by every test class.
    /// </summary>
    /// <remarks>
    /// The flag has to be shared rather than private to a class: every further load in <c>Single</c>
    /// mode unloads the previous scene and destroys the <c>NetworkManager</c>, and FishNet's internal
    /// <c>SceneManager</c> then receives an unload event with a broken reference and throws a
    /// <c>NullReferenceException</c> — which the Test Runner counts as a setup failure in whichever
    /// class happened to come second.
    ///
    /// Test isolation therefore comes from restarting the host, not the scene.
    /// </remarks>
    internal static class CreatureEditorTestScene
    {
        public const string Path = "Assets/Scenes/CreatureEditor.unity";

        private static bool _loaded;

        public static IEnumerator EnsureLoaded()
        {
            if (_loaded) yield break;

            yield return SceneManager.LoadSceneAsync(Path, LoadSceneMode.Single);
            _loaded = true;
        }
    }
}
