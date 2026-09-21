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
        public static CreaturePartInstance[] Instantiate(CreatureGenome genome, Transform[] bones, CreaturePartCatalog catalog,
            CreatureSkinPalette palette = null)
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
                    Spawn(genome, definition, gene, bone, catalog, palette, mirrored: false), i, mirrored: false));

                if (gene.Mirrored && definition.MirrorCapable)
                {
                    instances.Add(new CreaturePartInstance(
                        Spawn(genome, definition, gene, bone, catalog, palette, mirrored: true), i, mirrored: true));
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
            Transform bone, CreaturePartCatalog catalog, CreatureSkinPalette palette, bool mirrored)
        {
            // The attachment point and the player's own rotation are both reflected, and for different
            // reasons. The point, so the part lands on its own side of the body. The rotation, because
            // it is a correction the player made to <b>one</b> leg: splaying the right leg outwards has
            // to splay the left one outwards too, not send both the same way round the world. Without
            // this a mirrored pair turned in parallel, like a pair of legs on a turning wheel.
            PartGene effective = mirrored
                ? gene.WithLocalPosition(MirrorAcrossYZ(gene.LocalPosition)).WithLocalRotation(MirrorAcrossYZ(gene.LocalRotation))
                : gene;

            // Legs aim their foot at the ground, every other part aims away from the body.
            bool groundAligned = definition.Category == PartCategory.Locomotion;

            // The <b>pose</b>, on the other hand, needs no mirroring: it is resolved from scratch from
            // the mirrored attachment point, so the normal it is built on already points left.
            PartPose pose = PartPlacement.Resolve(genome, effective, definition.SkinOffset, groundAligned);
            Quaternion rotation = PartPlacement.FinalRotation(pose, effective);

            GameObject instance = Object.Instantiate(definition.Prefab, bone);
            instance.name = mirrored ? $"{definition.PartKey}_L" : definition.PartKey;

            // The foot goes in before anything else is done to the limb: it has to be mirrored with it,
            // painted with it and — for a leg — skinned onto the same chain. Adding it afterwards would
            // leave a right-hand hoof on a left leg and an unpainted foot under a painted limb.
            FitInto(instance, genome, gene, catalog);

            // A part's geometry does not have to start at its root — when it does not, the part hovers
            // above the skin by exactly the empty space in front of it in the prefab. We seat it against
            // the body.
            float seat = SeatDepth(definition);

            instance.transform.localPosition = pose.Position - rotation * (Vector3.forward * (seat * gene.Scale));
            instance.transform.localRotation = rotation;

            // Uniform scale. Never localScale.x = -1: negative scale flips the triangle winding, breaks
            // the lighting and gives colliders turned inside out.
            instance.transform.localScale = Vector3.one * gene.Scale;

            // The left-hand piece is a real reflection, not a second copy of the right one. The models
            // are right-hand — a right arm, a right hind leg — so without this a creature grows two
            // right hands, which reads as wrong long before anyone works out why.
            if (mirrored) MirroredMeshCache.Apply(instance);

            Paint(instance, genome, gene, palette);

            return instance;
        }

        /// <summary>
        /// Seats the part fitted into this limb's socket — the foot on a leg, the hand on an arm.
        /// </summary>
        /// <remarks>
        /// <para><b>The foot becomes part of the limb's own object</b>, not a separate attachment on the
        /// bone. Everything downstream then treats the limb as one piece for free: the leg chain adopts
        /// it along with the rest of the model and skins it onto the shank, so the foot swings with the
        /// leg; mirroring reflects it; the paint reaches it.</para>
        ///
        /// <para>The socket is an empty object in the limb's prefab, so where the foot sits — and which
        /// way it points — is decided by whoever built the limb, not by a rule in code.</para>
        /// </remarks>
        private static void FitInto(GameObject limb, CreatureGenome genome, in PartGene gene, CreaturePartCatalog catalog)
        {
            if (!gene.HasFitting || catalog == null) return;

            Transform socket = FindSocket(limb.transform);
            if (socket == null)
            {
                Debug.LogWarning($"Part \"{limb.name}\" has something fitted but no \"{CreaturePartDefinition.SocketName}\" — skipping the fitting.");
                return;
            }

            if (!catalog.TryGetPart(gene.FittingId, out CreaturePartDefinition fitting) || fitting == null || fitting.Prefab == null)
            {
                // A catalog without that foot degrades to a limb ending in a stump, exactly as an
                // unknown part degrades to no part at all.
                Debug.LogWarning($"No fitting with id {gene.FittingId} in catalog \"{catalog.name}\" — the limb keeps its stump.");
                return;
            }

            GameObject instance = Object.Instantiate(fitting.Prefab, socket);
            instance.name = fitting.PartKey;
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
        }

        /// <summary>The socket, wherever it sits in the prefab's hierarchy.</summary>
        private static Transform FindSocket(Transform limb)
        {
            foreach (Transform node in limb.GetComponentsInChildren<Transform>(true))
                if (node.name == CreaturePartDefinition.SocketName) return node;

            return null;
        }

        /// <summary>
        /// Paints the part: the colour the player gave it, or the creature's own, and its coat pattern.
        /// </summary>
        /// <remarks>
        /// <para>Part models arrive in whatever colour they were authored in — a pink arm next to a
        /// lime tentacle. That reads as a pile of spare parts rather than one animal, so the colour
        /// comes from the genome: the part's own <see cref="PartGene.Tint"/> when the player painted
        /// it, and <see cref="CreatureGenome.SecondaryColor"/> when they did not.</para>
        ///
        /// <para>Genome-driven, therefore identical on the server and on every client — the paintwork
        /// is part of the same deterministic build as the geometry, and nothing about it is sent
        /// separately.</para>
        /// </remarks>
        private static void Paint(GameObject instance, CreatureGenome genome, in PartGene gene, CreatureSkinPalette palette)
        {
            SkinPattern pattern = palette != null ? palette.Get(gene.PatternId) : null;
            CreatureSkinPainter.Paint(instance, gene.ResolveTint(genome.SecondaryColor), pattern);
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
