using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Transporting;
using Leeway.Creature;
using MessagePipe;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Leeway.Player
{
    public struct CellMoveData : IReplicateData
    {
        public float Horizontal;
        public float Vertical;

        private uint _tick;
        public void Dispose() { }
        public uint GetTick() => _tick;
        public void SetTick(uint value) => _tick = value;
    }

    public struct CellReconcileData : IReconcileData
    {
        public Vector2 Position;
        public Vector2 Velocity;

        private uint _tick;
        public void Dispose() { }
        public uint GetTick() => _tick;
        public void SetTick(uint value) => _tick = value;
    }

    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerCellController : CellEntity
    {
        private Rigidbody2D _rb;
        private Vector2 _moveInput;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
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
                PublishLocalCell(this);
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            if (IsOwner)
                PublishLocalCell(null);
        }

        private static void PublishLocalCell(CellEntity cell)
        {
            if (GlobalMessagePipe.IsInitialized)
                GlobalMessagePipe.GetPublisher<LocalCellChangedMessage>().Publish(new LocalCellChangedMessage(cell));
        }

        private void Update()
        {
            if (!IsOwner) return;

            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                _moveInput = Vector2.zero;
                return;
            }

            float horizontal = 0f;
            float vertical = 0f;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) horizontal -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) horizontal += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) vertical -= 1f;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) vertical += 1f;

            _moveInput = new Vector2(horizontal, vertical).normalized;
        }

        private void OnTick()
        {
            if (IsOwner)
            {
                Reconciliation(default);
                Move(new CellMoveData { Horizontal = _moveInput.x, Vertical = _moveInput.y });
            }
            else if (IsServerInitialized)
            {
                Move(default);
            }
        }

        public override void CreateReconcile()
        {
            Reconciliation(new CellReconcileData
            {
                Position = _rb.position,
                Velocity = _rb.linearVelocity
            });
        }

        [Replicate]
        private void Move(CellMoveData md, ReplicateState state = ReplicateState.Invalid, Channel channel = Channel.Unreliable)
        {
            var dir = new Vector2(md.Horizontal, md.Vertical);
            float delta = (float)TimeManager.TickDelta;
            _rb.linearVelocity = Vector2.Lerp(_rb.linearVelocity, dir * CurrentSpeed, delta * Config.Drag);
        }

        [Reconcile]
        private void Reconciliation(CellReconcileData rd, Channel channel = Channel.Unreliable)
        {
            _rb.position = rd.Position;
            _rb.linearVelocity = rd.Velocity;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (IsServerInitialized)
                TryEat(other);
        }
    }
}
