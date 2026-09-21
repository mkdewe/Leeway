using Leeway.Creature.Domain;
using NUnit.Framework;
using UnityEngine;

namespace Leeway.Tests
{
    /// <summary>
    /// A preset is the one place a creature outlives the session it was built in — so what these tests
    /// guard is that saving and loading it changes nothing about it, and that a file somebody has
    /// edited by hand ends in a message rather than in an exception.
    /// </summary>
    public class GenomeDocumentTests
    {
        private const float Tolerance = 0.0001f;

        private static CreatureGenome SampleGenome()
        {
            var genome = new CreatureGenome
            {
                PrimaryColor = new Color32(200, 120, 140, 255),
                SecondaryColor = new Color32(110, 66, 77, 255),
            };

            genome.AddVertebra(new VertebraGene(Vector3.zero, Quaternion.identity, 0.31f));
            genome.AddVertebra(new VertebraGene(new Vector3(0.02f, -0.01f, -0.35f), Quaternion.Euler(0f, 12f, 4f), 0.37f));
            genome.AddVertebra(new VertebraGene(new Vector3(0f, 0f, -0.33f), Quaternion.identity, 0.24f));

            genome.AddPart(new PartGene(StableHash.PartId("loco.hoof_legs"), 1,
                new Vector3(0.35f, -1f, 0f), Quaternion.Euler(5f, 0f, 0f), 1.2f, mirrored: true,
                hasLegOverride: true, new LegSpec(3, 0.275f)));

            genome.AddPart(new PartGene(StableHash.PartId("sense.eye"), 0,
                new Vector3(0.2f, 0.3f, 0.5f), Quaternion.identity, 0.9f, mirrored: true));

            return genome;
        }

        private static string KeyOf(int partId)
        {
            if (partId == StableHash.PartId("loco.hoof_legs")) return "loco.hoof_legs";
            if (partId == StableHash.PartId("sense.eye")) return "sense.eye";
            return null;
        }

        [Test]
        public void RoundTripThroughJson_PreservesTheWholeCreature()
        {
            CreatureGenome source = SampleGenome();

            string json = GenomeDocument.From(source, "Test creature", KeyOf).ToJson();

            Assert.IsTrue(GenomeDocument.TryFromJson(json, out GenomeDocument document, out string error), error);
            CreatureGenome loaded = document.ToGenome();

            Assert.AreEqual(source.VertebraCount, loaded.VertebraCount);
            Assert.AreEqual(source.PartCount, loaded.PartCount);
            Assert.AreEqual(source.PrimaryColor, loaded.PrimaryColor);
            Assert.AreEqual(source.SecondaryColor, loaded.SecondaryColor);
            Assert.AreEqual("Test creature", document.Name);

            for (int i = 0; i < source.VertebraCount; i++)
            {
                VertebraGene expected = source.GetVertebra(i);
                VertebraGene actual = loaded.GetVertebra(i);

                Assert.AreEqual(0f, Vector3.Distance(expected.LocalOffset, actual.LocalOffset), Tolerance, $"vertebra {i} offset");
                Assert.AreEqual(0f, Quaternion.Angle(expected.LocalRotation, actual.LocalRotation), 0.01f, $"vertebra {i} rotation");
                Assert.AreEqual(expected.Radius, actual.Radius, Tolerance, $"vertebra {i} radius");
            }

            for (int i = 0; i < source.PartCount; i++)
            {
                PartGene expected = source.GetPart(i);
                PartGene actual = loaded.GetPart(i);

                Assert.AreEqual(expected.PartId, actual.PartId, $"part {i} id");
                Assert.AreEqual(expected.BoneIndex, actual.BoneIndex, $"part {i} bone");
                Assert.AreEqual(0f, Vector3.Distance(expected.LocalPosition, actual.LocalPosition), Tolerance, $"part {i} position");
                Assert.AreEqual(0f, Quaternion.Angle(expected.LocalRotation, actual.LocalRotation), 0.01f, $"part {i} rotation");
                Assert.AreEqual(expected.Scale, actual.Scale, Tolerance, $"part {i} scale");
                Assert.AreEqual(expected.Mirrored, actual.Mirrored, $"part {i} mirrored");
                Assert.AreEqual(expected.HasLegOverride, actual.HasLegOverride, $"part {i} leg override");
            }

            PartGene leg = loaded.GetPart(0);
            Assert.AreEqual(3, leg.Leg.BendPoints, "The reshaped leg has to survive the save.");
            Assert.AreEqual(0.275f, leg.Leg.SegmentLength, Tolerance);
        }

        [Test]
        public void SavedFile_NamesItsPartsSoAHumanCanReadIt()
        {
            string json = GenomeDocument.From(SampleGenome(), "Test creature", KeyOf).ToJson();

            StringAssert.Contains("loco.hoof_legs", json,
                "A preset that names its parts only by hash cannot be read, diffed or fixed by hand.");
        }

        [Test]
        public void PartKey_OutranksAStaleIdentifier()
        {
            // What a hand-edited preset looks like: the key says one part, the identifier left behind
            // in the file says another. The key is the one that means something.
            var document = new GenomeDocument();
            string json = document.ToJson();

            var patched = json.Replace("\"_parts\": []",
                "\"_parts\": [{\"_key\":\"sense.eye\",\"_id\":12345,\"_bone\":0,\"_scale\":1.0}]");

            Assert.IsTrue(GenomeDocument.TryFromJson(patched, out GenomeDocument loaded, out string error), error);
            Assert.AreEqual(1, loaded.PartCount);
            Assert.AreEqual(StableHash.PartId("sense.eye"), loaded.ToGenome().GetPart(0).PartId,
                "The key has to win over the identifier stored next to it.");
        }

        [Test]
        public void MissingKey_FallsBackToTheStoredIdentifier()
        {
            CreatureGenome source = SampleGenome();

            // Saved without a catalog to name the parts — bare identifiers, and they still load.
            string json = GenomeDocument.From(source, "No catalog").ToJson();

            Assert.IsTrue(GenomeDocument.TryFromJson(json, out GenomeDocument document, out string error), error);
            Assert.AreEqual(source.GetPart(0).PartId, document.ToGenome().GetPart(0).PartId);
        }

        [Test]
        public void RoundTrip_PreservesThePaintwork()
        {
            CreatureGenome source = SampleGenome();
            source.BodyPattern = 4;
            source.SetPart(0, source.GetPart(0).WithTint(new Color32(180, 60, 40, 255)).WithPattern(2));

            string json = GenomeDocument.From(source, "Painted", KeyOf).ToJson();

            Assert.IsTrue(GenomeDocument.TryFromJson(json, out GenomeDocument document, out string error), error);
            CreatureGenome loaded = document.ToGenome();

            Assert.AreEqual(4, loaded.BodyPattern);

            PartGene painted = loaded.GetPart(0);
            Assert.IsTrue(painted.HasTint);
            Assert.AreEqual(180, painted.Tint.r);
            Assert.AreEqual(60, painted.Tint.g);
            Assert.AreEqual(40, painted.Tint.b);
            Assert.AreEqual(2, painted.PatternId);

            Assert.IsFalse(loaded.GetPart(1).HasTint, "A part nobody painted must not come back painted.");
        }

        [Test]
        public void PresetWrittenBeforePainting_LoadsUnpaintedRatherThanBlack()
        {
            // A file from before the paintwork existed: the fields are simply absent, and JsonUtility
            // fills them with zeros — which for a colour means opaque black unless "no alpha" is read
            // as "never painted".
            string json = GenomeDocument.From(SampleGenome(), "Old preset", KeyOf).ToJson()
                .Replace("\"_tint\": {\"r\": 0, \"g\": 0, \"b\": 0, \"a\": 0},", string.Empty);

            Assert.IsTrue(GenomeDocument.TryFromJson(json, out GenomeDocument document, out string error), error);

            CreatureGenome loaded = document.ToGenome();
            Assert.IsFalse(loaded.GetPart(0).HasTint);
            Assert.AreEqual(0, loaded.BodyPattern);
        }

        [Test]
        public void MalformedJson_IsReportedRatherThanThrown()
        {
            Assert.IsFalse(GenomeDocument.TryFromJson("{ this is not json", out GenomeDocument document, out string error));
            Assert.IsNull(document);
            Assert.IsNotEmpty(error);
        }

        [Test]
        public void EmptyFile_IsReportedRatherThanThrown()
        {
            Assert.IsFalse(GenomeDocument.TryFromJson("   ", out GenomeDocument document, out string error));
            Assert.IsNull(document);
            Assert.IsNotEmpty(error);
        }

        [Test]
        public void PresetFromANewerBuild_IsRefusedInsteadOfLoadedHalfway()
        {
            string json = GenomeDocument.From(SampleGenome(), "From the future", KeyOf).ToJson()
                .Replace($"\"_version\": {GenomeDocument.CurrentVersion}", $"\"_version\": {GenomeDocument.CurrentVersion + 1}");

            Assert.IsFalse(GenomeDocument.TryFromJson(json, out GenomeDocument document, out string error),
                "A format we do not know how to read must not be loaded as a creature missing half of itself.");
            Assert.IsNull(document);
            Assert.IsNotEmpty(error);
        }
    }
}
