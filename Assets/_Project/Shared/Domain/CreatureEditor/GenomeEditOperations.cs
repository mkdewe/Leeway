using UnityEngine;

namespace Leeway.Creature.Domain
{
    /// <summary>
    /// Edit operations on a genome. Each one is atomic: it mutates a clone, checks the structural
    /// invariants, and only then writes the result back into the original — so a failed operation
    /// never leaves the genome half-changed.
    /// </summary>
    /// <remarks>
    /// They enforce the structure (ranges, bone indices, attachment-site compatibility) and the
    /// budget — part categories no longer have any cardinality limits, so the only thing holding back
    /// a creature's growth is the currency.
    /// </remarks>
    public static class GenomeEditOperations
    {
        /// <summary>
        /// Adds a vertebra after the given one. At the end of the tail it extends the spine along the
        /// last segment's direction and narrows the radius; in the middle it halves the existing
        /// segment so the rest of the body does not twitch.
        /// </summary>
        public static bool TryAddVertebra(CreatureGenome genome, int afterIndex, PartRuleSet rules, out GenomeError error)
        {
            if (genome == null) return Fail(GenomeError.NullGenome, out error);
            if (rules == null) rules = PartRuleSet.Empty;
            if (genome.VertebraCount >= GenomeLimits.MaxVertebrae) return Fail(GenomeError.TooManyVertebrae, out error);
            if (afterIndex < 0 || afterIndex >= genome.VertebraCount) return Fail(GenomeError.VertebraIndexOutOfRange, out error);

            if (!CreatureBudget.CanAfford(genome, rules, rules.VertebraCost))
                return Fail(GenomeError.InsufficientFunds, out error);

            CreatureGenome draft = genome.Clone();
            int insertIndex = afterIndex + 1;
            VertebraGene previous = draft.GetVertebra(afterIndex);

            if (insertIndex < draft.VertebraCount)
            {
                // Inserting in the middle: we halve the segment leading to the successor.
                VertebraGene successor = draft.GetVertebra(insertIndex);
                Vector3 half = successor.LocalOffset * 0.5f;
                float radius = Mathf.Clamp((previous.Radius + successor.Radius) * 0.5f, GenomeLimits.MinRadius, GenomeLimits.MaxRadius);

                draft.SetVertebra(insertIndex, successor.WithOffset(half));
                draft.InsertVertebra(insertIndex, new VertebraGene(half, Quaternion.identity, radius));
            }
            else
            {
                AppendVertebra(draft, afterIndex);
            }

            ShiftBoneIndices(draft, insertIndex, +1);

            return Commit(genome, draft, rules, out error);
        }

        /// <summary>
        /// Adds a vertebra after the last one: continues the direction of the last segment and
        /// narrows the radius.
        /// </summary>
        private static void AppendVertebra(CreatureGenome draft, int afterIndex)
        {
            VertebraGene previous = draft.GetVertebra(afterIndex);

            Vector3 step = afterIndex > 0 ? previous.LocalOffset : GenomeLimits.DefaultSegmentOffset;
            if (step.sqrMagnitude < 0.0001f) step = GenomeLimits.DefaultSegmentOffset;
            step = Vector3.ClampMagnitude(step, GenomeLimits.MaxSegmentLength);

            float radius = Mathf.Clamp(previous.Radius * GenomeLimits.TailTaper, GenomeLimits.MinRadius, GenomeLimits.MaxRadius);
            draft.AddVertebra(new VertebraGene(step, Quaternion.identity, radius));
        }

        /// <summary>
        /// Lengthens the spine by one vertebra at the given end — this is how the creature's body grows.
        /// </summary>
        /// <remarks>
        /// <para>Adding at the tail is an ordinary extension of the chain. Adding at the head requires
        /// inserting a vertebra <b>before</b> the current start: the new head takes over the anchor,
        /// and the old one gets the segment leading back to it. That way the rest of the body stays
        /// exactly where it was and a new vertebra grows in front of it.</para>
        ///
        /// <para>Parts on the end vertebra <b>ride along to the new end</b>. A face is meant to stay a
        /// face: if the eyes and mouth stayed on the old vertebra, growing the head would turn them
        /// into torso parts and the whole operation would trip over the attachment-site rule.</para>
        /// </remarks>
        public static bool TryExtendSpine(CreatureGenome genome, bool atHead, PartRuleSet rules, out GenomeError error)
        {
            if (genome == null) return Fail(GenomeError.NullGenome, out error);
            if (rules == null) rules = PartRuleSet.Empty;
            if (genome.VertebraCount >= GenomeLimits.MaxVertebrae) return Fail(GenomeError.TooManyVertebrae, out error);
            if (genome.VertebraCount == 0) return Fail(GenomeError.TooFewVertebrae, out error);

            if (!CreatureBudget.CanAfford(genome, rules, rules.VertebraCost))
                return Fail(GenomeError.InsufficientFunds, out error);

            if (!atHead)
            {
                CreatureGenome tailDraft = genome.Clone();
                int end = tailDraft.VertebraCount - 1;

                AppendVertebra(tailDraft, end);
                CarryParts(tailDraft, end, end + 1);

                return Commit(genome, tailDraft, rules, out error);
            }

            CreatureGenome draft = genome.Clone();
            VertebraGene head = draft.GetVertebra(0);

            // The "in front of the head" direction is the reverse of the segment leading to vertebra two.
            Vector3 step = draft.VertebraCount > 1
                ? -draft.GetVertebra(1).LocalOffset
                : GenomeLimits.DefaultSegmentOffset.normalized * -GenomeLimits.MaxSegmentLength;

            if (step.sqrMagnitude < 1e-6f) step = -GenomeLimits.DefaultSegmentOffset;
            step = step.normalized * Mathf.Clamp(step.magnitude, GenomeLimits.MinSegmentLength, GenomeLimits.MaxSegmentLength);

            float radius = Mathf.Clamp(head.Radius * GenomeLimits.TailTaper, GenomeLimits.MinRadius, GenomeLimits.MaxRadius);

            // The new head stands in front of the old one and takes over its anchor...
            draft.SetVertebra(0, head.WithOffset(-step));
            draft.InsertVertebra(0, new VertebraGene(head.LocalOffset + step, Quaternion.identity, radius));

            // ...the face moves onto it (staying at index 0), and the rest of the body shifts one bone
            // index further along.
            ShiftBoneIndices(draft, 1, +1);

            return Commit(genome, draft, rules, out error);
        }

        /// <summary>
        /// Shortens the spine by the end vertebra — the inverse of <see cref="TryExtendSpine"/>,
        /// driven by dragging the same arrow towards the middle of the body.
        /// </summary>
        /// <remarks>
        /// The removed vertebra's parts <b>move onto the new end</b> instead of disappearing with it.
        /// Taking back a grown piece of head is not meant to cost the player their eyes and mouth —
        /// <see cref="TryRemoveVertebra"/> is the "delete this vertebra along with whatever sits on
        /// it" operation, and that one is left unchanged.
        /// </remarks>
        public static bool TryShrinkSpine(CreatureGenome genome, bool atHead, PartRuleSet rules, out GenomeError error)
        {
            if (genome == null) return Fail(GenomeError.NullGenome, out error);
            if (rules == null) rules = PartRuleSet.Empty;
            if (genome.VertebraCount <= GenomeLimits.MinVertebrae) return Fail(GenomeError.TooFewVertebrae, out error);

            CreatureGenome draft = genome.Clone();

            int index = atHead ? 0 : draft.VertebraCount - 1;
            int survivor = atHead ? 1 : draft.VertebraCount - 2;

            CarryParts(draft, index, survivor);

            VertebraGene removed = draft.GetVertebra(index);
            draft.RemoveVertebraAt(index);
            ShiftBoneIndices(draft, index, -1);

            // Removing the head moves the anchor onto the new first vertebra, so the body does not
            // jump by the length of the deleted segment.
            if (atHead) draft.SetVertebra(0, draft.GetVertebra(0).WithOffset(removed.LocalOffset));

            return Commit(genome, draft, rules, out error);
        }

        /// <summary>
        /// Removes a vertebra along with the parts attached to it and renumbers the parts further down
        /// the spine. Removing the head moves the anchor onto the new first vertebra so the body does
        /// not jump; removing one in the middle simply shortens the spine (we do not merge the
        /// segments, because the sum could exceed the length limit).
        /// </summary>
        public static bool TryRemoveVertebra(CreatureGenome genome, int index, PartRuleSet rules, out GenomeError error)
        {
            if (genome == null) return Fail(GenomeError.NullGenome, out error);
            if (index < 0 || index >= genome.VertebraCount) return Fail(GenomeError.VertebraIndexOutOfRange, out error);
            if (genome.VertebraCount <= GenomeLimits.MinVertebrae) return Fail(GenomeError.TooFewVertebrae, out error);

            CreatureGenome draft = genome.Clone();
            VertebraGene removed = draft.GetVertebra(index);

            for (int i = draft.PartCount - 1; i >= 0; i--)
            {
                if (draft.GetPart(i).BoneIndex == index)
                    draft.RemovePartAt(i);
            }

            draft.RemoveVertebraAt(index);
            ShiftBoneIndices(draft, index, -1);

            if (index == 0 && draft.VertebraCount > 0)
                draft.SetVertebra(0, draft.GetVertebra(0).WithOffset(removed.LocalOffset));

            return Commit(genome, draft, rules, out error);
        }

        /// <summary>
        /// Moves <b>one</b> vertebra, leaving the rest of the spine where it was.
        /// </summary>
        /// <remarks>
        /// <para>Vertebrae form a chain, so changing an offset raw drags the whole tail with it and the
        /// creature stretches along its entire length. Here we shorten or lengthen <b>only</b> the
        /// segment leading to this vertebra and subtract the difference from the next one — which
        /// keeps the further part of the body standing still.</para>
        ///
        /// <para>The segment length is clamped to
        /// <see cref="GenomeLimits.MinSegmentLength"/>..<see cref="GenomeLimits.MaxSegmentLength"/>.
        /// It is driven by dragging a handle, so it clamps rather than rejects.</para>
        /// </remarks>
        public static bool TrySetVertebraOffset(CreatureGenome genome, int index, Vector3 offset, out GenomeError error)
        {
            if (genome == null) return Fail(GenomeError.NullGenome, out error);
            if (index < 0 || index >= genome.VertebraCount) return Fail(GenomeError.VertebraIndexOutOfRange, out error);

            VertebraGene current = genome.GetVertebra(index);
            Vector3 clamped = ClampSegment(offset, index);

            // The tail stays put: whatever we added to this segment we subtract from the next one, in
            // its own frame.
            int next = index + 1;
            if (next < genome.VertebraCount)
            {
                VertebraGene successor = genome.GetVertebra(next);
                Vector3 compensation = Quaternion.Inverse(current.LocalRotation) * (current.LocalOffset - clamped);

                genome.SetVertebra(next, successor.WithOffset(
                    ClampCompensated(successor.LocalOffset + compensation, successor.LocalOffset)));
            }

            genome.SetVertebra(index, current.WithOffset(clamped));

            error = GenomeError.None;
            return true;
        }

        /// <summary>
        /// Clamps the segment after compensation, making sure it <b>does not flip direction</b>.
        /// </summary>
        /// <remarks>
        /// When the grabbed segment stretches further than the next one can give back, raw
        /// compensation turns that next one inside out and the spine folds in on itself. In that case
        /// we stop the segment at its minimum in its original direction — the tail twitches, but the
        /// body stays a body.
        /// </remarks>
        private static Vector3 ClampCompensated(Vector3 compensated, Vector3 reference)
        {
            if (reference.sqrMagnitude < 1e-8f) return ClampSegment(compensated, 1);

            Vector3 direction = reference.normalized;
            float along = Vector3.Dot(compensated, direction);

            if (along < GenomeLimits.MinSegmentLength) return direction * GenomeLimits.MinSegmentLength;

            return ClampSegment(compensated, 1);
        }

        /// <summary>
        /// Clamps a segment to the allowed length. Vertebra 0 has no predecessor, so it describes no
        /// segment — its offset is the head's anchor and is subject only to the upper bound.
        /// </summary>
        private static Vector3 ClampSegment(Vector3 offset, int index)
        {
            float length = offset.magnitude;

            if (index == 0) return Vector3.ClampMagnitude(offset, GenomeLimits.MaxSegmentLength);

            // A zero vector has no direction to stretch along — we restore the default spine step.
            if (length < 1e-5f) return GenomeLimits.DefaultSegmentOffset.normalized * GenomeLimits.MinSegmentLength;

            return offset / length * Mathf.Clamp(length, GenomeLimits.MinSegmentLength, GenomeLimits.MaxSegmentLength);
        }

        /// <summary>Sets a vertebra's radius, clamping to the allowed range. Driven by the scroll wheel, so it clamps rather than rejects.</summary>
        public static bool TrySetVertebraRadius(CreatureGenome genome, int index, float radius, out GenomeError error)
        {
            if (genome == null) return Fail(GenomeError.NullGenome, out error);
            if (index < 0 || index >= genome.VertebraCount) return Fail(GenomeError.VertebraIndexOutOfRange, out error);

            float clamped = Mathf.Clamp(radius, GenomeLimits.MinRadius, GenomeLimits.MaxRadius);
            genome.SetVertebra(index, genome.GetVertebra(index).WithRadius(clamped));

            error = GenomeError.None;
            return true;
        }

        public static bool TrySetVertebraRotation(CreatureGenome genome, int index, Quaternion rotation, out GenomeError error)
        {
            if (genome == null) return Fail(GenomeError.NullGenome, out error);
            if (index < 0 || index >= genome.VertebraCount) return Fail(GenomeError.VertebraIndexOutOfRange, out error);

            genome.SetVertebra(index, genome.GetVertebra(index).WithRotation(rotation));

            error = GenomeError.None;
            return true;
        }

        /// <summary>Attaches a part at the given bone's default spot.</summary>
        public static bool TryAttachPart(CreatureGenome genome, int boneIndex, int partId, bool mirrored, PartRuleSet rules, out GenomeError error)
        {
            if (genome == null) return Fail(GenomeError.NullGenome, out error);
            if (boneIndex < 0 || boneIndex >= genome.VertebraCount) return Fail(GenomeError.BoneIndexOutOfRange, out error);

            return TryAttachPart(genome, PartGene.Default(partId, (byte)boneIndex, mirrored), rules, out error);
        }

        public static bool TryAttachPart(CreatureGenome genome, in PartGene gene, PartRuleSet rules, out GenomeError error)
        {
            if (genome == null) return Fail(GenomeError.NullGenome, out error);
            if (genome.PartCount >= GenomeLimits.MaxParts) return Fail(GenomeError.TooManyParts, out error);
            if (rules == null) rules = PartRuleSet.Empty;
            if (!rules.TryGetRule(gene.PartId, out PartRule rule)) return Fail(GenomeError.UnknownPart, out error);

            // We check the budget at attach time already — otherwise the player would keep adding
            // parts that the commit would refuse anyway.
            if (!CreatureBudget.CanAfford(genome, rules, rule.Cost)) return Fail(GenomeError.InsufficientFunds, out error);

            CreatureGenome draft = genome.Clone();
            draft.AddPart(gene);

            return Commit(genome, draft, rules, out error);
        }

        /// <summary>
        /// Moves a part relative to its own bone and stores the point <b>already pulled onto the
        /// skin</b>. Driven by dragging a handle, so it clamps rather than rejects.
        /// </summary>
        /// <remarks>
        /// <para>The gene has to hold exactly the point where the part is seen. Previously it held a
        /// raw "request" of arbitrary length, and <see cref="PartPlacement"/> pulled it onto the
        /// surface anyway — so moving the cursor by <c>d</c> translated into moving the part by
        /// <c>d·(radius / length of the stored vector)</c>. The starter directions have a length on the
        /// order of 1.0 against a radius of 0.3, so the part travelled three times slower than the
        /// cursor and escaped from under it the longer the drag lasted.</para>
        ///
        /// <para>The projection is idempotent: a point on the skin fed back into
        /// <see cref="PartPlacement.Resolve"/> returns itself, so the stored value does not drift
        /// across successive edits.</para>
        /// </remarks>
        public static bool TrySetPartLocalPosition(CreatureGenome genome, int partIndex, Vector3 localPosition, out GenomeError error)
        {
            if (genome == null) return Fail(GenomeError.NullGenome, out error);
            if (partIndex < 0 || partIndex >= genome.PartCount) return Fail(GenomeError.PartIndexOutOfRange, out error);

            PartGene gene = genome.GetPart(partIndex);
            Vector3 clamped = Vector3.ClampMagnitude(localPosition, GenomeLimits.MaxPartOffset);
            Vector3 onSkin = PartPlacement.Resolve(genome, gene.BoneIndex, clamped, skinOffset: 0f).Position;

            genome.SetPart(partIndex, gene.WithLocalPosition(onSkin));

            error = GenomeError.None;
            return true;
        }

        /// <summary>
        /// Moves a part onto a different vertebra, keeping its place on the skin.
        /// </summary>
        /// <remarks>
        /// Needed when dragging along the body: one vertebra's skin ends halfway to its neighbour, so
        /// travelling further has to change the bone — otherwise the part stops in mid-air while the
        /// cursor keeps going.
        /// </remarks>
        public static bool TrySetPartBone(CreatureGenome genome, int partIndex, int boneIndex, Vector3 localPosition,
            PartRuleSet rules, out GenomeError error)
        {
            if (genome == null) return Fail(GenomeError.NullGenome, out error);
            if (partIndex < 0 || partIndex >= genome.PartCount) return Fail(GenomeError.PartIndexOutOfRange, out error);
            if (boneIndex < 0 || boneIndex >= genome.VertebraCount) return Fail(GenomeError.BoneIndexOutOfRange, out error);
            if (rules == null) rules = PartRuleSet.Empty;

            PartGene gene = genome.GetPart(partIndex);
            if (!rules.TryGetRule(gene.PartId, out PartRule rule)) return Fail(GenomeError.UnknownPart, out error);

            AttachmentSite site = GenomeValidator.SiteForBone(boneIndex, genome.VertebraCount);
            if ((rule.AllowedSites & site) == 0) return Fail(GenomeError.InvalidAttachmentSite, out error);

            Vector3 clamped = Vector3.ClampMagnitude(localPosition, GenomeLimits.MaxPartOffset);
            Vector3 onSkin = PartPlacement.Resolve(genome, boneIndex, clamped, skinOffset: 0f).Position;

            genome.SetPart(partIndex, gene.WithBoneIndex((byte)boneIndex).WithLocalPosition(onSkin));

            error = GenomeError.None;
            return true;
        }

        /// <summary>
        /// Sets a part's rotation correction — what the rotation gizmo adds <b>on top of</b> the
        /// automatic orientation from <see cref="PartOrientation"/>.
        /// </summary>
        public static bool TrySetPartRotation(CreatureGenome genome, int partIndex, Quaternion rotation, out GenomeError error)
        {
            if (genome == null) return Fail(GenomeError.NullGenome, out error);
            if (partIndex < 0 || partIndex >= genome.PartCount) return Fail(GenomeError.PartIndexOutOfRange, out error);

            genome.SetPart(partIndex, genome.GetPart(partIndex).WithLocalRotation(rotation.normalized));

            error = GenomeError.None;
            return true;
        }

        /// <summary>
        /// Reshapes an attached part's leg: the number of bends and the segment length.
        /// </summary>
        /// <remarks>
        /// <para>The record lands in the <b>gene</b>, not in the catalog — two pairs of the same legs
        /// on one creature may have different builds, and the catalog stays a starting point rather
        /// than a verdict.</para>
        ///
        /// <para>It applies to locomotion parts only: a recorded leg drives the gait, the stance
        /// height, the stability and the speed, whereas for an eye or a horn it means nothing and
        /// would be a dead byte in every packet.</para>
        ///
        /// <para><c>LegSpec</c> clamps both numbers to the bounds from <c>LegLimits</c> itself, so this
        /// operation does not have to validate them separately.</para>
        /// </remarks>
        public static bool TrySetPartLeg(CreatureGenome genome, int partIndex, LegSpec leg, PartRuleSet rules, out GenomeError error)
        {
            if (genome == null) return Fail(GenomeError.NullGenome, out error);
            if (partIndex < 0 || partIndex >= genome.PartCount) return Fail(GenomeError.PartIndexOutOfRange, out error);
            if (rules == null) rules = PartRuleSet.Empty;

            PartGene gene = genome.GetPart(partIndex);
            if (!rules.TryGetRule(gene.PartId, out PartRule rule)) return Fail(GenomeError.UnknownPart, out error);
            if (rule.Category != PartCategory.Locomotion) return Fail(GenomeError.InvalidAttachmentSite, out error);

            genome.SetPart(partIndex, gene.WithLeg(leg));

            error = GenomeError.None;
            return true;
        }

        /// <summary>
        /// Toggles a part between a single piece and a mirrored pair. Turning it on requires a part
        /// that has a mirrored version at all; turning it off is always allowed.
        /// </summary>
        public static bool TrySetPartMirrored(CreatureGenome genome, int partIndex, bool mirrored, PartRuleSet rules, out GenomeError error)
        {
            if (genome == null) return Fail(GenomeError.NullGenome, out error);
            if (partIndex < 0 || partIndex >= genome.PartCount) return Fail(GenomeError.PartIndexOutOfRange, out error);
            if (rules == null) rules = PartRuleSet.Empty;

            PartGene gene = genome.GetPart(partIndex);

            if (mirrored)
            {
                if (!rules.TryGetRule(gene.PartId, out PartRule rule)) return Fail(GenomeError.UnknownPart, out error);
                if (!rule.MirrorCapable) return Fail(GenomeError.MirrorNotSupported, out error);
            }

            genome.SetPart(partIndex, gene.WithMirrored(mirrored));

            error = GenomeError.None;
            return true;
        }

        public static bool TryDetachPart(CreatureGenome genome, int partIndex, out GenomeError error)
        {
            if (genome == null) return Fail(GenomeError.NullGenome, out error);
            if (partIndex < 0 || partIndex >= genome.PartCount) return Fail(GenomeError.PartIndexOutOfRange, out error);

            genome.RemovePartAt(partIndex);

            error = GenomeError.None;
            return true;
        }

        /// <summary>
        /// The structural invariants required after every edit. This is a subset of
        /// <see cref="GenomeValidator.Validate"/> without the blob size check, which only applies at
        /// commit time.
        /// </summary>
        public static GenomeValidationResult ValidateStructure(CreatureGenome genome, PartRuleSet rules)
        {
            if (genome == null) return GenomeValidationResult.Fail(GenomeError.NullGenome);
            if (rules == null) rules = PartRuleSet.Empty;

            if (genome.VertebraCount < GenomeLimits.MinVertebrae) return GenomeValidationResult.Fail(GenomeError.TooFewVertebrae);
            if (genome.VertebraCount > GenomeLimits.MaxVertebrae) return GenomeValidationResult.Fail(GenomeError.TooManyVertebrae);
            if (genome.PartCount > GenomeLimits.MaxParts) return GenomeValidationResult.Fail(GenomeError.TooManyParts);

            for (int i = 0; i < genome.VertebraCount; i++)
            {
                VertebraGene v = genome.GetVertebra(i);
                if (v.Radius < GenomeLimits.MinRadius || v.Radius > GenomeLimits.MaxRadius)
                    return GenomeValidationResult.Fail(GenomeError.RadiusOutOfRange, i);
                if (v.LocalOffset.magnitude > GenomeLimits.MaxSegmentLength)
                    return GenomeValidationResult.Fail(GenomeError.SegmentTooLong, i);
            }

            for (int i = 0; i < genome.PartCount; i++)
            {
                PartGene p = genome.GetPart(i);

                if (p.BoneIndex >= genome.VertebraCount)
                    return GenomeValidationResult.Fail(GenomeError.BoneIndexOutOfRange, i);
                if (!rules.TryGetRule(p.PartId, out PartRule rule))
                    return GenomeValidationResult.Fail(GenomeError.UnknownPart, i);

                AttachmentSite site = GenomeValidator.SiteForBone(p.BoneIndex, genome.VertebraCount);
                if ((rule.AllowedSites & site) == 0)
                    return GenomeValidationResult.Fail(GenomeError.InvalidAttachmentSite, i);
                if (p.Mirrored && !rule.MirrorCapable)
                    return GenomeValidationResult.Fail(GenomeError.MirrorNotSupported, i);
                if (p.Scale < GenomeLimits.MinPartScale || p.Scale > GenomeLimits.MaxPartScale)
                    return GenomeValidationResult.Fail(GenomeError.ScaleOutOfRange, i);
            }

            if (!CreatureBudget.Evaluate(genome, rules).IsAffordable)
                return GenomeValidationResult.Fail(GenomeError.InsufficientFunds);

            return GenomeValidationResult.Ok;
        }

        /// <summary>
        /// Moves parts from one vertebra to another. Used wherever an end of the body migrates to a
        /// different vertebra and whatever sits on it should travel along.
        /// </summary>
        private static void CarryParts(CreatureGenome genome, int fromBoneIndex, int toBoneIndex)
        {
            if (fromBoneIndex == toBoneIndex) return;

            int target = Mathf.Clamp(toBoneIndex, 0, GenomeLimits.MaxVertebrae - 1);

            for (int i = 0; i < genome.PartCount; i++)
            {
                PartGene p = genome.GetPart(i);
                if (p.BoneIndex != fromBoneIndex) continue;

                genome.SetPart(i, p.WithBoneIndex((byte)target));
            }
        }

        /// <summary>Shifts the bone indices of parts from <paramref name="fromBoneIndex"/> upwards by <paramref name="delta"/>.</summary>
        private static void ShiftBoneIndices(CreatureGenome genome, int fromBoneIndex, int delta)
        {
            for (int i = 0; i < genome.PartCount; i++)
            {
                PartGene p = genome.GetPart(i);
                if (p.BoneIndex < fromBoneIndex) continue;

                int shifted = Mathf.Clamp(p.BoneIndex + delta, 0, GenomeLimits.MaxVertebrae - 1);
                genome.SetPart(i, p.WithBoneIndex((byte)shifted));
            }
        }

        /// <summary>Writes the operation's result into the original, provided it passes the structural invariants.</summary>
        private static bool Commit(CreatureGenome target, CreatureGenome draft, PartRuleSet rules, out GenomeError error)
        {
            GenomeValidationResult result = ValidateStructure(draft, rules);
            if (!result.IsValid) return Fail(result.Error, out error);

            target.CopyFrom(draft);
            error = GenomeError.None;
            return true;
        }

        private static bool Fail(GenomeError reason, out GenomeError error)
        {
            error = reason;
            return false;
        }
    }
}
