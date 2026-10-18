using System;
using System.Collections.Generic;
using Leeway.CreatureEditor;
using Leeway.World;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Leeway.Planet
{
    /// <summary>
    /// Lets the camera leave the island: scrolling out past the farthest point of the play camera lifts the
    /// shot away from the ground until the whole planet is in view, and from there the planet is what the
    /// camera orbits.
    /// </summary>
    /// <remarks>
    /// <para>Two cameras share the work. The play camera keeps following the creature; a second camera,
    /// made here, takes over for the planet view and Cinemachine's brain blends between them, so the
    /// climb out and the descent back are one smooth move rather than a cut.</para>
    ///
    /// <para>The flat world - the island's terrain and sea, and its plants - is hidden while the planet is
    /// on screen and shown again as the camera heads back, because the flat sea is a plate of a few
    /// kilometres that would otherwise hang in front of the globe. The planet is shown only in the planet
    /// view for the converse reason: its pole lies under the island.</para>
    /// </remarks>
    public sealed class PlanetCameraDirector : MonoBehaviour
    {
        private enum View { Surface, Planet }

        [SerializeField] private PlanetView _planet;
        [SerializeField] private CinemachineCamera _playCamera;
        [SerializeField] private CreatureOrbitCamera _orbit;

        [Header("Planet camera")]
        [SerializeField, Range(20f, 60f)] private float _fieldOfView = 35f;
        [Tooltip("Seconds the camera takes to fly between the island and the planet.")]
        [SerializeField, Min(0.2f)] private float _blendSeconds = 2.4f;
        [Tooltip("Closest the planet camera gets to the surface, as a multiple of the radius.")]
        [SerializeField, Min(1.05f)] private float _nearFactor = 1.35f;
        [Tooltip("Farthest the planet camera backs off, as a multiple of the distance at which the planet just fits.")]
        [SerializeField, Min(1f)] private float _farFactor = 1.7f;
        [SerializeField, Range(0.02f, 0.5f)] private float _zoomStep = 0.12f;
        [Tooltip("Degrees of rotation per pixel of mouse movement at the closest range.")]
        [SerializeField] private float _dragSensitivity = 0.28f;
        [SerializeField] private float _damping = 9f;

        private const int ActivePriority = 50;
        private static readonly int PlanetViewId = Shader.PropertyToID("_LeewayPlanetView");

        private CinemachineCamera _planetCamera;
        private CinemachineBrain _brain;
        private CinemachineBlendDefinition _savedBlend;
        private bool _blendOverridden;

        private View _view;
        private float _yaw, _pitch, _distance;
        private float _smoothYaw, _smoothPitch, _smoothDistance;
        private bool _dragging;
        private float _ignoreScrollUntil;

        private float _switchedAt;
        private bool _flatHidden;
        private bool _planetHidden = true;
        private readonly List<Renderer> _hidden = new();

        /// <summary>True from the moment the camera sets out for the planet until it starts back.</summary>
        public bool InPlanetView => _view == View.Planet;

        public event Action<bool> ViewChanged;

        private float FitDistance => _planet.Radius / Mathf.Sin(_fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.3f;
        private float MinDistance => _planet.Radius * _nearFactor;
        private float MaxDistance => FitDistance * _farFactor;

        private void Awake()
        {
            var cameraObject = new GameObject("CM_Planet");
            cameraObject.transform.SetParent(transform, false);
            _planetCamera = cameraObject.AddComponent<CinemachineCamera>();
            _planetCamera.Priority.Value = 0;

            LensSettings lens = _planetCamera.Lens;
            lens.FieldOfView = _fieldOfView;
            // A planet is thousands of metres across; the play camera's clip planes would cut it in two.
            lens.NearClipPlane = 5f;
            lens.FarClipPlane = 200000f;
            _planetCamera.Lens = lens;
        }

        private void Start()
        {
            if (_orbit == null) _orbit = FindAnyObjectByType<CreatureOrbitCamera>();
            if (_playCamera == null && _orbit != null) _playCamera = FindPlayCamera();
        }

        private void OnDestroy()
        {
            Shader.SetGlobalFloat(PlanetViewId, 0f);
            if (_view == View.Planet) ShowFlatWorld();
        }

        private CinemachineCamera FindPlayCamera()
        {
            foreach (CinemachineCamera candidate in FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None))
                if (candidate.TryGetComponent<CinemachineFollow>(out _)) return candidate;
            return null;
        }

        private void Update()
        {
            if (_planet == null) return;

            if (_view == View.Surface) UpdateSurface();
            else UpdatePlanet();

            SettleVisibility();
            UpdateStylisation();
        }

        // The palette look of the island is made for a view of a few hundred metres; the farther the camera is from
        // the ground the less of it is applied, so the planet shows its own colours.
        private void UpdateStylisation()
        {
            UnityEngine.Camera main = UnityEngine.Camera.main;
            float amount = 0f;
            if (main != null && (_view == View.Planet || _planet.IsVisible))
            {
                float altitude = (main.transform.position - _planet.Center).magnitude - _planet.Radius;
                amount = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(250f, 2500f, altitude));
            }
            Shader.SetGlobalFloat(PlanetViewId, amount);
        }

        // ---------------------------------------------------------------- surface

        private void UpdateSurface()
        {
            if (_orbit == null || Time.unscaledTime < _ignoreScrollUntil) return;

            Mouse mouse = Mouse.current;
            if (mouse == null || OverUi()) return;

            bool live = _playCamera != null && CinemachineCore.IsLive(_playCamera);
            if (live && mouse.scroll.ReadValue().y < 0f && _orbit.AtFarLimit) EnterPlanetView();
        }

        // ---------------------------------------------------------------- planet

        private void UpdatePlanet()
        {
            Mouse mouse = Mouse.current;
            Keyboard keyboard = Keyboard.current;
            bool overUi = OverUi();

            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                ExitPlanetView();
                return;
            }

            if (mouse != null)
            {
                bool pressed = mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame;
                bool held = mouse.leftButton.isPressed || mouse.rightButton.isPressed;
                if (pressed && !overUi) _dragging = true;
                if (!held) _dragging = false;

                if (_dragging)
                {
                    // The globe follows the hand: drag right and the surface moves right, so the camera goes left.
                    // Close to the surface a pixel is a smaller piece of the sky, so the drag slows down.
                    float scale = Mathf.Clamp(_distance / FitDistance, 0.25f, 1.2f);
                    Vector2 delta = mouse.delta.ReadValue();
                    _yaw += delta.x * _dragSensitivity * scale;
                    _pitch = Mathf.Clamp(_pitch - delta.y * _dragSensitivity * scale, -82f, 82f);
                }

                float scroll = overUi ? 0f : mouse.scroll.ReadValue().y;
                if (scroll > 0f && _distance <= MinDistance * 1.01f && Time.unscaledTime > _ignoreScrollUntil)
                {
                    ExitPlanetView();
                    return;
                }

                if (!Mathf.Approximately(scroll, 0f))
                    _distance = Mathf.Clamp(_distance * (1f - Mathf.Sign(scroll) * _zoomStep), MinDistance, MaxDistance);
            }

            ApplyPose(false);
        }

        private void ApplyPose(bool snap)
        {
            float t = snap ? 1f : 1f - Mathf.Exp(-_damping * Time.unscaledDeltaTime);
            _smoothYaw = Mathf.LerpAngle(_smoothYaw, _yaw, t);
            _smoothPitch = Mathf.Lerp(_smoothPitch, _pitch, t);
            _smoothDistance = Mathf.Lerp(_smoothDistance, _distance, t);

            float yaw = _smoothYaw * Mathf.Deg2Rad, pitch = _smoothPitch * Mathf.Deg2Rad;
            var direction = new Vector3(Mathf.Cos(pitch) * Mathf.Sin(yaw), Mathf.Sin(pitch), Mathf.Cos(pitch) * Mathf.Cos(yaw));

            Vector3 centre = _planet.Center;
            Transform eye = _planetCamera.transform;
            eye.position = centre + direction * _smoothDistance;
            eye.rotation = Quaternion.LookRotation(-direction, Vector3.up);
        }

        // ---------------------------------------------------------------- switching

        public void EnterPlanetView()
        {
            if (_view == View.Planet || _planet == null) return;
            if (!_planet.EnsureBuilt()) return;

            _view = View.Planet;
            _switchedAt = Time.unscaledTime;
            _ignoreScrollUntil = Time.unscaledTime + 0.4f;

            // Starting on the far side of the pole from where the play camera looked, at a high angle,
            // so the descent later comes over the island the creature stands on.
            _yaw = _orbit != null ? _orbit.Yaw + 180f : 0f;
            _pitch = 58f;
            _distance = FitDistance;
            // The pose starts where the play camera is - just above the island, looking down - and the damping
            // then carries it out to the framing above, so the flight begins from the creature and not from a point in space.
            _smoothYaw = _yaw;
            Vector3 fromCentre = (_orbit != null && UnityEngine.Camera.main != null ? UnityEngine.Camera.main.transform.position : _planet.Center + Vector3.up * _planet.Radius) - _planet.Center;
            _smoothDistance = Mathf.Max(fromCentre.magnitude, _planet.Radius);
            _smoothPitch = Mathf.Clamp(Mathf.Asin(Mathf.Clamp(fromCentre.normalized.y, -1f, 1f)) * Mathf.Rad2Deg, 60f, 85f);

            _planet.SetVisible(true);
            _planetHidden = false;
            ApplyPose(false);

            if (_orbit != null) _orbit.Suspended = true;
            BlendWith(_blendSeconds);
            _planetCamera.Priority.Value = ActivePriority;
            ViewChanged?.Invoke(true);
        }

        public void ExitPlanetView()
        {
            if (_view == View.Surface) return;

            _view = View.Surface;
            _switchedAt = Time.unscaledTime;
            _ignoreScrollUntil = Time.unscaledTime + 1f;
            _dragging = false;

            ShowFlatWorld();
            if (_orbit != null) _orbit.Suspended = false;
            BlendWith(_blendSeconds);
            _planetCamera.Priority.Value = 0;
            ViewChanged?.Invoke(false);
        }

        private void BlendWith(float seconds)
        {
            if (_brain == null)
            {
                UnityEngine.Camera main = UnityEngine.Camera.main;
                if (main != null) main.TryGetComponent(out _brain);
            }
            if (_brain == null) return;

            if (!_blendOverridden)
            {
                _savedBlend = _brain.DefaultBlend;
                _blendOverridden = true;
            }
            _brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, seconds);
        }

        // Once the camera has arrived, what it is no longer looking at is switched off.
        private void SettleVisibility()
        {
            bool blending = _brain != null && _brain.IsBlending;
            if (blending || Time.unscaledTime - _switchedAt < 0.2f) return;

            if (_view == View.Planet && !_flatHidden) HideFlatWorld();
            if (_view == View.Surface && !_planetHidden)
            {
                _planet.SetVisible(false);
                _planetHidden = true;
            }

            if (_blendOverridden && _brain != null)
            {
                _brain.DefaultBlend = _savedBlend;
                _blendOverridden = false;
            }
        }

        private void HideFlatWorld()
        {
            _flatHidden = true;
            _hidden.Clear();

            foreach (Transform root in FlatWorldRoots())
            {
                foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(false))
                {
                    if (!renderer.enabled) continue;
                    renderer.enabled = false;
                    _hidden.Add(renderer);
                }
            }
        }

        private void ShowFlatWorld()
        {
            foreach (Renderer renderer in _hidden)
                if (renderer != null) renderer.enabled = true;

            _hidden.Clear();
            _flatHidden = false;
        }

        private IEnumerable<Transform> FlatWorldRoots()
        {
            var generator = FindAnyObjectByType<IslandGenerator>();
            if (generator != null) yield return generator.transform;

            var flora = FindAnyObjectByType<IslandFlora>();
            if (flora != null) yield return flora.transform;
        }

        private static bool OverUi() => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }
}
