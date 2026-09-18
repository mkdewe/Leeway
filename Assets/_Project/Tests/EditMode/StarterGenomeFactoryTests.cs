using Leeway.Creature.Domain;
using NUnit.Framework;

namespace Leeway.Tests
{
    /// <summary>
    /// The starter genome is computed by the server from the ClientId and the world seed. Were it not
    /// deterministic, or were it to fail its own validation, the player would get a creature the server
    /// would reject on the spot.
    /// </summary>
    public class StarterGenomeFactoryTests
    {
        private PartRuleSet _rules;

        [SetUp]
        public void SetUp() => _rules = GenomeTestFixtures.Rules();

        [Test]
        public void Create_SameSeed_ProducesByteIdenticalGenome()
        {
            byte[] first = GenomeCodec.Encode(StarterGenomeFactory.Create(4711, _rules));
            byte[] second = GenomeCodec.Encode(StarterGenomeFactory.Create(4711, _rules));

            CollectionAssert.AreEqual(first, second);
        }

        [Test]
        public void Create_DifferentSeeds_ProduceDifferentGenomes()
        {
            byte[] reference = GenomeCodec.Encode(StarterGenomeFactory.Create(1, _rules));

            bool anyDifferent = false;
            for (int seed = 2; seed <= 12 && !anyDifferent; seed++)
            {
                byte[] other = GenomeCodec.Encode(StarterGenomeFactory.Create(seed, _rules));
                anyDifferent = !AreEqual(reference, other);
            }

            Assert.IsTrue(anyDifferent, "Different seeds must give different creatures, otherwise every player looks the same.");
        }

        [Test]
        public void Create_AlwaysPassesFullValidation()
        {
            for (int seed = 0; seed < 64; seed++)
            {
                CreatureGenome genome = StarterGenomeFactory.Create(seed, _rules);
                GenomeValidationResult result = GenomeValidator.Validate(genome, _rules);

                Assert.IsTrue(result.IsValid, $"seed {seed}: {result}");
            }
        }

        [Test]
        public void Create_HasStarterSpineAndOnePartPerCategory()
        {
            CreatureGenome genome = StarterGenomeFactory.Create(1234, _rules);

            Assert.AreEqual(StarterGenomeFactory.StarterVertebraCount, genome.VertebraCount);
            Assert.AreEqual(1, genome.CountParts(PartCategory.Locomotion, _rules));
            Assert.AreEqual(1, genome.CountParts(PartCategory.Mouth, _rules));
            Assert.AreEqual(1, genome.CountParts(PartCategory.Sense, _rules));
        }

        [Test]
        public void Create_ZeroSeed_IsStillValid()
        {
            // Seed 0 is a fixed point of xorshift and gets its own handling inside the RNG.
            CreatureGenome genome = StarterGenomeFactory.Create(0, _rules);

            Assert.IsTrue(GenomeValidator.Validate(genome, _rules).IsValid);
        }

        [Test]
        public void Create_EncodedGenome_FitsWithinBlobLimit()
        {
            byte[] blob = GenomeCodec.Encode(StarterGenomeFactory.Create(99, _rules));
            Assert.LessOrEqual(blob.Length, GenomeLimits.MaxBlobBytes);
        }

        [Test]
        public void Create_WithEmptyCatalog_ProducesSpineWithoutParts()
        {
            CreatureGenome genome = StarterGenomeFactory.Create(5, PartRuleSet.Empty);

            Assert.AreEqual(StarterGenomeFactory.StarterVertebraCount, genome.VertebraCount);
            Assert.AreEqual(0, genome.PartCount);
            Assert.IsTrue(GenomeValidator.Validate(genome, PartRuleSet.Empty).IsValid,
                "An incomplete catalog should give a bare but valid creature — not an exception during the build.");
        }

        [Test]
        public void Create_SpineIsAnchoredAtOriginAndExtendsBackwards()
        {
            CreatureGenome genome = StarterGenomeFactory.Create(77, _rules);

            Assert.AreEqual(UnityEngine.Vector3.zero, genome.GetVertebra(0).LocalOffset,
                "Vertebra 0 is the head at the armature origin.");
            Assert.Less(genome.GetVertebra(1).LocalOffset.z, 0f,
                "The spine runs backwards along -Z so that transform.forward matches the Unity convention.");
        }

        private static bool AreEqual(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i]) return false;
            }
            return true;
        }
    }
}
