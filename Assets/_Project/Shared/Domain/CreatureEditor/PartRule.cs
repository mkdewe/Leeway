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

        /// <summary>
        /// Whether the model is the <b>whole</b> limb rather than one link of it.
        /// </summary>
        /// <remarks>
        /// A part normally supplies one link, which the leg chain repeats for each further one. A model
        /// drawn as a complete leg cannot be repeated — it would give the leg twice over — so the chain
        /// skins that single model across its links instead, and bends it at the knee the artist drew.
        /// </remarks>
        public readonly bool WholeLimb;

        /// <summary>
        /// Whether the part has a socket at its far end that something can be fitted into.
        /// </summary>
        /// <remarks>
        /// True of legs and arms, which end in an ankle or a wrist. A foot is then a part in its own
        /// right, so the same leg can walk on a hoof, a paw or a hand, and a new foot costs one model
        /// rather than one model per leg it could go on.
        /// </remarks>
        public readonly bool AcceptsFitting;

        /// <summary>What goes into the socket unless the player says otherwise. Zero for a part with no socket.</summary>
        public readonly int DefaultFittingId;

        public PartRule(int partId, PartCategory category, AttachmentSite allowedSites, bool mirrorCapable,
            PartStatContribution stats, int cost = 0, LegSpec leg = default, float skinOffset = 0f,
            bool wholeLimb = false, bool acceptsFitting = false, int defaultFittingId = 0)
        {
            AcceptsFitting = acceptsFitting;
            DefaultFittingId = defaultFittingId;
            SkinOffset = skinOffset;
            PartId = partId;
            Category = category;
            AllowedSites = allowedSites;
            MirrorCapable = mirrorCapable;
            Stats = stats;
            Cost = cost;
            Leg = leg;
            WholeLimb = wholeLimb;
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
