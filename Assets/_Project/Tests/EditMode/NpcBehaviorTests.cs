using Leeway.Creature.Domain;
using NUnit.Framework;
using UnityEngine;

namespace Leeway.Tests
{
    public class NpcBehaviorTests
    {
        private static NpcPerception Perception(bool hasThreat, Vector2 threat, bool hasFood, Vector2 food, Vector2 wander)
            => new NpcPerception(
                selfPosition: Vector2.zero,
                fleeDistance: 5f,
                hasThreat: hasThreat,
                threatPosition: threat,
                hasFood: hasFood,
                foodPosition: food,
                wanderTarget: wander);

        [Test]
        public void Factory_MapsTypeToBehavior()
        {
            Assert.IsInstanceOf<PredatorBehavior>(NpcBehaviorFactory.Create(NpcType.Predator));
            Assert.IsInstanceOf<FoodBehavior>(NpcBehaviorFactory.Create(NpcType.Food));
        }

        [Test]
        public void Food_WhenThreatNear_FleesDirectlyAway()
        {
            var p = Perception(hasThreat: true, threat: new Vector2(1f, 0f), hasFood: false, food: Vector2.zero, wander: new Vector2(9f, 9f));
            Vector2 target = new FoodBehavior().DecideTarget(in p);
            // self(0,0), threat(1,0) -> ucieczka w (-1,0) * fleeDistance 5 = (-5,0)
            Assert.AreEqual(new Vector2(-5f, 0f), target);
        }

        [Test]
        public void Food_WhenNoThreat_Wanders_EvenIfFoodPresent()
        {
            var wander = new Vector2(3f, 2f);
            var p = Perception(hasThreat: false, threat: Vector2.zero, hasFood: true, food: new Vector2(8f, 8f), wander: wander);
            // ofiara nigdy nie poluje
            Assert.AreEqual(wander, new FoodBehavior().DecideTarget(in p));
        }

        [Test]
        public void Predator_WhenFoodPresentAndNoThreat_ChasesFood()
        {
            var food = new Vector2(3f, 4f);
            var p = Perception(hasThreat: false, threat: Vector2.zero, hasFood: true, food: food, wander: new Vector2(9f, 9f));
            Assert.AreEqual(food, new PredatorBehavior().DecideTarget(in p));
        }

        [Test]
        public void Predator_WhenThreatPresent_FleesInsteadOfChasing()
        {
            var p = Perception(hasThreat: true, threat: new Vector2(0f, 2f), hasFood: true, food: new Vector2(3f, 0f), wander: Vector2.zero);
            Vector2 target = new PredatorBehavior().DecideTarget(in p);
            // ucieczka ma priorytet: self(0,0), threat(0,2) -> (0,-1)*5 = (0,-5)
            Assert.AreEqual(new Vector2(0f, -5f), target);
        }

        [Test]
        public void Predator_WhenNothingAround_Wanders()
        {
            var wander = new Vector2(-2f, 7f);
            var p = Perception(hasThreat: false, threat: Vector2.zero, hasFood: false, food: Vector2.zero, wander: wander);
            Assert.AreEqual(wander, new PredatorBehavior().DecideTarget(in p));
        }
    }
}
