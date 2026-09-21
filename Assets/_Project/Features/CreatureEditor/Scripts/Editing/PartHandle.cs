using System.Collections.Generic;
using UnityEngine;

namespace Leeway.CreatureEditor
{
    /// <summary>
    /// The grab handle for one instantiated part. It carries the index of the gene the part came from
    /// and knows how to highlight it on hover.
    /// </summary>
    /// <remarks>
    /// <see cref="Mirrored"/> is here so that dragging the left piece of a pair behaves intuitively:
    /// the mirrored instance has a flipped <c>x</c>, so moving the cursor right has to decrease
    /// <c>x</c> in the gene rather than increase it.
    /// </remarks>
    [RequireComponent(typeof(BoxCollider))]
    public class PartHandle : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private static MaterialPropertyBlock _sharedBlock;

        private readonly List<Renderer> _renderers = new();
        private readonly List<Color> _normalColors = new();

        private Color _highlightColor;

        /// <summary>The part's index in the genome — not the index of the instance.</summary>
        public int GeneIndex { get; private set; }

        public bool Mirrored { get; private set; }

        public void Bind(int geneIndex, bool mirrored, Color highlightColor)
        {
            GeneIndex = geneIndex;
            Mirrored = mirrored;
            _highlightColor = highlightColor;

            _renderers.Clear();
            _normalColors.Clear();
            GetComponentsInChildren(true, _renderers);

            _sharedBlock ??= new MaterialPropertyBlock();
            for (int i = 0; i < _renderers.Count; i++)
                _normalColors.Add(NormalColorOf(_renderers[i]));
        }

        /// <summary>
        /// The colour to go back to when the highlight comes off.
        /// </summary>
        /// <remarks>
        /// The property block comes first, and the material only as a fallback: parts are painted in
        /// the creature's colour through a block (<c>CreaturePartInstantiator.Tint</c>), and reading
        /// the material instead would repaint the part in the model's authored colour the first time
        /// the cursor left it.
        /// </remarks>
        private static Color NormalColorOf(Renderer renderer)
        {
            if (renderer == null) return Color.white;

            renderer.GetPropertyBlock(_sharedBlock);
            if (_sharedBlock.HasColor(BaseColorId)) return _sharedBlock.GetColor(BaseColorId);

            Material material = renderer.sharedMaterial;
            return material != null && material.HasProperty(BaseColorId)
                ? material.GetColor(BaseColorId)
                : Color.white;
        }

        public void SetHighlighted(bool highlighted)
        {
            _sharedBlock ??= new MaterialPropertyBlock();

            for (int i = 0; i < _renderers.Count; i++)
            {
                Renderer renderer = _renderers[i];
                if (renderer == null) continue;

                renderer.GetPropertyBlock(_sharedBlock);
                _sharedBlock.SetColor(BaseColorId, highlighted ? _highlightColor : _normalColors[i]);
                renderer.SetPropertyBlock(_sharedBlock);
            }
        }
    }
}
