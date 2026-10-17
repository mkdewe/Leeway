using Leeway.Creature.Domain;
using Leeway.CreatureEditor;
using Leeway.CreatureEditor.Authoring;
using NUnit.Framework;

namespace Leeway.Tests
{
    /// <summary>
    /// The shipped enemies, checked against the <b>real</b> catalog.
    /// </summary>
    /// <remarks>
    /// Deliberately not against a fixture: an enemy is built from part keys, and a key that is renamed
    /// or a part that is removed would leave the animal quietly missing its legs or its jaws. That is
    /// exactly the failure a hand-written fixture would hide, and the one worth a red suite.
    /// </remarks>
    public class EnemyGenomeTests
    {
        private static PartRuleSet CatalogRules()
        {
            CreaturePartCatalog catalog = PartAuthoringAudit.FindCatalog();
            Assert.IsNotNull(catalog, "The project has no part catalog.");

            return catalog.BuildRuleSet();
        }

        [Test]
        public void Snapper_IsAValidCreature()
        {
            PartRuleSet rules = CatalogRules();

            GenomeValidationResult result = GenomeValidator.Validate(EnemyGenomeLibrary.Snapper(rules), rules);

            Assert.IsTrue(result.IsValid, $"The Snapper does not validate: {result.Error}.");
        }

        [Test]
        public void Snapper_FitsInsideTheSameBudgetAPlayerHas()
        {
            PartRuleSet rules = CatalogRules();

            BudgetReport budget = CreatureBudget.Evaluate(EnemyGenomeLibrary.Snapper(rules), rules);

            Assert.GreaterOrEqual(budget.Remaining, 0, "An enemy has to be buildable by the rules players build under.");
        }

        /// <summary>
        /// Every attachment in the library is allowed to fail quietly, so the test has to state what
        /// the animal is actually supposed to have. Without this the Snapper could lose its jaws to a
        /// renamed key and still pass as "a valid creature".
        /// </summary>
        [Test]
        public void Snapper_HasLegsAndSomethingToBiteWith()
        {
            PartRuleSet rules = CatalogRules();
            CreatureGenome genome = EnemyGenomeLibrary.Snapper(rules);

            int legs = 0;
            bool armed = false;

            for (int i = 0; i < genome.PartCount; i++)
            {
                if (!rules.TryGetRule(genome.GetPart(i).PartId, out PartRule rule)) continue;

                if (rule.Category == PartCategory.Locomotion) legs++;
                if (rule.Category == PartCategory.Mouth || rule.Category == PartCategory.Weapon) armed = true;
            }

            Assert.Greater(legs, 0, "An enemy that cannot walk cannot hunt.");
            Assert.IsTrue(armed, "An enemy with nothing to hurt the player with is scenery.");
        }

        [Test]
        public void Snapper_CanReachAndHurtAStarterCreature()
        {
            PartRuleSet rules = CatalogRules();
            CreatureGenome genome = EnemyGenomeLibrary.Snapper(rules);

            CreatureStats stats = GenomeStatRules.Derive(genome, rules, CreatureStatTuning.Default);
            CombatTuning tuning = CombatTuning.Default;
            AttackSpec spec = CombatRules.Derive(genome, rules, in stats, in tuning);

            Assert.IsTrue(spec.Armed, "The Snapper is built around its maw — it must count as armed.");
            Assert.Greater(spec.Damage, 0f);
            Assert.Greater(spec.Reach, 0.5f, "It has to be able to reach a creature standing in front of it.");
            Assert.Less(spec.Cooldown, tuning.MaxCooldown, "A predator that swings at its slowest is not a threat.");

            CreatureStats prey = GenomeStatRules.Derive(
                StarterGenomeFactory.Create(1, rules), rules, CreatureStatTuning.Default);

            Assert.Less(spec.Damage, prey.MaxHp,
                "One blow should not kill a starter creature — a fight the player cannot react to is not a fight.");
        }

        /// <summary>The animal must stand on its legs, not lie on its belly — the legs carry the body.</summary>
        [Test]
        public void Snapper_StandsOnItsLegs()
        {
            PartRuleSet rules = CatalogRules();
            CreatureGenome genome = EnemyGenomeLibrary.Snapper(rules);

            Assert.Greater(GenomeStatRules.StandHeight(genome, rules), 0f);
        }
    }
}
