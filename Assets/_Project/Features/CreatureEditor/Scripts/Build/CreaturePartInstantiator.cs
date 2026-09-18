using System.Collections.Generic;
using Leeway.Creature.Domain;
using UnityEngine;

namespace Leeway.CreatureEditor
{
    /// <summary>
    /// One instantiated part together with a pointer back to the gene it came from.
    /// </summary>
    /// <remarks>
    /// The mapping is not one-to-one — a mirrored part yields two instances from one gene, and a part
    /// with no prefab yields none. Without this pointer the editor could not translate a click on an
    /// eye back into an entry in the genome.
    /// </remarks>
    public readonly struct CreaturePartInstance
    {
        public readonly GameObject Object;
        public readonly int GeneIndex;
        public readonly bool Mirrored;

        public CreaturePartInstance(GameObject instance, int geneIndex, bool mirrored)
        {
            Object = instance;
            GeneIndex = geneIndex;
            Mirrored = mirrored;
        }
    }

    /// <summary>
    /// Hangs part prefabs onto the bones.
    /// </summary>
    /// <remarks>
    /// Parts are parented straight to a bone, so they ride along with the skinning for free — they do
    /// not need skinning or synchronising of their own. That is the whole payoff of the commit model:
    /// only the genome travels over the network, and every client reconstructs the parts
    /// deterministically. Which is why part prefabs are plain <c>GameObject</c>s, never
    /// <c>NetworkObject</c>s.
    /// </remarks>
    public static class CreaturePartInstantiator
    {
        public static CreaturePartInstance[] Instantiate(CreatureGenome genome, Transform[] bones, CreaturePartCatalog catalog)
        {
            var instances = new List<CreaturePartInstance>(genome.PartCount * 2);
            if (catalog == null) return instances.ToArray();

            for (int i = 0; i < genome.PartCount; i++)
            {
                PartGene gene = genome.GetPart(i);

                if (gene.BoneIndex >= bones.Length) continue;

                if (!catalog.TryGetPart(gene.PartId, out CreaturePartDefinition definition) || definition == null)
                {
                    // A client with an older catalog should degrade visually, not fall over.
                    Debug.LogWarning($"No part with id {gene.PartId} in catalog \"{catalog.name}\" — skipping.");
                    continue;
                }

                if (definition.Prefab == null)
                {
                    Debug.LogWarning($"Part \"{definition.PartKey}\" has no prefab — skipping.");
                    continue;
                }

                Transform bone = bones[gene.BoneIndex];

                instances.Add(new CreaturePartInstance(
                    Spawn(genome, definition, gene, bone, mirrored: false), i, mirrored: false));

                if (gene.Mirrored && definition.MirrorCapable)
                {
                    instances.Add(new CreaturePartInstance(
                        Spawn(genome, definition, gene, bone, mirrored: true), i, mirrored: true));
                }
            }

            return instances.ToArray();
        }

        /// <summary>
        /// Seats one piece on the skin. The mirrored piece gets the gene's position flipped and is
        /// resolved <b>from scratch</b> — that way it lands on the surface on its own side of the body
        /// even when the body is not symmetric, rather than inheriting a mirrored point computed for
        /// the right-hand side.
        /// </summary>
        private static GameObject Spawn(CreatureGenome genome, CreaturePartDefinition definition, in PartGene gene,
            Transform bone, bool mirrored)
        {
            PartGene effective = mirrored ? gene.WithLocalPosition(MirrorAcrossYZ(gene.LocalPosition)) : gene;

            // Legs aim their foot at the ground, every other part aims away from the body.
            bool groundAligned = definition.Category == PartCategory.Locomotion;

            // The rotation already comes out right for this side of the body — a normal computed from
            // the mirrored position points left on its own. Mirroring the quaternion again would undo it.
            PartPose pose = PartPlacement.Resolve(genome, effective, definition.SkinOffset, groundAligned);
            Quaternion rotation = PartPlacement.FinalRotation(pose, effective);

            GameObject instance = Object.Instantiate(definition.Prefab, bone);
            instance.name = mirrored ? $"{definition.PartKey}_L" : definition.PartKey;

            // A part's geometry does not have to start at its root — when it does not, the part hovers
            // above the skin by exactly the empty space in front of it in the prefab. We seat it against
            // the body.
            float seat = SeatDepth(definition);

            instance.transform.localPosition = pose.Position - rotation * (Vector3.forward * (seat * gene.Scale));
            instance.transform.localRotation = rotation;

            // Uniform scale. Never localScale.x = -1: negative scale flips the triangle winding, breaks
            // the lighting and gives colliders turned inside out.
            instance.transform.localScale = Vector3.one * gene.Scale;

            return instance;
        }

        /// <summary>
        /// How much empty space a prefab has in front of its geometry, measured along <c>+Z</c>.
        /// </summary>
        /// <remarks>
        /// <para><c>PartPlacement</c> seats the part's <b>root</b> on the skin, so a part whose geometry
        /// only starts some way further along hovers above the body by exactly that much. Measured in
        /// the catalog: tentacle 11.5 cm, hoofed legs 12.5 cm, horn 9.5 cm. This is not a surface
        /// computation error — the gap between the surface from <c>PartPlacement</c> and the body mesh
        /// is a fraction of a millimetre.</para>
        ///
        /// <para>We only seat <b>towards zero</b>, never the other way: a part modelled around its own
        /// root (an eye, an antenna, a nostril) has a negative start and is meant to be half sunk into
        /// the body. Pushing it out onto the skin would turn an eye into a ball stuck on the outside.</para>
        ///
        /// <para>Deliberate tuning stays in <c>SkinOffset</c> — it still works and adds to the seating.</para>
        /// </remarks>
        private static float SeatDepth(CreaturePartDefinition definition)
        {
            if (definition.Prefab == null) return 0f;

            Transform root = definition.Prefab.transform;
            float nearest = float.MaxValue;

            foreach (MeshFilter filter in definition.Prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh mesh = filter.sharedMesh;
                if (mesh == null) continue;

                Bounds bounds = mesh.bounds;
                Vector3 min = bounds.min;
                Vector3 max = bounds.max;

                for (int corner = 0; corner < 8; corner++)
                {
                    var point = new Vector3(
                        (corner & 1) == 0 ? min.x : max.x,
                        (corner & 2) == 0 ? min.y : max.y,
                        (corner & 4) == 0 ? min.z : max.z);

                    Vector3 local = root.InverseTransformPoint(filter.transform.TransformPoint(point));
                    nearest = Mathf.Min(nearest, local.z);
                }
            }

            return nearest == float.MaxValue ? 0f : Mathf.Max(0f, nearest);
        }

        private static Vector3 MirrorAcrossYZ(Vector3 position) => new Vector3(-position.x, position.y, position.z);

        /// <summary>Mirroring a rotation across the YZ plane — the components perpendicular to the mirror axis change sign.</summary>
        private static Quaternion MirrorAcrossYZ(Quaternion rotation)
            => new Quaternion(rotation.x, -rotation.y, -rotation.z, rotation.w);
    }
}
