using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace Leeway.World
{
    /// <summary>
    /// Server picks the island seed; clients receive only that number and build the same
    /// island locally with <see cref="IslandGenerator"/>. Terrain itself never goes over the wire.
    /// </summary>
    [RequireComponent(typeof(IslandGenerator))]
    public sealed class IslandSeed : NetworkBehaviour
    {
        [Tooltip("0 = random seed for every match. Any other value always builds the same island.")]
        [SerializeField] private int _fixedSeed;

        private readonly SyncVar<int> _seed = new();
        private IslandGenerator _generator;
        private int _generatedSeed;
        // Seed serwera poza SyncVarem: spawner moze poprosic o wyspe, zanim ten obiekt wystartuje w sieci.
        private int _serverSeed;

        public IslandGenerator Generator => _generator != null ? _generator : _generator = GetComponent<IslandGenerator>();
        public int Seed => _seed.Value;

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            _seed.OnChange += OnSeedChanged;
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            _seed.OnChange -= OnSeedChanged;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            ServerEnsureGenerated();
            _seed.Value = _serverSeed;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            BuildIfNeeded(_seed.Value);
        }

        /// <summary>
        /// Generates the island on the server if it isn't yet. Spawners call it before placing
        /// anything on the ground, since NetworkBehaviour start order is not guaranteed.
        /// </summary>
        public void ServerEnsureGenerated()
        {
            if (_serverSeed == 0)
                _serverSeed = _fixedSeed != 0 ? _fixedSeed : Random.Range(1, int.MaxValue);
            BuildIfNeeded(_serverSeed);
            if (IsServerInitialized) _seed.Value = _serverSeed;
        }

        private void OnSeedChanged(int previous, int next, bool asServer)
        {
            if (!asServer) BuildIfNeeded(next);
        }

        private void BuildIfNeeded(int seed)
        {
            if (seed == 0 || (seed == _generatedSeed && Generator.IsGenerated)) return;
            _generatedSeed = seed;
            Generator.Generate(seed);
        }
    }
}
