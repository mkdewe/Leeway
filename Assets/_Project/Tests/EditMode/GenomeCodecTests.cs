using Leeway.Creature.Domain;
using NUnit.Framework;
using UnityEngine;

namespace Leeway.Tests
{
    /// <summary>
    /// The blob format is a network contract — these tests make sure the round-trip loses no data, and
    /// that damaged or hostile input ends in an error code rather than an exception or a huge
    /// allocation.
    /// </summary>
    public class GenomeCodecTests
    {
        private const float PositionTolerance = 0.001f;

        [Test]
        public void RoundTrip_PreservesRebuiltLeg()
        {
            var source = new CreatureGenome();
            source.AddVertebra(new VertebraGene(Vector3.zero, Quaternion.identity, 0.3f));
            source.AddVertebra(new VertebraGene(new Vector3(0f, 0f, -0.35f), Quaternion.identity, 0.3f));

            source.AddPart(PartGene.Default(1234, 1, mirrored: true).WithLeg(new LegSpec(3, 0.275f)));
            source.AddPart(PartGene.Default(5678, 0, mirrored: false));

            byte[] blob = GenomeCodec.Encode(source);
            Assert.IsTrue(GenomeCodec.TryDecode(blob, out CreatureGenome decoded, out GenomeError error), $"decoding failed: {error}");

            PartGene rebuilt = decoded.GetPart(0);
            Assert.IsTrue(rebuilt.HasLegOverride, "A rebuilt leg has to survive the trip over the wire.");
            Assert.AreEqual(3, rebuilt.Leg.BendPoints);
            Assert.AreEqual(0.275f, rebuilt.Leg.SegmentLength, PositionTolerance);

            Assert.IsFalse(decoded.GetPart(1).HasLegOverride, "A part with no rebuild must not acquire one along the way.");
        }

        [Test]
        public void Decode_AcceptsVersionOneBlobAndFallsBackToCatalogLeg()
        {
            // A genome from before the leg record was introduced: the same layout, a shorter part
            // record and three bytes missing at the end. It has to load, because otherwise every saved
            // creature turns to garbage on the day this ships.
            var source = new CreatureGenome();
            source.AddVertebra(new VertebraGene(Vector3.zero, Quaternion.identity, 0.3f));
            source.AddVertebra(new VertebraGene(new Vector3(0f, 0f, -0.35f), Quaternion.identity, 0.3f));
            source.AddPart(PartGene.Default(1234, 1, mirrored: true));

            byte[] current = GenomeCodec.Encode(source);

            int headerAndSpine = GenomeCodec.HeaderBytes + 2 * GenomeCodec.VertebraBytes;
            var legacy = new byte[headerAndSpine + GenomeCodec.PartBytesV1];
            System.Array.Copy(current, legacy, legacy.Length);
            legacy[1] = 1;

            Assert.IsTrue(GenomeCodec.TryDecode(legacy, out CreatureGenome decoded, out GenomeError error), $"the legacy genome did not load: {error}");

            Assert.AreEqual(1, decoded.PartCount);
            Assert.IsTrue(decoded.GetPart(0).Mirrored);
            Assert.IsFalse(decoded.GetPart(0).HasLegOverride,
                "A genome from before the leg record should take the build from the catalog, not guess it.");
        }

        [Test]
        public void RoundTrip_PreservesSpineWithinQuantizationTolerance()
        {
            var source = new CreatureGenome();
            source.AddVertebra(new VertebraGene(new Vector3(0.12f, -0.34f, 0.56f), Quaternion.Euler(10f, 20f, 30f), 0.42f));
            source.AddVertebra(new VertebraGene(new Vector3(0f, 0f, -0.35f), Quaternion.Euler(-15f, 0f, 5f), 0.28f));

            byte[] blob = GenomeCodec.Encode(source);
            Assert.IsTrue(GenomeCodec.TryDecode(blob, out CreatureGenome decoded, out GenomeError error), $"decoding failed: {error}");

            Assert.AreEqual(source.VertebraCount, decoded.VertebraCount);
            for (int i = 0; i < source.VertebraCount; i++)
            {
                VertebraGene expected = source.GetVertebra(i);
                VertebraGene actual = decoded.GetVertebra(i);

                Assert.AreEqual(expected.LocalOffset.x, actual.LocalOffset.x, PositionTolerance);
                Assert.AreEqual(expected.LocalOffset.y, actual.LocalOffset.y, PositionTolerance);
                Assert.AreEqual(expected.LocalOffset.z, actual.LocalOffset.z, PositionTolerance);
                Assert.AreEqual(expected.Radius, actual.Radius, PositionTolerance);

                // Quaternions are compared by angle — the representation may differ in sign.
                Assert.Less(Quaternion.Angle(expected.LocalRotation, actual.LocalRotation), 0.05f);
            }
        }

        [Test]
        public void RoundTrip_PreservesParts()
        {
            var source = GenomeTestFixtures.Valid();
            source.SetPart(0, source.GetPart(0).WithScale(1.75f).WithLocalPosition(new Vector3(0.2f, -0.1f, 0.05f)));

            byte[] blob = GenomeCodec.Encode(source);
            Assert.IsTrue(GenomeCodec.TryDecode(blob, out CreatureGenome decoded, out _));

            Assert.AreEqual(source.PartCount, decoded.PartCount);
            for (int i = 0; i < source.PartCount; i++)
            {
                PartGene expected = source.GetPart(i);
                PartGene actual = decoded.GetPart(i);

                Assert.AreEqual(expected.PartId, actual.PartId);
                Assert.AreEqual(expected.BoneIndex, actual.BoneIndex);
                Assert.AreEqual(expected.Mirrored, actual.Mirrored);
                Assert.AreEqual(expected.Scale, actual.Scale, PositionTolerance);
                Assert.AreEqual(expected.LocalPosition.x, actual.LocalPosition.x, PositionTolerance);
            }
        }

        [Test]
        public void RoundTrip_PreservesColors()
        {
            var source = GenomeTestFixtures.Valid();
            source.PrimaryColor = new Color32(11, 222, 133, 255);
            source.SecondaryColor = new Color32(9, 8, 7, 255);

            byte[] blob = GenomeCodec.Encode(source);
            Assert.IsTrue(GenomeCodec.TryDecode(blob, out CreatureGenome decoded, out _));

            Assert.AreEqual(source.PrimaryColor.r, decoded.PrimaryColor.r);
            Assert.AreEqual(source.PrimaryColor.g, decoded.PrimaryColor.g);
            Assert.AreEqual(source.PrimaryColor.b, decoded.PrimaryColor.b);
            Assert.AreEqual(source.SecondaryColor.g, decoded.SecondaryColor.g);
        }

        [Test]
        public void ComputeSize_MatchesEncodedLength()
        {
            var genome = GenomeTestFixtures.Valid();
            Assert.AreEqual(GenomeCodec.ComputeSize(genome), GenomeCodec.Encode(genome).Length);
        }

        [Test]
        public void Encode_MaximumGenome_FitsWithinBlobLimit()
        {
            var genome = GenomeTestFixtures.Spine(GenomeLimits.MaxVertebrae);
            for (int i = 0; i < GenomeLimits.MaxParts; i++)
                genome.AddPart(PartGene.Default(GenomeTestFixtures.EyeId, 0, mirrored: true));

            byte[] blob = GenomeCodec.Encode(genome);

            Assert.LessOrEqual(blob.Length, GenomeLimits.MaxBlobBytes,
                "The maximum genome has to fit the blob limit, otherwise a player can build a creature that cannot be sent.");
        }

        [Test]
        public void TryDecode_TruncatedBuffer_FailsWithoutThrowing()
        {
            byte[] blob = GenomeCodec.Encode(GenomeTestFixtures.Valid());
            var truncated = new byte[blob.Length - 5];
            System.Array.Copy(blob, truncated, truncated.Length);

            Assert.IsFalse(GenomeCodec.TryDecode(truncated, out _, out GenomeError error));
            Assert.AreEqual(GenomeError.MalformedPayload, error);
        }

        [Test]
        public void TryDecode_NullOrTooShort_ReturnsMalformed()
        {
            Assert.IsFalse(GenomeCodec.TryDecode(null, out _, out GenomeError nullError));
            Assert.AreEqual(GenomeError.MalformedPayload, nullError);

            Assert.IsFalse(GenomeCodec.TryDecode(new byte[3], out _, out GenomeError shortError));
            Assert.AreEqual(GenomeError.MalformedPayload, shortError);
        }

        [Test]
        public void TryDecode_BadMagic_ReturnsMalformed()
        {
            byte[] blob = GenomeCodec.Encode(GenomeTestFixtures.Valid());
            blob[0] = 0x00;

            Assert.IsFalse(GenomeCodec.TryDecode(blob, out _, out GenomeError error));
            Assert.AreEqual(GenomeError.MalformedPayload, error);
        }

        [Test]
        public void TryDecode_UnknownVersion_ReturnsUnsupportedVersion()
        {
            byte[] blob = GenomeCodec.Encode(GenomeTestFixtures.Valid());
            blob[1] = 200;

            Assert.IsFalse(GenomeCodec.TryDecode(blob, out _, out GenomeError error));
            Assert.AreEqual(GenomeError.UnsupportedVersion, error);
        }

        [Test]
        public void TryDecode_OversizedBuffer_ReturnsPayloadTooLarge()
        {
            var blob = new byte[GenomeLimits.MaxBlobBytes + 1];
            blob[0] = GenomeCodec.Magic;
            blob[1] = GenomeCodec.Version;

            Assert.IsFalse(GenomeCodec.TryDecode(blob, out _, out GenomeError error));
            Assert.AreEqual(GenomeError.PayloadTooLarge, error);
        }

        [Test]
        public void TryDecode_DeclaredCountBeyondLimit_IsRejected()
        {
            byte[] blob = GenomeCodec.Encode(GenomeTestFixtures.Valid());
            blob[2] = GenomeLimits.MaxVertebrae + 1;

            Assert.IsFalse(GenomeCodec.TryDecode(blob, out _, out GenomeError error));
            Assert.AreEqual(GenomeError.TooManyVertebrae, error);
        }

        [Test]
        public void TryDecode_CountMismatchingLength_ReturnsMalformed()
        {
            byte[] blob = GenomeCodec.Encode(GenomeTestFixtures.Valid());
            blob[3] = (byte)(blob[3] + 1); // declares one part more than the buffer holds

            Assert.IsFalse(GenomeCodec.TryDecode(blob, out _, out GenomeError error));
            Assert.AreEqual(GenomeError.MalformedPayload, error);
        }
    }
}
