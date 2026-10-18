using System;
using System.Collections.Generic;
using System.Diagnostics;
using Leeway.CreatureEditor;
using Leeway.Rendering;
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
    /// <para>Leaving the ground is leaving the air. How far the camera has climbed sets one number, how much of the
    /// sky is still air (see <see cref="SpaceBackdrop"/>), and that number does the rest: the haze that hides the planet
    /// from the ground thins and lets its colours through, the sky darkens to stars, and the flat world - the island's
    /// terrain, sea and plants - is switched off once the haze has swallowed it, a few renderers at a time, so that
    /// nothing is switched on or off in view and no frame has to do it all at once.</para>
    /// </remarks>
    public sealed class PlanetCameraDirector : MonoBehaviour
    {
        private enum View { Surface, Planet }

        [SerializeField] private PlanetView _planet;
        [SerializeField] private CinemachineCamera _playCamera;
        [SerializeField] private CreatureOrbitCamera _orbit;
        [SerializeField] private SpaceBackdrop _backdrop;

        [Header("Planet camera")]
        [SerializeField, Range(20f, 60f)] private float _fieldOfView = 35f;
        [Tooltip("Seconds the camera takes to fly between the island and the planet.")]
        [SerializeField, Min(0.5f)] private float _flightSeconds = 3.6f;
        [Tooltip("Closest the planet camera gets to the surface, as a multiple of the radius.")]
        [SerializeField, Min(1.05f)] private float _nearFactor = 1.35f;
        [Tooltip("Farthest the planet camera backs off, as a multiple of the distance at which the planet just fits.")]
        [SerializeField, Min(1f)] private float _farFactor = 1.7f;
        [Tooltip("Degrees the view is tilted up, so the planet sits below the panel across the top of the screen.")]
        [SerializeField, Range(0f, 10f)] private float _screenLift = 4.5f;
        [SerializeField, Range(0.02f, 0.5f)] private float _zoomStep = 0.12f;
        [Tooltip("Degrees of rotation per pixel of mouse movement at the closest range.")]
        [SerializeField] private float _dragSensitivity = 0.28f;
        [SerializeField] private float _damping = 9f;

        private const int ActivePriority = 50;
        // A planet is thousands of metres across: the clip planes of a camera that walks the island would cut it in two.
        private const float PlanetNear = 5f;
        private const float PlanetFar = 600000f;
        private static readonly int PlanetViewId = Shader.PropertyToID("_LeewayPlanetView");

        private CinemachineCamera _planetCamera;
        private CinemachineBrain _brain;
        private CinemachineBlendDefinition _savedBlend;
        private bool _blendOverridden;

        // The flight: 0 with the camera where the ground camera is, 1 with it in orbit round the planet.
        private float _flight;
        private CinemachineVirtualCameraBase _ground;
        private CinemachineVirtualCameraBase.StandbyUpdateMode _groundStandby;
        private bool _airEffects = true;
        private IslandWind _wind;
        private StylizedWaterReflections _reflections;
        private Vector3 _eye;
        private bool _hasEye;

        private View _view;
        private float _yaw, _pitch, _distance;
        private float _smoothYaw, _smoothPitch, _smoothDistance;
        private bool _dragging;
        private float _ignoreScrollUntil;

        private float _switchedAt;
        private float _space;

        // The flat world is switched off and on in small batches: a few hundred renderers at once is a visible stall.
        private const double BatchMilliseconds = 1.5;
        private enum Flat { Shown, Hiding, Hidden, Showing }

        private Flat _flat = Flat.Shown;
        private bool _flatWanted = true;
        private readonly List<Renderer> _hidden = new();
        private readonly List<Renderer> _toHide = new();
        private int _hideCursor;

        // Where the camera is between the ground (0) and open space (1).
        public float Space => _space;

        /// <summary>True from the moment the camera sets out for the planet until it starts back.</summary>
        public bool InPlanetView => _view == View.Planet;

        /// <summary>True while the camera is anywhere but on the ground: on its way out, in orbit, or still on its way back.</summary>
        public bool IsAway => _flight > 0f || _view == View.Planet;

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
            lens.NearClipPlane = PlanetNear;
            lens.FarClipPlane = PlanetFar;
            _planetCamera.Lens = lens;
        }

        private void Start()
        {
            if (_orbit == null) _orbit = FindAnyObjectByType<CreatureOrbitCamera>();
            if (_playCamera == null && _orbit != null) _playCamera = FindPlayCamera();
            if (_backdrop == null) _backdrop = FindAnyObjectByType<SpaceBackdrop>();
        }

        private void OnDestroy()
        {
            Shader.SetGlobalFloat(PlanetViewId, 0f);
            if (_reflections != null) _reflections.Suspended = false;
            if (_wind != null) _wind.StreaksEnabled = true;
            foreach (Renderer renderer in _hidden)
                if (renderer != null) renderer.enabled = true;
            _hidden.Clear();
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

            StepFlight();
            UpdateAtmosphere();
            SettleBlend();
        }

        // ---------------------------------------------------------------- air

        // How much of the air the camera has left behind, from its height over the home island. The planet cannot be
        // nearer than 0.35 of its radius, so the climb always ends in space whatever the size.
        private void UpdateAtmosphere()
        {
            UnityEngine.Camera main = UnityEngine.Camera.main;
            Vector3 eye = _hasEye ? _eye : main != null ? main.transform.position : _planet.Anchor;
            float radius = _planet.Radius;
            float top = Mathf.Clamp(radius * 0.3f, 300f, 2000f);
            float bottom = Mathf.Min(180f, top * 0.5f);

            float altitude = (eye - _planet.Center).magnitude - radius - _planet.SurfaceGap;
            _space = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(bottom, top, altitude));

            if (_backdrop != null) _backdrop.Space = _space;
            SetAirEffects(_space < 0.4f);

            // Reflections of the island are of no use from this high, and are the dearest part of drawing it.
            if (_reflections == null) _reflections = FindAnyObjectByType<StylizedWaterReflections>();
            if (_reflections != null) _reflections.Suspended = altitude > 120f;
            Shader.SetGlobalFloat(PlanetViewId, _space);

            // The flat world goes when the haze has swallowed it and returns before the haze lifts again.
            _flatWanted = _flatWanted ? _space < 0.35f : _space < 0.3f;
            StepFlatWorld();

            bool flatGone = _flat == Flat.Hidden;
            // Until the flat world is gone the planet is all haze, the same colour as the sea and the sky's foot; then it clears.
            _planet.SetHaze(flatGone ? 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.4f, 0.85f, _space)) : 1f);

            bool flatShown = _flat == Flat.Shown;
            bool wanted = _view == View.Planet || _space > 0.01f || !flatShown;
            if (wanted != _planet.IsVisible) _planet.SetVisible(wanted);
        }

        // Things that only make sense in the air - the motes drifting by the camera, the streaks of wind - are put away outside it.
        private void SetAirEffects(bool inAir)
        {
            if (inAir == _airEffects) return;
            _airEffects = inAir;

            UnityEngine.Camera main = UnityEngine.Camera.main;
            if (main != null)
            {
                foreach (ParticleSystem motes in main.GetComponentsInChildren<ParticleSystem>(true))
                {
                    ParticleSystem.EmissionModule emission = motes.emission;
                    emission.enabled = inAir;
                    if (!inAir) motes.Clear();
                }
            }

            if (_wind == null) _wind = FindAnyObjectByType<IslandWind>();
            if (_wind != null) _wind.StreaksEnabled = inAir;
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

            SmoothPose();
        }

        // The orbit pose eases towards where the player has put it.
        private void SmoothPose()
        {
            float t = 1f - Mathf.Exp(-_damping * Time.unscaledDeltaTime);
            _smoothYaw = Mathf.LerpAngle(_smoothYaw, _yaw, t);
            _smoothPitch = Mathf.Lerp(_smoothPitch, _pitch, t);
            _smoothDistance = Mathf.Lerp(_smoothDistance, _distance, t);

        }

        private void OrbitPose(out Vector3 position, out Quaternion rotation)
        {
            float yaw = _smoothYaw * Mathf.Deg2Rad, pitch = _smoothPitch * Mathf.Deg2Rad;
            var direction = new Vector3(Mathf.Cos(pitch) * Mathf.Sin(yaw), Mathf.Sin(pitch), Mathf.Cos(pitch) * Mathf.Cos(yaw));

            position = _planet.Center + direction * _smoothDistance;
            rotation = Quaternion.LookRotation(-direction, Vector3.up) * Quaternion.Euler(-_screenLift, 0f, 0f);
        }

        // ---------------------------------------------------------------- switching

        public void EnterPlanetView()
        {
            if (_view == View.Planet || _planet == null) return;
            if (!_planet.EnsureBuilt()) return;

            // Whichever camera has the view now is the one the flight leaves from and comes back to.
            if (_flight <= 0f && !TakeGroundCamera()) return;

            _view = View.Planet;
            _switchedAt = Time.unscaledTime;
            _ignoreScrollUntil = Time.unscaledTime + 0.4f;

            // Starting on the far side of the pole from where the play camera looked, at a high angle,
            // so the descent later comes over the island the creature stands on.
            FaceAwayFromTheSun();
            _distance = FitDistance;
            _smoothYaw = _yaw;
            _smoothPitch = _pitch;
            _smoothDistance = _distance;

            // The planet is on screen from the first frame of the climb, but lost in haze until the air thins.
            _planet.SetHaze(1f);
            _planet.SetVisible(true);

            if (_orbit != null) _orbit.Suspended = true;

            // The planet camera starts exactly where the ground camera is, so taking over is not seen.
            StepFlight();
            UseCut();
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

            if (_orbit != null) _orbit.Suspended = false;
            ViewChanged?.Invoke(false);
        }

        // The first view of the planet: from the dark side and above, the sun a little behind its edge, so that
        // the planet is half lit and the star is the first thing outside it.
        private void FaceAwayFromTheSun()
        {
            Vector4 sun = Shader.GetGlobalVector("_LeewaySunDirection");
            var flat = new Vector3(sun.x, 0f, sun.z);
            if (flat.sqrMagnitude < 0.01f) flat = Vector3.forward;
            flat.Normalize();
            Vector3 side = Vector3.Cross(Vector3.up, flat);
            Vector3 away = (-flat * 0.7f + side * 0.7f).normalized;

            _yaw = Mathf.Atan2(away.x, away.z) * Mathf.Rad2Deg;
            _pitch = 38f;
        }

        private bool TakeGroundCamera()
        {
            if (_brain == null)
            {
                UnityEngine.Camera main = UnityEngine.Camera.main;
                if (main != null) main.TryGetComponent(out _brain);
            }

            _ground = _brain != null ? _brain.ActiveVirtualCamera as CinemachineVirtualCameraBase : null;
            if (_ground == null || _ground == _planetCamera) return false;

            // Kept up to date while it is not the live one, so there is somewhere to come back to.
            _groundStandby = _ground.StandbyUpdate;
            _ground.StandbyUpdate = CinemachineVirtualCameraBase.StandbyUpdateMode.Always;
            return true;
        }

        // ---------------------------------------------------------------- the flight

        // The climb is not a straight line: what the eye wants is the same change of scale each moment, so the height
        // above the ground is blended as a logarithm. The first hundred metres then take as long on screen as the
        // last ten thousand, and the air thins at a pace that can be watched instead of in a blink.
        private void StepFlight()
        {
            float target = _view == View.Planet ? 1f : 0f;
            if (_flight == target && (target >= 1f || _ground == null))
            {
                if (target >= 1f) PoseOrbit();
                return;
            }

            _flight = Mathf.MoveTowards(_flight, target, Time.unscaledDeltaTime / _flightSeconds);
            if (_flight <= 0f && _view == View.Surface)
            {
                HandBack();
                return;
            }

            float s = Mathf.SmoothStep(0f, 1f, _flight);
            OrbitPose(out Vector3 orbitPosition, out Quaternion orbitRotation);

            CameraState from = _ground.State;
            Vector3 groundPosition = from.GetFinalPosition();
            Quaternion groundRotation = from.GetFinalOrientation();
            Vector3 centre = _planet.Center;

            Vector3 a = groundPosition - centre, b = orbitPosition - centre;
            float surface = _planet.Radius + _planet.SurfaceGap;
            float altitudeA = Mathf.Max(a.magnitude - surface, 30f), altitudeB = Mathf.Max(b.magnitude - surface, 30f);
            Vector3 direction = Vector3.Slerp(a.normalized, b.normalized, s);
            float distance = surface + Mathf.Exp(Mathf.Lerp(Mathf.Log(altitudeA), Mathf.Log(altitudeB), s));

            Transform eye = _planetCamera.transform;
            eye.position = centre + direction * distance;
            eye.rotation = Quaternion.Slerp(groundRotation, orbitRotation, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.05f, 0.85f, _flight)));
            _eye = eye.position;
            _hasEye = true;

            LensSettings lens = _planetCamera.Lens;
            lens.FieldOfView = Mathf.Lerp(from.Lens.FieldOfView, _fieldOfView, s);
            lens.NearClipPlane = Mathf.Exp(Mathf.Lerp(Mathf.Log(Mathf.Max(from.Lens.NearClipPlane, 0.01f)), Mathf.Log(PlanetNear), s));
            lens.FarClipPlane = Mathf.Exp(Mathf.Lerp(Mathf.Log(Mathf.Max(from.Lens.FarClipPlane, 10f)), Mathf.Log(PlanetFar), s));
            _planetCamera.Lens = lens;
        }

        // Arrived: the planet camera is simply where the orbit says.
        private void PoseOrbit()
        {
            OrbitPose(out Vector3 position, out Quaternion rotation);
            Transform eye = _planetCamera.transform;
            eye.SetPositionAndRotation(position, rotation);
            _eye = position;
            _hasEye = true;
        }

        // Back down: the ground camera is live again and the planet camera lets go.
        private void HandBack()
        {
            _flight = 0f;
            _hasEye = false;
            UseCut();
            _planetCamera.Priority.Value = 0;
            if (_ground != null) _ground.StandbyUpdate = _groundStandby;
            _ground = null;
        }

        private void UseCut()
        {
            if (_brain == null) return;

            if (!_blendOverridden)
            {
                _savedBlend = _brain.DefaultBlend;
                _blendOverridden = true;
            }
            _brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);
            _switchedAt = Time.unscaledTime;
        }

        // The brain goes back to blending as it did before, once the cut has been made.
        private void SettleBlend()
        {
            if (!_blendOverridden || _brain == null) return;
            if (_brain.IsBlending || Time.unscaledTime - _switchedAt < 0.2f) return;

            _brain.DefaultBlend = _savedBlend;
            _blendOverridden = false;
        }

        // ---------------------------------------------------------------- the flat world

        // Works through the switching of the flat world within a small budget of time each frame. Hiding walks the
        // renderers that are on; showing hands back the ones that were hidden. Turning round mid-way is fine: what has
        // been done stays done, and the next pass starts from there.
        private void StepFlatWorld()
        {
            if (!_flatWanted && (_flat == Flat.Shown || _flat == Flat.Showing))
            {
                _toHide.Clear();
                foreach (Transform root in FlatWorldRoots())
                    foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(false))
                        if (renderer.enabled) _toHide.Add(renderer);
                _hideCursor = 0;
                _flat = Flat.Hiding;
            }
            else if (_flatWanted && (_flat == Flat.Hidden || _flat == Flat.Hiding))
            {
                _flat = Flat.Showing;
            }

            if (_flat != Flat.Hiding && _flat != Flat.Showing) return;

            var clock = Stopwatch.StartNew();
            if (_flat == Flat.Hiding)
            {
                while (_hideCursor < _toHide.Count && clock.Elapsed.TotalMilliseconds < BatchMilliseconds)
                {
                    Renderer renderer = _toHide[_hideCursor++];
                    if (renderer == null || !renderer.enabled) continue;
                    renderer.enabled = false;
                    _hidden.Add(renderer);
                }
                if (_hideCursor >= _toHide.Count) { _flat = Flat.Hidden; _toHide.Clear(); }
            }
            else
            {
                while (_hidden.Count > 0 && clock.Elapsed.TotalMilliseconds < BatchMilliseconds)
                {
                    Renderer renderer = _hidden[^1];
                    _hidden.RemoveAt(_hidden.Count - 1);
                    if (renderer != null) renderer.enabled = true;
                }
                if (_hidden.Count == 0) _flat = Flat.Shown;
            }
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
