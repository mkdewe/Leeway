using UnityEngine;

namespace Leeway.Creature.Domain
{
    /// <summary>
    /// The body's suspension on its legs: a damped spring holding the torso at a given
    /// height above the ground.
    /// </summary>
    /// <remarks>
    /// <para>Replaces holding the body rigidly on an offset capsule. A rigid shape does not
    /// respond to terrain: it does not compress on landing, does not rise over a step, and
    /// does not shift weight onto the supporting leg. The spring gives all of that for one
    /// force per physics frame.</para>
    ///
    /// <para><b>Critical damping as the reference point.</b> At <c>damping = 2·sqrt(stiffness)</c>
    /// the body returns to its rest height as fast as it can without oscillating. Below that the
    /// creature would bob along on springs; above it, it would settle sluggishly. That is why the
    /// parameters are given as a stiffness plus a <see cref="DampingRatio"/> relative to critical,
    /// rather than as two independent numbers whose relationship nobody remembers.</para>
    /// </remarks>
    public readonly struct Suspension
    {
        /// <summary>Spring stiffness, in units of acceleration per metre of compression.</summary>
        public readonly float Stiffness;

        /// <summary>Damping relative to critical. 1 = critical, below it bobbing, above it sluggishness.</summary>
        public readonly float DampingRatio;

        /// <summary>How far below the rest height the spring still reaches for the ground.</summary>
        public readonly float MaxDroop;

        public Suspension(float stiffness, float dampingRatio, float maxDroop)
        {
            Stiffness = Mathf.Max(0f, stiffness);
            DampingRatio = Mathf.Max(0f, dampingRatio);
            MaxDroop = Mathf.Max(0f, maxDroop);
        }

        public static Suspension Default => new Suspension(70f, 1f, 0.45f);

        /// <summary>The damping coefficient derived from the stiffness and the ratio to critical.</summary>
        public float Damping => 2f * Mathf.Sqrt(Stiffness) * DampingRatio;

        /// <summary>
        /// The acceleration with which the spring lifts the body.
        /// </summary>
        /// <param name="rideHeight">The rest height at which the legs hold the torso.</param>
        /// <param name="groundDistance">The measured distance to the ground.</param>
        /// <param name="verticalVelocity">The body's current vertical velocity.</param>
        /// <param name="gravity">
        /// The gravitational acceleration to compensate for. The legs hold the body up
        /// <b>actively</b>, so at zero compression they give back exactly what gravity takes —
        /// otherwise the creature would settle onto the spring like a car on its suspension and
        /// stand lower the heavier it got.
        /// </param>
        /// <returns>
        /// Upward acceleration. Zero when the body hangs more than <see cref="MaxDroop"/> above
        /// the rest height — it is airborne then, and gravity is in charge.
        /// </returns>
        public float Acceleration(float rideHeight, float groundDistance, float verticalVelocity, float gravity = 0f)
        {
            float compression = rideHeight - groundDistance;

            // Too low for a leg to reach the ground: the creature is falling, not standing.
            if (compression < -MaxDroop) return 0f;

            float spring = compression * Stiffness;
            float damper = verticalVelocity * Damping;

            // The suspension only ever props up. Pulling the body down on rebound would glue the
            // creature to the ground and cancel every jump.
            return Mathf.Max(0f, spring - damper + gravity);
        }

        /// <summary>Whether, at this distance to the ground, the legs reach the surface at all.</summary>
        public bool IsGrounded(float rideHeight, float groundDistance) => groundDistance <= rideHeight + MaxDroop;
    }
}
