using UnityEngine;

namespace Leeway.Creature.Domain
{
    /// <summary>The gait style derived from the leg's number of bend points.</summary>
    public enum GaitStyle
    {
        /// <summary>One bend: an almost rigid leg, a bouncy gait, the foot lifted high.</summary>
        Stiff = 0,

        /// <summary>Two bends: an ordinary walk with a bending knee.</summary>
        Walk = 1,

        /// <summary>Three or more: smooth, insect-like motion, the foot barely leaving the ground.</summary>
        Insectoid = 2,
    }

    /// <summary>
    /// The description of one leg, derived from the part definition. Everything that decides how a
    /// leg looks and how it walks lives here and follows from a single number — the bend points.
    /// </summary>
    /// <remarks>
    /// A plain struct with no <c>Transform</c>s: the leg builder and the IK solver take their
    /// dimensions from it, and the gait layer its step parameters. That keeps the whole leg
    /// mechanic testable without a scene, like the rest of the creature domain.
    /// </remarks>
    public readonly struct LegSpec
    {
        /// <summary>How many bends the leg has. Zero means a single rigid segment from hip to foot.</summary>
        public readonly int BendPoints;

        /// <summary>The length of a single segment.</summary>
        public readonly float SegmentLength;

        public LegSpec(int bendPoints, float segmentLength)
        {
            BendPoints = Mathf.Clamp(bendPoints, LegLimits.MinBendPoints, LegLimits.MaxBendPoints);
            SegmentLength = Mathf.Clamp(segmentLength, LegLimits.MinSegmentLength, LegLimits.MaxSegmentLength);
        }

        /// <summary>The number of segments. Every bend adds one link to the chain.</summary>
        public int SegmentCount => BendPoints + 1;

        /// <summary>How many joints the chain has in total, hip and foot included.</summary>
        public int JointCount => SegmentCount + 1;

        /// <summary>How far the leg reaches when fully extended.</summary>
        public float Reach => SegmentCount * SegmentLength;

        public GaitStyle Style => BendPoints switch
        {
            <= 1 => GaitStyle.Stiff,
            2 => GaitStyle.Walk,
            _ => GaitStyle.Insectoid,
        };

        /// <summary>The step parameters for this leg.</summary>
        public GaitProfile Gait => GaitProfile.For(this);

        /// <summary>
        /// How high this leg carries the body. Legs never straighten completely — the slack is what
        /// the bends are made of, so the creature stands lower than its reach.
        /// </summary>
        public float RideHeight => Reach * StanceFactor;

        /// <summary>The fraction of the reach at which the leg holds the body at rest.</summary>
        public const float StanceFactor = 0.82f;

        /// <summary>
        /// The longest step this leg can take without lifting the foot off the ground.
        /// </summary>
        /// <remarks>
        /// A plain right triangle: the hypotenuse is the leg's reach, the vertical side is the ride
        /// height, and the horizontal one says how far forward and back the foot still reaches the
        /// ground. Doubled, it gives the full stride. This is a hard geometric limit — a longer step
        /// can only be written down, not taken: the foot would leave the ground or the leg would
        /// splay out into a straight line.
        /// </remarks>
        public float MaxStride
        {
            get
            {
                float horizontal = Reach * Reach - RideHeight * RideHeight;
                return horizontal > 0f ? 2f * Mathf.Sqrt(horizontal) : 0f;
            }
        }

        public override string ToString() => $"{BendPoints} bends, {SegmentCount} segments, reach {Reach:0.##}";
    }

    /// <summary>Leg bounds. Enforced inside <see cref="LegSpec"/>, so a leg outside the range cannot be built.</summary>
    public static class LegLimits
    {
        public const int MinBendPoints = 0;

        /// <summary>Beyond four bends the chain stops reading clearly and the IK starts curling up.</summary>
        public const int MaxBendPoints = 4;

        public const float MinSegmentLength = 0.05f;
        public const float MaxSegmentLength = 1.5f;
    }
}
