using UnityEngine;

namespace Leeway.Creature.Domain
{
    /// <summary>
    /// Pure gameplay rules for the cell phase — no Unity dependency beyond the maths.
    /// Split out of <c>CellEntity</c> (a NetworkBehaviour) so they can be unit-tested
    /// without booting the editor or the network.
    /// </summary>
    public static class CellRules
    {
        /// <summary>Whether the predator may eat the prey: both alive and a size advantage.</summary>
        public static bool CanEat(bool predatorAlive, bool preyAlive, float predatorSize, float preySize, float eatSizeRatio)
            => predatorAlive && preyAlive && predatorSize > preySize * eatSizeRatio;

        /// <summary>Movement speed — falls off as the cell grows, with a floor and a ceiling.</summary>
        public static float MoveSpeed(float baseSize, float currentSize, float baseSpeed, float maxSpeed, float speedFloorRatio)
        {
            float sizeRatio = baseSize / Mathf.Max(currentSize, 0.1f);
            return Mathf.Clamp(baseSpeed * sizeRatio, baseSpeed * speedFloorRatio, maxSpeed);
        }

        /// <summary>How much the cell grows after eating prey of the given size.</summary>
        public static float GrowthAmount(float preySize, float growthPerEat) => preySize * growthPerEat;
    }
}
