using Leeway.CreatureEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Leeway.Combat
{
    /// <summary>
    /// The player's end of a fight: one key, one blow.
    /// </summary>
    /// <remarks>
    /// <para>The input sits here rather than in <c>PlaygroundCreatureController</c> so that the
    /// dependency runs one way only — combat knows about the creature, the creature knows nothing
    /// about combat. A feature that can be lifted out of the project without the movement code
    /// noticing is worth a small component.</para>
    ///
    /// <para><b>Not while sculpting.</b> The same scene holds the editor, where the creature stands
    /// still and the mouse belongs to the tools. Swinging at things from the build screen would hit
    /// creatures the player cannot even see from there.</para>
    /// </remarks>
    [RequireComponent(typeof(CreatureCombatant))]
    public class CreatureAttackInput : MonoBehaviour
    {
        [Tooltip("The key that strikes. Shoving is on F, so this is deliberately next to it.")]
        [SerializeField] private Key _attackKey = Key.Space;

        private CreatureCombatant _combatant;
        private CreatureBody _body;
        private CreatureEditorController _editor;
        private bool _editorResolved;

        private void Awake()
        {
            _combatant = GetComponent<CreatureCombatant>();
            TryGetComponent(out _body);
        }

        private void Update()
        {
            if (_body == null || !_body.IsOwner) return;
            if (IsSculpting) return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard[_attackKey].wasPressedThisFrame) return;

            _combatant.RequestAttack();
        }

        /// <summary>
        /// Whether the player is at the build screen rather than out in the playground.
        /// </summary>
        /// <remarks>
        /// Resolved once and lazily: the creature is a prefab and cannot hold a reference to a scene
        /// object, and a scene with no editor in it — the creature phase — simply never has one.
        /// </remarks>
        private bool IsSculpting
        {
            get
            {
                if (!_editorResolved)
                {
                    _editorResolved = true;
                    _editor = FindAnyObjectByType<CreatureEditorController>();
                }

                return _editor != null && _editor.Mode == CreatureEditorMode.Sculpt;
            }
        }
    }
}
