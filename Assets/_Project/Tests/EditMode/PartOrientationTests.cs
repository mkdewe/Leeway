using Leeway.Creature.Domain;
using NUnit.Framework;
using UnityEngine;

namespace Leeway.Tests
{
    /// <summary>
    /// Automatic part orientation: "front points away from the body". The rule is purely geometric, so
    /// it can be pinned down without a scene — and it has to come out identically on the server and on
    /// every client, because they build the body from the same genome.
    /// </summary>
    public class PartOrientationTests
    {
        private const float Tolerance = 1e-3f;

        /// <summary>The direction a part looks in once the computed rotation is applied.</summary>
        private static Vector3 Facing(CreatureGenome genome, int boneIndex, Vector3 localPosition)
            => PartOrientation.Resolve(genome, boneIndex, localPosition) * Vector3.forward;

        [Test]
        public void Tangent_RunsTailwardAlongTheSpine()
        {
            CreatureGenome genome = GenomeTestFixtures.Spine();

            // The spine runs backwards along -Z, so the tangent towards the tail is -Z.
            Assert.AreEqual(Vector3.back, PartOrientation.SpineTangent(genome, 1).normalized);
        }

        [Test]
        public void PartOnTheSide_FacesSideways()
        {
            CreatureGenome genome = GenomeTestFixtures.Spine();

            Vector3 facing = Facing(genome, 1, new Vector3(0.4f, 0f, 0f));

            Assert.AreEqual(1f, Vector3.Dot(facing, Vector3.right), Tolerance,
                "A part attached to the right side of the torso should look right.");
        }

        [Test]
        public void PartUnderneath_FacesDown()
        {
            CreatureGenome genome = GenomeTestFixtures.Spine();

            Vector3 facing = Facing(genome, 1, new Vector3(0f, -0.4f, 0f));

            Assert.AreEqual(1f, Vector3.Dot(facing, Vector3.down), Tolerance,
                "A leg underneath should aim down.");
        }

        [Test]
        public void MirroredPosition_FacesTheOppositeWay()
        {
            CreatureGenome genome = GenomeTestFixtures.Spine();

            Vector3 right = Facing(genome, 1, new Vector3(0.4f, 0f, 0f));
            Vector3 left = Facing(genome, 1, new Vector3(-0.4f, 0f, 0f));

            Assert.AreEqual(-right.x, left.x, Tolerance, "The same part on the other side should look the other way.");
        }

        [Test]
        public void PartOnHeadAxis_FacesForwardOutOfTheHead()
        {
            CreatureGenome genome = GenomeTestFixtures.Spine();

            // A beak on the crown of the head has no radial component — the fallback direction has to
            // send it outwards, which means in front of the head.
            Vector3 facing = Facing(genome, 0, Vector3.zero);

            Assert.AreEqual(1f, Vector3.Dot(facing, Vector3.forward), Tolerance,
                "A part on the head axis should look forward, not into the body.");
        }

        [Test]
        public void PartOnTailAxis_FacesBackOutOfTheTail()
        {
            CreatureGenome genome = GenomeTestFixtures.Spine();

            Vector3 facing = Facing(genome, genome.VertebraCount - 1, Vector3.zero);

            Assert.AreEqual(1f, Vector3.Dot(facing, Vector3.back), Tolerance,
                "A part on the tail axis should look backwards.");
        }

        [Test]
        public void PartOnTorsoAxis_FallsBackToUp()
        {
            CreatureGenome genome = GenomeTestFixtures.Spine(vertebraCount: 4);

            // The middle of the torso has no distinguished direction along the axis — up is the least
            // surprising one and, more importantly, deterministic.
            Vector3 facing = Facing(genome, 1, Vector3.zero);

            Assert.AreEqual(1f, Vector3.Dot(facing, Vector3.up), Tolerance);
        }

        [Test]
        public void Resolve_IsDeterministic()
        {
            CreatureGenome genome = GenomeTestFixtures.Spine();
            var position = new Vector3(0.2f, 0.3f, 0f);

            Quaternion first = PartOrientation.Resolve(genome, 1, position);
            Quaternion second = PartOrientation.Resolve(genome, 1, position);

            Assert.AreEqual(0f, Quaternion.Angle(first, second), Tolerance,
                "The same pair of inputs has to give the same rotation — otherwise players see different creatures.");
        }

        [Test]
        public void ResolveFinal_LayersTheGeneRotationOnTopOfAuto()
        {
            CreatureGenome genome = GenomeTestFixtures.Spine();

            var basePosition = new Vector3(0.4f, 0f, 0f);
            Quaternion auto = PartOrientation.Resolve(genome, 1, basePosition);

            Quaternion correction = Quaternion.AngleAxis(30f, Vector3.forward);
            var gene = new PartGene(GenomeTestFixtures.EyeId, 1, basePosition, correction, 1f, false);

            Assert.AreEqual(0f, Quaternion.Angle(auto * correction, PartOrientation.ResolveFinal(genome, gene)), Tolerance,
                "The gene rotation should be a correction layered on top of the automatic orientation.");
        }

        [Test]
        public void ResolveFinal_WithoutCorrection_EqualsAuto()
        {
            CreatureGenome genome = GenomeTestFixtures.Spine();
            var position = new Vector3(0f, 0.4f, 0f);

            var gene = new PartGene(GenomeTestFixtures.EyeId, 1, position, Quaternion.identity, 1f, false);

            Assert.AreEqual(0f,
                Quaternion.Angle(PartOrientation.Resolve(genome, 1, position), PartOrientation.ResolveFinal(genome, gene)),
                Tolerance);
        }

        [Test]
        public void Resolve_OutOfRangeBone_ReturnsIdentity()
        {
            CreatureGenome genome = GenomeTestFixtures.Spine();

            Assert.AreEqual(Quaternion.identity, PartOrientation.Resolve(genome, 42, Vector3.one));
            Assert.AreEqual(Quaternion.identity, PartOrientation.Resolve(null, 0, Vector3.one));
        }
    }
}
