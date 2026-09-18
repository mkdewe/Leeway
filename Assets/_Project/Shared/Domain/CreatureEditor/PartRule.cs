using System.Collections.Generic;

namespace Leeway.Creature.Domain
{
    /// <summary>
    /// A pure snapshot of one part definition. The catalog (a ScriptableObject) projects itself onto
    /// a set of these rules, which keeps validation and stat derivation testable without Unity assets.
    /// </summary>
    public readonly struct PartRule
    {
        public readonly int PartId;
        public readonly PartCategory Category;
        public readonly AttachmentSite AllowedSites;
        public readonly bool MirrorCapable;
        public readonly PartStatContribution Stats;

        /// <summary>What attaching this part costs. A mirrored part costs the same as a single one — a pair is one decision by the player.</summary>
        public readonly int Cost;

        /// <summary>The leg build. Meaningful only for <see cref="PartCategory.Locomotion"/>.</summary>
        public readonly LegSpec Leg;

        /// <summary>How far the part lifts above the skin. Needed to work out where the hip really sits.</summary>
        public readonly float SkinOffset;

        public PartRule(int partId, PartCategory category, AttachmentSite allowedSites, bool mirrorCapable,
            PartStatContribution stats, int cost = 0, LegSpec leg = default, float skinOffset = 0f)
        {
            SkinOffset = skinOffset;
            PartId = partId;
            Category = category;
            AllowedSites = allowedSites;
            MirrorCapable = mirrorCapable;
            Stats = stats;
            Cost = cost;
            Leg = leg;
        }
    }

    /// <summary>
    /// An immutable set of part rules with an index by <see cref="PartRule.PartId"/>.
    /// The server always validates with its own set — never with one sent by a client.
    /// </summary>
    public sealed class PartRuleSet
    {
        private static readonly PartRule[] EmptyRules = new PartRule[0];

        private readonly PartRule[] _rules;
        private readonly Dictionary<int, PartRule> _byId;

        public static PartRuleSet Empty { get; } = new PartRuleSet(null);

        public IReadOnlyList<PartRule> Rules => _rules;
        public int Count => _rules.Length;

        /// <summary>The cost of one vertebra. Together with <see cref="PartRule.Cost"/> and <see cref="Budget"/> it forms the only size limit that actually applies to a creature.</summary>
        public int VertebraCost { get; }

        /// <summary>The currency pool for a single creature.</summary>
        public int Budget { get; }

        public PartRuleSet(IReadOnlyList<PartRule> rules, int vertebraCost = 0, int budget = int.MaxValue)
        {
            VertebraCost = vertebraCost;
            Budget = budget;

            if (rules == null || rules.Count == 0)
            {
                _rules = EmptyRules;
                _byId = new Dictionary<int, PartRule>();
                return;
            }

            _byId = new Dictionary<int, PartRule>(rules.Count);
            var accepted = new List<PartRule>(rules.Count);
            for (int i = 0; i < rules.Count; i++)
            {
                PartRule rule = rules[i];
                // A duplicate PartId is a catalog data error — the first entry wins, and the catalog
                // reports the collision in OnValidate.
                if (_byId.ContainsKey(rule.PartId)) continue;

                _byId.Add(rule.PartId, rule);
                accepted.Add(rule);
            }
            _rules = accepted.ToArray();
        }

        public bool TryGetRule(int partId, out PartRule rule) => _byId.TryGetValue(partId, out rule);

        public bool Contains(int partId) => _byId.ContainsKey(partId);

        /// <summary>Returns the rules of a given category that fit the given attachment site.</summary>
        public List<PartRule> GetEligible(PartCategory category, AttachmentSite site)
        {
            var result = new List<PartRule>();
            for (int i = 0; i < _rules.Length; i++)
            {
                PartRule rule = _rules[i];
                if (rule.Category != category) continue;
                if ((rule.AllowedSites & site) == 0) continue;
                result.Add(rule);
            }
            return result;
        }
    }
}
