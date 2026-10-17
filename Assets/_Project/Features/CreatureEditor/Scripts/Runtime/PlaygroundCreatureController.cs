using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Transporting;
using Leeway.Creature.Domain;
using MessagePipe;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Leeway.CreatureEditor
{
    public struct PlaygroundMoveData : IReplicateData
    {
        public float Forward;

        /// <summary>Sideways step. Together with <see cref="Forward"/> it gives all eight directions.</summary>
        public float Strafe;

        /// <summary>
        /// The camera's heading at the moment the key was pressed. The input is relative to it, so it
        /// has to travel in the payload — otherwise a reconciliation replay would compute a different
        /// direction.
        /// </summary>
        public float CameraYaw;

        private uint _tick;
        public void Dispose() { }
        public uint GetTick() => _tick;
        public void SetTick(uint value) => _tick = value;
    }

    public struct PlaygroundReconcileData : IReconcileData
    {
        public Vector3 Position;
        public Vector3 Velocity;
        public float Yaw;

        private uint _tick;
        public void Dispose() { }
        public uint GetTick() => _tick;
        public void SetTick(uint value) => _tick = value;
    }

    /// <summary>
    /// Creature movement in the playground with client-side prediction. It repeats the pattern from
    /// <c>PlayerCreatureController</c>, but takes its speed from the genome rather than from a config.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class PlaygroundCreatureController : CreatureBody
    {
        [Header("Control")]
        [Tooltip("The maximum acceleration the legs can transfer to the ground (m/s^2).")]
        [SerializeField] private float _acceleration = 22f;

        [Tooltip("Friction braking once the key is released (m/s^2). Lower = a longer slide.")]
        [SerializeField] private float _braking = 14f;

        [Tooltip("The game camera - its heading is the frame of reference for the movement keys. " +
                 "Left empty: the creature is a prefab, so it finds the camera in the scene at startup.")]
        [SerializeField] private CreatureOrbitCamera _orbitCamera;

        [Header("Toppling")]
        [Tooltip("How long the fall takes at TWICE the stability limit. " +
                 "This is the reaction window: a gentler overload gives more time, a sharper one less.")]
        [SerializeField, Min(0.05f)] private float _tipSeconds = 0.5f;

        [Tooltip("How fast the creature recovers its balance once it eases off the turn.")]
        [SerializeField, Min(0.05f)] private float _tipRecoverySeconds = 0.35f;

        [Tooltip("The lean at a load exactly on the stability limit (degrees). " +
                 "This is the signal that the legs are working flat out - not yet a fall.")]
        [SerializeField] private float _maxBankDegrees = 12f;

        [Tooltip("Additional lean at a full loss of balance (degrees). That is what you see just before the fall.")]
        [SerializeField] private float _tipDegrees = 38f;

        [Tooltip("How much stability a leaning creature loses. 0 = the lean changes nothing " +
                 "and you can balance forever; higher = the point of no return arrives sooner.")]
        [SerializeField, Range(0f, 0.9f)] private float _tipCollapse = 0.6f;

        private Rigidbody _rb;
        private CreatureShover _shover;
        private CreatureSuspension _suspension;
        private Vector2 _moveInput;
        private float _yaw;

        /// <summary>The lateral load in units of the creature's own limit, signed. 1 = on the edge.</summary>
        private float _bankLoad;

        /// <summary>How far the loss of balance has gone. 1 = the creature goes over.</summary>
        private float _tipProgress;

        /// <summary>Which way the lean is going. Fixed once, at the start of the loss of balance.</summary>
        private float _tipSign;

        /// <summary>The horizontal velocity from the previous step — the real body acceleration is derived from it.</summary>
        private Vector3 _previousPlanarVelocity;

        /// <summary>Movement done by the muscles, when the creature has it. Otherwise the capsule does it.</summary>
        private CreatureMuscleLocomotion _muscles;

        /// <summary>Where a creature with no player behind it wants to go, and how hard. See <see cref="ServerDrive"/>.</summary>
        private float _aiYaw;
        private float _aiThrottle;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            TryGetComponent(out _shover);
            TryGetComponent(out _suspension);
            TryGetComponent(out _muscles);
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
            if (!IsOwner) return;

            _yaw = transform.eulerAngles.y;

            // A prefab cannot hold a reference to a scene object, so we find the camera at startup. Only
            // for the owner — other people's creatures do not read input, so they have no use for the
            // camera's heading.
            if (_orbitCamera == null) _orbitCamera = FindFirstObjectByType<CreatureOrbitCamera>();

            PublishLocalBody(this);
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            if (IsOwner) PublishLocalBody(null);
        }

        private static void PublishLocalBody(CreatureBody body)
        {
            if (GlobalMessagePipe.IsInitialized)
                GlobalMessagePipe.GetPublisher<LocalCreatureBodyChangedMessage>().Publish(new LocalCreatureBodyChangedMessage(body));
        }

        private void Update()
        {
            if (!IsOwner) return;

            var keyboard = Keyboard.current;

            // The input is relative to the camera — that is how the player reads it. The creature does
            // not snap onto that heading, it turns towards it (see LocomotionSteering).
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

            if (keyboard != null && keyboard.fKey.wasPressedThisFrame && _shover != null)
                _shover.RequestShove();
        }

        /// <summary>
        /// Sends the player's input. <b>Never call <see cref="Reconciliation"/> here</b> — the reconcile
        /// payload is produced solely in <see cref="CreateReconcile"/>, which FishNet fires itself at
        /// the right moment.
        /// </summary>
        /// <remarks>
        /// Calling the reconcile method from game code goes to <c>Reconcile_Current</c>, and on the
        /// server that broadcasts the given payload as the authoritative state. Calling
        /// <c>Reconciliation(default)</c> every tick therefore sent a zeroed position and glued the
        /// creature to the centre of the world, driven into the floor.
        /// </remarks>
        private void OnTick()
        {
            if (IsOwner)
            {
                Move(new PlaygroundMoveData
                {
                    Forward = _moveInput.y,
                    Strafe = _moveInput.x,
                    CameraYaw = _orbitCamera != null ? _orbitCamera.Yaw : 0f,
                });
            }
            else if (IsServerInitialized)
            {
                // A creature with nobody at the controls is still driven through this same step, so an
                // animal walks with the traction, the suspension and the toppling a player's creature
                // walks with. With nothing driving it, the data is empty and the step is the old one.
                Move(new PlaygroundMoveData
                {
                    Forward = _aiThrottle,
                    Strafe = 0f,
                    CameraYaw = _aiYaw,
                });
            }
        }

        /// <summary>
        /// Points a creature that has no player behind it.
        /// </summary>
        /// <remarks>
        /// <para>Deliberately the same door the player's input goes through: a heading and a throttle,
        /// fed into the replicated step. The AI therefore cannot do anything a player could not — it
        /// cannot turn faster than the genome allows, cannot outrun its own legs and topples in a
        /// corner it takes too fast.</para>
        ///
        /// <para>A throttle of zero leaves the heading alone, because the steering treats empty input
        /// as "no opinion" and holds the current one. An animal that wants to stand and keep facing
        /// something therefore asks for a throttle small enough not to travel — see
        /// <c>NpcCreatureAgent</c>.</para>
        /// </remarks>
        /// <param name="heading">Where it wants to face, in world space. The vertical part is ignored.</param>
        /// <param name="throttle">0 to 1 — how much of its top speed to use.</param>
        [Server]
        public void ServerDrive(Vector3 heading, float throttle)
        {
            heading.y = 0f;

            if (heading.sqrMagnitude > 1e-6f) _aiYaw = LocomotionSteering.YawOf(heading);
            _aiThrottle = Mathf.Clamp01(throttle);
        }

        public override void CreateReconcile()
        {
            Reconciliation(new PlaygroundReconcileData
            {
                Position = _rb.position,
                Velocity = _rb.linearVelocity,
                Yaw = _yaw,
            });
        }

        [Replicate]
        private void Move(PlaygroundMoveData md, ReplicateState state = ReplicateState.Invalid, Channel channel = Channel.Unreliable)
        {
            float delta = (float)TimeManager.TickDelta;

            // The body's real acceleration from the previous physics step. Measured from velocity rather
            // than derived from the commanded heading, because it is what actually acts on the
            // creature — see CheckRollover.
            Vector3 planar = _rb.linearVelocity;
            planar.y = 0f;
            Vector3 planarAcceleration = (planar - _previousPlanarVelocity) / Mathf.Max(delta, 1e-4f);
            _previousPlanarVelocity = planar;

            // A knocked-down creature stops predicting. The flag comes from a reliable, server-side
            // SyncVar, so it is identical on both sides for the ticks that matter and replication stays
            // deterministic. The ragdoll is driven by the server alone — trying to predict it would
            // break reconciliation. Getting up is still a knockdown: control is silent, but the physics
            // has to keep running (see RiseInPlace).
            if (IsKnockedDown)
            {
                RiseInPlace(delta);
                return;
            }

            // The creature turns towards the direction being held rather than snapping onto it, and only
            // sets off once it is facing it — hence the turn on the spot when reversing, rather than a
            // sideways slide.
            Vector3 desired = LocomotionSteering.DesiredDirection(md.Forward, md.Strafe, md.CameraYaw);

            float previousYaw = _yaw;
            _yaw = LocomotionSteering.StepYaw(_yaw, desired, Stats.TurnSpeed, delta);
            float throttle = LocomotionSteering.Throttle(_yaw, desired);

            // The acceleration was measured from before this turn, so we take the sideways axis from
            // before it too.
            CheckRollover(planarAcceleration, previousYaw, delta);

            Quaternion targetRotation = Quaternion.Euler(0f, _yaw, 0f);
            Vector3 direction = targetRotation * Vector3.forward * throttle;

            // Muscle-driven movement is the server's business alone: a jointed body cannot be replayed,
            // so the owner's copy of this step does nothing and waits for the correction. What the
            // client loses is a round trip of responsiveness; what it gains is a creature that is moved
            // by being pushed rather than by being told where to be.
            if (_muscles != null && _muscles.IsActive)
            {
                if (IsServerInitialized)
                    _muscles.Drive(direction, Stats.MoveSpeed, GenomeStatRules.StandHeight(Genome, Rules), delta);

                return;
            }

            // The suspension runs inside replication, so reconciliation replays it along with the rest of
            // the step. Outside that loop the client would drift away from the server.
            _suspension?.Simulate(delta);

            // With no ground contact there is nothing to push off — in the air only momentum remains.
            bool grounded = _suspension == null || _suspension.IsGrounded;
            if (grounded) ApplyTraction(direction * Stats.MoveSpeed, throttle, delta);

            _rb.MoveRotation(targetRotation);
        }

        /// <summary>
        /// The physics step for a creature that is already standing but still getting up.
        /// </summary>
        /// <remarks>
        /// <para>Control is silent throughout getting up, but <b>the physics must not be</b>. The
        /// locomotion body comes back out of kinematic mode the moment it is stood up, so if the step
        /// were empty, gravity alone would act on it for the half second of the animation and the
        /// creature would get up driven tens of centimetres into the ground. The suspension therefore
        /// has to keep running and hold it at the legs' working height.</para>
        ///
        /// <para>A zero target velocity gives friction braking: a creature that has just gone over picks
        /// itself up on the spot rather than sliding on along the arc that toppled it.</para>
        ///
        /// <para>During the sprawl we do nothing — the body is governed by the ragdoll then, and the
        /// locomotion body is kinematic and disabled.</para>
        /// </remarks>
        private void RiseInPlace(float delta)
        {
            // A creature lying down or getting up does not lean — its pose is governed by the ragdoll,
            // and then by the blend back into the rest pose.
            Settle(delta);

            if (!CurrentKnockdown.IsRising) return;

            _suspension?.Simulate(delta);

            if (_suspension == null || _suspension.IsGrounded) ApplyTraction(Vector3.zero, 0f, delta);
        }

        /// <summary>
        /// Leans the creature under lateral load and lays it out once the lean passes the point of no
        /// return.
        /// </summary>
        /// <remarks>
        /// <para>The condition is physical, not arbitrary. The creature stays up as long as the moment
        /// from the lateral force on the lever arm of the centre-of-mass height is smaller than the
        /// restoring moment from its weight on the lever arm of the stance width — that is, as long as
        /// <c>a_lateral &lt; g · (stance / height)</c>. That limit is computed by
        /// <see cref="CreatureStats.MaxLateralAcceleration"/> from the actual leg build.</para>
        ///
        /// <para><b>Toppling is a process, not an event.</b> There used to be a time threshold here:
        /// for a moment nothing visible happened, and then the creature was suddenly on the ground. The
        /// player had neither a warning nor anything to correct. Now the overload drives
        /// <see cref="_tipProgress"/> — a growing lean <b>outwards from the arc</b>, visible before it is
        /// too late, which recedes as soon as you ease off the turn. Only a full lean lays the creature
        /// out, and it does so in the direction it was leaning.</para>
        ///
        /// <para>The rate of growth is proportional to the <b>excess</b> over the limit, so a gentle
        /// overrun gives a long window to react and a sharp one a short window. At twice the limit the
        /// fall takes exactly <see cref="_tipSeconds"/>.</para>
        ///
        /// <para><b>We measure the body's real acceleration, not its heading.</b> There used to be an
        /// estimate of <c>a = v · ω</c> from the commanded turn rate here — and it left the mechanic
        /// practically dead. The reason: <c>Throttle</c> is the cosine of the heading error, so
        /// <b>turning cuts its own speed</b> and that product never came out high. The real lateral force
        /// is put into the creature by <see cref="ApplyTraction"/>, which clamps the acceleration to the
        /// legs' grip — i.e. well above the toppling limit. The measured acceleration covers both:
        /// travelling along an arc is also a real change of velocity, so the old condition is contained
        /// in the new one.</para>
        ///
        /// <para>We take the <b>component perpendicular to the creature's front</b>, because that is the
        /// direction its stance is narrow in. Braking straight ahead would tip it over its own head,
        /// across the long side of its silhouette — a different and far more stable axis, which we do not
        /// compute here.</para>
        ///
        /// <para>The lean itself is computed by whoever computes movement, because it is visual. The
        /// knockdown is decided by the server alone: the ragdoll is not predicted, so a client has no
        /// business deciding on its own that it is lying down.</para>
        /// </remarks>
        private void CheckRollover(Vector3 planarAcceleration, float yaw, float delta)
        {
            if (IsKnockedDown) { Settle(delta); return; }

            float limit = Stats.MaxLateralAcceleration;
            if (limit <= 0f) { Settle(delta); return; }

            // With no ground contact there is nothing for a foot to catch on — nothing topples in the air.
            if (_suspension != null && !_suspension.IsGrounded) { Settle(delta); return; }

            Vector3 right = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
            float sideways = Vector3.Dot(planarAcceleration, right);

            // The load in units of the nominal limit: 1 = exactly on the edge. This is the plain "how
            // hard are the legs working" signal, and the bank comes from it.
            _bankLoad = Mathf.Clamp(sideways / limit, -1f, 1f);

            // The further the creature is already leaning, the less it can take: the mass moves outside
            // the stance, so the restoring moment's lever arm shrinks. This is the feedback that turns
            // toppling into a point of no return — without it the creature settled into a permanent lean
            // and balanced there indefinitely.
            float effective = limit * (1f - _tipProgress * _tipCollapse);
            float excess = Mathf.Abs(sideways) / Mathf.Max(0.01f, effective) - 1f;

            if (excess <= 0f) { Settle(delta); return; }

            // The inertial reaction acts opposite to the acceleration: the legs push the body one way,
            // the mass lays out the other.
            if (_tipProgress <= 0f) _tipSign = -Mathf.Sign(sideways);

            _tipProgress = Mathf.Min(1f, _tipProgress + excess * delta / Mathf.Max(0.05f, _tipSeconds));

            if (_tipProgress < 1f) return;
            if (!IsServerInitialized) return;

            // WHETHER it goes over was already decided by the growing lean — the impulse only shows how
            // it falls, and it goes the same way the creature was leaning. Hence the floor on the
            // knockdown threshold: without it a slight overrun would give an impulse too weak to get
            // through Knockdown, and the creature would balance forever.
            Vector3 outward = right * _tipSign;
            float push = Mathf.Max(KnockdownImpulseThreshold * 1.05f, Stats.Mass * excess * limit * _tipSeconds);

            if (Knockdown(outward * push, MiddleBoneIndex()))
            {
                _tipProgress = 0f;
                _bankLoad = 0f;
            }
        }

        /// <summary>Recovering balance: the lean returns to upright once the legs can cope again.</summary>
        private void Settle(float delta)
        {
            _tipProgress = Mathf.Max(0f, _tipProgress - delta / Mathf.Max(0.05f, _tipRecoverySeconds));
            _bankLoad = Mathf.MoveTowards(_bankLoad, 0f, delta / Mathf.Max(0.05f, _tipRecoverySeconds));
        }

        /// <summary>
        /// The body's lean: the bank from the current load plus the growing loss of balance.
        /// </summary>
        /// <remarks>
        /// Both components go <b>outwards from the arc</b>, so the direction the creature leans in is
        /// exactly the one it will go over in. That is what makes this mechanic legible: the player sees
        /// the side and the rate, and has something to read "time to ease off" from.
        /// </remarks>
        public override float LeanDegrees => -_bankLoad * _maxBankDegrees + _tipSign * _tipProgress * _tipDegrees;

        /// <summary>The bone halfway down the spine — where the creature's mass actually sits.</summary>
        private int MiddleBoneIndex() => Built?.Bones == null ? 0 : Built.Bones.Length / 2;

        /// <summary>
        /// Accelerates and brakes the creature with <b>force</b>, limited by the legs' grip.
        /// </summary>
        /// <remarks>
        /// <para>The velocity used to be written directly, which made the movement not physics but
        /// kinematic control: a shove from outside vanished on the next tick, mass meant nothing, and
        /// the creature stopped as if switched off.</para>
        ///
        /// <para>Now we compute the acceleration needed to reach the target velocity and <b>clamp it to
        /// what the legs can transfer</b>. That way a heavier creature takes longer to get up to speed, a
        /// slide after a collision plays out, and releasing the key gives friction braking rather than an
        /// instant stop.</para>
        /// </remarks>
        private void ApplyTraction(Vector3 targetVelocity, float throttle, float delta)
        {
            Vector3 velocity = _rb.linearVelocity;

            // The vertical belongs to the suspension and to gravity — traction only moves the plane.
            var horizontal = new Vector3(velocity.x, 0f, velocity.z);
            Vector3 error = targetVelocity - horizontal;

            // A released key means braking, not an instant stop.
            float grip = throttle > 0.01f ? _acceleration : _braking;

            Vector3 acceleration = Vector3.ClampMagnitude(error / Mathf.Max(delta, 1e-4f), grip);
            _rb.AddForce(acceleration, ForceMode.Acceleration);
        }

        [Reconcile]
        private void Reconciliation(PlaygroundReconcileData rd, Channel channel = Channel.Unreliable)
        {
            if (IsKnockedDown) return;

            _rb.position = rd.Position;
            _rb.linearVelocity = rd.Velocity;

            // A correction is a velocity jump imposed from outside, not the result of forces acting on
            // the creature — without this the next step would derive a fictitious acceleration from it.
            _previousPlanarVelocity = new Vector3(rd.Velocity.x, 0f, rd.Velocity.z);
            _rb.rotation = Quaternion.Euler(0f, rd.Yaw, 0f);
            _yaw = rd.Yaw;
        }
    }
}
