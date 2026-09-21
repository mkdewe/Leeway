using FishNet.Object;
using Leeway.Creature.Domain;
using Leeway.CreatureEditor;
using MessagePipe;
using UnityEngine;

namespace Leeway.Combat
{
    /// <summary>
    /// The ability to hit something. One blow, aimed with the body the creature was built with.
    /// </summary>
    /// <remarks>
    /// <para><b>The server decides everything.</b> A client asks to strike and nothing more: it names
    /// no target, no position and no damage. The server runs its own overlap query, checks the arc
    /// itself and applies the result. Anything else and a doctored packet would be enough to kill a
    /// player from across the map — the same reasoning as in <see cref="CreatureShover"/>, which this
    /// sits beside: a shove moves someone, a strike hurts them.</para>
    ///
    /// <para><b>What the blow is like comes out of the genome</b>, through <see cref="CombatRules"/>:
    /// a maw on a long neck reaches further than a horn set in the chest, a heavy creature swings
    /// slower than a light one, and a creature with nothing to bite with can still throw its weight
    /// about — just not well. Building a predator is therefore something the player does in the
    /// editor, not something this component is configured for.</para>
    ///
    /// <para><b>Teams, not factions.</b> Wildlife does not bite wildlife and players do not hit each
    /// other by accident; the team is a plain number on the prefab. It is deliberately not a rich
    /// system — when the game wants packs, prey and rivalry, this is the one place to grow it.</para>
    /// </remarks>
    [RequireComponent(typeof(CreatureBody))]
    public class CreatureCombatant : NetworkBehaviour
    {
        /// <summary>The team players' creatures are on.</summary>
        public const byte PlayerTeam = 0;

        /// <summary>The team everything wild is on.</summary>
        public const byte WildTeam = 1;

        [Header("Balance")]
        [SerializeField] private CombatTuning _tuning = CombatTuning.Default;

        [Tooltip("Creatures on the same team never hit one another. 0 = players, 1 = wildlife.")]
        [SerializeField] private byte _team = PlayerTeam;

        [Tooltip("What the strike may hit. It still only damages creatures - this is the broad-phase filter.")]
        [SerializeField] private LayerMask _hitMask = ~0;

        [Header("Feel")]
        [Tooltip("How far past its own reach a blow may still find a target, to forgive a near miss (metres).")]
        [SerializeField, Min(0f)] private float _forgiveness = 0.2f;

        private readonly Collider[] _overlap = new Collider[24];

        private CreatureBody _body;
        private AttackSpec _spec;
        private float _lastStrike = float.NegativeInfinity;

        /// <summary>The blow this creature's build produces. Recomputed whenever the body changes.</summary>
        public AttackSpec Spec => _spec;

        public byte Team => _team;

        /// <summary>Seconds until the next blow may be thrown. Zero when it is ready.</summary>
        public float CooldownRemaining => Mathf.Max(0f, _spec.Cooldown - (Now - _lastStrike));

        private float Now => TimeManager != null ? (float)TimeManager.Tick * (float)TimeManager.TickDelta : Time.time;

        private void Awake()
        {
            _body = GetComponent<CreatureBody>();
            _body.BodyRebuilt += OnBodyRebuilt;

            Rebuild(_body.Genome, _body.Stats);
        }

        private void OnDestroy()
        {
            if (_body != null) _body.BodyRebuilt -= OnBodyRebuilt;
        }

        private void OnBodyRebuilt(CreatureGenome genome, CreatureStats stats) => Rebuild(genome, stats);

        private void Rebuild(CreatureGenome genome, CreatureStats stats)
            => _spec = CombatRules.Derive(genome, _body.Rules, in stats, in _tuning);

        /// <summary>Called by the player's controls. Nothing happens while the creature is down.</summary>
        public void RequestAttack()
        {
            if (!IsOwner || !_body.IsAlive || _body.IsKnockedDown) return;

            // The cooldown is checked here as well so a held key does not flood the server with calls
            // it will refuse anyway. The server's own check is the one that decides.
            if (CooldownRemaining > 0f) return;

            CmdAttack();
        }

        [ServerRpc(RequireOwnership = true)]
        private void CmdAttack() => ServerAttack();

        /// <summary>
        /// Throws the blow. Split out of the RPC so the AI — which has no client behind it — strikes
        /// through exactly the same rules a player does.
        /// </summary>
        /// <returns>True when something was hit.</returns>
        [Server]
        public bool ServerAttack()
        {
            if (!_body.IsAlive || _body.IsKnockedDown) return false;
            if (!CombatRules.IsReady(Now, _lastStrike, _spec.Cooldown)) return false;

            _lastStrike = Now;

            Vector3 origin = transform.position;
            Vector3 forward = transform.forward;

            CreatureBody target = FindTarget(origin, forward, out Vector3 point);
            if (target == null)
            {
                RpcStruck(null, 0f, origin, false);
                return false;
            }

            float damage = _spec.Damage;

            // The knockback goes first, and deliberately so: a creature that is already dead refuses to
            // be knocked down (see CreatureBody.Knockdown), so damaging it first would leave the
            // killing blow the only one in the game that does not send its victim to the ground.
            Vector3 impulse = CombatRules.Knockback(origin, target.transform.position, in _spec, in _tuning);
            bool toppled = target.Knockdown(impulse, NearestBone(target, point));

            target.TakeDamage(damage);

            RpcStruck(target, damage, point, toppled);

            if (!target.IsAlive) RpcDied(target);

            return true;
        }

        /// <summary>
        /// The creature this blow lands on: the nearest one in front, within reach.
        /// </summary>
        /// <remarks>
        /// The nearest rather than the most damaged or the most dangerous — a blow goes where the body
        /// is pointing, and picking anything cleverer would have the creature biting past the animal in
        /// its face.
        /// </remarks>
        [Server]
        private CreatureBody FindTarget(Vector3 origin, Vector3 forward, out Vector3 point)
        {
            point = origin;

            float reach = _spec.Reach + _forgiveness;
            int count = Physics.OverlapSphereNonAlloc(origin, reach, _overlap, _hitMask, QueryTriggerInteraction.Ignore);

            CreatureBody best = null;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                Collider collider = _overlap[i];
                if (collider == null) continue;

                CreatureBody candidate = collider.GetComponentInParent<CreatureBody>();
                if (candidate == null || candidate == _body) continue;
                if (SameTeam(candidate)) continue;

                Vector3 candidatePoint = collider.ClosestPoint(origin);

                var spec = new AttackSpec(_spec.Damage, reach, _spec.ArcDegrees, _spec.Cooldown, _spec.Impulse, _spec.Armed);
                if (!CombatRules.CanStrike(_body.IsAlive, _body.IsKnockedDown, candidate.IsAlive,
                        origin, forward, candidatePoint, in spec)) continue;

                float distance = (candidatePoint - origin).sqrMagnitude;
                if (distance >= bestDistance) continue;

                bestDistance = distance;
                best = candidate;
                point = candidatePoint;
            }

            return best;
        }

        /// <summary>Whether the other creature fights on the same side. Anything without a combatant is fair game.</summary>
        private bool SameTeam(CreatureBody other)
            => other.TryGetComponent(out CreatureCombatant combatant) && combatant._team == _team;

        /// <summary>Which bone took the blow — it decides how the creature folds up if it goes down.</summary>
        private static int NearestBone(CreatureBody target, Vector3 worldPoint)
        {
            Transform[] bones = target.Built?.Bones;
            if (bones == null || bones.Length == 0) return 0;

            int best = 0;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < bones.Length; i++)
            {
                if (bones[i] == null) continue;

                float distance = (bones[i].position - worldPoint).sqrMagnitude;
                if (distance >= bestDistance) continue;

                bestDistance = distance;
                best = i;
            }

            return best;
        }

        /// <summary>
        /// Tells everyone watching that a blow was thrown — including the ones that missed, because a
        /// swing at thin air is exactly as visible as one that connects.
        /// </summary>
        [ObserversRpc(RunLocally = true)]
        private void RpcStruck(CreatureBody target, float damage, Vector3 point, bool knockedDown)
        {
            if (!GlobalMessagePipe.IsInitialized) return;

            GlobalMessagePipe.GetPublisher<CreatureStruckMessage>()
                .Publish(new CreatureStruckMessage(_body, target, damage, point, knockedDown));
        }

        [ObserversRpc(RunLocally = true)]
        private void RpcDied(CreatureBody target)
        {
            if (!GlobalMessagePipe.IsInitialized || target == null) return;

            GlobalMessagePipe.GetPublisher<CreatureDiedMessage>().Publish(new CreatureDiedMessage(target, _body));
        }

        private void OnDrawGizmosSelected()
        {
            if (_spec.Reach <= 0f) return;

            // The arc the blow covers, drawn where it is actually aimed.
            Gizmos.color = new Color(1f, 0.4f, 0.2f, 0.7f);
            Vector3 origin = transform.position;

            int steps = 16;
            for (int i = 0; i <= steps; i++)
            {
                float angle = Mathf.Lerp(-_spec.ArcDegrees * 0.5f, _spec.ArcDegrees * 0.5f, i / (float)steps);
                Gizmos.DrawRay(origin, Quaternion.Euler(0f, angle, 0f) * transform.forward * _spec.Reach);
            }
        }
    }
}
