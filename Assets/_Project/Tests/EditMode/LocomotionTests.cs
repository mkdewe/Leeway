using Leeway.Creature.Domain;
using NUnit.Framework;
using UnityEngine;

namespace Leeway.Tests
{
    /// <summary>
    /// The maths of walking: leg build, step cycle and the IK solver. The whole layer is pure, so the
    /// gait can be checked without a scene, a creature or a network.
    /// </summary>
    public class LocomotionTests
    {
        private const float Tolerance = 1e-3f;

        // --- LegSpec ---

        [Test]
        public void EachBendPoint_AddsOneSegment()
        {
            Assert.AreEqual(1, new LegSpec(0, 0.3f).SegmentCount);
            Assert.AreEqual(3, new LegSpec(2, 0.3f).SegmentCount);
            Assert.AreEqual(4, new LegSpec(3, 0.3f).SegmentCount, "Three bend points make four links.");
        }

        [Test]
        public void MoreBends_ReachFurther()
        {
            Assert.Less(new LegSpec(1, 0.3f).Reach, new LegSpec(3, 0.3f).Reach,
                "A longer chain has to reach further — the stride length depends on it.");
        }

        [Test]
        public void BendCount_PicksTheGaitStyle()
        {
            Assert.AreEqual(GaitStyle.Stiff, new LegSpec(1, 0.3f).Style);
            Assert.AreEqual(GaitStyle.Walk, new LegSpec(2, 0.3f).Style);
            Assert.AreEqual(GaitStyle.Insectoid, new LegSpec(3, 0.3f).Style);
            Assert.AreEqual(GaitStyle.Insectoid, new LegSpec(4, 0.3f).Style);
        }

        [Test]
        public void BendCount_IsClampedToTheAllowedRange()
        {
            Assert.AreEqual(LegLimits.MaxBendPoints, new LegSpec(99, 0.3f).BendPoints);
            Assert.AreEqual(LegLimits.MinBendPoints, new LegSpec(-5, 0.3f).BendPoints);
        }

        // --- The character of the gait ---

        /// <summary>
        /// The heart of the requirement: the bend count has to genuinely change the gait, not just the
        /// look of the leg. A stiff leg has to lift its foot high, a many-linked one glides low.
        /// </summary>
        [Test]
        public void StifferLeg_LiftsItsFootHigherRelativeToReach()
        {
            GaitProfile stiff = new LegSpec(1, 0.3f).Gait;
            GaitProfile insect = new LegSpec(4, 0.3f).Gait;

            Assert.Greater(stiff.StepHeight / new LegSpec(1, 0.3f).Reach,
                           insect.StepHeight / new LegSpec(4, 0.3f).Reach,
                           "An almost stiff leg has to lift its foot higher relative to its own reach.");
        }

        [Test]
        public void MoreBends_KeepTheFootOnTheGroundLonger()
        {
            Assert.Less(new LegSpec(1, 0.3f).Gait.DutyFactor, new LegSpec(4, 0.3f).Gait.DutyFactor,
                "An insect gait has a longer stance phase — hence its smoothness.");
        }

        // --- The step cycle ---

        [Test]
        public void FootStaysOnTheGroundThroughoutStance()
        {
            GaitProfile gait = new LegSpec(2, 0.3f).Gait;

            for (float phase = 0f; phase < gait.DutyFactor; phase += 0.05f)
                Assert.AreEqual(0f, gait.FootOffset(phase).y, Tolerance, $"The foot left the ground during stance at phase {phase}.");
        }

        [Test]
        public void FootTravelsBackwardsDuringStance()
        {
            GaitProfile gait = new LegSpec(2, 0.3f).Gait;

            // During stance the foot stands still, so relative to the body it travels backwards.
            Assert.Greater(gait.FootOffset(0f).x, gait.FootOffset(gait.DutyFactor * 0.9f).x);
        }

        [Test]
        public void FootLiftsAndLandsFlushDuringSwing()
        {
            GaitProfile gait = new LegSpec(2, 0.3f).Gait;
            float mid = gait.DutyFactor + (1f - gait.DutyFactor) * 0.5f;

            Assert.AreEqual(gait.StepHeight, gait.FootOffset(mid).y, Tolerance, "The top of the arc has to reach the full step height.");
            Assert.AreEqual(0f, gait.FootOffset(0.999f).y, 0.01f, "The foot has to land flush, not drive into the ground.");
        }

        [Test]
        public void StepCycleIsContinuousAcrossTheWrap()
        {
            GaitProfile gait = new LegSpec(2, 0.3f).Gait;

            Assert.AreEqual(gait.FootOffset(0f).x, gait.FootOffset(1f).x, Tolerance);
            Assert.AreEqual(gait.FootOffset(0.25f).x, gait.FootOffset(1.25f).x, Tolerance, "The phase has to wrap around.");
        }

        [Test]
        public void IsPlanted_MatchesTheDutyFactor()
        {
            GaitProfile gait = new LegSpec(2, 0.3f).Gait;

            Assert.IsTrue(gait.IsPlanted(0f));
            Assert.IsFalse(gait.IsPlanted(gait.DutyFactor + 0.01f));
        }

        [Test]
        public void LeftAndRightLegs_StepInOppositePhase()
        {
            Assert.AreEqual(0.5f, Mathf.Abs(GaitProfile.PhaseOffset(0, 2) - GaitProfile.PhaseOffset(1, 2)), Tolerance,
                "A pair of legs has to step in antiphase, otherwise the creature hops on both feet.");
        }

        // --- Turning towards the held direction ---

        [Test]
        public void Input_IsReadInCameraSpace()
        {
            // "Up the screen" means "away from the camera", so rotating the camera rotates the input direction.
            Vector3 straight = LocomotionSteering.DesiredDirection(1f, 0f, cameraYaw: 0f);
            Vector3 turned = LocomotionSteering.DesiredDirection(1f, 0f, cameraYaw: 90f);

            Assert.AreEqual(1f, Vector3.Dot(straight, Vector3.forward), Tolerance);
            Assert.AreEqual(1f, Vector3.Dot(turned, Vector3.right), Tolerance);
        }

        [Test]
        public void DiagonalInput_IsNotFasterThanStraight()
        {
            Vector3 diagonal = LocomotionSteering.DesiredDirection(1f, 1f, 0f);

            Assert.AreEqual(1f, diagonal.magnitude, Tolerance, "A diagonal must not be faster than walking straight.");
        }

        [Test]
        public void Creature_TurnsTowardsTheInput_WithoutSnapping()
        {
            // Input to the right (90 degrees), the creature looks forward (0).
            Vector3 desired = LocomotionSteering.DesiredDirection(0f, 1f, 0f);

            float afterOneStep = LocomotionSteering.StepYaw(0f, desired, turnSpeedDegrees: 150f, deltaTime: 0.1f);

            Assert.AreEqual(15f, afterOneStep, 0.1f, "The turn has to run at the turn rate, not snap.");
            Assert.Less(afterOneStep, 90f, "The creature must not teleport onto a new heading.");
        }

        [Test]
        public void Turning_EventuallyReachesTheInputDirection()
        {
            Vector3 desired = LocomotionSteering.DesiredDirection(0f, 1f, 0f);

            float yaw = 0f;
            for (int i = 0; i < 100; i++) yaw = LocomotionSteering.StepYaw(yaw, desired, 150f, 0.05f);

            Assert.AreEqual(90f, Mathf.DeltaAngle(0f, yaw) , 0.5f);
        }

        [Test]
        public void EmptyInput_LeavesTheHeadingAlone()
        {
            Assert.AreEqual(42f, LocomotionSteering.StepYaw(42f, Vector3.zero, 150f, 0.1f), Tolerance,
                "With no input the creature must not straighten itself out.");
        }

        /// <summary>
        /// The heart of "turning on the spot": while reversing, the front is still turned away, so the
        /// throttle sits at zero and the creature turns first.
        /// </summary>
        [Test]
        public void Throttle_IsZeroWhenFacingAwayFromTheInput()
        {
            Vector3 back = LocomotionSteering.DesiredDirection(-1f, 0f, 0f);

            Assert.AreEqual(0f, LocomotionSteering.Throttle(currentYaw: 0f, back), Tolerance);
        }

        [Test]
        public void Throttle_IsFullWhenFacingTheInput()
        {
            Vector3 ahead = LocomotionSteering.DesiredDirection(1f, 0f, 0f);

            Assert.AreEqual(1f, LocomotionSteering.Throttle(0f, ahead), Tolerance);
        }

        [Test]
        public void Throttle_RisesAsTheCreatureTurnsIntoTheInput()
        {
            Vector3 side = LocomotionSteering.DesiredDirection(0f, 1f, 0f);

            float facingAway = LocomotionSteering.Throttle(0f, side);
            float halfway = LocomotionSteering.Throttle(45f, side);
            float facingIt = LocomotionSteering.Throttle(90f, side);

            Assert.Less(facingAway, halfway);
            Assert.Less(halfway, facingIt);
        }

        // --- Stance ---

        /// <summary>
        /// Stance height is measured from the <b>hip</b>, not from the root. A regression seen in the
        /// game: counting the whole body radius stood the creature too high, the leg locked out to full
        /// extension and the foot barely grazed the ground.
        /// </summary>
        [Test]
        public void StandHeight_LeavesTheLegBent_NotFullyExtended()
        {
            CreatureGenome genome = GenomeTestFixtures.Spine();
            PartRuleSet rules = GenomeTestFixtures.Rules();
            genome.AddPart(new PartGene(GenomeTestFixtures.LegsId, 1, new Vector3(0.3f, -1f, 0f),
                Quaternion.identity, 1f, mirrored: true));

            float stand = GenomeStatRules.StandHeight(genome, rules);
            Assert.Greater(stand, 0f, "A creature with legs has to have a positive stance height.");

            // Where the hip lands at this stance and how much the leg has left to the ground.
            rules.TryGetRule(GenomeTestFixtures.LegsId, out PartRule rule);
            Matrix4x4[] boneToRoot = SpineAnchor.BoneToRoot(genome);
            PartPose pose = PartPlacement.Resolve(genome, genome.GetPart(0), rule.SkinOffset);
            float hipHeight = stand + boneToRoot[1].MultiplyPoint3x4(pose.Position).y;

            float extension = hipHeight / rule.Leg.Reach;

            Assert.Less(extension, 0.95f, $"The leg is overextended ({extension:P0} of its reach) — it will run out of bend.");
            Assert.Greater(extension, 0.5f, $"The leg is tucked up too far ({extension:P0} of its reach).");
        }

        [Test]
        public void StandHeight_IsZeroWithoutLegs()
        {
            CreatureGenome genome = GenomeTestFixtures.Spine();
            genome.AddPart(PartGene.Default(GenomeTestFixtures.JawId, 0, mirrored: false));

            Assert.AreEqual(0f, GenomeStatRules.StandHeight(genome, GenomeTestFixtures.Rules()), Tolerance,
                "A creature without legs lies belly to the ground — there is nothing to straighten.");
        }

        // --- Suspension ---

        [Test]
        public void Suspension_PushesUpWhenCompressed()
        {
            Suspension spring = Suspension.Default;

            // The body lower than the rest height => the spring has to lift it.
            Assert.Greater(spring.Acceleration(rideHeight: 0.5f, groundDistance: 0.3f, verticalVelocity: 0f), 0f);
        }

        [Test]
        public void Suspension_NeverPullsTheBodyDown()
        {
            Suspension spring = Suspension.Default;

            // Pulling down on the rebound would glue the creature to the ground and cancel the jump.
            Assert.AreEqual(0f, spring.Acceleration(rideHeight: 0.5f, groundDistance: 0.9f, verticalVelocity: 0f), Tolerance);
            Assert.AreEqual(0f, spring.Acceleration(rideHeight: 0.5f, groundDistance: 0.3f, verticalVelocity: 20f), Tolerance);
        }

        [Test]
        public void Suspension_DeeperCompression_PushesHarder()
        {
            Suspension spring = Suspension.Default;

            float shallow = spring.Acceleration(0.5f, 0.4f, 0f);
            float deep = spring.Acceleration(0.5f, 0.1f, 0f);

            Assert.Greater(deep, shallow, "A deeper compression has to push the legs out harder.");
        }

        [Test]
        public void Suspension_DampingOpposesUpwardMotion()
        {
            Suspension spring = Suspension.Default;

            float still = spring.Acceleration(0.5f, 0.3f, 0f);
            float rising = spring.Acceleration(0.5f, 0.3f, 1f);

            Assert.Less(rising, still, "Without damping the creature would bob like it was on leaf springs.");
        }

        [Test]
        public void Suspension_InTheAir_LeavesTheBodyToGravity()
        {
            Suspension spring = Suspension.Default;
            float farBelow = 0.5f + spring.MaxDroop + 0.5f;

            Assert.IsFalse(spring.IsGrounded(0.5f, farBelow));
            Assert.AreEqual(0f, spring.Acceleration(0.5f, farBelow, 0f), Tolerance);
        }

        /// <summary>
        /// Legs hold the body up actively, so at the rest height they give back exactly what gravity
        /// takes away. Without that the creature would settle onto the spring and stand lower the
        /// heavier it is.
        /// </summary>
        [Test]
        public void Suspension_AtRestHeight_ExactlyCancelsGravity()
        {
            Suspension spring = Suspension.Default;
            const float gravity = 9.81f;

            float acceleration = spring.Acceleration(rideHeight: 0.5f, groundDistance: 0.5f,
                verticalVelocity: 0f, gravity: gravity);

            Assert.AreEqual(gravity, acceleration, Tolerance, "At the rest height the creature should stand, not settle.");
        }

        [Test]
        public void Suspension_CriticalDampingIsTwiceRootStiffness()
        {
            var spring = new Suspension(100f, dampingRatio: 1f, maxDroop: 0.4f);

            Assert.AreEqual(20f, spring.Damping, Tolerance,
                "Critical damping is 2*sqrt(stiffness) — the parameter choice rests on that.");
        }

        // --- IK ---

        private static Vector3[] Chain(int joints, float segment)
        {
            var points = new Vector3[joints];
            for (int i = 0; i < joints; i++) points[i] = new Vector3(0f, -segment * i, 0f);
            return points;
        }

        [Test]
        public void Ik_PutsTheFootOnAReachableTarget()
        {
            var joints = Chain(4, 0.3f);
            var target = new Vector3(0.3f, -0.5f, 0.2f);

            LimbIk.Solve(joints, 0.3f, target, Vector3.forward);

            Assert.AreEqual(0f, Vector3.Distance(joints[^1], target), 0.01f, "The foot missed a reachable target.");
        }

        [Test]
        public void Ik_KeepsSegmentsAtTheirLength()
        {
            var joints = Chain(5, 0.25f);
            LimbIk.Solve(joints, 0.25f, new Vector3(0.4f, -0.6f, 0.1f), Vector3.forward);

            for (int i = 1; i < joints.Length; i++)
                Assert.AreEqual(0.25f, Vector3.Distance(joints[i - 1], joints[i]), 0.01f,
                    $"Link {i} changed length — the leg is stretching.");
        }

        [Test]
        public void Ik_NeverMovesTheHip()
        {
            var joints = Chain(4, 0.3f);
            Vector3 hip = joints[0];

            LimbIk.Solve(joints, 0.3f, new Vector3(1f, -0.2f, 0f), Vector3.forward);

            Assert.AreEqual(hip, joints[0], "The hip has to stay where the body holds it.");
        }

        [Test]
        public void Ik_StraightensTowardsAnUnreachableTarget()
        {
            var joints = Chain(3, 0.3f);
            var target = new Vector3(0f, -50f, 0f);

            LimbIk.Solve(joints, 0.3f, target, Vector3.forward);

            Assert.AreEqual(0.6f, Vector3.Distance(joints[0], joints[^1]), 0.01f,
                "Beyond its reach the leg has to be straightened to full length.");
        }

        [Test]
        public void Ik_BendsTowardsTheHint()
        {
            var forward = Chain(4, 0.3f);
            var backward = Chain(4, 0.3f);
            var target = new Vector3(0f, -0.6f, 0f);

            LimbIk.Solve(forward, 0.3f, target, Vector3.forward);
            LimbIk.Solve(backward, 0.3f, target, Vector3.back);

            Assert.Greater(forward[1].z, 0.01f, "The knee did not go towards the hint.");
            Assert.Less(backward[1].z, -0.01f, "The knee cannot be bent the other way.");
        }

        [Test]
        public void Ik_IsDeterministic()
        {
            var a = Chain(4, 0.3f);
            var b = Chain(4, 0.3f);
            var target = new Vector3(0.2f, -0.5f, 0.3f);

            LimbIk.Solve(a, 0.3f, target, Vector3.forward);
            LimbIk.Solve(b, 0.3f, target, Vector3.forward);

            for (int i = 0; i < a.Length; i++)
                Assert.AreEqual(a[i], b[i], $"Joint {i} came out differently for the same inputs.");
        }
    }
}
