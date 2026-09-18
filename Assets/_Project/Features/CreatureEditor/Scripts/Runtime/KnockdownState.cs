using System;
using UnityEngine;

namespace Leeway.CreatureEditor
{
    /// <summary>
    /// The authoritative knockdown state. Only the server writes it; clients read it to fire off a
    /// cosmetic ragdoll locally.
    /// </summary>
    /// <remarks>
    /// <para>A knockdown has <b>two phases</b>, not one. First the creature lies down and the ragdoll
    /// is in charge (<see cref="IsSprawled"/>), then it is back on its feet but still getting up and
    /// not listening to input (<see cref="IsRising"/>). Without that second phase the creature jumped
    /// straight from a sprawl into a run — from a position it briefly held in two different places at
    /// once.</para>
    ///
    /// <para>It deliberately does <b>not</b> carry bone positions. The ragdoll is simulated locally on
    /// each player's machine and is allowed to diverge — synchronising a dozen transforms for a visual
    /// effect would be a waste of bandwidth. Only the outcome is authoritative:
    /// <see cref="Landing"/>, the place where the server put the creature back on its feet. That has
    /// to be identical everywhere, because movement prediction starts again from it.</para>
    /// </remarks>
    [Serializable]
    public struct KnockdownState : IEquatable<KnockdownState>
    {
        public bool IsDown;
        public uint StartTick;
        public Vector3 Impulse;
        public byte HitBoneIndex;

        /// <summary>Whether the creature is already back on its feet and merely finishing getting up.</summary>
        public bool IsRising;

        /// <summary>
        /// Where the server placed the creature. It only matters once <see cref="IsRising"/> is set.
        /// </summary>
        public Vector3 Landing;

        public static KnockdownState Standing => default;

        /// <summary>Lying down with the ragdoll in charge. Then and only then are the bone bodies enabled.</summary>
        public bool IsSprawled => IsDown && !IsRising;

        public bool Equals(KnockdownState other)
            => IsDown == other.IsDown
            && IsRising == other.IsRising
            && StartTick == other.StartTick
            && HitBoneIndex == other.HitBoneIndex
            && Impulse == other.Impulse
            && Landing == other.Landing;

        public override bool Equals(object obj) => obj is KnockdownState other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(IsDown, IsRising, StartTick, Impulse, Landing, HitBoneIndex);
    }
}
