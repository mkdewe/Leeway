using UnityEngine;

namespace Leeway.Creature.Domain
{
    /// <summary>
    /// The step parameters of one leg. Derived from <see cref="LegSpec"/>, so the number of bends
    /// genuinely changes how the creature walks, not just how it looks.
    /// </summary>
    public readonly struct GaitProfile
    {
        /// <summary>How far the foot reaches in a single step.</summary>
        public readonly float StepLength;

        /// <summary>How high the foot lifts during the swing phase.</summary>
        public readonly float StepHeight;

        /// <summary>The fraction of the cycle the foot spends on the ground. The rest is swing.</summary>
        public readonly float DutyFactor;

        /// <summary>
        /// How many steps per second this gait wants to take at any speed.
        /// </summary>
        /// <remarks>
        /// This is the quantity that turns speed into step <b>length</b> rather than step frequency.
        /// An animal moving faster lengthens its stride, it does not shuffle its legs ever faster on
        /// the spot — without this, a gait computed from a fixed step length winds up to dozens of
        /// cycles per second and all you see is a vibration.
        /// </remarks>
        public readonly float TargetCadence;

        public readonly GaitStyle Style;

        public GaitProfile(float stepLength, float stepHeight, float dutyFactor, float targetCadence, GaitStyle style)
        {
            StepLength = stepLength;
            StepHeight = stepHeight;
            DutyFactor = Mathf.Clamp(dutyFactor, 0.1f, 0.95f);
            TargetCadence = Mathf.Max(0.1f, targetCadence);
            Style = style;
        }

        /// <summary>
        /// Matches the step parameters to the leg build.
        /// </summary>
        /// <remarks>
        /// The leg's reach sets the scale: a longer chain takes longer steps and lifts the foot
        /// higher. The style changes the character — an almost rigid leg has to lift its foot high
        /// just to clear the ground, and spends less time in support, which makes the gait bouncy.
        /// A many-linked leg skims low and keeps the foot down longer, giving smooth, insect-like motion.
        /// </remarks>
        public static GaitProfile For(in LegSpec spec)
        {
            float reach = spec.Reach;

            return spec.Style switch
            {
                GaitStyle.Stiff => new GaitProfile(reach * 0.45f, reach * 0.34f, 0.45f, 2.6f, GaitStyle.Stiff),
                GaitStyle.Walk => new GaitProfile(reach * 0.60f, reach * 0.20f, 0.60f, 2.0f, GaitStyle.Walk),
                _ => new GaitProfile(reach * 0.75f, reach * 0.11f, 0.72f, 3.2f, GaitStyle.Insectoid),
            };
        }

        /// <summary>Whether the foot is on the ground at this phase.</summary>
        public bool IsPlanted(float phase) => Wrap(phase) < DutyFactor;

        /// <summary>
        /// The foot's place in the cycle, expressed as an offset along the direction of travel and a
        /// height above the ground. The <c>x</c> value runs from <c>+StepLength/2</c> (foot forward,
        /// start of support) to <c>-StepLength/2</c> (foot back, end of support), then arcs forward again.
        /// </summary>
        public Vector2 FootOffset(float phase) => FootOffset(phase, StepLength);

        /// <summary>
        /// The same, but for a step <b>matched to the current speed</b>. <see cref="StepLength"/> is
        /// only the resting length the gait starts from — the gait layer stretches the stride to hold
        /// <see cref="TargetCadence"/>, up to the limit of the leg's reach.
        /// </summary>
        public Vector2 FootOffset(float phase, float stepLength)
        {
            float t = Wrap(phase);
            float half = stepLength * 0.5f;

            if (t < DutyFactor)
            {
                // Support: the foot stands still, so relative to the body it travels backwards linearly.
                float k = t / DutyFactor;
                return new Vector2(Mathf.Lerp(half, -half, k), 0f);
            }

            // Swing: the foot arcs forward again. A sine gives exactly zero height at both ends, so the
            // foot does not drive into the ground on touchdown.
            float s = (t - DutyFactor) / (1f - DutyFactor);
            return new Vector2(Mathf.Lerp(-half, half, s), Mathf.Sin(s * Mathf.PI) * StepHeight);
        }

        /// <summary>
        /// The phase offset for the leg at a given index. Left/right pairs run in antiphase, and
        /// successive pairs are spread out so a many-legged creature does not plant everything at once.
        /// </summary>
        public static float PhaseOffset(int legIndex, int legCount)
        {
            if (legCount <= 1) return 0f;

            int pairIndex = legIndex / 2;
            int pairCount = Mathf.Max(1, (legCount + 1) / 2);

            float sideOffset = (legIndex % 2 == 0) ? 0f : 0.5f;
            float pairOffset = pairIndex / (float)pairCount * 0.5f;

            return Wrap(sideOffset + pairOffset);
        }

        private static float Wrap(float phase)
        {
            phase %= 1f;
            return phase < 0f ? phase + 1f : phase;
        }
    }

    /// <summary>
    /// Turning the creature towards the direction being held.
    /// </summary>
    /// <remarks>
    /// <para>The creature does not snap onto a new heading and does not slide sideways — it
    /// <b>turns</b> towards the direction the player is holding and only sets off once it is roughly
    /// facing it. Hence the characteristic turn on the spot when reversing: the front is still facing
    /// away, so the throttle sits near zero.</para>
    ///
    /// <para>Pure maths, no <c>Transform</c>s — the gait runs inside the replication loop, so it has
    /// to produce an identical result on every reconciliation replay.</para>
    /// </remarks>
    public static class LocomotionSteering
    {
        /// <summary>Below this magnitude we treat the input as empty.</summary>
        private const float InputEpsilon = 0.01f;

        /// <summary>The angle in degrees described by a direction on the XZ plane.</summary>
        public static float YawOf(Vector3 direction) => Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;

        /// <summary>
        /// Pulls the current heading towards the desired one, no faster than the turn rate allows.
        /// Empty input leaves the heading alone — the creature does not straighten out by itself.
        /// </summary>
        public static float StepYaw(float currentYaw, Vector3 desiredDirection, float turnSpeedDegrees, float deltaTime)
        {
            if (desiredDirection.sqrMagnitude < InputEpsilon * InputEpsilon) return currentYaw;

            return Mathf.MoveTowardsAngle(currentYaw, YawOf(desiredDirection), turnSpeedDegrees * deltaTime);
        }

        /// <summary>
        /// How much of the top speed may be used right now.
        /// </summary>
        /// <remarks>
        /// The dot product of the facing and the input direction: reversing gives zero, so the
        /// creature first turns on the spot and only accelerates once it is looking where it means to
        /// go. Without this, turning around would look like sliding sideways across the floor.
        /// </remarks>
        public static float Throttle(float currentYaw, Vector3 desiredDirection)
        {
            float magnitude = desiredDirection.magnitude;
            if (magnitude < InputEpsilon) return 0f;

            Vector3 facing = Quaternion.Euler(0f, currentYaw, 0f) * Vector3.forward;
            float alignment = Vector3.Dot(facing, desiredDirection / magnitude);

            return Mathf.Clamp01(alignment) * Mathf.Clamp01(magnitude);
        }

        /// <summary>
        /// The direction of travel from the player's input, rotated into camera space.
        /// </summary>
        /// <remarks>
        /// The input is relative to the camera, because that is how the player reads it: "up the
        /// screen" means "away from me". The creature then turns towards that direction, so its own
        /// front still determines where it actually goes.
        /// </remarks>
        public static Vector3 DesiredDirection(float forward, float strafe, float cameraYaw)
        {
            var raw = new Vector3(strafe, 0f, forward);
            if (raw.sqrMagnitude > 1f) raw.Normalize();

            return Quaternion.Euler(0f, cameraYaw, 0f) * raw;
        }
    }
}
