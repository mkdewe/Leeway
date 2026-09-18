using UnityEngine;

namespace Leeway.Creature.Domain
{
    public enum NpcType { Food, Predator }

    /// <summary>
    /// A snapshot of an NPC's perception handed to a strategy — pure data context (no
    /// MonoBehaviour), which keeps the AI decisions unit-testable.
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
    /// An NPC decision strategy. A new kind of behaviour means a new class, with no changes to the
    /// existing ones (open/closed).
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

    /// <summary>Prey — wanders, flees from threats, never hunts.</summary>
    public sealed class FoodBehavior : NpcBehavior
    {
        public override Vector2 DecideTarget(in NpcPerception p)
        {
            if (p.HasThreat) return Flee(in p);
            return p.WanderTarget;
        }
    }

    /// <summary>Predator — flees from anything bigger, otherwise chases prey.</summary>
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
