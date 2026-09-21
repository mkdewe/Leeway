using UnityEngine;

namespace Leeway.CreatureEditor
{
    /// <summary>
    /// What a creature's puppet is made of: how heavy its muscles are, how far they bend, and how hard
    /// a hit has to be before it goes down.
    /// </summary>
    /// <remarks>
    /// A plain serialisable class rather than a ScriptableObject: these numbers are tuned per creature
    /// prefab while looking at it, not shared across the project the way the body build settings are.
    /// </remarks>
    [System.Serializable]
    public class CreaturePuppetSettings
    {
        public static CreaturePuppetSettings Default { get; } = new CreaturePuppetSettings();

        [Header("Bodies")]
        [Tooltip("Linear damping on every muscle. Higher settles a knocked-down creature sooner.")]
        [SerializeField, Min(0f)] private float _drag = 0.4f;

        [SerializeField, Min(0f)] private float _angularDrag = 3f;

        [Tooltip("Muscle collider radius as a fraction of the body radius at that vertebra. Below 1 the " +
                 "colliders sit inside the skin, which keeps a creature from looking wider than it hits.")]
        [SerializeField, Range(0.2f, 1.5f)] private float _colliderScale = 0.85f;

        [Header("Joints")]
        [SerializeField, Range(0f, 90f)] private float _swingLimitDegrees = 35f;
        [SerializeField, Range(0f, 90f)] private float _twistLimitDegrees = 20f;

        [Header("Muscles")]
        [Tooltip("How hard the puppet is pulled onto the animated pose. 1 is rigid, 0 is a rag doll. " +
                 "Keep it at 1. Below 1, BehaviourPuppet treats the puppet as having just been hit on " +
                 "every frame, so the knock-out test never rests and a standing creature can spawn — and " +
                 "stay — on the ground. Toppling belongs to the collision threshold, not to pin slack.")]
        [SerializeField, Range(0f, 1f)] private float _pinWeight = 1f;

        [SerializeField, Range(0f, 1f)] private float _muscleWeight = 1f;

        [Tooltip("How much of the puppet's pose is written back onto the creature's own bones.")]
        [SerializeField, Range(0f, 1f)] private float _mappingWeight = 1f;

        [Tooltip("Strength of the joint drives that hold a muscle on its animated rotation. PuppetMaster " +
                 "ships with 100, which is tuned for a humanoid limb — a vertebra is heavier and on a " +
                 "longer arm, and sags at that value.")]
        [SerializeField, Min(0f)] private float _muscleSpring = 2500f;

        [SerializeField, Min(0f)] private float _muscleDamper = 40f;

        [Tooltip("Pin the muscles by rotation as well as position. Holds a long spine far better.")]
        [SerializeField] private bool _angularPinning = true;

        [Header("Legs")]
        [Tooltip("Mass of one leg-link muscle. Far lighter than a vertebra — a leg that weighed as much " +
                 "as the torso would drag the creature over every time it took a step.")]
        [SerializeField, Min(0.05f)] private float _legMuscleMass = 0.4f;

        [Tooltip("Leg collider radius as a fraction of the link's length.")]
        [SerializeField, Range(0.05f, 0.5f)] private float _legColliderThickness = 0.18f;

        [Tooltip("How hard a leg link is held on its solved pose. Below 1 the legs give a little when " +
                 "they hit something, instead of the whole creature being shoved.")]
        [SerializeField, Range(0f, 1f)] private float _legPinWeight = 0.7f;

        [Header("Knockdown")]
        [Tooltip("The impact force a muscle has to take before it is knocked off the animated pose.")]
        [SerializeField, Min(0f)] private float _collisionThreshold = 2f;

        [Tooltip("How far a muscle may be dragged from its target before the creature counts as down.")]
        [SerializeField, Range(0.001f, 10f)] private float _knockOutDistance = 0.2f;

        [Tooltip("How long a knocked-down creature lies there before it starts getting up.")]
        [SerializeField, Min(0f)] private float _getUpDelay = 2.5f;

        public float Drag => _drag;
        public float AngularDrag => _angularDrag;
        public float ColliderScale => _colliderScale;
        public float SwingLimitDegrees => _swingLimitDegrees;
        public float TwistLimitDegrees => _twistLimitDegrees;
        public float PinWeight => _pinWeight;
        public float MuscleWeight => _muscleWeight;
        public float MappingWeight => _mappingWeight;
        public float MuscleSpring => _muscleSpring;
        public float MuscleDamper => _muscleDamper;
        public bool AngularPinning => _angularPinning;
        public float LegMuscleMass => _legMuscleMass;
        public float LegColliderThickness => _legColliderThickness;
        public float LegPinWeight => _legPinWeight;
        public float CollisionThreshold => _collisionThreshold;
        public float KnockOutDistance => _knockOutDistance;
        public float GetUpDelay => _getUpDelay;
    }
}
