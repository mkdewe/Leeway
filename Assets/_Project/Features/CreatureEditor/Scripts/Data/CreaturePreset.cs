using Leeway.Creature.Domain;
using UnityEngine;

namespace Leeway.CreatureEditor
{
    /// <summary>
    /// A ready-made creature kept as a project asset — a starting body, a test subject, an NPC
    /// species.
    /// </summary>
    /// <remarks>
    /// <para>The same <see cref="GenomeDocument"/> the player's own presets are saved as, so there is
    /// one format and one loading path: an asset shipped with the game and a file written on a
    /// player's disk differ only in where they come from.</para>
    ///
    /// <para>The asset holds the document, never a <see cref="CreatureGenome"/>: the genome is mutable
    /// and would be edited <b>in the asset</b> by anything that took it. <see cref="ToGenome"/> hands
    /// out a fresh one every time.</para>
    /// </remarks>
    [CreateAssetMenu(fileName = "CreaturePreset", menuName = "Leeway/Creature/Preset")]
    public class CreaturePreset : ScriptableObject
    {
        [Tooltip("The name shown to the player. The asset's file name is for the project, not for the game.")]
        [SerializeField] private string _displayName = "Preset";

        [TextArea, Tooltip("What this creature is for — a note to whoever finds the asset later.")]
        [SerializeField] private string _notes;

        [SerializeField] private GenomeDocument _document = new GenomeDocument();

        public string DisplayName => string.IsNullOrWhiteSpace(_displayName) ? name : _displayName;

        public GenomeDocument Document => _document;

        /// <summary>A fresh genome built from the asset. The asset is not affected by what is done to it.</summary>
        public CreatureGenome ToGenome() => _document?.ToGenome();

        /// <summary>
        /// Records a genome into this asset.
        /// </summary>
        /// <remarks>
        /// The catalog is here only so the parts land in the file under their keys — the asset stays
        /// readable when someone opens it in a text editor, and survives a part identifier being
        /// recomputed.
        /// </remarks>
        public void Capture(CreatureGenome genome, CreaturePartCatalog catalog)
        {
            _document = GenomeDocument.From(genome, DisplayName, catalog != null ? catalog.KeyOf : null);
        }
    }
}
