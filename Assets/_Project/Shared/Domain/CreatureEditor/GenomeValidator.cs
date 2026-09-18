using UnityEngine;

namespace Leeway.Creature.Domain
{
    /// <summary>The result of validating a genome — the reason plus the index of the offending element (vertebra or part).</summary>
    public readonly struct GenomeValidationResult
    {
        public readonly bool IsValid;
        public readonly GenomeError Error;

        /// <summary>The index of the element validation tripped on, or -1 when the error is global.</summary>
        public readonly int Index;

        private GenomeValidationResult(bool isValid, GenomeError error, int index)
        {
            IsValid = isValid;
            Error = error;
            Index = index;
        }

        public static GenomeValidationResult Ok { get; } = new GenomeValidationResult(true, GenomeError.None, -1);
        public static GenomeValidationResult Fail(GenomeError error, int index = -1) => new GenomeValidationResult(false, error, index);

        public override string ToString() => IsValid ? "OK" : $"{Error} @ {Index}";
    }

    /// <summary>
    /// Full genome validation. Run twice: by the client before sending (fast feedback) and by the
    /// server on receipt — the server uses <b>its own</b> <see cref="PartRuleSet"/> and never trusts
    /// the client's catalog.
    /// </summary>
    public static class GenomeValidator
    {
        /// <summary>
        /// Derives the attachment site from the bone's position: the first is the head, the last is
        /// the tail, everything between is torso. With a single vertebra the head wins.
        /// </summary>
        /// <remarks>
        /// The end vertebrae count as torso <b>as well</b>. With two or three vertebrae the whole body
        /// is nothing but ends, so without this there would be nowhere for legs to sit — and shortening
        /// the spine invalidated them purely because their vertebra suddenly became the tail. The
        /// restrictions that actually mean something (a mouth and eyes only on the head, a fin only on
        /// the tail) are left untouched.
        /// </remarks>
        public static AttachmentSite SiteForBone(int boneIndex, int vertebraCount)
        {
            if (vertebraCount <= 0) return AttachmentSite.None;
            if (boneIndex < 0 || boneIndex >= vertebraCount) return AttachmentSite.None;
            if (boneIndex == 0) return AttachmentSite.Head | AttachmentSite.Torso;
            if (boneIndex == vertebraCount - 1) return AttachmentSite.Tail | AttachmentSite.Torso;
            return AttachmentSite.Torso;
        }

        public static GenomeValidationResult Validate(CreatureGenome genome, PartRuleSet rules)
        {
            if (genome == null) return GenomeValidationResult.Fail(GenomeError.NullGenome);
            if (rules == null) rules = PartRuleSet.Empty;

            GenomeValidationResult spine = ValidateSpine(genome);
            if (!spine.IsValid) return spine;

            GenomeValidationResult parts = ValidateParts(genome, rules);
            if (!parts.IsValid) return parts;

            // The server prices the budget with its own rates — a price sent by the client does not exist.
            if (!CreatureBudget.Evaluate(genome, rules).IsAffordable)
                return GenomeValidationResult.Fail(GenomeError.InsufficientFunds);

            if (GenomeCodec.ComputeSize(genome) > GenomeLimits.MaxBlobBytes)
                return GenomeValidationResult.Fail(GenomeError.PayloadTooLarge);

            return GenomeValidationResult.Ok;
        }

        private static GenomeValidationResult ValidateSpine(CreatureGenome genome)
        {
            if (genome.VertebraCount < GenomeLimits.MinVertebrae)
                return GenomeValidationResult.Fail(GenomeError.TooFewVertebrae);
            if (genome.VertebraCount > GenomeLimits.MaxVertebrae)
                return GenomeValidationResult.Fail(GenomeError.TooManyVertebrae);

            for (int i = 0; i < genome.VertebraCount; i++)
            {
                VertebraGene v = genome.GetVertebra(i);

                if (v.Radius < GenomeLimits.MinRadius || v.Radius > GenomeLimits.MaxRadius)
                    return GenomeValidationResult.Fail(GenomeError.RadiusOutOfRange, i);

                if (v.LocalOffset.magnitude > GenomeLimits.MaxSegmentLength)
                    return GenomeValidationResult.Fail(GenomeError.SegmentTooLong, i);
            }

            return GenomeValidationResult.Ok;
        }

        private static GenomeValidationResult ValidateParts(CreatureGenome genome, PartRuleSet rules)
        {
            if (genome.PartCount > GenomeLimits.MaxParts)
                return GenomeValidationResult.Fail(GenomeError.TooManyParts);

            for (int i = 0; i < genome.PartCount; i++)
            {
                PartGene p = genome.GetPart(i);

                if (p.BoneIndex >= genome.VertebraCount)
                    return GenomeValidationResult.Fail(GenomeError.BoneIndexOutOfRange, i);

                if (!rules.TryGetRule(p.PartId, out PartRule rule))
                    return GenomeValidationResult.Fail(GenomeError.UnknownPart, i);

                AttachmentSite site = SiteForBone(p.BoneIndex, genome.VertebraCount);
                if ((rule.AllowedSites & site) == 0)
                    return GenomeValidationResult.Fail(GenomeError.InvalidAttachmentSite, i);

                if (p.Mirrored && !rule.MirrorCapable)
                    return GenomeValidationResult.Fail(GenomeError.MirrorNotSupported, i);

                if (p.Scale < GenomeLimits.MinPartScale || p.Scale > GenomeLimits.MaxPartScale)
                    return GenomeValidationResult.Fail(GenomeError.ScaleOutOfRange, i);
            }

            return GenomeValidationResult.Ok;
        }

    }
}
