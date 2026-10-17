using System.Collections.Generic;
using FishNet.Object;
using Leeway.Creature.Domain;
using Leeway.CreatureEditor;
using UnityEngine;

namespace Leeway.Combat
{
    /// <summary>
    /// Puts wildlife in the world and keeps a population of it there.
    /// </summary>
    /// <remarks>
    /// <para>It mirrors <c>CreatureEditorGameManager</c> deliberately: spawn through
    /// <c>ServerManager.Spawn</c>, then hand the body a genome with <c>ServerSetGenome</c>. An enemy
    /// is built exactly the way a player's creature is built, from the same catalog, so everything
    /// downstream — stats, legs, skin, physics — needs to know nothing about enemies at all.</para>
    ///
    /// <para>Restocking is on a timer rather than tied to the death itself, because a creature is
    /// despawned some seconds after it dies (<see cref="NpcCreatureAgent"/>), and counting bodies is
    /// the one measure that stays right whether they died, fell out of the world or were never
    /// spawned.</para>
    /// </remarks>
    public class NpcCreatureSpawner : NetworkBehaviour
    {
        [Header("Prefabs")]
        [Tooltip("The enemy body. The same creature prefab as the player's, with an NpcCreatureAgent on it.")]
        [SerializeField] private GameObject _enemyPrefab;

        [Header("Data")]
        [SerializeField] private CreaturePartCatalog _catalog;

        [Header("Population")]
        [SerializeField, Min(0)] private int _count = 3;
        [SerializeField, Min(1f)] private float _spawnRadius = 18f;
        [SerializeField] private float _spawnHeight = 1.5f;

        [Tooltip("How far from the world's centre the first ring of enemies keeps away, so nothing lands on a player.")]
        [SerializeField, Min(0f)] private float _keepClear = 8f;

        [Tooltip("Seconds between checks that the population is still full.")]
        [SerializeField, Min(1f)] private float _restockSeconds = 12f;

        private readonly List<NetworkObject> _spawned = new();
        private float _nextRestock;

        public override void OnStartServer()
        {
            base.OnStartServer();

            for (int i = 0; i < _count; i++) SpawnOne(i);

            _nextRestock = Time.time + _restockSeconds;
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            _spawned.Clear();
        }

        private void Update()
        {
            if (!IsServerInitialized || Time.time < _nextRestock) return;

            _nextRestock = Time.time + _restockSeconds;

            _spawned.RemoveAll(nob => nob == null || !nob.IsSpawned);

            for (int i = _spawned.Count; i < _count; i++) SpawnOne(i);
        }

        [Server]
        private void SpawnOne(int index)
        {
            if (_enemyPrefab == null)
            {
                Debug.LogError("No enemy prefab — there is nothing to spawn.", this);
                return;
            }

            GameObject instance = Instantiate(_enemyPrefab, SpawnPosition(index), Quaternion.identity);

            var networkObject = instance.GetComponent<NetworkObject>();
            if (networkObject == null)
            {
                Debug.LogError("The enemy prefab has no NetworkObject.", this);
                Destroy(instance);
                return;
            }

            ServerManager.Spawn(networkObject);
            _spawned.Add(networkObject);

            if (!instance.TryGetComponent(out CreatureBody body))
            {
                Debug.LogError("The enemy prefab has no CreatureBody.", this);
                return;
            }

            PartRuleSet rules = _catalog != null ? _catalog.BuildRuleSet() : PartRuleSet.Empty;
            CreatureGenome genome = EnemyGenomeLibrary.Snapper(rules);

            if (!body.ServerSetGenome(genome, out GenomeError error))
                Debug.LogError($"The enemy's genome was rejected ({error}) — check the part catalog.", this);
        }

        /// <summary>Spreads the animals around a ring, clear of where players start.</summary>
        private Vector3 SpawnPosition(int index)
        {
            float angle = index * 2.399963f; // the golden angle — an even spread without a table of positions
            float radius = Mathf.Max(_keepClear, _spawnRadius * (0.6f + 0.4f * Mathf.Repeat(index * 0.37f, 1f)));

            return transform.position + new Vector3(Mathf.Cos(angle) * radius, _spawnHeight, Mathf.Sin(angle) * radius);
        }
    }
}
