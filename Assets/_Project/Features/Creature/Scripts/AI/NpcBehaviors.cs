using UnityEngine;

namespace Leeway.Creature.AI
{
    /// <summary>
    /// Strategia decyzyjna NPC. Percepcja (skan otoczenia) zostaje w
    /// <see cref="NpcCellController"/>; strategia tylko wybiera cel ruchu.
    /// Nowy typ zachowania = nowa klasa, bez modyfikacji istniejących (OCP).
    /// </summary>
    public interface INpcBehavior
    {
        Vector2 DecideTarget(NpcCellController self, CellEntity closestFood, CellEntity closestThreat, Vector2 wanderTarget);
    }

    public abstract class NpcBehavior : INpcBehavior
    {
        public abstract Vector2 DecideTarget(NpcCellController self, CellEntity closestFood, CellEntity closestThreat, Vector2 wanderTarget);

        protected static Vector2 Flee(NpcCellController self, CellEntity threat)
        {
            Vector2 pos = self.transform.position;
            Vector2 dir = (pos - (Vector2)threat.transform.position).normalized;
            return pos + dir * self.Config.FleeDistance;
        }
    }

    /// <summary>Ofiara — błądzi, ucieka przed zagrożeniem, nigdy nie poluje.</summary>
    public sealed class FoodBehavior : NpcBehavior
    {
        public override Vector2 DecideTarget(NpcCellController self, CellEntity closestFood, CellEntity closestThreat, Vector2 wanderTarget)
        {
            if (closestThreat != null) return Flee(self, closestThreat);
            return wanderTarget;
        }
    }

    /// <summary>Drapieżnik — ucieka przed większym, w przeciwnym razie goni ofiarę.</summary>
    public sealed class PredatorBehavior : NpcBehavior
    {
        public override Vector2 DecideTarget(NpcCellController self, CellEntity closestFood, CellEntity closestThreat, Vector2 wanderTarget)
        {
            if (closestThreat != null) return Flee(self, closestThreat);
            if (closestFood != null) return closestFood.transform.position;
            return wanderTarget;
        }
    }

    public static class NpcBehaviorFactory
    {
        public static INpcBehavior Create(NpcCellController.NpcType type) => type switch
        {
            NpcCellController.NpcType.Predator => new PredatorBehavior(),
            _ => new FoodBehavior(),
        };
    }
}
