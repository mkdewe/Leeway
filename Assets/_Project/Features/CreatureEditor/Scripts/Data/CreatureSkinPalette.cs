using System;
using System.Collections.Generic;
using UnityEngine;

namespace Leeway.CreatureEditor
{
    /// <summary>One colour offered by the palette.</summary>
    [Serializable]
    public struct SkinSwatch
    {
        [SerializeField] private string _name;
        [SerializeField] private Color _color;

        public SkinSwatch(string name, Color color)
        {
            _name = name;
            _color = color;
        }

        public string Name => _name;
        public Color32 Color => _color;
    }

    /// <summary>
    /// One coat pattern: the markings and the relief that go with them.
    /// </summary>
    /// <remarks>
    /// The base map is a <b>greyscale mask</b>, not a finished texture — it is multiplied by the
    /// colour the player picked, so one pattern serves every colour instead of needing a spotted
    /// texture per hue. The normal map is what makes scales look raised rather than drawn on.
    /// </remarks>
    [Serializable]
    public class SkinPattern
    {
        [SerializeField] private string _name = "Pattern";
        [SerializeField] private Texture2D _baseMap;
        [SerializeField] private Texture2D _normalMap;

        [Tooltip("How many times the pattern repeats across the body. Bigger numbers make finer markings.")]
        [SerializeField, Min(0.01f)] private float _tiling = 2f;

        [Tooltip("How deep the relief reads. 0 keeps the markings flat.")]
        [SerializeField, Range(0f, 3f)] private float _normalStrength = 1f;

        public SkinPattern() { }

        public SkinPattern(string name, Texture2D baseMap, Texture2D normalMap, float tiling, float normalStrength)
        {
            _name = name;
            _baseMap = baseMap;
            _normalMap = normalMap;
            _tiling = tiling;
            _normalStrength = normalStrength;
        }

        public string Name => _name;
        public Texture2D BaseMap => _baseMap;
        public Texture2D NormalMap => _normalMap;
        public float Tiling => Mathf.Max(0.01f, _tiling);
        public float NormalStrength => _normalStrength;

        /// <summary>Whether this entry paints anything at all — bare skin is a pattern with no maps.</summary>
        public bool HasMaps => _baseMap != null || _normalMap != null;
    }

    /// <summary>
    /// The colours and coat patterns the player can paint a creature with.
    /// </summary>
    /// <remarks>
    /// <para>An asset rather than a hard-coded list, for the same reason the part catalog is one:
    /// adding a colour or a coat is then art work, not programming. The palette panel builds itself
    /// from whatever is in here.</para>
    ///
    /// <para><b>The index is the contract.</b> A pattern travels in the genome as its position in this
    /// list (<see cref="Leeway.Creature.Domain.PartGene.PatternId"/>), so entries may be added at the
    /// end freely, and reordering or removing one repaints every creature that referred to it.
    /// Position <c>0</c> is bare skin and is expected to stay that way.</para>
    /// </remarks>
    [CreateAssetMenu(fileName = "SkinPalette", menuName = "Leeway/Creature/Skin Palette")]
    public class CreatureSkinPalette : ScriptableObject
    {
        [SerializeField] private SkinSwatch[] _colors = Array.Empty<SkinSwatch>();

        [Tooltip("Entry 0 is bare skin — no maps. Append new coats at the end: the position is what the genome stores.")]
        [SerializeField] private List<SkinPattern> _patterns = new();

        public IReadOnlyList<SkinSwatch> Colors => _colors;
        public IReadOnlyList<SkinPattern> Patterns => _patterns;

        /// <summary>
        /// The pattern behind an identifier, or <c>null</c> for bare skin.
        /// </summary>
        /// <remarks>
        /// An identifier this palette does not know comes back as <c>null</c> rather than as an error:
        /// a creature built with a palette that has since grown has to keep loading, just without the
        /// markings nobody here can draw.
        /// </remarks>
        public SkinPattern Get(byte patternId)
        {
            if (_patterns == null || patternId >= _patterns.Count) return null;

            SkinPattern pattern = _patterns[patternId];
            return pattern != null && pattern.HasMaps ? pattern : null;
        }
    }
}
