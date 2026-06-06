using UnityEngine;

namespace Leeway.Creature.Domain
{
    /// <summary>
    /// Czyste reguły rozgrywki komórek — bez zależności od Unity poza matematyką.
    /// Wydzielone z <c>CellEntity</c> (NetworkBehaviour), żeby dało się je testować
    /// jednostkowo bez uruchamiania edytora ani sieci.
    /// </summary>
    public static class CellRules
    {
        /// <summary>Czy drapieżnik może zjeść ofiarę (oboje żywi i przewaga rozmiaru).</summary>
        public static bool CanEat(bool predatorAlive, bool preyAlive, float predatorSize, float preySize, float eatSizeRatio)
            => predatorAlive && preyAlive && predatorSize > preySize * eatSizeRatio;

        /// <summary>Prędkość ruchu — maleje wraz ze wzrostem, z dolnym i górnym limitem.</summary>
        public static float MoveSpeed(float baseSize, float currentSize, float baseSpeed, float maxSpeed, float speedFloorRatio)
        {
            float sizeRatio = baseSize / Mathf.Max(currentSize, 0.1f);
            return Mathf.Clamp(baseSpeed * sizeRatio, baseSpeed * speedFloorRatio, maxSpeed);
        }

        /// <summary>O ile rośnie komórka po zjedzeniu ofiary danego rozmiaru.</summary>
        public static float GrowthAmount(float preySize, float growthPerEat) => preySize * growthPerEat;
    }
}
