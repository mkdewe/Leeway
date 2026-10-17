using UnityEngine;

namespace Leeway.Creature.Domain
{
    /// <summary>Stats derived from the genome. Output only — nothing here is authored or editable.</summary>
    public readonly struct CreatureStats
    {
        public readonly float MaxHp;
        public readonly float Mass;
        public readonly float MoveSpeed;
        public readonly float TurnSpeed;
        public readonly float AttackDamage;
        public readonly float SenseRadius;

        /// <summary>
        /// How much lateral acceleration the creature can hold before it topples (m/s²).
        /// </summary>
        /// <remarks>
        /// <para>The ordinary toppling condition: the creature stays up as long as the moment of the
        /// centrifugal force about the outer foot is smaller than the restoring moment from its
        /// weight. Hence <c>a &lt; g · (half the stance width / centre-of-mass height)</c> — a wide,
        /// low creature holds the road, while a tall one on thin legs goes over on the first sharp
        /// corner.</para>
        ///
        /// <para>Zero means "nothing to stand on" — a legless creature lies belly-down and has nothing
        /// to topple.</para>
        /// </remarks>
        public readonly float MaxLateralAcceleration;

        public CreatureStats(float maxHp, float mass, float moveSpeed, float turnSpeed, float attackDamage, float senseRadius,
            float maxLateralAcceleration = 0f)
        {
            MaxHp = maxHp;
            Mass = mass;
            MoveSpeed = moveSpeed;
            TurnSpeed = turnSpeed;
            AttackDamage = attackDamage;
            SenseRadius = senseRadius;
            MaxLateralAcceleration = maxLateralAcceleration;
        }

        public override string ToString()
            => $"HP {MaxHp:0.#} | mass {Mass:0.##} kg | speed {MoveSpeed:0.##} | turn {TurnSpeed:0.#} | damage {AttackDamage:0.#} | sense {SenseRadius:0.#} | stability {MaxLateralAcceleration:0.#} m/s²";
    }

    /// <summary>
    /// Balance constants handed to <see cref="GenomeStatRules"/>. Kept separate from the rules so a
    /// designer can tune them in a ScriptableObject without recompiling, and so tests can pin their
    /// own values and not break when the balance changes.
    /// </summary>
    [System.Serializable]
    public struct CreatureStatTuning
    {
        public float BaseHp;
        public float HpPerVertebra;
        public float BaseMass;

        /// <summary>How many kilograms fall on one unit of a vertebra's volume (r³).</summary>
        public float MassPerVolume;

        public float BaseSpeed;

        /// <summary>The mass at which the speed multiplier is exactly 1.</summary>
        public float ReferenceMass;

        public float MinSpeedFactor;
        public float MaxSpeedFactor;

        /// <summary>Turn rate in <b>degrees per second</b> at neutral mass.</summary>
        public float BaseTurnSpeed;
        public float BaseAttackDamage;
        public float BaseSenseRadius;

        /// <summary>How much speed one leg bend point adds.</summary>
        public float SpeedPerBendPoint;

        /// <summary>
        /// How far above a comfortable cadence the legs may churn before we decide the creature has
        /// nothing left to run faster with.
        /// </summary>
        /// <remarks>
        /// The speed ceiling is no longer a matter of taste: a leg with reach <c>R</c> takes a longest
        /// step of <see cref="LegSpec.MaxStride"/> and wants to take
        /// <see cref="GaitProfile.TargetCadence"/> steps per second, which gives its natural walking
        /// speed. The headroom says how far a sprint may exceed it. Without this limit, stubby legs
        /// carried the creature at 6.4 m/s against their own 0.95 m/s, and instead of steps there was
        /// a vibration on the spot.
        /// </remarks>
        public float LegSpeedHeadroom;

        /// <summary>How fast a creature without a single locomotion part crawls.</summary>
        public float CrawlSpeed;

        /// <summary>The gravitational acceleration used in the toppling condition.</summary>
        public float Gravity;

        /// <summary>
        /// How far above the static toppling limit a creature that balances itself can hold.
        /// </summary>
        /// <remarks>
        /// <para>The moment condition alone describes a rigid body stood on legs. A live animal leans
        /// into the corner and plants its feet outside the arc, so it really does take more — without
        /// this headroom the creature would go over on every sharper turn, and that is steering, not
        /// physics. The headroom is a multiplier, so it <b>does not flatten the differences</b>: a low,
        /// wide creature still takes a corner a tall one on thin legs cannot.</para>
        ///
        /// <para><b>Calibration, not taste.</b> A headroom of 2.5 raised the starter creature's limit
        /// to 4.35 m/s², while its own locomotion produces at most ~3.5 m/s² in a sustained sharp turn
        /// (measured). Toppling was therefore <b>unreachable</b>: the mechanic existed but could not
        /// fire. A headroom of 1.5 gives a limit of 2.6 m/s², above a gentle turn and below the
        /// maximum — exactly where it is meant to decide.</para>
        /// </remarks>
        public float LeanTolerance;

        public static CreatureStatTuning Default => new CreatureStatTuning
        {
            BaseHp = 40f,
            HpPerVertebra = 20f,
            BaseMass = 4f,
            MassPerVolume = 60f,
            BaseSpeed = 3.5f,
            ReferenceMass = 12f,
            MinSpeedFactor = 0.5f,
            MaxSpeedFactor = 1.5f,
            BaseTurnSpeed = 150f,
            BaseAttackDamage = 5f,
            BaseSenseRadius = 6f,
            SpeedPerBendPoint = 0.35f,
            LegSpeedHeadroom = 1.6f,
            CrawlSpeed = 1.1f,
            Gravity = 9.81f,
            LeanTolerance = 1.5f,
        };
    }

    /// <summary>
    /// Deriving stats from the genome. A pure function of three inputs — the same genome, catalog and
    /// tuning always give the same result on the client and on the server.
    /// </summary>
    public static class GenomeStatRules
    {
        /// <summary>
        /// The height at which the legs hold the torso. The longest leg decides — that is the one
        /// lifting the body, while shorter ones simply reach further down. Zero means a legless
        /// creature lying belly-down on the ground.
        /// </summary>
        public static float RideHeight(CreatureGenome genome, PartRuleSet rules)
        {
            if (genome == null || rules == null) return 0f;

            float tallest = 0f;
            for (int i = 0; i < genome.PartCount; i++)
            {
                if (!TryResolveLeg(genome, rules, i, out LegSpec leg, out _)) continue;

                tallest = Mathf.Max(tallest, leg.RideHeight);
            }

            return tallest;
        }

        /// <summary>
        /// The leg of part number <paramref name="partIndex"/>, if it is a locomotion part at all.
        /// </summary>
        /// <remarks>
        /// One gate for all the rules. The player's recorded reshaping takes precedence
        /// (<see cref="PartGene.EffectiveLeg"/>) — otherwise the stats would be computed from the
        /// catalog leg while the creature walked on the one the player sculpted.
        /// </remarks>
        public static bool TryResolveLeg(CreatureGenome genome, PartRuleSet rules, int partIndex,
            out LegSpec leg, out PartGene gene)
        {
            leg = default;
            gene = default;

            if (genome == null || rules == null || partIndex < 0 || partIndex >= genome.PartCount) return false;

            gene = genome.GetPart(partIndex);
            if (!rules.TryGetRule(gene.PartId, out PartRule rule)) return false;
            if (rule.Category != PartCategory.Locomotion) return false;

            leg = gene.EffectiveLeg(rule.Leg);
            return true;
        }

        /// <summary>
        /// How high above the ground the creature's <b>root</b> stands so the legs work comfortably bent.
        /// </summary>
        /// <remarks>
        /// <para>We measure from the <b>hip</b>, not from the root. Legs do not grow out of the centre
        /// of the body but out of its underside — the hip already hangs some way below the root, so
        /// adding the whole carcass radius to the leg reach stood the creature too high. The leg then
        /// straightened out rigidly to full extension, lost its bend, and the foot barely brushed the
        /// ground.</para>
        ///
        /// <para>The leg with the lowest-hanging hip decides: that is the one that touches the ground
        /// first, while higher-mounted ones simply reach further down.</para>
        /// </remarks>
        public static float StandHeight(CreatureGenome genome, PartRuleSet rules)
        {
            if (genome == null || rules == null) return 0f;

            Matrix4x4[] boneToRoot = SpineAnchor.BoneToRoot(genome);
            float tallest = 0f;

            for (int i = 0; i < genome.PartCount; i++)
            {
                if (!TryResolveLeg(genome, rules, i, out LegSpec leg, out PartGene gene)) continue;
                if (gene.BoneIndex >= boneToRoot.Length) continue;

                rules.TryGetRule(gene.PartId, out PartRule rule);
                PartPose pose = PartPlacement.Resolve(genome, gene, rule.SkinOffset);
                Vector3 hip = boneToRoot[gene.BoneIndex].MultiplyPoint3x4(pose.Position);

                // The hip should land exactly at the leg's working height above the ground.
                tallest = Mathf.Max(tallest, leg.RideHeight - hip.y);
            }

            return tallest;
        }

        /// <summary>
        /// Half the stance width: how far from the body axis the outermost foot stands.
        /// </summary>
        /// <remarks>
        /// This is the lever arm the restoring moment works on when toppling. We compute it from the
        /// <b>hip position</b>, because the foot stands roughly underneath it — and a mirrored part
        /// puts a second one on the opposite side, so the stance is symmetric.
        /// </remarks>
        public static float StanceHalfWidth(CreatureGenome genome, PartRuleSet rules)
        {
            if (genome == null || rules == null) return 0f;

            Matrix4x4[] boneToRoot = SpineAnchor.BoneToRoot(genome);
            float widest = 0f;

            for (int i = 0; i < genome.PartCount; i++)
            {
                if (!TryResolveLeg(genome, rules, i, out _, out PartGene gene)) continue;
                if (gene.BoneIndex >= boneToRoot.Length) continue;

                rules.TryGetRule(gene.PartId, out PartRule rule);
                PartPose pose = PartPlacement.Resolve(genome, gene, rule.SkinOffset);
                Vector3 hip = boneToRoot[gene.BoneIndex].MultiplyPoint3x4(pose.Position);

                float half = gene.Mirrored ? Mathf.Abs(hip.x) : Mathf.Abs(hip.x) * 0.5f;

                // Legs attached exactly on the body axis give a stance width of zero — and that is the
                // truth about such a creature: it stands as if on a unicycle. But zero must not come
                // out of this method, because zero reads downstream as "no legs, nothing to topple",
                // i.e. exactly the opposite of what is needed. The floor turns that case into
                // "extremely unstable", which is what it really is.
                widest = Mathf.Max(widest, Mathf.Max(half, MinStanceHalfWidth));
            }

            return widest;
        }

        /// <summary>The narrowest stance we still count as standing on legs rather than having none.</summary>
        private const float MinStanceHalfWidth = 0.02f;

        /// <summary>The thickest vertebra — the carcass radius the creature leans on the world with.</summary>
        public static float BodyRadius(CreatureGenome genome)
        {
            float radius = 0f;
            for (int i = 0; i < genome.VertebraCount; i++)
                radius = Mathf.Max(radius, genome.GetVertebra(i).Radius);

            return radius;
        }

        public static CreatureStats Derive(CreatureGenome genome, PartRuleSet rules, CreatureStatTuning tuning)
        {
            if (genome == null) return default;
            if (rules == null) rules = PartRuleSet.Empty;

            var partTotals = default(PartStatContribution);
            float bendBonus = 0f;

            // The fastest leg in the set. Not the sum: adding pairs of legs buys stability and
            // carrying capacity, but does not make the creature take longer steps.
            float legSpeed = 0f;
            bool hasLegs = false;

            for (int i = 0; i < genome.PartCount; i++)
            {
                PartGene gene = genome.GetPart(i);
                if (!rules.TryGetRule(gene.PartId, out PartRule rule)) continue;

                partTotals += rule.Stats;

                // The foot counts too: a hoof is heavier than a paw and a clawed hand hits harder
                // than either, and those differences are the whole reason to choose between them.
                if (gene.HasFitting && rules.TryGetRule(gene.FittingId, out PartRule fitting))
                    partTotals += fitting.Stats;

                if (!TryResolveLeg(genome, rules, i, out LegSpec leg, out _)) continue;

                hasLegs = true;

                // Bend points genuinely improve the gait: a longer chain takes a longer step, so a leg
                // with more bends carries the creature faster.
                bendBonus += leg.BendPoints * tuning.SpeedPerBendPoint;

                // This leg's natural walking speed: the longest step it can take, times the rate at
                // which it wants to take them.
                legSpeed = Mathf.Max(legSpeed, leg.MaxStride * leg.Gait.TargetCadence);
            }

            float bodyMass = 0f;
            for (int i = 0; i < genome.VertebraCount; i++)
            {
                float r = genome.GetVertebra(i).Radius;
                bodyMass += r * r * r * tuning.MassPerVolume;
            }

            float mass = Mathf.Max(0.01f, tuning.BaseMass + bodyMass + partTotals.MassKg);
            float maxHp = tuning.BaseHp + tuning.HpPerVertebra * genome.VertebraCount + partTotals.HpBonus;

            // A heavier creature is slower, but the multiplier is bounded both ways — without that a
            // minimal genome would be a rocket and a maximal one would not budge.
            float massFactor = Mathf.Clamp(tuning.ReferenceMass / mass, tuning.MinSpeedFactor, tuning.MaxSpeedFactor);
            float moveSpeed = Mathf.Max(0f, (tuning.BaseSpeed + partTotals.SpeedBonus + bendBonus) * massFactor);

            // Bonuses must not carry the creature faster than its own legs do. This is not balance but
            // geometry: above that limit the foot cannot reach the ground at the rate the body is
            // running away from it, and the gait falls apart into a vibration.
            moveSpeed = hasLegs
                ? Mathf.Min(moveSpeed, legSpeed * Mathf.Max(1f, tuning.LegSpeedHeadroom))
                : Mathf.Min(moveSpeed, Mathf.Max(0f, tuning.CrawlSpeed));

            float turnSpeed = tuning.BaseTurnSpeed * massFactor;
            float attackDamage = tuning.BaseAttackDamage + partTotals.DamageBonus;
            float senseRadius = tuning.BaseSenseRadius + partTotals.SenseRadiusBonus;

            // The toppling condition: the stance lever arm against the height the mass hangs at.
            float standHeight = StandHeight(genome, rules);
            float halfWidth = StanceHalfWidth(genome, rules);
            float comHeight = Mathf.Max(0.05f, standHeight);

            float maxLateral = hasLegs && halfWidth > 1e-4f
                ? tuning.Gravity * halfWidth / comHeight * Mathf.Max(1f, tuning.LeanTolerance)
                : 0f;

            return new CreatureStats(maxHp, mass, moveSpeed, turnSpeed, attackDamage, senseRadius, maxLateral);
        }
    }
}
