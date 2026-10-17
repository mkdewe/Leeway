using System;
using System.Collections.Generic;
using UnityEngine;
using Leeway.World.Domain;
namespace Leeway.Creature.Domain
{
    [Serializable]
    public struct PopulationRule
    {
        [SerializeField] private IslandSpecies _species;
        [SerializeField,Min(0)] private int _groups;
        [SerializeField,Min(1)] private int _groupSize;
        [SerializeField] private Vector2 _altitude;
        [SerializeField,Range(1,45)] private float _maxSlope;
        [SerializeField] private bool _nearFreshWater;
        [SerializeField,Min(1)] private float _territoryRadius;
        public IslandSpecies Species=>_species;
        public int Groups=>Mathf.Clamp(_groups,0,8);
        public int GroupSize=>Mathf.Clamp(_groupSize,1,8);
        public Vector2 Altitude=>_altitude;
        public float MaxSlope=>_maxSlope;
        public bool NearFreshWater=>_nearFreshWater;
        public float TerritoryRadius=>Mathf.Max(4,_territoryRadius);
        public PopulationRule(IslandSpecies species,int groups,int size,float low,float high,float slope,bool fresh,float territory)
        {_species=species;_groups=groups;_groupSize=size;_altitude=new Vector2(low,high);_maxSlope=slope;_nearFreshWater=fresh;_territoryRadius=territory;}
        public static PopulationRule[] Defaults()=>new[]{
            new PopulationRule(IslandSpecies.MossGrazer,2,3,1.5f,23,24,false,12),
            new PopulationRule(IslandSpecies.ReedRunner,2,3,0.8f,13,22,false,10),
            new PopulationRule(IslandSpecies.RustSnapper,2,2,4,35,27,false,16),
            new PopulationRule(IslandSpecies.Rockhorn,1,2,15,75,30,false,12),
            new PopulationRule(IslandSpecies.BrookGatherer,1,3,1.5f,40,26,true,10),
            new PopulationRule(IslandSpecies.ForestLongneck,1,2,6,42,23,false,13),
            new PopulationRule(IslandSpecies.SandCrawler,1,3,0.8f,18,24,false,8),
            new PopulationRule(IslandSpecies.SickleStalker,1,2,6,45,28,false,14),
            new PopulationRule(IslandSpecies.DuneRam,1,2,2,22,25,false,10)};
    }
    public readonly struct PopulationSlot
    {
        public readonly IslandSpecies Species;
        public readonly Vector3 Position,Home;
        public readonly float TerritoryRadius;
        public readonly int Variant,Group;
        public PopulationSlot(IslandSpecies species,Vector3 pos,Vector3 home,float radius,int variant,int group)
        {Species=species;Position=pos;Home=home;TerritoryRadius=radius;Variant=variant;Group=group;}
    }
    public static class IslandPopulationPlan
    {
        public static bool CanTraverse(IslandHeightField f,Vector3 start,Vector3 end)
        {
            float previous=f.SampleHeight(start.x,start.z);
            for(int i=1;i<=5;i++)
            {
                var p=Vector3.Lerp(start,end,i/5f);float height=f.SampleHeight(p.x,p.z);
                if(!f.IsLand(p.x,p.z)||f.IsInsideCave(p.x,p.z)||Mathf.Abs(height-previous)>0.6f||
                    Vector3.Angle(f.SampleNormal(p.x,p.z),Vector3.up)>34)return false;
                previous=height;
            }
            return true;
        }
        public static bool Suitable(IslandHeightField f,Vector3 p,in PopulationRule r,float safeRadius)
        {
            float h=f.SampleHeight(p.x,p.z),altitude=h-f.Settings.SeaLevel;
            if(altitude<r.Altitude.x||altitude>r.Altitude.y||!f.IsLand(p.x,p.z)||f.IsInsideCave(p.x,p.z,2))return false;
            var d=p-f.HubCenter;d.y=0;if(d.sqrMagnitude<safeRadius*safeRadius)return false;
            if(Vector3.Angle(f.SampleNormal(p.x,p.z),Vector3.up)>r.MaxSlope)return false;
            // Keep the whole body on dry, continuous ground, away from cliff lips.
            for(int i=0;i<8;i++)
            {
                float a=i*Mathf.PI/4;float x=p.x+Mathf.Cos(a)*1.5f,z=p.z+Mathf.Sin(a)*1.5f;
                if(!f.IsLand(x,z)||Mathf.Abs(f.SampleHeight(x,z)-h)>1.1f)return false;
            }
            if(r.NearFreshWater)
            {
                bool near=false;
                foreach(var lake in f.Lakes)
                {var delta=p-lake.Center;delta.y=0;if(delta.magnitude<lake.Radius+16){near=true;break;}}
                if(!near)foreach(var river in f.Rivers)
                {foreach(var point in river.Points){var delta=p-point;delta.y=0;if(delta.sqrMagnitude<256){near=true;break;}}if(near)break;}
                if(!near)return false;
            }
            return true;
        }
        public static List<PopulationSlot> Build(IslandHeightField f,IReadOnlyList<PopulationRule> rules,int cap=32,float safeRadius=28)
        {
            var output=new List<PopulationSlot>();if(f==null||rules==null)return output;
            int group=0;
            foreach(var rule in rules)
            {
                var rng=new System.Random(unchecked(f.Seed*397+(int)rule.Species*7919+127));
                for(int g=0;g<rule.Groups&&output.Count<cap;g++,group++)
                {
                    Vector3 home=default;bool found=false;
                    for(int attempt=0;attempt<2500;attempt++)
                    {
                        float x=((float)rng.NextDouble()-0.5f)*f.Settings.Size*0.88f;
                        float z=((float)rng.NextDouble()-0.5f)*f.Settings.Size*0.88f;
                        var p=new Vector3(x,f.SampleHeight(x,z),z);
                        if(!Suitable(f,p,in rule,safeRadius))continue;
                        bool close=false;foreach(var slot in output)
                        {var d=slot.Home-p;d.y=0;if(d.sqrMagnitude<28*28){close=true;break;}}
                        if(close)continue;home=p;found=true;break;
                    }
                    if(!found)continue;
                    for(int j=0;j<rule.GroupSize&&output.Count<cap;j++)
                    {
                        Vector3 p=home;bool member=j==0;
                        for(int attempt=0;!member&&attempt<100;attempt++)
                        {
                            float a=(float)rng.NextDouble()*Mathf.PI*2,dist=2+(float)rng.NextDouble()*5;
                            p=home+new Vector3(Mathf.Cos(a)*dist,0,Mathf.Sin(a)*dist);p.y=f.SampleHeight(p.x,p.z);
                            if(!Suitable(f,p,in rule,safeRadius))continue;
                            member=true;foreach(var other in output)
                            {var d=other.Position-p;d.y=0;if(d.sqrMagnitude<2.5f*2.5f){member=false;break;}}
                        }
                        if(member)output.Add(new PopulationSlot(rule.Species,p,home,rule.TerritoryRadius,rng.Next(1,int.MaxValue),group));
                    }
                }
            }
            return output;
        }
    }
}
