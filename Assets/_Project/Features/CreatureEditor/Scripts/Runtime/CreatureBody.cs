using System;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Transporting;
using Leeway.Creature.Domain;
using MessagePipe;
using UnityEngine;

namespace Leeway.CreatureEditor
{
    /// <summary>
    /// A networked creature built from a genome. The server is the only source of truth about the
    /// genome, the HP and the knockdown state; clients reconstruct the whole body locally.
    /// </summary>
    /// <remarks>
    /// <para><b>Why this does not derive from <c>CreatureEntity</c>:</b> that class derives HP from
    /// <c>CreatureEntityConfig</c> and brings in the eating mechanic, which the editor playground does
    /// not use. Here HP comes from the <b>genome</b> (the vertebra count plus part bonuses), so
    /// inheriting would mean reworking a working prototype of the creature phase. Duplicating ~30 lines
    /// is cheaper than risking a broken <c>CreaturePhase</c>.</para>
    ///
    /// <para><b>An idempotent rebuild</b> guarded by the genome hash makes it safe to call
    /// <see cref="RebuildBody"/> from <c>OnStartServer</c>, <c>OnStartClient</c> and from
    /// <c>OnChange</c> in any order and any number of times. On a host <c>OnChange</c> fires twice
    /// (once as server, once as client) — the second call is then a no-op.</para>
    /// </remarks>
    public class CreatureBody : NetworkBehaviour
    {
        /// <summary>
        /// The minimum gap between commits. Without it, spamming full genomes (validation plus a mesh
        /// rebuild per packet) is a trivial DoS on the server.
        /// </summary>
        private const uint CommitCooldownTicks = 15;

        /// <summary>The ragdoll bodies' layer — it collides with neither locomotion capsules nor other ragdolls.</summary>
        private const string RagdollLayerName = "CreatureRagdoll";

        [Header("Dependencies")]
        [SerializeField] private CreaturePartCatalog _catalog;
        [SerializeField] private CreatureBodyBuildSettings _buildSettings;
        [SerializeField] private CreatureStatTuning _tuning = CreatureStatTuning.Default;

        [Tooltip("The locomotion capsule on the root. The builder only changes its dimensions - it never creates or removes a collider.")]
        [SerializeField] private CapsuleCollider _locomotionCollider;

        [Header("Knockdown")]
        [SerializeField] private CreatureRagdoll _ragdoll;

        [Tooltip("How long the creature lies down before the server stands it back up.")]
        [SerializeField] private float _knockdownSeconds = 2f;

        [Tooltip("The minimum impulse that topples at all. Below it the creature merely gets a shove.")]
        [SerializeField] private float _knockdownImpulseThreshold = 4f;

        /// <summary>
        /// The threshold below which an impulse only shoves rather than topples.
        /// </summary>
        /// <remarks>
        /// Visible to derived classes, because toppling from one's own speed decides on its own that the
        /// creature goes down, and has to know how hard to push for that decision to get through
        /// <see cref="Knockdown"/>.
        /// </remarks>
        protected float KnockdownImpulseThreshold => _knockdownImpulseThreshold;

        [Header("Commit")]
        [Tooltip("When on, the server only accepts a genome while the creature stands inside an EditorPad trigger.")]
        [SerializeField] private bool _requireEditorPad = true;

        private readonly SyncVar<GenomePayload> _genome = new SyncVar<GenomePayload>(
            new SyncTypeSettings(WritePermission.ServerOnly, ReadPermission.Observers, 0f, Channel.Reliable));

        private readonly SyncVar<KnockdownState> _knockdown = new SyncVar<KnockdownState>(
            new SyncTypeSettings(WritePermission.ServerOnly, ReadPermission.Observers, 0f, Channel.Reliable));

        private readonly SyncVar<float> _hp = new SyncVar<float>();
        private readonly SyncVar<bool> _isAlive = new SyncVar<bool>();

        private uint _builtGenomeHash;
        private BuiltCreatureBody _built;
        private CreatureGenome _genomeCache;
        private CreatureStats _stats;

        private CreatureLocomotion _locomotion;
        private bool _locomotionResolved;

        private uint _lastCommitTick;
        private bool _hasCommitted;
        private int _editorPadOverlaps;

        public CreaturePartCatalog Catalog => _catalog;
        public PartRuleSet Rules => _catalog != null ? _catalog.BuildRuleSet() : PartRuleSet.Empty;
        public CreatureStatTuning Tuning => _tuning;

        public float Hp => _hp.Value;
        public float MaxHp => _stats.MaxHp;
        public bool IsAlive => _isAlive.Value;
        public CreatureStats Stats => _stats;
        public BuiltCreatureBody Built => _built;

        /// <summary>The current genome. To edit it, take a <see cref="CreatureGenome.Clone"/> — this instance mirrors the network state.</summary>
        public CreatureGenome Genome => _genomeCache;

        /// <summary>
        /// How many degrees the body is leaning sideways. Signed: positive to the right.
        /// </summary>
        /// <remarks>
        /// Purely visual and <b>local</b>. The locomotion body has its rotation frozen in X and Z,
        /// because a predicted capsule that topples on its own would fight reconciliation — the lean is
        /// therefore the visual layer's job, exactly like the gait and the ragdoll. The base class never
        /// leans; it is computed by whoever computes movement.
        /// </remarks>
        public virtual float LeanDegrees => 0f;

        public bool IsKnockedDown => _knockdown.Value.IsDown;
        public KnockdownState CurrentKnockdown => _knockdown.Value;

        /// <summary>
        /// Whether the creature stands inside an editing zone. We count overlaps rather than keeping a
        /// bool — with two touching pads, leaving one must not switch the other off.
        /// </summary>
        public bool IsInEditorPad => _editorPadOverlaps > 0;

        public event Action<CreatureGenome, CreatureStats> BodyRebuilt;
        public event Action<float> HpChanged;
        public event Action<KnockdownState> KnockdownChanged;

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            _genome.OnChange += OnGenomeSyncChanged;
            _knockdown.OnChange += OnKnockdownSyncChanged;
            _hp.OnChange += OnHpSyncChanged;
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            _genome.OnChange -= OnGenomeSyncChanged;
            _knockdown.OnChange -= OnKnockdownSyncChanged;
            _hp.OnChange -= OnHpSyncChanged;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            _isAlive.Value = true;
            RebuildBody(_genome.Value);
            TimeManager.OnTick += OnServerTick;
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            if (TimeManager != null) TimeManager.OnTick -= OnServerTick;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            // The SyncVar values arrive before this runs, so this single call also covers players
            // joining a session already in progress.
            RebuildBody(_genome.Value);
        }

        private void OnDestroy() => _built?.Dispose();

        private void OnGenomeSyncChanged(GenomePayload previous, GenomePayload next, bool asServer) => RebuildBody(next);

        private void OnKnockdownSyncChanged(KnockdownState previous, KnockdownState next, bool asServer)
        {
            ApplyKnockdownVisuals(next);
            KnockdownChanged?.Invoke(next);
        }

        private void OnHpSyncChanged(float previous, float next, bool asServer) => HpChanged?.Invoke(next);

        /// <summary>
        /// Sets the genome on the server side. Validates with <b>its own</b> catalog — never with
        /// anything that came from a client.
        /// </summary>
        [Server]
        public bool ServerSetGenome(CreatureGenome genome, out GenomeError error)
        {
            GenomeValidationResult result = GenomeValidator.Validate(genome, Rules);
            if (!result.IsValid)
            {
                error = result.Error;
                return false;
            }

            var payload = GenomePayload.FromGenome(genome);
            if (payload.Blob.Length > GenomeLimits.MaxBlobBytes)
            {
                error = GenomeError.PayloadTooLarge;
                return false;
            }

            _genome.Value = payload;

            // The server rebuilds its own body in the same call, so its collider and mass match what the
            // clients are about to see.
            RebuildBody(payload);

            _hp.Value = _hp.Value <= 0f ? _stats.MaxHp : Mathf.Min(_hp.Value, _stats.MaxHp);

            error = GenomeError.None;
            return true;
        }

        internal void EnterEditorPad() => _editorPadOverlaps++;
        internal void ExitEditorPad() => _editorPadOverlaps = Mathf.Max(0, _editorPadOverlaps - 1);

        /// <summary>
        /// The client's entry point into the commit loop. Validates locally (fast feedback, no network
        /// traffic on an obvious error), then sends the complete genome for server-side verification.
        /// </summary>
        /// <returns>False if the genome failed before it was even sent.</returns>
        public bool RequestCommit(CreatureGenome genome, out GenomeError error)
        {
            error = GenomeError.None;

            if (!IsOwner)
            {
                Debug.LogWarning($"[{name}] Commit rejected locally — this is not this client's creature.", this);
                return false;
            }

            GenomeValidationResult result = GenomeValidator.Validate(genome, Rules);
            if (!result.IsValid)
            {
                error = result.Error;
                PublishCommitResult(false, error);
                return false;
            }

            var payload = GenomePayload.FromGenome(genome);
            if (payload.Blob.Length > GenomeLimits.MaxBlobBytes)
            {
                error = GenomeError.PayloadTooLarge;
                PublishCommitResult(false, error);
                return false;
            }

            CmdCommitGenome(payload);
            return true;
        }

        /// <summary>
        /// Server-side verification of a commit. It trusts nothing except its own part catalog and its
        /// own clock.
        /// </summary>
        [ServerRpc(RequireOwnership = true)]
        private void CmdCommitGenome(GenomePayload payload, Channel channel = Channel.Reliable)
        {
            NetworkConnection sender = Owner;

            uint tick = TimeManager.Tick;
            if (_hasCommitted && tick - _lastCommitTick < CommitCooldownTicks)
            {
                TargetCommitResult(sender, false, GenomeError.RateLimited);
                return;
            }

            if (_requireEditorPad && !IsInEditorPad)
            {
                TargetCommitResult(sender, false, GenomeError.NotInEditorPad);
                return;
            }

            // An empty or undecodable blob never reaches the validator — the serializer returns default
            // once the length limit is exceeded.
            if (payload.IsEmpty)
            {
                TargetCommitResult(sender, false, GenomeError.MalformedPayload);
                return;
            }

            if (!payload.TryDecode(out CreatureGenome genome, out GenomeError decodeError))
            {
                TargetCommitResult(sender, false, decodeError);
                return;
            }

            if (!ServerSetGenome(genome, out GenomeError validationError))
            {
                TargetCommitResult(sender, false, validationError);
                return;
            }

            // The cooldown only starts after a successful commit — rejected attempts must not block the
            // player's next, correct one.
            _lastCommitTick = tick;
            _hasCommitted = true;

            TargetCommitResult(sender, true, GenomeError.None);
        }

        [TargetRpc]
        private void TargetCommitResult(NetworkConnection conn, bool accepted, GenomeError error)
            => PublishCommitResult(accepted, error);

        private void PublishCommitResult(bool accepted, GenomeError error)
        {
            if (!accepted)
                Debug.LogWarning($"[{name}] Genome rejected: {error}.", this);

            if (GlobalMessagePipe.IsInitialized)
                GlobalMessagePipe.GetPublisher<GenomeCommitResultMessage>().Publish(new GenomeCommitResultMessage(accepted, error));
        }

        /// <summary>
        /// Topples the creature. Called <b>on the server only</b> — the ragdoll is never predicted, so a
        /// client has no business deciding on its own that it is lying down.
        /// </summary>
        /// <returns>False if the impulse was too weak or the creature is already down.</returns>
        [Server]
        public bool Knockdown(Vector3 impulse, int hitBoneIndex)
        {
            if (_knockdown.Value.IsDown || !_isAlive.Value) return false;
            if (impulse.magnitude < _knockdownImpulseThreshold) return false;

            _knockdown.Value = new KnockdownState
            {
                IsDown = true,
                StartTick = TimeManager.Tick,
                Impulse = impulse,
                HitBoneIndex = (byte)Mathf.Clamp(hitBoneIndex, 0, byte.MaxValue),
            };

            // A host is server and client at once, but OnChange does not fire for the server side on a
            // server-side write — the visuals have to be kicked off explicitly.
            ApplyKnockdownVisuals(_knockdown.Value);
            return true;
        }

        /// <summary>
        /// Stands the creature back up <b>completely</b>: plans the landing, switches the ragdoll off and
        /// returns control immediately. In normal play the server gets here through
        /// <see cref="BeginRise"/> and only after the getting-up animation — this method is a shortcut
        /// for code that wants the creature on its feet at once.
        /// </summary>
        [Server]
        public void Recover()
        {
            if (!_knockdown.Value.IsDown) return;

            BeginRise();

            _knockdown.Value = KnockdownState.Standing;
            ApplyKnockdownVisuals(KnockdownState.Standing);
        }

        /// <summary>
        /// Ends the sprawl: works out where to stand, switches the ragdoll off and enters the getting-up
        /// phase. Control does not come back yet — the knockdown flag stays raised.
        /// </summary>
        /// <remarks>
        /// <para>The landing position travels in the <b>network state</b>, not just to the local body.
        /// Previously the server moved its own transform and counted on the clients catching up with the
        /// next reconciliation — but reconciliation is muted for the duration of a knockdown, so a client
        /// only learned about the new spot after standing up and ran from the old position for a
        /// moment.</para>
        ///
        /// <para>The split into two phases exists so that getting up is <b>visible</b>. The bones blend
        /// from the ragdoll pose into the rest pose over <c>CreatureRagdoll.RecoverySeconds</c> and
        /// control is locked for exactly that long — otherwise the creature would break into a run
        /// halfway through getting up.</para>
        /// </remarks>
        [Server]
        private void BeginRise()
        {
            KnockdownState state = _knockdown.Value;
            if (!state.IsDown || state.IsRising) return;

            state.IsRising = true;
            state.Landing = ResolveLanding();

            _knockdown.Value = state;
            ApplyKnockdownVisuals(state);
        }

        /// <summary>
        /// Where the creature should stand. The height comes from the <b>suspension</b>, not from the
        /// collider: that is the height the legs really hold it at, so a creature placed there has
        /// neither to hop nor to settle.
        /// </summary>
        private Vector3 ResolveLanding()
        {
            if (_ragdoll == null) return transform.position;

            float standHeight = GenomeStatRules.StandHeight(_genomeCache, Rules);
            return _ragdoll.ResolveRecoveryPosition(transform.position, standHeight);
        }

        /// <summary>
        /// The knockdown countdown runs on the network tick, not in <c>Update</c>.
        /// </summary>
        /// <remarks>
        /// Two reasons. First, correctness: <c>StartTick</c> is in ticks, so measuring in frames would
        /// introduce drift at a variable frame rate. Second, a Unity trap — a derived class
        /// (<c>PlaygroundCreatureController</c>) declares its own private <c>Update</c>, which
        /// <b>hides</b> the base method, and Unity calls only the derived one. The base countdown simply
        /// would not run.
        /// </remarks>
        private void OnServerTick()
        {
            if (!_knockdown.Value.IsDown) return;

            uint elapsed = TimeManager.Tick - _knockdown.Value.StartTick;

            if (!_knockdown.Value.IsRising)
            {
                if (elapsed >= KnockdownTicks) BeginRise();
                return;
            }

            if (elapsed >= KnockdownTicks + RiseTicks)
            {
                _knockdown.Value = KnockdownState.Standing;
                ApplyKnockdownVisuals(KnockdownState.Standing);
            }
        }

        private uint KnockdownTicks => (uint)Mathf.Max(1f, _knockdownSeconds / (float)TimeManager.TickDelta);

        /// <summary>How many ticks getting up takes — exactly as long as the bone pose blend.</summary>
        private uint RiseTicks
        {
            get
            {
                float seconds = _ragdoll != null ? _ragdoll.RecoverySeconds : 0f;
                return (uint)Mathf.Max(1f, seconds / (float)TimeManager.TickDelta);
            }
        }

        private void ApplyKnockdownVisuals(KnockdownState state)
        {
            if (_ragdoll == null) return;

            if (state.IsSprawled && !_ragdoll.IsActive)
            {
                Vector3 inherited = TryGetComponent(out Rigidbody rb) ? rb.linearVelocity : Vector3.zero;
                _ragdoll.Activate(state.Impulse, state.HitBoneIndex, inherited);

                // The step cycle dies with the body. Without this, IK would keep planting the feet on the
                // ground, so a fallen creature would stand on straight legs.
                Locomotion?.Suspend();
            }
            else if (!state.IsSprawled && _ragdoll.IsActive)
            {
                // The order matters here. Deactivate records the fall pose in world space, Stand moves the
                // root up to the suspension height, and only then does BlendToBindPose rebase the bones
                // onto the new root and start the blend — that way getting up begins exactly where the
                // creature was lying, with no jump.
                _ragdoll.Deactivate();
                Stand(state.IsRising ? state.Landing : transform.position);

                // The armature has to return upright BEFORE the blend starts: the bones' rest pose is
                // recorded relative to it, so a leaning armature would stand the creature up permanently
                // crooked. The order is safe, because BlendToBindPose begins by restoring the fall pose
                // in world space and compensates for whatever that rotation did to the bones.
                Locomotion?.ResetLean();

                _ragdoll.BlendToBindPose();
                Locomotion?.Resume();
            }
        }

        /// <summary>
        /// Seats the locomotion body at the spot the server chose and zeroes its momentum.
        /// </summary>
        /// <remarks>
        /// Clearing the velocity is necessary because the body is coming back out of kinematic mode —
        /// without it the creature would get up already carrying whatever took it down, in a direction
        /// that two seconds of lying there had rendered meaningless. Called <b>after</b>
        /// <c>Deactivate</c>, once the body is non-kinematic again: Unity rejects a velocity write to a
        /// kinematic body with a warning.
        /// </remarks>
        private void Stand(Vector3 landing)
        {
            transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            transform.position = landing;

            if (!TryGetComponent(out Rigidbody rb)) return;

            rb.position = landing;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        /// <summary>
        /// The gait layer, resolved lazily.
        /// </summary>
        /// <remarks>
        /// Deliberately without an <c>Awake</c>: <c>PlaygroundCreatureController</c> declares its own
        /// private <c>Awake</c>, which would hide the base one and Unity would never call it — the same
        /// trap <see cref="OnServerTick"/> describes.
        /// </remarks>
        private CreatureLocomotion Locomotion
        {
            get
            {
                if (_locomotionResolved) return _locomotion;

                _locomotionResolved = true;
                TryGetComponent(out _locomotion);

                return _locomotion;
            }
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
        }

        /// <summary>
        /// Reconstructs the body from the genome. The hash guard makes this method safe to call from
        /// anywhere in the lifecycle, in any order.
        /// </summary>
        private void RebuildBody(GenomePayload payload)
        {
            if (payload.IsEmpty) return;
            if (payload.Hash == _builtGenomeHash && _built != null) return;

            if (!payload.TryDecode(out CreatureGenome genome, out GenomeError error))
            {
                Debug.LogError($"[{name}] Failed to decode the genome: {error}", this);
                return;
            }

            _builtGenomeHash = payload.Hash;
            _genomeCache = genome;

            _built?.Dispose();
            _built = CreatureBodyBuilder.Build(genome, _catalog, _buildSettings, transform, _tuning);
            if (_built == null) return;

            _stats = _built.Stats;

            // The mass from the genome goes into the rigid body. Without this every creature weighed the
            // same as the prefab and body size had no physical consequences at all — the recoil from a
            // shove and the inertia were identical for a hummingbird and an elephant.
            if (TryGetComponent(out Rigidbody rigidbody)) rigidbody.mass = Mathf.Max(0.1f, _stats.Mass);

            // The capsule wraps the torso alone. The clearance under the belly is made by the suspension
            // (CreatureSuspension), not by an offset collider — a rigid offset did not respond to terrain.
            CreatureColliderFitter.Fit(_locomotionCollider, genome, CreatureRigBuilder.ComputeBoneToRoot(genome), _buildSettings);

            if (TryGetComponent(out Rigidbody rootBody)) rootBody.mass = _stats.Mass;

            // The ragdoll is recreated along with the rig: bones do not survive a rebuild, so the old
            // chain of bodies would hang off destroyed objects.
            if (_ragdoll != null)
                _ragdoll.Build(_built, rootBody, _locomotionCollider, _stats.Mass, LayerMask.NameToLayer(RagdollLayerName));

            BodyRebuilt?.Invoke(genome, _stats);
        }
    }
}
