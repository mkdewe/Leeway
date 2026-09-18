using Leeway.Creature.Domain;
using NUnit.Framework;
using UnityEngine;

namespace Leeway.Tests
{
    /// <summary>
    /// Seating parts on the skin. The two defects this set guards against were visible in the game:
    /// parts sank into the body because nobody measured the vertebra radius, and a mouth on the front
    /// edge looked up instead of forward.
    /// </summary>
    public class PartPlacementTests
    {
        private const float Tolerance = 1e-3f;

        private static CreatureGenome Spine(float radius = 0.3f) => GenomeTestFixtures.Spine(3, radius);

        // --- Skin thickness ---

        [Test]
        public void Part_SitsOnTheSkin_NotInsideTheBody()
        {
            const float radius = 0.4f;
            CreatureGenome genome = Spine(radius);

            PartPose pose = PartPlacement.Resolve(genome, 1, new Vector3(0.05f, 0f, 0f), skinOffset: 0f);

            Assert.AreEqual(radius, pose.Position.magnitude, Tolerance,
                "The part should sit on the body surface, not at the point from the gene.");
            Assert.AreEqual(radius, pose.SurfaceRadius, Tolerance);
        }

        [Test]
        public void ThickerVertebra_PushesThePartFurtherOut()
        {
            PartPose thin = PartPlacement.Resolve(Spine(0.15f), 1, new Vector3(0.05f, 0f, 0f), 0f);
            PartPose fat = PartPlacement.Resolve(Spine(0.6f), 1, new Vector3(0.05f, 0f, 0f), 0f);

            Assert.Less(thin.Position.magnitude, fat.Position.magnitude,
                "A thicker vertebra has to push the part further out — otherwise it sinks into the flesh.");
        }

        [Test]
        public void SkinOffset_LiftsThePartAboveTheSurface()
        {
            CreatureGenome genome = Spine(0.3f);
            var position = new Vector3(0.05f, 0f, 0f);

            PartPose flush = PartPlacement.Resolve(genome, 1, position, skinOffset: 0f);
            PartPose lifted = PartPlacement.Resolve(genome, 1, position, skinOffset: 0.1f);

            Assert.AreEqual(flush.Position.magnitude + 0.1f, lifted.Position.magnitude, Tolerance);
        }

        [Test]
        public void PartFarFromTheAxis_IsPulledBackOntoTheSurface()
        {
            CreatureGenome genome = Spine(0.3f);

            // A player can drag the gene far outside the body — placement has to pull it back to the skin.
            PartPose pose = PartPlacement.Resolve(genome, 1, new Vector3(5f, 0f, 0f), 0f);

            Assert.AreEqual(0.3f, pose.Position.magnitude, Tolerance);
        }

        [Test]
        public void RadiusIsInterpolatedAlongTheSegment()
        {
            var genome = new CreatureGenome();
            genome.AddVertebra(new VertebraGene(Vector3.zero, Quaternion.identity, 0.2f));
            genome.AddVertebra(new VertebraGene(new Vector3(0f, 0f, -1f), Quaternion.identity, 0.6f));

            // Halfway along the segment between vertebrae of 0.2 and 0.6 the radius should be 0.4.
            Assert.AreEqual(0.4f, PartPlacement.BodyRadiusAt(genome, 0, 0.5f), Tolerance,
                "The radius has to follow the same interpolation the mesh generator uses.");
        }

        /// <summary>
        /// The reported defect: parts sank into the body. The body is a <b>tube</b>, so the skin lies
        /// one radius away from the spine axis — a point one radius from the centre of a vertebra in a
        /// diagonal direction sits under the skin.
        /// </summary>
        [Test]
        public void PartAttachedDiagonally_SitsOnTheTube_NotInsideIt()
        {
            const float radius = 0.3f;
            CreatureGenome genome = Spine(radius);

            // Diagonally: sideways and along the body, towards the tail.
            PartPose pose = PartPlacement.Resolve(genome, 1, new Vector3(0.3f, 0f, -0.3f), skinOffset: 0f);

            float fromAxis = Vector3.ProjectOnPlane(pose.Position, Vector3.forward).magnitude;

            Assert.AreEqual(radius, fromAxis, Tolerance,
                "The part should sit on the surface of the tube, not on a sphere around the vertebra centre.");
            Assert.Greater(Vector3.Dot(pose.Position, Vector3.back), Tolerance,
                "The shift along the body was lost — the part merely rotated about the vertebra.");
        }

        [Test]
        public void PartOnTheHeadTip_SitsOnTheCap()
        {
            const float radius = 0.3f;
            CreatureGenome genome = Spine(radius);

            // The cap is a hemisphere of the end vertebra's radius — that is how the mesh generator closes the body.
            PartPose pose = PartPlacement.Resolve(genome, 0, new Vector3(0f, 0.2f, 0.2f), 0f);

            Assert.AreEqual(radius, pose.Position.magnitude, Tolerance,
                "On the cap the skin sits exactly one radius from the end vertebra.");
        }

        [Test]
        public void DraggingAlongTheBody_StopsHalfwayToTheNeighbour()
        {
            CreatureGenome genome = Spine(0.3f);
            float half = GenomeLimits.DefaultSegmentOffset.magnitude * 0.5f;

            // The gene dragged far towards the tail — beyond that the skin belongs to the neighbour.
            PartPose pose = PartPlacement.Resolve(genome, 1, new Vector3(0.05f, 0f, -5f), 0f);

            Assert.AreEqual(half, Vector3.Dot(pose.Position, Vector3.back), Tolerance,
                "The part should stop halfway to the neighbouring vertebra.");
            Assert.AreEqual(0.3f, Vector3.ProjectOnPlane(pose.Position, Vector3.forward).magnitude, Tolerance,
                "Along the way the part has to stay on the skin.");
        }

        // --- Facing direction ---

        [Test]
        public void MouthOnTheFrontEdge_FacesForward()
        {
            CreatureGenome genome = Spine();

            // The spine runs along -Z, so the front of the head is +Z. A mouth placed at the front has
            // to look ahead — this was a specific reported defect.
            PartPose pose = PartPlacement.Resolve(genome, 0, new Vector3(0f, 0f, 0.3f), 0f);

            Assert.AreEqual(1f, Vector3.Dot(pose.Outward, Vector3.forward), Tolerance,
                "A mouth on the front edge looked up instead of forward.");
        }

        [Test]
        public void PartOnTheHeadCap_BlendsForwardAndSideways()
        {
            CreatureGenome genome = Spine();

            // On the cap the normal should behave as it does on a sphere: diagonally, not in a jump.
            PartPose pose = PartPlacement.Resolve(genome, 0, new Vector3(0.3f, 0f, 0.3f), 0f);

            Assert.Greater(pose.Outward.x, 0.1f, "The sideways component was lost.");
            Assert.Greater(pose.Outward.z, 0.1f, "The forward component was lost.");
        }

        [Test]
        public void PartOnTheSide_FacesSideways()
        {
            PartPose pose = PartPlacement.Resolve(Spine(), 1, new Vector3(0.3f, 0f, 0f), 0f);

            Assert.AreEqual(1f, Vector3.Dot(pose.Outward, Vector3.right), Tolerance);
        }

        [Test]
        public void PartUnderneath_FacesDown()
        {
            PartPose pose = PartPlacement.Resolve(Spine(), 1, new Vector3(0f, -0.3f, 0f), 0f);

            Assert.AreEqual(1f, Vector3.Dot(pose.Outward, Vector3.down), Tolerance);
        }

        /// <summary>
        /// The heart of the fix: the rotation has to genuinely follow the drag. Previously a fixed
        /// socket dominated over the position from the gene and the part barely changed direction.
        /// </summary>
        [Test]
        public void DraggingAroundTheBody_ActuallyTurnsThePart()
        {
            CreatureGenome genome = Spine();

            Vector3 right = PartPlacement.Resolve(genome, 1, new Vector3(0.3f, 0f, 0f), 0f).Outward;
            Vector3 up = PartPlacement.Resolve(genome, 1, new Vector3(0f, 0.3f, 0f), 0f).Outward;
            Vector3 left = PartPlacement.Resolve(genome, 1, new Vector3(-0.3f, 0f, 0f), 0f).Outward;

            Assert.AreEqual(90f, Vector3.Angle(right, up), 1f, "The rotation did not keep up with the move to the top.");
            Assert.AreEqual(180f, Vector3.Angle(right, left), 1f, "The rotation did not keep up with the move to the other side.");
        }

        [Test]
        public void Rotation_LooksAlongTheOutwardNormal()
        {
            CreatureGenome genome = Spine();
            PartPose pose = PartPlacement.Resolve(genome, 1, new Vector3(0.2f, 0.2f, 0f), 0f);

            Assert.AreEqual(1f, Vector3.Dot(pose.Rotation * Vector3.forward, pose.Outward), Tolerance,
                "The front of the part should coincide with the surface normal.");
        }

        [Test]
        public void FinalRotation_KeepsThePlayersCorrectionOnTop()
        {
            CreatureGenome genome = Spine();
            var position = new Vector3(0.3f, 0f, 0f);

            Quaternion correction = Quaternion.AngleAxis(35f, Vector3.forward);
            var gene = new PartGene(GenomeTestFixtures.EyeId, 1, position, correction, 1f, false);

            PartPose pose = PartPlacement.Resolve(genome, gene, 0f);

            Assert.AreEqual(0f, Quaternion.Angle(pose.Rotation * correction, PartPlacement.FinalRotation(pose, gene)),
                Tolerance);
        }

        // --- The attachment stays with its own bone ---

        /// <summary>
        /// The reported defect: moving a vertebra made a part "rotate about some other pivot". The
        /// cause — the attachment was decomposed relative to the spine tangent, and that tangent is
        /// averaged over both neighbours.
        /// </summary>
        [Test]
        public void MovingANeighbouringVertebra_DoesNotDisturbThePart()
        {
            CreatureGenome genome = GenomeTestFixtures.Spine(4);
            var attachment = new Vector3(0.3f, 0f, 0f);

            PartPose before = PartPlacement.Resolve(genome, 1, attachment, 0f);

            // We move vertebra 2 — the part sits on vertebra 1 and has no business moving.
            GenomeEditOperations.TrySetVertebraOffset(genome, 2, new Vector3(0.3f, 0.2f, -0.5f), out _);

            PartPose after = PartPlacement.Resolve(genome, 1, attachment, 0f);

            Assert.AreEqual(0f, Vector3.Distance(before.Position, after.Position), Tolerance,
"Moving the neighbour shifted the part.");
            Assert.AreEqual(0f, Vector3.Angle(before.Outward, after.Outward), 0.01f,
"Moving the neighbour rotated the part.");
        }

        [Test]
        public void RotatingTheOwnVertebra_DoesNotChangeTheLocalAttachment()
        {
            CreatureGenome genome = GenomeTestFixtures.Spine(4);
            var attachment = new Vector3(0.3f, 0f, 0f);

            PartPose before = PartPlacement.Resolve(genome, 1, attachment, 0f);

            // Rotating its own bone should carry the part along, so in that bone's space nothing
            // changes — the part is rigidly bound to it.
            GenomeEditOperations.TrySetVertebraRotation(genome, 1, Quaternion.Euler(0f, 40f, 15f), out _);

            PartPose after = PartPlacement.Resolve(genome, 1, attachment, 0f);

            Assert.AreEqual(0f, Vector3.Distance(before.Position, after.Position), Tolerance);
        }

        [Test]
        public void ThickeningTheOwnVertebra_PushesThePartOutAlongTheSameDirection()
        {
            CreatureGenome genome = GenomeTestFixtures.Spine(4, radius: 0.3f);
            var attachment = new Vector3(0.3f, 0.1f, 0f);

            PartPose thin = PartPlacement.Resolve(genome, 1, attachment, 0f);

            GenomeEditOperations.TrySetVertebraRadius(genome, 1, 0.55f, out _);
            PartPose fat = PartPlacement.Resolve(genome, 1, attachment, 0f);

            Assert.AreEqual(0.55f, fat.Position.magnitude, Tolerance, "The part should stay on the skin after thickening.");
            Assert.AreEqual(0f, Vector3.Angle(thin.Outward, fat.Outward), 0.01f,
                "Thickening should push the part out along the same direction, not rotate it.");
        }

        [Test]
        public void ThickeningANeighbour_LeavesThePartAlone()
        {
            CreatureGenome genome = GenomeTestFixtures.Spine(4, radius: 0.3f);
            var attachment = new Vector3(0.3f, 0f, 0f);

            PartPose before = PartPlacement.Resolve(genome, 1, attachment, 0f);
            GenomeEditOperations.TrySetVertebraRadius(genome, 2, 0.75f, out _);
            PartPose after = PartPlacement.Resolve(genome, 1, attachment, 0f);

            Assert.AreEqual(before.Position, after.Position, "The neighbour's thickness is none of this part's business.");
        }

        // --- Legs aim at the ground ---

        /// <summary>
        /// A leg attached to the side of the torso and turned along the body normal would stick out
        /// horizontally. The foot has to reach the ground wherever the leg is attached.
        /// </summary>
        [Test]
        public void GroundAlignedPart_FacesDown_WhereverItIsAttached()
        {
            CreatureGenome genome = Spine();

            foreach (var attachment in new[] { new Vector3(0.3f, 0f, 0f), new Vector3(0f, 0.3f, 0f), new Vector3(-0.3f, 0.1f, 0f) })
            {
                PartPose pose = PartPlacement.Resolve(genome, 1, attachment, 0f, groundAligned: true);
                Vector3 facing = pose.Rotation * Vector3.forward;

                Assert.AreEqual(1f, Vector3.Dot(facing, Vector3.down), Tolerance,
                    $"A leg attached at {attachment} does not aim at the ground.");
            }
        }

        [Test]
        public void GroundAlignedPart_KeepsItsPlaceOnTheSkin()
        {
            CreatureGenome genome = Spine(0.3f);
            var attachment = new Vector3(0.3f, 0f, 0f);

            PartPose outward = PartPlacement.Resolve(genome, 1, attachment, 0f);
            PartPose grounded = PartPlacement.Resolve(genome, 1, attachment, 0f, groundAligned: true);

            Assert.AreEqual(outward.Position, grounded.Position,
                "Changing the facing must not move the attachment point.");
        }

        [Test]
        public void NonGroundAlignedPart_StillFacesAwayFromTheBody()
        {
            PartPose pose = PartPlacement.Resolve(Spine(), 1, new Vector3(0.3f, 0f, 0f), 0f, groundAligned: false);

            Assert.AreEqual(1f, Vector3.Dot(pose.Rotation * Vector3.forward, Vector3.right), Tolerance,
                "Eyes and mouths should still look away from the body.");
        }

        [Test]
        public void Resolve_IsDeterministic()
        {
            CreatureGenome genome = Spine();
            var position = new Vector3(0.2f, 0.3f, -0.1f);

            PartPose first = PartPlacement.Resolve(genome, 1, position, 0.02f);
            PartPose second = PartPlacement.Resolve(genome, 1, position, 0.02f);

            Assert.AreEqual(first.Position, second.Position);
            Assert.AreEqual(0f, Quaternion.Angle(first.Rotation, second.Rotation), Tolerance);
        }
    }
}
