using UnityEngine;

namespace Leeway.Creature.Domain
{
    /// <summary>Where and how to place a part: a point on the skin plus an "outward" rotation.</summary>
    public readonly struct PartPose
    {
        /// <summary>The position in bone space, already pushed out onto the body surface.</summary>
        public readonly Vector3 Position;

        public readonly Quaternion Rotation;

        /// <summary>The body normal at the attachment point — the direction the part looks in.</summary>
        public readonly Vector3 Outward;

        /// <summary>The body radius at that spot, i.e. the measured "skin thickness".</summary>
        public readonly float SurfaceRadius;

        public PartPose(Vector3 position, Quaternion rotation, Vector3 outward, float surfaceRadius)
        {
            Position = position;
            Rotation = rotation;
            Outward = outward;
            SurfaceRadius = surfaceRadius;
        }
    }

    /// <summary>
    /// Seats a part on the body surface and rotates it along that surface's normal.
    /// </summary>
    /// <remarks>
    /// <para>The body is a <b>tube capped with domes</b> — a capsule — and has to be measured exactly
    /// that way. Along the torso the skin lies at the radius <b>from the spine axis</b>, not from the
    /// centre of the vertebra: a point offset by the radius in a direction oblique to the spine lands
    /// <b>inside</b> the flesh. That was why parts sank into the body — the more the attachment
    /// direction lay along the torso, the deeper the part drowned.</para>
    ///
    /// <para>So the direction from the gene is split into two components in bone space: along the
    /// spine (kept, because it says <b>where</b> on the body the part sits) and sideways (pushed out
    /// to the skin, because it says <b>how thick</b> the body is at that spot). At the tips of the
    /// head and tail we switch to the dome model — a sphere with the end vertebra's radius, the same
    /// one the mesh generator closes the body with. That way a mouth placed on the leading edge looks
    /// <b>forwards</b> rather than up.</para>
    ///
    /// <para><b>The direction is computed purely from the gene's position</b>, never from a fixed
    /// socket offset. Previously the socket (a vector on the order of 0.2) dominated the drag and the
    /// part barely changed its rotation at all, while its small radial component could turn a jaw
    /// downwards. The socket is a scalar today: only "how far above the skin".</para>
    ///
    /// <para>The body radius is interpolated exactly the way the mesh generator does it, so the part
    /// sits on the same surface you see on screen and does not sink into it when a vertebra's
    /// thickness changes.</para>
    /// </remarks>
    public static class PartPlacement
    {
        private const float Epsilon = 1e-6f;

        /// <param name="groundAligned">
        /// The part should aim at the <b>ground</b> rather than away from the body. This applies to
        /// legs: a leg attached to the flank of the torso and rotated along the normal would stick out
        /// horizontally instead of reaching the ground with its foot. The attachment point is
        /// unchanged — only the facing is.
        /// </param>
        public static PartPose Resolve(CreatureGenome genome, int boneIndex, Vector3 localPosition, float skinOffset,
            bool groundAligned = false)
        {
            if (genome == null || boneIndex < 0 || boneIndex >= genome.VertebraCount)
                return new PartPose(localPosition, Quaternion.identity, Vector3.up, 0f);

            Vector3 request = ResolveRequest(genome, boneIndex, localPosition);
            Vector3 axis = SpineAxis(genome, boneIndex);

            float axial = Vector3.Dot(request, axis);
            Vector3 radial = request - axial * axis;

            float radius = genome.GetVertebra(boneIndex).Radius;

            // Past the end vertebra there is no tube any more, only the dome closing the body off.
            bool onCap = (boneIndex == 0 && axial < -Epsilon)
                      || (boneIndex == genome.VertebraCount - 1 && axial > Epsilon);

            Vector3 outward;
            Vector3 surfacePoint;
            float surfaceRadius;

            if (onCap)
            {
                // The dome is a sphere with the end vertebra's radius, so a point offset by that
                // radius in any direction lies exactly on the skin.
                outward = request.normalized;
                surfaceRadius = radius;
                surfacePoint = outward * radius;
            }
            else
            {
                // Tube: along the body the part stays where the player pointed, and sideways it comes
                // out by exactly as much flesh as there is at that particular spot.
                axial = ClampToOwnSegment(genome, boneIndex, axial);
                outward = RadialDirection(radial, axis);
                surfaceRadius = BodyRadiusAt(genome, boneIndex, axial);
                surfacePoint = axis * axial + outward * surfaceRadius;
            }

            Vector3 position = surfacePoint + outward * skinOffset;

            Quaternion rotation = groundAligned
                ? PartOrientation.LookOutward(GroundDirection(genome, boneIndex), ReferenceAxis)
                : PartOrientation.LookOutward(outward, ReferenceAxis);

            return new PartPose(position, rotation, outward, surfaceRadius);
        }

        /// <summary>
        /// The reference axis for a part's rotation, fixed in bone space.
        /// </summary>
        /// <remarks>
        /// Deliberately not the spine tangent: the tangent changes every time a neighbour moves, so
        /// the part would tilt for no reason. An attachment is meant to be <b>rigid relative to its
        /// own bone</b> — rotating the vertebra rotates the part with it, and nothing else does.
        /// </remarks>
        private static readonly Vector3 ReferenceAxis = Vector3.forward;

        public static PartPose Resolve(CreatureGenome genome, in PartGene gene, float skinOffset, bool groundAligned = false)
            => Resolve(genome, gene.BoneIndex, gene.LocalPosition, skinOffset, groundAligned);

        /// <summary>
        /// The "down" direction expressed in a given bone's space.
        /// </summary>
        /// <remarks>
        /// The creature's root stays upright (rotation in X and Z is frozen), so world down coincides
        /// with root down. Undoing the bone's own rotation is enough to make the leg aim at the ground
        /// regardless of how bent the spine is at that point.
        /// </remarks>
        private static Vector3 GroundDirection(CreatureGenome genome, int boneIndex)
        {
            Matrix4x4[] boneToRoot = SpineAnchor.BoneToRoot(genome);
            if (boneIndex >= boneToRoot.Length) return Vector3.down;

            return Quaternion.Inverse(boneToRoot[boneIndex].rotation) * Vector3.down;
        }

        /// <summary>The final rotation — automatic plus the player's correction from the gizmo.</summary>
        public static Quaternion FinalRotation(in PartPose pose, in PartGene gene) => pose.Rotation * gene.LocalRotation;

        /// <summary>
        /// The player's raw "request": the attachment direction stored in the gene. A zero attachment
        /// points nowhere, so at the tips of the body we send the part along the spine, and mid-body
        /// we send it up.
        /// </summary>
        private static Vector3 ResolveRequest(CreatureGenome genome, int boneIndex, Vector3 localPosition)
        {
            if (localPosition.sqrMagnitude > Epsilon) return localPosition;

            if (boneIndex == 0) return -SpineAxis(genome, boneIndex);
            if (boneIndex == genome.VertebraCount - 1) return SpineAxis(genome, boneIndex);

            return Vector3.up;
        }

        /// <summary>
        /// The "away from the body" direction, i.e. the component perpendicular to the spine.
        /// </summary>
        private static Vector3 RadialDirection(Vector3 radial, Vector3 axis)
        {
            if (radial.sqrMagnitude > Epsilon) return radial.normalized;

            // An attachment exactly along the spine points to no side of the body.
            Vector3 up = Vector3.up - Vector3.Dot(Vector3.up, axis) * axis;

            // A vertical spine — then any horizontal axis is already perpendicular.
            return up.sqrMagnitude > Epsilon ? up.normalized : Vector3.right;
        }

        /// <summary>
        /// The spine direction in a given bone's space, pointing towards the tail.
        /// </summary>
        /// <remarks>
        /// Computed purely from the vertebra's <b>own</b> gene (the segment from its predecessor),
        /// never as an average of both neighbours. An attachment is meant to be rigid relative to its
        /// own bone, so moving a neighbouring vertebra must not shift or rotate a part whose own bone
        /// did not even twitch. The head has no predecessor, so its only segment is the one leading to
        /// vertebra number 1.
        /// </remarks>
        private static Vector3 SpineAxis(CreatureGenome genome, int boneIndex)
        {
            if (boneIndex > 0)
            {
                VertebraGene self = genome.GetVertebra(boneIndex);

                // The offset is stored in the parent's space — undoing this bone's own rotation brings
                // it into this bone's space.
                Vector3 fromPrevious = Quaternion.Inverse(self.LocalRotation) * self.LocalOffset;
                if (fromPrevious.sqrMagnitude > Epsilon) return fromPrevious.normalized;
            }
            else if (genome.VertebraCount > 1)
            {
                Vector3 toNext = genome.GetVertebra(1).LocalOffset;
                if (toNext.sqrMagnitude > Epsilon) return toNext.normalized;
            }

            // The default convention: successive vertebrae run backwards along -Z.
            return Vector3.back;
        }

        /// <summary>
        /// Clamps the offset along the body to the stretch this vertebra is responsible for: half the
        /// way to its predecessor and half the way to its successor. Beyond that the skin belongs to
        /// the neighbour and is described by the neighbour's radius.
        /// </summary>
        private static float ClampToOwnSegment(CreatureGenome genome, int boneIndex, float axial)
        {
            float backward = boneIndex > 0
                ? genome.GetVertebra(boneIndex).LocalOffset.magnitude * 0.5f
                : 0f;

            float forward = boneIndex + 1 < genome.VertebraCount
                ? genome.GetVertebra(boneIndex + 1).LocalOffset.magnitude * 0.5f
                : 0f;

            return Mathf.Clamp(axial, -backward, forward);
        }

        /// <summary>
        /// The body radius at a given point along the segment, measured from the spine axis.
        /// <paramref name="axial"/> is positive towards the tail.
        /// </summary>
        /// <remarks>
        /// The smoothing has to be <b>the same</b> one the mesh generator uses (<c>t²(3-2t)</c>) —
        /// otherwise the part sits on a surface that is not on screen.
        /// </remarks>
        public static float BodyRadiusAt(CreatureGenome genome, int boneIndex, float axial)
        {
            float here = genome.GetVertebra(boneIndex).Radius;

            if (axial > Epsilon && boneIndex + 1 < genome.VertebraCount)
            {
                float span = genome.GetVertebra(boneIndex + 1).LocalOffset.magnitude;
                if (span <= Epsilon) return here;

                return Mathf.Lerp(here, genome.GetVertebra(boneIndex + 1).Radius, Smoothstep(Mathf.Clamp01(axial / span)));
            }

            if (axial < -Epsilon && boneIndex > 0)
            {
                float span = genome.GetVertebra(boneIndex).LocalOffset.magnitude;
                if (span <= Epsilon) return here;

                return Mathf.Lerp(here, genome.GetVertebra(boneIndex - 1).Radius, Smoothstep(Mathf.Clamp01(-axial / span)));
            }

            return here;
        }

        private static float Smoothstep(float t) => t * t * (3f - 2f * t);
    }
}
