using UnityEngine;
using System.Collections;
using RootMotion.Dynamics;

namespace RootMotion.Demos
{

    /// <summary>
    /// Adjusts PuppetMaster and BehaviourPuppet behaviour, dynamic pin and mapping weight control based on collisions with the hands and objects.
    /// </summary>
    public class VRPuppet : MonoBehaviour
    {

        [System.Serializable]
        public class GroupWeight
        {
            [Tooltip("The muscle groups to apply this pinWeightMlp and muscleWeightMlp to.")]
            public Muscle.Group[] groups;
            [Range(0f, 1f)] public float pinWeightMlp = 0.5f;
            [Range(0f, 1f)] public float muscleWeightMlp = 0.5f;
        }

        public PuppetMaster puppetMaster;
        public BehaviourPuppet puppet;

        [Tooltip("When the puppet is touched, sets pin weight and muscle weight values for these groups.")]
        public GroupWeight[] groupWeights = new GroupWeight[0];
        [Tooltip("When the puppet is touched, sets muscle Rigidbody drag to this value to reduce the rubber chicken effect.")]
        public float drag = 2f;
        [Tooltip("The time of blending in this script's effects when the puppet is touched.")]
        public float blendInTime = 0.05f;
        [Tooltip("The time of blending out this script's effects when the puppet is not touched any more.")]
        public float blendOutTime = 1f;

        private float dam = 0f;
        private float damTime = -100f;
        private float damV;
        private float map, mapV;
        private float dragW = 1f;

        private float GetPinWeightMlp(Muscle m)
        {
            foreach (GroupWeight w in groupWeights)
            {
                foreach (Muscle.Group g in w.groups)
                {
                    if (g == m.props.group) return w.pinWeightMlp;
                }
            }
            return 1f;
        }

        private float GetMuscleWeightMlp(Muscle m)
        {
            foreach (GroupWeight w in groupWeights)
            {
                foreach (Muscle.Group g in w.groups)
                {
                    if (g == m.props.group) return w.muscleWeightMlp;
                }
            }
            return 1f;
        }

        void Start()
        {
            // Register to get a call from the puppet if it collides with anything
            puppet.OnCollisionImpulse += OnCollisionImpulse;
            puppet.OnPreMuscleHit += OnMuscleHit;

            puppet.masterProps.normalMode = BehaviourPuppet.NormalMode.Active;
        }

        void Update()
        {
            // Dynamically adjust drag, pin and mapping weights based on collisions so we can have perfect animation until there is a collision
            dragW = Mathf.MoveTowards(dragW, 1f, Time.deltaTime * 2f);

            bool unpinned = !puppet.enabled || puppet.state == BehaviourPuppet.State.Unpinned || !puppetMaster.isAlive || puppetMaster.isKilling;

            float damTarget = Time.time > damTime + 0.2f ? 0f : 1f;
            if (unpinned) damTarget = 1f;

            float mapTarget = damTarget;
            if (unpinned) mapTarget = 1f;

            float sDampTime = damTarget > dam ? blendInTime : (puppet.enabled ? blendOutTime : blendInTime);
            dam = Mathf.SmoothDamp(dam, damTarget, ref damV, sDampTime);

            float mDampTime = mapTarget > map ? blendInTime : blendOutTime;
            map = Mathf.SmoothDamp(map, mapTarget, ref mapV, mDampTime);

            if (unpinned) dam = Mathf.Min(dam, map);

            float d = unpinned ? 0f : drag * dragW;
            float angularD = unpinned ? 0.05f : drag * dragW;
            float m = Mathf.Lerp(0f, 1f, map);

            for (int i = 0; i < puppet.puppetMaster.muscles.Length; i++)
            {
                puppet.puppetMaster.muscles[i].props.pinWeight = Mathf.Lerp(1f, GetPinWeightMlp(puppet.puppetMaster.muscles[i]), dam);
                puppet.puppetMaster.muscles[i].props.muscleWeight = Mathf.Lerp(1f, GetMuscleWeightMlp(puppet.puppetMaster.muscles[i]), dam);

                //puppet.puppetMaster.muscles[i].rigidbody.drag = d;
                puppetMaster.muscles[i].rigidbody.AddForce(-(puppetMaster.muscles[i].rigidbody.linearVelocity - puppetMaster.muscles[i].targetVelocity) * d * Time.deltaTime, ForceMode.VelocityChange);
                puppet.puppetMaster.muscles[i].rigidbody.angularDamping = angularD;

                puppet.puppetMaster.muscles[i].state.mappingWeightMlp = m;
            }
        }

        public void OnTouch()
        {
            damTime = Time.time;
        }

        // Called by BehaviourPuppet when it collides with something
        void OnCollisionImpulse(MuscleCollision c, float impulse)
        {
            damTime = Time.time;
        }

        // Called by BehaviourPuppet when it's muscles are hit via MuscleCollisionBroadCaster.Hit()
        void OnMuscleHit(MuscleHit hit)
        {
            damTime = Time.time;
            dragW = 0f;
        }
    }
}
