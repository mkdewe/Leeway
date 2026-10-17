using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Transporting;
using Leeway.Creature;
using Leeway.World;
using UnityEngine;

namespace Leeway.GamePhases
{
    public class CreaturePhaseGameManager : NetworkBehaviour
    {
        [Header("Prefaby")]
        [SerializeField] private GameObject _playerCreaturePrefab;
        [SerializeField] private GameObject _preyCreaturePrefab;
        [SerializeField] private GameObject _predatorCreaturePrefab;
        [SerializeField] private GameObject _foodItemPrefab;

        [Header("Ustawienia spawnu")]
        [SerializeField] private int _foodCount = 25;
        [SerializeField] private int _preyCount = 8;
        [SerializeField] private int _predatorCount = 3;
        [SerializeField] private float _worldRadius = 30f;
        [SerializeField] private float _foodRespawnDelay = 6f;

        [Header("Wyspa (opcjonalnie - bez niej plaska arena jak dotad)")]
        [SerializeField] private IslandSeed _island;
        [Tooltip("Promien od srodka wyspy, w ktorym spawnuja sie jedzenie i stworzenia.")]
        [SerializeField] private float _islandSpawnRadius = 140f;

        private readonly Dictionary<int, NetworkObject> _playerCreatures = new();
        private readonly System.Random _spawnRng = new();

        private bool HasIsland => _island != null && _island.Generator.IsGenerated;

        public override void OnStartServer()
        {
            base.OnStartServer();
            // Teren musi istniec przed spawnem - kolejnosc OnStartServer miedzy obiektami nie jest gwarantowana.
            if (_island != null) _island.ServerEnsureGenerated();
            ServerManager.OnRemoteConnectionState += OnRemoteConnectionState;
            SpawnEnvironment();

            // When the match is started from the main menu, everyone connected before this scene
            // loaded — their OnRemoteConnectionState has already fired, so spawn them here.
            foreach (NetworkConnection connection in ServerManager.Clients.Values)
                SpawnPlayerCreature(connection);
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            ServerManager.OnRemoteConnectionState -= OnRemoteConnectionState;
        }

        private void OnRemoteConnectionState(NetworkConnection conn, RemoteConnectionStateArgs args)
        {
            if (args.ConnectionState == RemoteConnectionState.Started)
                SpawnPlayerCreature(conn);
            else if (args.ConnectionState == RemoteConnectionState.Stopped)
                HandlePlayerDisconnected(conn);
        }

        [Server]
        private void SpawnPlayerCreature(NetworkConnection conn)
        {
            if (conn == null || _playerCreatures.ContainsKey(conn.ClientId)) return;

            Vector3 spawnPos = PlayerSpawnPosition();
            var go = Instantiate(_playerCreaturePrefab, spawnPos, Quaternion.identity);
            var nob = go.GetComponent<NetworkObject>();
            ServerManager.Spawn(nob, conn);

            _playerCreatures[conn.ClientId] = nob;
        }

        [Server]
        private void HandlePlayerDisconnected(NetworkConnection conn)
        {
            if (_playerCreatures.TryGetValue(conn.ClientId, out var nob))
            {
                if (nob != null && nob.IsSpawned)
                    nob.Despawn();
                _playerCreatures.Remove(conn.ClientId);
            }
        }

        [Server]
        private void SpawnEnvironment()
        {
            for (int i = 0; i < _foodCount; i++)
                SpawnFood();

            for (int i = 0; i < _preyCount; i++)
                SpawnPrey();

            for (int i = 0; i < _predatorCount; i++)
                SpawnPredator();
        }

        [Server]
        private void SpawnFood()
        {
            Vector3 pos = WildSpawnPosition();
            var go = Instantiate(_foodItemPrefab, pos, Quaternion.identity);
            var nob = go.GetComponent<NetworkObject>();
            ServerManager.Spawn(nob);

            if (nob.TryGetComponent<FoodItem>(out var food))
                food.Collected += _ => RespawnFoodDelayedAsync().Forget();
        }

        [Server]
        private void SpawnPrey()
        {
            Vector3 pos = WildSpawnPosition();
            var go = Instantiate(_preyCreaturePrefab, pos, Quaternion.identity);
            ServerManager.Spawn(go.GetComponent<NetworkObject>());
        }

        [Server]
        private void SpawnPredator()
        {
            Vector3 pos = WildSpawnPosition();
            var go = Instantiate(_predatorCreaturePrefab, pos, Quaternion.identity);
            ServerManager.Spawn(go.GetComponent<NetworkObject>());
        }

        private Vector3 PlayerSpawnPosition()
        {
            if (!HasIsland) return RandomGroundPosition(_worldRadius * 0.3f);

            IslandGenerator island = _island.Generator;
            float hubRadius = island.Field.Settings.HubRadius;
            return island.RandomLandPoint(_spawnRng, island.HubPosition, hubRadius * 0.6f) + Vector3.up;
        }

        /// <summary>Jedzenie i stworzenia: lad wyspy poza safe hubem (GDD 3.2).</summary>
        private Vector3 WildSpawnPosition()
        {
            if (!HasIsland) return RandomGroundPosition(_worldRadius);

            IslandGenerator island = _island.Generator;
            float keepOut = island.Field.Settings.HubRadius + island.Field.Settings.HubBlend;
            Vector3 point = island.HubPosition;
            for (int attempt = 0; attempt < 10; attempt++)
            {
                point = island.RandomLandPoint(_spawnRng, island.IslandCenter, _islandSpawnRadius);
                Vector3 toHub = point - island.HubPosition;
                toHub.y = 0f;
                if (toHub.magnitude > keepOut) break;
            }
            return point + Vector3.up;
        }

        private Vector3 RandomGroundPosition(float radius)
        {
            Vector2 circle = UnityEngine.Random.insideUnitCircle * radius;
            return new Vector3(circle.x, 1f, circle.y);
        }

        private async UniTaskVoid RespawnFoodDelayedAsync()
        {
            await UniTask.Delay(TimeSpan.FromSeconds(_foodRespawnDelay));
            if (IsServerInitialized)
                SpawnFood();
        }
    }
}
