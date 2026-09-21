using System.Collections.Generic;
using UnityEngine;

namespace Leeway.Creature.Domain
{
    /// <summary>
    /// The editable description of a creature — the spine plus the attached parts. This is the
    /// only representation the editor and the rules work on; for transmission it is turned into
    /// a versioned byte blob by <see cref="GenomeCodec"/>.
    /// </summary>
    /// <remarks>
    /// The class deliberately does not validate itself — the mutators are raw, and correctness is
    /// enforced by <see cref="GenomeEditOperations"/> (client) and <see cref="GenomeValidator"/>
    /// (server). That lets tests build deliberately invalid genomes.
    /// </remarks>
    public sealed class CreatureGenome
    {
        private readonly List<VertebraGene> _vertebrae = new List<VertebraGene>();
        private readonly List<PartGene> _parts = new List<PartGene>();

        public Color32 PrimaryColor { get; set; } = new Color32(120, 180, 110, 255);
        public Color32 SecondaryColor { get; set; } = new Color32(60, 90, 60, 255);

        /// <summary>
        /// The coat pattern on the skin, as an index into the skin palette. <c>0</c> is bare skin.
        /// </summary>
        /// <remarks>
        /// The body carries one pattern, while every part carries its own (<see cref="PartGene.PatternId"/>):
        /// the skin is a single mesh with a single renderer, so painting one vertebra differently from
        /// its neighbour would take vertex colours and a shader of our own — a bigger change than the
        /// markings are worth today.
        /// </remarks>
        public byte BodyPattern { get; set; }

        public IReadOnlyList<VertebraGene> Vertebrae => _vertebrae;
        public IReadOnlyList<PartGene> Parts => _parts;

        public int VertebraCount => _vertebrae.Count;
        public int PartCount => _parts.Count;

        public CreatureGenome() { }

        public CreatureGenome(IEnumerable<VertebraGene> vertebrae, IEnumerable<PartGene> parts)
        {
            if (vertebrae != null) _vertebrae.AddRange(vertebrae);
            if (parts != null) _parts.AddRange(parts);
        }

        public VertebraGene GetVertebra(int index) => _vertebrae[index];
        public PartGene GetPart(int index) => _parts[index];

        public void AddVertebra(VertebraGene gene) => _vertebrae.Add(gene);
        public void InsertVertebra(int index, VertebraGene gene) => _vertebrae.Insert(index, gene);
        public void SetVertebra(int index, VertebraGene gene) => _vertebrae[index] = gene;
        public void RemoveVertebraAt(int index) => _vertebrae.RemoveAt(index);

        public void AddPart(PartGene gene) => _parts.Add(gene);
        public void SetPart(int index, PartGene gene) => _parts[index] = gene;
        public void RemovePartAt(int index) => _parts.RemoveAt(index);

        /// <summary>Counts the parts in a category. The validator and the edit operations need it for cardinality checks.</summary>
        public int CountParts(PartCategory category, PartRuleSet rules)
        {
            if (rules == null) return 0;

            int count = 0;
            for (int i = 0; i < _parts.Count; i++)
            {
                if (rules.TryGetRule(_parts[i].PartId, out PartRule rule) && rule.Category == category)
                    count++;
            }
            return count;
        }

        public CreatureGenome Clone()
        {
            var clone = new CreatureGenome(_vertebrae, _parts)
            {
                PrimaryColor = PrimaryColor,
                SecondaryColor = SecondaryColor,
                BodyPattern = BodyPattern,
            };
            return clone;
        }

        /// <summary>Overwrites the contents from another genome — used by "Revert" in the editor, without allocating a new object.</summary>
        public void CopyFrom(CreatureGenome other)
        {
            if (other == null) return;

            _vertebrae.Clear();
            _vertebrae.AddRange(other._vertebrae);
            _parts.Clear();
            _parts.AddRange(other._parts);
            PrimaryColor = other.PrimaryColor;
            SecondaryColor = other.SecondaryColor;
            BodyPattern = other.BodyPattern;
        }
    }
}
