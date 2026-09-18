using Leeway.Creature.Domain;
using Leeway.Player;
using UnityEngine;

namespace Leeway.Creature
{
    [RequireComponent(typeof(Rigidbody))]
    public class NpcCreatureController : CreatureEntity
    {
        [SerializeField] private NpcCreatureType _npcType = NpcCreatureType.Prey;
        [SerializeField] private float _senseRadius = 10f;
        [SerializeField] private float _fleeRadius = 6f;
        [SerializeField] private float _wanderRadius = 8f;
        [SerializeField] private float _directionChangeInterval = 3f;
        [SerializeField] private float _attackCooldown = 1.5f;

        public NpcCreatureType NpcType => _npcType;

        private Rigidbody _rb;
        private INpcCreatureBehavior _behavior;
        private Vector3 _wanderTarget;
        private float _nextDirectionChange;
        private float _nextAttackTime;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            _behavior = NpcCreatureBehaviorFactory.Create(_npcType);
            SetNewWanderTarget();
        }

        private void FixedUpdate()
        {
            if (!IsServerInitialized || !IsAlive) return;
            UpdateAI();
        }

        private void UpdateAI()
        {
            if (Time.time > _nextDirectionChange)
            {
                SetNewWanderTarget();
                _nextDirectionChange = Time.time + _directionChangeInterval + Random.Range(-0.8f, 0.8f);
            }

            ScanSurroundings(out CreatureEntity closestTarget, out CreatureEntity closestThreat);

            var perception = new CreaturePerception(
                selfPosition: transform.position,
                fleeDistance: Config.FleeDistance,
                hasThreat: closestThreat != null,
                threatPosition: closestThreat != null ? closestThreat.transform.position : Vector3.zero,
                hasTarget: closestTarget != null,
                targetPosition: closestTarget != null ? closestTarget.transform.position : Vector3.zero,
                wanderTarget: _wanderTarget);

            ApplyMovement(_behavior.DecideTarget(in perception));

            if (_npcType == NpcCreatureType.Predator && closestTarget != null && Time.time >= _nextAttackTime && CanAttack(closestTarget))
            {
                _nextAttackTime = Time.time + _attackCooldown;
                TryAttack(closestTarget);
            }
        }

        private void ScanSurroundings(out CreatureEntity closestTarget, out CreatureEntity closestThreat)
        {
            closestTarget = null;
            closestThreat = null;
            float closestTargetDist = float.MaxValue;
            float closestThreatDist = float.MaxValue;

            Collider[] nearby = Physics.OverlapSphere(transform.position, _senseRadius);
            foreach (var col in nearby)
            {
                if (col.gameObject == gameObject) continue;
                if (!col.TryGetComponent<CreatureEntity>(out var entity)) continue;
                if (!entity.IsAlive) continue;

                float dist = Vector3.Distance(transform.position, entity.transform.position);

                if (_npcType == NpcCreatureType.Predator && IsHuntable(entity) && dist < closestTargetDist)
                {
                    closestTarget = entity;
                    closestTargetDist = dist;
                }
                else if (_npcType == NpcCreatureType.Prey && IsDangerous(entity) && dist < _fleeRadius && dist < closestThreatDist)
                {
                    closestThreat = entity;
                    closestThreatDist = dist;
                }
            }
        }

        private static bool IsHuntable(CreatureEntity entity)
            => entity is PlayerCreatureController || (entity is NpcCreatureController npc && npc.NpcType == NpcCreatureType.Prey);

        private static bool IsDangerous(CreatureEntity entity)
            => entity is PlayerCreatureController || (entity is NpcCreatureController npc && npc.NpcType == NpcCreatureType.Predator);

        private void ApplyMovement(Vector3 targetPos)
        {
            Vector3 toTarget = targetPos - transform.position;
            toTarget.y = 0f;
            if (toTarget.magnitude < 0.15f) return;

            Vector3 moveDir = toTarget.normalized;
            Vector3 targetVelocity = moveDir * CurrentSpeed;
            targetVelocity.y = _rb.linearVelocity.y;
            _rb.linearVelocity = Vector3.Lerp(_rb.linearVelocity, targetVelocity, Time.fixedDeltaTime * Config.Drag);

            Quaternion lookRot = Quaternion.LookRotation(moveDir, Vector3.up);
            _rb.MoveRotation(Quaternion.Slerp(_rb.rotation, lookRot, Time.fixedDeltaTime * Config.RotationSpeed));
        }

        private void SetNewWanderTarget()
        {
            Vector2 offset = Random.insideUnitCircle * _wanderRadius;
            _wanderTarget = transform.position + new Vector3(offset.x, 0f, offset.y);
        }
    }
}
