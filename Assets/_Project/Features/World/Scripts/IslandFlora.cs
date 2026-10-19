using Leeway.Gravity;
using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using Leeway.Rendering;
using Leeway.World.Domain;
using UnityEngine;

namespace Leeway.World
{
    /// <summary>
    /// The plants of the island that can be eaten: berry bushes, fruit trees, mushrooms, herbs.
    /// </summary>
    /// <remarks>
    /// <para><b>Nothing is sent but what was picked.</b> The plants themselves are not network objects. The
    /// seed that already builds the island on every machine also fixes where every plant stands (see
    /// <see cref="FloraRules"/> and <c>IslandScatter</c>), so each machine plants its own. What has to be
    /// shared is only which of them are bare, and for how long - a small dictionary of plant number to the
    /// moment it bears fruit again.</para>
    ///
    /// <para><b>The server decides.</b> A creature asks to eat; the server finds the nearest plant in reach
    /// that has fruit, checks the distance itself and hands back what it gave. A doctored client can ask for
    /// nothing it is not standing next to.</para>
    /// </remarks>
    public sealed class IslandFlora : NetworkBehaviour
    {
        [SerializeField] private IslandSeed _island;
        [SerializeField] private FloraSet _set;

        [Tooltip("Seconds a new plant takes to grow to full size when the island appears.")]
        [SerializeField, Min(0.1f)] private float _growSeconds = 1.4f;

        /// <summary>Plant number to the server time at which it bears fruit again. Only bare plants are in it.</summary>
        private readonly SyncDictionary<int, float> _bare = new();

        private sealed class Plant
        {
            public FloraKind Kind;
            public Vector3 Position;
            public GameObject Object;
            public GameObject Fruit;
            public float BornAt;
            public Vector3 FullScale;
            public bool ShowsFruit = true;
        }

        private readonly List<Plant> _plants = new();
        private Transform _container;
        private int _builtForSeed = -1;

        private float Now => TimeManager != null ? (float)TimeManager.Tick * (float)TimeManager.TickDelta : Time.time;

        public int PlantCount => _plants.Count;

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            _bare.OnChange += OnBareChanged;
            if (_island != null) _island.Generator.Generated += OnGenerated;
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            _bare.OnChange -= OnBareChanged;
            if (_island != null) _island.Generator.Generated -= OnGenerated;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            if (_island != null) _island.ServerEnsureGenerated();
            TryBuild();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            TryBuild();
        }

        private void OnDestroy()
        {
            if (_container != null) Destroy(_container.gameObject);
        }

        private void OnGenerated(IslandHeightField field) => TryBuild();

        /// <summary>Plants every plant of the plan, once per island.</summary>
        private void TryBuild()
        {
            if (_island == null || _set == null) return;

            IslandGenerator generator = _island.Generator;
            if (!generator.IsGenerated) return;

            IslandHeightField field = generator.Field;
            if (field.Seed == _builtForSeed) return;

            Clear();
            _builtForSeed = field.Seed;

            _container = new GameObject("IslandFlora").transform;

            List<ScatterPoint> plan = IslandScatter.Plan(field, FloraRules.Rules(), FloraRules.VariantCounts());
            for (int i = 0; i < plan.Count; i++)
            {
                ScatterPoint point = plan[i];
                var kind = (FloraKind)point.Rule;
                GameObject prefab = _set.PrefabFor(kind);
                if (prefab == null) continue;

                Vector3 world = generator.FieldToWorld(point.Position);
                GameObject instance = Instantiate(prefab, world, Quaternion.Euler(0f, point.Yaw, 0f), _container);
                instance.name = FloraRules.Of(kind).Name + " " + i;

                var plant = new Plant
                {
                    Kind = kind,
                    Position = world,
                    Object = instance,
                    Fruit = instance.transform.Find(FloraSet.FruitName)?.gameObject,
                    FullScale = Vector3.one * point.Scale,
                    BornAt = Time.time + (world - generator.IslandCenter).magnitude * 0.012f,
                };

                // Its levels of detail are measured at the size it will have, not the nothing it starts as.
                instance.transform.localScale = plant.FullScale;
                LodConvention.Ensure(instance);
                instance.transform.localScale = Vector3.zero;
                _plants.Add(plant);
            }

            RefreshAllFruit();
        }

        public void BuildArchipelago(PlanetModel model,Vector3 center)
        {
            if(_set==null || model.PatchCount==0)return;
            Clear();
            _builtForSeed=model.GetPatch(0).Field.Seed;
            _container=new GameObject("PlanetFlora").transform;
            for(int island=0;island<model.PatchCount;island++)
            {
                var patch=model.GetPatch(island);
                var plan=IslandScatter.Plan(patch.Field,FloraRules.Rules(),FloraRules.VariantCounts());
                foreach(var point in plan)
                {
                    var kind=(FloraKind)point.Rule;
                    var prefab=_set.PrefabFor(kind);
                    if(prefab==null)continue;
                    Vector3 world=patch.ToWorld(point.Position,center);
                    var instance=Instantiate(prefab,world,patch.RotationAt(point.Position)*Quaternion.Euler(0,point.Yaw,0),_container);
                    instance.name=FloraRules.Of(kind).Name+" "+_plants.Count;
                    var plant=new Plant
                    {
                        Kind=kind,Position=world,Object=instance,
                        Fruit=instance.transform.Find(FloraSet.FruitName)?.gameObject,
                        FullScale=Vector3.one*point.Scale,BornAt=Time.time,ShowsFruit=true
                    };
                    instance.transform.localScale=plant.FullScale;
                    LodConvention.Ensure(instance);
                    _plants.Add(plant);
                }
            }
            RefreshAllFruit();
        }

        private void Clear()
        {
            if (_container != null) Destroy(_container.gameObject);
            _container = null;
            _plants.Clear();
        }

        private void Update()
        {
            // Plants grow in: scale up from nothing, a wave out from the middle of the island.
            foreach (Plant plant in _plants)
            {
                if (plant.Object == null) continue;
                float t = Mathf.Clamp01((Time.time - plant.BornAt) / _growSeconds);
                float eased = t * t * (3f - 2f * t);
                plant.Object.transform.localScale = plant.FullScale * eased;
            }

            if (IsServerInitialized) ServerRegrow();
        }

        /// <summary>Plants whose time has come bear fruit again: they leave the dictionary and everyone sees it.</summary>
        private float _nextRegrowCheck;
        private readonly List<int> _ripe = new();

        private void ServerRegrow()
        {
            if (Time.time < _nextRegrowCheck) return;
            _nextRegrowCheck = Time.time + 1f;

            float now = Now;
            _ripe.Clear();
            foreach (KeyValuePair<int, float> bare in _bare)
                if (now >= bare.Value) _ripe.Add(bare.Key);

            foreach (int index in _ripe) _bare.Remove(index);
        }

        private void OnBareChanged(SyncDictionaryOperation operation, int key, float value, bool asServer)
        {
            if (operation == SyncDictionaryOperation.Complete || operation == SyncDictionaryOperation.Clear) RefreshAllFruit();
            else if (key >= 0 && key < _plants.Count) ShowFruit(_plants[key], operation != SyncDictionaryOperation.Add && operation != SyncDictionaryOperation.Set);
        }

        private void RefreshAllFruit()
        {
            for (int i = 0; i < _plants.Count; i++) ShowFruit(_plants[i], !_bare.ContainsKey(i));
        }

        private static void ShowFruit(Plant plant, bool shown)
        {
            plant.ShowsFruit = shown;
            if (plant.Fruit != null && plant.Fruit.activeSelf != shown) plant.Fruit.SetActive(shown);
        }

        public bool TryGetPlant(int index, out Vector3 position, out FloraKind kind)
        {
            position = default;
            kind = default;
            if (index < 0 || index >= _plants.Count) return false;

            position = _plants[index].Position;
            kind = _plants[index].Kind;
            return true;
        }

        /// <summary>Whether plant <paramref name="index"/> has fruit on it now.</summary>
        public bool IsRipe(int index) => index >= 0 && index < _plants.Count && !_bare.ContainsKey(index);

        /// <summary>
        /// The nearest plant with fruit that a creature standing at <paramref name="position"/> can reach.
        /// </summary>
        public bool TryFindRipe(Vector3 position, out int index, out FloraSpec spec)
        {
            index = -1;
            spec = default;
            float best = float.MaxValue;

            for (int i = 0; i < _plants.Count; i++)
            {
                Plant plant = _plants[i];
                if (_bare.ContainsKey(i)) continue;

                FloraSpec candidate = FloraRules.Of(plant.Kind);
                Vector3 offset = plant.Position - position;
                Vector3 up = GravityWorld.UpAt(position, gameObject.scene);
                float vertical = Mathf.Abs(Vector3.Dot(offset, up));
                offset = Vector3.ProjectOnPlane(offset, up);

                float distance = offset.magnitude;
                if (distance > candidate.Reach || vertical > candidate.Reach + 1.2f || distance >= best) continue;

                best = distance;
                index = i;
                spec = candidate;
            }

            return index >= 0;
        }

        /// <summary>Makes a plant bear fruit again at once - for tests and for whatever wants to restock the island.</summary>
        [Server]
        public void ServerRipen(int index)
        {
            if (_bare.ContainsKey(index)) _bare.Remove(index);
        }

        /// <summary>
        /// Picks the nearest ripe plant within reach: it goes bare until it has grown fruit again.
        /// </summary>
        /// <returns>True when something was picked; <paramref name="spec"/> says what it gave.</returns>
        [Server]
        public bool ServerHarvest(Vector3 position, out FloraSpec spec)
        {
            if (!TryFindRipe(position, out int index, out spec)) return false;

            _bare[index] = Now + spec.RegrowSeconds;
            return true;
        }
    }
}
