using Leeway.World.Domain;
using Leeway.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace Leeway.Planet
{
    /// <summary>
    /// The star system as seen from the planet: the star, the other planets going round it and the moons
    /// going round ours, placed from a <see cref="StarSystemModel"/> and the system clock.
    /// </summary>
    /// <remarks>
    /// <para>One rotation ties the system to the sky of the island. The day and night of the island decide
    /// where the star stands; the system is turned about the sun's path so that its star stands there, and
    /// everything else - the other planets, the moons, the fixed stars of the sky shader - turns with it.
    /// That is the planet turning on its axis, seen from the ground and from space alike.</para>
    ///
    /// <para>The star, the other planets and the stars are far too distant to be real: they are drawn on a
    /// sphere round the camera, at the right direction and a size that shows them, so they can neither be
    /// walked round nor be hidden behind the planet's own far clip. Our moons are the exception - they are
    /// near enough to pass in front of the planet and are placed in the world.</para>
    ///
    /// <para>The first moon is also the moon of the island's sky: while the camera is in the air the sky
    /// shader draws a disc where this moon is, and from space the moon itself takes over from the same
    /// place and the same size.</para>
    /// </remarks>
    [DefaultExecutionOrder(200)]
    public sealed class SpaceBackdrop : MonoBehaviour
    {
        private static readonly int SunDirectionId = Shader.PropertyToID("_LeewaySunDirection");
        private static readonly int MoonDirectionId = Shader.PropertyToID("_LeewayMoonDirection");
        private static readonly int MoonSizeId = Shader.PropertyToID("_LeewayMoonSize");
        private static readonly int SkyRotationId = Shader.PropertyToID("_LeewaySkyRotation");
        private static readonly int SpaceId = Shader.PropertyToID("_LeewaySpace");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int AccentId = Shader.PropertyToID("_Accent");
        private static readonly int BandsId = Shader.PropertyToID("_Bands");
        private static readonly int LightDirId = Shader.PropertyToID("_LightDir");
        private static readonly int GlowId = Shader.PropertyToID("_Glow");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [SerializeField] private PlanetView _planet;
        [SerializeField] private Material _bodyMaterial;
        [SerializeField] private Material _glowMaterial;

        [Header("Look")]
        [Tooltip("How big a planet of our planet's size looks from one AU away, as an angle in radians.")]
        [SerializeField, Range(0.005f, 0.1f)] private float _planetAngle = 0.035f;
        [Tooltip("Angular radius of the star from the ground, and from space.")]
        [SerializeField] private Vector2 _starAngles = new(0.016f, 0.07f);
        [SerializeField] private Color _starColour = new(1f, 0.84f, 0.55f);
        [Tooltip("The distant bodies are drawn only once the camera's far plane reaches this far.")]
        [SerializeField, Min(1000f)] private float _minFar = 15000f;

        private sealed class Body
        {
            public Transform Transform;
            public Material Material;
        }

        private float _space;
        private StarSystemModel _system;
        private PlanetSettings _systemSettings;
        private int _systemSeed;
        private Transform _root;
        private Body _star;
        private Transform _glow;
        private Material _glowInstance;
        private Body[] _planets = System.Array.Empty<Body>();
        private Body[] _moons = System.Array.Empty<Body>();
        private Mesh _sphere, _quad;
        private IslandWind _wind;
        private Vector3 _ecliptic;
        private bool _shown;

        /// <summary>The system as it is now, once the world has a seed.</summary>
        public StarSystemModel Model => _system;

        /// <summary>0 on the ground, 1 outside the air; set by whoever moves the camera.</summary>
        public float Space
        {
            get => _space;
            set => _space = Mathf.Clamp01(value);
        }

        /// <summary>Where the first moon is, seen from the home island.</summary>
        public Vector3 MoonDirection { get; private set; } = Vector3.down;

        private void Awake()
        {
            Vector3 dawn = SolarCycle.Evaluate(0, 6f, 24f).SunDirection;
            Vector3 noon = SolarCycle.Evaluate(0, 12f, 24f).SunDirection;
            _ecliptic = Vector3.Cross(dawn, noon).normalized;
        }

        private void OnDestroy()
        {
            Teardown();
            Shader.SetGlobalFloat(SpaceId, 0f);
            Shader.SetGlobalMatrix(SkyRotationId, Matrix4x4.zero);
            Shader.SetGlobalFloat(MoonSizeId, 0f);
            if (_sphere != null) Destroy(_sphere);
            if (_quad != null) Destroy(_quad);
        }

        private void LateUpdate()
        {
            Shader.SetGlobalFloat(SpaceId, _space);
            if (_planet == null) return;

            int seed = _planet.Seed;
            if (seed == 0) return;
            EnsureSystem(seed, _planet.Current());

            Vector4 sunVector = Shader.GetGlobalVector(SunDirectionId);
            Vector3 sun = new Vector3(sunVector.x, sunVector.y, sunVector.z);
            if (sun.sqrMagnitude < 0.5f) return;
            sun.Normalize();

            double seconds = Clock();
            Vector3 home = _system.PlanetPosition(_system.HomeIndex, seconds);
            Vector3 towardsStar = -home.normalized;
            Quaternion turn = Quaternion.LookRotation(sun, _ecliptic) * Quaternion.Inverse(Quaternion.LookRotation(towardsStar, Vector3.up));
            Shader.SetGlobalMatrix(SkyRotationId, Matrix4x4.Rotate(Quaternion.Inverse(turn)));

            Vector3 centre = _planet.Center;
            float radius = _planet.Radius;
            PlaceMoons(turn, seconds, centre, radius, sun);

            UnityEngine.Camera cam = UnityEngine.Camera.main;
            bool show = cam != null && _planet.IsVisible && _space > 0.05f && cam.farClipPlane >= _minFar;
            if (show != _shown)
            {
                _shown = show;
                _root.gameObject.SetActive(show);
            }
            if (!show) return;

            PlaceDistant(cam, turn, seconds, home, sun);
        }

        private double Clock()
        {
            if (_wind == null) _wind = FindAnyObjectByType<IslandWind>();
            return _wind != null ? _wind.WeatherTime : Time.timeAsDouble;
        }

        // ---------------------------------------------------------------- placing

        private void PlaceMoons(Quaternion turn, double seconds, Vector3 centre, float radius, Vector3 sun)
        {
            Vector3 anchor = _planet.Anchor;
            if (_moons.Length == 0)
            {
                MoonDirection = Vector3.down;
                Shader.SetGlobalVector(MoonDirectionId, MoonDirection);
                Shader.SetGlobalFloat(MoonSizeId, 0.01f);
                return;
            }

            for (int i = 0; i < _moons.Length; i++)
            {
                StarSystemModel.Moon moon = _system.Moons[i];
                Vector3 position = centre + turn * _system.MoonOffset(i, seconds) * radius;
                Transform t = _moons[i].Transform;
                t.position = position;
                t.localScale = Vector3.one * (moon.Size * radius * 2f);
                _moons[i].Material.SetVector(LightDirId, sun);

                if (i != 0) continue;
                Vector3 fromIsland = position - anchor;
                MoonDirection = fromIsland.normalized;
                Shader.SetGlobalVector(MoonDirectionId, MoonDirection);
                Shader.SetGlobalFloat(MoonSizeId, Mathf.Atan2(moon.Size * radius, fromIsland.magnitude));
            }
        }

        private void PlaceDistant(UnityEngine.Camera cam, Quaternion turn, double seconds, Vector3 home, Vector3 sun)
        {
            Vector3 eye = cam.transform.position;
            float far = cam.farClipPlane * 0.9f;

            float starAngle = Mathf.Lerp(_starAngles.x, _starAngles.y, Mathf.SmoothStep(0f, 1f, _space));
            _star.Transform.position = eye + sun * far;
            _star.Transform.localScale = Vector3.one * (2f * far * Mathf.Tan(starAngle));

            _glow.position = eye + sun * far * 0.99f;
            _glow.rotation = Quaternion.LookRotation(_glow.position - eye);
            _glow.localScale = Vector3.one * (2f * far * Mathf.Tan(starAngle * 4.5f));
            _glowInstance.SetColor(ColorId, _starColour * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.05f, 0.5f, _space)));

            for (int i = 0, k = 0; i < _system.Planets.Count; i++)
            {
                if (i == _system.HomeIndex) continue;
                Body body = _planets[k++];

                Vector3 position = _system.PlanetPosition(i, seconds);
                Vector3 offset = turn * (position - home);
                float distance = Mathf.Max(0.05f, offset.magnitude);
                float angle = Mathf.Clamp(_planetAngle * _system.Planets[i].Size / distance, 0.0022f, 0.13f);

                body.Transform.position = eye + offset / distance * (far * 0.98f);
                body.Transform.localScale = Vector3.one * (2f * far * 0.98f * Mathf.Tan(angle));
                body.Material.SetVector(LightDirId, turn * -position.normalized);
            }
        }

        // ---------------------------------------------------------------- building

        private void EnsureSystem(int seed, PlanetSettings settings)
        {
            if (_system != null && seed == _systemSeed && settings.SameSystemAs(_systemSettings)) return;

            Teardown();
            _systemSeed = seed;
            _systemSettings = settings.Clone();
            _system = new StarSystemModel(settings.PlanetCount, settings.MoonCount, unchecked(seed + settings.SeedOffset * 7919));

            _root = new GameObject("StarSystem").transform;
            _root.SetParent(transform, false);

            if (_sphere == null) _sphere = PrimitiveMesh(PrimitiveType.Sphere);
            if (_quad == null) _quad = PrimitiveMesh(PrimitiveType.Quad);

            _star = MakeBody("Star", _starColour, _starColour, 0f, 1f);

            var glow = new GameObject("StarGlow");
            glow.transform.SetParent(_root, false);
            glow.AddComponent<MeshFilter>().sharedMesh = _quad;
            var glowRenderer = glow.AddComponent<MeshRenderer>();
            _glowInstance = new Material(_glowMaterial);
            glowRenderer.sharedMaterial = _glowInstance;
            Unlit(glowRenderer);
            _glow = glow.transform;

            _planets = new Body[_system.Planets.Count - 1];
            for (int i = 0, k = 0; i < _system.Planets.Count; i++)
            {
                if (i == _system.HomeIndex) continue;
                StarSystemModel.Planet p = _system.Planets[i];
                _planets[k++] = MakeBody("Planet " + i, p.Colour, p.Accent, p.Kind == StarSystemModel.Kind.Gas ? 5f + (i % 3) * 2f : 0f, 0f);
            }

            _moons = new Body[_system.Moons.Count];
            for (int i = 0; i < _moons.Length; i++)
                _moons[i] = MakeBody("Moon " + i, _system.Moons[i].Colour, _system.Moons[i].Colour * 0.78f, 0f, 0f);

            // The moons are placed in the world, near enough to be seen up close.
            _root.gameObject.SetActive(_shown);
        }

        private Body MakeBody(string name, Color colour, Color accent, float bands, float glow)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            go.AddComponent<MeshFilter>().sharedMesh = _sphere;
            var renderer = go.AddComponent<MeshRenderer>();

            var material = new Material(_bodyMaterial);
            material.SetColor(BaseColorId, colour);
            material.SetColor(AccentId, accent);
            material.SetFloat(BandsId, bands);
            material.SetFloat(GlowId, glow);
            renderer.sharedMaterial = material;
            Unlit(renderer);

            return new Body { Transform = go.transform, Material = material };
        }

        private static void Unlit(Renderer renderer)
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        private static Mesh PrimitiveMesh(PrimitiveType type)
        {
            GameObject temp = GameObject.CreatePrimitive(type);
            Mesh mesh = Instantiate(temp.GetComponent<MeshFilter>().sharedMesh);
            Destroy(temp);
            return mesh;
        }

        private void Teardown()
        {
            if (_root != null) Destroy(_root.gameObject);
            foreach (Body body in _planets) if (body.Material != null) Destroy(body.Material);
            foreach (Body body in _moons) if (body.Material != null) Destroy(body.Material);
            if (_star != null && _star.Material != null) Destroy(_star.Material);
            if (_glowInstance != null) Destroy(_glowInstance);
            _planets = System.Array.Empty<Body>();
            _moons = System.Array.Empty<Body>();
            _star = null;
            _system = null;
        }
    }
}
