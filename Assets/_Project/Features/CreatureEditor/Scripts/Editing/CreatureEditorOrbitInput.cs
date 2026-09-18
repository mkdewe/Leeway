using Unity.Cinemachine;
using UnityEngine;

namespace Leeway.CreatureEditor
{
    /// <summary>
    /// Orbits the sculpting camera around the preview — only while the right mouse button is held.
    /// </summary>
    /// <remarks>
    /// It replaces <c>CinemachineInputAxisController</c>, which by default hangs straight off
    /// <c>&lt;Mouse&gt;/delta</c> and therefore spins the camera on every mouse move — including when
    /// the player is merely aiming at a vertebra. We write the axes here by hand, which keeps all the
    /// editor's input inside <see cref="CreatureEditorInput"/> and stops the scroll wheel being shared
    /// between camera zoom and growing flesh.
    ///
    /// <para>This camera's damping smooths only the <b>jumps of the orbit centre</b> — and it does
    /// jump, because it is the centre of the visible silhouette and moves after every edit (see
    /// <see cref="CreatureFocusPoint"/>). It does not touch the orbit itself: Cinemachine damps the
    /// target point and adds the camera offset after the damping, so mouse-turning is crisp at any
    /// setting.</para>
    /// </remarks>
    [RequireComponent(typeof(CinemachineOrbitalFollow))]
    public class CreatureEditorOrbitInput : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private CreatureEditorInput _input;
        [SerializeField] private CinemachineOrbitalFollow _orbit;

        [Header("Sensitivity")]
        [Tooltip("Degrees of rotation per pixel of horizontal mouse movement.")]
        [SerializeField] private float _horizontalSpeed = 0.2f;

        [Tooltip("Degrees of rotation per pixel of vertical mouse movement.")]
        [SerializeField] private float _verticalSpeed = 0.12f;

        [Tooltip("Moving the mouse up raises the camera above the creature instead of lowering it.")]
        [SerializeField] private bool _invertVertical = true;

        private void Reset() => _orbit = GetComponent<CinemachineOrbitalFollow>();

        private void Update()
        {
            if (_input == null || _orbit == null || !_input.IsOrbiting) return;

            Vector2 delta = _input.PointerDelta;
            if (delta.sqrMagnitude <= 0f) return;

            float vertical = _invertVertical ? -delta.y : delta.y;

            // ClampValue already handles wrapping the horizontal axis (-180..180) and the hard range of
            // the vertical one, so we do not duplicate the orbit configuration's rules here.
            _orbit.HorizontalAxis.Value = _orbit.HorizontalAxis.ClampValue(
                _orbit.HorizontalAxis.Value + delta.x * _horizontalSpeed);

            _orbit.VerticalAxis.Value = _orbit.VerticalAxis.ClampValue(
                _orbit.VerticalAxis.Value + vertical * _verticalSpeed);
        }
    }
}
