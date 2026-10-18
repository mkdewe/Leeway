using System;
using System.Threading.Tasks;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using Leeway.World;
using Leeway.World.Domain;
using UnityEngine;
using UnityEngine.Rendering;

namespace Leeway.Planet
{
    /// <summary>
    /// The whole planet the home island lies on: builds it from the world seed and the numbers the
    /// player may change - size, share of sea, number of islands, and the planets and moons of the star
    /// system round it - and shows it when the camera has pulled far enough back.
    /// </summary>
    /// <remarks>
    /// <para>The planet's centre sits straight under the home island, so its north pole is the island and
    /// the flat world played on is a small patch of its surface.</para>
    ///
    /// <para>Sampling a quarter of a million facets takes the better part of a second, so it never happens
    /// on the frame the player asks to look at the planet: the planet is built on a worker thread as soon as the
    /// island is there, and again, in the background, whenever its numbers or the world seed change. The mesh
    /// that is on screen stays until the new one is ready, then is swapped in a single upload.</para>
    ///
    /// <para>The numbers are the host's to set: they travel as synced values and every machine builds
    /// the same planet from them and the island seed. A peer that is not the host sees them but cannot
    /// change them; a game that has not started a network at all can edit freely.</para>
    /// </remarks>
    public sealed class PlanetView : NetworkBehaviour
    {
        private static readonly int FogAmountId = Shader.PropertyToID("_FogAmount");

        [Tooltip("The numbers the planet has until the host changes them.")]
        [SerializeField] private PlanetSettings _defaults = new();

        [SerializeField] private IslandSeed _islandSeed;
        [SerializeField] private Material _material;

        [Tooltip("How far under the sea floor of the home island the planet's surface lies at the pole, in metres.")]
        [SerializeField, Min(0f)] private float _surfaceGap = 3f;

        private readonly SyncVar<float> _radius = new();
        private readonly SyncVar<float> _water = new();
        private readonly SyncVar<int> _islands = new();
        private readonly SyncVar<int> _planets = new();
        private readonly SyncVar<int> _moons = new();
        private readonly SyncVar<int> _seedOffset = new();

        private GameObject _surface;
        private Material _runtimeMaterial;
        private PlanetTerrain _terrain;
        private PlanetTerrain _next;
        private PlanetModel _nextModel;
        private PlanetSettings _nextSettings;
        private int _nextSeed;
        private bool _prefetch;
        private PlanetModel _model;
        private PlanetSettings _built;
        private int _builtSeed;
        private bool _visible;
        private readonly PlanetSettings _live = new();

        // The build on the worker thread, and what it was asked to build.
        private Task<PlanetModel> _job;
        private PlanetSettings _jobSettings;
        private int _jobSeed;

        /// <summary>Raised after the planet has been rebuilt.</summary>
        public event Action Rebuilt;

        public PlanetModel Model => _model;
        public bool IsBuilt => _model != null;
        public bool IsVisible => _visible;

        /// <summary>How many tiles of ground are drawn, and how many are made, for the planet as it is now.</summary>
        public int ShownTiles => _terrain != null ? _terrain.ShownTiles : 0;
        public int ResidentTiles => _terrain != null ? _terrain.ResidentTiles : 0;
        public float Radius => Current().Radius;

        /// <summary>The seed the sky and the planet are made from.</summary>
        public int Seed => _islandSeed != null ? unchecked(_islandSeed.Seed + Current().SeedOffset * 7919) : 0;

        /// <summary>The planet's centre in the world: straight under the home island.</summary>
        public Vector3 Center
        {
            get
            {
                Vector3 anchor = Anchor;
                return anchor + Vector3.down * (Current().Radius + _surfaceGap);
            }
        }

        /// <summary>The home island: the point of the planet's surface the player stands on.</summary>
        public Vector3 Anchor => _islandSeed != null ? _islandSeed.transform.position : Vector3.zero;

        /// <summary>Metres between the home island's sea level and the planet's surface under it.</summary>
        public float SurfaceGap => _surfaceGap;

        /// <summary>False for a client that is not the host.</summary>
        public bool CanEdit => !IsClientInitialized || IsServerInitialized;

        /// <summary>The numbers in force right now.</summary>
        public PlanetSettings Current()
        {
            if ((IsServerInitialized || IsClientInitialized) && _radius.Value > 0f)
            {
                _live.Radius = _radius.Value;
                _live.WaterCoverage = _water.Value;
                _live.IslandCount = _islands.Value;
                _live.PlanetCount = _planets.Value;
                _live.MoonCount = _moons.Value;
                _live.SeedOffset = _seedOffset.Value;
                return _live;
            }

            return _defaults;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            _radius.Value = _defaults.Radius;
            _water.Value = _defaults.WaterCoverage;
            _islands.Value = _defaults.IslandCount;
            _planets.Value = _defaults.PlanetCount;
            _moons.Value = _defaults.MoonCount;
            _seedOffset.Value = _defaults.SeedOffset;
        }

        private void Awake()
        {
            _surface = new GameObject("PlanetSurface");
            _surface.transform.SetParent(transform, false);
            _runtimeMaterial = new Material(_material);
        }

        private void OnDestroy()
        {
            _terrain?.Dispose();
            _next?.Dispose();
            if (_runtimeMaterial != null) Destroy(_runtimeMaterial);
        }

        /// <summary>Changes the planet. Only the host (or a game with no network) may.</summary>
        public bool Apply(float radius, float waterCoverage, int islandCount)
        {
            if (!CanEdit) return false;

            var next = Current().Clone();
            next.Radius = radius;
            next.WaterCoverage = waterCoverage;
            next.IslandCount = islandCount;
            Store(next);
            return true;
        }

        /// <summary>Changes the star system round the planet. Only the host (or a game with no network) may.</summary>
        public bool ApplySystem(int planetCount, int moonCount)
        {
            if (!CanEdit) return false;

            var next = Current().Clone();
            next.PlanetCount = planetCount;
            next.MoonCount = moonCount;
            Store(next);
            return true;
        }

        /// <summary>The same sizes with another arrangement of islands.</summary>
        public bool Reroll()
        {
            if (!CanEdit) return false;

            var next = Current().Clone();
            next.SeedOffset = unchecked(next.SeedOffset + 1);
            Store(next);
            return true;
        }

        private void Store(PlanetSettings next)
        {
            if (IsServerInitialized)
            {
                _radius.Value = next.Radius;
                _water.Value = next.WaterCoverage;
                _islands.Value = next.IslandCount;
                _planets.Value = next.PlanetCount;
                _moons.Value = next.MoonCount;
                _seedOffset.Value = next.SeedOffset;
            }
            else
            {
                _defaults.Radius = next.Radius;
                _defaults.WaterCoverage = next.WaterCoverage;
                _defaults.IslandCount = next.IslandCount;
                _defaults.PlanetCount = next.PlanetCount;
                _defaults.MoonCount = next.MoonCount;
                _defaults.SeedOffset = next.SeedOffset;
            }
        }

        public void SetVisible(bool visible)
        {
            if (_visible && !visible) _terrain?.Hide();
            _visible = visible;
        }

        /// <summary>Asks for the fine tiles of the planet before they are needed: the camera is about to leave.</summary>
        public void Prefetch(bool wanted) => _prefetch = wanted;

        /// <summary>How much of the planet is lost in the air's haze: 1 hides it completely, 0 shows its colours.</summary>
        public void SetHaze(float amount)
        {
            _runtimeMaterial.SetFloat(FogAmountId, Mathf.Clamp01(amount));
        }

        /// <summary>
        /// Makes sure a planet matching the current numbers is built or on its way. Returns false while the
        /// world has no seed yet; true does not mean the mesh is ready - see <see cref="IsBuilt"/>.
        /// </summary>
        public bool EnsureBuilt()
        {
            int seed = _islandSeed != null ? _islandSeed.Seed : 0;
            if (seed == 0) return false;

            PlanetSettings settings = Current();
            bool upToDate = _model != null && seed == _builtSeed && settings.SameAs(_built);
            bool inProgress = (_job != null && seed == _jobSeed && settings.SameAs(_jobSettings))
                || (_next != null && seed == _nextSeed && settings.SameAs(_nextSettings));

            // One build at a time: a newer request waits for the one that is running.
            if (!upToDate && !inProgress && _job == null) Start(seed, settings);
            return true;
        }

        private void LateUpdate()
        {
            Collect();

            // Built as soon as the island is, so that the player never waits for it.
            IslandGenerator generator = _islandSeed != null ? _islandSeed.Generator : null;
            if (generator != null && generator.IsGenerated) EnsureBuilt();

            if (_model != null) _surface.transform.position = Center;

            UnityEngine.Camera camera = UnityEngine.Camera.main;
            if (camera == null) return;

            // A new planet is made ready behind the one on show and swapped in when it is whole.
            if (_next != null)
            {
                _next.LimitLevel = PlanetTerrain.MinLevel;
                _next.Update(camera, false);
                if (_next.CoarseReady) SwapIn();
            }

            if (_terrain != null && (_visible || _prefetch))
            {
                _terrain.LimitLevel = PlanetTerrain.MaxLevel;
                _terrain.Update(camera, _visible);
            }
        }

        private void SwapIn()
        {
            _terrain?.Dispose();
            _terrain = _next;
            _next = null;

            _model = _nextModel;
            _built = _nextSettings;
            _builtSeed = _nextSeed;
            _surface.transform.position = Center;
            Rebuilt?.Invoke();
        }

        private void Start(int seed, PlanetSettings settings)
        {
            float homeRadius = HomeIslandRadius();
            PlanetSettings snapshot = settings.Clone();
            int modelSeed = unchecked(seed + snapshot.SeedOffset * 7919);

            // The real ground of the home island, so that from above it is the island the creature walks on.
            IslandGenerator generator = _islandSeed != null ? _islandSeed.Generator : null;
            IslandHeightField field = generator != null ? generator.Field : null;

            _jobSeed = seed;
            _jobSettings = snapshot;
            _job = Task.Run(() =>
            {
                var model = new PlanetModel(snapshot.Radius, snapshot.WaterCoverage, snapshot.IslandCount, modelSeed, homeRadius);
                if (field != null) model.SetHome((x, z) => field.SampleHeight(x, z) - field.Settings.SeaLevel, field.Settings.HalfSize * 0.95f);
                return model;
            });
        }

        // Takes the finished model off the worker thread and starts making its ground.
        private void Collect()
        {
            if (_job == null || !_job.IsCompleted) return;

            Task<PlanetModel> job = _job;
            _job = null;

            if (job.IsFaulted)
            {
                Debug.LogException(job.Exception.GetBaseException());
                return;
            }

            // The numbers may have moved on while it was building; then it is not used, and the next one starts.
            if (!_jobSettings.SameAs(Current()) || _jobSeed != (_islandSeed != null ? _islandSeed.Seed : 0)) return;

            _next?.Dispose();
            _nextModel = job.Result;
            _nextSettings = _jobSettings;
            _nextSeed = _jobSeed;
            _next = new PlanetTerrain(_nextModel, _surface.transform, _runtimeMaterial);
        }

        // The playable island's real reach, so the planet's home island matches what the player stands on.
        private float HomeIslandRadius()
        {
            IslandGenerator generator = _islandSeed != null ? _islandSeed.Generator : null;
            var shape = generator != null && generator.Biome != null ? generator.Biome.Shape : null;
            return shape != null ? shape.CoastRadius * shape.HalfSize * 1.15f : 190f;
        }
    }
}
