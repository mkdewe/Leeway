using FishNet.Object;
using RootMotion.Dynamics;
using UnityEngine;

namespace Leeway.CreatureEditor
{
    /// <summary>
    /// Turns what happens to the puppet into what happens to the creature: a body shoved in the physics
    /// scene is a creature pushed in the game.
    /// </summary>
    /// <remarks>
    /// <para><b>Why a feedback path and not simply "the puppet is the creature".</b> Movement is
    /// predicted: the client runs it ahead of the server and the server corrects it
    /// (<c>PlayerCreatureController</c>'s <c>Replicate</c>/<c>Reconcile</c> pair). A predicted body has
    /// to be resimulated from any past tick, and a jointed ragdoll cannot be — it has state in every
    /// joint and no way to rewind it. So the puppet stays outside prediction and speaks to it instead:
    /// it reports how far it has been pushed off the pose, and the <b>server</b> turns that into
    /// velocity on the predicted body, which reaches every client through the usual correction.</para>
    ///
    /// <para><b>The server decides.</b> A client's own puppet is a local simulation — believing it
    /// would mean believing a client about where it gets pushed to.</para>
    ///
    /// <para>The measurement is the hips muscle's <b>horizontal</b> offset from its target. Vertical
    /// offset is what a creature standing on uneven ground has anyway, and feeding it back would make
    /// the creature launch itself off its own legs.</para>
    /// </remarks>
    [RequireComponent(typeof(CreaturePuppet))]
    public class CreaturePuppetPush : NetworkBehaviour
    {
        [Tooltip("How far the puppet has to be shoved off its pose before the creature is moved at all. " +
                 "Below it, ordinary muscle slack would nudge the creature for no reason.")]
        [SerializeField, Min(0f)] private float _deadzone = 0.05f;

        [Tooltip("How much of the shove becomes speed. 1 makes the creature travel about as fast as the " +
                 "muscle was displaced, per second.")]
        [SerializeField, Min(0f)] private float _strength = 6f;

        [Tooltip("The fastest a shove alone may throw a creature.")]
        [SerializeField, Min(0f)] private float _maxSpeed = 6f;

        private CreaturePuppet _puppet;
        private Rigidbody _rigidbody;

        private void Awake()
        {
            _puppet = GetComponent<CreaturePuppet>();
            TryGetComponent(out _rigidbody);
        }

        private void FixedUpdate()
        {
            if (!IsServerInitialized || _rigidbody == null) return;

            Vector3 shove = MeasureShove();
            if (shove == Vector3.zero) return;

            Vector3 velocity = _rigidbody.linearVelocity + shove;

            // Only the horizontal part is capped: the vertical component is the creature's own falling,
            // and clamping it would make gravity weaker the faster a creature was shoved.
            var horizontal = new Vector3(velocity.x, 0f, velocity.z);
            if (horizontal.magnitude > _maxSpeed) horizontal = horizontal.normalized * _maxSpeed;

            _rigidbody.linearVelocity = new Vector3(horizontal.x, velocity.y, horizontal.z);
        }

        /// <summary>
        /// How hard, and in which direction, the puppet is currently being pushed off its pose.
        /// </summary>
        /// <remarks>
        /// Zero while the creature is down: a fallen creature's muscles are far from their targets by
        /// definition, and feeding that back would send the carcass skating across the arena.
        /// </remarks>
        private Vector3 MeasureShove()
        {
            PuppetMaster puppetMaster = _puppet.PuppetMaster;
            if (puppetMaster == null || puppetMaster.muscles.Length == 0 || _puppet.IsDown) return Vector3.zero;

            Muscle hips = puppetMaster.muscles[0];
            if (hips?.joint == null || hips.target == null) return Vector3.zero;

            Vector3 offset = hips.joint.transform.position - hips.target.position;
            offset.y = 0f;

            float distance = offset.magnitude;
            if (distance <= _deadzone) return Vector3.zero;

            return offset.normalized * ((distance - _deadzone) * _strength * Time.fixedDeltaTime);
        }
    }
}
