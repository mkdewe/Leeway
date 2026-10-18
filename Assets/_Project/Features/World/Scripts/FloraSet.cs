using Leeway.World.Domain;
using UnityEngine;

namespace Leeway.World
{
    /// <summary>The models of the edible plants, one per kind, in the order of <see cref="FloraKind"/>.</summary>
    /// <remarks>
    /// Each prefab is a plant and, as a child named <see cref="FruitName"/>, whatever is picked from it. The
    /// fruit is switched off while the plant is bare, so a plant that has been eaten from looks it.
    /// </remarks>
    [CreateAssetMenu(menuName = "Leeway/World/Flora Set", fileName = "FloraSet")]
    public sealed class FloraSet : ScriptableObject
    {
        public const string FruitName = "Fruit";

        [SerializeField] private GameObject[] _prefabs = new GameObject[0];

        public GameObject PrefabFor(FloraKind kind)
        {
            int index = (int)kind;
            return index >= 0 && index < _prefabs.Length ? _prefabs[index] : null;
        }

#if UNITY_EDITOR
        public void EditorSet(GameObject[] prefabs) => _prefabs = prefabs;
#endif
    }
}
