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

        /// <summary>
        /// The colour the player painted this part.
        /// </summary>
        /// <remarks>
        /// <b>Alpha 0 means "not painted"</b>, and the part then wears the creature's
        /// <see cref="CreatureGenome.SecondaryColor"/>. A flag would be tidier, but this way every
        /// gene built before painting existed — and every preset written without the field — comes out
        /// unpainted instead of black, which is what "no colour recorded" has to mean.
        /// </remarks>
        public readonly Color32 Tint;

        /// <summary>
        /// The coat pattern, as an index into the skin palette. <c>0</c> is bare skin.
        /// </summary>
        /// <remarks>
        /// An index rather than a texture reference, for the same reason <see cref="PartId"/> is a
        /// hash: the genome travels over the network and has to mean the same thing on a machine whose
        /// assets are its own. An index nobody recognises falls back to bare skin — a pattern is
        /// decoration, and decoration must never fail a creature to load.
        /// </remarks>
        public readonly byte PatternId;

        /// <summary>
        /// The part fitted into this one's socket — the foot on a leg, the hand on an arm.
        /// </summary>
        /// <remarks>
        /// <para><b>Why a field on the gene and not an attachment of its own.</b> A foot is not
        /// attached to the body: it is attached to a leg, travels with it, is mirrored with it and is
        /// thrown away with it. Recording it as a second part with a pointer would mean an attachment
        /// graph — ordering, orphans, cycles — for a relationship that is always exactly one level
        /// deep. One id on the host gene says the same thing and cannot come apart.</para>
        ///
        /// <para>Zero means the limb ends in whatever the model ends in: a stump.</para>
        /// </remarks>
        public readonly int FittingId;

        public PartGene(int partId, byte boneIndex, Vector3 localPosition, Quaternion localRotation, float scale, bool mirrored)
            : this(partId, boneIndex, localPosition, localRotation, scale, mirrored, false, default)
        {
        }

        public PartGene(int partId, byte boneIndex, Vector3 localPosition, Quaternion localRotation, float scale,
            bool mirrored, bool hasLegOverride, LegSpec leg)
            : this(partId, boneIndex, localPosition, localRotation, scale, mirrored, hasLegOverride, leg,
                default, 0)
        {
        }

        public PartGene(int partId, byte boneIndex, Vector3 localPosition, Quaternion localRotation, float scale,
            bool mirrored, bool hasLegOverride, LegSpec leg, Color32 tint, byte patternId, int fittingId = 0)
        {
            PartId = partId;
            BoneIndex = boneIndex;
            LocalPosition = localPosition;
            LocalRotation = localRotation;
            Scale = scale;
            Mirrored = mirrored;
            HasLegOverride = hasLegOverride;
            Leg = leg;
            Tint = tint;
            PatternId = patternId;
            FittingId = fittingId;
        }

        /// <summary>Whether anything is fitted into this part's socket.</summary>
        public bool HasFitting => FittingId != 0;

        /// <summary>Whether the player gave this part a colour of its own.</summary>
        public bool HasTint => Tint.a > 0;

        /// <summary>The colour to paint the part in: the player's, or the creature's when it was never painted.</summary>
        public Color32 ResolveTint(Color32 fallback) => HasTint ? Tint : fallback;

        /// <summary>A part attached at its bone's default spot — the starting point for the editor palette.</summary>
        public static PartGene Default(int partId, byte boneIndex, bool mirrored)
            => new PartGene(partId, boneIndex, Vector3.zero, Quaternion.identity, 1f, mirrored);

        public PartGene WithBoneIndex(byte boneIndex) => new PartGene(PartId, boneIndex, LocalPosition, LocalRotation, Scale, Mirrored, HasLegOverride, Leg, Tint, PatternId, FittingId);
        public PartGene WithLocalPosition(Vector3 localPosition) => new PartGene(PartId, BoneIndex, localPosition, LocalRotation, Scale, Mirrored, HasLegOverride, Leg, Tint, PatternId, FittingId);
        public PartGene WithLocalRotation(Quaternion localRotation) => new PartGene(PartId, BoneIndex, LocalPosition, localRotation, Scale, Mirrored, HasLegOverride, Leg, Tint, PatternId, FittingId);
        public PartGene WithScale(float scale) => new PartGene(PartId, BoneIndex, LocalPosition, LocalRotation, scale, Mirrored, HasLegOverride, Leg, Tint, PatternId, FittingId);

        /// <summary>Fits a part into this one's socket — a foot into a leg. <c>0</c> takes it out again.</summary>
        public PartGene WithFitting(int fittingId) => new PartGene(PartId, BoneIndex, LocalPosition, LocalRotation, Scale, Mirrored, HasLegOverride, Leg, Tint, PatternId, fittingId);
        public PartGene WithMirrored(bool mirrored) => new PartGene(PartId, BoneIndex, LocalPosition, LocalRotation, Scale, mirrored, HasLegOverride, Leg, Tint, PatternId, FittingId);

        /// <summary>Records a bespoke leg build in this gene.</summary>
        public PartGene WithLeg(LegSpec leg) => new PartGene(PartId, BoneIndex, LocalPosition, LocalRotation, Scale, Mirrored, true, leg, Tint, PatternId, FittingId);

        /// <summary>Drops the reshaping — the leg reverts to the catalog build.</summary>
        public PartGene WithoutLegOverride() => new PartGene(PartId, BoneIndex, LocalPosition, LocalRotation, Scale, Mirrored, false, default, Tint, PatternId, FittingId);

        /// <summary>Paints the part. An alpha of 0 unpaints it — back to the creature's own colour.</summary>
        public PartGene WithTint(Color32 tint) => new PartGene(PartId, BoneIndex, LocalPosition, LocalRotation, Scale, Mirrored, HasLegOverride, Leg, tint, PatternId, FittingId);

        /// <summary>Puts a coat pattern on the part. <c>0</c> is bare skin.</summary>
        public PartGene WithPattern(byte patternId) => new PartGene(PartId, BoneIndex, LocalPosition, LocalRotation, Scale, Mirrored, HasLegOverride, Leg, Tint, patternId, FittingId);

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
