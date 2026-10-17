using System;
using Leeway.Creature.Domain;
using Leeway.CreatureEditor;
using MessagePipe;
using TMPro;
using UnityEngine;
namespace Leeway.Combat
{
    public class CombatHud:MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _healthText,_hitText,_abilityText;
        [SerializeField] private GameObject _combatPanel;
        [SerializeField] private Color _healthy=new(0.85f,0.92f,0.85f,1),_hurt=new(0.95f,0.55f,0.35f,1);
        [SerializeField,Range(0,1)] private float _hurtBelow=0.4f;
        [SerializeField,Min(0.2f)] private float _hitSeconds=1.6f;
        private IDisposable _subscriptions;
        private CreatureBody _body;
        private CreatureCombatant _fighter;
        private CreatureEditorController _editor;
        private float _hitUntil,_nextResolve,_nextLabels;
        private NpcCreatureAgent[] _labels=Array.Empty<NpcCreatureAgent>();
        private GUIStyle _labelStyle;
        private void OnEnable()
        {
            if(!GlobalMessagePipe.IsInitialized)return;
            var bag=DisposableBag.CreateBuilder();
            GlobalMessagePipe.GetSubscriber<LocalCreatureBodyChangedMessage>().Subscribe(m=>Bind(m.Body)).AddTo(bag);
            GlobalMessagePipe.GetSubscriber<CreatureStruckMessage>().Subscribe(OnStruck).AddTo(bag);
            GlobalMessagePipe.GetSubscriber<CreatureDiedMessage>().Subscribe(OnDied).AddTo(bag);
            _subscriptions=bag.Build();
        }
        private void OnDisable(){_subscriptions?.Dispose();_subscriptions=null;}
        private void Bind(CreatureBody body)
        {_body=body;_fighter=body?body.GetComponent<CreatureCombatant>():null;if(_hitText)_hitText.text=string.Empty;}
        private bool Playing=>_body&&_fighter&&(!_editor||_editor.Mode==CreatureEditorMode.Play);
        private void Update()
        {
            if(Time.time>=_nextResolve)
            {
                _nextResolve=Time.time+1;
                if(!_editor)_editor=FindAnyObjectByType<CreatureEditorController>();
                if(!_body)foreach(var b in FindObjectsByType<CreatureBody>(FindObjectsSortMode.None))if(b.IsOwner){Bind(b);break;}
            }
            if(_combatPanel)_combatPanel.SetActive(Playing);
            if(!Playing)return;
            if(_healthText)
            {
                _healthText.color=_body.Hp/Mathf.Max(1,_body.MaxHp)<=_hurtBelow?_hurt:_healthy;
                _healthText.text=_body.IsAlive?$"HP {_body.Hp:0} / {_body.MaxHp:0}":"Pokonany";
            }
            if(_hitText&&Time.time>=_hitUntil)_hitText.text=_fighter.Notice;
            if(_abilityText)
            {
                var g=_body.Genome;var r=_body.Rules;
                var primary=BodyAbilityRules.Primary(g,r);var secondary=BodyAbilityRules.Secondary(g,r);
                string attack=_fighter.CooldownRemaining>0?$" · {_fighter.CooldownRemaining:0.0}s":" · gotowe";
                string line="LPM: "+BodyAbilityRules.Name(primary)+attack;
                if(secondary!=primary)line+="    R: "+BodyAbilityRules.Name(secondary);
                if(BodyAbilityRules.Has(g,r,CreatureAbility.Scan))line+="\nQ: czujność"+(_fighter.ScanCooldown>0?$" ({_fighter.ScanCooldown:0}s)":"");
                if(BodyAbilityRules.Has(g,r,CreatureAbility.Forage))line+="    E: pomoc / żerowanie";
                line+="\nShift: sprint    Spacja: skok    F: odepchnięcie    1 / 2 / 3: paszcza / ramiona / róg";
                _abilityText.text=!_body.IsAlive?"Odrodzenie w obozie za chwilę.":_body.IsInEditorPad?"Strefa edytora — wyjdź poza krąg, aby walczyć.":line;
            }
            if(Time.time>=_nextLabels){_nextLabels=Time.time+0.5f;_labels=FindObjectsByType<NpcCreatureAgent>(FindObjectsSortMode.None);}
        }
        private void OnStruck(CreatureStruckMessage m)
        {
            if(!_hitText||!_body||m.Damage<=0)return;
            if(m.Target!=_body&&m.Attacker!=_body)return;
            _hitText.color=m.Target==_body?_hurt:_healthy;
            _hitText.text=(m.Target==_body?"Otrzymano −":"Trafienie −")+m.Damage.ToString("0")+" HP"+(m.KnockedDown?" · przewrócenie":"");
            _hitUntil=Time.time+_hitSeconds;
        }
        private void OnDied(CreatureDiedMessage m)
        {
            if(!_hitText||!_body)return;
            if(m.Body!=_body&&m.Killer!=_body)return;
            _hitText.text=m.Body==_body?"Zostałeś pokonany":"Przeciwnik pokonany · E: żerowanie";
            _hitUntil=Time.time+3;
        }
        private void OnGUI()
        {
            if(!Playing||Event.current.type!=EventType.Repaint)return;
            var camera=Camera.main;if(!camera)return;
            _labelStyle??=new GUIStyle(GUI.skin.box){fontSize=12,alignment=TextAnchor.MiddleCenter};
            foreach(var npc in _labels)
            {
                if(!npc)continue;var b=npc.GetComponent<CreatureBody>();if(!b)continue;
                float distance=Vector3.Distance(_body.transform.position,npc.transform.position);
                float range=_fighter.ScanActive?_body.Stats.SenseRadius*2:12;
                if(distance>range)continue;
                var world=npc.transform.position+Vector3.up*1.2f;
                if(Physics.Linecast(camera.transform.position,world,1,QueryTriggerInteraction.Ignore))continue;
                var p=camera.WorldToScreenPoint(world);if(p.z<=0||p.x<0||p.x>Screen.width||p.y<0||p.y>Screen.height)continue;
                GUI.color=npc.IsPredator?new Color(1,0.65f,0.5f):npc.Disposition==NpcCreatureType.Territorial?
                    new Color(1,0.85f,0.55f):new Color(0.7f,0.95f,0.8f);
                string text=npc.SpeciesName+"\n"+(b.IsAlive?$"{b.Hp:0} / {b.MaxHp:0} HP":"Pokonany · E");
                GUI.Box(new Rect(p.x-90,Screen.height-p.y-35,180,38),text,_labelStyle);
            }
            GUI.color=Color.white;
        }
    }
}
