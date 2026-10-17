using System.Collections.Generic;
using Leeway.Creature.Domain;
using UnityEngine;

namespace Leeway.CreatureEditor
{
    /// <summary>
    /// Makes a built creature match the game's look (GDD 4.2): parts take the genome's secondary
    /// colour instead of the neutral placeholder grey, and chosen parts (eyes) glow. The glow is
    /// bright and saturated, so PaletteGrade keeps it as an accent and bloom picks it up - the
    /// "dark world, vivid accents" rule.
    /// </summary>
    /// <remarks>
    /// Works on a networked <see cref="CreatureBody"/> and on the editor's <see cref="CreatureBodyPreview"/>.
    /// Only MaterialPropertyBlocks, re-applied after every rebuild:
    /// the shared part material stays untouched and nothing is instanced per creature.
    /// </remarks>
    public sealed class CreatureStyle : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [Tooltip("How far parts move from the neutral part colour toward the genome's secondary colour.")]
        [SerializeField, Range(0f, 1f)] private float _partTint = 0.85f;
        [SerializeField] private Color _neutralPartColor = new(0.75f, 0.72f, 0.65f);

        [Header("Glow")]
        [SerializeField] private CreaturePartDefinition[] _glowingParts;
        [SerializeField, ColorUsage(false, true)] private Color _glowColor = new(1.6f, 0.62f, 0.08f);

        private CreatureBody _body;
        private CreatureBodyPreview _preview;
        private MaterialPropertyBlock _block;
        private readonly HashSet<int> _glowingIds = new();
        private readonly List<Renderer> _renderers = new();

        private void Awake()
        {
            TryGetComponent(out _body);
            TryGetComponent(out _preview);
            _block = new MaterialPropertyBlock();
            if (_glowingParts == null) return;
            foreach (CreaturePartDefinition part in _glowingParts)
                if (part != null) _glowingIds.Add(part.PartId);
        }

        private void OnEnable()
        {
            if (_body != null) _body.BodyRebuilt += OnBodyRebuilt;
            if (_preview != null) _preview.Rebuilt += OnBodyRebuilt;
            Apply(_body != null ? _body.Genome : _preview != null ? _preview.Genome : null);
        }

        private void OnDisable()
        {
            if (_body != null) _body.BodyRebuilt -= OnBodyRebuilt;
            if (_preview != null) _preview.Rebuilt -= OnBodyRebuilt;
        }

        private void OnBodyRebuilt(CreatureGenome genome, CreatureStats stats) => Apply(genome);

        private void Apply(CreatureGenome genome)
        {
            BuiltCreatureBody built = _body != null ? _body.Built : _preview != null ? _preview.Body : null;
            if (built == null || genome == null) return;

            Color partColor = Color.Lerp(_neutralPartColor, genome.SecondaryColor, _partTint);
            foreach (CreaturePartInstance part in built.PartInstances)
            {
                if (part.Object == null) continue;
                bool glows = part.GeneIndex >= 0 && part.GeneIndex < genome.Parts.Count
                             && _glowingIds.Contains(genome.Parts[part.GeneIndex].PartId);

                part.Object.GetComponentsInChildren(true, _renderers);
                foreach (Renderer r in _renderers)
                {
                    r.GetPropertyBlock(_block);
                    _block.SetColor(BaseColorId, glows ? Color.white : partColor);
                    _block.SetColor(EmissionColorId, glows ? _glowColor : Color.black);
                    r.SetPropertyBlock(_block);
                }
            }
        }
    }
}
