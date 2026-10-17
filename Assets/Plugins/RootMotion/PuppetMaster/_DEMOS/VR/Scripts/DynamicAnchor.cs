using UnityEngine;
using Zenject;

namespace RootMotion.Demos
{
    /// <summary>
    /// Follows a target Transform using physics only.
    /// </summary>
    public class DynamicAnchor : MonoBehaviour
    {
        [Tooltip("The Transform to follow.")]
        public Transform target;

        [Header("Mass")]
        [Tooltip("Base mass for the Rigidbody")]
        public float mass = 1f;
        private float baseMass = 1f;
        [Tooltip("Adds this value to the Rigidbody mass by target velocity magnitude.")]
        public float velocityMassAdd = 0.5f;
        private float baseVelocityMassAdd = 0.5f;

        [Header("Spring")]
        [Tooltip("Master Spring.")]
        [Range(0f, 1f)] public float spring = 1f;
        [Tooltip("Adds drag when in collision.")]
        public float drag = 1f;
        [Tooltip("The multiplier of collision impulse, applies to every collision effect.")] public float collisionSpringLoss = 1f;
        public float regainSpringSpeed = 3f;
        [Tooltip("Collisions are able to reduce the spring down to this value.")]
        public float minSpring = 0.05f;

        [Header("Rotation")]
        [Tooltip("Rotation spring of the DynamicAnchor joint.")]
        public float rotationSpring = 100000f;
        [Tooltip("Rotation damper of the DynamicAnchor joint.")]
        public float rotationDamper = 0f;
        [Tooltip("Dampers rotation when spring power has been lost due to collision.")]
        public float collisionDamperAdd = 1000f;
        [Tooltip("Adds this value to the rotation damper by target velocity magnitude.")]
        public float velocityDamperAdd = 0f;

        #region Divergence Settings
        [Header("Divergence Settings")]
        [Tooltip("Determines if to track whether the source diverges from the target. Tracking divergence adds additional overhead.")]
        [SerializeField]
        private bool trackDivergence;
        /// <summary>
        /// Determines if to track whether the source diverges from the target. Tracking divergence adds additional overhead.
        /// </summary>
        public bool TrackDivergenceDistance
        {
            get
            {
                return trackDivergence;
            }
            set
            {
                trackDivergence = value;
            }
        }
        [Tooltip("The distance the target has to be away from the source to be considered diverged.")]
        [SerializeField]
        private Vector3 divergenceThreshold = Vector3.one * 0.1f;
        /// <summary>
        /// The distance the target has to be away from the source to be considered diverged.
        /// </summary>
        public Vector3 DivergenceDistanceThreshold
        {
            get
            {
                return divergenceThreshold;
            }
            set
            {
                divergenceThreshold = value;
            }
        }
        [SerializeField]
        private float divergenceForceThreshold;
        public float DivergenceForceThreshold
        {
            get
            {
                return divergenceForceThreshold;
            }
            set
            {
                divergenceForceThreshold = value;
            }
        }
        #endregion

        public delegate void CollisionDelegate(Collision c, float impulse, bool isStay);
        public CollisionDelegate OnProcessCollision;
        private Rigidbody r;
        private ConfigurableJoint j;
        private float w = 1f;
        private Vector3 targetVelocity;
        private Vector3 lastTargetPosition;
        private float targetVelMag;
        private const float springMlp = 1000f;
        private const float damperMlp = 100f;

        protected virtual void Start()
        {
            // Add the Rigidbody
            r = gameObject.AddComponent<Rigidbody>();
            r.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            r.interpolation = RigidbodyInterpolation.Interpolate;
            OnRecenter();
            // Making sure the target has a Rigidbody so we could attach a Joint
            var targetR = target.gameObject.GetComponent<Rigidbody>();
            if (targetR == null)
            {
                targetR = target.gameObject.AddComponent<Rigidbody>();
                targetR.isKinematic = true;
            }
            // Build the joint for rotation
            j = gameObject.AddComponent<ConfigurableJoint>();
            j.connectedBody = targetR;
            j.xMotion = ConfigurableJointMotion.Free;
            j.yMotion = ConfigurableJointMotion.Free;
            j.zMotion = ConfigurableJointMotion.Free;
            j.angularXMotion = ConfigurableJointMotion.Free;
            j.angularYMotion = ConfigurableJointMotion.Free;
            j.angularZMotion = ConfigurableJointMotion.Free;
            j.rotationDriveMode = RotationDriveMode.Slerp;
            lastTargetPosition = target.position;
        }

        // Call when position/rotation needs to be updated immediately.
        public void OnRecenter()
        {
            transform.position = target.position;
            transform.rotation = target.rotation;
            lastTargetPosition = target.position;
        }

        private void Update()
        {
            Vector3 targetVelocity = (target.position - lastTargetPosition) / Time.deltaTime;
            if (Time.deltaTime == 0f) targetVelocity = Vector3.zero;
            targetVelMag = targetVelocity.magnitude;
            lastTargetPosition = target.position;
            if (TrackDivergenceDistance && Vector3.Distance(target.position, transform.position) > DivergenceDistanceThreshold.magnitude || targetVelMag > DivergenceForceThreshold)
            {
                OnRecenter();
            }
        }

        void FixedUpdate()
        {
            // Kinematic, do nothing
            if (r.isKinematic)
            {
                r.MovePosition(target.position);
                r.MoveRotation(Quaternion.identity);
                return;
            }
            // Regaining spring
            w += Time.deltaTime * regainSpringSpeed;
            w = Mathf.Min(w, 1f);
            // Mass
            r.mass = mass + targetVelMag * velocityMassAdd;
            // Drag
            float d = (1f - w) * drag;
            r.linearDamping = d;
            r.angularDamping = d;
            // Position
            Vector3 positionOffset = target.position - r.position;
            Vector3 p = positionOffset / Time.fixedDeltaTime;
            Vector3 force = -r.linearVelocity + p;
            force *= w;
            force *= spring;
            r.AddForce(force, ForceMode.VelocityChange);
            // Rotation
            var drive = new JointDrive();
            drive.positionSpring = rotationSpring * w * spring;
            drive.positionDamper = rotationDamper + collisionDamperAdd * (1f - w);
            drive.positionDamper += targetVelMag * velocityDamperAdd;
            drive.maximumForce = float.MaxValue;
            j.slerpDrive = drive;
        }

        void OnCollisionEnter(Collision c)
        {
            ProcessCollision(c, false);
        }

        void OnCollisionStay(Collision c)
        {
            ProcessCollision(c, true);
        }

        // Reducing spring by collision impulse
        protected virtual void ProcessCollision(Collision c, bool isStay)
        {
            if (c.contactCount == 0) return;
            if (c.collider.attachedRigidbody == null) return;
            float impulse = GetImpulse(c);
            w -= impulse * collisionSpringLoss * Mathf.Clamp(w, 0f, 1f);
            w = Mathf.Max(w, Mathf.Min(spring, minSpring));
            if (OnProcessCollision != null) OnProcessCollision(c, impulse, isStay);
        }

        // If you need to extend DynamicAnchor, you can override this to specify spring loss per layer or muscle type or whatever you need.
        protected virtual float GetImpulse(Collision c)
        {
            return c.impulse.magnitude;
        }

        public void UpdateMass()
        {
            Debug.Log("TimeScale: " + Time.timeScale);
            mass = baseMass * Time.timeScale * Time.timeScale;
            velocityMassAdd = baseVelocityMassAdd * Time.timeScale * Time.timeScale;
        }

        public void MaximizeMass()
        {
            mass = baseMass;
            velocityMassAdd = baseVelocityMassAdd;
        }

        public void MinimizeMass()
        {
            mass = baseMass * 0.01f;
            velocityMassAdd = baseVelocityMassAdd * 0.01f;
        }
    }
}
