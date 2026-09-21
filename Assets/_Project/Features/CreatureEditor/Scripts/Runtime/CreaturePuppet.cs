using System;
using Leeway.Creature.Domain;
using RootMotion.Dynamics;
using UnityEngine;

namespace Leeway.CreatureEditor
{
    /// <summary>
    /// The creature's physical body: a PuppetMaster puppet rebuilt with every body, and the layer
    /// rules that decide what it may hit.
    /// </summary>
    /// <remarks>
    /// <para><b>What this buys.</b> Until now a creature met the world through a single locomotion
    /// capsule and only became physical once it was knocked down. The puppet makes the carcass itself
    /// physical the whole time: it is what collides with the arena, with props, and with other
    /// creatures, and it is what a shove or a charge actually pushes.</para>
    ///
    /// <para><b>The simulation stays local.</b> Muscle poses will differ between clients and that is
    /// the right trade — the same one the old ragdoll made. What is binding is the server's verdict on
    /// whether a creature is <b>down</b> (<see cref="KnockdownState"/>); the physics only has to make
    /// that verdict look believable.</para>
    ///
    /// <para><b>Where the puppet is simulated.</b> Given a <see cref="CreaturePhysicsWorld"/> in the
    /// level, the puppet is moved into its own physics scene, out of reach of FishNet's prediction
    /// replay — which resimulates the shared scene several times per tick and tears a jointed chain
    /// apart. Without one it stays in the shared scene: fine while the network runs in Unity physics
    /// mode, and the thing to fix before switching <c>TimeManager._physicsMode</c> over.</para>
    /// </remarks>
    [DisallowMultipleComponent]
    public class CreaturePuppet : MonoBehaviour
    {
        [SerializeField] private CreaturePuppetSettings _settings = new();

        [Header("Layers")]
        [Tooltip("The layer the muscles go on. It collides with the world and with other creatures' muscles.")]
        [SerializeField] private string _ragdollLayer = "CreatureRagdoll";

        [Tooltip("The layer of the creature's own locomotion capsule — never collides with its own muscles.")]
        [SerializeField] private string _controllerLayer = "Creature";

        [Tooltip("Where the legs come from. Left empty it is looked up on this object — only a creature with no legs needs none.")]
        [SerializeField] private CreatureLocomotion _locomotion;

        private PuppetMaster _puppetMaster;
        private BehaviourPuppet _behaviour;
        private CreaturePhysicsWorld _physicsWorld;
        private bool _legsPending;

        private void Awake()
        {
            if (_locomotion == null) _locomotion = GetComponent<CreatureLocomotion>();
        }

        /// <summary>Raised when the puppet is knocked off its animated pose, i.e. the creature goes down.</summary>
        public event Action Knockdown;

        /// <summary>Raised when the puppet has climbed back onto the animated pose.</summary>
        public event Action Recovered;

        public PuppetMaster PuppetMaster => _puppetMaster;

        /// <summary>Whether the creature is currently off its feet, as far as physics is concerned.</summary>
        public bool IsDown => _behaviour != null && _behaviour.state != BehaviourPuppet.State.Puppet;

        /// <summary>
        /// Rebuilds the puppet for a freshly built body.
        /// </summary>
        /// <remarks>
        /// The bones do not survive a rebuild, so neither can the puppet: every muscle points at a
        /// transform that has just been destroyed. Tearing it down and building it again is also the
        /// only way to follow a creature that has grown a vertebra.
        /// </remarks>
        public void Rebuild(BuiltCreatureBody body, CreatureGenome genome, float totalMass)
        {
            Clear();

            int ragdollLayer = ResolveLayer(_ragdollLayer);
            int controllerLayer = ResolveLayer(_controllerLayer);

            _puppetMaster = CreaturePuppetFactory.Build(body, genome, transform, _settings, totalMass,
                ragdollLayer, controllerLayer);

            if (_puppetMaster == null) return;

            MoveToPhysicsWorld();

            _behaviour = _puppetMaster.GetComponentInChildren<BehaviourPuppet>();

            // A freshly built puppet is on its feet, and it has to say so. BehaviourPuppet starts
            // unpinned, which everything else reads as "this creature is down" — muscle-driven movement
            // would then refuse to move it, and the knockdown state would be wrong from the first frame.
            _behaviour?.SetState(BehaviourPuppet.State.Puppet);

            Subscribe();

            // The legs do not exist yet: they are unfolded by the locomotion layer, which rebuilds them
            // from the same event that brought us here. So the leg muscles are added on the next physics
            // step — which is also where PuppetMaster requires AddMuscle to be called from.
            _legsPending = true;
        }

        /// <summary>
        /// Hands the puppet over to the creatures' own physics scene, when the level has one.
        /// </summary>
        /// <remarks>
        /// <para>Without one the puppet stays in the shared scene and everything still works — it is
        /// simply exposed to whatever else steps physics there, which under FishNet's prediction means
        /// being resimulated several times per tick. That is a decision for the level to make, by
        /// having a <see cref="CreaturePhysicsWorld"/> or not, rather than something to fail over.</para>
        ///
        /// <para>Looked up rather than injected: the puppet is built at runtime inside a creature that
        /// was spawned by the network, so there is nothing in the prefab that could have pointed at a
        /// scene object.</para>
        /// </remarks>
        private void MoveToPhysicsWorld()
        {
            if (_physicsWorld == null) _physicsWorld = FindAnyObjectByType<CreaturePhysicsWorld>();
            if (_physicsWorld == null || !_physicsWorld.IsReady) return;

            _physicsWorld.Adopt(_puppetMaster.gameObject);
        }

        /// <summary>
        /// Hangs a muscle on every leg link, so the legs collide with the world like the rest of the body.
        /// </summary>
        /// <remarks>
        /// <para>The muscle body is a <b>new, unscaled object</b> that targets the link rather than being
        /// it: the solver stretches a link along its own axis every frame, and a capsule on a
        /// non-uniformly scaled transform does not have the shape it claims to have.</para>
        ///
        /// <para>Each leg's first link connects to the muscle of the bone its part hangs from, so a kick
        /// pushes the carcass and a stumble travels up the body instead of stopping at the hip.</para>
        /// </remarks>
        private void AttachLegMuscles()
        {
            if (_puppetMaster == null || !_puppetMaster.initiated || _locomotion == null) return;

            foreach (ProceduralLeg leg in _locomotion.Legs)
            {
                Rigidbody connectTo = MuscleBodyFor(leg.Hip);
                if (connectTo == null) continue;

                float radius = Mathf.Max(0.03f, leg.Spec.SegmentLength * _settings.LegColliderThickness);

                // The muscles hold the chain's <b>anchors</b>, never the links themselves: a link is
                // stretched along its own axis every frame, and PuppetMaster reads the joint's target
                // rotation straight out of that transform's matrix. A squashed matrix has no rotation
                // left in it, the target comes back NaN, and the joint hurls the ragdoll across the
                // level — see ProceduralLeg.MuscleTargets.
                for (int i = 0; i < leg.MuscleTargets.Count; i++)
                {
                    Transform anchor = leg.MuscleTargets[i];
                    if (anchor == null) continue;

                    // The last link is the foot: the behaviour treats a foot differently from a shin
                    // when it decides whether a collision means the creature tripped.
                    Muscle.Group group = i == leg.MuscleTargets.Count - 1 ? Muscle.Group.Foot : Muscle.Group.Leg;

                    connectTo = AddLegMuscle(anchor, connectTo, leg.Spec.SegmentLength, radius, i, group);
                }
            }
        }

        private Rigidbody AddLegMuscle(Transform segment, Rigidbody connectTo, float length, float radius, int index, Muscle.Group group)
        {
            var muscleObject = new GameObject("LegMuscle_" + index);
            muscleObject.layer = ResolveLayer(_ragdollLayer);
            muscleObject.transform.SetParent(_puppetMaster.transform, false);
            muscleObject.transform.SetPositionAndRotation(segment.position, segment.rotation);

            var rigidbody = muscleObject.AddComponent<Rigidbody>();
            rigidbody.mass = Mathf.Max(0.05f, _settings.LegMuscleMass);
            rigidbody.linearDamping = _settings.Drag;
            rigidbody.angularDamping = _settings.AngularDrag;
            rigidbody.interpolation = RigidbodyInterpolation.Interpolate;

            // A link runs along its own +Z, the same convention the whole part system is built on.
            var capsule = muscleObject.AddComponent<CapsuleCollider>();
            capsule.direction = 2;
            capsule.radius = radius;
            capsule.height = length + 2f * radius;
            capsule.center = new Vector3(0f, 0f, length * 0.5f);

            ConfigurableJoint joint = muscleObject.AddComponent<ConfigurableJoint>();
            joint.enablePreprocessing = false;

            // The parent stays the parent: AddMuscle reparents the target to whatever it is handed, and
            // a leg link belongs under its hip. Layers stay ours too — the links are also the editor's
            // click targets, and PuppetMaster would put them on the creature's own layer.
            _puppetMaster.AddMuscle(joint, segment, connectTo, segment.parent,
                new Muscle.Props(1f, _settings.LegPinWeight, 1f, 1f) { group = group }, forceTreeHierarchy: false, forceLayers: false);

            return rigidbody;
        }

        /// <summary>The muscle body belonging to the bone a part hangs from, or <c>null</c> when the part is not on one.</summary>
        private Rigidbody MuscleBodyFor(Transform partInstance)
        {
            Transform bone = partInstance != null ? partInstance.parent : null;
            if (bone == null) return null;

            foreach (Muscle muscle in _puppetMaster.muscles)
                if (muscle.target == bone) return muscle.rigidbody;

            return null;
        }

        private void FixedUpdate()
        {
            if (!_legsPending) return;

            _legsPending = false;
            AttachLegMuscles();
        }

        /// <summary>
        /// Moves the physical body to where the creature now is, without it travelling there.
        /// </summary>
        /// <remarks>
        /// <para>The creature is repositioned by things that are not physics: it spawns, the server
        /// stands it up after a knockdown, a correction arrives. The puppet lives in its own scene and
        /// knows none of that — it would try to <b>catch up</b>, sprinting across the level with its
        /// muscles, and anything coupling the two would turn that into a creature flung off the map.</para>
        ///
        /// <para>So every jump the creature makes has to be told to the puppet as a jump.</para>
        /// </remarks>
        public void Teleport()
        {
            if (_puppetMaster == null) return;

            _puppetMaster.Teleport(transform.position, transform.rotation, moveToTarget: true);
        }

        /// <summary>Drives an impulse into the puppet — a shove, a charge, a falling rock.</summary>
        /// <param name="boneIndex">Which muscle takes the hit. Out-of-range values land on the nearest one.</param>
        public void AddImpulse(Vector3 impulse, int boneIndex)
        {
            if (_puppetMaster == null || _puppetMaster.muscles.Length == 0) return;

            int index = Mathf.Clamp(boneIndex, 0, _puppetMaster.muscles.Length - 1);
            Muscle muscle = _puppetMaster.muscles[index];
            if (muscle?.rigidbody == null) return;

            muscle.rigidbody.AddForce(impulse, ForceMode.Impulse);
        }

        /// <summary>Knocks the creature off its feet outright, without waiting for a collision to do it.</summary>
        public void KnockDown(Vector3 impulse, int boneIndex)
        {
            if (_behaviour == null) return;

            _behaviour.SetState(BehaviourPuppet.State.Unpinned);
            AddImpulse(impulse, boneIndex);
        }

        /// <summary>
        /// Pulls the creature back onto its animated pose.
        /// </summary>
        /// <remarks>
        /// Getting up is a state change and nothing more — the muscles climb back onto the target under
        /// their own pin, from wherever the fall left them. The server has already decided <b>where</b>
        /// the creature stands up (<see cref="CreatureBody"/> moves the root first); this only decides
        /// that it does.
        /// </remarks>
        public void StandUp()
        {
            if (_behaviour == null) return;

            _behaviour.SetState(BehaviourPuppet.State.Puppet);
        }

        private void OnDestroy() => Clear();

        private void Clear()
        {
            Unsubscribe();

            if (_puppetMaster != null)
            {
                // The puppet holds the whole physical skeleton, so destroying its object is the whole
                // teardown — the muscles are its children.
                Destroy(_puppetMaster.gameObject);
                _puppetMaster = null;
            }

            _behaviour = null;
        }

        private void Subscribe()
        {
            if (_behaviour == null) return;

            EventOf(_behaviour.onLoseBalance)?.AddListener(OnLoseBalance);
            EventOf(_behaviour.onRegainBalance)?.AddListener(OnRegainBalance);
        }

        private void Unsubscribe()
        {
            if (_behaviour == null) return;

            EventOf(_behaviour.onLoseBalance)?.RemoveListener(OnLoseBalance);
            EventOf(_behaviour.onRegainBalance)?.RemoveListener(OnRegainBalance);
        }

        /// <summary>
        /// The <c>UnityEvent</c> inside a puppet event, created if the behaviour has none yet.
        /// </summary>
        /// <remarks>
        /// A <c>BehaviourPuppet</c> dragged onto a prefab in the inspector gets its events built by
        /// Unity's serialisation; one added in code at runtime does not, so the fields come back null
        /// and subscribing would throw. Since the puppet is built from a genome rather than authored,
        /// the runtime path is the only one this project ever takes.
        /// </remarks>
        private static UnityEngine.Events.UnityEvent EventOf(BehaviourBase.PuppetEvent puppetEvent)
            => puppetEvent.unityEvent ??= new UnityEngine.Events.UnityEvent();

        private void OnLoseBalance() => Knockdown?.Invoke();

        private void OnRegainBalance() => Recovered?.Invoke();

        /// <summary>
        /// The layer by name, or the default layer with a word about it.
        /// </summary>
        /// <remarks>
        /// A missing layer is worth saying out loud: everything would still run, and the creature would
        /// quietly collide with its own capsule — which looks like the physics being broken rather than
        /// a project setting being absent.
        /// </remarks>
        private static int ResolveLayer(string name)
        {
            int layer = LayerMask.NameToLayer(name);
            if (layer >= 0) return layer;

            Debug.LogWarning($"No layer named \"{name}\" — the puppet will share a layer with everything else, " +
                             "and a creature will fight its own locomotion capsule. Add the layer in Project Settings.");
            return 0;
        }
    }
}
