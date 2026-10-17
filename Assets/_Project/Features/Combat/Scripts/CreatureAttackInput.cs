using Leeway.Creature.Domain;
using Leeway.CreatureEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
namespace Leeway.Combat
{
    [RequireComponent(typeof(CreatureCombatant))]
    public class CreatureAttackInput:MonoBehaviour
    {
        [SerializeField] private Key _attackKey=Key.None;
        private CreatureCombatant _combatant;private CreatureBody _body;
        private CreatureEditorController _editor;
        private void Awake(){_combatant=GetComponent<CreatureCombatant>();_body=GetComponent<CreatureBody>();}
        private void Update()
        {
            if(!_body||!_body.IsOwner||!_body.IsAlive)return;
            if(!_editor)_editor=FindAnyObjectByType<CreatureEditorController>();
            if(_editor&&_editor.Mode==CreatureEditorMode.Sculpt)return;
            if(EventSystem.current&&EventSystem.current.currentSelectedGameObject&&
                EventSystem.current.currentSelectedGameObject.GetComponent<TMPro.TMP_InputField>())return;
            var k=Keyboard.current;var m=Mouse.current;
            bool overUI=EventSystem.current&&EventSystem.current.IsPointerOverGameObject();
            if(m!=null&&!overUI)
            {
                if(m.leftButton.wasPressedThisFrame)_combatant.RequestAttack();
            }
            if(k==null)return;
            if(_attackKey!=Key.None&&k[_attackKey].wasPressedThisFrame)_combatant.RequestAttack();
            if(k.digit1Key.wasPressedThisFrame)_combatant.RequestAbility(CreatureAbility.Bite);
            if(k.digit2Key.wasPressedThisFrame)_combatant.RequestAbility(CreatureAbility.Claw);
            if(k.digit3Key.wasPressedThisFrame)_combatant.RequestAbility(CreatureAbility.Horn);
            if(k.rKey.wasPressedThisFrame)_combatant.RequestAbility(BodyAbilityRules.Secondary(_body.Genome,_body.Rules));
            if(k.qKey.wasPressedThisFrame)_combatant.RequestAbility(CreatureAbility.Scan);
            if(k.eKey.wasPressedThisFrame)_combatant.RequestAbility(CreatureAbility.Forage);
        }
    }
}
