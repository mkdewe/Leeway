using UnityEngine;
namespace Leeway.Creature.Domain
{
    public enum IslandSpecies : byte { MossGrazer, ReedRunner, RustSnapper, Rockhorn, BrookGatherer, ForestLongneck, SandCrawler, SickleStalker, DuneRam }
    public static class IslandSpeciesLibrary
    {
        public static string Name(IslandSpecies species)=>species switch {
            IslandSpecies.MossGrazer=>"Mchoryjek",IslandSpecies.ReedRunner=>"Biegus trzcinowy",
            IslandSpecies.RustSnapper=>"Zębacz rdzawy",IslandSpecies.Rockhorn=>"Rogacz skalny",
            IslandSpecies.ForestLongneck=>"Długoszyj leśny",IslandSpecies.SandCrawler=>"Ośmionóg plażowy",
            IslandSpecies.SickleStalker=>"Sierporęki",IslandSpecies.DuneRam=>"Taraniec wydmowy",
            _=>"Chwytacz potokowy"};
        public static string Notes(IslandSpecies species)=>species switch {
            IslandSpecies.MossGrazer=>"Niski, zielony roślinożerca na czterech kopytnych nogach. Żyje w grupach na łąkach; ucieka przed zagrożeniem.",
            IslandSpecies.ReedRunner=>"Smukły dwunóg na ptasich nogach. Jasne oczy i drobna paszcza; płochliwe grupy na niskich wybrzeżach.",
            IslandSpecies.RustSnapper=>"Rdzawy łowca na czterech łapach, z dużą paszczą i długim ogonem. Poluje na mniejsze gatunki; ranny wycofuje się.",
            IslandSpecies.Rockhorn=>"Krępy obrońca wyżyn, z szerokim rozstawem nóg i rogiem. Ostrzega przed atakiem i nie ściga poza terytorium.",
            IslandSpecies.ForestLongneck=>"Wysoka głowa na długiej szyi, cztery kopytne nogi. Spokojny roślinożerca wyższych łąk.",
            IslandSpecies.SandCrawler=>"Długi, niski tułów na ośmiu krótkich nogach. Małe grupy na suchych plażach; niezależny rytm par nóg.",
            IslandSpecies.SickleStalker=>"Dwunożny drapieżnik o dużych chwytnych ramionach. Walczy szerokim zamachem dłoni.",
            IslandSpecies.DuneRam=>"Szeroki tułów, krótkie nogi i para przednich rogów. Terytorialny mieszkaniec suchych nizin.",
            _=>"Niebieski zbieracz z chwytnymi ramionami i dłońmi ośmiornicy. Żyje przy potokach; ramiona pozwalają pomagać i żerować."};
        public static NpcCreatureType Behavior(IslandSpecies species)=>(species==IslandSpecies.RustSnapper||species==IslandSpecies.SickleStalker)?
            NpcCreatureType.Predator:(species==IslandSpecies.Rockhorn||species==IslandSpecies.DuneRam)?NpcCreatureType.Territorial:NpcCreatureType.Prey;
        public static CreatureGenome Create(IslandSpecies species,PartRuleSet rules,int variant=0)
        {
            if(species>=IslandSpecies.ForestLongneck)
            {var extra=CreateExtended(species,rules);Vary(extra,variant);return extra;}
            if(species==IslandSpecies.BrookGatherer)
            {
                var upright=ShowcaseGenomes.Humanoid(rules);
                upright.PrimaryColor=new Color32(91,153,157,255);upright.SecondaryColor=new Color32(48,87,111,255);
                upright.BodyPattern=5;
                for(int i=0;i<upright.PartCount;i++)
                {
                    var gene=upright.GetPart(i);
                    if(rules.TryGetRule(gene.PartId,out var rule)&&rule.Category==PartCategory.Grasper)
                        upright.SetPart(i,gene.WithFitting(StableHash.PartId("limb.octopus")));
                }
                Vary(upright,variant);return upright;
            }
            var g=new CreatureGenome();
            if(species==IslandSpecies.RustSnapper)g=EnemyGenomeLibrary.Snapper(rules);
            else
            {
                bool reed=species==IslandSpecies.ReedRunner, horn=species==IslandSpecies.Rockhorn;
                float[] radii=reed?new[]{0.20f,0.29f,0.16f}:horn?
                    new[]{0.36f,0.48f,0.43f,0.32f,0.15f}:new[]{0.24f,0.35f,0.32f,0.17f};
                for(int i=0;i<radii.Length;i++)
                    g.AddVertebra(new VertebraGene(i==0?Vector3.zero:
                        new Vector3(0,reed?(i==1?-0.12f:0):0,-0.42f),Quaternion.identity,radii[i]));
                g.PrimaryColor=reed?new Color32(200,177,111,255):horn?new Color32(121,132,145,255):new Color32(130,167,112,255);
                g.SecondaryColor=reed?new Color32(83,115,108,255):horn?new Color32(68,81,95,255):new Color32(57,95,70,255);
                g.BodyPattern=(byte)(horn?4:reed?2:5);
                Attach(g,rules,"mouth.maw",0,new Vector3(0,-0.1f,1),false,reed?0.45f:horn?0.7f:0.5f);
                Attach(g,rules,"sense.eye",0,new Vector3(0.65f,0.45f,0.7f),true,reed?1.15f:0.85f);
                Attach(g,rules,reed?"loco.runner_legs":"loco.hoof_legs",1,new Vector3(0.8f,-1,0),true,1,
                    reed?"limb.paw":"limb.cloven_hoof");
                if(!reed)Attach(g,rules,"loco.hoof_legs",2,new Vector3(0.8f,-1,0),true,1,"limb.hoof");
                if(horn)Attach(g,rules,"weapon.horn",0,new Vector3(0,0.5f,1),false,1.15f);
            }
            Vary(g,variant);return g;
        }
        private static CreatureGenome CreateExtended(IslandSpecies species,PartRuleSet rules)
        {
            var g=new CreatureGenome();float[] radii;Vector3[] offsets=null;
            switch(species)
            {
                case IslandSpecies.ForestLongneck:
                    radii=new[]{0.20f,0.13f,0.15f,0.38f,0.32f,0.10f};
                    offsets=new[]{Vector3.zero,new Vector3(0,-0.30f,-0.05f),new Vector3(0,-0.32f,-0.10f),
                        new Vector3(0,-0.18f,-0.18f),new Vector3(0,0,-0.42f),new Vector3(0,0,-0.42f)};
                    g.PrimaryColor=new Color32(168,187,128,255);g.SecondaryColor=new Color32(80,111,90,255);g.BodyPattern=4;break;
                case IslandSpecies.SandCrawler:
                    radii=new[]{0.17f,0.22f,0.25f,0.25f,0.23f,0.17f,0.09f};
                    g.PrimaryColor=new Color32(193,155,109,255);g.SecondaryColor=new Color32(103,88,73,255);g.BodyPattern=2;break;
                case IslandSpecies.SickleStalker:
                    radii=new[]{0.25f,0.32f,0.28f,0.12f};
                    offsets=new[]{Vector3.zero,new Vector3(0,-0.26f,-0.10f),new Vector3(0,-0.22f,-0.28f),new Vector3(0,0,-0.4f)};
                    g.PrimaryColor=new Color32(133,118,159,255);g.SecondaryColor=new Color32(66,55,89,255);g.BodyPattern=5;break;
                default:
                    radii=new[]{0.32f,0.52f,0.50f,0.30f,0.10f};
                    g.PrimaryColor=new Color32(184,125,82,255);g.SecondaryColor=new Color32(102,69,51,255);g.BodyPattern=4;break;
            }
            for(int i=0;i<radii.Length;i++)
                g.AddVertebra(new VertebraGene(offsets!=null?offsets[i]:i==0?Vector3.zero:
                    new Vector3(0,0,species==IslandSpecies.SandCrawler?-0.27f:-0.4f),Quaternion.identity,radii[i]));
            Attach(g,rules,"mouth.maw",0,new Vector3(0,-0.15f,1),false,species==IslandSpecies.SickleStalker?0.8f:0.55f);
            Attach(g,rules,"sense.eye",0,new Vector3(0.65f,0.45f,0.7f),true,0.9f);
            if(species==IslandSpecies.ForestLongneck)
            {
                Attach(g,rules,"loco.hoof_legs",3,new Vector3(0.8f,-1,0),true,1,"limb.cloven_hoof");
                Attach(g,rules,"loco.hoof_legs",4,new Vector3(0.8f,-1,0),true,1,"limb.cloven_hoof");
            }
            else if(species==IslandSpecies.SandCrawler)
            {
                for(int i=1;i<=4;i++)Attach(g,rules,"loco.stub_legs",i,new Vector3(1,-0.8f,0),true,1,"limb.paw");
                for(int i=0;i<g.PartCount;i++)
                {var p=g.GetPart(i);if(rules.TryGetRule(p.PartId,out var r)&&r.Category==PartCategory.Locomotion)
                    g.SetPart(i,p.WithLeg(new LegSpec(1,0.14f)));}
            }
            else if(species==IslandSpecies.SickleStalker)
            {
                Attach(g,rules,"loco.runner_legs",2,new Vector3(0.8f,-1,0),true,1,"limb.paw");
                Attach(g,rules,"grasp.claw_arm",1,new Vector3(1,-0.15f,0.3f),true,1.7f,"limb.fingers");
            }
            else
            {
                Attach(g,rules,"loco.stub_legs",1,new Vector3(0.9f,-1,0),true,1,"limb.cloven_hoof");
                Attach(g,rules,"loco.stub_legs",2,new Vector3(0.9f,-1,0),true,1,"limb.cloven_hoof");
                Attach(g,rules,"weapon.horn",0,new Vector3(0.5f,0.35f,1),true,1.6f);
                for(int i=0;i<g.PartCount;i++)
                {var p=g.GetPart(i);if(rules.TryGetRule(p.PartId,out var r)&&r.Category==PartCategory.Locomotion)
                    g.SetPart(i,p.WithLeg(new LegSpec(1,0.18f)));}
            }
            return g;
        }
        private static void Vary(CreatureGenome g,int variant)
        {
            if(variant==0)return;
            var rng=new System.Random(variant);
            float scale=0.92f+(float)rng.NextDouble()*0.16f;
            for(int i=0;i<g.VertebraCount;i++)
            {var v=g.GetVertebra(i);g.SetVertebra(i,new VertebraGene(v.LocalOffset,v.LocalRotation,v.Radius*scale));}
            Color c=g.PrimaryColor;c*=0.94f+(float)rng.NextDouble()*0.12f;c.a=1;g.PrimaryColor=c;
        }
        private static void Attach(CreatureGenome g,PartRuleSet rules,string key,int bone,Vector3 direction,bool mirror,float scale=1,string fitting=null)
        {
            int id=StableHash.PartId(key);
            if(rules==null||!rules.TryGetRule(id,out var rule))return;
            int foot=rule.AcceptsFitting?(fitting==null?rule.DefaultFittingId:StableHash.PartId(fitting)):0;
            var gene=new PartGene(id,(byte)bone,direction,Quaternion.identity,scale,mirror&&rule.MirrorCapable,
                false,default,default,0,foot);
            GenomeEditOperations.TryAttachPart(g,gene,rules,out _);
        }
    }
}
