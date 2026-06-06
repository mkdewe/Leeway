using UnityEngine;

namespace Leeway.Creature.Domain
{
    public enum NpcType { Food, Predator }

    /// <summary>
    /// Migawka percepcji NPC przekazywana do strategii — czysty kontekst danych
    /// (bez MonoBehaviour), dzięki czemu decyzje AI są testowalne jednostkowo.
    /// </summary>
    public readonly struct NpcPerception
    {
        public readonly Vector2 SelfPosition;
        public readonly float FleeDistance;
        public readonly bool HasThreat;
        public readonly Vector2 ThreatPosition;
        public readonly bool HasFood;
        public readonly Vector2 FoodPosition;
        public readonly Vector2 WanderTarget;

        public NpcPerception(Vector2 selfPosition, float fleeDistance, bool hasThreat, Vector2 threatPosition,
            bool hasFood, Vector2 foodPosition, Vector2 wanderTarget)
        {
            SelfPosition = selfPosition;
            FleeDistance = fleeDistance;
            HasThreat = hasThreat;
            ThreatPosition = threatPosition;
            HasFood = hasFood;
            FoodPosition = foodPosition;
            WanderTarget = wanderTarget;
        }
    }

    /// <summary>
    /// Strategia decyzyjna NPC. Nowy typ zachowania = nowa klasa, bez modyfikacji
    /// istniejących (OCP).
    /// </summary>
    public interface INpcBehavior
    {
        Vector2 DecideTarget(in NpcPerception perception);
    }

    public abstract class NpcBehavior : INpcBehavior
    {
        public abstract Vector2 DecideTarget(in NpcPerception perception);

        protected static Vector2 Flee(in NpcPerception p)
        {
            Vector2 dir = (p.SelfPosition - p.ThreatPosition).normalized;
            return p.SelfPosition + dir * p.FleeDistance;
        }
    }

    /// <summary>Ofiara — błądzi, ucieka przed zagrożeniem, nigdy nie poluje.</summary>
    public sealed class FoodBehavior : NpcBehavior
    {
        public override Vector2 DecideTarget(in NpcPerception p)
        {
            if (p.HasThreat) return Flee(in p);
            return p.WanderTarget;
        }
    }

    /// <summary>Drapieżnik — ucieka przed większym, w przeciwnym razie goni ofiarę.</summary>
    public sealed class PredatorBehavior : NpcBehavior
    {
        public override Vector2 DecideTarget(in NpcPerception p)
        {
            if (p.HasThreat) return Flee(in p);
            if (p.HasFood) return p.FoodPosition;
            return p.WanderTarget;
        }
    }

    public static class NpcBehaviorFactory
    {
        public static INpcBehavior Create(NpcType type) => type switch
        {
            NpcType.Predator => new PredatorBehavior(),
            _ => new FoodBehavior(),
        };
    }
}
