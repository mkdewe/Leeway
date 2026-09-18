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
            {
                // We read the base colour from the material rather than assuming white — otherwise
                // removing the highlight would brighten a part that had a tint of its own.
                Material material = _renderers[i].sharedMaterial;
                _normalColors.Add(material != null && material.HasProperty(BaseColorId)
                    ? material.GetColor(BaseColorId)
                    : Color.white);
            }
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
