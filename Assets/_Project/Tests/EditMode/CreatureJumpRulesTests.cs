using Leeway.Creature.Domain;
using NUnit.Framework;
using UnityEngine;

namespace Leeway.Tests
{
    public class CreatureJumpRulesTests
    {
        private static readonly CreatureStatTuning Tuning = CreatureStatTuning.Default;

        [Test]
        public void JumpHeight_WithoutLegs_IsZero()
        {
            CreatureGenome genome = GenomeTestFixtures.Spine();
            genome.AddPart(PartGene.Default(GenomeTestFixtures.JawId, 0, mirrored: false));

            Assert.AreEqual(0f, CreatureJumpRules.JumpHeight(genome, GenomeTestFixtures.Rules(), 10f, Tuning));
        }

        [Test]
        public void JumpHeight_WithLegs_IsPositiveAndCapped()
        {
            CreatureGenome genome = GenomeTestFixtures.Valid();
            float height = CreatureJumpRules.JumpHeight(genome, GenomeTestFixtures.Rules(), 10f, Tuning);

            Assert.Greater(height, 0f);
            Assert.LessOrEqual(height, CreatureJumpRules.MaxHeight);
        }

        [Test]
        public void JumpHeight_HeavierCreature_JumpsLower()
        {
            CreatureGenome genome = GenomeTestFixtures.Valid();
            PartRuleSet rules = GenomeTestFixtures.Rules();

            float light = CreatureJumpRules.JumpHeight(genome, rules, 6f, Tuning);
            float heavy = CreatureJumpRules.JumpHeight(genome, rules, 25f, Tuning);

            Assert.Greater(light, heavy);
        }

        [Test]
        public void JumpHeight_SecondPairOfLegs_HelpsButLessThanDouble()
        {
            PartRuleSet rules = GenomeTestFixtures.Rules();
            CreatureGenome onePair = GenomeTestFixtures.Spine();
            onePair.AddPart(PartGene.Default(GenomeTestFixtures.LegsId, 1, mirrored: true));
            CreatureGenome twoPairs = GenomeTestFixtures.Spine();
            twoPairs.AddPart(PartGene.Default(GenomeTestFixtures.LegsId, 0, mirrored: true));
            twoPairs.AddPart(PartGene.Default(GenomeTestFixtures.LegsId, 2, mirrored: true));

            float one = CreatureJumpRules.JumpHeight(onePair, rules, 10f, Tuning);
            float two = CreatureJumpRules.JumpHeight(twoPairs, rules, 10f, Tuning);

            Assert.Greater(two, one);
            Assert.Less(two, one * 2f);
        }

        [Test]
        public void TakeOffSpeed_ReachesTheRequestedHeight()
        {
            const float height = 1.2f;
            float v = CreatureJumpRules.TakeOffSpeed(height, 9.81f);

            // Ballistic peak: v^2 / 2g.
            Assert.AreEqual(height, v * v / (2f * 9.81f), 1e-4f);
            Assert.AreEqual(0f, CreatureJumpRules.TakeOffSpeed(0f, 9.81f));
        }
    }
}
