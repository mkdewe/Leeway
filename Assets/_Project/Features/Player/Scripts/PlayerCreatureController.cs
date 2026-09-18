using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Transporting;
using Leeway.Creature;
using MessagePipe;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Leeway.Player
{
    public struct CreatureMoveData : IReplicateData
    {
        public float Forward;
        public float Strafe;
        public float Yaw;

        private uint _tick;
        public void Dispose() { }
        public uint GetTick() => _tick;
        public void SetTick(uint value) => _tick = value;
    }

    public struct CreatureReconcileData : IReconcileData
    {
        public Vector3 Position;
        public Vector3 Velocity;
        public float Yaw;

        private uint _tick;
        public void Dispose() { }
        public uint GetTick() => _tick;
        public void SetTick(uint value) => _tick = value;
    }

    [RequireComponent(typeof(Rigidbody))]
    public class PlayerCreatureController : CreatureEntity
    {
        [SerializeField] private float _lookSensitivity = 3f;

        private Rigidbody _rb;
        private Vector2 _moveInput;
        private float _yaw;
        private float _nextAttackTime;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            TimeManager.OnTick += OnTick;
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            TimeManager.OnTick -= OnTick;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (IsOwner)
            {
                _yaw = transform.eulerAngles.y;
                PublishLocalCreature(this);
            }
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            if (IsOwner)
                PublishLocalCreature(null);
        }

        private static void PublishLocalCreature(CreatureEntity creature)
        {
            if (GlobalMessagePipe.IsInitialized)
                GlobalMessagePipe.GetPublisher<LocalCreatureChangedMessage>().Publish(new LocalCreatureChangedMessage(creature));
        }

        private void Update()
        {
            if (!IsOwner) return;

            var keyboard = Keyboard.current;
            var mouse = Mouse.current;

            float forward = 0f;
            float strafe = 0f;
            if (keyboard != null)
            {
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) forward += 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) forward -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) strafe += 1f;
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) strafe -= 1f;
            }
            _moveInput = new Vector2(strafe, forward);

            if (mouse != null)
            {
                _yaw += mouse.delta.x.ReadValue() * _lookSensitivity * 0.1f;

                if (mouse.leftButton.wasPressedThisFrame)
                    TryRequestAttack();
            }
        }

        private void TryRequestAttack()
        {
            if (Time.time < _nextAttackTime) return;
            _nextAttackTime = Time.time + Config.AttackCooldown;
            RequestAttack();
        }

        [ServerRpc]
        private void RequestAttack()
        {
            if (!IsAlive) return;

            Vector3 origin = transform.position + transform.forward * 0.6f + Vector3.up * 0.5f;
            Collider[] hits = Physics.OverlapSphere(origin, Config.AttackRange);
            CreatureEntity best = null;
            float bestDist = float.MaxValue;
            foreach (var hit in hits)
            {
                if (hit.gameObject == gameObject) continue;
                if (!hit.TryGetComponent<CreatureEntity>(out var target) || !target.IsAlive) continue;
                float dist = Vector3.Distance(transform.position, target.transform.position);
                if (dist < bestDist) { best = target; bestDist = dist; }
            }
            if (best != null) TryAttack(best);
        }

        private void OnTick()
        {
            // The reconcile state is produced solely in CreateReconcile. Called from here it would go
            // to Reconcile_Server and broadcast a zeroed state as the authoritative one.
            if (IsOwner)
            {
                Move(new CreatureMoveData { Forward = _moveInput.y, Strafe = _moveInput.x, Yaw = _yaw });
            }
            else if (IsServerInitialized)
            {
                Move(default);
            }
        }

        public override void CreateReconcile()
        {
            Reconciliation(new CreatureReconcileData
            {
                Position = _rb.position,
                Velocity = _rb.linearVelocity,
                Yaw = _yaw
            });
        }

        [Replicate]
        private void Move(CreatureMoveData md, ReplicateState state = ReplicateState.Invalid, Channel channel = Channel.Unreliable)
        {
            float delta = (float)TimeManager.TickDelta;

            Quaternion targetRot = Quaternion.Euler(0f, md.Yaw, 0f);
            Vector3 forward = targetRot * Vector3.forward;
            Vector3 right = targetRot * Vector3.right;
            Vector3 moveDir = forward * md.Forward + right * md.Strafe;
            if (moveDir.sqrMagnitude > 1f) moveDir.Normalize();

            Vector3 targetVelocity = moveDir * CurrentSpeed;
            targetVelocity.y = _rb.linearVelocity.y;

            _rb.linearVelocity = Vector3.Lerp(_rb.linearVelocity, targetVelocity, delta * Config.Drag);
            _rb.MoveRotation(targetRot);
        }

        [Reconcile]
        private void Reconciliation(CreatureReconcileData rd, Channel channel = Channel.Unreliable)
        {
            _rb.position = rd.Position;
            _rb.linearVelocity = rd.Velocity;
            _rb.rotation = Quaternion.Euler(0f, rd.Yaw, 0f);
            _yaw = rd.Yaw;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (IsServerInitialized)
                TryCollectFood(other);
        }
    }
}
