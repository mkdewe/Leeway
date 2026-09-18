using FishNet.Serializing;
using Leeway.Creature.Domain;
using Leeway.CreatureEditor;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Leeway.Tests
{
    /// <summary>
    /// Tests for the genome transport layer against a real FishNet <see cref="Writer"/> /
    /// <see cref="Reader"/>. Writes and reads go through the same code as the network does, so they
    /// also catch a drift in the codegen convention.
    /// </summary>
    public class GenomePayloadNetworkTests
    {
        private static Reader ReaderOver(Writer writer)
            => new Reader(writer.GetArraySegment(), null);

        private static CreatureGenome Genome(int seed = 5)
            => StarterGenomeFactory.Create(seed, GenomeTestFixtures.Rules());

        [Test]
        public void WriteRead_RoundTripsBlobAndHash()
        {
            var original = GenomePayload.FromGenome(Genome());

            var writer = new Writer();
            writer.WriteGenomePayload(original);
            GenomePayload restored = ReaderOver(writer).ReadGenomePayload();

            Assert.AreEqual(original.Hash, restored.Hash, "The hash did not survive transport.");
            Assert.AreEqual(original.Blob.Length, restored.Blob.Length, "The blob length did not survive transport.");
            CollectionAssert.AreEqual(original.Blob, restored.Blob, "The blob differs after the round-trip.");
        }

        [Test]
        public void WriteRead_DecodedGenomeMatchesOriginal()
        {
            CreatureGenome source = Genome(11);

            var writer = new Writer();
            writer.WriteGenomePayload(GenomePayload.FromGenome(source));
            GenomePayload restored = ReaderOver(writer).ReadGenomePayload();

            Assert.IsTrue(restored.TryDecode(out CreatureGenome decoded, out GenomeError error), $"Decoding after transport failed: {error}.");
            Assert.AreEqual(source.VertebraCount, decoded.VertebraCount);
            Assert.AreEqual(source.PartCount, decoded.PartCount);
        }

        [Test]
        public void WriteRead_NullBlob_SurvivesAsEmpty()
        {
            var writer = new Writer();
            writer.WriteGenomePayload(default);

            GenomePayload restored = ReaderOver(writer).ReadGenomePayload();
            Assert.IsTrue(restored.IsEmpty, "An empty payload should come back empty, not as a zero-length array with a hash.");
        }

        /// <summary>
        /// The most important test in this file. A client declaring an absurd length has to be rejected
        /// <b>before</b> the allocation — otherwise it takes the server down on out-of-memory before
        /// genome validation even starts.
        /// </summary>
        [Test]
        public void Read_DeclaredLengthBeyondLimit_RejectedBeforeAllocating()
        {
            var writer = new Writer();
            writer.WriteInt32(int.MaxValue);

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Rejected a genome with a declared length"));
            GenomePayload restored = ReaderOver(writer).ReadGenomePayload();

            Assert.IsTrue(restored.IsEmpty, "A payload declaring a length of 2 GB was accepted.");
        }

        [Test]
        public void Read_LengthJustOverCap_Rejected()
        {
            var writer = new Writer();
            writer.WriteInt32(GenomeLimits.MaxBlobBytes + 1);

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Rejected a genome with a declared length"));
            Assert.IsTrue(ReaderOver(writer).ReadGenomePayload().IsEmpty, "Blob o jeden bajt ponad limit przeszedl.");
        }

        [Test]
        public void Read_NegativeLength_Rejected()
        {
            var writer = new Writer();
            writer.WriteInt32(-42);

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Rejected a genome with a declared length"));
            Assert.IsTrue(ReaderOver(writer).ReadGenomePayload().IsEmpty, "A negative length other than the sentinel should be rejected.");
        }

        [Test]
        public void MaximumLegalGenome_FitsInTransport()
        {
            PartRuleSet rules = GenomeTestFixtures.Rules();
            CreatureGenome genome = StarterGenomeFactory.Create(1, rules);

            while (GenomeEditOperations.TryAddVertebra(genome, genome.VertebraCount - 1, rules, out _)) { }

            var payload = GenomePayload.FromGenome(genome);
            Assert.AreEqual(GenomeLimits.MaxVertebrae, genome.VertebraCount, "Failed to reach the maximum vertebra count.");
            Assert.LessOrEqual(payload.Blob.Length, GenomeLimits.MaxBlobBytes,
                $"The maximum genome ({payload.Blob.Length} B) does not fit the transport limit.");

            var writer = new Writer();
            writer.WriteGenomePayload(payload);
            Assert.AreEqual(payload.Hash, ReaderOver(writer).ReadGenomePayload().Hash);
        }

        /// <summary>
        /// <c>SyncVar&lt;T&gt;</c> detects a change through <c>EqualityComparer&lt;T&gt;.Default</c>.
        /// Without an <c>Equals</c> of its own it would compare the array by reference and every write
        /// would count as a change — that is, a full body rebuild for everyone, every tick.
        /// </summary>
        [Test]
        public void Equality_ComparesContentNotReference()
        {
            CreatureGenome genome = Genome(9);

            var a = GenomePayload.FromGenome(genome);
            var b = GenomePayload.FromGenome(genome);

            Assert.AreNotSame(a.Blob, b.Blob, "The test is meaningless if both instances share the same array.");
            Assert.IsTrue(a.Equals(b), "Two payloads built from the same genome should be equal.");
            Assert.IsTrue(System.Collections.Generic.EqualityComparer<GenomePayload>.Default.Equals(a, b),
                "The default comparer — the one SyncVar uses — did not consider the payloads equal.");
        }

        [Test]
        public void Equality_DifferentGenomes_AreNotEqual()
        {
            PartRuleSet rules = GenomeTestFixtures.Rules();
            CreatureGenome genome = StarterGenomeFactory.Create(4, rules);
            var before = GenomePayload.FromGenome(genome);

            Assert.IsTrue(GenomeEditOperations.TryAddVertebra(genome, genome.VertebraCount - 1, rules, out _));
            var after = GenomePayload.FromGenome(genome);

            Assert.IsFalse(before.Equals(after), "Adding a vertebra did not change the payload — the commit would be invisible to SyncVar.");
        }
    }
}
