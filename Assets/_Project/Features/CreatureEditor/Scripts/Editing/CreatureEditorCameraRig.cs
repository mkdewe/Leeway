using Unity.Cinemachine;
using UnityEngine;

namespace Leeway.CreatureEditor
{
    /// <summary>The editor's two working modes: sculpting and testing in the playground.</summary>
    public enum CreatureEditorMode
    {
        /// <summary>Sculpting on the preview. The networked creature stands still.</summary>
        Sculpt,

        /// <summary>Driving the creature in the playground. The preview is hidden.</summary>
        Play,
    }

    /// <summary>
    /// Switches the camera between the editor orbit and following the creature.
    /// </summary>
    /// <remarks>
    /// Switching is a change of <c>Priority</c> — the blending is handled by
    /// <c>CinemachineBrain</c>, so there is not a single line of interpolation here.
    /// </remarks>
    public class CreatureEditorCameraRig : MonoBehaviour
    {
        private const int ActivePriority = 20;
        private const int IdlePriority = 5;

        [Header("Cameras")]
        [SerializeField] private CinemachineCamera _sculptCamera;
        [SerializeField] private CinemachineCamera _playCamera;

        [Header("Targets")]
        [Tooltip("The preview root - the camera orbits around the creature being sculpted.")]
        [SerializeField] private Transform _sculptTarget;

        public CreatureEditorMode Mode { get; private set; } = CreatureEditorMode.Sculpt;

        private void Start() => Apply();

        public void SetMode(CreatureEditorMode mode)
        {
            if (Mode == mode) return;

            Mode = mode;
            Apply();
        }

        /// <summary>Whether the play camera still has something to follow. False means nobody is on screen.</summary>
        public bool HasPlayTarget => _playCamera != null && _playCamera.Follow != null;

        /// <summary>Attaches the play-mode camera to the local player's creature; <c>null</c> detaches it.</summary>
        public void SetPlayTarget(Transform target)
        {
            if (_playCamera == null) return;

            Transform focus = FocusOf(target);

            _playCamera.Follow = focus;
            _playCamera.LookAt = focus;
        }

        /// <summary>
        /// The axis the shot should turn around: the centre of the creature's visible silhouette,
        /// rather than the genome's anchor point.
        /// </summary>
        /// <remarks>
        /// Those two points do not coincide — <c>SpineAnchor</c> centres the bones, while what is on
        /// screen is the skin, the attached parts and the legs. A camera aimed at the root carries the
        /// creature around the screen on every turn instead of holding it in the middle of the frame.
        /// The arithmetic and the measurements live in <see cref="CreatureFocusPoint"/>.
        /// </remarks>
        private static Transform FocusOf(Transform target)
        {
            if (target == null) return null;

            if (!target.TryGetComponent(out CreatureFocusPoint focus))
                focus = target.gameObject.AddComponent<CreatureFocusPoint>();

            return focus.Pivot;
        }

        private void Apply()
        {
            if (_sculptCamera != null)
            {
                Transform focus = FocusOf(_sculptTarget);

                _sculptCamera.Follow = focus;
                _sculptCamera.LookAt = focus;
                _sculptCamera.Priority.Value = Mode == CreatureEditorMode.Sculpt ? ActivePriority : IdlePriority;
            }

            if (_playCamera != null)
                _playCamera.Priority.Value = Mode == CreatureEditorMode.Play ? ActivePriority : IdlePriority;
        }
    }
}
