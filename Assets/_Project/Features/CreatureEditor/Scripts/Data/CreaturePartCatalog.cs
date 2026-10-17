using System.Collections.Generic;
using Leeway.Creature.Domain;
using UnityEngine;

namespace Leeway.CreatureEditor
{
    /// <summary>
    /// The set of every available part. It plays two roles: it supplies the prefabs for building the
    /// body, and it projects itself onto a pure <see cref="PartRuleSet"/> for the domain layer.
    /// </summary>
    /// <remarks>
    /// The catalog reaches runtime objects through <c>[SerializeField]</c> rather than VContainer —
    /// objects spawned by FishNet bypass dependency injection, and singletons are banned in this
    /// project.
    /// </remarks>
    [CreateAssetMenu(fileName = "PartCatalog", menuName = "Leeway/Creature/Part Catalog")]
    public class CreaturePartCatalog : ScriptableObject
    {
        [SerializeField] private CreaturePartDefinition[] _parts = new CreaturePartDefinition[0];

        [Header("Budget")]
        [Tooltip("What one spine vertebra costs.")]
        [SerializeField, Min(0)] private int _vertebraCost = 10;

        [Tooltip("The currency pool for a creature. Deliberately set high - the budget exists, but constrains nobody yet.")]
        [SerializeField, Min(0)] private int _budget = 1000000;

        private Dictionary<int, CreaturePartDefinition> _byId;
        private PartRuleSet _ruleSet;

        public IReadOnlyList<CreaturePartDefinition> Parts => _parts;

        private void OnEnable() => Invalidate();

        private void OnValidate()
        {
            Invalidate();
            ReportCollisions();
        }

        public bool TryGetPart(int partId, out CreaturePartDefinition definition)
        {
            EnsureIndex();
            return _byId.TryGetValue(partId, out definition);
        }

        /// <summary>
        /// The authored key behind an identifier, or <c>null</c> when this catalog has no such part.
        /// </summary>
        /// <remarks>
        /// The identifier is the key's hash and the hash is one-way, so a saved creature can only name
        /// its parts by asking the catalog. Used when writing presets — a file that says
        /// <c>"loco.hoof_legs"</c> can be read, and one that says <c>-179235692</c> cannot.
        /// </remarks>
        public string KeyOf(int partId)
            => TryGetPart(partId, out CreaturePartDefinition definition) && definition != null ? definition.PartKey : null;

        public List<CreaturePartDefinition> GetByCategory(PartCategory category)
        {
            var result = new List<CreaturePartDefinition>();
            for (int i = 0; i < _parts.Length; i++)
            {
                if (_parts[i] != null && _parts[i].Category == category)
                    result.Add(_parts[i]);
            }
            return result;
        }

        /// <summary>
        /// A snapshot of the rules for the domain layer. The server validates an incoming genome with
        /// <b>this</b> set — never with anything that came from a client.
        /// </summary>
        public PartRuleSet BuildRuleSet()
        {
            if (_ruleSet != null) return _ruleSet;

            var rules = new List<PartRule>(_parts.Length);
            for (int i = 0; i < _parts.Length; i++)
            {
                if (_parts[i] == null) continue;
                rules.Add(_parts[i].ToRule());
            }

            _ruleSet = new PartRuleSet(rules, _vertebraCost, _budget);
            return _ruleSet;
        }

        private void EnsureIndex()
        {
            if (_byId != null) return;

            _byId = new Dictionary<int, CreaturePartDefinition>(_parts.Length);
            for (int i = 0; i < _parts.Length; i++)
            {
                CreaturePartDefinition part = _parts[i];
                if (part == null) continue;
                if (_byId.ContainsKey(part.PartId)) continue;

                _byId.Add(part.PartId, part);
            }
        }

        private void Invalidate()
        {
            _byId = null;
            _ruleSet = null;
        }

        /// <summary>
        /// A duplicate key or a hash collision would quietly hide one of the parts from genomes, so we
        /// report it loudly, in the editor.
        /// </summary>
        private void ReportCollisions()
        {
            var seenKeys = new Dictionary<string, CreaturePartDefinition>(_parts.Length);
            var seenIds = new Dictionary<int, CreaturePartDefinition>(_parts.Length);

            for (int i = 0; i < _parts.Length; i++)
            {
                CreaturePartDefinition part = _parts[i];
                if (part == null) continue;

                if (seenKeys.TryGetValue(part.PartKey, out CreaturePartDefinition duplicate))
                {
                    Debug.LogError($"[{name}] Duplicate PartKey \"{part.PartKey}\": {duplicate.name} and {part.name}.", this);
                    continue;
                }
                seenKeys.Add(part.PartKey, part);

                if (seenIds.TryGetValue(part.PartId, out CreaturePartDefinition collided))
                    Debug.LogError($"[{name}] PartId hash collision between \"{collided.PartKey}\" and \"{part.PartKey}\" — change one of the keys.", this);
                else
                    seenIds.Add(part.PartId, part);
            }
        }

        [ContextMenu("Log rule set")]
        private void LogRuleSet()
        {
            PartRuleSet rules = BuildRuleSet();
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[{name}] Rule set — {rules.Count} parts:");

            for (int i = 0; i < rules.Rules.Count; i++)
            {
                PartRule rule = rules.Rules[i];
                sb.AppendLine($"  {rule.PartId,12} | {rule.Category,-10} | {rule.AllowedSites,-18} | mirror={rule.MirrorCapable,-5} | cost={rule.Cost,-5} | " +
                              $"hp+{rule.Stats.HpBonus} spd+{rule.Stats.SpeedBonus} dmg+{rule.Stats.DamageBonus} sense+{rule.Stats.SenseRadiusBonus} mass={rule.Stats.MassKg}kg");
            }

            Debug.Log(sb.ToString(), this);
        }
    }
}
