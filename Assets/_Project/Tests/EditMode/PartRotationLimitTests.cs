using Leeway.Creature.Domain;
using NUnit.Framework;
using UnityEngine;

namespace Leeway.Tests
{
    /// <summary>
    /// How far a part may be turned. A limb that can be turned freely ends up pointing into the body
    /// it hangs from — see <see cref="PartRotationLimits"/>.
    /// </summary>
    public class PartRotationLimitTests
    {
        private static float SwingOf(Quaternion rotation)
            => Vector3.Angle(Vector3.forward, rotation * Vector3.forward);

        [Test]
        public void ASmallTurn_IsLeftAlone()
        {
            Quaternion wanted = Quaternion.AngleAxis(20f, Vector3.right);
            Quaternion clamped = PartRotationLimits.ClampSwing(wanted, PartRotationLimits.LimbSwingDegrees);

            Assert.AreEqual(0f, Quaternion.Angle(wanted, clamped), 0.01f);
        }

        [Test]
        public void ATurnPastTheLimit_IsHeldAtTheLimit()
        {
            Quaternion clamped = PartRotationLimits.ClampSwing(
                Quaternion.AngleAxis(120f, Vector3.right), PartRotationLimits.LimbSwingDegrees);

            Assert.AreEqual(PartRotationLimits.LimbSwingDegrees, SwingOf(clamped), 0.5f);
        }

        /// <summary>The direction has to survive the clamp — a leg splayed to the left stays on the left.</summary>
        [Test]
        public void TheDirectionOfTheTurn_IsKept()
        {
            Quaternion clamped = PartRotationLimits.ClampSwing(
                Quaternion.AngleAxis(150f, Vector3.up), PartRotationLimits.LimbSwingDegrees);

            Vector3 aimed = clamped * Vector3.forward;

            Assert.Greater(aimed.x, 0f, "A turn to one side must not come back on the other.");
            Assert.AreEqual(PartRotationLimits.LimbSwingDegrees, SwingOf(clamped), 0.5f);
        }

        /// <summary>
        /// Twist about the limb's own axis is how a knee is aimed, and it is not a swing — the limit
        /// must not touch it.
        /// </summary>
        [Test]
        public void TwistAboutTheLimbsOwnAxis_IsNotLimited()
        {
            Quaternion twist = Quaternion.AngleAxis(170f, Vector3.forward);
            Quaternion clamped = PartRotationLimits.ClampSwing(twist, PartRotationLimits.LimbSwingDegrees);

            Assert.AreEqual(0f, Quaternion.Angle(twist, clamped), 0.5f);
        }

        [Test]
        public void APartThatIsNotALimb_TurnsFreely()
        {
            Quaternion wanted = Quaternion.AngleAxis(170f, Vector3.right);

            Assert.AreEqual(180f, PartRotationLimits.SwingLimitFor(PartCategory.Weapon), 0.01f);
            Assert.AreEqual(0f, Quaternion.Angle(wanted,
                PartRotationLimits.ClampSwing(wanted, PartRotationLimits.SwingLimitFor(PartCategory.Weapon))), 0.01f);
        }

        /// <summary>
        /// The whole point, stated as the failure it prevents: a leg turned as far as the gizmo allows
        /// still hangs below its hip rather than folding up into the torso.
        /// </summary>
        [Test]
        public void ALegTurnedToTheLimit_StillPointsAwayFromTheBody()
        {
            foreach (Vector3 axis in new[] { Vector3.right, Vector3.up, -Vector3.right, -Vector3.up })
            {
                Quaternion clamped = PartRotationLimits.ClampSwing(
                    Quaternion.AngleAxis(179f, axis), PartRotationLimits.SwingLimitFor(PartCategory.Locomotion));

                // +Z is "away from the body" for every part; the limb must keep pointing that way.
                Assert.Greater((clamped * Vector3.forward).z, 0.5f, $"turned about {axis}");
            }
        }

        [Test]
        public void TheEditOperationAppliesTheLimit()
        {
            PartRuleSet rules = GenomeTestFixtures.Rules();
            CreatureGenome genome = GenomeTestFixtures.Spine();

            Assert.IsTrue(GenomeEditOperations.TryAttachPart(genome,
                new PartGene(GenomeTestFixtures.LegsId, 1, new Vector3(0.3f, -1f, 0f), Quaternion.identity, 1f, true),
                rules, out _));

            Assert.IsTrue(GenomeEditOperations.TrySetPartRotation(genome, 0,
                Quaternion.AngleAxis(140f, Vector3.right), rules, out _));

            Assert.AreEqual(PartRotationLimits.LimbSwingDegrees, SwingOf(genome.GetPart(0).LocalRotation), 0.5f);
        }
    }
}
