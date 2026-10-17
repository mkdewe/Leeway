using System.Collections.Generic;
using FishNet.Object;
using Leeway.Creature.Domain;
using Leeway.CreatureEditor;
using Leeway.World;
using UnityEngine;
namespace Leeway.Combat
{
    /// <summary>Server-owned, seeded habitat populations. Empty slots restock after a delay.</summary>
    public class NpcCreatureSpawner:NetworkBehaviour
    {
        [Header("Prefabs and data")]
        [SerializeField] private GameObject _enemyPrefab;
        [SerializeField] private CreaturePartCatalog _catalog;
        [SerializeField] private IslandSeed _island;
        [SerializeField] private IslandPopulationProfile _population;
        [Header("Arena fallback")]
        [SerializeField,Min(0)] private int _count=3;
        [SerializeField,Min(1)] private float _spawnRadius=18;
        [SerializeField] private float _spawnHeight=1.5f;
        [SerializeField,Min(0)] private float _keepClear=8;
        [Header("Restocking")]
        [SerializeField,Min(5)] private float _restockSeconds=60;
        private sealed class Slot
        {
            public PopulationSlot Plan;
            public NetworkObject Body;
            public float ReadyAt;
        }
        private readonly List<Slot> _slots=new();
        private int _seed;
        private float _nextSpawn;
        private PartRuleSet _rules;
        public int PlannedCount=>_slots.Count;
        public int LivingCount
        {get{int n=0;foreach(var slot in _slots)if(slot.Body&&slot.Body.IsSpawned&&slot.Body.GetComponent<CreatureBody>().IsAlive)n++;return n;}}
        public override void OnStartServer()
        {
            base.OnStartServer();_slots.Clear();
            _rules=_catalog?_catalog.BuildRuleSet():PartRuleSet.Empty;
            if(_island)
            {
                _island.ServerEnsureGenerated();_seed=_island.Generator.Field.Seed;
                var plan=IslandPopulationPlan.Build(_island.Generator.Field,
                    _population?_population.Species:PopulationRule.Defaults(),
                    _population?_population.PopulationCap:24,_population?_population.SafeHubRadius:28);
                foreach(var p in plan)_slots.Add(new Slot{Plan=p});
            }
            else
            {
                _seed=20261017;
                for(int i=0;i<_count;i++)
                {
                    float a=i*2.399963f,r=Mathf.Max(_keepClear,_spawnRadius*(0.6f+0.4f*Mathf.Repeat(i*0.37f,1)));
                    var p=transform.position+new Vector3(Mathf.Cos(a)*r,0,Mathf.Sin(a)*r);
                    if(Physics.Raycast(p+Vector3.up*100,Vector3.down,out var hit,200,1,QueryTriggerInteraction.Ignore))p.y=hit.point.y;
                    _slots.Add(new Slot{Plan=new PopulationSlot(IslandSpecies.RustSnapper,p,p,14,i+1,i)});
                }
            }
            _nextSpawn=Time.time+0.5f;
        }
        public override void OnStopServer(){base.OnStopServer();_slots.Clear();_rules=null;}
        private void Update()
        {
            if(!IsServerInitialized||Time.time<_nextSpawn)return;
            _nextSpawn=Time.time+0.25f;
            if(_island&&_island.Generator.Field.Seed!=_seed)return;
            foreach(var slot in _slots)
            {
                if(slot.Body&&slot.Body.IsSpawned)continue;
                if(slot.ReadyAt<0){slot.ReadyAt=Time.time+_restockSeconds;continue;}
                if(Time.time<slot.ReadyAt)continue;
                SpawnOne(slot);break;
            }
        }
        [Server] private void SpawnOne(Slot slot)
        {
            if(!_enemyPrefab||_rules==null)return;
            var p=slot.Plan;var genome=IslandSpeciesLibrary.Create(p.Species,_rules,p.Variant);
            var valid=GenomeValidator.Validate(genome,_rules);
            if(!valid.IsValid){slot.ReadyAt=Time.time+60;Debug.LogError("Wildlife genome rejected: "+p.Species+" "+valid.Error,this);return;}
            Vector3 position=_island?_island.Generator.FieldToWorld(p.Position):p.Position;
            Vector3 home=_island?_island.Generator.FieldToWorld(p.Home):p.Home;
            float stand=GenomeStatRules.StandHeight(genome,_rules);
            var instance=Instantiate(_enemyPrefab,position+Vector3.up*Mathf.Max(stand+0.12f,0.6f),Quaternion.Euler(0,p.Variant%360,0));
            instance.name=IslandSpeciesLibrary.Name(p.Species)+" ["+p.Group+"]";
            var nob=instance.GetComponent<NetworkObject>();var body=instance.GetComponent<CreatureBody>();
            if(!nob||!body){Destroy(instance);slot.ReadyAt=Time.time+60;return;}
            var agent=instance.GetComponent<NpcCreatureAgent>();
            if(agent)agent.Configure(p.Species,home,p.TerritoryRadius,_island?_island.Generator:null);
            var fighter=instance.GetComponent<CreatureCombatant>();if(fighter)fighter.ConfigureTeam((byte)(2+(int)p.Species));
            ServerManager.Spawn(nob);
            if(!body.ServerSetGenome(genome,out var error))
            {Debug.LogError("Wildlife spawn rejected: "+error,this);nob.Despawn();slot.ReadyAt=Time.time+60;return;}
            slot.Body=nob;slot.ReadyAt=-1;
        }
    }
}
