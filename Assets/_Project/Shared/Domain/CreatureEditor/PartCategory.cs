using System;

namespace Leeway.Creature.Domain
{
    /// <summary>
    /// Category of a body part. Drives the tab grouping in the editor palette and
    /// feeds into the stats derived from the genome.
    /// </summary>
    /// <remarks>
    /// The values travel over the network inside part rules, so the numbers are part of
    /// the contract — only ever append new categories at the end.
    /// </remarks>
    public enum PartCategory
    {
        Locomotion = 0,
        Mouth = 1,
        Sense = 2,

        /// <summary>Graspers and arms — whatever the creature manipulates the world with.</summary>
        Grasper = 3,

        /// <summary>Weapons and armour: horns, spikes, plates.</summary>
        Weapon = 4,

        /// <summary>Ornaments with no combat effect — crests, fins, dorsal spikes.</summary>
        Detail = 5,

        /// <summary>
        /// What a limb ends in: a hoof, a paw, a hand. Never attached to the body itself — it is
        /// fitted into a leg or an arm, which is why it has a category of its own.
        /// </summary>
        Extremity = 6,
    }

    /// <summary>
    /// Where along the spine a part may be attached. Derived from the bone index:
    /// 0 → <see cref="Head"/>, last → <see cref="Tail"/>, everything else → <see cref="Torso"/>.
    /// </summary>
    [Flags]
    public enum AttachmentSite
    {
        None = 0,
        Head = 1 << 0,
        Torso = 1 << 1,
        Tail = 1 << 2,
        Any = Head | Torso | Tail,
    }
}
