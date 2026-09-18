using Leeway.Creature.Domain;
using NUnit.Framework;
using UnityEngine;

namespace Leeway.Tests
{
    /// <summary>
    /// Validation is the server's last line of defence against a forged genome — every rejection case
    /// has its own test here.
    /// </summary>
    public class GenomeValidatorTests
    {
        private PartRuleSet _rules;

        [SetUp]
        public void SetUp() => _rules = GenomeTestFixtures.Rules();

        [Test]
        public void Validate_CompleteGenome_IsValid()
        {
            GenomeValidationResult result = GenomeValidator.Validate(GenomeTestFixtures.Valid(), _rules);
            Assert.IsTrue(result.IsValid, result.ToString());
        }

        [Test]
        public void Validate_NullGenome_Fails()
        {
            Assert.AreEqual(GenomeError.NullGenome, GenomeValidator.Validate(null, _rules).Error);
        }

        [Test]
        public void SiteForBone_MapsHeadTorsoAndTail()
        {
            // The end vertebrae count as torso too — otherwise a short creature would have nowhere to
            // keep its legs, and shortening the spine would invalidate the ones it already has.
            Assert.AreEqual(AttachmentSite.Head | AttachmentSite.Torso, GenomeValidator.SiteForBone(0, 3));
            Assert.AreEqual(AttachmentSite.Torso, GenomeValidator.SiteForBone(1, 3));
            Assert.AreEqual(AttachmentSite.Tail | AttachmentSite.Torso, GenomeValidator.SiteForBone(2, 3));
            Assert.AreEqual(AttachmentSite.None, GenomeValidator.SiteForBone(5, 3));
        }

        [Test]
        public void Validate_TooFewVertebrae_Fails()
        {
            CreatureGenome genome = GenomeTestFixtures.Spine(1);
            Assert.AreEqual(GenomeError.TooFewVertebrae, GenomeValidator.Validate(genome, _rules).Error);
        }

        [Test]
        public void Validate_TooManyVertebrae_Fails()
        {
            CreatureGenome genome = GenomeTestFixtures.Spine(GenomeLimits.MaxVertebrae + 1);
            Assert.AreEqual(GenomeError.TooManyVertebrae, GenomeValidator.Validate(genome, _rules).Error);
        }

        [Test]
        public void Validate_RadiusOutOfRange_FailsAndReportsIndex()
        {
            CreatureGenome genome = GenomeTestFixtures.Valid();
            genome.SetVertebra(1, genome.GetVertebra(1).WithRadius(GenomeLimits.MaxRadius + 1f));

            GenomeValidationResult result = GenomeValidator.Validate(genome, _rules);

            Assert.AreEqual(GenomeError.RadiusOutOfRange, result.Error);
            Assert.AreEqual(1, result.Index);
        }

        [Test]
        public void Validate_SegmentTooLong_Fails()
        {
            CreatureGenome genome = GenomeTestFixtures.Valid();
            genome.SetVertebra(2, genome.GetVertebra(2).WithOffset(new Vector3(0f, 0f, -5f)));

            Assert.AreEqual(GenomeError.SegmentTooLong, GenomeValidator.Validate(genome, _rules).Error);
        }

        [Test]
        public void Validate_PartOnNonexistentBone_Fails()
        {
            CreatureGenome genome = GenomeTestFixtures.Valid();
            genome.AddPart(PartGene.Default(GenomeTestFixtures.AntennaId, 9, mirrored: false));

            Assert.AreEqual(GenomeError.BoneIndexOutOfRange, GenomeValidator.Validate(genome, _rules).Error);
        }

        [Test]
        public void Validate_UnknownPartId_Fails()
        {
            CreatureGenome genome = GenomeTestFixtures.Valid();
            genome.AddPart(PartGene.Default(GenomeTestFixtures.UnknownId, 0, mirrored: false));

            Assert.AreEqual(GenomeError.UnknownPart, GenomeValidator.Validate(genome, _rules).Error);
        }

        [Test]
        public void Validate_PartOnDisallowedSite_Fails()
        {
            // The jaw is allowed on the head only, and vertebra 1 is torso.
            CreatureGenome genome = GenomeTestFixtures.Spine();
            genome.AddPart(PartGene.Default(GenomeTestFixtures.LegsId, 1, mirrored: true));
            genome.AddPart(PartGene.Default(GenomeTestFixtures.JawId, 1, mirrored: false));

            Assert.AreEqual(GenomeError.InvalidAttachmentSite, GenomeValidator.Validate(genome, _rules).Error);
        }

        [Test]
        public void Validate_MirroredOnNonMirrorCapablePart_Fails()
        {
            CreatureGenome genome = GenomeTestFixtures.Spine();
            genome.AddPart(PartGene.Default(GenomeTestFixtures.LegsId, 1, mirrored: true));
            genome.AddPart(PartGene.Default(GenomeTestFixtures.JawId, 0, mirrored: true));

            Assert.AreEqual(GenomeError.MirrorNotSupported, GenomeValidator.Validate(genome, _rules).Error);
        }

        [Test]
        public void Validate_ScaleOutOfRange_Fails()
        {
            CreatureGenome genome = GenomeTestFixtures.Valid();
            genome.SetPart(0, genome.GetPart(0).WithScale(GenomeLimits.MaxPartScale + 1f));

            Assert.AreEqual(GenomeError.ScaleOutOfRange, GenomeValidator.Validate(genome, _rules).Error);
        }

        [Test]
        public void Validate_WithoutLocomotion_Passes()
        {
            // No category is required any more — a legless creature is legal, and the budget is the
            // only thing that limits how far it can grow.
            CreatureGenome genome = GenomeTestFixtures.Spine();
            genome.AddPart(PartGene.Default(GenomeTestFixtures.JawId, 0, mirrored: false));

            Assert.IsTrue(GenomeValidator.Validate(genome, _rules).IsValid);
        }

        [Test]
        public void Validate_ManyPartsOfOneCategory_Passes()
        {
            CreatureGenome genome = GenomeTestFixtures.Valid();
            genome.AddPart(PartGene.Default(GenomeTestFixtures.FinId, 2, mirrored: true));
            genome.AddPart(PartGene.Default(GenomeTestFixtures.JawId, 0, mirrored: false));
            genome.AddPart(PartGene.Default(GenomeTestFixtures.AntennaId, 0, mirrored: false));
            genome.AddPart(PartGene.Default(GenomeTestFixtures.AntennaId, 1, mirrored: false));

            Assert.IsTrue(GenomeValidator.Validate(genome, _rules).IsValid);
        }

        [Test]
        public void Validate_OverBudget_Fails()
        {
            CreatureGenome genome = GenomeTestFixtures.Valid();
            BudgetReport report = CreatureBudget.Evaluate(genome, _rules);

            // The server prices the budget with its own list — the same genome fails against a tighter pool.
            var tight = GenomeTestFixtures.Rules(budget: report.Spent - 1);

            Assert.AreEqual(GenomeError.InsufficientFunds, GenomeValidator.Validate(genome, tight).Error);
        }

        [Test]
        public void Validate_ExactlyOnBudget_Passes()
        {
            CreatureGenome genome = GenomeTestFixtures.Valid();
            BudgetReport report = CreatureBudget.Evaluate(genome, _rules);

            var exact = GenomeTestFixtures.Rules(budget: report.Spent);

            Assert.IsTrue(GenomeValidator.Validate(genome, exact).IsValid);
        }

        [Test]
        public void Validate_TooManyParts_Fails()
        {
            // A technical blob ceiling, not a gameplay rule — in practice it is only reachable with a
            // forged packet, which is why server-side validation has to catch it.
            CreatureGenome genome = GenomeTestFixtures.Spine();
            for (int i = 0; i <= GenomeLimits.MaxParts; i++)
                genome.AddPart(PartGene.Default(GenomeTestFixtures.EyeId, 0, mirrored: true));

            Assert.AreEqual(GenomeError.TooManyParts, GenomeValidator.Validate(genome, _rules).Error);
        }

        [Test]
        public void Validate_UnknownPart_WhenRuleSetIsEmpty_Fails()
        {
            // A server with an empty catalog can accept nothing — which confirms validation rests on
            // the rules it is handed, not on the contents of the genome.
            Assert.AreEqual(GenomeError.UnknownPart, GenomeValidator.Validate(GenomeTestFixtures.Valid(), PartRuleSet.Empty).Error);
        }
    }
}
