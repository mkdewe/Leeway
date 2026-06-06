using System;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using Leeway.Creature.Domain;
using UnityEngine;

namespace Leeway.Creature
{
    /// <summary>
    /// Bazowa komórka sieciowa. Trzyma autorytatywny (serwerowy) stan i reguły
    /// jedzenia/wzrostu/śmierci. Wizualizacja (skala) jest w <see cref="CellVisuals"/>,
    /// a stan jest wystawiany na zewnątrz jako właściwości tylko-do-odczytu + czyste
    /// zdarzenia C#, żeby konsumenci (HUD, kamera) nie zależeli od API SyncVar FishNet.
    /// </summary>
    public class CellEntity : NetworkBehaviour
    {
        [SerializeField] private CellEntityConfig _config;
        public CellEntityConfig Config => _config;

        private readonly SyncVar<float> _size = new SyncVar<float>();
        private readonly SyncVar<float> _hp = new SyncVar<float>();
        private readonly SyncVar<bool> _isAlive = new SyncVar<bool>();

        public float Size => _size.Value;
        public float Hp => _hp.Value;
        public bool IsAlive => _isAlive.Value;

        public event Action<float> SizeChanged;
        public event Action<float> HpChanged;
        public event Action<CellEntity> Died;

        /// <summary>Aktualna prędkość ruchu — maleje wraz ze wzrostem komórki.</summary>
        public float CurrentSpeed =>
            CellRules.MoveSpeed(_config.BaseSize, _size.Value, _config.BaseSpeed, _config.MaxSpeed, _config.SpeedFloorRatio);

        public override void OnStartServer()
        {
            base.OnStartServer();
            _size.Value = _config.BaseSize;
            _hp.Value = _config.BaseHp;
            _isAlive.Value = true;
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            _size.OnChange += OnSizeSyncChanged;
            _hp.OnChange += OnHpSyncChanged;
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            _size.OnChange -= OnSizeSyncChanged;
            _hp.OnChange -= OnHpSyncChanged;
        }

        private void OnSizeSyncChanged(float prev, float next, bool asServer) => SizeChanged?.Invoke(next);
        private void OnHpSyncChanged(float prev, float next, bool asServer) => HpChanged?.Invoke(next);

        public bool CanEat(CellEntity other)
        {
            if (other == null || other == this) return false;
            return CellRules.CanEat(_isAlive.Value, other._isAlive.Value, _size.Value, other._size.Value, _config.EatSizeRatio);
        }

        /// <summary>Server-side: próbuje zjeść komórkę, z którą nastąpiła kolizja triggera.</summary>
        [Server]
        public void TryEat(Collider2D other)
        {
            if (other.TryGetComponent<CellEntity>(out var cell))
                Eat(cell);
        }

        [Server]
        public void Eat(CellEntity food)
        {
            if (!CanEat(food)) return;
            GrowBy(CellRules.GrowthAmount(food._size.Value, _config.GrowthPerEat));
            food.Die();
        }

        [Server]
        public void GrowBy(float amount)
        {
            _size.Value = Mathf.Min(_size.Value + amount, _config.MaxSize);
        }

        [Server]
        public void TakeDamage(float damage)
        {
            if (!_isAlive.Value) return;
            _hp.Value = Mathf.Max(0f, _hp.Value - damage);
            if (_hp.Value <= 0f) Die();
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
