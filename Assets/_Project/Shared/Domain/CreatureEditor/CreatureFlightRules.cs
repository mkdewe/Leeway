using UnityEngine;

namespace Leeway.Creature.Domain
{
    /// <summary>
    /// What wings do. Pure maths over the genome, so the server and every predicting client work out the
    /// same flap and the same glide from the same build.
    /// </summary>
    /// <remarks>
    /// <para>Flying is <b>work</b>. Each press of the jump key beats the wings once - a lift of speed and a
    /// cost in stamina - and a creature that keeps pressing climbs while it has the strength. Hold the key
    /// instead and the wings stay out: the creature stops falling and slides forward, which costs much
    /// less, and is how a tired one gets home.</para>
    ///
    /// <para>Bigger wings lift more, but the lift is shared with the weight they carry: the same wings
    /// that lift a light animal barely hold up a heavy one.</para>
    /// </remarks>
    public static class CreatureFlightRules
    {
        /// <summary>The key of the wing part in the catalog.</summary>
        public const string WingKey = "wing.feather";

        public const float FlapCost = 16f;
        public const float GlideDrainPerSecond = 5f;
        public const float FlapCooldown = 0.22f;

        /// <summary>Forward speed in a glide, as a multiple of walking speed.</summary>
        public const float GlideSpeedFactor = 1.3f;

        /// <summary>
        /// The total wing area, in units of one pair at the standard size: a mirrored wing counts twice and
        /// a scaled one by its area. Zero for a creature with none.
        /// </summary>
        public static float WingPower(CreatureGenome genome, PartRuleSet rules)
        {
            if (genome == null || rules == null) return 0f;

            float power = 0f;
            for (int i = 0; i < genome.PartCount; i++)
            {
                PartGene gene = genome.GetPart(i);
                if (!rules.TryGetRule(gene.PartId, out PartRule rule) || rule.Category != PartCategory.Wing) continue;

                power += (gene.Mirrored ? 2f : 1f) * gene.Scale * gene.Scale * 0.5f;
            }

            return power;
        }

        public static bool CanFly(float wingPower) => wingPower > 0.05f;

        /// <summary>The upward speed a single beat gives (m/s).</summary>
        public static float FlapSpeed(float wingPower, float mass)
        {
            if (!CanFly(wingPower)) return 0f;
            return Mathf.Clamp(2.2f + 1.9f * wingPower / Mathf.Pow(Mathf.Max(1f, mass), 0.33f), 2.4f, 6.5f);
        }

        /// <summary>The fastest a gliding creature sinks (m/s). Big wings on a light body barely sink at all.</summary>
        public static float GlideSinkSpeed(float wingPower, float mass)
        {
            if (!CanFly(wingPower)) return float.PositiveInfinity;
            return Mathf.Clamp(3.2f - 1.1f * wingPower / Mathf.Pow(Mathf.Max(1f, mass), 0.33f), 0.5f, 3.2f);
        }
    }

    /// <summary>
    /// The creature's strength to spend: running, flying, jumping and fighting all draw on it, and it comes
    /// back when the creature rests.
    /// </summary>
    public static class CreatureStaminaRules
    {
        public const float SprintDrainPerSecond = 14f;
        public const float JumpCost = 10f;
        public const float AttackCost = 8f;

        public const float RegenPerSecond = 16f;

        /// <summary>Seconds after the last spending before it starts to come back.</summary>
        public const float RegenDelay = 0.8f;

        /// <summary>The level a worn-out creature has to climb back to before it can sprint or take wing again.</summary>
        public const float RecoveryThreshold = 20f;

        public static float Max(CreatureGenome genome)
            => genome == null ? 100f : 100f + 6f * Mathf.Max(0, genome.VertebraCount - 3);
    }
}
