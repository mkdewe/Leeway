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
    ///
    /// <para><b>Links do not have to be equal.</b> A real leg is a long thigh and a shorter shank, and
    /// when the visible model is one authored limb the joint has to land where <b>that model</b> bends
    /// — anywhere else and the mesh creases in the wrong place. Hence the overload taking a length per
    /// link; the uniform one is the same solver with every length the same.</para>
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
            => Solve(joints, null, segmentLength, target, bendHint, iterations);

        /// <summary>
        /// The same solver with a length per link — <paramref name="lengths"/>[i] is the distance
        /// between joint i and joint i+1.
        /// </summary>
        /// <remarks>
        /// Used by legs whose visible model is a single authored limb: the knee joint is placed where
        /// the model itself bends, so the thigh and the shank come out the length the artist drew them.
        /// </remarks>
        public static void Solve(Vector3[] joints, float[] lengths, Vector3 target, Vector3 bendHint,
            int iterations = DefaultIterations)
            => Solve(joints, lengths, 0f, target, bendHint, iterations);

        private static void Solve(Vector3[] joints, float[] lengths, float uniformLength, Vector3 target,
            Vector3 bendHint, int iterations)
        {
            if (joints == null || joints.Length < 2) return;
            if (lengths != null && lengths.Length < joints.Length - 1) return;

            Vector3 root = joints[0];
            int segments = joints.Length - 1;

            float reach = 0f;
            for (int i = 0; i < segments; i++) reach += LengthAt(lengths, uniformLength, i);

            // Target out of reach: there is nothing to iterate, the chain simply straightens out.
            Vector3 toTarget = target - root;
            if (toTarget.sqrMagnitude >= reach * reach)
            {
                Vector3 direction = toTarget.sqrMagnitude > Epsilon ? toTarget.normalized : Vector3.down;

                float travelled = 0f;
                for (int i = 1; i < joints.Length; i++)
                {
                    travelled += LengthAt(lengths, uniformLength, i - 1);
                    joints[i] = root + direction * travelled;
                }

                return;
            }

            SeedBend(joints, lengths, uniformLength, root, target, bendHint, reach);

            for (int iteration = 0; iteration < iterations; iteration++)
            {
                // Backwards: the foot lands on the target and the rest is dragged after it.
                joints[^1] = target;
                for (int i = joints.Length - 2; i >= 0; i--)
                    joints[i] = MoveTowards(joints[i + 1], joints[i], LengthAt(lengths, uniformLength, i));

                // Forwards: the hip returns to its place and the rest is dragged after it.
                joints[0] = root;
                for (int i = 1; i < joints.Length; i++)
                    joints[i] = MoveTowards(joints[i - 1], joints[i], LengthAt(lengths, uniformLength, i - 1));
            }
        }

        private static float LengthAt(float[] lengths, float uniformLength, int index)
            => lengths != null ? lengths[index] : uniformLength;

        /// <summary>
        /// Lays the joints out along an arc bowed towards the hint before FABRIK starts. This is the
        /// only thing that decides which way the knee will bend.
        /// </summary>
        private static void SeedBend(Vector3[] joints, float[] lengths, float uniformLength, Vector3 root,
            Vector3 target, Vector3 bendHint, float reach)
        {
            Vector3 axis = target - root;
            float distance = axis.magnitude;
            if (distance < Epsilon) return;

            Vector3 forward = axis / distance;

            // The hint perpendicular to the chain axis — the component along the axis adds nothing.
            Vector3 side = bendHint - Vector3.Dot(bendHint, forward) * forward;
            if (side.sqrMagnitude < Epsilon) return;

            side.Normalize();

            float slack = Mathf.Max(0f, reach - distance);
            float bulge = slack * 0.5f;

            // The seed follows the chain's own proportions: with unequal links the knee sits where its
            // own link ends, not halfway along, or FABRIK starts from a pose that has to be undone.
            float travelled = 0f;
            for (int i = 1; i < joints.Length - 1; i++)
            {
                travelled += LengthAt(lengths, uniformLength, i - 1);
                float t = reach > Epsilon ? travelled / reach : i / (float)(joints.Length - 1);

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
