using UnityEngine;
namespace Leeway.Creature.Domain
{
    public enum CreatureAbility : byte { BodyStrike, Bite, Claw, Horn, Kick, Scan, Forage }
    public static class BodyAbilityRules
    {
        public static string Name(CreatureAbility a)=>a switch {
            CreatureAbility.Bite=>"Ugryzienie",CreatureAbility.Claw=>"Zamach ramion",
            CreatureAbility.Horn=>"Pchnięcie rogiem",CreatureAbility.Kick=>"Kopnięcie",CreatureAbility.Scan=>"Czujność",
            CreatureAbility.Forage=>"Pomoc / żerowanie",_=>"Uderzenie ciałem"};
        private static bool Matches(PartRule r,CreatureAbility a)=>a switch {
            CreatureAbility.Bite=>r.Category==PartCategory.Mouth,
            CreatureAbility.Claw=>r.Category==PartCategory.Grasper,
            CreatureAbility.Horn=>r.PartId==StableHash.PartId("weapon.horn"),
            CreatureAbility.Kick=>r.Category==PartCategory.Locomotion,
            CreatureAbility.Scan=>r.Category==PartCategory.Sense,
            CreatureAbility.Forage=>r.Category==PartCategory.Mouth||r.Category==PartCategory.Grasper,
            _=>false};
        public static bool Has(CreatureGenome g,PartRuleSet rules,CreatureAbility a)
        {
            if(a==CreatureAbility.BodyStrike)return g!=null;
            if(g==null||rules==null)return false;
            for(int i=0;i<g.PartCount;i++)if(rules.TryGetRule(g.GetPart(i).PartId,out var r)&&Matches(r,a))return true;
            return false;
        }
        public static CreatureAbility Primary(CreatureGenome g,PartRuleSet r)=>
            Has(g,r,CreatureAbility.Bite)?CreatureAbility.Bite:
            Has(g,r,CreatureAbility.Claw)?CreatureAbility.Claw:
            Has(g,r,CreatureAbility.Horn)?CreatureAbility.Horn:CreatureAbility.BodyStrike;
        public static CreatureAbility Secondary(CreatureGenome g,PartRuleSet r)=>
            Has(g,r,CreatureAbility.Horn)?CreatureAbility.Horn:
            Has(g,r,CreatureAbility.Claw)?CreatureAbility.Claw:Primary(g,r);
        public static float Windup(CreatureAbility a)=>AttackTimeline.For(a).Windup;
        public static AttackSpec Attack(CreatureGenome g,PartRuleSet rules,in CreatureStats stats,
            CreatureAbility ability,in CombatTuning tuning)
        {
            if(ability==CreatureAbility.BodyStrike)return CombatRules.Derive(in stats,tuning.UnarmedReach,false,in tuning);
            float reach=0,bonus=0;
            if(g!=null&&rules!=null)
            {
                var bones=SpineAnchor.BoneToRoot(g);
                for(int i=0;i<g.PartCount;i++)
                {
                    var gene=g.GetPart(i);
                    if(!rules.TryGetRule(gene.PartId,out var rule)||!Matches(rule,ability)||gene.BoneIndex>=bones.Length)continue;
                    var pose=PartPlacement.Resolve(g,gene,rule.SkinOffset);
                    var pos=bones[gene.BoneIndex].MultiplyPoint3x4(pose.Position);
                    float extra=ability==CreatureAbility.Claw||ability==CreatureAbility.Kick?rule.Leg.Reach*gene.Scale:0;
                    reach=Mathf.Max(reach,pos.z+extra+0.2f*gene.Scale);
                    float damage=rule.Stats.DamageBonus;
                    if(ability==CreatureAbility.Claw&&gene.HasFitting&&rules.TryGetRule(gene.FittingId,out var fitting))
                        damage+=fitting.Stats.DamageBonus;
                    bonus=Mathf.Max(bonus,damage*Mathf.Sqrt(gene.Scale));
                }
            }
            float factor=ability==CreatureAbility.Horn?1.35f:1;
            float damageValue=(tuning.UnarmedFactor*5+bonus)*factor;
            float cooldown=CombatRules.Cooldown(stats.Mass,in tuning)*(ability==CreatureAbility.Horn?1.65f:ability==CreatureAbility.Claw?0.9f:1);
            return new AttackSpec(damageValue,Mathf.Max(0.5f,reach+tuning.ReachPadding),
                ability==CreatureAbility.Claw?145:ability==CreatureAbility.Horn?55:ability==CreatureAbility.Kick?80:100,
                cooldown,damageValue*tuning.ImpulsePerDamage*(ability==CreatureAbility.Horn?1.5f:ability==CreatureAbility.Kick?0.7f:0.35f),true);
        }
    }
}
