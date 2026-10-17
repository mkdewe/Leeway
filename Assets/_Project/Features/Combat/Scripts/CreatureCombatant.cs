using FishNet.Object;
using FishNet.Object.Synchronizing;
using Leeway.Creature.Domain;
using Leeway.CreatureEditor;
using MessagePipe;
using UnityEngine;
namespace Leeway.Combat
{
    [RequireComponent(typeof(CreatureBody))]
    public class CreatureCombatant:NetworkBehaviour
    {
        public const byte PlayerTeam=0,WildTeam=1;
        [SerializeField] private CombatTuning _tuning=CombatTuning.Default;
        [SerializeField] private byte _team=PlayerTeam;
        [SerializeField] private LayerMask _hitMask=~0;
        [SerializeField,Min(0)] private float _forgiveness=0.2f;
        private readonly Collider[] _overlap=new Collider[96];
        private readonly SyncVar<float> _readyAt=new(),_impactAt=new(),_scanReady=new(),_forageReady=new();
        private readonly SyncVar<bool> _winding=new();
        private readonly SyncVar<CreatureAbility> _action=new();
        private CreatureBody _body;
        private AttackSpec _spec,_pendingSpec;
        private Vector3 _attackHeading;
        private bool _harvested;
        private float _scanUntil,_noticeUntil;
        private string _notice;
        public AttackSpec Spec=>_spec;
        public byte Team=>_team;
        public CreatureAbility PendingAbility=>_action.Value;
        public bool IsWindingUp=>_winding.Value;
        public float WindupProgress=>1-Mathf.Clamp01((_impactAt.Value-Now)/BodyAbilityRules.Windup(PendingAbility));
        public float CooldownRemaining=>Mathf.Max(0,_readyAt.Value-Now);
        public float ScanCooldown=>Mathf.Max(0,_scanReady.Value-Now);
        public float ForageCooldown=>Mathf.Max(0,_forageReady.Value-Now);
        public bool ScanActive=>Time.time<_scanUntil;
        public string Notice=>Time.time<_noticeUntil?_notice:string.Empty;
        private float Now=>TimeManager!=null?(float)TimeManager.Tick*(float)TimeManager.TickDelta:Time.time;
        public void ConfigureTeam(byte team){_team=team;}
        private void Awake()
        {_body=GetComponent<CreatureBody>();_body.BodyRebuilt+=OnBodyRebuilt;OnBodyRebuilt(_body.Genome,_body.Stats);}
        private void OnDestroy(){if(_body)_body.BodyRebuilt-=OnBodyRebuilt;}
        private void OnBodyRebuilt(CreatureGenome genome,CreatureStats stats)
        {_spec=BodyAbilityRules.Attack(genome,_body.Rules,in stats,BodyAbilityRules.Primary(genome,_body.Rules),in _tuning);
         if(IsServerInitialized)_winding.Value=false;}
        public AttackSpec AttackFor(CreatureAbility ability)
        {var stats=_body.Stats;return BodyAbilityRules.Attack(_body.Genome,_body.Rules,in stats,ability,in _tuning);}
        public void RequestAttack()=>RequestAbility(BodyAbilityRules.Primary(_body.Genome,_body.Rules));
        public void RequestAbility(CreatureAbility ability)
        {
            if(!IsOwner||!_body.IsAlive||_body.IsKnockedDown||_body.IsInEditorPad)return;
            if(!BodyAbilityRules.Has(_body.Genome,_body.Rules,ability))return;
            if(ability<=CreatureAbility.Horn&&CooldownRemaining>0)return;
            if(ability==CreatureAbility.Scan&&ScanCooldown>0||ability==CreatureAbility.Forage&&ForageCooldown>0)return;
            CmdAbility(ability);
        }
        [ServerRpc(RequireOwnership=true)] private void CmdAbility(CreatureAbility ability)=>ServerUseAbility(ability);
        [Server] public bool ServerAttack()=>ServerUseAbility(BodyAbilityRules.Primary(_body.Genome,_body.Rules));
        [Server] public bool ServerUseAbility(CreatureAbility ability)
        {
            if((byte)ability>(byte)CreatureAbility.Forage||!_body.IsAlive||_body.IsKnockedDown||
                _body.IsInEditorPad||_winding.Value||!BodyAbilityRules.Has(_body.Genome,_body.Rules,ability))return false;
            if(ability==CreatureAbility.Scan)return Scan();
            if(ability==CreatureAbility.Forage)return Forage();
            if(Now<_readyAt.Value)return false;
            _pendingSpec=AttackFor(ability);_action.Value=ability;
            _attackHeading=transform.forward;
            _impactAt.Value=Now+BodyAbilityRules.Windup(ability);
            _readyAt.Value=Now+_pendingSpec.Cooldown;_winding.Value=true;
            return true;
        }
        private void Update()
        {
            if(!IsServerInitialized||!_winding.Value)return;
            if(!_body.IsAlive||_body.IsKnockedDown||_body.IsInEditorPad){_winding.Value=false;return;}
            if(Now<_impactAt.Value)return;
            _winding.Value=false;ResolveAttack();
        }
        private void ResolveAttack()
        {
            Vector3 origin=transform.position;
            var target=FindTarget(origin,_attackHeading,in _pendingSpec,out var point);
            if(!target){RpcStruck(null,0,origin,false);return;}
            Vector3 impulse=CombatRules.Knockback(origin,target.transform.position,in _pendingSpec,in _tuning);
            bool toppled=target.Knockdown(impulse,NearestBone(target,point));
            target.TakeDamage(_pendingSpec.Damage);
            if(target.TryGetComponent(out NpcCreatureAgent npc))npc.RecordHit(_body);
            RpcStruck(target,_pendingSpec.Damage,point,toppled);
            if(!target.IsAlive)RpcDied(target);
        }
        private CreatureBody FindTarget(Vector3 origin,Vector3 forward,in AttackSpec attack,out Vector3 point)
        {
            point=origin;float reach=attack.Reach+_forgiveness;
            int count=Physics.OverlapSphereNonAlloc(origin,reach,_overlap,_hitMask,QueryTriggerInteraction.Ignore);
            CreatureBody best=null;float distance=float.MaxValue;
            for(int i=0;i<count;i++)
            {
                var c=_overlap[i];if(!c)continue;var candidate=c.GetComponentInParent<CreatureBody>();
                if(!candidate||candidate==_body||candidate.IsInEditorPad||SameTeam(candidate))continue;
                Vector3 p=c.ClosestPoint(origin);
                var spec=new AttackSpec(attack.Damage,reach,attack.ArcDegrees,attack.Cooldown,attack.Impulse,attack.Armed);
                if(!CombatRules.CanStrike(_body.IsAlive,_body.IsKnockedDown,candidate.IsAlive,origin,forward,p,in spec))continue;
                if(!ClearTo(candidate))continue;
                float d=(p-origin).sqrMagnitude;if(d>=distance)continue;
                best=candidate;point=p;distance=d;
            }
            return best;
        }
        private bool ClearTo(CreatureBody target)
        {
            var origin=transform.position+Vector3.up*0.1f;
            if(!Physics.Linecast(origin,target.transform.position+Vector3.up*0.1f,out var hit,1,QueryTriggerInteraction.Ignore))return true;
            return hit.collider.GetComponentInParent<CreatureBody>()==target;
        }
        private bool SameTeam(CreatureBody other)=>other.TryGetComponent(out CreatureCombatant c)&&c._team==_team;
        private bool Scan()
        {
            if(Now<_scanReady.Value)return false;_scanReady.Value=Now+8;
            int count=Physics.OverlapSphereNonAlloc(transform.position,_body.Stats.SenseRadius*2,_overlap,_hitMask,QueryTriggerInteraction.Ignore);
            var seen=new System.Collections.Generic.HashSet<CreatureBody>();
            for(int i=0;i<count;i++)
            {var b=_overlap[i]?_overlap[i].GetComponentInParent<CreatureBody>():null;if(b&&b!=_body&&b.IsAlive)seen.Add(b);}
            RpcUtility(CreatureAbility.Scan,seen.Count);return true;
        }
        private bool Forage()
        {
            if(Now<_forageReady.Value)return false;
            int count=Physics.OverlapSphereNonAlloc(transform.position,2.5f,_overlap,_hitMask,QueryTriggerInteraction.Ignore);
            CreatureBody best=null;float nearest=float.MaxValue;bool assist=false;
            bool arms=BodyAbilityRules.Has(_body.Genome,_body.Rules,CreatureAbility.Claw);
            for(int i=0;i<count;i++)
            {
                var c=_overlap[i];if(!c)continue;var b=c.GetComponentInParent<CreatureBody>();
                if(!b||b==_body||b.IsInEditorPad||!ClearTo(b))continue;
                bool help=arms&&b.IsAlive&&b.IsKnockedDown&&SameTeam(b);
                bool eat=!b.IsAlive&&b.TryGetComponent(out NpcCreatureAgent _)&&
                    b.TryGetComponent(out CreatureCombatant fighter)&&!fighter._harvested&&_body.Hp<_body.MaxHp;
                float d=(b.transform.position-transform.position).sqrMagnitude;
                if(!(help||eat)||d>=nearest)continue;nearest=d;best=b;assist=help;
            }
            if(!best){_forageReady.Value=Now+0.6f;RpcUtility(CreatureAbility.Forage,0);return false;}
            _forageReady.Value=Now+6;
            if(assist){best.Recover();RpcUtility(CreatureAbility.Forage,-1);return true;}
            var corpse=best.GetComponent<CreatureCombatant>();corpse._harvested=true;
            float amount=Mathf.Min(_body.MaxHp-_body.Hp,_body.MaxHp*0.22f);_body.Heal(amount);
            RpcUtility(CreatureAbility.Forage,amount);
            if(best.NetworkObject.IsSpawned)best.NetworkObject.Despawn();
            return true;
        }
        private static int NearestBone(CreatureBody target,Vector3 point)
        {
            var bones=target.Built?.Bones;if(bones==null)return 0;int index=0;float best=float.MaxValue;
            for(int i=0;i<bones.Length;i++)if(bones[i])
            {float d=(bones[i].position-point).sqrMagnitude;if(d<best){best=d;index=i;}}
            return index;
        }
        [ObserversRpc(RunLocally=true)]
        private void RpcStruck(CreatureBody target,float damage,Vector3 point,bool down)
        {
            if(target&&target.TryGetComponent(out CreatureCombatPresentation visual))visual.Flash();
            if(GlobalMessagePipe.IsInitialized)GlobalMessagePipe.GetPublisher<CreatureStruckMessage>()
                .Publish(new CreatureStruckMessage(_body,target,damage,point,down));
        }
        [ObserversRpc(RunLocally=true)] private void RpcDied(CreatureBody target)
        {if(GlobalMessagePipe.IsInitialized&&target)GlobalMessagePipe.GetPublisher<CreatureDiedMessage>().Publish(new CreatureDiedMessage(target,_body));}
        [ObserversRpc(RunLocally=true)] private void RpcUtility(CreatureAbility ability,float result)
        {
            if(!IsOwner)return;_noticeUntil=Time.time+3;
            if(ability==CreatureAbility.Scan){_scanUntil=Time.time+5;_notice="Wykryto stworzeń: "+result.ToString("0");}
            else _notice=result<0?"Pomoc: sojusznik podniesiony":result>0?"Żerowanie: +"+result.ToString("0")+" HP":"Podejdź do rannego sojusznika lub pokonanego zwierzęcia.";
        }
    }
}
