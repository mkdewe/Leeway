using Leeway.Gravity;
using System.Collections.Generic;
using Leeway.Creature.Domain;
using UnityEngine;

namespace Leeway.CreatureEditor
{
    /// <summary>
    /// What a creature leaves behind. Every creature has a skeleton drawn from its own build - a skull at
    /// the head, a spine with ribs, the long bones of its legs and arms - hidden under the skin. When it
    /// dies the body decays: the skin crumbles away and browns, the parts shrink, and the bones are left.
    /// When the body is finally taken away the skeleton stays where it lay, and lies there a long time.
    /// </summary>
    /// <remarks>
    /// <para><b>Built from the look, not from a model.</b> The pieces are made from the genome and the
    /// bones' own positions: the spine's vertebrae from the creature's radii, the ribs from how wide it is,
    /// the limbs from the chains the legs are made of. A long-necked, eight-legged creature leaves a long
    /// neck and eight sets of limb bones, with no bone model authored for any of them.</para>
    ///
    /// <para><b>Local and unreplicated.</b> The decay and the bones are a picture of a fact the network
    /// already carries (the creature is dead), so each machine draws its own. The pieces are parented to the
    /// body's bones, so they lie exactly as the ragdoll does; when the body goes they are let go with their
    /// pose and carry on as props.</para>
    /// </remarks>
    [RequireComponent(typeof(CreatureBody))]
    public sealed class CreatureRemains : MonoBehaviour
    {
        [Tooltip("The bone material - a Leeway/Toon with a bone colour.")]
        [SerializeField] private Material _boneMaterial;

        [Tooltip("Seconds a body takes to fall to pieces after it dies.")]
        [SerializeField, Min(5f)] private float _decaySeconds = 40f;

        [Tooltip("Seconds the skeleton lies there once the body is gone, before it sinks into the ground.")]
        [SerializeField, Min(10f)] private float _boneSeconds = 300f;

        private static readonly int DecayId = Shader.PropertyToID("_Decay");

        private CreatureBody _body;
        private CreatureLocomotion _locomotion;
        private readonly List<GameObject> _pieces = new();
        private readonly List<Renderer> _bodyRenderers = new();
        private MaterialPropertyBlock _block;
        private float _deadFor;
        private bool _built;
        private bool _quitting;

        /// <summary>How far the body has fallen apart: 0 intact, 1 gone.</summary>
        public float Decay => Mathf.Clamp01(_deadFor / _decaySeconds);

        /// <summary>Whether the skeleton has been drawn.</summary>
        public bool HasSkeleton => _built;

        private void Awake()
        {
            _body = GetComponent<CreatureBody>();
            _locomotion = GetComponent<CreatureLocomotion>();
            _block = new MaterialPropertyBlock();
        }

        private void OnApplicationQuit() => _quitting = true;

        private void Update()
        {
            if (_body == null || _body.Built == null) return;

            if (_body.IsAlive)
            {
                // A revived creature (a respawn builds a new body, but a test may heal this one) is whole again.
                if (_built) ClearSkeleton();
                return;
            }

            if (!_built) BuildSkeleton();

            _deadFor += Time.deltaTime;
            ApplyDecay(Decay);
        }

        private void ClearSkeleton()
        {
            foreach (GameObject piece in _pieces)
                if (piece != null) Destroy(piece);

            _pieces.Clear();
            _built = false;
            _deadFor = 0f;
            ApplyDecay(0f);
        }

        // ---------------------------------------------------------------- the skeleton

        private void BuildSkeleton()
        {
            _built = true;
            BuiltCreatureBody built = _body.Built;
            CreatureGenome genome = _body.Genome;
            Transform[] bones = built.Bones;
            if (bones == null || bones.Length == 0 || genome == null) return;

            // Everything that is the body and not the bones about to be added: the skin, the parts, the legs. They are
            // collected from the built body rather than searched for under this object, because the physical puppet
            // carries some of them off into the creatures' own physics scene.
            _bodyRenderers.Clear();
            if (built.Renderer != null) _bodyRenderers.Add(built.Renderer);

            foreach (CreaturePartInstance part in built.PartInstances)
                if (part.Object != null) _bodyRenderers.AddRange(part.Object.GetComponentsInChildren<Renderer>(true));

            if (_locomotion != null)
                foreach (ProceduralLeg leg in _locomotion.Legs)
                    foreach (Transform segment in leg.Segments)
                        if (segment != null) _bodyRenderers.AddRange(segment.GetComponentsInChildren<Renderer>(true));

            int count = bones.Length;
            for (int i = 0; i < count; i++)
            {
                if (bones[i] == null) continue;

                float radius = i < genome.VertebraCount ? genome.GetVertebra(i).Radius : 0.2f;
                float thickness = Mathf.Clamp(radius * 0.13f, 0.018f, 0.07f);

                var piece = new SkeletonMesh();

                // The spine: this vertebra to the next, which narrows towards the tail.
                if (i + 1 < count && bones[i + 1] != null)
                {
                    float taper = Mathf.Lerp(1f, 0.55f, i / (float)Mathf.Max(1, count - 1));
                    piece.Bone(Vector3.zero, bones[i].InverseTransformPoint(bones[i + 1].position), thickness * taper);
                }
                else piece.Knob(Vector3.zero, thickness * 0.7f);

                if (i == 0) AddSkull(piece, bones[i], radius);
                else if (i < count - 1) AddRibs(piece, bones[i], radius, thickness);

                Attach(bones[i], piece, "Vertebra");
            }

            AddLimbs();
        }

        /// <summary>The skull: a rounded cranium, a lower jaw and dark sockets where the eyes were.</summary>
        private void AddSkull(SkeletonMesh piece, Transform head, float radius)
        {
            float r = Mathf.Max(0.07f, radius * 0.75f);
            Vector3 forward = head.InverseTransformDirection(transform.forward);
            if (forward.sqrMagnitude < 1e-4f) forward = Vector3.forward;
            forward.Normalize();
            Vector3 up = head.InverseTransformDirection(GravityWorld.UpAt(head.position,gameObject.scene)).normalized;
            Quaternion look = Quaternion.LookRotation(forward, up);

            piece.Knob(forward * r * 0.55f + up * r * 0.1f, r, new Vector3(0.9f, 0.85f, 1.15f), look);
            piece.Knob(forward * r * 1.35f - up * r * 0.15f, r * 0.55f, new Vector3(0.7f, 0.55f, 1.1f), look);
            piece.Bone(forward * r * 0.4f - up * r * 0.55f, forward * r * 1.7f - up * r * 0.5f, r * 0.14f);
        }

        /// <summary>Ribs: arcs round the spine, a few to a vertebra, narrower towards the tail.</summary>
        private void AddRibs(SkeletonMesh piece, Transform bone, float radius, float thickness)
        {
            Vector3 right = bone.InverseTransformDirection(transform.right).normalized;
            Vector3 up = bone.InverseTransformDirection(GravityWorld.UpAt(bone.position,gameObject.scene)).normalized;
            Vector3 forward = Vector3.Cross(right, up).normalized;

            float r = Mathf.Max(0.06f, radius * 0.92f);
            for (int k = -1; k <= 1; k++)
            {
                Vector3 along = forward * (k * r * 0.55f);
                foreach (float side in new[] { -1f, 1f })
                {
                    Vector3 previous = along + up * (r * 0.15f);
                    for (int s = 1; s <= 4; s++)
                    {
                        float angle = s / 4f * Mathf.PI * 0.62f;
                        Vector3 point = along + right * (side * Mathf.Sin(angle) * r) + up * (Mathf.Cos(angle) * r * 0.9f);
                        piece.Bone(previous, point, thickness * 0.3f, 4);
                        previous = point;
                    }
                }
            }
        }

        /// <summary>The bones of every leg and arm, along the very chain the limb is made of.</summary>
        private void AddLimbs()
        {
            if (_locomotion == null) return;

            foreach (ProceduralLeg leg in _locomotion.Legs)
            {
                var anchors = leg.MuscleTargets;
                var joints = leg.Joints;
                if (anchors == null || joints == null) continue;

                for (int i = 0; i < anchors.Count && i + 1 < joints.Count; i++)
                {
                    Transform anchor = anchors[i];
                    if (anchor == null) continue;

                    var piece = new SkeletonMesh();
                    float thickness = Mathf.Lerp(0.032f, 0.02f, i / (float)Mathf.Max(1, anchors.Count));
                    piece.Bone(anchor.InverseTransformPoint(joints[i]), anchor.InverseTransformPoint(joints[i + 1]), thickness);

                    // The last link ends in a few toe bones, spread like a foot.
                    if (i == anchors.Count - 1)
                    {
                        Vector3 foot = anchor.InverseTransformPoint(joints[i + 1]);
                        Vector3 direction = (foot - anchor.InverseTransformPoint(joints[i])).normalized;
                        Vector3 spread = Vector3.Cross(direction, GravityWorld.UpAt(transform.position,gameObject.scene)).normalized * 0.04f;
                        for (int toe = -1; toe <= 1; toe++)
                            piece.Bone(foot, foot + direction * 0.07f + spread * toe, 0.012f, 4);
                    }

                    Attach(anchor, piece, "LimbBone");
                }
            }
        }

        private void Attach(Transform parent, SkeletonMesh mesh, string name)
        {
            if (mesh.IsEmpty) return;

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh.ToMesh(name);

            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _boneMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

            _pieces.Add(go);
        }

        // ---------------------------------------------------------------- decay

        /// <summary>The body crumbles; the bones, which are not in the list, do not.</summary>
        private void ApplyDecay(float decay)
        {
            foreach (Renderer renderer in _bodyRenderers)
            {
                if (renderer == null || IsBone(renderer)) continue;

                renderer.GetPropertyBlock(_block);
                _block.SetFloat(DecayId, decay);
                renderer.SetPropertyBlock(_block);

                // By the end nothing of it is left to draw.
                renderer.enabled = decay < 0.985f;
            }
        }

        private bool IsBone(Renderer renderer) => renderer.gameObject.name == "Vertebra" || renderer.gameObject.name == "LimbBone";

        // ---------------------------------------------------------------- what stays

        private void OnDestroy()
        {
            if (_quitting || !_built || !Application.isPlaying || !gameObject.scene.isLoaded) return;

            // The body is going: let the bones go with their pose and leave them lying.
            foreach (GameObject piece in _pieces)
            {
                if (piece == null) continue;

                piece.transform.SetParent(null, true);
                piece.AddComponent<RemainsLifetime>().Begin(_boneSeconds);
            }

            _pieces.Clear();
        }
    }

    /// <summary>A bone left behind: it lies there for a long while, then sinks into the ground and is gone.</summary>
    public sealed class RemainsLifetime : MonoBehaviour
    {
        private float _remaining;
        private float _sinkFor = 12f;

        public void Begin(float seconds) => _remaining = seconds;

        private void Update()
        {
            _remaining -= Time.deltaTime;
            if (_remaining > 0f) return;

            transform.position -= GravityWorld.UpAt(transform.position,gameObject.scene) * (0.05f * Time.deltaTime);
            _sinkFor -= Time.deltaTime;
            if (_sinkFor <= 0f) Destroy(gameObject);
        }
    }
}
