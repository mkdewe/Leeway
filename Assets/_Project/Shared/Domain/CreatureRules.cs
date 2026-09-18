using UnityEngine;

namespace Leeway.Creature.Domain
{
    /// <summary>
    /// Pure combat and foraging rules for the creature phase — no Unity dependency
    /// beyond the maths, so they stay unit-testable.
    /// </summary>
    public static class CreatureRules
    {
        public static bool CanAttack(bool attackerAlive, bool targetAlive, float distance, float attackRange)
            => attackerAlive && targetAlive && distance <= attackRange;

        public static float ApplyDamage(float currentHp, float damage)
            => Mathf.Max(0f, currentHp - damage);

        public static float AddFood(float currentFood, float amount, float maxFood)
            => Mathf.Clamp(currentFood + amount, 0f, maxFood);
    }
}
