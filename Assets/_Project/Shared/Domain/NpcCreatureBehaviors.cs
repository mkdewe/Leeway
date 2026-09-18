using UnityEngine;

namespace Leeway.Creature.Domain
{
    public enum NpcCreatureType { Prey, Predator }

    /// <summary>
    /// A snapshot of an NPC creature's perception handed to a strategy — pure data context (no
    /// MonoBehaviour), which keeps the AI decisions unit-testable.
    /// </summary>
    public readonly struct CreaturePerception
    {
        public readonly Vector3 SelfPosition;
        public readonly float FleeDistance;
        public readonly bool HasThreat;
        public readonly Vector3 ThreatPosition;
        public readonly bool HasTarget;
        public readonly Vector3 TargetPosition;
        public readonly Vector3 WanderTarget;

        public CreaturePerception(Vector3 selfPosition, float fleeDistance, bool hasThreat, Vector3 threatPosition,
            bool hasTarget, Vector3 targetPosition, Vector3 wanderTarget)
        {
            SelfPosition = selfPosition;
            FleeDistance = fleeDistance;
            HasThreat = hasThreat;
            ThreatPosition = threatPosition;
            HasTarget = hasTarget;
            TargetPosition = targetPosition;
            WanderTarget = wanderTarget;
        }
    }

    /// <summary>
    /// An NPC creature's decision strategy. A new kind of behaviour means a new class, with no
    /// changes to the existing ones (open/closed).
    /// </summary>
    public interface INpcCreatureBehavior
    {
        Vector3 DecideTarget(in CreaturePerception perception);
    }

    public abstract class NpcCreatureBehavior : INpcCreatureBehavior
    {
        public abstract Vector3 DecideTarget(in CreaturePerception perception);

        protected static Vector3 Flee(in CreaturePerception p)
        {
            Vector3 dir = p.SelfPosition - p.ThreatPosition;
            dir.y = 0f;
            dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.forward;
            return p.SelfPosition + dir * p.FleeDistance;
        }
    }

    /// <summary>Prey — wanders, flees from threats, never attacks.</summary>
    public sealed class PreyBehavior : NpcCreatureBehavior
    {
        public override Vector3 DecideTarget(in CreaturePerception p)
        {
            if (p.HasThreat) return Flee(in p);
            return p.WanderTarget;
        }
    }

    /// <summary>Predator — flees from threats, otherwise chases its target.</summary>
    public sealed class PredatorCreatureBehavior : NpcCreatureBehavior
    {
        public override Vector3 DecideTarget(in CreaturePerception p)
        {
            if (p.HasThreat) return Flee(in p);
            if (p.HasTarget) return p.TargetPosition;
            return p.WanderTarget;
        }
    }

    public static class NpcCreatureBehaviorFactory
    {
        public static INpcCreatureBehavior Create(NpcCreatureType type) => type switch
        {
            NpcCreatureType.Predator => new PredatorCreatureBehavior(),
            _ => new PreyBehavior(),
        };
    }
}
