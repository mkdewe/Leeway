using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Leeway.CreatureEditor
{
    /// <summary>
    /// Orbits the game camera around the creature, driven by the right mouse button.
    /// </summary>
    /// <remarks>
    /// <para>The camera has its <b>own</b> heading, independent of where the creature is looking. That
    /// is why the Cinemachine binding has to stay in <c>WorldSpace</c>: a mode pinned to the target's
    /// rotation would swing the shot around with the creature and fight whatever the player sets with
    /// the mouse.</para>
    ///
    /// <para>The camera's heading is also the frame of reference for input — "up the screen" means
    /// "away from the camera". The controller takes it from here and sends it in the movement payload,
    /// so the server and the reconciliation replays compute the direction in exactly the same way.</para>
    ///
    /// <para><b>The orbit smoothing has to live here</b>, in <see cref="_damping"/>, because
    /// Cinemachine's <c>PositionDamping</c> does not touch it: what gets damped is the <b>target
    /// point</b>, and the camera offset is added rigidly after the damping. Measured — rotating the
    /// offset gives exactly zero radius and aim error regardless of whether the damping is set to 0 or
    /// to 1 s. Pulling the smoothing out of here and handing it to Cinemachine <b>will not
    /// work</b>.</para>
    /// </remarks>
    public class CreatureOrbitCamera : MonoBehaviour
    {
        [SerializeField] private CinemachineCamera _playCamera;

        [Tooltip("The camera offset at zero heading. We rotate it around the creature.")]
        [SerializeField] private Vector3 _baseOffset = new(0f, 1.7f, -4f);

        [Tooltip("Degrees of rotation per pixel of mouse movement.")]
        [SerializeField] private float _sensitivity = 0.22f;

        [Tooltip("How fast the shot pulls towards the requested heading.")]
        [SerializeField] private float _damping = 14f;

        private CinemachineFollow _follow;
        private float _yaw;
        private float _smoothedYaw;

        /// <summary>The camera's heading in degrees — the frame of reference for the player's input.</summary>
        public float Yaw => _smoothedYaw;

        private void Awake()
        {
            if (_playCamera != null) _playCamera.TryGetComponent(out _follow);
        }

        private void LateUpdate()
        {
            if (_follow == null) return;

            var mouse = Mouse.current;
            if (mouse != null && mouse.rightButton.isPressed)
                _yaw += mouse.delta.x.ReadValue() * _sensitivity;

            // Smoothing, so a jerk of the mouse does not carry straight through to the shot — nor, via
            // the input frame of reference, to the creature's heading.
            _smoothedYaw = Mathf.LerpAngle(_smoothedYaw, _yaw, 1f - Mathf.Exp(-_damping * Time.deltaTime));

            _follow.FollowOffset = Quaternion.Euler(0f, _smoothedYaw, 0f) * _baseOffset;
        }
    }
}
