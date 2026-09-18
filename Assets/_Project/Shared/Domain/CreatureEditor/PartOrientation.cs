using UnityEngine;

namespace Leeway.Creature.Domain
{
    /// <summary>
    /// Derives a part's rotation from where it sits on the body: a part looks <b>away</b> from the
    /// spine, so an eye on the flank aims sideways and a leg underneath aims down.
    /// </summary>
    /// <remarks>
    /// <para>The rotation stored in <see cref="PartGene.LocalRotation"/> is not the final rotation
    /// but the <b>player's correction layered on top</b> of whatever this class computes
    /// (see <see cref="Resolve"/>). That way dragging an eye to the other side of the body turns it
    /// automatically, while a manual tilt from the gizmo is not lost on every move.</para>
    ///
    /// <para>A pure function of the genome — no <c>Transform</c>s at all. The server and every client
    /// build the body from the same genome, so the orientation has to come out identical everywhere,
    /// just like the rest of the body build.</para>
    /// </remarks>
    public static class PartOrientation
    {
        /// <summary>Below this radius we treat the part as sitting on the spine axis and reach for a fallback direction.</summary>
        private const float RadialEpsilon = 1e-4f;

        /// <summary>Above this dot product the look direction is too parallel to the spine to use it as "up".</summary>
        private const float ParallelDot = 0.99f;

        /// <summary>
        /// The automatic rotation of a part attached to the given vertebra, in that bone's space.
        /// </summary>
        public static Quaternion Resolve(CreatureGenome genome, int boneIndex, Vector3 localPosition)
        {
            if (genome == null || boneIndex < 0 || boneIndex >= genome.VertebraCount)
                return Quaternion.identity;

            Vector3 tangent = SpineTangent(genome, boneIndex);
            Vector3 forward = ResolveForward(genome, boneIndex, localPosition, tangent);

            return LookOutward(forward, tangent);
        }

        /// <summary>
        /// Composes a rotation looking along <paramref name="forward"/>. The spine tangent is the
        /// natural "up": it keeps the roll stable along the whole body. When the part looks along the
        /// spine (a beak on the tip of the head) the tangent is parallel and a fallback is needed.
        /// </summary>
        public static Quaternion LookOutward(Vector3 forward, Vector3 tangent)
        {
            if (forward.sqrMagnitude < RadialEpsilon) return Quaternion.identity;

            Vector3 up = Mathf.Abs(Vector3.Dot(forward, tangent)) < ParallelDot ? tangent : Vector3.up;
            if (Mathf.Abs(Vector3.Dot(forward, up)) > ParallelDot) up = Vector3.right;

            return Quaternion.LookRotation(forward, up);
        }

        /// <summary>The final rotation: the automatic orientation with the player's correction layered on top.</summary>
        public static Quaternion ResolveFinal(CreatureGenome genome, in PartGene gene)
            => Resolve(genome, gene.BoneIndex, gene.LocalPosition) * gene.LocalRotation;

        /// <summary>
        /// The spine direction in a given bone's space, pointing towards the tail. We average both
        /// neighbouring segments so it does not jump from vertebra to vertebra across a bend.
        /// </summary>
        public static Vector3 SpineTangent(CreatureGenome genome, int boneIndex)
        {
            bool hasNext = boneIndex + 1 < genome.VertebraCount;
            bool hasPrev = boneIndex > 0;

            // The successor is a child, so its offset is already in this bone's space.
            Vector3 toNext = hasNext ? genome.GetVertebra(boneIndex + 1).LocalOffset : Vector3.zero;

            // The predecessor is the parent: the vector to it has to be rotated into this bone's space.
            VertebraGene self = genome.GetVertebra(boneIndex);
            Vector3 toPrev = hasPrev ? Quaternion.Inverse(self.LocalRotation) * -self.LocalOffset : Vector3.zero;

            Vector3 tailward = Vector3.zero;
            if (hasNext && toNext.sqrMagnitude > RadialEpsilon) tailward += toNext.normalized;
            if (hasPrev && toPrev.sqrMagnitude > RadialEpsilon) tailward -= toPrev.normalized;

            return tailward.sqrMagnitude > RadialEpsilon ? tailward.normalized : Vector3.back;
        }

        private static Vector3 ResolveForward(CreatureGenome genome, int boneIndex, Vector3 localPosition, Vector3 tangent)
        {
            // The component perpendicular to the spine — that is "away from the body".
            Vector3 radial = localPosition - Vector3.Dot(localPosition, tangent) * tangent;
            if (radial.sqrMagnitude > RadialEpsilon) return radial.normalized;

            // The part sits exactly on the axis. At the ends of the body "outward" means along the
            // spine: forwards from the head, backwards from the tail.
            if (boneIndex == 0) return -tangent;
            if (boneIndex == genome.VertebraCount - 1) return tangent;

            // Mid-torso there is no distinguished direction — up is the least surprising one.
            Vector3 fallback = Vector3.up - Vector3.Dot(Vector3.up, tangent) * tangent;
            return fallback.sqrMagnitude > RadialEpsilon ? fallback.normalized : Vector3.up;
        }
    }
}
