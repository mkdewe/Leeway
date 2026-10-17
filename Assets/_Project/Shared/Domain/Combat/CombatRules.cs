using UnityEngine;

namespace Leeway.Creature.Domain
{
    /// <summary>
    /// One creature's blow, derived from its genome. Output only — nothing here is authored.
    /// </summary>
    /// <remarks>
    /// A creature fights with the body it was built with: a maw and a horn hit hard and far, a bare
    /// head barely at all, and a heavy animal swings slower than a light one. All of that follows from
    /// the genome, so a player who builds a predator gets one without a single switch to set.
    /// </remarks>
    public readonly struct AttackSpec
    {
        /// <summary>Hit points taken off the target.</summary>
        public readonly float Damage;

        /// <summary>How far in front of the creature's root the blow lands, in metres.</summary>
        public readonly float Reach;

        /// <summary>The cone in front of the creature the blow covers, in degrees, full width.</summary>
        public readonly float ArcDegrees;

        /// <summary>Seconds between one blow and the next.</summary>
        public readonly float Cooldown;

        /// <summary>The knockback, in newton-seconds. Enough of it topples the target.</summary>
        public readonly float Impulse;

        /// <summary>Whether a mouth or a weapon is doing the hitting, rather than the bare body.</summary>
        public readonly bool Armed;

        public AttackSpec(float damage, float reach, float arcDegrees, float cooldown, float impulse, bool armed)
        {
            Damage = damage;
            Reach = reach;
            ArcDegrees = arcDegrees;
            Cooldown = cooldown;
            Impulse = impulse;
            Armed = armed;
        }

        public override string ToString()
            => $"{Damage:0.#} dmg | reach {Reach:0.##} m | arc {ArcDegrees:0}° | every {Cooldown:0.##} s | impulse {Impulse:0.#}";
    }

    /// <summary>
    /// Balance constants for combat. Kept apart from the rules for the same reason as
    /// <see cref="CreatureStatTuning"/>: a designer tunes them without a recompile, and a test pins
    /// its own values so it does not break when the balance moves.
    /// </summary>
    [System.Serializable]
    public struct CombatTuning
    {
        /// <summary>Added to the reach of whatever does the hitting — a bite lands slightly beyond the teeth.</summary>
        public float ReachPadding;

        /// <summary>The reach of a creature with nothing to hit with, measured from its root.</summary>
        public float UnarmedReach;

        /// <summary>The cone in front of the creature a blow covers, full width in degrees.</summary>
        public float ArcDegrees;

        /// <summary>Seconds between blows at the reference mass.</summary>
        public float BaseCooldown;

        public float MinCooldown;
        public float MaxCooldown;

        /// <summary>The mass at which the cooldown is exactly <see cref="BaseCooldown"/>.</summary>
        public float ReferenceMass;

        /// <summary>
        /// How strongly mass slows the swing. Below 1 a tenfold mass does <b>not</b> make the creature
        /// ten times slower — a big animal is slower, not helpless.
        /// </summary>
        public float MassCooldownExponent;

        /// <summary>Newton-seconds of knockback per point of damage.</summary>
        public float ImpulsePerDamage;

        /// <summary>How much of the knockback goes upwards. Purely upward pushes look like a jump, not a hit.</summary>
        public float UpwardBias;

        /// <summary>
        /// What a creature with no mouth and no weapon does with a blow, as a fraction of its damage.
        /// </summary>
        /// <remarks>
        /// Not zero: anything with a body can throw its weight about. But a creature that has grown
        /// jaws should be plainly better at it, or there is no reason to grow them.
        /// </remarks>
        public float UnarmedFactor;

        public static CombatTuning Default => new CombatTuning
        {
            ReachPadding = 0.35f,
            UnarmedReach = 0.6f,
            ArcDegrees = 110f,
            BaseCooldown = 1.1f,
            MinCooldown = 0.45f,
            MaxCooldown = 2.6f,
            ReferenceMass = 12f,
            MassCooldownExponent = 0.35f,
            ImpulsePerDamage = 0.7f,
            UpwardBias = 0.25f,
            UnarmedFactor = 0.45f,
        };
    }

    /// <summary>
    /// Pure combat maths: who can hit whom, how hard, and which way they fly. No Unity beyond the
    /// vector types, so every one of these decisions is testable without a scene — and the server can
    /// take them without trusting anything a client said.
    /// </summary>
    public static class CombatRules
    {
        /// <summary>The categories that count as something to fight with.</summary>
        public static bool IsWeapon(PartCategory category)
            => category == PartCategory.Mouth || category == PartCategory.Weapon || category == PartCategory.Grasper;

        /// <summary>
        /// The blow a genome produces.
        /// </summary>
        /// <param name="stats">The stats already derived from that genome — the damage comes from there.</param>
        /// <param name="reach">How far the hitting part sticks out in front, from <see cref="StrikeReach"/>.</param>
        /// <param name="armed">Whether the creature has anything to hit with.</param>
        public static AttackSpec Derive(in CreatureStats stats, float reach, bool armed, in CombatTuning tuning)
        {
            float damage = Mathf.Max(0f, stats.AttackDamage) * (armed ? 1f : Mathf.Clamp01(tuning.UnarmedFactor));

            return new AttackSpec(
                damage,
                Mathf.Max(0.1f, reach + tuning.ReachPadding),
                Mathf.Clamp(tuning.ArcDegrees, 10f, 360f),
                Cooldown(stats.Mass, tuning),
                damage * Mathf.Max(0f, tuning.ImpulsePerDamage),
                armed);
        }

        /// <summary>The blow a genome produces, worked out from the genome itself.</summary>
        public static AttackSpec Derive(CreatureGenome genome, PartRuleSet rules, in CreatureStats stats, in CombatTuning tuning)
        {
            float reach = StrikeReach(genome, rules, tuning, out bool armed);
            return Derive(in stats, reach, armed, in tuning);
        }

        /// <summary>
        /// How far in front of the root the creature's best weapon sits.
        /// </summary>
        /// <remarks>
        /// <para>Measured from the actual build, not guessed from the body's size: a maw on a long neck
        /// reaches further than one set into the chest, and that difference is the whole point of
        /// letting players place parts where they like.</para>
        ///
        /// <para>Only what sticks out <b>forwards</b> counts. A horn on the tail is a fine deterrent but
        /// it is not what the creature bites with, and letting it set the reach would have creatures
        /// striking from behind themselves.</para>
        /// </remarks>
        public static float StrikeReach(CreatureGenome genome, PartRuleSet rules, in CombatTuning tuning, out bool armed)
        {
            armed = false;
            if (genome == null || rules == null) return tuning.UnarmedReach;

            Matrix4x4[] boneToRoot = SpineAnchor.BoneToRoot(genome);
            float furthest = 0f;

            for (int i = 0; i < genome.PartCount; i++)
            {
                PartGene gene = genome.GetPart(i);
                if (!rules.TryGetRule(gene.PartId, out PartRule rule)) continue;
                if (!IsWeapon(rule.Category)) continue;
                if (gene.BoneIndex >= boneToRoot.Length) continue;

                PartPose pose = PartPlacement.Resolve(genome, gene, rule.SkinOffset);
                Vector3 inRoot = boneToRoot[gene.BoneIndex].MultiplyPoint3x4(pose.Position);

                armed = true;
                furthest = Mathf.Max(furthest, inRoot.z);
            }

            return armed && furthest > 0f ? furthest : tuning.UnarmedReach;
        }

        /// <summary>Seconds between blows: heavier swings slower.</summary>
        public static float Cooldown(float mass, in CombatTuning tuning)
        {
            float reference = Mathf.Max(0.01f, tuning.ReferenceMass);
            float ratio = Mathf.Max(0.01f, mass) / reference;

            return Mathf.Clamp(
                tuning.BaseCooldown * Mathf.Pow(ratio, tuning.MassCooldownExponent),
                tuning.MinCooldown,
                tuning.MaxCooldown);
        }

        /// <summary>
        /// Whether a blow lands: the target has to be alive, within reach and in front.
        /// </summary>
        /// <remarks>
        /// The distance is measured <b>flat</b>. A creature standing on a boulder is neither harder nor
        /// easier to bite than one on the ground, and taking height into account only meant that tall
        /// creatures could not reach short ones.
        /// </remarks>
        public static bool CanStrike(bool attackerAlive, bool attackerDown, bool targetAlive,
            Vector3 origin, Vector3 forward, Vector3 target, in AttackSpec spec)
        {
            if (!attackerAlive || attackerDown || !targetAlive) return false;

            return InArc(origin, forward, target, spec.Reach, spec.ArcDegrees);
        }

        /// <summary>Whether a point lies in the cone in front of the creature, within reach.</summary>
        public static bool InArc(Vector3 origin, Vector3 forward, Vector3 point, float reach, float arcDegrees)
        {
            Vector3 offset = point - origin;
            offset.y = 0f;

            float distance = offset.magnitude;
            if (distance > reach) return false;

            // Right on top of the attacker: there is no direction to compare, and refusing the blow
            // there would make a creature safe by walking into its attacker.
            if (distance < 1e-4f) return true;

            Vector3 flatForward = forward;
            flatForward.y = 0f;
            if (flatForward.sqrMagnitude < 1e-6f) return true;

            float angle = Vector3.Angle(flatForward.normalized, offset / distance);
            return angle <= arcDegrees * 0.5f;
        }

        /// <summary>
        /// The knockback from a blow: away from the attacker, with a little lift.
        /// </summary>
        /// <remarks>
        /// The lift is what makes a hit read as a hit — a purely horizontal push slides the target
        /// along the ground, and the friction under it eats most of that before anyone sees it.
        /// </remarks>
        public static Vector3 Knockback(Vector3 origin, Vector3 target, in AttackSpec spec, in CombatTuning tuning)
        {
            Vector3 away = target - origin;
            away.y = 0f;

            Vector3 direction = away.sqrMagnitude > 1e-6f ? away.normalized : Vector3.forward;
            return (direction + Vector3.up * Mathf.Max(0f, tuning.UpwardBias)).normalized * spec.Impulse;
        }

        /// <summary>Whether enough time has passed since the last blow.</summary>
        public static bool IsReady(float now, float lastStrike, float cooldown)
            => now - lastStrike >= cooldown;
    }
}
