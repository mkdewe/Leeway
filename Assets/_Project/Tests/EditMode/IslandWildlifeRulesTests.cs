using System;
using System.Linq;
using Leeway.Creature.Domain;
using Leeway.CreatureEditor;
using Leeway.CreatureEditor.Authoring;
using Leeway.World.Domain;
using NUnit.Framework;
using UnityEngine;
namespace Leeway.Tests
{
    public class IslandWildlifeRulesTests
    {
        private static PartRuleSet Rules()=>PartAuthoringAudit.FindCatalog().BuildRuleSet();
        [Test] public void EverySpeciesAndVariantIsBuildableFromRealCatalog()
        {
            var rules=Rules();
            foreach(IslandSpecies s in Enum.GetValues(typeof(IslandSpecies)))
            for(int variant=0;variant<12;variant++)
            {
                var g=IslandSpeciesLibrary.Create(s,rules,variant);
                Assert.That(GenomeValidator.Validate(g,rules).IsValid,Is.True,s+" variant "+variant);
                Assert.That(g.CountParts(PartCategory.Locomotion,rules),Is.GreaterThan(0),s.ToString());
                Assert.That(GenomeStatRules.StandHeight(g,rules),Is.GreaterThan(0),s.ToString());
                Assert.That(CreatureBudget.Evaluate(g,rules).Remaining,Is.GreaterThanOrEqualTo(0));
                Assert.That(g.Parts.All(p=>rules.TryGetRule(p.PartId,out _)),Is.True);
            }
        }
        [Test] public void AnatomyActuallyGatesAbilities()
        {
            var r=Rules();var bare=GenomeTestFixtures.Spine();
            Assert.That(BodyAbilityRules.Has(bare,r,CreatureAbility.BodyStrike),Is.True);
            foreach(var a in new[]{CreatureAbility.Bite,CreatureAbility.Claw,CreatureAbility.Horn,CreatureAbility.Scan,CreatureAbility.Forage})
                Assert.That(BodyAbilityRules.Has(bare,r,a),Is.False,a.ToString());
            var horn=IslandSpeciesLibrary.Create(IslandSpecies.Rockhorn,r);
            Assert.That(BodyAbilityRules.Has(horn,r,CreatureAbility.Horn),Is.True);
            Assert.That(BodyAbilityRules.Has(horn,r,CreatureAbility.Claw),Is.False);
            var gatherer=IslandSpeciesLibrary.Create(IslandSpecies.BrookGatherer,r);
            Assert.That(BodyAbilityRules.Has(gatherer,r,CreatureAbility.Claw),Is.True);
            Assert.That(BodyAbilityRules.Has(gatherer,r,CreatureAbility.Scan),Is.True);
        }
        [Test] public void HornHasReadableWindupAndNarrowerStrongerStrike()
        {
            var r=Rules();var g=IslandSpeciesLibrary.Create(IslandSpecies.Rockhorn,r);
            var stats=GenomeStatRules.Derive(g,r,CreatureStatTuning.Default);
            var bite=BodyAbilityRules.Attack(g,r,in stats,CreatureAbility.Bite,CombatTuning.Default);
            var horn=BodyAbilityRules.Attack(g,r,in stats,CreatureAbility.Horn,CombatTuning.Default);
            Assert.That(horn.Damage,Is.GreaterThan(bite.Damage));
            Assert.That(horn.ArcDegrees,Is.LessThan(bite.ArcDegrees));
            Assert.That(horn.Cooldown,Is.GreaterThan(bite.Cooldown));
            Assert.That(BodyAbilityRules.Windup(CreatureAbility.Horn),Is.GreaterThan(0.45f));
            Assert.That(horn.Impulse,Is.GreaterThan(bite.Impulse));
        }
        [Test] public void LargerMouthChangesItsOwnAttack()
        {
            var r=Rules();var a=IslandSpeciesLibrary.Create(IslandSpecies.MossGrazer,r);
            var b=a.Clone();int index=Enumerable.Range(0,b.PartCount).First(i=>r.TryGetRule(b.GetPart(i).PartId,out var rule)&&rule.Category==PartCategory.Mouth);
            b.SetPart(index,b.GetPart(index).WithScale(1.5f));
            var stats=GenomeStatRules.Derive(a,r,CreatureStatTuning.Default);
            var small=BodyAbilityRules.Attack(a,r,in stats,CreatureAbility.Bite,CombatTuning.Default);
            var large=BodyAbilityRules.Attack(b,r,in stats,CreatureAbility.Bite,CombatTuning.Default);
            Assert.That(large.Damage,Is.GreaterThan(small.Damage));Assert.That(large.Reach,Is.GreaterThan(small.Reach));
        }
        [TestCase(1)][TestCase(77)][TestCase(447301002)]
        public void PopulationIsDeterministicDrySpacedAndOutsideHub(int seed)
        {
            var biome=UnityEditor.AssetDatabase.LoadAssetAtPath<Leeway.World.IslandBiome>("Assets/_Project/Features/World/Data/Biome_Coast.asset");
            var f=IslandHeightField.Build(biome.Shape,seed);var rules=PopulationRule.Defaults();
            var a=IslandPopulationPlan.Build(f,rules);var b=IslandPopulationPlan.Build(f,rules);
            Assert.That(a.Count,Is.InRange(23,32));Assert.That(a.Count,Is.EqualTo(b.Count));
            Assert.That(a.Select(s=>s.Species).Distinct().Count(),Is.EqualTo(9));
            for(int i=0;i<a.Count;i++)
            {
                Assert.That(Vector3.Distance(a[i].Position,b[i].Position),Is.LessThan(0.001f));
                var rule=rules.First(r=>r.Species==a[i].Species);
                Assert.That(IslandPopulationPlan.Suitable(f,a[i].Position,in rule,28),Is.True);
                for(int j=0;j<i;j++)
                {var delta=a[i].Position-a[j].Position;delta.y=0;Assert.That(delta.magnitude,Is.GreaterThanOrEqualTo(2.49f));}
            }
            var coast=new Vector3(f.Settings.HalfSize,0,f.Settings.HalfSize);
            Assert.That(IslandPopulationPlan.CanTraverse(f,f.HubCenter,coast),Is.False,"Animals must not walk into the ocean.");
            Assert.That(IslandPopulationPlan.Build(f,rules,0),Is.Empty);
        }
    }
}
