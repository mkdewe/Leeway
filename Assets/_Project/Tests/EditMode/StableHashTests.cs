using Leeway.Creature.Domain;
using NUnit.Framework;

namespace Leeway.Tests
{
    /// <summary>
    /// Pins the part-id hash to the published FNV-1a vectors. A change in the result of these tests
    /// means every saved genome has stopped resolving its parts — this is not a cosmetic test.
    /// </summary>
    public class StableHashTests
    {
        [Test]
        public void Fnv1a32_EmptyString_ReturnsOffsetBasis()
        {
            Assert.AreEqual(0x811c9dc5u, StableHash.Fnv1a32(string.Empty));
            Assert.AreEqual(0x811c9dc5u, StableHash.Fnv1a32((string)null));
        }

        [Test]
        public void Fnv1a32_KnownVectors_MatchReferenceImplementation()
        {
            Assert.AreEqual(0xe40c292cu, StableHash.Fnv1a32("a"));
            Assert.AreEqual(0xbf9cf968u, StableHash.Fnv1a32("foobar"));
        }

        [Test]
        public void Fnv1a32_Bytes_MatchStringOfSameContent()
        {
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes("foobar");
            Assert.AreEqual(StableHash.Fnv1a32("foobar"), StableHash.Fnv1a32(bytes));
        }

        [Test]
        public void PartId_IsStableAndDistinct()
        {
            Assert.AreEqual(StableHash.PartId("loco.legs"), StableHash.PartId("loco.legs"));
            Assert.AreNotEqual(StableHash.PartId("loco.legs"), StableHash.PartId("loco.fin"));
        }
    }

    public class DeterministicRngTests
    {
        [Test]
        public void SameSeed_ProducesSameSequence()
        {
            var a = new DeterministicRng(1234);
            var b = new DeterministicRng(1234);

            for (int i = 0; i < 32; i++)
                Assert.AreEqual(a.NextUInt(), b.NextUInt());
        }

        [Test]
        public void ZeroSeed_DoesNotCollapseToFixedPoint()
        {
            var rng = new DeterministicRng(0);

            uint first = rng.NextUInt();
            uint second = rng.NextUInt();

            Assert.AreNotEqual(0u, first);
            Assert.AreNotEqual(first, second);
        }

        [Test]
        public void NextInt_StaysWithinBounds()
        {
            var rng = new DeterministicRng(99);

            for (int i = 0; i < 256; i++)
            {
                int value = rng.NextInt(5);
                Assert.GreaterOrEqual(value, 0);
                Assert.Less(value, 5);
            }
        }

        [Test]
        public void NextInt_WithNonPositiveBound_ReturnsZero()
        {
            var rng = new DeterministicRng(7);
            Assert.AreEqual(0, rng.NextInt(0));
            Assert.AreEqual(0, rng.NextInt(-3));
        }

        [Test]
        public void NextFloat_StaysInUnitInterval()
        {
            var rng = new DeterministicRng(4242);

            for (int i = 0; i < 256; i++)
            {
                float value = rng.NextFloat();
                Assert.GreaterOrEqual(value, 0f);
                Assert.Less(value, 1f);
            }
        }
    }
}
