using System.Collections.Generic;
using System.Reflection;
using Leeway.Creature.Domain;
using RootMotion.Dynamics;
using UnityEngine;

namespace Leeway.CreatureEditor
{
    /// <summary>
    /// Builds a PuppetMaster puppet for a creature that was assembled from a genome.
    /// </summary>
    /// <remarks>
    /// <para><b>Why not <c>PuppetMaster.SetUp</c>.</b> The packaged setup duplicates the character,
    /// creates a <c>"&lt;name&gt; Root"</c> GameObject and <b>reparents the target under it</b>. Our
    /// creature is a <c>NetworkObject</c> whose armature has to stay under its own root — moving it out
    /// would detach the bones from the object FishNet moves, and the creature would walk away from its
    /// own skeleton. So the puppet is assembled here by hand: the same shape as <c>SetUpMuscles</c>
    /// builds (a joint chain whose muscles point at the target bones by name), without touching the
    /// creature's hierarchy.</para>
    ///
    /// <para><b>The puppet is a second skeleton.</b> The creature's own bones stay the animated target,
    /// driven as before by the genome and the leg solver; the puppet is a chain of rigid bodies that
    /// mirrors them, carries the colliders and does the colliding. That is PuppetMaster's whole model —
    /// a kinematic target with a physical body pinned to it by muscles, which can be knocked off it.</para>
    ///
    /// <para><b>Built once per body rebuild.</b> A rebuild destroys the bones, so a puppet pointing at
    /// the old ones would drive a skeleton that no longer exists.</para>
    /// </remarks>
    public static class CreaturePuppetFactory
    {
        public const string PuppetObjectName = "Puppet";
        private const string BehavioursObjectName = "Behaviours";

        /// <summary>The smallest a muscle collider may get — a bead of a vertebra still has to hit things.</summary>
        private const float MinColliderRadius = 0.06f;

        /// <summary>Unity's built-in layer 0, where the arena and everything else in the world lives.</summary>
        private const int DefaultLayer = 0;

        /// <summary>
        /// Assembles the puppet. Returns <c>null</c> when there is nothing to build from.
        /// </summary>
        /// <param name="creatureRoot">The creature's root — what the puppet maps the carcass back onto.</param>
        /// <param name="totalMass">The creature's mass, spread over the muscles.</param>
        public static PuppetMaster Build(BuiltCreatureBody body, CreatureGenome genome, Transform creatureRoot,
            CreaturePuppetSettings settings, float totalMass, int ragdollLayer, int controllerLayer)
        {
            if (body?.Bones == null || body.Bones.Length == 0 || creatureRoot == null) return null;

            settings ??= CreaturePuppetSettings.Default;

            // Built switched off: PuppetMaster initiates in Awake, and it has to find a finished chain
            // and a filled muscle list, not a puppet halfway through being assembled.
            var puppetObject = new GameObject(PuppetObjectName);
            puppetObject.SetActive(false);
            puppetObject.transform.SetParent(creatureRoot, false);
            puppetObject.transform.SetPositionAndRotation(creatureRoot.position, creatureRoot.rotation);

            Transform[] bones = body.Bones;
            var joints = new List<ConfigurableJoint>(bones.Length);
            var targets = new List<Transform>(bones.Length);
            Rigidbody previous = null;
            Transform previousBone = puppetObject.transform;

            float perBoneMass = Mathf.Max(0.2f, totalMass / bones.Length);

            for (int i = 0; i < bones.Length; i++)
            {
                Transform bone = bones[i];
                if (bone == null) continue;

                // The name is the link: muscles find their target by it, exactly as PuppetMaster's own
                // setup does.
                var muscleObject = new GameObject(bone.name);
                muscleObject.layer = ragdollLayer;
                muscleObject.transform.SetParent(previousBone, false);
                muscleObject.transform.SetPositionAndRotation(bone.position, bone.rotation);

                var rigidbody = muscleObject.AddComponent<Rigidbody>();
                rigidbody.mass = perBoneMass;
                rigidbody.linearDamping = settings.Drag;
                rigidbody.angularDamping = settings.AngularDrag;
                rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
                rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

                var collider = muscleObject.AddComponent<SphereCollider>();
                collider.radius = Mathf.Max(MinColliderRadius, RadiusOf(genome, body, i) * settings.ColliderScale);

                ConfigurableJoint joint = muscleObject.AddComponent<ConfigurableJoint>();
                Configure(joint, previous, settings);

                joints.Add(joint);
                targets.Add(bone);

                previous = rigidbody;
                previousBone = muscleObject.transform;
            }

            if (joints.Count == 0)
            {
                Object.Destroy(puppetObject);
                return null;
            }

            PuppetMaster puppetMaster = puppetObject.AddComponent<PuppetMaster>();
            puppetMaster.targetRoot = creatureRoot;
            puppetMaster.muscles = BuildMuscles(joints, targets);
            puppetMaster.mode = PuppetMaster.Mode.Active;
            puppetMaster.pinWeight = settings.PinWeight;
            puppetMaster.muscleWeight = settings.MuscleWeight;
            puppetMaster.mappingWeight = settings.MappingWeight;

            // The stock spring is tuned for a humanoid limb of a couple of kilos. A creature's vertebra
            // is heavier and swings on a longer arm, and at the packaged 100 the chain simply hangs off
            // its pinned head like a rope.
            puppetMaster.muscleSpring = settings.MuscleSpring;
            puppetMaster.muscleDamper = settings.MuscleDamper;
            puppetMaster.angularPinning = settings.AngularPinning;

            AddBehaviour(puppetObject.transform, settings, ragdollLayer);

            // The creature's own collider must not fight its puppet: they occupy the same space by
            // design, so they would shove each other apart the moment physics started.
            Physics.IgnoreLayerCollision(controllerLayer, ragdollLayer);

            puppetObject.SetActive(true);

            return puppetMaster;
        }

        /// <summary>
        /// The muscle list, in the shape PuppetMaster validates: the free muscle first.
        /// </summary>
        /// <remarks>
        /// PuppetMaster insists the first muscle is the one everything else hangs off — the hips of a
        /// humanoid, the head vertebra here, since the spine is built from it outwards. It is the only
        /// muscle whose joint has no connected body.
        /// </remarks>
        private static Muscle[] BuildMuscles(List<ConfigurableJoint> joints, List<Transform> targets)
        {
            var muscles = new Muscle[joints.Count];

            for (int i = 0; i < joints.Count; i++)
            {
                muscles[i] = new Muscle
                {
                    joint = joints[i],
                    target = targets[i],
                    name = joints[i].name,
                    props = new Muscle.Props(1f, 1f, 1f, 1f) { group = GroupOf(i, joints.Count) },
                };
            }

            return muscles;
        }

        /// <summary>
        /// Which body part a vertebra counts as.
        /// </summary>
        /// <remarks>
        /// <para>The groups are how <c>BehaviourPuppet</c> reasons about a body: it looks for exactly one
        /// <c>Hips</c> to hang the character off, and unpins whole groups at a time when something hits
        /// them. Left at the default, every muscle claims to be the hips, and the behaviour says so.</para>
        ///
        /// <para>The spine runs head to tail, so the first vertebra is the head and the last is the
        /// tail. The <b>hips</b> are the root muscle — PuppetMaster's own name for "the one everything
        /// else hangs from", which for this creature is the head vertebra rather than a pelvis.</para>
        /// </remarks>
        private static Muscle.Group GroupOf(int index, int count)
        {
            if (index == 0) return Muscle.Group.Hips;
            if (index == count - 1 && count > 2) return Muscle.Group.Tail;

            return Muscle.Group.Spine;
        }

        /// <summary>
        /// A muscle joint: locked in translation, limited in rotation.
        /// </summary>
        /// <remarks>
        /// The angular drive is left to PuppetMaster — it writes the slerp drive itself from the muscle
        /// weights every step, so anything set here would be overwritten on the first frame. What is
        /// ours is the shape of the chain: how far a vertebra may bend before the joint stops it.
        /// </remarks>
        private static void Configure(ConfigurableJoint joint, Rigidbody connectedTo, CreaturePuppetSettings settings)
        {
            joint.connectedBody = connectedTo;
            joint.autoConfigureConnectedAnchor = true;
            joint.enablePreprocessing = false;

            // The root muscle hangs off nothing, and in PhysX "nothing" means the world. Locking its
            // motion the way a vertebra's is locked nails the creature to a point in space: it can be
            // hit, it just cannot be moved, and a knocked-down creature rotates around its own head
            // instead of falling over. So the free muscle stays free, and what holds it up is
            // PuppetMaster's pin, which is exactly what a pin is for.
            if (connectedTo == null)
            {
                joint.xMotion = ConfigurableJointMotion.Free;
                joint.yMotion = ConfigurableJointMotion.Free;
                joint.zMotion = ConfigurableJointMotion.Free;
                joint.angularXMotion = ConfigurableJointMotion.Free;
                joint.angularYMotion = ConfigurableJointMotion.Free;
                joint.angularZMotion = ConfigurableJointMotion.Free;
                return;
            }

            joint.xMotion = ConfigurableJointMotion.Locked;
            joint.yMotion = ConfigurableJointMotion.Locked;
            joint.zMotion = ConfigurableJointMotion.Locked;

            joint.angularXMotion = ConfigurableJointMotion.Limited;
            joint.angularYMotion = ConfigurableJointMotion.Limited;
            joint.angularZMotion = ConfigurableJointMotion.Limited;

            joint.lowAngularXLimit = new SoftJointLimit { limit = -settings.TwistLimitDegrees };
            joint.highAngularXLimit = new SoftJointLimit { limit = settings.TwistLimitDegrees };
            joint.angularYLimit = new SoftJointLimit { limit = settings.SwingLimitDegrees };
            joint.angularZLimit = new SoftJointLimit { limit = settings.SwingLimitDegrees };
        }

        /// <summary>
        /// Adds the behaviour that turns an impact into a fall and a fall into getting up.
        /// </summary>
        /// <remarks>
        /// On a child of the puppet, because that is where PuppetMaster looks for behaviours — it
        /// collects them from its own children first, and from its parent's only if it finds none.
        /// </remarks>
        private static void AddBehaviour(Transform puppet, CreaturePuppetSettings settings, int ragdollLayer)
        {
            var behaviours = new GameObject(BehavioursObjectName);
            behaviours.transform.SetParent(puppet, false);

            BehaviourPuppet puppetBehaviour = behaviours.AddComponent<BehaviourPuppet>();

            // A behaviour authored in the inspector gets its arrays from Unity's serialisation; one
            // added in code gets nulls, and BehaviourPuppet walks both of these as it initiates — which
            // throws before a single muscle has been updated. Ours are empty on purpose: no layer has a
            // collision resistance of its own, and no muscle group overrides the master settings.
            puppetBehaviour.collisionResistanceMultipliers ??= new BehaviourPuppet.CollisionResistanceMultiplier[0];
            puppetBehaviour.groupOverrides ??= new BehaviourPuppet.MusclePropsGroup[0];

            CreateEvents(puppetBehaviour);

            puppetBehaviour.collisionThreshold = settings.CollisionThreshold;
            puppetBehaviour.knockOutDistance = settings.KnockOutDistance;
            puppetBehaviour.getUpDelay = settings.GetUpDelay;

            // The masks are the difference between a physical creature and a statue. Left at their
            // runtime default of Nothing — which is what a component added in code gets — no impact can
            // ever knock the puppet off its pose, and a fallen creature has no ground to get up from:
            // everything still runs, and nothing ever happens.
            //
            // The two masks must not overlap, and that is not a style point: a layer that is both
            // "ground" and "something that knocks me over" means the creature is knocked over by
            // standing on the floor. It spawns unpinned, never recovers, and every system that asks
            // whether it is down is told yes. So the world is ground, and other creatures are impacts.
            puppetBehaviour.collisionLayers = 1 << ragdollLayer;
            puppetBehaviour.groundLayers = 1 << DefaultLayer;
        }

        /// <summary>
        /// Gives every one of the behaviour's events something to invoke.
        /// </summary>
        /// <remarks>
        /// <para><c>BehaviourPuppet</c> fires its own events as it changes state — losing balance,
        /// getting up, landing prone. Each is a <c>PuppetEvent</c> holding a <c>UnityEvent</c> that
        /// Unity's serialisation creates for a component dropped on a prefab and does <b>not</b> create
        /// for one added in code, so the first state change throws inside the package.</para>
        ///
        /// <para>Walked by reflection rather than named one by one: the list of events belongs to
        /// PuppetMaster and grows with it, and a name we forgot to add here would fail as a crash at the
        /// moment a creature first falls over.</para>
        /// </remarks>
        private static void CreateEvents(BehaviourPuppet behaviour)
        {
            const BindingFlags fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            foreach (FieldInfo field in behaviour.GetType().GetFields(fields))
            {
                if (field.FieldType != typeof(BehaviourBase.PuppetEvent)) continue;

                // PuppetEvent is a struct, so it has to be boxed, filled in and written back — assigning
                // through the field's value alone would change a copy and leave the behaviour as it was.
                var puppetEvent = (BehaviourBase.PuppetEvent)field.GetValue(behaviour);

                // Every field Unity would have filled in. Trigger invokes the event, then walks the
                // animation list, then asks whether it should switch behaviour — and that last question
                // is answered by comparing the name against "", so a null name means "yes, switch to a
                // behaviour called nothing", and the package complains once per state change.
                puppetEvent.unityEvent ??= new UnityEngine.Events.UnityEvent();
                puppetEvent.animations ??= new BehaviourBase.AnimatorEvent[0];
                puppetEvent.switchToBehaviour ??= string.Empty;

                field.SetValue(behaviour, puppetEvent);
            }
        }

        /// <summary>
        /// How thick the creature is at a vertebra.
        /// </summary>
        /// <remarks>
        /// From the genome when there is one — it is the same radius the skin is generated from, so the
        /// collider sits inside the body the player can see. Without a genome we fall back to the gap
        /// between neighbouring bones, which is the best guess the built body alone can offer.
        /// </remarks>
        private static float RadiusOf(CreatureGenome genome, BuiltCreatureBody body, int index)
        {
            if (genome != null && index < genome.VertebraCount) return genome.GetVertebra(index).Radius;

            if (body.Bones.Length < 2) return 0.2f;

            int other = index > 0 ? index - 1 : 1;
            return Vector3.Distance(body.Bones[index].position, body.Bones[other].position) * 0.45f;
        }
    }
}
