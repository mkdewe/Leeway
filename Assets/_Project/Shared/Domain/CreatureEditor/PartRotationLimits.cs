using UnityEngine;

namespace Leeway.Creature.Domain
{
    /// <summary>
    /// How far a part may be turned away from the direction its own build gives it.
    /// </summary>
    /// <remarks>
    /// <para><b>Why a limb is not free to rotate.</b> Most parts are aimed: a horn points where it is
    /// turned. A leg is not — it is the top of a chain that reaches for the ground, and its direction
    /// comes from the placement, not from the player's wrist. Turning one far enough sent the foot
    /// <b>above</b> the hip and the whole limb folded back into the torso, where it looked like the
    /// model had sunk into the body. The solver then spent every frame reaching for ground that was
    /// behind the creature.</para>
    ///
    /// <para>Which is a case for a limit, not for taking the gizmo away: splaying legs out to the
    /// sides or angling them forward is exactly the kind of thing a creature editor is for. So the
    /// <b>twist</b> — rotation about the limb's own axis, which is how a knee is aimed — stays free,
    /// and the <b>swing</b> away from that axis is capped at an angle that keeps a leg a leg.</para>
    /// </remarks>
    public static class PartRotationLimits
    {
        /// <summary>How far a limb may swing away from the direction its placement gave it.</summary>
        public const float LimbSwingDegrees = 40f;

        /// <summary>The limit that applies to a part of this category, or 180° when it is free to turn.</summary>
        public static float SwingLimitFor(PartCategory category)
            => category == PartCategory.Locomotion ? LimbSwingDegrees : 180f;

        /// <summary>
        /// Trims a rotation so it swings no further than <paramref name="maxSwingDegrees"/> off the
        /// part's axis, leaving the twist about that axis untouched.
        /// </summary>
        public static Quaternion ClampSwing(Quaternion rotation, float maxSwingDegrees)
        {
            if (maxSwingDegrees >= 180f) return rotation;

            rotation = rotation.normalized;

            // Swing-twist decomposition about +Z, the axis every part is built along.
            var rotationAxis = new Vector3(rotation.x, rotation.y, rotation.z);
            Vector3 alongAxis = Vector3.Project(rotationAxis, Vector3.forward);

            var twist = new Quaternion(alongAxis.x, alongAxis.y, alongAxis.z, rotation.w);
            if (twist.x * twist.x + twist.y * twist.y + twist.z * twist.z + twist.w * twist.w < 1e-8f)
                twist = Quaternion.identity;

            twist = twist.normalized;

            Quaternion swing = rotation * Quaternion.Inverse(twist);
            swing.ToAngleAxis(out float angle, out Vector3 axis);

            // ToAngleAxis reports 0..360; past a half turn the same rotation is a smaller one the
            // other way, and clamping the larger number would flip the part instead of holding it.
            if (angle > 180f)
            {
                angle = 360f - angle;
                axis = -axis;
            }

            if (angle <= maxSwingDegrees || float.IsNaN(axis.x)) return rotation;

            return Quaternion.AngleAxis(maxSwingDegrees, axis) * twist;
        }
    }
}
