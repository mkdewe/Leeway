using FishNet.Object;
using Leeway.Creature.Domain;
using Leeway.CreatureEditor;
using UnityEngine;

namespace Leeway.Combat
{
    /// <summary>
    /// What makes a creature with no player behind it act like an animal: it looks, it chooses, it
    /// walks and it bites.
    /// </summary>
    /// <remarks>
    /// <para><b>Server only.</b> Everything here decides, and decisions are the server's. The clients
    /// see the result through the same replication a player's creature uses — they never simulate the
    /// animal, so there is nothing for them to disagree about.</para>
    ///
    /// <para><b>It drives the ordinary controller</b> rather than moving the transform itself. That is
    /// the whole trick: traction, the suspension, toppling in a sharp turn, the legs pacing themselves
    /// off the body's travel — an enemy gets all of it because it walks through
    /// <see cref="PlaygroundCreatureController"/> exactly as a player does. Writing a second mover
    /// would have produced a second set of movement bugs and an enemy that slid about like a puck.</para>
    ///
    /// <para><b>The decision itself lives in the domain</b> (<see cref="INpcCreatureBehavior"/>), so
    /// what a predator does as opposed to prey is a unit test rather than a play session.</para>
    /// </remarks>
    [RequireComponent(typeof(CreatureBody))]
    public class NpcCreatureAgent : NetworkBehaviour
    {
        [Header("Behaviour")]
        [SerializeField] private NpcCreatureType _type = NpcCreatureType.Predator;

        [Tooltip("How far from where it woke up the animal will wander.")]
        [SerializeField, Min(1f)] private float _wanderRadius = 14f;

        [Tooltip("Seconds before it picks somewhere new to wander to.")]
        [SerializeField, Min(0.5f)] private float _wanderSeconds = 5f;

        [Header("Fighting")]
        [Tooltip("How much of its reach it closes to before striking. Below 1 it walks right up to the target.")]
        [SerializeField, Range(0.3f, 1f)] private float _engageFraction = 0.85f;

        [Tooltip("Seconds a body lies there before it is removed. Zero leaves it lying.")]
        [SerializeField, Min(0f)] private float _corpseSeconds = 8f;

        [Tooltip("What the animal may notice. Creatures only - the rest is scenery.")]
        [SerializeField] private LayerMask _senseMask = ~0;

        /// <summary>The throttle used while holding position: enough for the body to keep turning, not enough to travel.</summary>
        private const float FacingThrottle = 0.03f;

        private readonly Collider[] _sensed = new Collider[32];

        private CreatureBody _body;
        private CreatureCombatant _combatant;
        private PlaygroundCreatureController _controller;
        private INpcCreatureBehavior _behavior;

        private Vector3 _home;
        private Vector3 _wanderTarget;
        private float _nextWander;
        private float _deadSince = -1f;
        private EditorPad _pad;

        private float Now => (float)TimeManager.Tick * (float)TimeManager.TickDelta;

        private void Awake()
        {
            _body = GetComponent<CreatureBody>();
            TryGetComponent(out _combatant);
            TryGetComponent(out _controller);

            _behavior = NpcCreatureBehaviorFactory.Create(_type);
        }

        public override void OnStartServer()
        {
            base.OnStartServer();

            _home = transform.position;
            _wanderTarget = _home;
            _nextWander = 0f;

            TimeManager.OnTick += OnServerTick;
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            TimeManager.OnTick -= OnServerTick;
        }

        private void OnServerTick()
        {
            if (!IsServerInitialized) return;

            if (!_body.IsAlive)
            {
                HandleDeath();
                return;
            }

            // Flat on its back it has no say in anything until it is up again.
            if (_body.IsKnockedDown)
            {
                _controller?.ServerDrive(transform.forward, 0f);
                return;
            }

            // Wandered onto the build pad: turn round and leave. The pad is furniture, not territory,
            // and an animal standing on it blocks the one place the game lets a player rebuild.
            if (_body.IsInEditorPad && TryLeavePad()) return;

            CreatureBody quarry = FindQuarry(out float distance);

            Vector3 goal = _behavior.DecideTarget(new CreaturePerception(
                selfPosition: transform.position,
                fleeDistance: _body.Stats.SenseRadius,
                hasThreat: false,
                threatPosition: Vector3.zero,
                hasTarget: quarry != null,
                targetPosition: quarry != null ? quarry.transform.position : Vector3.zero,
                wanderTarget: WanderTarget()));

            Vector3 heading = goal - transform.position;
            heading.y = 0f;

            if (heading.sqrMagnitude < 1e-4f) heading = transform.forward;

            // Close enough to bite: stop walking but keep facing, so circling the animal does not take
            // it out of the fight.
            float engageRange = _combatant != null ? _combatant.Spec.Reach * _engageFraction : 0f;
            bool engaged = quarry != null && distance <= engageRange;

            _controller?.ServerDrive(heading, engaged ? FacingThrottle : 1f);

            if (engaged) _combatant?.ServerAttack();
        }

        /// <summary>
        /// The nearest creature worth attacking: alive, on another team and within the senses this
        /// genome grew.
        /// </summary>
        private CreatureBody FindQuarry(out float distance)
        {
            distance = float.MaxValue;

            float radius = Mathf.Max(1f, _body.Stats.SenseRadius);
            int count = Physics.OverlapSphereNonAlloc(transform.position, radius, _sensed, _senseMask, QueryTriggerInteraction.Ignore);

            CreatureBody best = null;

            for (int i = 0; i < count; i++)
            {
                Collider collider = _sensed[i];
                if (collider == null) continue;

                CreatureBody candidate = collider.GetComponentInParent<CreatureBody>();
                if (candidate == null || candidate == _body || !candidate.IsAlive) continue;

                // Whoever is standing on the editor pad is building, not playing. Hunting them there
                // shoved players off the pad mid-build — and a commit off the pad is refused, so the
                // animal quietly cost them the creature they had just made.
                if (candidate.IsInEditorPad) continue;

                // Its own kind is not dinner. Anything without a combatant has no team and is fair game.
                if (_combatant != null && candidate.TryGetComponent(out CreatureCombatant other)
                    && other.Team == _combatant.Team) continue;

                Vector3 offset = candidate.transform.position - transform.position;
                offset.y = 0f;

                float candidateDistance = offset.magnitude;
                if (candidateDistance >= distance) continue;

                distance = candidateDistance;
                best = candidate;
            }

            return best;
        }

        /// <summary>
        /// Walks straight off the editor pad, away from its centre.
        /// </summary>
        /// <remarks>
        /// The direction comes from the pad in the scene rather than from a remembered spot, because an
        /// animal that was shoved onto the pad has no memory of a way out. Away from the middle is the
        /// shortest one from anywhere on it.
        /// </remarks>
        private bool TryLeavePad()
        {
            if (_pad == null) _pad = FindAnyObjectByType<EditorPad>();
            if (_pad == null) return false;

            Vector3 out_ = transform.position - _pad.transform.position;
            out_.y = 0f;

            if (out_.sqrMagnitude < 1e-4f) out_ = transform.forward;

            _controller?.ServerDrive(out_.normalized, 1f);
            return true;
        }

        /// <summary>Somewhere to be when there is nothing to chase. Renewed every few seconds.</summary>
        private Vector3 WanderTarget()
        {
            if (Now < _nextWander) return _wanderTarget;

            _nextWander = Now + _wanderSeconds;

            Vector2 offset = Random.insideUnitCircle * _wanderRadius;
            _wanderTarget = _home + new Vector3(offset.x, 0f, offset.y);

            return _wanderTarget;
        }

        /// <summary>
        /// Lets the body lie where it fell, then takes it away.
        /// </summary>
        /// <remarks>
        /// The pause is not decoration: without it a creature vanishes at the instant of the killing
        /// blow, and the player is left unsure whether they killed it or it walked off.
        /// </remarks>
        private void HandleDeath()
        {
            _controller?.ServerDrive(transform.forward, 0f);

            if (_deadSince < 0f) _deadSince = Now;
            if (_corpseSeconds <= 0f || Now - _deadSince < _corpseSeconds) return;

            if (NetworkObject != null && NetworkObject.IsSpawned) NetworkObject.Despawn();
        }
    }
}
