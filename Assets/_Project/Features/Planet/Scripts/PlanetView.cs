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
        private PlanetTerrain _waterTerrain;
        private PlanetPlayableWorld _playable;
        public PlanetPlayableWorld PlayableWorld => _playable;
        private PlanetTerrain _next;
        private PlanetModel _nextModel;
        private PlanetSettings _nextSettings;
        private int _nextSeed;
        private bool _prefetch;
        private PlanetModel _model;
        private PlanetSettings _built;
        private int _builtSeed;
        private bool _visible;
        private UnityEngine.Camera _loadingCamera;
        private int _savedCullingMask;
        public bool SurfacePrepared => _terrain!=null && _terrain.ViewReady && (_waterTerrain==null || _waterTerrain.ViewReady);
        public bool StableGroundReadyAt(Vector3 point) => _terrain!=null && _terrain.StableGroundReadyAt(point);
        public void SetGroundAnchors(System.Collections.Generic.IReadOnlyList<Vector3> points,int level) => _terrain?.SetGroundAnchors(points,level);
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
        public float Radius => _model?.Radius ?? Current().Radius;

        /// <summary>The seed the sky and the planet are made from.</summary>
        public int Seed => _islandSeed != null ? unchecked(_islandSeed.Seed + Current().SeedOffset * 7919) : 0;

        /// <summary>The planet's centre in the world: straight under the home island.</summary>
        public Vector3 Center
        {
            get
            {
                Vector3 anchor = Anchor;
                var patch = _model?.GetPatch(0);
                return patch != null ? anchor - patch.SurfaceVector(patch.Field.HubCenter)
                    : anchor + Vector3.down * Current().Radius;
            }
        }

        /// <summary>The home island: the point of the planet's surface the player stands on.</summary>
        public Vector3 Anchor => _islandSeed != null ? _islandSeed.transform.position : Vector3.zero;

        /// <summary>Metres between the home island's sea level and the planet's surface under it.</summary>
        public float SurfaceGap => 0f;

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
            _surface.SetActive(false);
            var biome = _islandSeed != null ? _islandSeed.Generator.Biome : null;
            _runtimeMaterial = new Material(biome != null && biome.GroundMaterial != null ? biome.GroundMaterial : _material);
            if (Application.isPlaying && _islandSeed != null)
            {
                _playable = gameObject.AddComponent<PlanetPlayableWorld>();
                _playable.Initialize(this, _islandSeed.Generator);
            }
        }

        private void OnDisable() => GatePresentation(false);

        private void OnDestroy()
        {
            GatePresentation(false);
            _terrain?.Dispose();
            _waterTerrain?.Dispose();
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
            bool wasVisible=_visible;
            _visible = (visible || (_playable != null && _playable.IsReady)) && (_playable==null || _playable.IsReady);
            _surface.SetActive(_visible);
            if (!_visible && wasVisible) { _terrain?.Hide(); _waterTerrain?.Hide(); }
        }

        /// <summary>Asks for the fine tiles of the planet before they are needed: the camera is about to leave.</summary>
        public void Prefetch(bool wanted) => _prefetch = wanted;

        /// <summary>How much of the planet is lost in the air's haze: 1 hides it completely, 0 shows its colours.</summary>
        public void SetHaze(float amount)
        {
            _runtimeMaterial.SetFloat(FogAmountId, _playable != null && _playable.IsReady ? 0f : Mathf.Clamp01(amount));
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
            if (generator != null && generator.IsGenerated)
            {
                EnsureBuilt();
                GatePresentation(_playable!=null&&!_playable.IsReady);
            }

            if (_model != null) _surface.transform.position = Center;

            UnityEngine.Camera camera = UnityEngine.Camera.main;
            if (camera == null) return;

            // A new planet is made ready behind the one on show and swapped in when it is whole.
            if (_next != null)
            {
                _next.LimitLevel = PlanetTerrain.MinLevel;
                _next.Update(camera, false);
                if (_next.CoarseReady && (_playable==null || !_playable.IsPreparing)) SwapIn();
            }

            if (_playable != null) _visible = _playable.IsReady;
            _surface.SetActive(_visible);
            // Finish the visible view while hidden; collision alone is not a ready world.
            if (_terrain != null)
            {
                _terrain.LimitLevel = PlanetTerrain.MaxLevel;
                _terrain.Update(camera, _visible);
                if (_waterTerrain != null)
                {
                    _waterTerrain.LimitLevel = PlanetTerrain.MaxLevel;
                    _waterTerrain.Update(camera, _visible);
                }
            }
        }

        public void PublishPreparedSurface()
        {
            SetVisible(true);
            var camera=UnityEngine.Camera.main;
            if(camera!=null){_terrain?.Update(camera,true);_waterTerrain?.Update(camera,true);}
            GatePresentation(false);
        }

        private void GatePresentation(bool loading)
        {
            if(loading)
            {
                var camera=UnityEngine.Camera.main;
                if(camera==null)return;
                if(_loadingCamera==null){_loadingCamera=camera;_savedCullingMask=camera.cullingMask;}
                _loadingCamera.cullingMask=0;
            }
            else if(_loadingCamera!=null)
            {
                _loadingCamera.cullingMask=_savedCullingMask;
                _loadingCamera=null;
            }
        }

        private void SwapIn()
        {
            _terrain?.Dispose();
            _terrain = _next;
            _next = null;

            _model = _nextModel;
            _waterTerrain?.Dispose();
            var waterMaterial = _islandSeed != null ? _islandSeed.Generator.Biome.WaterMaterial : null;
            if (waterMaterial != null) _waterTerrain = new PlanetTerrain(_model, _surface.transform, waterMaterial, water: true);
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
                if (field != null) model.BuildArchipelago(field, modelSeed);
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
            var biome = _islandSeed != null ? _islandSeed.Generator.Biome : null;
            var colours = biome != null ? PlanetPalette.ForBiome(_nextModel, biome) : null;
            _next = new PlanetTerrain(_nextModel, _surface.transform, _runtimeMaterial, colour: colours);
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
