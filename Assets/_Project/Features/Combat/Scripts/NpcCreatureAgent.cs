using FishNet.Object;
using FishNet.Object.Synchronizing;
using Leeway.Creature.Domain;
using Leeway.CreatureEditor;
using Leeway.World;
using UnityEngine;
namespace Leeway.Combat
{
    [RequireComponent(typeof(CreatureBody))]
    public class NpcCreatureAgent:NetworkBehaviour
    {
        [SerializeField] private NpcCreatureType _type=NpcCreatureType.Predator;
        [SerializeField,Min(1)] private float _wanderRadius=14;
        [SerializeField,Min(0.5f)] private float _wanderSeconds=5;
        [SerializeField,Range(0.3f,1)] private float _engageFraction=0.85f;
        [SerializeField,Min(0)] private float _corpseSeconds=40;
        [SerializeField] private LayerMask _senseMask=~0;
        private readonly SyncVar<IslandSpecies> _species=new();
        private readonly Collider[] _sensed=new Collider[96];
        private CreatureBody _body,_aggressor;
        private CreatureCombatant _combatant;
        private PlaygroundCreatureController _controller;
        private IslandGenerator _island;
        private Vector3 _home,_wanderTarget,_heading;
        private float _nextWander,_nextThink,_deadSince=-1,_provokedUntil,_throttle;
        private bool _configured;
        private System.Random _rng;
        public IslandSpecies Species=>_species.Value;
        public string SpeciesName=>IslandSpeciesLibrary.Name(Species);
        public NpcCreatureType Disposition=>IslandSpeciesLibrary.Behavior(Species);
        public Vector3 Home=>_home;
        public bool IsPredator=>_type==NpcCreatureType.Predator;
        private float Now=>TimeManager!=null?(float)TimeManager.Tick*(float)TimeManager.TickDelta:Time.time;
        private void Awake()
        {_body=GetComponent<CreatureBody>();TryGetComponent(out _combatant);TryGetComponent(out _controller);}
        public void Configure(IslandSpecies species,Vector3 home,float territory,IslandGenerator island)
        {_species.Value=species;_type=IslandSpeciesLibrary.Behavior(species);_home=home;_wanderRadius=territory;_island=island;_configured=true;}
        public override void OnStartServer()
        {
            base.OnStartServer();if(!_configured)_home=transform.position;
            _rng=new System.Random((int)(_home.x*997+_home.z*73)^((int)Species*7919));
            _wanderTarget=_home;_nextWander=0;_nextThink=0;_deadSince=-1;_heading=transform.forward;
            TimeManager.OnTick+=OnServerTick;
        }
        public override void OnStopServer(){base.OnStopServer();if(TimeManager!=null)TimeManager.OnTick-=OnServerTick;}
        [Server] public void RecordHit(CreatureBody attacker){_aggressor=attacker;_provokedUntil=Now+12;}
        private void OnServerTick()
        {
            if(!IsServerInitialized||!isActiveAndEnabled)return;
            if(!_body.IsAlive)
            {
                _controller?.ServerDrive(transform.forward,0);
                if(_deadSince<0)_deadSince=Now;
                if(_corpseSeconds>0&&Now-_deadSince>_corpseSeconds&&NetworkObject.IsSpawned)NetworkObject.Despawn();
                return;
            }
            if(_body.IsKnockedDown){_controller?.ServerDrive(transform.forward,0);return;}
            if(Now>=_nextThink){_nextThink=Now+0.2f;Think();}
            _controller?.ServerDrive(_heading,_throttle);
        }
        private void Think()
        {
            float radius=Mathf.Max(3,_body.Stats.SenseRadius);
            CreatureBody quarry=null,threat=null;float best=float.MaxValue,nearestThreat=float.MaxValue;
            bool provoked=_aggressor&&_aggressor.IsAlive&&Now<_provokedUntil;
            int count=Physics.OverlapSphereNonAlloc(transform.position,radius,_sensed,_senseMask,QueryTriggerInteraction.Ignore);
            for(int i=0;i<count;i++)
            {
                var c=_sensed[i];if(!c)continue;var other=c.GetComponentInParent<CreatureBody>();
                if(!other||other==_body||!other.IsAlive||other.IsInEditorPad)continue;
                other.TryGetComponent(out NpcCreatureAgent npc);
                if(npc&&npc.Species==Species)continue;
                float distance=FlatDistance(transform.position,other.transform.position);
                bool player=npc==null;
                bool danger=(npc&&npc.IsPredator)||(player&&distance<4)||(provoked&&other==_aggressor);
                if(danger&&distance<nearestThreat){threat=other;nearestThreat=distance;}
                bool hunt=_type==NpcCreatureType.Predator&&(player||npc&&npc._type==NpcCreatureType.Prey);
                bool defend=_type==NpcCreatureType.Territorial&&
                    ((provoked&&other==_aggressor)||FlatDistance(other.transform.position,_home)<6);
                if(!(hunt||defend)||distance>=best||!Visible(other))continue;
                quarry=other;best=distance;
            }
            Vector3 goal=WanderTarget();
            bool flee=_type==NpcCreatureType.Prey&&threat||_body.Hp<_body.MaxHp*0.2f&&threat;
            if(flee)
            {var away=transform.position-threat.transform.position;away.y=0;goal=transform.position+away.normalized*8;quarry=null;}
            else if(quarry)goal=quarry.transform.position;
            if(FlatDistance(transform.position,_home)>_wanderRadius*2.2f||_body.IsInEditorPad)
            {goal=_home;quarry=null;}
            var delta=goal-transform.position;delta.y=0;
            _heading=delta.sqrMagnitude>0.01f?delta.normalized:transform.forward;
            _throttle=delta.magnitude<1?0:quarry||flee?1:0.48f;
            if(quarry&&_combatant)
            {
                var ability=BodyAbilityRules.Secondary(_body.Genome,_body.Rules);
                if(ability!=CreatureAbility.Horn&&ability!=CreatureAbility.Claw)ability=BodyAbilityRules.Primary(_body.Genome,_body.Rules);
                var attack=_combatant.AttackFor(ability);
                if(best<=attack.Reach*_engageFraction)
                {_throttle=0.03f;_combatant.ServerUseAbility(ability);}
            }
            if(_island&&_throttle>0.05f)SteerOnLand();
        }
        private bool Visible(CreatureBody other)
        {
            var from=transform.position+Vector3.up*0.15f;var to=other.transform.position+Vector3.up*0.15f;
            if(!Physics.Linecast(from,to,out var hit,1,QueryTriggerInteraction.Ignore))return true;
            return hit.collider.GetComponentInParent<CreatureBody>()==other;
        }
        private void SteerOnLand()
        {
            var f=_island.Field;if(f==null)return;
            var local=_island.WorldToField(transform.position);
            for(int attempt=0;attempt<7;attempt++)
            {
                float angle=attempt==0?0:((attempt+1)/2)*35*(attempt%2==0?-1:1);
                var dir=Quaternion.Euler(0,angle,0)*_heading;
                var end=_island.WorldToField(transform.position+dir*(1.8f+_body.Stats.MoveSpeed*0.35f));
                if(!IslandPopulationPlan.CanTraverse(f,local,end))continue;
                _heading=dir;if(attempt>0)_throttle=Mathf.Min(_throttle,0.5f);return;
            }
            _throttle=0;_nextWander=0;
        }
        private Vector3 WanderTarget()
        {
            if(Now<_nextWander)return _wanderTarget;
            _nextWander=Now+_wanderSeconds*(0.8f+(float)_rng.NextDouble()*0.4f);
            for(int i=0;i<20;i++)
            {
                float a=(float)_rng.NextDouble()*Mathf.PI*2,r=Mathf.Sqrt((float)_rng.NextDouble())*_wanderRadius;
                var p=_home+new Vector3(Mathf.Cos(a)*r,0,Mathf.Sin(a)*r);
                if(_island)
                {
                    var l=_island.WorldToField(p);
                    if(!_island.Field.IsLand(l.x,l.z)||Vector3.Angle(_island.Field.SampleNormal(l.x,l.z),Vector3.up)>30)continue;
                    p.y=_island.GroundHeight(p);
                }
                _wanderTarget=p;return p;
            }
            return _home;
        }
        private static float FlatDistance(Vector3 a,Vector3 b){a.y=b.y=0;return Vector3.Distance(a,b);}
    }
}
