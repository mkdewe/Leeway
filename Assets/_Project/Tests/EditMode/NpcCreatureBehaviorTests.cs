using Leeway.Creature.Domain;
using NUnit.Framework;
using UnityEngine;

namespace Leeway.Tests
{
    public class NpcCreatureBehaviorTests
    {
        private static CreaturePerception Perception(bool hasThreat, Vector3 threat, bool hasTarget, Vector3 target, Vector3 wander)
            => new CreaturePerception(
                selfPosition: Vector3.zero,
                fleeDistance: 5f,
                hasThreat: hasThreat,
                threatPosition: threat,
                hasTarget: hasTarget,
                targetPosition: target,
                wanderTarget: wander);

        [Test]
        public void Factory_MapsTypeToBehavior()
        {
            Assert.IsInstanceOf<PredatorCreatureBehavior>(NpcCreatureBehaviorFactory.Create(NpcCreatureType.Predator));
            Assert.IsInstanceOf<PreyBehavior>(NpcCreatureBehaviorFactory.Create(NpcCreatureType.Prey));
        }

        [Test]
        public void Prey_WhenThreatNear_FleesDirectlyAway()
        {
            var p = Perception(hasThreat: true, threat: new Vector3(1f, 0f, 0f), hasTarget: false, target: Vector3.zero, wander: new Vector3(9f, 0f, 9f));
            Vector3 target = new PreyBehavior().DecideTarget(in p);
            Assert.AreEqual(new Vector3(-5f, 0f, 0f), target);
        }

        [Test]
        public void Prey_WhenNoThreat_Wanders()
        {
            var wander = new Vector3(3f, 0f, 2f);
            var p = Perception(hasThreat: false, threat: Vector3.zero, hasTarget: false, target: Vector3.zero, wander: wander);
            Assert.AreEqual(wander, new PreyBehavior().DecideTarget(in p));
        }

        [Test]
        public void Predator_WhenTargetPresentAndNoThreat_ChasesTarget()
        {
            var target = new Vector3(3f, 0f, 4f);
            var p = Perception(hasThreat: false, threat: Vector3.zero, hasTarget: true, target: target, wander: new Vector3(9f, 0f, 9f));
            Assert.AreEqual(target, new PredatorCreatureBehavior().DecideTarget(in p));
        }

        [Test]
        public void Predator_WhenThreatPresent_FleesInsteadOfChasing()
        {
            var p = Perception(hasThreat: true, threat: new Vector3(0f, 0f, 2f), hasTarget: true, target: new Vector3(3f, 0f, 0f), wander: Vector3.zero);
            Vector3 target = new PredatorCreatureBehavior().DecideTarget(in p);
            Assert.AreEqual(new Vector3(0f, 0f, -5f), target);
        }

        [Test]
        public void Predator_WhenNothingAround_Wanders()
        {
            var wander = new Vector3(-2f, 0f, 7f);
            var p = Perception(hasThreat: false, threat: Vector3.zero, hasTarget: false, target: Vector3.zero, wander: wander);
            Assert.AreEqual(wander, new PredatorCreatureBehavior().DecideTarget(in p));
        }
    }
}
