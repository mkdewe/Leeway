using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Transporting;
using Leeway.Creature.Domain;
using Leeway.CreatureEditor;
using UnityEngine;

namespace Leeway.GamePhases
{
    /// <summary>
    /// Runs the shared editor playground: spawns a creature per player and gives it a deterministic
    /// starter genome.
    /// </summary>
    /// <remarks>
    /// It repeats the pattern from <see cref="CreaturePhaseGameManager"/> — listening to connection
    /// state, a dictionary keyed by <c>ClientId</c>, spawning through <c>ServerManager.Spawn</c>.
    /// </remarks>
    public class CreatureEditorGameManager : NetworkBehaviour
    {
        [Header("Prefabs")]
        [SerializeField] private GameObject _playgroundCreaturePrefab;

        [Header("Data")]
        [SerializeField] private CreaturePartCatalog _catalog;

        [Header("Spawn settings")]
        [Tooltip("Mixed with the ClientId so creatures differ between sessions but repeat within one.")]
        [SerializeField] private int _worldSeed = 20260727;
        [SerializeField] private float _spawnRadius = 6f;
        [SerializeField] private float _spawnHeight = 1.5f;

        private readonly Dictionary<int, NetworkObject> _playerBodies = new();

        public override void OnStartServer()
        {
            base.OnStartServer();
            ServerManager.OnRemoteConnectionState += OnRemoteConnectionState;

            // The host does not always go through OnRemoteConnectionState for its own connection, so we
            // handle already-connected clients explicitly.
            foreach (NetworkConnection connection in ServerManager.Clients.Values)
                SpawnPlayerBody(connection);
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            ServerManager.OnRemoteConnectionState -= OnRemoteConnectionState;

            // Without this the entries survive the server stopping, and after restarting within the
            // same session SpawnPlayerBody decides the player already has a creature — nobody gets a body.
            _playerBodies.Clear();
        }

        private void OnRemoteConnectionState(NetworkConnection conn, RemoteConnectionStateArgs args)
        {
            if (args.ConnectionState == RemoteConnectionState.Started)
                SpawnPlayerBody(conn);
            else if (args.ConnectionState == RemoteConnectionState.Stopped)
                HandlePlayerDisconnected(conn);
        }

        [Server]
        private void SpawnPlayerBody(NetworkConnection conn)
        {
            if (conn == null || _playerBodies.ContainsKey(conn.ClientId)) return;
            if (_playgroundCreaturePrefab == null)
            {
                Debug.LogError("No playground creature prefab — there is nothing to spawn.", this);
                return;
            }

            GameObject instance = Instantiate(_playgroundCreaturePrefab, ResolveSpawnPosition(conn.ClientId), Quaternion.identity);
            var networkObject = instance.GetComponent<NetworkObject>();
            ServerManager.Spawn(networkObject, conn);

            _playerBodies[conn.ClientId] = networkObject;

            if (!instance.TryGetComponent(out CreatureBody body))
            {
                Debug.LogError("The creature prefab has no CreatureBody component.", this);
                return;
            }

            PartRuleSet rules = _catalog != null ? _catalog.BuildRuleSet() : PartRuleSet.Empty;
            CreatureGenome starter = StarterGenomeFactory.Create(conn.ClientId ^ _worldSeed, rules);

            if (!body.ServerSetGenome(starter, out GenomeError error))
                Debug.LogError($"Starter genome rejected ({error}) — check that the part catalog is complete.", this);
        }

        [Server]
        private void HandlePlayerDisconnected(NetworkConnection conn)
        {
            if (!_playerBodies.TryGetValue(conn.ClientId, out NetworkObject networkObject)) return;

            if (networkObject != null && networkObject.IsSpawned)
                networkObject.Despawn();

            _playerBodies.Remove(conn.ClientId);
        }

        /// <summary>Spreads players around a circle so the starting creatures do not intersect.</summary>
        private Vector3 ResolveSpawnPosition(int clientId)
        {
            float angle = clientId * 2.399963f; // the golden angle — even spread without a table of positions
            return new Vector3(Mathf.Cos(angle) * _spawnRadius, _spawnHeight, Mathf.Sin(angle) * _spawnRadius);
        }
    }
}
