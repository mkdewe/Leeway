using Leeway.Creature.Domain;
using Leeway.CreatureEditor;
using NUnit.Framework;
using UnityEngine;

namespace Leeway.Tests
{
    /// <summary>
    /// The skin texture has to match the body it covers, or the paint smears.
    /// </summary>
    /// <remarks>
    /// The failure this guards against is not a crash: a fixed square texture spread the same number of
    /// texels around a 40 cm waist as along a 3 m body, so a stroke came out four times coarser along
    /// the creature than across it. That is the "the paint blurs" nobody could tune away with the brush.
    /// </remarks>
    public class SkinTextureSizeTests
    {
        private GameObject _host;
        private CreatureSkinCanvas _canvas;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("SkinCanvasHost");
            _canvas = _host.AddComponent<CreatureSkinCanvas>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null) Object.DestroyImmediate(_host);
        }

        /// <summary>A tube of <paramref name="vertebrae"/> rings, each <paramref name="step"/> apart.</summary>
        private static CreatureGenome Tube(int vertebrae, float step, float radius)
        {
            var genome = new CreatureGenome();
            for (int i = 0; i < vertebrae; i++)
                genome.AddVertebra(new VertebraGene(i == 0 ? Vector3.zero : new Vector3(0f, 0f, -step),
                    Quaternion.identity, radius));

            return genome;
        }

        private static float Length(int vertebrae, float step, float radius) => (vertebrae - 1) * step + radius * 2f;

        [Test]
        public void ALongBody_GetsATallTexture()
        {
            Vector2Int size = _canvas.SkinTextureSize(Tube(9, 0.35f, 0.16f));

            Assert.Greater(size.y, size.x, "A body far longer than it is round needs the detail along it.");
        }

        [Test]
        public void AFatBody_GetsAWideTexture()
        {
            Vector2Int size = _canvas.SkinTextureSize(Tube(3, 0.30f, 0.45f));

            Assert.Greater(size.x, size.y, "A body far rounder than it is long needs the detail around it.");
        }

        /// <summary>
        /// The whole point: a texel covers about the same patch of skin in both directions, so a
        /// brush stroke is as sharp along the creature as across it.
        /// </summary>
        [Test]
        public void TexelsComeOutRoughlySquare()
        {
            foreach ((int vertebrae, float step, float radius) in new[]
                     {
                         (9, 0.35f, 0.16f),
                         (3, 0.30f, 0.45f),
                         (5, 0.28f, 0.26f),
                         (2, 0.35f, 0.20f),
                     })
            {
                Vector2Int size = _canvas.SkinTextureSize(Tube(vertebrae, step, radius));

                float around = size.x / (2f * Mathf.PI * radius);
                float along = size.y / Length(vertebrae, step, radius);
                float ratio = Mathf.Max(around, along) / Mathf.Min(around, along);

                Assert.Less(ratio, 2.1f,
                    $"{vertebrae} vertebrae of {step} at radius {radius}: texel density is {around:F0} around " +
                    $"and {along:F0} along — the paint will smear in the coarser direction.");
            }
        }

        [Test]
        public void ABiggerCreature_GetsABiggerTexture()
        {
            Vector2Int small = _canvas.SkinTextureSize(Tube(3, 0.2f, 0.12f));
            Vector2Int large = _canvas.SkinTextureSize(Tube(8, 0.4f, 0.4f));

            Assert.Less(small.x * small.y, large.x * large.y, "Detail has to keep up with the surface it covers.");
        }

        [Test]
        public void TheSizesArePowersOfTwoWithinTheLimits()
        {
            Vector2Int size = _canvas.SkinTextureSize(Tube(6, 0.33f, 0.28f));

            Assert.AreEqual(size.x, Mathf.NextPowerOfTwo(size.x));
            Assert.AreEqual(size.y, Mathf.NextPowerOfTwo(size.y));
            Assert.GreaterOrEqual(size.x, 256);
            Assert.LessOrEqual(size.y, 2048);
        }

        [Test]
        public void AGenomeWithNothingInIt_StillGivesAUsableTexture()
        {
            Vector2Int size = _canvas.SkinTextureSize(new CreatureGenome());

            Assert.GreaterOrEqual(size.x, 256);
            Assert.GreaterOrEqual(size.y, 256);
        }
    }

    /// <summary>The showcase humanoid, checked the way the shipped enemies are.</summary>
    public class ShowcaseGenomeTests
    {
        private static PartRuleSet CatalogRules()
        {
            CreaturePartCatalog catalog = Leeway.CreatureEditor.Authoring.PartAuthoringAudit.FindCatalog();
            Assert.IsNotNull(catalog, "The project has no part catalog.");

            return catalog.BuildRuleSet();
        }

        [Test]
        public void Humanoid_IsAValidCreature()
        {
            PartRuleSet rules = CatalogRules();

            GenomeValidationResult result = GenomeValidator.Validate(ShowcaseGenomes.Humanoid(rules), rules);

            Assert.IsTrue(result.IsValid, $"The humanoid does not validate: {result.Error}.");
        }

        [Test]
        public void Humanoid_StandsOnTwoLegsWithArmsAndAFace()
        {
            PartRuleSet rules = CatalogRules();
            CreatureGenome genome = ShowcaseGenomes.Humanoid(rules);

            int legs = 0, arms = 0, faces = 0;
            for (int i = 0; i < genome.PartCount; i++)
            {
                PartGene gene = genome.GetPart(i);
                if (!rules.TryGetRule(gene.PartId, out PartRule rule)) continue;

                if (rule.Category == PartCategory.Locomotion) legs++;
                if (rule.Category == PartCategory.Grasper) arms++;
                if (rule.Category == PartCategory.Mouth || rule.Category == PartCategory.Sense) faces++;

                if (rule.AcceptsFitting)
                    Assert.IsTrue(gene.HasFitting, "A limb on the showcase creature should not end in a stump.");
            }

            Assert.AreEqual(1, legs, "One mirrored pair of legs.");
            Assert.AreEqual(1, arms, "One mirrored pair of arms.");
            Assert.GreaterOrEqual(faces, 2, "A face needs a mouth and eyes.");
            Assert.Greater(GenomeStatRules.StandHeight(genome, rules), 0.5f, "It should stand up, not lie down.");
        }

        [Test]
        public void Humanoid_FitsTheBudget()
        {
            PartRuleSet rules = CatalogRules();

            Assert.GreaterOrEqual(CreatureBudget.Evaluate(ShowcaseGenomes.Humanoid(rules), rules).Remaining, 0);
        }
    }
}
