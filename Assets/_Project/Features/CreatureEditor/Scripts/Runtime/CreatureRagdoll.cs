using System.Collections.Generic;
using LitMotion;
using UnityEngine;

namespace Leeway.CreatureEditor
{
    /// <summary>
    /// The creature's physical knockdown: a chain of rigid bodies on the spine bones, built together
    /// with the body and kept switched off until it falls over.
    /// </summary>
    /// <remarks>
    /// <para><b>Do not switch <c>NetworkManager → TimeManager._physicsMode</c> from <c>Unity</c> (0) to
    /// <c>TimeManager</c> (1) while the ragdolls live in the default physics scene.</b> In
    /// <c>TimeManager</c> mode the <c>PredictionManager</c> calls <c>SimulatePhysics</c> inside the
    /// reconciliation replay loop, and that runs the global <c>Physics.Simulate</c> — resimulating
    /// <b>every</b> body in the scene without resetting its state. The ragdoll would then run several
    /// times too fast and explode. The way out, for the future, is a separate <c>PhysicsScene</c>
    /// stepped by hand.</para>
    ///
    /// <para>The ragdoll is created <b>once, switched off</b>, when the body is built. Building it only
    /// on the knockdown gives a frame hitch and a one-frame jump of the joint solver — exactly at the
    /// moment the player is watching.</para>
    ///
    /// <para>The simulation itself is <b>cosmetic and purely local</b>. Bone poses will differ between
    /// clients, and that is the right trade: nobody will notice, while synchronising a dozen transforms
    /// at 30 Hz for a visual effect is a waste of bandwidth. The only binding value is the standing-up
    /// position, which the server computes.</para>
    /// </remarks>
    public class CreatureRagdoll : MonoBehaviour
    {
        [Header("Bone bodies")]
        [SerializeField] private float _boneDrag = 0.4f;
        [SerializeField] private float _boneAngularDrag = 3f;

        [Header("Joints")]
        [SerializeField] private float _swingLimitDegrees = 35f;
        [SerializeField] private float _twistLimitDegrees = 20f;

        [Header("Getting up")]
        [Tooltip("How long blending the ragdoll pose back into the rest pose takes. The getting-up phase " +
                 "in CreatureBody lasts the same - control returns exactly when the animation ends.")]
        [SerializeField] private float _recoverySeconds = 0.45f;
        [SerializeField] private float _groundProbeHeight = 3f;

        private readonly List<Rigidbody> _boneBodies = new();
        private readonly List<Collider> _boneColliders = new();
        private readonly List<Quaternion> _bindLocalRotations = new();
        private readonly List<Vector3> _bindLocalPositions = new();

        /// <summary>The bones' world pose at the moment physics was switched off — getting up starts from it.</summary>
        private readonly List<Vector3> _fallWorldPositions = new();
        private readonly List<Quaternion> _fallWorldRotations = new();

        private Transform[] _bones;
        private Rigidbody _rootBody;
        private Collider _locomotionCollider;
        private MotionHandle _recoveryMotion;

        public bool IsActive { get; private set; }
        public int BoneCount => _boneBodies.Count;

        /// <summary>How long getting up takes. The knockdown state keeps control locked for that long.</summary>
        public float RecoverySeconds => Mathf.Max(0f, _recoverySeconds);

        /// <summary>
        /// Recreates the ragdoll for a new rig. Called after every body rebuild — bones do not survive a
        /// rebuild, so the old chain would point at destroyed objects.
        /// </summary>
        public void Build(BuiltCreatureBody body, Rigidbody rootBody, Collider locomotionCollider, float totalMass, int ragdollLayer)
        {
            Clear();

            _rootBody = rootBody;
            _locomotionCollider = locomotionCollider;

            if (body?.Bones == null || body.Bones.Length == 0) return;

            _bones = body.Bones;
            float perBoneMass = Mathf.Max(0.2f, totalMass / _bones.Length);

            for (int i = 0; i < _bones.Length; i++)
            {
                Transform bone = _bones[i];
                if (bone == null) continue;

                _bindLocalRotations.Add(bone.localRotation);
                _bindLocalPositions.Add(bone.localPosition);

                var boneBody = bone.gameObject.AddComponent<Rigidbody>();
                boneBody.mass = perBoneMass;
                boneBody.linearDamping = _boneDrag;
                boneBody.angularDamping = _boneAngularDrag;
                boneBody.isKinematic = true;

                // Interpolation stays off while the creature is on its feet and only switches on together
                // with the ragdoll (see Activate). An interpolated body overwrites the transform every
                // frame with its own previous pose from physics — but the bone of a standing creature is
                // carried by the hierarchy, not by physics. The result was that the whole spine stayed
                // pinned to the rotation it had when it was built: the root turned while the carcass, and
                // everything hanging off it, kept looking sideways.
                boneBody.interpolation = RigidbodyInterpolation.None;

                var sphere = bone.gameObject.AddComponent<SphereCollider>();
                sphere.radius = Mathf.Max(0.08f, RadiusOf(body, i));
                sphere.enabled = false;
                bone.gameObject.layer = ragdollLayer;

                _boneBodies.Add(boneBody);
                _boneColliders.Add(sphere);

                if (i > 0) AttachJoint(boneBody, _boneBodies[i - 1]);
            }
        }

        private static float RadiusOf(BuiltCreatureBody body, int index)
        {
            // The vertebra radius is in the genome, but the ragdoll only gets the built body — the
            // distance to a neighbour is a good enough approximation of the thickness.
            if (body.Bones.Length < 2) return 0.2f;

            int other = index > 0 ? index - 1 : 1;
            return Vector3.Distance(body.Bones[index].position, body.Bones[other].position) * 0.45f;
        }

        private void AttachJoint(Rigidbody bone, Rigidbody parent)
        {
            var joint = bone.gameObject.AddComponent<ConfigurableJoint>();
            joint.connectedBody = parent;
            joint.autoConfigureConnectedAnchor = true;

            joint.xMotion = ConfigurableJointMotion.Locked;
            joint.yMotion = ConfigurableJointMotion.Locked;
            joint.zMotion = ConfigurableJointMotion.Locked;

            joint.angularXMotion = ConfigurableJointMotion.Limited;
            joint.angularYMotion = ConfigurableJointMotion.Limited;
            joint.angularZMotion = ConfigurableJointMotion.Limited;

            joint.lowAngularXLimit = new SoftJointLimit { limit = -_twistLimitDegrees };
            joint.highAngularXLimit = new SoftJointLimit { limit = _twistLimitDegrees };
            joint.angularYLimit = new SoftJointLimit { limit = _swingLimitDegrees };
            joint.angularZLimit = new SoftJointLimit { limit = _swingLimitDegrees };

            joint.enablePreprocessing = false;
        }

        /// <summary>Switches the creature into knockdown mode and drives an impulse into it.</summary>
        public void Activate(Vector3 impulse, int hitBoneIndex, Vector3 inheritedVelocity)
        {
            if (IsActive || _boneBodies.Count == 0) return;

            if (_recoveryMotion.IsActive()) _recoveryMotion.Cancel();
            IsActive = true;

            if (_rootBody != null) _rootBody.isKinematic = true;
            if (_locomotionCollider != null) _locomotionCollider.enabled = false;

            for (int i = 0; i < _boneBodies.Count; i++)
            {
                _boneColliders[i].enabled = true;
                _boneBodies[i].isKinematic = false;

                // The bone is governed by physics now, so interpolation makes sense again — it smooths a
                // fall computed in physics steps up to the frame rate.
                _boneBodies[i].interpolation = RigidbodyInterpolation.Interpolate;

                // Carrying the momentum over — without it a running creature falls on the spot.
                _boneBodies[i].linearVelocity = inheritedVelocity;
            }

            int index = Mathf.Clamp(hitBoneIndex, 0, _boneBodies.Count - 1);
            _boneBodies[index].AddForce(impulse, ForceMode.Impulse);
        }

        /// <summary>
        /// Switches the ragdoll's physics off and records the pose the fall left it in.
        /// </summary>
        /// <remarks>
        /// The blending itself is <b>not</b> here. Right after this call the server moves the root to the
        /// standing-up spot, and moving the root carries the whole bone hierarchy with it — a pose
        /// recorded earlier in local coordinates would travel along and getting up would start with a hop
        /// the height of the suspension. So we record the pose in <b>world space</b>, and the blend is
        /// started by <see cref="BlendToBindPose"/> after the move.
        /// </remarks>
        public void Deactivate()
        {
            if (!IsActive) return;
            IsActive = false;

            _fallWorldPositions.Clear();
            _fallWorldRotations.Clear();

            for (int i = 0; i < _boneBodies.Count; i++)
            {
                _boneBodies[i].isKinematic = true;
                _boneColliders[i].enabled = false;

                // The bone goes back under the hierarchy, so interpolation has to go off along with the
                // physics — otherwise from this moment on it would pin the spine to the current rotation.
                _boneBodies[i].interpolation = RigidbodyInterpolation.None;
            }

            if (_bones != null)
            {
                for (int i = 0; i < _bones.Length; i++)
                {
                    _fallWorldPositions.Add(_bones[i] != null ? _bones[i].position : Vector3.zero);
                    _fallWorldRotations.Add(_bones[i] != null ? _bones[i].rotation : Quaternion.identity);
                }
            }

            if (_locomotionCollider != null) _locomotionCollider.enabled = true;
            if (_rootBody != null) _rootBody.isKinematic = false;
        }

        /// <summary>
        /// Restores the pose the bones had when physics was switched off — in world space, so moving the
        /// root in the meantime does not disturb it.
        /// </summary>
        /// <remarks>
        /// We go from the root down the chain, because <c>SetPositionAndRotation</c> derives the local
        /// coordinates relative to the parent: a child has to land on an already-corrected parent,
        /// otherwise the error accumulates along the spine.
        /// </remarks>
        private void RestoreFallPose()
        {
            if (_bones == null || _fallWorldPositions.Count != _bones.Length) return;

            for (int i = 0; i < _bones.Length; i++)
            {
                if (_bones[i] == null) continue;
                _bones[i].SetPositionAndRotation(_fallWorldPositions[i], _fallWorldRotations[i]);
            }
        }

        /// <summary>
        /// Where the creature should stand once it has picked itself up. The <b>server</b> computes it —
        /// clients receive that position in the knockdown state, which removes any pose divergence.
        /// </summary>
        /// <param name="fallback">The position to return when there is no ground beneath the creature.</param>
        /// <param name="standHeight">
        /// How high above the ground the root stands when the creature is on its feet — exactly the same
        /// number the suspension holds (<c>GenomeStatRules.StandHeight</c>). Zero or less means "no legs",
        /// and then the clearance is computed from the capsule alone.
        /// </param>
        /// <remarks>
        /// This used to be <b>half the capsule's height</b>, and that was a mistake twice over. First,
        /// the locomotion capsule lies along <c>Z</c> (<c>direction = 2</c>), so its "height" is the
        /// <b>length</b> of the carcass, not the clearance under the belly — a two-metre creature stood
        /// up a metre above the ground. Second, even a correctly measured capsule is not what we are
        /// after: the creature is held up by its <b>suspension</b>, at a height that follows from the
        /// legs' reach, not by a collider resting on the ground. Putting it anywhere else means the
        /// spring starts from a hop or from a collapse — and that is what "the height broke" looked like
        /// after every fall.
        /// </remarks>
        public Vector3 ResolveRecoveryPosition(Vector3 fallback, float standHeight)
        {
            if (_boneBodies.Count == 0) return fallback;

            float clearance = standHeight > 0.001f ? standHeight : BellyClearance();

            Vector3 center = Vector3.zero;
            for (int i = 0; i < _boneBodies.Count; i++) center += _boneBodies[i].position;
            center /= _boneBodies.Count;

            var origin = new Vector3(center.x, center.y + _groundProbeHeight, center.z);
            int groundMask = ~((1 << gameObject.layer) | (1 << LayerMask.NameToLayer("CreatureRagdoll")));

            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, _groundProbeHeight * 3f, groundMask, QueryTriggerInteraction.Ignore))
                return hit.point + Vector3.up * clearance;

            return new Vector3(center.x, fallback.y, center.z);
        }

        /// <summary>
        /// How high above the ground the root has to be for the capsule to rest on its belly. This
        /// applies to a creature <b>with no legs</b> — one that nothing lifts.
        /// </summary>
        private float BellyClearance()
        {
            if (_locomotionCollider is not CapsuleCollider capsule) return 0.6f;

            // A lying capsule (direction 0/2) reaches vertically only by its radius; one standing on the
            // Y axis, by half its height.
            float reach = capsule.direction == 1
                ? Mathf.Max(capsule.height * 0.5f, capsule.radius)
                : capsule.radius;

            return Mathf.Max(0.05f, reach - capsule.center.y);
        }

        /// <summary>
        /// Blends the bones back into the rest pose — and <b>the positions, not just the rotations</b>.
        /// </summary>
        /// <remarks>
        /// Skipping the positions was the real cause of the "shifted centre" after a fall. In ragdoll
        /// mode every bone is moved by physics, so it writes its <c>localPosition</c> relative to the
        /// armature — and the joints only lock linear motion to within the solver's accuracy, while the
        /// first bone of the chain is pinned to nothing at all. What was left after getting up was a
        /// permanent offset of the whole spine relative to the capsule: the mesh, the parts, the hips and
        /// the suspension probes travelled beside the body the player actually steers, and it accumulated
        /// with every further fall. The rest pose is recorded at build time and is the single source of
        /// truth about where a bone belongs.
        /// </remarks>
        public void BlendToBindPose()
        {
            if (_bones == null || _bindLocalRotations.Count != _bones.Length) return;

            RestoreFallPose();

            var startRotations = new Quaternion[_bones.Length];
            var startPositions = new Vector3[_bones.Length];
            for (int i = 0; i < _bones.Length; i++)
            {
                startRotations[i] = _bones[i] != null ? _bones[i].localRotation : Quaternion.identity;
                startPositions[i] = _bones[i] != null ? _bones[i].localPosition : Vector3.zero;
            }

            // Restoring the pose hard in a single frame looks like an animation hitch; blending costs one
            // LitMotion and is purely cosmetic.
            _recoveryMotion = LMotion.Create(0f, 1f, _recoverySeconds)
                .WithEase(Ease.OutQuad)
                // Closing hard rather than by interpolation: a bone has to land exactly in the rest pose,
                // otherwise the remaining error stays for good and accumulates across further falls.
                .WithOnComplete(SnapToBindPose)
                .Bind(t =>
                {
                    for (int i = 0; i < _bones.Length; i++)
                    {
                        if (_bones[i] == null) continue;

                        _bones[i].localRotation = Quaternion.Slerp(startRotations[i], _bindLocalRotations[i], t);
                        _bones[i].localPosition = Vector3.Lerp(startPositions[i], _bindLocalPositions[i], t);
                    }
                })
                .AddTo(gameObject);
        }

        private void SnapToBindPose()
        {
            if (_bones == null || _bindLocalRotations.Count != _bones.Length) return;

            for (int i = 0; i < _bones.Length; i++)
            {
                if (_bones[i] == null) continue;

                _bones[i].localRotation = _bindLocalRotations[i];
                _bones[i].localPosition = _bindLocalPositions[i];
            }
        }

        private void Clear()
        {
            if (_recoveryMotion.IsActive()) _recoveryMotion.Cancel();

            // The order matters: a joint holding a destroyed body logs an error.
            for (int i = 0; i < _boneBodies.Count; i++)
            {
                if (_boneBodies[i] == null) continue;

                var joint = _boneBodies[i].GetComponent<ConfigurableJoint>();
                if (joint != null) DestroySafely(joint);
            }

            for (int i = 0; i < _boneColliders.Count; i++)
                if (_boneColliders[i] != null) DestroySafely(_boneColliders[i]);

            for (int i = 0; i < _boneBodies.Count; i++)
                if (_boneBodies[i] != null) DestroySafely(_boneBodies[i]);

            _boneBodies.Clear();
            _boneColliders.Clear();
            _bindLocalRotations.Clear();
            _bindLocalPositions.Clear();
            _fallWorldPositions.Clear();
            _fallWorldRotations.Clear();
            _bones = null;
            IsActive = false;
        }

        private static void DestroySafely(Object target)
        {
            if (target == null) return;

            if (Application.isPlaying) Destroy(target);
            else DestroyImmediate(target);
        }

        private void OnDestroy() => Clear();
    }
}
