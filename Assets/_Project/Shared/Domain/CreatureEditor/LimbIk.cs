using UnityEngine;

namespace Leeway.Creature.Domain
{
    /// <summary>
    /// An IK solver for a chain with any number of joints (FABRIK).
    /// </summary>
    /// <remarks>
    /// <para>FABRIK rather than a closed-form solution, because the number of bends comes from the
    /// catalog and varies from leg to leg — a two-link formula could not handle a tentacle with four
    /// bends. The method is iterative, but it converges in a handful of passes and has no
    /// singularity when the chain is straight.</para>
    ///
    /// <para>The solver works on <b>positions</b>, not rotations — turning positions into bone
    /// rotations is the runtime layer's job. That keeps all the maths pure and pinnable by a test
    /// without a scene.</para>
    ///
    /// <para><b>The bend direction</b> does not follow from FABRIK itself: the chain bends in
    /// whatever plane it happens to lie in. So before solving we splay the joints towards
    /// <c>bendHint</c> — without that the knee can bend sideways or backwards, depending on where
    /// the chain started from.</para>
    /// </remarks>
    public static class LimbIk
    {
        private const float Epsilon = 1e-6f;

        /// <summary>How many back-and-forth passes. Four is enough for chains of this length.</summary>
        public const int DefaultIterations = 4;

        /// <summary>
        /// Places the joints so the last one lands as close to the target as it can.
        /// </summary>
        /// <param name="joints">
        /// Joints from the hip (index 0, immovable) to the foot (last). Modified in place.
        /// </param>
        /// <param name="segmentLength">The length of one link — they are all equal.</param>
        /// <param name="target">The target foot position in world space.</param>
        /// <param name="bendHint">The direction the knees should push towards.</param>
        public static void Solve(Vector3[] joints, float segmentLength, Vector3 target, Vector3 bendHint,
            int iterations = DefaultIterations)
        {
            if (joints == null || joints.Length < 2) return;

            Vector3 root = joints[0];
            int segments = joints.Length - 1;
            float reach = segments * segmentLength;

            // Target out of reach: there is nothing to iterate, the chain simply straightens out.
            Vector3 toTarget = target - root;
            if (toTarget.sqrMagnitude >= reach * reach)
            {
                Vector3 direction = toTarget.sqrMagnitude > Epsilon ? toTarget.normalized : Vector3.down;
                for (int i = 1; i < joints.Length; i++)
                    joints[i] = root + direction * (segmentLength * i);

                return;
            }

            SeedBend(joints, root, target, bendHint, segmentLength);

            for (int iteration = 0; iteration < iterations; iteration++)
            {
                // Backwards: the foot lands on the target and the rest is dragged after it.
                joints[^1] = target;
                for (int i = joints.Length - 2; i >= 0; i--)
                    joints[i] = MoveTowards(joints[i + 1], joints[i], segmentLength);

                // Forwards: the hip returns to its place and the rest is dragged after it.
                joints[0] = root;
                for (int i = 1; i < joints.Length; i++)
                    joints[i] = MoveTowards(joints[i - 1], joints[i], segmentLength);
            }
        }

        /// <summary>
        /// Lays the joints out along an arc bowed towards the hint before FABRIK starts. This is the
        /// only thing that decides which way the knee will bend.
        /// </summary>
        private static void SeedBend(Vector3[] joints, Vector3 root, Vector3 target, Vector3 bendHint, float segmentLength)
        {
            Vector3 axis = target - root;
            float distance = axis.magnitude;
            if (distance < Epsilon) return;

            Vector3 forward = axis / distance;

            // The hint perpendicular to the chain axis — the component along the axis adds nothing.
            Vector3 side = bendHint - Vector3.Dot(bendHint, forward) * forward;
            if (side.sqrMagnitude < Epsilon) return;

            side.Normalize();

            int segments = joints.Length - 1;
            float slack = Mathf.Max(0f, segments * segmentLength - distance);
            float bulge = slack * 0.5f;

            for (int i = 1; i < joints.Length - 1; i++)
            {
                float t = i / (float)segments;
                joints[i] = root + forward * (distance * t) + side * (Mathf.Sin(t * Mathf.PI) * bulge);
            }
        }

        private static Vector3 MoveTowards(Vector3 from, Vector3 towards, float distance)
        {
            Vector3 direction = towards - from;
            float length = direction.magnitude;

            if (length < Epsilon) return from + Vector3.down * distance;

            return from + direction / length * distance;
        }
    }
}
