using FishNet.Object;
using UnityEngine;

namespace Leeway.CreatureEditor
{
    /// <summary>
    /// Shoving another creature. The client asks, the server decides.
    /// </summary>
    /// <remarks>
    /// The target is picked by the <b>server</b> with its own spherecast, not by a client passing a
    /// <c>NetworkObject</c>. If the target came from the client, a doctored packet would be enough to
    /// knock players over from the far side of the map.
    /// </remarks>
    [RequireComponent(typeof(CreatureBody))]
    public class CreatureShover : NetworkBehaviour
    {
        [SerializeField] private float _range = 2.2f;
        [SerializeField] private float _radius = 0.6f;
        [SerializeField] private float _impulse = 9f;
        [SerializeField] private float _upwardBias = 0.35f;

        [Tooltip("The gap between shoves, in ticks - the same protection as on a commit.")]
        [SerializeField] private uint _cooldownTicks = 20;

        private CreatureBody _body;
        private uint _lastShoveTick;
        private bool _hasShoved;

        private void Awake() => _body = GetComponent<CreatureBody>();

        /// <summary>Called by the player's controls. No effect while the creature is down itself.</summary>
        public void RequestShove()
        {
            if (!IsOwner || _body.IsKnockedDown) return;
            CmdShove();
        }

        [ServerRpc(RequireOwnership = true)]
        private void CmdShove()
        {
            uint tick = TimeManager.Tick;
            if (_hasShoved && tick - _lastShoveTick < _cooldownTicks) return;
            if (_body.IsKnockedDown) return;

            _lastShoveTick = tick;
            _hasShoved = true;

            ServerShove();
        }

        /// <summary>Split out of the RPC so server-side tests and NPCs can shove without pretending to be a client.</summary>
        [Server]
        public bool ServerShove()
        {
            Vector3 origin = transform.position;
            Vector3 direction = transform.forward;

            RaycastHit[] hits = Physics.SphereCastAll(origin, _radius, direction, _range,
                ~0, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < hits.Length; i++)
            {
                if (!hits[i].collider.TryGetComponent(out CreatureBody target)) continue;
                if (target == _body) continue;

                Vector3 push = (direction + Vector3.up * _upwardBias).normalized * _impulse;
                int bone = NearestBone(target, hits[i].point);

                if (target.Knockdown(push, bone)) return true;
            }

            return false;
        }

        /// <summary>Which bone was hit — it decides how the creature folds up as it falls.</summary>
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
    }
}
