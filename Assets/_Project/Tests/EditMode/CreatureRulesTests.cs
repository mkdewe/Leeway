using Leeway.Creature.Domain;
using NUnit.Framework;

namespace Leeway.Tests
{
    public class CreatureRulesTests
    {
        [Test]
        public void CanAttack_WhenInRangeAndBothAlive_ReturnsTrue()
        {
            Assert.IsTrue(CreatureRules.CanAttack(attackerAlive: true, targetAlive: true, distance: 1.2f, attackRange: 1.5f));
        }

        [Test]
        public void CanAttack_WhenOutOfRange_ReturnsFalse()
        {
            Assert.IsFalse(CreatureRules.CanAttack(true, true, distance: 2f, attackRange: 1.5f));
        }

        [Test]
        public void CanAttack_WhenAttackerDead_ReturnsFalse()
        {
            Assert.IsFalse(CreatureRules.CanAttack(attackerAlive: false, targetAlive: true, distance: 0.5f, attackRange: 1.5f));
        }

        [Test]
        public void CanAttack_WhenTargetDead_ReturnsFalse()
        {
            Assert.IsFalse(CreatureRules.CanAttack(attackerAlive: true, targetAlive: false, distance: 0.5f, attackRange: 1.5f));
        }

        [Test]
        public void ApplyDamage_SubtractsAndClampsAtZero()
        {
            Assert.AreEqual(10f, CreatureRules.ApplyDamage(25f, 15f), 0.0001f);
            Assert.AreEqual(0f, CreatureRules.ApplyDamage(10f, 25f), 0.0001f);
        }

        [Test]
        public void AddFood_AddsAndClampsToMax()
        {
            Assert.AreEqual(50f, CreatureRules.AddFood(40f, 10f, 100f), 0.0001f);
            Assert.AreEqual(100f, CreatureRules.AddFood(95f, 20f, 100f), 0.0001f);
        }
    }
}
