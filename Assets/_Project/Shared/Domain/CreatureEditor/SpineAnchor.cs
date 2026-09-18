using UnityEngine;

namespace Leeway.Creature.Domain
{
    /// <summary>
    /// The creature's anchor point: the fixed centre of the body that everything turns around.
    /// </summary>
    /// <remarks>
    /// <para>The genome describes the spine from the head (vertebra 0 at the origin) backwards, so
    /// the <b>raw</b> armature root sits on the tip of the nose. Leaving it there has three
    /// unpleasant consequences: the creature turns around its own nose instead of its torso, the
    /// camera keeps its distance from the nose and ends up inside the body, and every added
    /// vertebra shifts the carcass relative to its own anchor.</para>
    ///
    /// <para>So the armature is offset such that the <b>centre of the spine</b> lands on the anchor
    /// point. The "forward" direction is unchanged — it is still the head-and-eyes side, i.e.
    /// <c>+Z</c>.</para>
    /// </remarks>
    public static class SpineAnchor
    {
        /// <summary>
        /// The armature's offset relative to the root that puts the centre of the body on the
        /// anchor point.
        /// </summary>
        public static Vector3 Offset(CreatureGenome genome)
        {
            if (genome == null || genome.VertebraCount == 0) return Vector3.zero;

            return -Center(genome);
        }

        /// <summary>
        /// Bone matrices relative to the root, with the anchor taken into account. Computed straight
        /// from the genome, so the result does not depend on where the creature is standing.
        /// </summary>
        public static Matrix4x4[] BoneToRoot(CreatureGenome genome)
        {
            if (genome == null) return new Matrix4x4[0];

            var matrices = new Matrix4x4[genome.VertebraCount];
            Matrix4x4 accumulated = Matrix4x4.Translate(Offset(genome));

            for (int i = 0; i < genome.VertebraCount; i++)
            {
                VertebraGene gene = genome.GetVertebra(i);
                accumulated *= Matrix4x4.TRS(gene.LocalOffset, gene.LocalRotation, Vector3.one);
                matrices[i] = accumulated;
            }

            return matrices;
        }

        /// <summary>The centre of the spine in raw armature space (vertebra 0 at the origin).</summary>
        public static Vector3 Center(CreatureGenome genome)
        {
            if (genome == null || genome.VertebraCount == 0) return Vector3.zero;

            Matrix4x4 accumulated = Matrix4x4.identity;
            Vector3 min = Vector3.zero;
            Vector3 max = Vector3.zero;

            for (int i = 0; i < genome.VertebraCount; i++)
            {
                VertebraGene gene = genome.GetVertebra(i);
                accumulated *= Matrix4x4.TRS(gene.LocalOffset, gene.LocalRotation, Vector3.one);

                Vector3 position = accumulated.GetColumn(3);

                if (i == 0)
                {
                    min = max = position;
                    continue;
                }

                min = Vector3.Min(min, position);
                max = Vector3.Max(max, position);
            }

            return (min + max) * 0.5f;
        }

        /// <summary>
        /// The "forward" direction in creature space. Always the head side, i.e. where the eyes
        /// sit — <c>W</c> on the keyboard leads exactly this way.
        /// </summary>
        public static Vector3 Forward => Vector3.forward;
    }
}
