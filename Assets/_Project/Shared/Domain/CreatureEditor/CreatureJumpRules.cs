using UnityEngine;

namespace Leeway.Creature.Domain
{
    /// <summary>
    /// How high a creature can jump, from what it stands on.
    /// </summary>
    /// <remarks>
    /// <para>Legs are springs: the push-off lasts as long as the leg can extend, so a longer leg
    /// (more reach) and more joints to unfold (bend points) give a higher jump. Extra pairs of legs
    /// add force, but with diminishing returns - four legs do not jump twice as high as two.</para>
    ///
    /// <para>Mass works against it the same way it works against speed: bounded both ways, so a tiny
    /// genome does not become a flea and a heavy one can still hop.</para>
    ///
    /// <para>No legs, no jump. Arms, mouths and senses do not push off the ground.</para>
    /// </remarks>
    public static class CreatureJumpRules
    {
        /// <summary>Jump height per metre of leg reach, before the joint, leg-count and mass factors.</summary>
        public const float HeightPerReach = 1.6f;

        /// <summary>How much each bend point adds to the push-off.</summary>
        public const float BendBonus = 0.18f;

        public const float MinMassFactor = 0.45f;
        public const float MaxMassFactor = 1.5f;
        public const float MaxHeight = 3f;

        /// <summary>Peak jump height in metres; 0 for a creature without legs.</summary>
        public static float JumpHeight(CreatureGenome genome, PartRuleSet rules, float mass, CreatureStatTuning tuning)
        {
            if (genome == null || rules == null) return 0f;

            float bestPushOff = 0f;
            int legCount = 0;

            for (int i = 0; i < genome.PartCount; i++)
            {
                if (!GenomeStatRules.TryResolveLeg(genome, rules, i, out LegSpec leg, out PartGene gene)) continue;

                legCount += gene.Mirrored ? 2 : 1;
                bestPushOff = Mathf.Max(bestPushOff, leg.Reach * (1f + leg.BendPoints * BendBonus));
            }

            if (legCount == 0) return 0f;

            // sqrt: a second pair helps, a third helps less - force grows, the leg's extension does not.
            float legFactor = Mathf.Sqrt(legCount / 2f);
            float massFactor = Mathf.Clamp(tuning.ReferenceMass / Mathf.Max(0.01f, mass), MinMassFactor, MaxMassFactor);

            return Mathf.Min(MaxHeight, bestPushOff * HeightPerReach * legFactor * massFactor);
        }

        /// <summary>Take-off speed that reaches <paramref name="height"/> under <paramref name="gravity"/>.</summary>
        public static float TakeOffSpeed(float height, float gravity)
            => height <= 0f ? 0f : Mathf.Sqrt(2f * Mathf.Max(0.01f, gravity) * height);
    }
}
