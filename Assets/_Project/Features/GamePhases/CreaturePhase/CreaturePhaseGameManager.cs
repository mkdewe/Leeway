using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Transporting;
using Leeway.Creature;
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

        private readonly Dictionary<int, NetworkObject> _playerCreatures = new();

        public override void OnStartServer()
        {
            base.OnStartServer();
            ServerManager.OnRemoteConnectionState += OnRemoteConnectionState;
            SpawnEnvironment();
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
            Vector3 spawnPos = RandomGroundPosition(_worldRadius * 0.3f);
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
            Vector3 pos = RandomGroundPosition(_worldRadius);
            var go = Instantiate(_foodItemPrefab, pos, Quaternion.identity);
            var nob = go.GetComponent<NetworkObject>();
            ServerManager.Spawn(nob);

            if (nob.TryGetComponent<FoodItem>(out var food))
                food.Collected += _ => RespawnFoodDelayedAsync().Forget();
        }

        [Server]
        private void SpawnPrey()
        {
            Vector3 pos = RandomGroundPosition(_worldRadius);
            var go = Instantiate(_preyCreaturePrefab, pos, Quaternion.identity);
            ServerManager.Spawn(go.GetComponent<NetworkObject>());
        }

        [Server]
        private void SpawnPredator()
        {
            Vector3 pos = RandomGroundPosition(_worldRadius);
            var go = Instantiate(_predatorCreaturePrefab, pos, Quaternion.identity);
            ServerManager.Spawn(go.GetComponent<NetworkObject>());
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
