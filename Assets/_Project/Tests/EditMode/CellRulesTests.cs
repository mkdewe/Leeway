using Leeway.Creature.Domain;
using NUnit.Framework;

namespace Leeway.Tests
{
    public class CellRulesTests
    {
        [Test]
        public void CanEat_WhenPredatorBiggerThanThreshold_ReturnsTrue()
        {
            // 1.6 > 1.0 * 1.15 = 1.15  -> true
            Assert.IsTrue(CellRules.CanEat(true, true, predatorSize: 1.6f, preySize: 1.0f, eatSizeRatio: 1.15f));
        }

        [Test]
        public void CanEat_WhenEqualSizes_ReturnsFalse()
        {
            // 1.0 > 1.0 * 1.15 = 1.15 -> false (to był pierwotny bug: wszyscy ten sam rozmiar)
            Assert.IsFalse(CellRules.CanEat(true, true, 1.0f, 1.0f, 1.15f));
        }

        [Test]
        public void CanEat_WhenPredatorDead_ReturnsFalse()
        {
            Assert.IsFalse(CellRules.CanEat(predatorAlive: false, preyAlive: true, 5f, 1f, 1.15f));
        }

        [Test]
        public void CanEat_WhenPreyDead_ReturnsFalse()
        {
            Assert.IsFalse(CellRules.CanEat(predatorAlive: true, preyAlive: false, 5f, 1f, 1.15f));
        }

        [Test]
        public void MoveSpeed_AtBaseSize_EqualsBaseSpeed()
        {
            float speed = CellRules.MoveSpeed(baseSize: 1f, currentSize: 1f, baseSpeed: 5f, maxSpeed: 12f, speedFloorRatio: 0.3f);
            Assert.AreEqual(5f, speed, 0.0001f);
        }

        [Test]
        public void MoveSpeed_WhenSmaller_IsFaster()
        {
            // ratio = 1 / 0.5 = 2 -> 10 (w granicach)
            float speed = CellRules.MoveSpeed(1f, 0.5f, 5f, 12f, 0.3f);
            Assert.AreEqual(10f, speed, 0.0001f);
        }

        [Test]
        public void MoveSpeed_WhenTiny_IsClampedToMax()
        {
            float speed = CellRules.MoveSpeed(1f, 0.1f, 5f, 12f, 0.3f);
            Assert.AreEqual(12f, speed, 0.0001f);
        }

        [Test]
        public void MoveSpeed_WhenHuge_IsClampedToFloor()
        {
            // floor = 5 * 0.3 = 1.5
            float speed = CellRules.MoveSpeed(1f, 100f, 5f, 12f, 0.3f);
            Assert.AreEqual(1.5f, speed, 0.0001f);
        }

        [Test]
        public void GrowthAmount_IsPreySizeTimesGrowth()
        {
            Assert.AreEqual(0.125f, CellRules.GrowthAmount(preySize: 0.5f, growthPerEat: 0.25f), 0.0001f);
        }
    }
}
