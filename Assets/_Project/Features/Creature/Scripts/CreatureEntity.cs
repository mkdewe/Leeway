using System;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using Leeway.Creature.Domain;
using UnityEngine;

namespace Leeway.Creature
{
    /// <summary>
    /// The base networked creature. It holds the authoritative (server-side) HP and food state and
    /// the combat and foraging rules, exposed as read-only properties plus plain C# events, so
    /// consumers (the HUD, the camera) do not depend on FishNet's SyncVar API.
    /// </summary>
    public class CreatureEntity : NetworkBehaviour
    {
        [SerializeField] private CreatureEntityConfig _config;
        public CreatureEntityConfig Config => _config;

        private readonly SyncVar<float> _hp = new SyncVar<float>();
        private readonly SyncVar<float> _food = new SyncVar<float>();
        private readonly SyncVar<bool> _isAlive = new SyncVar<bool>();

        public float Hp => _hp.Value;
        public float Food => _food.Value;
        public bool IsAlive => _isAlive.Value;

        public event Action<float> HpChanged;
        public event Action<float> FoodChanged;
        public event Action<CreatureEntity> Died;

        public virtual float CurrentSpeed => _config.BaseSpeed;

        public override void OnStartServer()
        {
            base.OnStartServer();
            _hp.Value = _config.BaseHp;
            _food.Value = 0f;
            _isAlive.Value = true;
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            _hp.OnChange += OnHpSyncChanged;
            _food.OnChange += OnFoodSyncChanged;
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            _hp.OnChange -= OnHpSyncChanged;
            _food.OnChange -= OnFoodSyncChanged;
        }

        private void OnHpSyncChanged(float prev, float next, bool asServer) => HpChanged?.Invoke(next);
        private void OnFoodSyncChanged(float prev, float next, bool asServer) => FoodChanged?.Invoke(next);

        public bool CanAttack(CreatureEntity other)
        {
            if (other == null || other == this) return false;
            float distance = Vector3.Distance(transform.position, other.transform.position);
            return CreatureRules.CanAttack(_isAlive.Value, other._isAlive.Value, distance, _config.AttackRange);
        }

        [Server]
        public void TryAttack(CreatureEntity target)
        {
            if (!CanAttack(target)) return;
            target.TakeDamage(_config.AttackDamage);
        }

        [Server]
        public void TakeDamage(float damage)
        {
            if (!_isAlive.Value) return;
            _hp.Value = CreatureRules.ApplyDamage(_hp.Value, damage);
            if (_hp.Value <= 0f) Die();
        }

        [Server]
        public void AddFood(float amount)
        {
            if (!_isAlive.Value) return;
            _food.Value = CreatureRules.AddFood(_food.Value, amount, _config.MaxFood);
        }

        /// <summary>Server-side: tries to collect the food that triggered the collision.</summary>
        [Server]
        public void TryCollectFood(Collider other)
        {
            if (other.TryGetComponent<FoodItem>(out var food))
                food.Consume(this);
        }

        [Server]
        public virtual void Die()
        {
            if (!_isAlive.Value) return;
            _isAlive.Value = false;
            Died?.Invoke(this);
            NetworkObject.Despawn();
        }
    }
}
