using Leeway.Creature.Domain;
using NUnit.Framework;
using UnityEngine;

namespace Leeway.Tests
{
    /// <summary>
    /// Feet and hands as parts in their own right: fitted into a limb's socket, priced, carried over
    /// the wire and written into a preset.
    /// </summary>
    public class PartFittingTests
    {
        private static readonly int LimbId = StableHash.PartId("loco.legs");
        private static readonly int HoofId = StableHash.PartId("limb.hoof");
        private static readonly int PawId = StableHash.PartId("limb.paw");

        private const int LimbCost = 25;
        private const int HoofCost = 10;
        private const int PawCost = 30;

        /// <summary>A catalog with one limb that takes a foot, and two feet to choose between.</summary>
        private static PartRuleSet Rules(int budget = 1000)
            => new PartRuleSet(new[]
            {
                new PartRule(LimbId, PartCategory.Locomotion, AttachmentSite.Torso, true,
                    new PartStatContribution { SpeedBonus = 1.5f, MassKg = 1.2f }, LimbCost,
                    new LegSpec(1, 0.3f), 0f, true, acceptsFitting: true, defaultFittingId: HoofId),

                new PartRule(HoofId, PartCategory.Extremity, AttachmentSite.None, false,
                    new PartStatContribution { SpeedBonus = 0.4f, MassKg = 0.5f }, HoofCost),

                new PartRule(PawId, PartCategory.Extremity, AttachmentSite.None, false,
                    new PartStatContribution { DamageBonus = 1f, MassKg = 0.4f }, PawCost),

                new PartRule(StableHash.PartId("mouth.jaw"), PartCategory.Mouth, AttachmentSite.Head, false,
                    new PartStatContribution { DamageBonus = 7f }, 25),
            }, vertebraCost: 10, budget: budget);

        private static CreatureGenome WithLimb(PartRuleSet rules, int fittingId, out int partIndex)
        {
            CreatureGenome genome = GenomeTestFixtures.Spine();

            var gene = new PartGene(LimbId, 1, new Vector3(0.3f, -1f, 0f), Quaternion.identity, 1f, true,
                false, default, default, 0, fittingId);

            Assert.IsTrue(GenomeEditOperations.TryAttachPart(genome, gene, rules, out GenomeError error), $"attach failed: {error}");
            partIndex = genome.PartCount - 1;

            return genome;
        }

        [Test]
        public void ALimbWithAFoot_IsValid()
        {
            PartRuleSet rules = Rules();
            CreatureGenome genome = WithLimb(rules, HoofId, out _);

            Assert.IsTrue(GenomeValidator.Validate(genome, rules).IsValid);
        }

        /// <summary>A hoof is not a body part: it goes into a limb or nowhere.</summary>
        [Test]
        public void AFootAttachedToTheSpine_IsRejected()
        {
            PartRuleSet rules = Rules();
            CreatureGenome genome = GenomeTestFixtures.Spine();

            genome.AddPart(new PartGene(HoofId, 1, new Vector3(0.3f, -1f, 0f), Quaternion.identity, 1f, false));

            GenomeValidationResult result = GenomeValidator.Validate(genome, rules);

            Assert.IsFalse(result.IsValid);
            Assert.AreEqual(GenomeError.InvalidAttachmentSite, result.Error);
        }

        [Test]
        public void SomethingFittedIntoAPartWithNoSocket_IsRejected()
        {
            PartRuleSet rules = Rules();
            CreatureGenome genome = GenomeTestFixtures.Spine();

            genome.AddPart(new PartGene(StableHash.PartId("mouth.jaw"), 0, new Vector3(0f, 0f, 1f),
                Quaternion.identity, 1f, false, false, default, default, 0, HoofId));

            GenomeValidationResult result = GenomeValidator.Validate(genome, rules);

            Assert.IsFalse(result.IsValid);
            Assert.AreEqual(GenomeError.FittingNotSupported, result.Error);
        }

        [Test]
        public void ALimbFittedWithSomethingThatIsNotAFoot_IsRejected()
        {
            PartRuleSet rules = Rules();
            CreatureGenome genome = WithLimb(rules, StableHash.PartId("mouth.jaw"), out _);

            GenomeValidationResult result = GenomeValidator.Validate(genome, rules);

            Assert.IsFalse(result.IsValid);
            Assert.AreEqual(GenomeError.InvalidFitting, result.Error);
        }

        [Test]
        public void TheFootIsPaidFor()
        {
            PartRuleSet rules = Rules();

            int bare = CreatureBudget.Evaluate(WithLimb(rules, 0, out _), rules).Spent;
            int shod = CreatureBudget.Evaluate(WithLimb(rules, HoofId, out _), rules).Spent;

            Assert.AreEqual(HoofCost, shod - bare);
        }

        /// <summary>Swapping one foot for another is priced as the difference, not as a second purchase.</summary>
        [Test]
        public void SwappingAFoot_ChargesOnlyTheDifference()
        {
            PartRuleSet rules = Rules();
            CreatureGenome genome = WithLimb(rules, HoofId, out int partIndex);

            int before = CreatureBudget.Evaluate(genome, rules).Spent;

            Assert.IsTrue(GenomeEditOperations.TrySetPartFitting(genome, partIndex, PawId, rules, out GenomeError error), $"{error}");
            Assert.AreEqual(PawId, genome.GetPart(partIndex).FittingId);

            int after = CreatureBudget.Evaluate(genome, rules).Spent;
            Assert.AreEqual(PawCost - HoofCost, after - before);
        }

        [Test]
        public void AFootTooExpensiveForWhatIsLeft_IsRefused()
        {
            // Room for the vertebrae and the limb, and four coins over — not enough for the paw.
            PartRuleSet rules = Rules(budget: 3 * 10 + LimbCost + HoofCost + 4);
            CreatureGenome genome = WithLimb(rules, HoofId, out int partIndex);

            Assert.IsFalse(GenomeEditOperations.TrySetPartFitting(genome, partIndex, PawId, rules, out GenomeError error));
            Assert.AreEqual(GenomeError.InsufficientFunds, error);
            Assert.AreEqual(HoofId, genome.GetPart(partIndex).FittingId, "A refused swap must leave the old foot on.");
        }

        [Test]
        public void TakingTheFootOut_IsAllowedAndRefunded()
        {
            PartRuleSet rules = Rules();
            CreatureGenome genome = WithLimb(rules, HoofId, out int partIndex);

            int before = CreatureBudget.Evaluate(genome, rules).Spent;

            Assert.IsTrue(GenomeEditOperations.TrySetPartFitting(genome, partIndex, 0, rules, out _));
            Assert.IsFalse(genome.GetPart(partIndex).HasFitting);
            Assert.AreEqual(-HoofCost, CreatureBudget.Evaluate(genome, rules).Spent - before);
        }

        /// <summary>
        /// The foot has to count towards the creature's stats, or choosing between a hoof and a paw
        /// would be a decision about nothing.
        /// </summary>
        [Test]
        public void TheFootContributesItsStats()
        {
            PartRuleSet rules = Rules();
            CreatureStatTuning tuning = CreatureStatTuning.Default;

            CreatureStats bare = GenomeStatRules.Derive(WithLimb(rules, 0, out _), rules, tuning);
            CreatureStats clawed = GenomeStatRules.Derive(WithLimb(rules, PawId, out _), rules, tuning);

            Assert.Greater(clawed.AttackDamage, bare.AttackDamage, "The paw's claws have to show up in the damage.");
            Assert.Greater(clawed.Mass, bare.Mass, "A foot weighs something.");
        }

        [Test]
        public void TheFootSurvivesTheWire()
        {
            PartRuleSet rules = Rules();
            CreatureGenome genome = WithLimb(rules, PawId, out int partIndex);

            byte[] blob = GenomeCodec.Encode(genome);

            Assert.IsTrue(GenomeCodec.TryDecode(blob, out CreatureGenome decoded, out GenomeError error), $"{error}");
            Assert.AreEqual(PawId, decoded.GetPart(partIndex).FittingId);
        }

        [Test]
        public void TheFootSurvivesAPresetFile()
        {
            PartRuleSet rules = Rules();
            CreatureGenome genome = WithLimb(rules, PawId, out int partIndex);

            string json = GenomeDocument.From(genome, "test", id => id == PawId ? "limb.paw" : "loco.legs").ToJson();

            Assert.IsTrue(GenomeDocument.TryFromJson(json, out GenomeDocument document, out string error), error);
            Assert.AreEqual(PawId, document.ToGenome().GetPart(partIndex).FittingId);
        }

        /// <summary>
        /// A genome written before limbs had sockets still has to load — its limbs simply come back
        /// with nothing fitted, which is exactly what they had.
        /// </summary>
        [Test]
        public void AGenomeFromBeforeSockets_StillLoads()
        {
            PartRuleSet rules = Rules();
            CreatureGenome genome = WithLimb(rules, HoofId, out int partIndex);

            byte[] blob = GenomeCodec.Encode(genome);

            // Rewrite it as the previous format: version 3, four bytes shorter per part.
            var older = new byte[GenomeCodec.HeaderBytes + genome.VertebraCount * GenomeCodec.VertebraBytes
                                 + genome.PartCount * GenomeCodec.PartBytesV3];

            System.Array.Copy(blob, older, GenomeCodec.HeaderBytes + genome.VertebraCount * GenomeCodec.VertebraBytes);
            older[1] = 3;

            int from = GenomeCodec.HeaderBytes + genome.VertebraCount * GenomeCodec.VertebraBytes;
            int to = from;
            for (int i = 0; i < genome.PartCount; i++)
            {
                System.Array.Copy(blob, from, older, to, GenomeCodec.PartBytesV3);
                from += GenomeCodec.PartBytes;
                to += GenomeCodec.PartBytesV3;
            }

            Assert.IsTrue(GenomeCodec.TryDecode(older, out CreatureGenome decoded, out GenomeError error), $"{error}");
            Assert.AreEqual(0, decoded.GetPart(partIndex).FittingId);
            Assert.AreEqual(LimbId, decoded.GetPart(partIndex).PartId);
        }
    }
}
