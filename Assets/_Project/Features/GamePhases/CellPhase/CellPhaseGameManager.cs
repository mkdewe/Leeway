using FishNet.Connection;
using FishNet.Object;
using FishNet.Transporting;
using Leeway.Creature;
using Leeway.Player;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Leeway.GamePhases
{
    public class CellPhaseGameManager : NetworkBehaviour
    {
        [Header("Prefabs")]
        [SerializeField] private GameObject _playerCellPrefab;
        [SerializeField] private GameObject _foodCellPrefab;
        [SerializeField] private GameObject _predatorCellPrefab;

        [Header("Spawn Settings")]
        [SerializeField] private int _foodCount = 40;
        [SerializeField] private int _predatorCount = 6;
        [SerializeField] private float _worldRadius = 25f;
        [SerializeField] private float _foodRespawnDelay = 8f;

        private readonly Dictionary<int, NetworkObject> _playerCells = new();
        private int _currentFoodCount;

        public override void OnStartServer()
        {
            base.OnStartServer();
            ServerManager.OnRemoteConnectionState += OnRemoteConnectionState;
            SpawnNpcCells();
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            ServerManager.OnRemoteConnectionState -= OnRemoteConnectionState;
        }

        private void OnRemoteConnectionState(NetworkConnection conn, RemoteConnectionStateArgs args)
        {
            if (args.ConnectionState == RemoteConnectionState.Started)
                SpawnPlayerCell(conn);
            else if (args.ConnectionState == RemoteConnectionState.Stopped)
                HandlePlayerDisconnected(conn);
        }

        [Server]
        private void SpawnPlayerCell(NetworkConnection conn)
        {
            Vector2 spawnPos = Random.insideUnitCircle * (_worldRadius * 0.4f);
            var go = Instantiate(_playerCellPrefab, (Vector3)spawnPos, Quaternion.identity);
            var nob = go.GetComponent<NetworkObject>();
            ServerManager.Spawn(nob, conn);

            _playerCells[conn.ClientId] = nob;
        }

        [Server]
        private void HandlePlayerDisconnected(NetworkConnection conn)
        {
            if (_playerCells.TryGetValue(conn.ClientId, out var nob))
            {
                if (nob != null && nob.IsSpawned)
                    nob.Despawn();
                _playerCells.Remove(conn.ClientId);
            }
        }

        [Server]
        private void SpawnNpcCells()
        {
            for (int i = 0; i < _foodCount; i++)
                SpawnFood();

            for (int i = 0; i < _predatorCount; i++)
                SpawnPredator();

            _currentFoodCount = _foodCount;
        }

        [Server]
        private void SpawnFood()
        {
            Vector2 pos = Random.insideUnitCircle * _worldRadius;
            var go = Instantiate(_foodCellPrefab, (Vector3)pos, Quaternion.identity);
            var nob = go.GetComponent<NetworkObject>();
            ServerManager.Spawn(nob);

            if (nob.TryGetComponent<CellEntity>(out var cell))
                cell.Died += _ => StartCoroutine(RespawnFoodDelayed());
        }

        [Server]
        private void SpawnPredator()
        {
            Vector2 pos = Random.insideUnitCircle * _worldRadius;
            var go = Instantiate(_predatorCellPrefab, (Vector3)pos, Quaternion.identity);
            ServerManager.Spawn(go.GetComponent<NetworkObject>());
        }

        private IEnumerator RespawnFoodDelayed()
        {
            yield return new WaitForSeconds(_foodRespawnDelay);
            if (IsServerInitialized)
                SpawnFood();
        }
    }
}
