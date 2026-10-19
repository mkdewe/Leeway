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

        /// <summary>Space was pressed. Travels in the payload so a replay jumps on the same tick.</summary>
        public bool Jump;
        public bool Sprint;

        /// <summary>Space was pressed this tick: one beat of the wings, if it has any.</summary>
        public bool Flap;

        /// <summary>Space is held: the wings stay out and the creature glides.</summary>
        public bool Glide;

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
        public float Stamina;

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
        [Header("Flight and stamina")]
        [Tooltip("How much of the ground control a creature keeps in the air while it has its wings out (0 to 1).")]
        [SerializeField, Range(0f, 1f)] private float _airControl = 0.55f;

        [Header("Control")]
        [Tooltip("The maximum acceleration the legs can transfer to the ground (m/s^2).")]
        [SerializeField] private float _acceleration = 22f;

        [Tooltip("Friction braking once the key is released (m/s^2). Lower = a longer slide.")]
        [SerializeField] private float _braking = 14f;
        [SerializeField, Range(1f, 2f)] private float _sprintMultiplier = 1.45f;
        private bool _sprintHeld;
        public bool IsSprinting { get; private set; }
        public float SprintMultiplier => _sprintMultiplier;

        [Tooltip("The game camera - its heading is the frame of reference for the movement keys. " +
                 "Left empty: the creature is a prefab, so it finds the camera in the scene at startup.")]
        [SerializeField] private CreatureOrbitCamera _orbitCamera;

        [Header("Water")]
        [Tooltip("Layers that are a water surface (raycast only - nothing stands on them).")]
        [SerializeField] private LayerMask _waterMask = 1 << 4;

        [Tooltip("Where the body floats: the root this far above the water surface (m). Small = mostly submerged.")]
        [SerializeField] private float _floatHeight = 0.1f;

        [Tooltip("How hard the water pushes a sunk body back up (acceleration per metre).")]
        [SerializeField, Min(0f)] private float _buoyancyStiffness = 55f;

        [Tooltip("Damping relative to critical: 1 settles without bobbing, lower bobs on the surface.")]
        [SerializeField, Range(0.1f, 2f)] private float _buoyancyDamping = 0.9f;

        [Tooltip("Swimming speed as a fraction of the walking speed.")]
        [SerializeField, Range(0.1f, 1f)] private float _swimSpeedFactor = 0.6f;

        [Header("Toppling")]
        [Tooltip("How long the fall takes at TWICE the stability limit. " +
                 "This is the reaction window: a gentler overload gives more time, a sharper one less.")]
        [SerializeField, Min(0.05f)] private float _tipSeconds = 0.5f;

        [Header("Slopes")]
        [Tooltip("The steepest slope the legs hold on (degrees). Beyond it the grip fades and the creature slides.")]
        [SerializeField, Range(20f, 70f)] private float _maxWalkSlope = 42f;

        [Tooltip("Slope (degrees) at which it cannot keep its footing for long.")]
        [SerializeField, Range(30f, 85f)] private float _fallSlope = 58f;

        [Tooltip("How much of gravity along the slope acts once the grip is gone. 1 = a frictionless slide.")]
        [SerializeField, Min(0f)] private float _slideGravity = 1f;

        [Tooltip("Seconds of sliding on a steep slope before it loses its balance and tumbles.")]
        [SerializeField, Min(0.1f)] private float _tumbleSeconds = 1.3f;

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
        private bool _jumpQueued;
        private bool _flapQueued;
        private bool _glideHeld;
        private float _yaw;

        /// <summary>The lateral load in units of the creature's own limit, signed. 1 = on the edge.</summary>
        private float _bankLoad;

        /// <summary>How far the loss of balance has gone. 1 = the creature goes over.</summary>
        private float _tipProgress;
        private float _slideTime;

        private float _stamina = -1f;
        private float _staminaDelay;
        private float _flapCooldown;
        private float _sinceFlap = 99f;
        private bool _exhausted;
        private bool _gliding;
        private float _wingPower;
        private float _maxStamina = 100f;

        /// <summary>The strength left to run, fly and fight with. Replicated to its owner with the movement.</summary>
        public float Stamina => _stamina < 0f ? _maxStamina : _stamina;
        public float MaxStamina => _maxStamina;

        /// <summary>Worn out: it cannot sprint or take wing until it has caught its breath.</summary>
        public bool IsExhausted => _exhausted;

        /// <summary>Whether this creature has wings at all.</summary>
        public bool CanFly => CreatureFlightRules.CanFly(_wingPower);

        /// <summary>The wings are out and carrying it.</summary>
        public bool IsGliding => _gliding;

        /// <summary>Seconds since the last beat of the wings.</summary>
        public float SinceFlap => _sinceFlap;

        /// <summary>Seconds until the next wing beat is allowed.</summary>
        public float FlapCooldownRemaining => _flapCooldown;

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
            _rb.constraints = RigidbodyConstraints.FreezeRotation;
            _rb.useGravity = false;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            BodyRebuilt += OnBuiltForFlight;
        }

        private void OnBuiltForFlight(CreatureGenome genome, CreatureStats stats)
        {
            _wingPower = CreatureFlightRules.WingPower(genome, Rules);
            _maxStamina = CreatureStaminaRules.Max(genome);
            if (_stamina > _maxStamina) _stamina = _maxStamina;
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

            _yaw = Surface.YawOf(transform.forward);

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
            _sprintHeld = keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);

            // Latched until the next tick sends it - a press shorter than a tick must not be lost.
            if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
            {
                _jumpQueued = true;
                _flapQueued = true;
            }

            _glideHeld = keyboard != null && keyboard.spaceKey.isPressed;

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
                    Jump = _jumpQueued,
                    Sprint = _sprintHeld,
                    Flap = _flapQueued,
                    Glide = _glideHeld,
                });
                _jumpQueued = false;
                _flapQueued = false;
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
            heading = Surface.Tangent(heading);

            if (heading.sqrMagnitude > 1e-6f) _aiYaw = Surface.YawOf(heading);
            _aiThrottle = Mathf.Clamp01(throttle);
        }

        public override void CreateReconcile()
        {
            Reconciliation(new PlaygroundReconcileData
            {
                Position = _rb.position,
                Velocity = _rb.linearVelocity,
                Yaw = Surface.YawOf(_rb.rotation * Vector3.forward),
                Stamina = _stamina,
            });
        }

        [Replicate]
        private void Move(PlaygroundMoveData md, ReplicateState state = ReplicateState.Invalid, Channel channel = Channel.Unreliable)
        {
            if (!IsAlive || !Leeway.Gravity.GravityWorld.IsSceneReady(gameObject.scene)) { IsSprinting = false; return; }
            float delta = (float)TimeManager.TickDelta;

            // Read before the suspension runs: it adds lift every tick that only the next physics step's
            // ground contact cancels, so a creature standing still reads as rising after it.
            var frame = Surface;
            Vector3 up = frame.Up;
            if (!_rb.isKinematic) _rb.AddForce(GravityAcceleration, ForceMode.Acceleration);
            float risingBefore = frame.Vertical(_rb.linearVelocity);

            // The body's real acceleration from the previous physics step. Measured from velocity rather
            // than derived from the commanded heading, because it is what actually acts on the
            // creature — see CheckRollover.
            Vector3 planar = frame.Tangent(_rb.linearVelocity);
            Vector3 planarAcceleration = frame.Tangent(planar - _previousPlanarVelocity) / Mathf.Max(delta, 1e-4f);
            _previousPlanarVelocity = planar;

            // A knocked-down creature stops predicting. The flag comes from a reliable, server-side
            // SyncVar, so it is identical on both sides for the ticks that matter and replication stays
            // deterministic. The ragdoll is driven by the server alone — trying to predict it would
            // break reconciliation. Getting up is still a knockdown: control is silent, but the physics
            // has to keep running (see RiseInPlace).
            if (IsKnockedDown)
            {
                IsSprinting = false;
                RiseInPlace(delta);
                return;
            }

            // The creature turns towards the direction being held rather than snapping onto it, and only
            // sets off once it is facing it — hence the turn on the spot when reversing, rather than a
            // sideways slide.
            Vector3 desired = LocomotionSteering.DesiredDirection(md.Forward, md.Strafe, md.CameraYaw);

            _yaw = frame.YawOf(_rb.rotation * Vector3.forward);
            float previousYaw = _yaw;
            _yaw = LocomotionSteering.StepYaw(_yaw, desired, Stats.TurnSpeed, delta);
            float throttle = LocomotionSteering.Throttle(_yaw, desired);

            // The acceleration was measured from before this turn, so we take the sideways axis from
            // before it too.
            CheckRollover(planarAcceleration, previousYaw, delta);

            Quaternion targetRotation = frame.Rotation(_yaw);
            Vector3 direction = targetRotation * Vector3.forward * throttle;

            // Muscle-driven movement is the server's business alone: a jointed body cannot be replayed,
            // so the owner's copy of this step does nothing and waits for the correction. What the
            // client loses is a round trip of responsiveness; what it gains is a creature that is moved
            // by being pushed rather than by being told where to be.
            if (_muscles != null && _muscles.IsActive)
            {
                if (IsServerInitialized)
                    _muscles.Drive(direction, CreatureSprintRules.Speed(Stats.MoveSpeed, md.Sprint, _suspension == null || _suspension.IsGrounded, Genome != null && Genome.CountParts(PartCategory.Locomotion, Rules) > 0, _sprintMultiplier), GenomeStatRules.StandHeight(Genome, Rules), delta);

                return;
            }

            // The suspension runs inside replication, so reconciliation replays it along with the rest of
            // the step. Outside that loop the client would drift away from the server.
            _suspension?.Simulate(delta);

            // With no ground contact there is nothing to push off — in the air only momentum remains.
            bool grounded = _suspension == null || _suspension.IsGrounded;
            // Water holds the body up where the legs cannot reach the bottom - and lets it paddle along.
            bool swimming = Float(grounded, delta);

            if (_stamina < 0f) _stamina = _maxStamina;
            _flapCooldown = Mathf.Max(0f, _flapCooldown - delta);
            _sinceFlap += delta;
            _staminaDelay -= delta;
            if (_stamina <= 0.5f) _exhausted = true;
            else if (_exhausted && _stamina >= CreatureStaminaRules.RecoveryThreshold) _exhausted = false;
            bool spent = false;

            IsSprinting = md.Sprint && grounded && !swimming && throttle > 0.01f && !_exhausted &&
                Genome != null && Genome.CountParts(PartCategory.Locomotion, Rules) > 0;
            if (IsSprinting)
            {
                _stamina -= CreatureStaminaRules.SprintDrainPerSecond * delta;
                spent = true;
            }

            // Only from the ground, and not again while still rising from the last push-off. A press that jumps is
            // not also a beat of the wings.
            bool hasLegs = Genome != null && Genome.CountParts(PartCategory.Locomotion, Rules) > 0;
            bool legJump = md.Jump && grounded && risingBefore < 0.5f && !_exhausted && _stamina >= CreatureStaminaRules.JumpCost;
            if (legJump)
            {
                Jump();
                _stamina -= CreatureStaminaRules.JumpCost;
                spent = true;
            }

            // Standing on the ground, or lifted off it by a jump or a beat: the legs reach the ground for a good
            // way up, so "grounded" alone says nothing about whether the wings are doing the work.
            float stand = GenomeStatRules.StandHeight(Genome, Rules);
            bool lifted = !grounded || (_suspension != null && _suspension.GroundDistance > stand + 0.2f) || frame.Vertical(_rb.linearVelocity) > 1.2f;

            // Wings. A beat lifts; holding the key glides. Both cost stamina, the beat far more.
            _gliding = false;
            bool wingsOut = false;
            if (CanFly && !swimming)
            {
                if (md.Flap && _flapCooldown <= 0f && !_exhausted && _stamina >= CreatureFlightRules.FlapCost && !legJump && (lifted || !hasLegs))
                {
                    Vector3 v = _rb.linearVelocity;
                    v += up * (Mathf.Max(-frame.Vertical(v), 0f) + CreatureFlightRules.FlapSpeed(_wingPower, Stats.Mass));
                    v += direction * (Stats.MoveSpeed * 0.25f);
                    _rb.linearVelocity = v;
                    _stamina -= CreatureFlightRules.FlapCost;
                    _flapCooldown = CreatureFlightRules.FlapCooldown;
                    _sinceFlap = 0f;
                    spent = true;
                    wingsOut = true;
                }
                else if (md.Glide && lifted && !_exhausted && frame.Vertical(_rb.linearVelocity) < 0.5f)
                {
                    Vector3 v = _rb.linearVelocity;
                    float sink = CreatureFlightRules.GlideSinkSpeed(_wingPower, Stats.Mass);
                    float vertical = frame.Vertical(v);
                    if (vertical < -sink) v += up * (Mathf.MoveTowards(vertical, -sink, 30f * delta) - vertical);
                    _rb.linearVelocity = v;
                    _stamina -= CreatureFlightRules.GlideDrainPerSecond * delta;
                    spent = true;
                    _gliding = true;
                    wingsOut = true;
                }
                else if (lifted && _sinceFlap < 0.6f) wingsOut = true;
            }

            if (spent) _staminaDelay = CreatureStaminaRules.RegenDelay;
            else if (_staminaDelay <= 0f) _stamina = Mathf.Min(_maxStamina, _stamina + CreatureStaminaRules.RegenPerSecond * delta);
            _stamina = Mathf.Max(0f, _stamina);
            float speed = CreatureSprintRules.Speed(Stats.MoveSpeed, IsSprinting, grounded, true, _sprintMultiplier);
            if (grounded)
            {
                // The steeper the ground, the less the legs can hold: grip fades, the body slides downhill and,
                // if it keeps sliding, loses its balance.
                float steepness = Steepness(out Vector3 downhill, out float slope);
                ApplyTraction(direction * (speed * (1f - 0.7f * steepness)), throttle, delta, 1f - steepness);
                Slide(steepness, downhill, slope, delta);
            }
            else
            {
                _slideTime = Mathf.Max(0f, _slideTime - delta);
                if (swimming) ApplyTraction(direction * (Stats.MoveSpeed * _swimSpeedFactor), throttle, delta);
                else if (wingsOut) ApplyTraction(direction * (Stats.MoveSpeed * (_gliding ? CreatureFlightRules.GlideSpeedFactor : 1f)), throttle, delta, _airControl);
            }

            // Only from the ground, and not again while still rising from the last push-off.

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

            Vector3 right = Surface.Rotation(yaw) * Vector3.right;
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

        /// <summary>0 on ground the legs hold, up to 1 on ground too steep to stand on.</summary>
        private float Steepness(out Vector3 downhill, out float slope)
        {
            downhill = Vector3.zero;
            slope = 0f;
            if (_suspension == null) return 0f;

            Vector3 normal = _suspension.GroundNormal;
            slope = Vector3.Angle(normal, Up);
            downhill = Vector3.ProjectOnPlane(-Up, normal);
            downhill = downhill.sqrMagnitude > 1e-6f ? downhill.normalized : Vector3.zero;

            return Mathf.InverseLerp(_maxWalkSlope, _maxWalkSlope + 12f, slope);
        }

        /// <summary>
        /// Gravity along a slope the legs cannot hold, and the loss of balance that follows a long slide.
        /// </summary>
        private void Slide(float steepness, Vector3 downhill, float slope, float delta)
        {
            if (steepness <= 0f || downhill == Vector3.zero)
            {
                _slideTime = Mathf.Max(0f, _slideTime - delta * 2f);
                return;
            }

            float along = GravityAcceleration.magnitude * Mathf.Sin(slope * Mathf.Deg2Rad) * _slideGravity * steepness;
            _rb.AddForce(downhill * along, ForceMode.Acceleration);

            // Footing is lost on a very steep slope at once, and on a merely steep one once it is really sliding.
            Vector3 flat = Surface.Tangent(_rb.linearVelocity);
            bool sliding = slope >= _fallSlope || Vector3.Dot(flat, downhill) > 3f;
            _slideTime = sliding ? _slideTime + delta : Mathf.Max(0f, _slideTime - delta);

            if (_slideTime < _tumbleSeconds || !IsServerInitialized) return;

            float push = Mathf.Max(KnockdownImpulseThreshold * 1.2f, Stats.Mass * 1.5f);
            if (Knockdown(downhill * push, MiddleBoneIndex())) _slideTime = 0f;
        }

        /// <summary>
        /// Buoyancy: a damped spring pulling the root up to just above the water surface. Runs inside the
        /// replicated step (the water is static), so prediction and the server float the same way.
        /// </summary>
        /// <returns>True while the body is in the water.</returns>
        private bool Float(bool grounded, float delta)
        {
            if (!TryWaterSurface(out float surface)) return false;

            float depth = surface + _floatHeight;
            if (depth <= -0.05f) return false;

            float damping = 2f * Mathf.Sqrt(_buoyancyStiffness) * _buoyancyDamping;

            // Standing on the bottom the legs already hold the weight; only floating needs to cancel gravity.
            float gravity = grounded ? 0f : GravityAcceleration.magnitude;
            float acceleration = depth * _buoyancyStiffness - Surface.Vertical(_rb.linearVelocity) * damping + gravity;

            _rb.linearVelocity += Up * (acceleration * delta);
            return true;
        }

        /// <summary>The water surface under (or over) the creature, if there is water here at all.</summary>
        private bool TryWaterSurface(out float surface)
        {
            if (Leeway.Gravity.GravityWorld.TryWaterSurface(_rb.position,gameObject.scene,out var waterPoint))
            { surface=Vector3.Dot(waterPoint-_rb.position,Up); return surface > -3f; }
            const float probe = 3f;
            surface = 0f;

            if (!Physics.Raycast(_rb.position + Up * probe, -Up, out RaycastHit hit, probe * 2f, _waterMask,
                    QueryTriggerInteraction.Collide))
                return false;

            surface = Vector3.Dot(hit.point - _rb.position, Up);
            return true;
        }

        /// <summary>How high this creature jumps, from its legs and mass (see <see cref="CreatureJumpRules"/>).</summary>
        public float JumpHeight => CreatureJumpRules.JumpHeight(Genome, Rules, Stats.Mass, Tuning);

        /// <summary>
        /// The push-off: a vertical velocity change, so the peak height does not depend on the tick rate
        /// or on the mass (the mass is already in <see cref="JumpHeight"/>). Runs inside the replicated
        /// step, so prediction and the server agree on it.
        /// </summary>
        private void Jump()
        {
            float speed = CreatureJumpRules.TakeOffSpeed(JumpHeight, GravityAcceleration.magnitude);
            if (speed <= 0f) return;

            Vector3 velocity = _rb.linearVelocity;
            velocity += Up * Mathf.Max(0f, speed - Surface.Vertical(velocity));
            _rb.linearVelocity = velocity;
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
        private void ApplyTraction(Vector3 targetVelocity, float throttle, float delta, float gripScale = 1f)
        {
            Vector3 velocity = _rb.linearVelocity;

            // The vertical belongs to the suspension and to gravity — traction only moves the plane.
            var horizontal = Surface.Tangent(velocity);
            Vector3 error = targetVelocity - horizontal;

            // A released key means braking, not an instant stop.
            float grip = (throttle > 0.01f ? _acceleration : _braking) * Mathf.Clamp01(gripScale);

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
            _previousPlanarVelocity = Surface.Tangent(rd.Velocity);
            _rb.rotation = Surface.Rotation(rd.Yaw);
            _yaw = rd.Yaw;
            if (rd.Stamina >= 0f) _stamina = rd.Stamina;
        }
    }
}
