using UnityEngine;

namespace Leeway.Creature.Domain
{
    /// <summary>
    /// A part attached to a specific vertebra. <see cref="PartId"/> is the stable FNV-1a hash
    /// of the catalog key — never an asset reference, because the genome travels over the
    /// network and has to resolve against the receiver's own catalog.
    /// </summary>
    public readonly struct PartGene
    {
        public readonly int PartId;
        public readonly byte BoneIndex;
        public readonly Vector3 LocalPosition;
        public readonly Quaternion LocalRotation;
        public readonly float Scale;

        /// <summary>Whether the part should also be instantiated mirrored across the YZ plane.</summary>
        public readonly bool Mirrored;

        /// <summary>
        /// Whether the player reshaped this leg in the editor. Without it <see cref="Leg"/> means
        /// nothing and the catalog's build applies.
        /// </summary>
        /// <remarks>
        /// A flag rather than a sentinel value: zero bend points is a legal leg, so "nothing was
        /// recorded" cannot be told apart from "the leg is stiff" by the number alone.
        /// </remarks>
        public readonly bool HasLegOverride;

        /// <summary>
        /// The leg build recorded by the player. Meaningful only when <see cref="HasLegOverride"/>
        /// is set, and only for locomotion parts.
        /// </summary>
        public readonly LegSpec Leg;

        public PartGene(int partId, byte boneIndex, Vector3 localPosition, Quaternion localRotation, float scale, bool mirrored)
            : this(partId, boneIndex, localPosition, localRotation, scale, mirrored, false, default)
        {
        }

        public PartGene(int partId, byte boneIndex, Vector3 localPosition, Quaternion localRotation, float scale,
            bool mirrored, bool hasLegOverride, LegSpec leg)
        {
            PartId = partId;
            BoneIndex = boneIndex;
            LocalPosition = localPosition;
            LocalRotation = localRotation;
            Scale = scale;
            Mirrored = mirrored;
            HasLegOverride = hasLegOverride;
            Leg = leg;
        }

        /// <summary>A part attached at its bone's default spot — the starting point for the editor palette.</summary>
        public static PartGene Default(int partId, byte boneIndex, bool mirrored)
            => new PartGene(partId, boneIndex, Vector3.zero, Quaternion.identity, 1f, mirrored);

        public PartGene WithBoneIndex(byte boneIndex) => new PartGene(PartId, boneIndex, LocalPosition, LocalRotation, Scale, Mirrored, HasLegOverride, Leg);
        public PartGene WithLocalPosition(Vector3 localPosition) => new PartGene(PartId, BoneIndex, localPosition, LocalRotation, Scale, Mirrored, HasLegOverride, Leg);
        public PartGene WithLocalRotation(Quaternion localRotation) => new PartGene(PartId, BoneIndex, LocalPosition, localRotation, Scale, Mirrored, HasLegOverride, Leg);
        public PartGene WithScale(float scale) => new PartGene(PartId, BoneIndex, LocalPosition, LocalRotation, scale, Mirrored, HasLegOverride, Leg);
        public PartGene WithMirrored(bool mirrored) => new PartGene(PartId, BoneIndex, LocalPosition, LocalRotation, Scale, mirrored, HasLegOverride, Leg);

        /// <summary>Records a bespoke leg build in this gene.</summary>
        public PartGene WithLeg(LegSpec leg) => new PartGene(PartId, BoneIndex, LocalPosition, LocalRotation, Scale, Mirrored, true, leg);

        /// <summary>Drops the reshaping — the leg reverts to the catalog build.</summary>
        public PartGene WithoutLegOverride() => new PartGene(PartId, BoneIndex, LocalPosition, LocalRotation, Scale, Mirrored, false, default);

        /// <summary>
        /// The leg build that actually applies: the player's record, or the catalog when there is none.
        /// </summary>
        /// <remarks>
        /// The only place this precedence is decided. The gait, the stats, the stance height and the
        /// chain builder all have to read exactly the same leg, otherwise the creature would compute
        /// its speed from one and take its steps with another.
        /// </remarks>
        public LegSpec EffectiveLeg(in LegSpec catalogDefault) => HasLegOverride ? Leg : catalogDefault;
    }
}
