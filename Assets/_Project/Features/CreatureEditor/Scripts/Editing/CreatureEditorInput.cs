using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Leeway.CreatureEditor
{
    /// <summary>
    /// The only place where the creature editor touches the keyboard and mouse.
    /// </summary>
    /// <remarks>
    /// <para><b>A departure from the plan:</b> the actions are built in code rather than from a
    /// <c>.inputactions</c> asset with a generated wrapper. The reason is practical — the asset is
    /// hand-written JSON plus a code generator switched on in the importer, i.e. two extra points of
    /// failure at a stage where the bindings are still going to change. <c>InputAction</c> gives the
    /// same model (composites, modifiers, rebinding), so moving to an asset later is a matter of
    /// copying the bindings out of this one class.</para>
    ///
    /// <para>A deliberate inconsistency with <c>PlayerCreatureController</c>, which queries
    /// <c>Keyboard.current</c> directly: that is enough for WASD, but the editor's interactions
    /// (drag with a modifier, context-sensitive scroll) would not survive it.</para>
    /// </remarks>
    public class CreatureEditorInput : MonoBehaviour
    {
        private InputAction _select;
        private InputAction _drag;
        private InputAction _pointer;
        private InputAction _orbit;
        private InputAction _zoom;
        private InputAction _addVertebra;
        private InputAction _removeVertebra;
        private InputAction _toggleMode;
        private InputAction _returnToEditor;
        private InputAction _apply;
        private InputAction _revert;
        private InputAction _cycleSelection;
        private InputAction _toggleSymmetry;

        /// <summary>The cursor position in screen pixels.</summary>
        public Vector2 PointerPosition => _pointer?.ReadValue<Vector2>() ?? Vector2.zero;

        /// <summary>How far the cursor moved this frame.</summary>
        public Vector2 PointerDelta => _drag?.ReadValue<Vector2>() ?? Vector2.zero;

        /// <summary>Whether a left-button drag is in progress.</summary>
        public bool IsDragging => _select != null && _select.IsPressed();

        /// <summary>Whether a right-button camera orbit is in progress.</summary>
        public bool IsOrbiting => _orbit != null && _orbit.IsPressed();

        public float ZoomDelta => _zoom?.ReadValue<Vector2>().y ?? 0f;

        public event Action SelectPressed;
        public event Action SelectReleased;
        public event Action AddVertebraPressed;
        public event Action RemoveVertebraPressed;
        public event Action ToggleModePressed;

        /// <summary>Leaving the playground and going back to sculpting. Separate from <see cref="ToggleModePressed"/> because it only works one way.</summary>
        public event Action ReturnToEditorPressed;
        public event Action ApplyPressed;
        public event Action RevertPressed;
        public event Action<int> CycleSelectionPressed;
        public event Action ToggleSymmetryPressed;

        private void Awake()
        {
            _pointer = new InputAction("Pointer", InputActionType.Value, "<Mouse>/position");
            _drag = new InputAction("Drag", InputActionType.Value, "<Mouse>/delta");
            _select = new InputAction("Select", InputActionType.Button, "<Mouse>/leftButton");
            _orbit = new InputAction("Orbit", InputActionType.Button, "<Mouse>/rightButton");
            _zoom = new InputAction("Zoom", InputActionType.Value, "<Mouse>/scroll");

            _addVertebra = new InputAction("AddVertebra", InputActionType.Button, "<Keyboard>/e");
            _removeVertebra = new InputAction("RemoveVertebra", InputActionType.Button, "<Keyboard>/q");
            _toggleMode = new InputAction("ToggleMode", InputActionType.Button, "<Keyboard>/tab");

            // Escape returns to the editor rather than toggling. The toggle already has Tab, and the
            // reflexive "get me out of here" key has to do exactly one thing — otherwise pressing it
            // while sculpting would throw the player out into the field.
            _returnToEditor = new InputAction("ReturnToEditor", InputActionType.Button, "<Keyboard>/escape");
            _apply = new InputAction("Apply", InputActionType.Button, "<Keyboard>/enter");
            _apply.AddBinding("<Keyboard>/numpadEnter");
            _revert = new InputAction("Revert", InputActionType.Button, "<Keyboard>/backspace");
            _toggleSymmetry = new InputAction("ToggleSymmetry", InputActionType.Button, "<Keyboard>/m");

            // One action for both directions of the selection — the direction travels in the value, so
            // adding a gamepad is one binding rather than a second code path.
            _cycleSelection = new InputAction("CycleSelection", InputActionType.Button);
            _cycleSelection.AddCompositeBinding("1DAxis")
                .With("Negative", "<Keyboard>/leftBracket")
                .With("Positive", "<Keyboard>/rightBracket");

            _select.performed += _ => SelectPressed?.Invoke();
            _select.canceled += _ => SelectReleased?.Invoke();
            _addVertebra.performed += _ => AddVertebraPressed?.Invoke();
            _removeVertebra.performed += _ => RemoveVertebraPressed?.Invoke();
            _toggleMode.performed += _ => ToggleModePressed?.Invoke();
            _returnToEditor.performed += _ => ReturnToEditorPressed?.Invoke();
            _apply.performed += _ => ApplyPressed?.Invoke();
            _revert.performed += _ => RevertPressed?.Invoke();
            _toggleSymmetry.performed += _ => ToggleSymmetryPressed?.Invoke();
            _cycleSelection.performed += ctx => CycleSelectionPressed?.Invoke(ctx.ReadValue<float>() >= 0f ? 1 : -1);
        }

        private void OnEnable() => ForEachAction(a => a.Enable());
        private void OnDisable() => ForEachAction(a => a.Disable());

        private void OnDestroy() => ForEachAction(a => a.Dispose());

        private void ForEachAction(Action<InputAction> apply)
        {
            if (_pointer == null) return;

            apply(_pointer);
            apply(_drag);
            apply(_select);
            apply(_orbit);
            apply(_zoom);
            apply(_addVertebra);
            apply(_removeVertebra);
            apply(_toggleMode);
            apply(_returnToEditor);
            apply(_apply);
            apply(_revert);
            apply(_cycleSelection);
            apply(_toggleSymmetry);
        }
    }
}
