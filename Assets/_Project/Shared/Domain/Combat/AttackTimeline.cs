using UnityEngine;

namespace Leeway.Creature.Domain
{
    /// <summary>The stretch of an attack animation a moment of time falls in.</summary>
    public enum AttackPhase : byte { Windup, Active, Recover, Done }

    /// <summary>
    /// When the parts of an attack happen, for every kind of attack.
    /// </summary>
    /// <remarks>
    /// <para>An attack is an animation, and the animation is marked: <b>Windup</b> is the body drawing back,
    /// <b>Active</b> is the stretch of frames between the two events - <i>hit window opens</i> and <i>hit
    /// window closes</i> - and <b>Recover</b> is the body coming back to rest. A blow hurts only what is
    /// inside its area while the window is open. The window is as short as the move: a snap of the jaw
    /// is a tenth of a second, a thrust of the horn a little more.</para>
    ///
    /// <para>The numbers live here, in plain maths, so the server and every client read the same
    /// timeline from the one synced start time and agree on which frame it is.</para>
    /// </remarks>
    public readonly struct AttackTimeline
    {
        public readonly float Windup;
        public readonly float Active;
        public readonly float Recover;

        public AttackTimeline(float windup, float active, float recover)
        {
            Windup = windup;
            Active = active;
            Recover = recover;
        }

        public float Total => Windup + Active + Recover;

        /// <summary>The phase at a time counted from the start of the attack.</summary>
        public AttackPhase PhaseAt(float seconds)
        {
            if (seconds < Windup) return AttackPhase.Windup;
            if (seconds < Windup + Active) return AttackPhase.Active;
            return seconds < Total ? AttackPhase.Recover : AttackPhase.Done;
        }

        public static AttackTimeline For(CreatureAbility ability) => ability switch
        {
            CreatureAbility.Bite => new AttackTimeline(0.22f, 0.12f, 0.35f),
            CreatureAbility.Claw => new AttackTimeline(0.30f, 0.16f, 0.40f),
            CreatureAbility.Kick => new AttackTimeline(0.28f, 0.14f, 0.45f),
            CreatureAbility.Horn => new AttackTimeline(0.55f, 0.18f, 0.50f),
            _ => new AttackTimeline(0.18f, 0.14f, 0.30f),
        };

        /// <summary>
        /// How far (metres) the striking part is carried forward at the peak of the blow - the head
        /// stretching out for a bite, the creature throwing its weight behind a horn. Part of the move,
        /// and of how far the blow reaches.
        /// </summary>
        public static float Lunge(CreatureAbility ability) => ability switch
        {
            CreatureAbility.Bite => 0.32f,
            CreatureAbility.Horn => 0.40f,
            CreatureAbility.Kick => 0.10f,
            _ => 0.12f,
        };

        /// <summary>The radius (metres) of the area a striking part covers while the hit window is open.</summary>
        public static float AreaRadius(CreatureAbility ability) => ability switch
        {
            CreatureAbility.Bite => 0.30f,
            CreatureAbility.Horn => 0.28f,
            CreatureAbility.Kick => 0.26f,
            _ => 0.26f,
        };
    }
}
