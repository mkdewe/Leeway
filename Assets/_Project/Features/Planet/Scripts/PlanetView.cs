using System;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using Leeway.World;
using Leeway.World.Domain;
using UnityEngine;
using UnityEngine.Rendering;

namespace Leeway.Planet
{
    /// <summary>
    /// The whole planet the home island lies on: builds it from the world seed and the three numbers the
    /// player may change - size, share of sea and number of islands - and shows it when the camera has
    /// pulled far enough back.
    /// </summary>
    /// <remarks>
    /// <para>The planet's centre sits straight under the home island, so its north pole is the island and
    /// the flat world played on is a small patch of its surface. The planet is built only when it is first
    /// wanted, not at the start of a match, and rebuilt when its numbers or the world seed change.</para>
    ///
    /// <para>The three numbers are the host's to set: they travel as synced values and every machine builds
    /// the same planet from them and the island seed. A peer that is not the host sees them but cannot
    /// change them; a game that has not started a network at all can edit freely.</para>
    /// </remarks>
    public sealed class PlanetView : NetworkBehaviour
    {
        [Tooltip("The numbers the planet has until the host changes them.")]
        [SerializeField] private PlanetSettings _defaults = new();

        [SerializeField] private IslandSeed _islandSeed;
        [SerializeField] private Material _material;

        [Tooltip("How far under the sea floor of the home island the planet's surface lies at the pole, in metres.")]
        [SerializeField, Min(0f)] private float _surfaceGap = 40f;

        [Tooltip("Subdivision of the planet's mesh. Each step makes four times the facets.")]
        [SerializeField, Range(3, 7)] private int _detail = 6;

        private readonly SyncVar<float> _radius = new();
        private readonly SyncVar<float> _water = new();
        private readonly SyncVar<int> _islands = new();
        private readonly SyncVar<int> _seedOffset = new();

        private GameObject _surface;
        private MeshFilter _filter;
        private MeshRenderer _renderer;
        private PlanetModel _model;
        private PlanetSettings _built;
        private int _builtSeed;
        private bool _visible;
        private readonly PlanetSettings _live = new();

        /// <summary>Raised after the planet has been rebuilt.</summary>
        public event Action Rebuilt;

        public PlanetModel Model => _model;
        public bool IsBuilt => _model != null;
        public bool IsVisible => _visible;
        public float Radius => Current().Radius;

        /// <summary>The planet's centre in the world: straight under the home island.</summary>
        public Vector3 Center
        {
            get
            {
                Vector3 anchor = _islandSeed != null ? _islandSeed.transform.position : Vector3.zero;
                return anchor + Vector3.down * (Current().Radius + _surfaceGap);
            }
        }

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
            _seedOffset.Value = _defaults.SeedOffset;
        }

        private void Awake()
        {
            _surface = new GameObject("PlanetSurface");
            _surface.transform.SetParent(transform, false);
            _filter = _surface.AddComponent<MeshFilter>();
            _renderer = _surface.AddComponent<MeshRenderer>();
            _renderer.sharedMaterial = _material;
            _renderer.shadowCastingMode = ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.enabled = false;
        }

        private void OnDestroy()
        {
            if (_filter != null && _filter.sharedMesh != null) Destroy(_filter.sharedMesh);
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
                _seedOffset.Value = next.SeedOffset;
            }
            else
            {
                _defaults.Radius = next.Radius;
                _defaults.WaterCoverage = next.WaterCoverage;
                _defaults.IslandCount = next.IslandCount;
                _defaults.SeedOffset = next.SeedOffset;
            }
        }

        public void SetVisible(bool visible)
        {
            _visible = visible;
            if (visible) EnsureBuilt();
            _renderer.enabled = visible && IsBuilt;
        }

        /// <summary>Builds the planet if it is missing or out of date. Returns false while the world has no seed yet.</summary>
        public bool EnsureBuilt()
        {
            int seed = _islandSeed != null ? _islandSeed.Seed : 0;
            if (seed == 0) return false;

            PlanetSettings settings = Current();
            if (_model != null && seed == _builtSeed && settings.SameAs(_built)) return true;

            Build(seed, settings);
            return true;
        }

        private void LateUpdate()
        {
            if (_visible && EnsureBuilt()) _renderer.enabled = true;
            if (_model != null) _surface.transform.position = Center;
        }

        private void Build(int seed, PlanetSettings settings)
        {
            float homeRadius = HomeIslandRadius();
            _model = new PlanetModel(settings.Radius, settings.WaterCoverage, settings.IslandCount,
                unchecked(seed + settings.SeedOffset * 7919), homeRadius);

            _filter.sharedMesh = PlanetMeshBuilder.Build(_model, _detail, _filter.sharedMesh);
            _surface.transform.position = Center;

            _built = settings.Clone();
            _builtSeed = seed;
            Rebuilt?.Invoke();
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
