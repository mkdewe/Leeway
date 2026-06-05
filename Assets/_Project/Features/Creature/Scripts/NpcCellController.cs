using FishNet.Object;
using Leeway.Creature.AI;
using UnityEngine;

namespace Leeway.Creature
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class NpcCellController : CellEntity
    {
        public enum NpcType { Food, Predator }

        [SerializeField] private NpcType _npcType = NpcType.Food;
        [SerializeField] private float _chaseRadius = 8f;
        [SerializeField] private float _fleeRadius = 5f;
        [SerializeField] private float _wanderRadius = 6f;
        [SerializeField] private float _directionChangeInterval = 2.5f;

        private Rigidbody2D _rb;
        private INpcBehavior _behavior;
        private Vector2 _wanderTarget;
        private float _nextDirectionChange;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            _behavior = NpcBehaviorFactory.Create(_npcType);
            SetNewWanderTarget();
        }

        private void FixedUpdate()
        {
            if (!IsServerInitialized) return;
            UpdateAI();
        }

        private void UpdateAI()
        {
            if (Time.time > _nextDirectionChange)
            {
                SetNewWanderTarget();
                _nextDirectionChange = Time.time + _directionChangeInterval + Random.Range(-0.8f, 0.8f);
            }

            ScanSurroundings(out CellEntity closestFood, out CellEntity closestThreat);

            Vector2 target = _behavior.DecideTarget(this, closestFood, closestThreat, _wanderTarget);
            ApplyMovement(target);
        }

        private void ScanSurroundings(out CellEntity closestFood, out CellEntity closestThreat)
        {
            closestFood = null;
            closestThreat = null;
            float closestFoodDist = float.MaxValue;
            float closestThreatDist = float.MaxValue;

            Collider2D[] nearby = Physics2D.OverlapCircleAll(transform.position, _chaseRadius);
            foreach (var col in nearby)
            {
                if (col.gameObject == gameObject) continue;
                if (!col.TryGetComponent<CellEntity>(out var entity)) continue;
                if (!entity.IsAlive) continue;

                float dist = Vector2.Distance(transform.position, entity.transform.position);

                if (CanEat(entity) && dist < closestFoodDist)
                {
                    closestFood = entity;
                    closestFoodDist = dist;
                }
                else if (entity.CanEat(this) && dist < _fleeRadius && dist < closestThreatDist)
                {
                    closestThreat = entity;
                    closestThreatDist = dist;
                }
            }
        }

        private void ApplyMovement(Vector2 target)
        {
            Vector2 dir = target - _rb.position;
            if (dir.magnitude < 0.1f) return;

            _rb.linearVelocity = Vector2.Lerp(_rb.linearVelocity, dir.normalized * CurrentSpeed, Time.fixedDeltaTime * Config.Drag);
        }

        private void SetNewWanderTarget()
        {
            _wanderTarget = (Vector2)transform.position + Random.insideUnitCircle * _wanderRadius;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (IsServerInitialized)
                TryEat(other);
        }
    }
}
