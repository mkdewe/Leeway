using Leeway.Creature.Domain;
using UnityEngine;

namespace Leeway.CreatureEditor
{
    /// <summary>
    /// Builds the bone hierarchy from the spine. Every vertebra is one bone, and bone <c>i</c> is a
    /// child of bone <c>i-1</c>, so a bend propagates down the tail exactly as recorded in the genome.
    /// </summary>
    public static class CreatureRigBuilder
    {
        public const string ArmatureName = "Armature";

        /// <summary>
        /// Creates the armature under <paramref name="root"/> and returns the bones in vertebra order.
        /// The armature sits at identity relative to the root — an assumption the bindpose computation
        /// in <see cref="SpineMeshGenerator"/> relies on.
        /// </summary>
        public static Transform[] Build(CreatureGenome genome, Transform root, out Transform armature)
        {
            armature = new GameObject(ArmatureName).transform;
            armature.SetParent(root, false);
            armature.localPosition = Vector3.zero;
            armature.localRotation = Quaternion.identity;
            armature.localScale = Vector3.one;

            // The anchor goes into the first bone, not into the armature transform: the mesh, the
            // bindposes and the colliders are all computed from the same chain, so the offset has to
            // live inside it. A shifted armature would desynchronise the rest pose from the skinning.
            Vector3 anchor = SpineAnchor.Offset(genome);

            var bones = new Transform[genome.VertebraCount];
            Transform parent = armature;

            for (int i = 0; i < genome.VertebraCount; i++)
            {
                VertebraGene gene = genome.GetVertebra(i);

                var bone = new GameObject($"Vertebra_{i}").transform;
                bone.SetParent(parent, false);

                // Only the head gets the anchor — the rest of the chain follows it.
                bone.localPosition = i == 0 ? gene.LocalOffset + anchor : gene.LocalOffset;
                bone.localRotation = gene.LocalRotation;

                // The radius goes into the mesh geometry, never into the bone scale: non-uniform scale
                // under linear skinning produces shearing and breaks the collider fit.
                bone.localScale = Vector3.one;

                bones[i] = bone;
                parent = bone;
            }

            return bones;
        }

        /// <summary>
        /// Bone matrices relative to the root, computed straight from the genome. We deliberately do
        /// not read <c>Transform</c>s — that way the result does not depend on where in the world the
        /// root is standing, and is identical on every machine.
        /// </summary>
        public static Matrix4x4[] ComputeBoneToRoot(CreatureGenome genome)
        {
            // The anchor sits inside — see SpineAnchor. A shared source, so the mesh, the colliders and
            // the stance calculations cannot drift apart.
            return SpineAnchor.BoneToRoot(genome);
        }
    }
}
