using Leeway.Creature.Domain;
using NUnit.Framework;
using UnityEngine;

namespace Leeway.Tests
{
    /// <summary>
    /// Deriving stats has to be a pure function of the genome, the catalog and the tuning — client
    /// and server compute them independently and have to arrive at the same result.
    /// </summary>
    public class GenomeStatRulesTests
    {
        private PartRuleSet _rules;
        private CreatureStatTuning _tuning;

        [SetUp]
        public void SetUp()
        {
            _rules = GenomeTestFixtures.Rules();
            _tuning = CreatureStatTuning.Default;
        }

        [Test]
        public void Derive_HpGrowsWithVertebraCount()
        {
            CreatureStats three = GenomeStatRules.Derive(GenomeTestFixtures.Spine(3), _rules, _tuning);
            CreatureStats five = GenomeStatRules.Derive(GenomeTestFixtures.Spine(5), _rules, _tuning);

            Assert.AreEqual(2f * _tuning.HpPerVertebra, five.MaxHp - three.MaxHp, 0.0001f,
                "Every vertebra is a fixed portion of HP — that is the editor's basic promise to the player.");
        }

        [Test]
        public void Derive_AddsPartBonuses()
        {
            CreatureGenome bare = GenomeTestFixtures.Spine();
            CreatureGenome equipped = GenomeTestFixtures.Valid();

            CreatureStats bareStats = GenomeStatRules.Derive(bare, _rules, _tuning);
            CreatureStats equippedStats = GenomeStatRules.Derive(equipped, _rules, _tuning);

            // Jaw: +5 HP and +7 damage, eye: +4 sense radius.
            Assert.AreEqual(5f, equippedStats.MaxHp - bareStats.MaxHp, 0.0001f);
            Assert.AreEqual(7f, equippedStats.AttackDamage - bareStats.AttackDamage, 0.0001f);
            Assert.AreEqual(4f, equippedStats.SenseRadius - bareStats.SenseRadius, 0.0001f);
        }

        [Test]
        public void Derive_HeavierBodyIsSlower()
        {
            // This test is about the mass multiplier alone, so the crawl ceiling comes off —
            // otherwise both bare spines would hit the same limit and the comparison would mean
            // nothing. The ceiling itself has a test of its own.
            CreatureStatTuning tuning = WithoutLegCeiling(_tuning);

            // Both radii are chosen so the mass multiplier stays inside the unclamped range.
            CreatureStats light = GenomeStatRules.Derive(GenomeTestFixtures.Spine(3, 0.35f), _rules, tuning);
            CreatureStats heavy = GenomeStatRules.Derive(GenomeTestFixtures.Spine(3, 0.45f), _rules, tuning);

            Assert.Greater(heavy.Mass, light.Mass);
            Assert.Less(heavy.MoveSpeed, light.MoveSpeed);
            Assert.Greater(heavy.MoveSpeed, 0f);
        }

        [Test]
        public void Derive_SpeedFactorIsClampedAtBothEnds()
        {
            CreatureStatTuning tuning = WithoutLegCeiling(_tuning);

            CreatureStats tiny = GenomeStatRules.Derive(
                GenomeTestFixtures.Spine(GenomeLimits.MinVertebrae, GenomeLimits.MinRadius), _rules, tuning);
            CreatureStats huge = GenomeStatRules.Derive(
                GenomeTestFixtures.Spine(GenomeLimits.MaxVertebrae, GenomeLimits.MaxRadius), _rules, tuning);

            Assert.AreEqual(tuning.BaseSpeed * tuning.MaxSpeedFactor, tiny.MoveSpeed, 0.0001f,
                "The smallest creature must not be a rocket.");
            Assert.AreEqual(tuning.BaseSpeed * tuning.MinSpeedFactor, huge.MoveSpeed, 0.0001f,
                "The largest creature still has to move.");
        }

        /// <summary>
        /// Tuning without the leg-imposed ceiling — for tests that are about something else.
        /// </summary>
        /// <remarks>
        /// We raise the ceiling rather than switch it off: the rule stays the same, it simply stops
        /// binding. That way the test does not know about an exception the code does not have.
        /// </remarks>
        private static CreatureStatTuning WithoutLegCeiling(CreatureStatTuning tuning)
        {
            tuning.CrawlSpeed = 1000f;
            tuning.LegSpeedHeadroom = 1000f;
            return tuning;
        }

        [Test]
        public void Derive_SpeedIsCappedByWhatTheLegsCanCarry()
        {
            // Legs from the fixture catalog: 2 bend points of 0.26 m each, so a reach of 0.78 m.
            CreatureGenome genome = GenomeTestFixtures.Valid();

            GenomeStatRules.TryResolveLeg(genome, _rules, 0, out LegSpec leg, out _);
            float natural = leg.MaxStride * leg.Gait.TargetCadence;

            CreatureStats stats = GenomeStatRules.Derive(genome, _rules, _tuning);

            Assert.LessOrEqual(stats.MoveSpeed, natural * _tuning.LegSpeedHeadroom + 0.0001f,
                "Bonuses must not carry a creature faster than its own legs can.");
        }

        [Test]
        public void Derive_LongerLegsCarryFaster()
        {
            CreatureGenome shortLegs = GenomeTestFixtures.Valid();
            CreatureGenome longLegs = GenomeTestFixtures.Valid();

            // The same part, rebuilt in the editor into a longer leg.
            longLegs.SetPart(0, longLegs.GetPart(0).WithLeg(new LegSpec(2, 0.6f)));

            CreatureStats slow = GenomeStatRules.Derive(shortLegs, _rules, _tuning);
            CreatureStats fast = GenomeStatRules.Derive(longLegs, _rules, _tuning);

            Assert.Greater(fast.MoveSpeed, slow.MoveSpeed,
                "A longer leg takes a longer stride, so it has to carry faster.");
        }

        [Test]
        public void Derive_WithoutLegs_CrawlsInsteadOfSprinting()
        {
            CreatureStats stats = GenomeStatRules.Derive(GenomeTestFixtures.Spine(), _rules, _tuning);

            Assert.AreEqual(_tuning.CrawlSpeed, stats.MoveSpeed, 0.0001f,
                "A creature without a single locomotion part has nothing to run on.");
            Assert.AreEqual(0f, stats.MaxLateralAcceleration, 0.0001f,
                "No legs, nothing to topple.");
        }

        [Test]
        public void Derive_TallerOnNarrowStance_IsEasierToTipOver()
        {
            CreatureGenome low = GenomeTestFixtures.Valid();
            CreatureGenome tall = GenomeTestFixtures.Valid();

            tall.SetPart(0, tall.GetPart(0).WithLeg(new LegSpec(2, 0.7f)));

            CreatureStats lowStats = GenomeStatRules.Derive(low, _rules, _tuning);
            CreatureStats tallStats = GenomeStatRules.Derive(tall, _rules, _tuning);

            Assert.Greater(tallStats.MoveSpeed, lowStats.MoveSpeed);
            Assert.Less(tallStats.MaxLateralAcceleration, lowStats.MaxLateralAcceleration,
                "Mass hung higher over the same stance topples in a gentler turn.");
        }

        /// <summary>
        /// The fraction of the theoretical peak a creature actually reaches. Measured at ~0.7.
        /// </summary>
        private const float ReachableFraction = 0.75f;

        /// <summary>
        /// The topple limit has to be reachable by the creature that carries it.
        /// </summary>
        /// <remarks>
        /// <para>A gate against a regression that once slipped through unnoticed: the balancing headroom
        /// pushed the starter creature's limit up to 4.35 m/s², while its own locomotion produces at
        /// most ~3.5 in a sustained turn. The topple mechanic existed, it was covered by tests — and it
        /// could never fire once.</para>
        ///
        /// <para>The product <c>MoveSpeed × TurnSpeed</c> is the theoretical peak of centripetal
        /// acceleration (<c>a = v·ω</c>). What is actually reachable is less, because
        /// <c>LocomotionSteering.Throttle</c> is the cosine of the heading error, so <b>turning cuts its
        /// own speed</b> and both maxima cannot be had at once. Hence the required margin rather than a
        /// plain "less than".</para>
        /// </remarks>
        [Test]
        public void Derive_TippingLimitIsReachableByItsOwnTurning()
        {
            CreatureGenome genome = GenomeTestFixtures.Valid();
            CreatureStats stats = GenomeStatRules.Derive(genome, _rules, _tuning);

            Assert.Greater(stats.MaxLateralAcceleration, 0f, "A creature standing on legs has to have a topple limit.");

            float theoreticalPeak = stats.MoveSpeed * stats.TurnSpeed * Mathf.Deg2Rad;

            Assert.Less(stats.MaxLateralAcceleration, theoreticalPeak * ReachableFraction,
                "The topple limit sits above what the creature can reach on its own legs — " +
                "the topple mechanic has no way to fire.");
        }

        [Test]
        public void Derive_UnknownParts_ContributeNothing()
        {
            CreatureGenome genome = GenomeTestFixtures.Valid();

            CreatureStats withCatalog = GenomeStatRules.Derive(genome, _rules, _tuning);
            CreatureStats withoutCatalog = GenomeStatRules.Derive(genome, PartRuleSet.Empty, _tuning);

            Assert.Less(withoutCatalog.MaxHp, withCatalog.MaxHp);
            Assert.AreEqual(_tuning.BaseAttackDamage, withoutCatalog.AttackDamage, 0.0001f);
        }

        [Test]
        public void Derive_NullGenome_ReturnsDefault()
        {
            CreatureStats stats = GenomeStatRules.Derive(null, _rules, _tuning);
            Assert.AreEqual(0f, stats.MaxHp);
            Assert.AreEqual(0f, stats.Mass);
        }

        [Test]
        public void Derive_IsDeterministic()
        {
            CreatureGenome genome = GenomeTestFixtures.Valid();

            CreatureStats first = GenomeStatRules.Derive(genome, _rules, _tuning);
            CreatureStats second = GenomeStatRules.Derive(genome, _rules, _tuning);

            Assert.AreEqual(first.MaxHp, second.MaxHp);
            Assert.AreEqual(first.Mass, second.Mass);
            Assert.AreEqual(first.MoveSpeed, second.MoveSpeed);
        }
    }
}
