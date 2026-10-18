using UnityEngine;

namespace Leeway.Rendering
{
    /// <summary>
    /// How the level-of-detail of the world's models is set, in one place the artists and the designers can
    /// change without touching a prefab or a line of code.
    /// </summary>
    /// <remarks>
    /// <para>A model carries its own levels by name: any renderer (or a node above it) whose name ends in
    /// <c>_LOD0</c>, <c>_LOD1</c>, <c>_LOD2</c> and so on belongs to that level, and a prefab that already has a
    /// Unity LODGroup is left exactly as it was made. This asset says only <i>when</i> each level takes over:
    /// the same numbers for every model, so a model that is replaced by a new one, with more or fewer levels,
    /// is picked up with no further work.</para>
    ///
    /// <para>The asset is loaded from <c>Resources/LodProfile</c>; without it the defaults below apply.</para>
    /// </remarks>
    [CreateAssetMenu(menuName = "Leeway/Rendering/Lod Profile", fileName = "LodProfile")]
    public sealed class LodProfile : ScriptableObject
    {
        public const string ResourceName = "LodProfile";
        public const string Suffix = "_LOD";

        [Tooltip("Share of the screen's height an object fills when the next level takes over: first the change from LOD0 to LOD1, then LOD1 to LOD2, and so on. Falling.")]
        [SerializeField] private float[] _transitions = { 0.4f, 0.16f, 0.06f, 0.025f };

        [Tooltip("Below this share of the screen's height a single-level object is no longer drawn at all. Zero never culls.")]
        [SerializeField, Range(0f, 0.2f)] private float _cullHeight = 0.012f;

        [Tooltip("Scales every distance at which levels change, in the game's settings: above 1 keeps detail for longer.")]
        [SerializeField, Range(0.3f, 4f)] private float _lodBias = 1.5f;

        private static LodProfile _loaded;

        /// <summary>The profile in force: the asset if there is one, defaults if not.</summary>
        public static LodProfile Current
        {
            get
            {
                if (_loaded == null) _loaded = Resources.Load<LodProfile>(ResourceName);
                if (_loaded == null) _loaded = CreateInstance<LodProfile>();
                return _loaded;
            }
        }

        public float CullHeight => _cullHeight;
        public float LodBias => _lodBias;

        /// <summary>Screen height at which level <paramref name="level"/> hands over to the next; the last given repeats, halving.</summary>
        public float TransitionAfter(int level)
        {
            if (_transitions == null || _transitions.Length == 0) return 0.2f / (level + 1);
            if (level < _transitions.Length) return Mathf.Max(0.001f, _transitions[level]);
            return Mathf.Max(0.001f, _transitions[_transitions.Length - 1] * Mathf.Pow(0.5f, level - _transitions.Length + 1));
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplyToTheGame() => QualitySettings.lodBias = Current.LodBias;
    }
}
