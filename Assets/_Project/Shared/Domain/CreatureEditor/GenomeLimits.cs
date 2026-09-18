using UnityEngine;

namespace Leeway.Creature.Domain
{
    /// <summary>
    /// Hard genome bounds. Enforced both by the edit operations (client) and by server-side
    /// validation — the server never trusts the client's limits.
    /// </summary>
    public static class GenomeLimits
    {
        public const int MinVertebrae = 2;

        /// <summary>
        /// The upper count bounds are technical, not design decisions: the count in the blob
        /// is a single byte, and the blob length has to fit inside <see cref="MaxBlobBytes"/>.
        /// What actually constrains the player is the budget (<see cref="CreatureBudget"/>).
        /// </summary>
        public const int MaxVertebrae = 64;

        public const int MaxParts = 64;

        public const float MinRadius = 0.08f;
        public const float MaxRadius = 0.8f;

        /// <summary>
        /// The shortest spine segment. Without a lower bound, neighbouring vertebrae could be
        /// squeezed into a single point — the tube then lost its direction, and the normals of
        /// parts attached there stopped making sense.
        /// </summary>
        public const float MinSegmentLength = 0.12f;

        /// <summary>The longest spine segment (distance between neighbouring vertebrae).</summary>
        public const float MaxSegmentLength = 0.9f;

        public const float MinPartScale = 0.25f;
        public const float MaxPartScale = 3f;

        /// <summary>How far a part may be dragged away from its own bone.</summary>
        public const float MaxPartOffset = 1.5f;

        /// <summary>Upper bound on the encoded genome size. Checked before allocating when reading from the network.</summary>
        public const int MaxBlobBytes = 4096;

        /// <summary>
        /// Default spine step. Vertebra 0 is the head at the armature root, and subsequent
        /// vertebrae run backwards along -Z, so the creature's <c>transform.forward</c> lines up
        /// with Unity's convention (+Z is forward).
        /// </summary>
        public static readonly Vector3 DefaultSegmentOffset = new Vector3(0f, 0f, -0.35f);

        public const float DefaultHeadRadius = 0.30f;

        /// <summary>How much the radius narrows when a vertebra is appended at the tail.</summary>
        public const float TailTaper = 0.85f;
    }
}
