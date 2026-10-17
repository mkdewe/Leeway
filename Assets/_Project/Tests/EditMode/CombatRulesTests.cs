using Leeway.Creature.Domain;
using NUnit.Framework;
using UnityEngine;

namespace Leeway.Tests
{
    /// <summary>
    /// The combat maths, pinned without a scene: who a blow reaches, how hard it lands and how often.
    /// </summary>
    public class CombatRulesTests
    {
        private static CombatTuning Tuning => CombatTuning.Default;

        private static CreatureStats Stats(float damage = 12f, float mass = 12f)
            => new CreatureStats(100f, mass, 3f, 150f, damage, 6f);

        [Test]
        public void InArc_TargetStraightAhead_IsHit()
        {
            Assert.IsTrue(CombatRules.InArc(Vector3.zero, Vector3.forward, new Vector3(0f, 0f, 1.5f), 2f, 90f));
        }

        [Test]
        public void InArc_TargetBehind_IsMissed()
        {
            Assert.IsFalse(CombatRules.InArc(Vector3.zero, Vector3.forward, new Vector3(0f, 0f, -1.5f), 2f, 90f));
        }

        [Test]
        public void InArc_TargetBeyondReach_IsMissed()
        {
            Assert.IsFalse(CombatRules.InArc(Vector3.zero, Vector3.forward, new Vector3(0f, 0f, 2.5f), 2f, 90f));
        }

        /// <summary>
        /// Height must not decide a fight: a creature on a rock is neither harder nor easier to bite
        /// than one on the flat, and measuring in three dimensions put tall creatures out of reach of
        /// short ones for no reason a player could see.
        /// </summary>
        [Test]
        public void InArc_HeightDifference_DoesNotCount()
        {
            Assert.IsTrue(CombatRules.InArc(Vector3.zero, Vector3.forward, new Vector3(0f, 3f, 1.5f), 2f, 90f));
        }

        [Test]
        public void InArc_TargetInsideTheAttacker_IsHit()
        {
            Assert.IsTrue(CombatRules.InArc(Vector3.zero, Vector3.forward, Vector3.zero, 2f, 90f));
        }

        [Test]
        public void Cooldown_HeavierCreature_SwingsSlower()
        {
            CombatTuning tuning = Tuning;

            float light = CombatRules.Cooldown(tuning.ReferenceMass * 0.25f, in tuning);
            float heavy = CombatRules.Cooldown(tuning.ReferenceMass * 4f, in tuning);

            Assert.Less(light, heavy);
        }

        [Test]
        public void Cooldown_IsClampedBothWays()
        {
            CombatTuning tuning = Tuning;

            Assert.AreEqual(tuning.MinCooldown, CombatRules.Cooldown(0.001f, in tuning), 1e-4f);
            Assert.AreEqual(tuning.MaxCooldown, CombatRules.Cooldown(1_000_000f, in tuning), 1e-4f);
        }

        [Test]
        public void Derive_UnarmedCreature_HitsWeakerThanAnArmedOne()
        {
            CombatTuning tuning = Tuning;
            CreatureStats stats = Stats();

            AttackSpec armed = CombatRules.Derive(in stats, 1f, armed: true, in tuning);
            AttackSpec bare = CombatRules.Derive(in stats, 1f, armed: false, in tuning);

            Assert.Less(bare.Damage, armed.Damage);
            Assert.Greater(bare.Damage, 0f, "A creature with no parts can still throw its weight about.");
        }

        [Test]
        public void Derive_ImpulseFollowsDamage()
        {
            CombatTuning tuning = Tuning;
            CreatureStats weak = Stats(damage: 4f);
            CreatureStats strong = Stats(damage: 20f);

            Assert.Less(
                CombatRules.Derive(in weak, 1f, true, in tuning).Impulse,
                CombatRules.Derive(in strong, 1f, true, in tuning).Impulse);
        }

        [Test]
        public void Knockback_PushesAwayFromTheAttackerAndSlightlyUp()
        {
            CombatTuning tuning = Tuning;
            CreatureStats stats = Stats();
            AttackSpec spec = CombatRules.Derive(in stats, 1f, true, in tuning);

            Vector3 impulse = CombatRules.Knockback(Vector3.zero, new Vector3(0f, 0f, 2f), in spec, in tuning);

            Assert.Greater(impulse.z, 0f, "The target is pushed away, not pulled in.");
            Assert.Greater(impulse.y, 0f, "A blow lifts the target a little — otherwise friction eats the push.");
            Assert.AreEqual(spec.Impulse, impulse.magnitude, 1e-3f);
        }

        /// <summary>
        /// The reach is a fact about the build, not a constant: this is what makes a maw on a long neck
        /// worth more than the same maw on the chest.
        /// </summary>
        [Test]
        public void StrikeReach_MouthFurtherForward_ReachesFurther()
        {
            CombatTuning tuning = Tuning;
            PartRuleSet rules = GenomeTestFixtures.Rules();

            CreatureGenome near = GenomeTestFixtures.Spine(radius: 0.2f);
            Assert.IsTrue(GenomeEditOperations.TryAttachPart(near,
                new PartGene(GenomeTestFixtures.JawId, 0, new Vector3(0f, 0f, 1f), Quaternion.identity, 1f, false),
                rules, out _));

            CreatureGenome far = GenomeTestFixtures.Spine(radius: 0.6f);
            Assert.IsTrue(GenomeEditOperations.TryAttachPart(far,
                new PartGene(GenomeTestFixtures.JawId, 0, new Vector3(0f, 0f, 1f), Quaternion.identity, 1f, false),
                rules, out _));

            float nearReach = CombatRules.StrikeReach(near, rules, in tuning, out bool nearArmed);
            float farReach = CombatRules.StrikeReach(far, rules, in tuning, out bool farArmed);

            Assert.IsTrue(nearArmed);
            Assert.IsTrue(farArmed);
            Assert.Less(nearReach, farReach);
        }

        [Test]
        public void StrikeReach_NoWeapon_FallsBackAndReportsUnarmed()
        {
            CombatTuning tuning = Tuning;
            PartRuleSet rules = GenomeTestFixtures.Rules();

            float reach = CombatRules.StrikeReach(GenomeTestFixtures.Spine(), rules, in tuning, out bool armed);

            Assert.IsFalse(armed);
            Assert.AreEqual(tuning.UnarmedReach, reach, 1e-4f);
        }

        /// <summary>A sense organ is not a weapon — growing eyes must not lengthen the bite.</summary>
        [Test]
        public void StrikeReach_SensePart_DoesNotArmTheCreature()
        {
            CombatTuning tuning = Tuning;
            PartRuleSet rules = GenomeTestFixtures.Rules();

            CreatureGenome genome = GenomeTestFixtures.Spine();
            Assert.IsTrue(GenomeEditOperations.TryAttachPart(genome,
                new PartGene(GenomeTestFixtures.EyeId, 0, new Vector3(0.3f, 0.4f, 1f), Quaternion.identity, 1f, true),
                rules, out _));

            CombatRules.StrikeReach(genome, rules, in tuning, out bool armed);

            Assert.IsFalse(armed);
        }

        [Test]
        public void IsReady_OnlyAfterTheCooldownHasPassed()
        {
            Assert.IsFalse(CombatRules.IsReady(now: 1.0f, lastStrike: 0.5f, cooldown: 1f));
            Assert.IsTrue(CombatRules.IsReady(now: 1.6f, lastStrike: 0.5f, cooldown: 1f));
        }

        [Test]
        public void CanStrike_KnockedDownAttacker_CannotHit()
        {
            CombatTuning tuning = Tuning;
            CreatureStats stats = Stats();
            AttackSpec spec = CombatRules.Derive(in stats, 1f, true, in tuning);

            Assert.IsFalse(CombatRules.CanStrike(attackerAlive: true, attackerDown: true, targetAlive: true,
                Vector3.zero, Vector3.forward, new Vector3(0f, 0f, 0.5f), in spec));
        }

        [Test]
        public void CanStrike_DeadTarget_IsNotHitAgain()
        {
            CombatTuning tuning = Tuning;
            CreatureStats stats = Stats();
            AttackSpec spec = CombatRules.Derive(in stats, 1f, true, in tuning);

            Assert.IsFalse(CombatRules.CanStrike(attackerAlive: true, attackerDown: false, targetAlive: false,
                Vector3.zero, Vector3.forward, new Vector3(0f, 0f, 0.5f), in spec));
        }
    }
}
