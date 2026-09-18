using Leeway.Creature.Domain;
using NUnit.Framework;
using UnityEngine;

namespace Leeway.Tests
{
    /// <summary>
    /// The densest set in the whole domain. Adding and removing vertebrae renumbers the bone indices
    /// and changes the attachment sites (the last vertebra stops being the tail), so this is where a
    /// silent defect is easiest to make — one that would show up only as a part floating in mid-air on
    /// another player's screen.
    /// </summary>
    public class GenomeEditOperationsTests
    {
        private PartRuleSet _rules;

        [SetUp]
        public void SetUp() => _rules = GenomeTestFixtures.Rules();

        // --- Removing vertebrae ---

        [Test]
        public void RemoveVertebra_DropsPartsAttachedToRemovedBone()
        {
            CreatureGenome genome = GenomeTestFixtures.Spine(4);
            genome.AddPart(PartGene.Default(GenomeTestFixtures.EyeId, 0, mirrored: true));
            genome.AddPart(PartGene.Default(GenomeTestFixtures.AntennaId, 1, mirrored: false));

            Assert.IsTrue(GenomeEditOperations.TryRemoveVertebra(genome, 1, _rules, out GenomeError error), error.ToString());

            Assert.AreEqual(3, genome.VertebraCount);
            Assert.AreEqual(1, genome.PartCount, "A part sitting on the removed vertebra has to disappear with it.");
            Assert.AreEqual(GenomeTestFixtures.EyeId, genome.GetPart(0).PartId);
        }

        [Test]
        public void RemoveVertebra_ReindexesPartsOnLaterBones()
        {
            CreatureGenome genome = GenomeTestFixtures.Spine(4);
            genome.AddPart(PartGene.Default(GenomeTestFixtures.EyeId, 0, mirrored: true));
            genome.AddPart(PartGene.Default(GenomeTestFixtures.AntennaId, 2, mirrored: false));

            Assert.IsTrue(GenomeEditOperations.TryRemoveVertebra(genome, 1, _rules, out GenomeError error), error.ToString());

            Assert.AreEqual(3, genome.VertebraCount);
            Assert.AreEqual(0, genome.GetPart(0).BoneIndex, "A part before the removed vertebra must not move.");
            Assert.AreEqual(1, genome.GetPart(1).BoneIndex, "A part after the removed vertebra has to shift down by one.");
        }

        [Test]
        public void RemoveVertebra_Head_MovesAnchorToNewFirstVertebra()
        {
            var genome = new CreatureGenome();
            var anchor = new Vector3(0f, 0.5f, 0f);
            genome.AddVertebra(new VertebraGene(anchor, Quaternion.identity, 0.3f));
            genome.AddVertebra(new VertebraGene(GenomeLimits.DefaultSegmentOffset, Quaternion.identity, 0.3f));
            genome.AddVertebra(new VertebraGene(GenomeLimits.DefaultSegmentOffset, Quaternion.identity, 0.3f));

            Assert.IsTrue(GenomeEditOperations.TryRemoveVertebra(genome, 0, _rules, out GenomeError error), error.ToString());

            Assert.AreEqual(2, genome.VertebraCount);
            Assert.AreEqual(anchor, genome.GetVertebra(0).LocalOffset,
                "The new head takes over the old one's anchor so the body does not jump.");
        }

        [Test]
        public void RemoveVertebra_AtMinimumCount_Fails()
        {
            CreatureGenome genome = GenomeTestFixtures.Spine(GenomeLimits.MinVertebrae);

            Assert.IsFalse(GenomeEditOperations.TryRemoveVertebra(genome, 0, _rules, out GenomeError error));
            Assert.AreEqual(GenomeError.TooFewVertebrae, error);
            Assert.AreEqual(GenomeLimits.MinVertebrae, genome.VertebraCount);
        }

        [Test]
        public void RemoveVertebra_OutOfRangeIndex_Fails()
        {
            CreatureGenome genome = GenomeTestFixtures.Spine(4);

            Assert.IsFalse(GenomeEditOperations.TryRemoveVertebra(genome, 9, _rules, out GenomeError error));
            Assert.AreEqual(GenomeError.VertebraIndexOutOfRange, error);
        }

        // --- Adding vertebrae ---

        [Test]
        public void AddVertebra_AtTail_ExtendsSpineAndTapersRadius()
        {
            CreatureGenome genome = GenomeTestFixtures.Valid();
            float previousRadius = genome.GetVertebra(2).Radius;

            Assert.IsTrue(GenomeEditOperations.TryAddVertebra(genome, 2, _rules, out GenomeError error), error.ToString());

            Assert.AreEqual(4, genome.VertebraCount);
            VertebraGene added = genome.GetVertebra(3);
            Assert.AreEqual(previousRadius * GenomeLimits.TailTaper, added.Radius, 0.0001f);
            Assert.AreEqual(GenomeLimits.DefaultSegmentOffset.z, added.LocalOffset.z, 0.0001f);
        }

        [Test]
        public void AddVertebra_InMiddle_SplitsSegmentAndPreservesTotalLength()
        {
            CreatureGenome genome = GenomeTestFixtures.Valid();
            float lengthBefore = TotalSpineLength(genome);

            Assert.IsTrue(GenomeEditOperations.TryAddVertebra(genome, 1, _rules, out GenomeError error), error.ToString());

            Assert.AreEqual(4, genome.VertebraCount);
            Assert.AreEqual(lengthBefore, TotalSpineLength(genome), 0.0001f,
                "Inserting in the middle halves the segment, so the rest of the body must not move.");
            Assert.AreEqual(genome.GetVertebra(2).LocalOffset, genome.GetVertebra(3).LocalOffset);
        }

        [Test]
        public void AddVertebra_InMiddle_ShiftsPartsOnLaterBones()
        {
            CreatureGenome genome = GenomeTestFixtures.Spine(4);
            genome.AddPart(PartGene.Default(GenomeTestFixtures.LegsId, 1, mirrored: true));
            genome.AddPart(PartGene.Default(GenomeTestFixtures.AntennaId, 2, mirrored: false));

            Assert.IsTrue(GenomeEditOperations.TryAddVertebra(genome, 1, _rules, out GenomeError error), error.ToString());

            Assert.AreEqual(5, genome.VertebraCount);
            Assert.AreEqual(1, genome.GetPart(0).BoneIndex, "A part before the insertion point stays on its own bone.");
            Assert.AreEqual(3, genome.GetPart(1).BoneIndex, "A part after the insertion point shifts up by one.");
        }

        [Test]
        public void AddVertebra_AtMaximumCount_Fails()
        {
            CreatureGenome genome = GenomeTestFixtures.Spine(GenomeLimits.MaxVertebrae);

            Assert.IsFalse(GenomeEditOperations.TryAddVertebra(genome, 0, _rules, out GenomeError error));
            Assert.AreEqual(GenomeError.TooManyVertebrae, error);
            Assert.AreEqual(GenomeLimits.MaxVertebrae, genome.VertebraCount);
        }

        [Test]
        public void AddVertebra_WhenItWouldInvalidateTailOnlyPart_FailsAndLeavesGenomeUntouched()
        {
            // The fin is allowed on the tail only. Adding a vertebra at the end turns the previous tail
            // into torso, so the operation has to roll back completely.
            CreatureGenome genome = GenomeTestFixtures.Spine(3);
            genome.AddPart(PartGene.Default(GenomeTestFixtures.FinId, 2, mirrored: true));

            Assert.IsFalse(GenomeEditOperations.TryAddVertebra(genome, 2, _rules, out GenomeError error));

            Assert.AreEqual(GenomeError.InvalidAttachmentSite, error);
            Assert.AreEqual(3, genome.VertebraCount, "A failed operation must not leave the added vertebra behind.");
            Assert.AreEqual(1, genome.PartCount);
            Assert.AreEqual(2, genome.GetPart(0).BoneIndex);
        }

        // --- Parts ---

        [Test]
        public void AttachPart_SecondLocomotionPart_Succeeds()
        {
            CreatureGenome genome = GenomeTestFixtures.Valid();

            Assert.IsTrue(GenomeEditOperations.TryAttachPart(genome, 2, GenomeTestFixtures.FinId, true, _rules, out GenomeError error), error.ToString());
            Assert.AreEqual(4, genome.PartCount, "Categories no longer have count limits.");
        }

        [Test]
        public void AttachPart_WithoutFunds_Fails()
        {
            CreatureGenome genome = GenomeTestFixtures.Valid();
            var broke = GenomeTestFixtures.Rules(budget: CreatureBudget.Evaluate(genome, _rules).Spent);

            Assert.IsFalse(GenomeEditOperations.TryAttachPart(genome, 2, GenomeTestFixtures.FinId, true, broke, out GenomeError error));
            Assert.AreEqual(GenomeError.InsufficientFunds, error);
            Assert.AreEqual(3, genome.PartCount, "A rejected attachment must not leave the part in the genome.");
        }

        [Test]
        public void AddVertebra_WithoutFunds_Fails()
        {
            CreatureGenome genome = GenomeTestFixtures.Valid();
            var broke = GenomeTestFixtures.Rules(budget: CreatureBudget.Evaluate(genome, _rules).Spent);

            Assert.IsFalse(GenomeEditOperations.TryAddVertebra(genome, 2, broke, out GenomeError error));
            Assert.AreEqual(GenomeError.InsufficientFunds, error);
            Assert.AreEqual(3, genome.VertebraCount);
        }

        [Test]
        public void AttachPart_UnknownPartId_Fails()
        {
            CreatureGenome genome = GenomeTestFixtures.Valid();

            Assert.IsFalse(GenomeEditOperations.TryAttachPart(genome, 0, GenomeTestFixtures.UnknownId, false, _rules, out GenomeError error));
            Assert.AreEqual(GenomeError.UnknownPart, error);
        }

        [Test]
        public void AttachPart_OnDisallowedSite_Fails()
        {
            CreatureGenome genome = GenomeTestFixtures.Spine();
            genome.AddPart(PartGene.Default(GenomeTestFixtures.LegsId, 1, mirrored: true));

            // The jaw is head-only, and vertebra 1 is torso.
            Assert.IsFalse(GenomeEditOperations.TryAttachPart(genome, 1, GenomeTestFixtures.JawId, false, _rules, out GenomeError error));
            Assert.AreEqual(GenomeError.InvalidAttachmentSite, error);
            Assert.AreEqual(1, genome.PartCount);
        }

        [Test]
        public void AttachPart_MirroredOnNonMirrorCapablePart_Fails()
        {
            CreatureGenome genome = GenomeTestFixtures.Spine();
            genome.AddPart(PartGene.Default(GenomeTestFixtures.LegsId, 1, mirrored: true));

            Assert.IsFalse(GenomeEditOperations.TryAttachPart(genome, 0, GenomeTestFixtures.JawId, true, _rules, out GenomeError error));
            Assert.AreEqual(GenomeError.MirrorNotSupported, error);
        }

        [Test]
        public void AttachPart_OnNonexistentBone_Fails()
        {
            CreatureGenome genome = GenomeTestFixtures.Valid();

            Assert.IsFalse(GenomeEditOperations.TryAttachPart(genome, 7, GenomeTestFixtures.AntennaId, false, _rules, out GenomeError error));
            Assert.AreEqual(GenomeError.BoneIndexOutOfRange, error);
        }

        [Test]
        public void DetachPart_RemovesPartAndFreesBudget()
        {
            CreatureGenome genome = GenomeTestFixtures.Valid();
            int before = CreatureBudget.Evaluate(genome, _rules).Spent;

            Assert.IsTrue(GenomeEditOperations.TryDetachPart(genome, 0, out GenomeError error), error.ToString());

            Assert.AreEqual(2, genome.PartCount);
            Assert.AreEqual(before - GenomeTestFixtures.PartCost, CreatureBudget.Evaluate(genome, _rules).Spent);
            Assert.IsTrue(GenomeValidator.Validate(genome, _rules).IsValid,
                "A creature with no locomotion part is now perfectly legal.");
        }

        [Test]
        public void DetachPart_OutOfRangeIndex_Fails()
        {
            CreatureGenome genome = GenomeTestFixtures.Valid();

            Assert.IsFalse(GenomeEditOperations.TryDetachPart(genome, 12, out GenomeError error));
            Assert.AreEqual(GenomeError.PartIndexOutOfRange, error);
        }

        // --- Sculpting ---

        [Test]
        public void SetVertebraRadius_ClampsIntoAllowedRange()
        {
            CreatureGenome genome = GenomeTestFixtures.Valid();

            Assert.IsTrue(GenomeEditOperations.TrySetVertebraRadius(genome, 0, 99f, out _));
            Assert.AreEqual(GenomeLimits.MaxRadius, genome.GetVertebra(0).Radius, 0.0001f);

            Assert.IsTrue(GenomeEditOperations.TrySetVertebraRadius(genome, 0, -5f, out _));
            Assert.AreEqual(GenomeLimits.MinRadius, genome.GetVertebra(0).Radius, 0.0001f);
        }

        [Test]
        public void SetVertebraOffset_ClampsSegmentLength()
        {
            CreatureGenome genome = GenomeTestFixtures.Valid();

            Assert.IsTrue(GenomeEditOperations.TrySetVertebraOffset(genome, 1, new Vector3(0f, 0f, -50f), out _));

            Assert.AreEqual(GenomeLimits.MaxSegmentLength, genome.GetVertebra(1).LocalOffset.magnitude, 0.0001f);
            Assert.IsTrue(GenomeEditOperations.ValidateStructure(genome, _rules).IsValid);
        }

        // --- Stretching a single vertebra ---

        /// <summary>
        /// The heart of the fix: the vertebra chain meant that dragging one vertebra pulled the whole
        /// tail along and the creature stretched over its entire length.
        /// </summary>
        [Test]
        public void MovingOneVertebra_LeavesTheRestOfTheSpineInPlace()
        {
            CreatureGenome genome = GenomeTestFixtures.Spine(4);
            Vector3 tailBefore = TipPosition(genome);

            // A stretch of 0.15 — the next segment has enough slack to absorb it.
            Assert.IsTrue(GenomeEditOperations.TrySetVertebraOffset(genome, 1, new Vector3(0f, 0f, -0.5f), out _));

            Assert.AreEqual(0.5f, genome.GetVertebra(1).LocalOffset.magnitude, 0.001f, "The grabbed segment should stretch.");
            Assert.AreEqual(0f, Vector3.Distance(tailBefore, TipPosition(genome)), 0.001f,
                "The rest of the spine should stay put — only the grabbed segment stretches.");
        }

        /// <summary>
        /// The limit of the compensation: once the next segment has nothing left to give, the tail has
        /// to move. Better that than letting the vertebrae run into each other.
        /// </summary>
        [Test]
        public void StretchingBeyondWhatTheNextSegmentCanAbsorb_MovesTheTail()
        {
            CreatureGenome genome = GenomeTestFixtures.Spine(4);
            Vector3 tailBefore = TipPosition(genome);

            GenomeEditOperations.TrySetVertebraOffset(genome, 1, new Vector3(0f, 0f, -0.9f), out _);

            Assert.AreEqual(GenomeLimits.MinSegmentLength, genome.GetVertebra(2).LocalOffset.magnitude, 0.001f,
                "The next segment should compress to the minimum, not below it.");
            Assert.Greater(Vector3.Distance(tailBefore, TipPosition(genome)), 0f);
        }

        [Test]
        public void SegmentLength_IsClampedBetweenMinAndMax()
        {
            CreatureGenome genome = GenomeTestFixtures.Spine(4);

            GenomeEditOperations.TrySetVertebraOffset(genome, 1, new Vector3(0f, 0f, -50f), out _);
            Assert.AreEqual(GenomeLimits.MaxSegmentLength, genome.GetVertebra(1).LocalOffset.magnitude, 0.001f);

            GenomeEditOperations.TrySetVertebraOffset(genome, 1, new Vector3(0f, 0f, -0.001f), out _);
            Assert.AreEqual(GenomeLimits.MinSegmentLength, genome.GetVertebra(1).LocalOffset.magnitude, 0.001f,
                "Without a lower bound, neighbouring vertebrae could be squeezed into a single point.");
        }

        [Test]
        public void CollapsedSegment_RecoversADirection()
        {
            CreatureGenome genome = GenomeTestFixtures.Spine(3);

            Assert.IsTrue(GenomeEditOperations.TrySetVertebraOffset(genome, 1, Vector3.zero, out _));

            Assert.AreEqual(GenomeLimits.MinSegmentLength, genome.GetVertebra(1).LocalOffset.magnitude, 0.001f,
                "A zero vector has no direction — the segment has to be given the default one.");
        }

        // --- Extending the spine ---

        [Test]
        public void ExtendingAtTheTail_AddsAVertebra()
        {
            CreatureGenome genome = GenomeTestFixtures.Spine(3);

            Assert.IsTrue(GenomeEditOperations.TryExtendSpine(genome, atHead: false, _rules, out GenomeError error), error.ToString());
            Assert.AreEqual(4, genome.VertebraCount);
        }

        [Test]
        public void ExtendingAtTheHead_PutsTheNewVertebraInFront()
        {
            CreatureGenome genome = GenomeTestFixtures.Spine(3);
            Vector3 tailBefore = TipPosition(genome);

            Assert.IsTrue(GenomeEditOperations.TryExtendSpine(genome, atHead: true, _rules, out GenomeError error), error.ToString());

            Assert.AreEqual(4, genome.VertebraCount);
            Assert.AreEqual(0f, Vector3.Distance(tailBefore, TipPosition(genome)), 0.001f,
                "Adding a head must not move the rest of the body.");
        }

        [Test]
        public void ExtendingAtTheHead_ShiftsPartsOntoTheirNewBoneIndex()
        {
            CreatureGenome genome = GenomeTestFixtures.Spine(3);

            // The antenna sits on the torso — adding a head pushes it one vertebra further along.
            genome.AddPart(PartGene.Default(GenomeTestFixtures.AntennaId, 1, mirrored: false));

            Assert.IsTrue(GenomeEditOperations.TryExtendSpine(genome, atHead: true, _rules, out GenomeError error), error.ToString());

            Assert.AreEqual(2, genome.GetPart(0).BoneIndex,
                "The part kept its old index and now hangs on a different vertebra.");
        }

        /// <summary>
        /// The face travels to the new tip. Were the eyes to stay on the old vertebra, growing a head
        /// would turn them into torso parts — and those are allowed on the head only, so the whole
        /// operation would fall over on the attachment-site rule.
        /// </summary>
        [Test]
        public void ExtendingAtTheHead_CarriesTheFaceOntoTheNewHead()
        {
            CreatureGenome genome = GenomeTestFixtures.Spine(3);
            genome.AddPart(PartGene.Default(GenomeTestFixtures.EyeId, 0, mirrored: true));

            Assert.IsTrue(GenomeEditOperations.TryExtendSpine(genome, atHead: true, _rules, out GenomeError error), error.ToString());

            Assert.AreEqual(4, genome.VertebraCount);
            Assert.AreEqual(0, genome.GetPart(0).BoneIndex, "The eye stayed on the old vertebra and stopped being a head part.");
        }

        [Test]
        public void ExtendingAtTheTail_CarriesTailOnlyPartsOntoTheNewTail()
        {
            CreatureGenome genome = GenomeTestFixtures.Spine(3);
            genome.AddPart(PartGene.Default(GenomeTestFixtures.FinId, 2, mirrored: true));

            Assert.IsTrue(GenomeEditOperations.TryExtendSpine(genome, atHead: false, _rules, out GenomeError error), error.ToString());

            Assert.AreEqual(3, genome.GetPart(0).BoneIndex, "The fin did not travel to the new end of the tail.");
        }

        // --- Shortening the spine ---

        [Test]
        public void ShrinkingAtTheTail_RemovesTheLastVertebra()
        {
            CreatureGenome genome = GenomeTestFixtures.Spine(4);

            Assert.IsTrue(GenomeEditOperations.TryShrinkSpine(genome, atHead: false, _rules, out GenomeError error), error.ToString());
            Assert.AreEqual(3, genome.VertebraCount);
        }

        [Test]
        public void ShrinkingAtTheHead_KeepsTheFaceOnTheNewHead()
        {
            CreatureGenome genome = GenomeTestFixtures.Spine(3);
            genome.AddPart(PartGene.Default(GenomeTestFixtures.EyeId, 0, mirrored: true));

            Assert.IsTrue(GenomeEditOperations.TryShrinkSpine(genome, atHead: true, _rules, out GenomeError error), error.ToString());

            Assert.AreEqual(2, genome.VertebraCount);
            Assert.AreEqual(1, genome.PartCount, "Removing a vertebra ate the eye instead of transplanting it onto the new head.");
            Assert.AreEqual(0, genome.GetPart(0).BoneIndex);
        }

        [Test]
        public void ShrinkingAtTheTail_MovesTailPartsOntoTheNewTail()
        {
            CreatureGenome genome = GenomeTestFixtures.Spine(4);
            genome.AddPart(PartGene.Default(GenomeTestFixtures.FinId, 3, mirrored: true));

            Assert.IsTrue(GenomeEditOperations.TryShrinkSpine(genome, atHead: false, _rules, out GenomeError error), error.ToString());

            Assert.AreEqual(2, genome.GetPart(0).BoneIndex, "The fin did not move across to the new tail.");
        }

        [Test]
        public void Shrinking_StopsAtTheMinimumVertebraCount()
        {
            CreatureGenome genome = GenomeTestFixtures.Spine(GenomeLimits.MinVertebrae);

            Assert.IsFalse(GenomeEditOperations.TryShrinkSpine(genome, atHead: false, _rules, out GenomeError error));
            Assert.AreEqual(GenomeError.TooFewVertebrae, error);
            Assert.AreEqual(GenomeLimits.MinVertebrae, genome.VertebraCount);
        }

        /// <summary>
        /// The position of the tail tip in the <b>raw</b> chain, with no anchoring.
        /// </summary>
        /// <remarks>
        /// We deliberately do not use <c>SpineAnchor.BoneToRoot</c>: anchoring recentres the body, so
        /// any change in length shifts the whole frame and there would be no way to tell "the tail
        /// moved" apart from "the body recentred".
        /// </remarks>
        private static Vector3 TipPosition(CreatureGenome genome)
        {
            Matrix4x4 accumulated = Matrix4x4.identity;
            for (int i = 0; i < genome.VertebraCount; i++)
            {
                VertebraGene gene = genome.GetVertebra(i);
                accumulated *= Matrix4x4.TRS(gene.LocalOffset, gene.LocalRotation, Vector3.one);
            }

            return accumulated.GetColumn(3);
        }

        [Test]
        public void SetVertebraRadius_OutOfRangeIndex_Fails()
        {
            CreatureGenome genome = GenomeTestFixtures.Valid();

            Assert.IsFalse(GenomeEditOperations.TrySetVertebraRadius(genome, 42, 0.3f, out GenomeError error));
            Assert.AreEqual(GenomeError.VertebraIndexOutOfRange, error);
        }

        // --- Moving parts ---

        [Test]
        public void SetPartLocalPosition_MovesPartWithoutChangingItsBone()
        {
            CreatureGenome genome = GenomeTestFixtures.Valid();
            byte boneBefore = genome.GetPart(0).BoneIndex;
            var target = new Vector3(0.1f, 0.2f, -0.05f);

            Assert.IsTrue(GenomeEditOperations.TrySetPartLocalPosition(genome, 0, target, out GenomeError error), error.ToString());

            Vector3 stored = genome.GetPart(0).LocalPosition;

            Assert.AreEqual(0.3f, Vector3.ProjectOnPlane(stored, Vector3.forward).magnitude, 0.001f,
                "The gene has to hold a point lying on the skin — otherwise the drag runs at a different scale than the cursor.");
            Assert.AreEqual(boneBefore, genome.GetPart(0).BoneIndex,
                "Moving a part must not change the vertebra it is assigned to.");
            Assert.IsTrue(GenomeEditOperations.ValidateStructure(genome, _rules).IsValid);
        }

        /// <summary>
        /// The write has to be idempotent: a point taken off the skin and fed back in gives itself.
        /// Otherwise every further edit would nudge the part a little.
        /// </summary>
        [Test]
        public void SetPartLocalPosition_IsIdempotent()
        {
            CreatureGenome genome = GenomeTestFixtures.Valid();

            Assert.IsTrue(GenomeEditOperations.TrySetPartLocalPosition(genome, 0, new Vector3(0.1f, 0.2f, -0.05f), out _));
            Vector3 once = genome.GetPart(0).LocalPosition;

            Assert.IsTrue(GenomeEditOperations.TrySetPartLocalPosition(genome, 0, once, out _));

            Assert.AreEqual(0f, Vector3.Distance(once, genome.GetPart(0).LocalPosition), 0.0001f);
        }

        [Test]
        public void SetPartLocalPosition_FarOutside_PullsThePartBackOntoTheSkin()
        {
            CreatureGenome genome = GenomeTestFixtures.Valid();

            Assert.IsTrue(GenomeEditOperations.TrySetPartLocalPosition(genome, 0, new Vector3(0f, 99f, 0f), out _));

            Assert.AreEqual(0.3f, genome.GetPart(0).LocalPosition.magnitude, 0.001f,
                "A part dragged far outside the body should return to the skin, not stop at the offset limit.");
        }

        /// <summary>
        /// Dragging along the body has to hand over from vertebra to vertebra — one vertebra's skin
        /// ends halfway to its neighbour.
        /// </summary>
        [Test]
        public void SetPartBone_MovesThePartOntoTheNewVertebraSkin()
        {
            CreatureGenome genome = GenomeTestFixtures.Valid(); // part 0 is the legs on vertebra 1

            Assert.IsTrue(GenomeEditOperations.TrySetPartBone(genome, 0, 2, new Vector3(0f, -0.9f, 0f), _rules, out GenomeError error),
                error.ToString());

            Assert.AreEqual(2, genome.GetPart(0).BoneIndex);
            Assert.AreEqual(0.3f, genome.GetPart(0).LocalPosition.magnitude, 0.001f,
                "The part should land on the new vertebra's skin.");
        }

        [Test]
        public void SetPartBone_RefusesASiteTheRuleDoesNotAllow()
        {
            CreatureGenome genome = GenomeTestFixtures.Valid(); // part 1 is the jaw on the head

            Assert.IsFalse(GenomeEditOperations.TrySetPartBone(genome, 1, 1, Vector3.up, _rules, out GenomeError error));

            Assert.AreEqual(GenomeError.InvalidAttachmentSite, error);
            Assert.AreEqual(0, genome.GetPart(1).BoneIndex, "A rejected transfer must not move the part.");
        }

        [Test]
        public void SetPartLocalPosition_OutOfRangeIndex_Fails()
        {
            CreatureGenome genome = GenomeTestFixtures.Valid();

            Assert.IsFalse(GenomeEditOperations.TrySetPartLocalPosition(genome, 42, Vector3.zero, out GenomeError error));
            Assert.AreEqual(GenomeError.PartIndexOutOfRange, error);
        }

        // --- Part rotation and symmetry ---

        [Test]
        public void SetPartRotation_StoresTheCorrectionOnly()
        {
            CreatureGenome genome = GenomeTestFixtures.Valid();
            Quaternion correction = Quaternion.AngleAxis(45f, Vector3.up);

            Assert.IsTrue(GenomeEditOperations.TrySetPartRotation(genome, 0, correction, out GenomeError error), error.ToString());

            Assert.AreEqual(0f, Quaternion.Angle(correction, genome.GetPart(0).LocalRotation), 1e-3f);
            Assert.IsTrue(GenomeEditOperations.ValidateStructure(genome, _rules).IsValid);
        }

        [Test]
        public void SetPartRotation_OutOfRangeIndex_Fails()
        {
            CreatureGenome genome = GenomeTestFixtures.Valid();

            Assert.IsFalse(GenomeEditOperations.TrySetPartRotation(genome, 42, Quaternion.identity, out GenomeError error));
            Assert.AreEqual(GenomeError.PartIndexOutOfRange, error);
        }

        [Test]
        public void SetPartMirrored_TogglesPairOnAndOff()
        {
            CreatureGenome genome = GenomeTestFixtures.Spine();
            genome.AddPart(PartGene.Default(GenomeTestFixtures.EyeId, 0, mirrored: false));

            Assert.IsTrue(GenomeEditOperations.TrySetPartMirrored(genome, 0, true, _rules, out GenomeError error), error.ToString());
            Assert.IsTrue(genome.GetPart(0).Mirrored, "Failed to switch on symmetry for a part that has a mirrored version.");

            Assert.IsTrue(GenomeEditOperations.TrySetPartMirrored(genome, 0, false, _rules, out error), error.ToString());
            Assert.IsFalse(genome.GetPart(0).Mirrored, "Failed to go back down to a single instance.");
        }

        [Test]
        public void SetPartMirrored_OnPartWithoutMirrorVersion_Fails()
        {
            CreatureGenome genome = GenomeTestFixtures.Spine();
            genome.AddPart(PartGene.Default(GenomeTestFixtures.JawId, 0, mirrored: false)); // the jaw has no pair

            Assert.IsFalse(GenomeEditOperations.TrySetPartMirrored(genome, 0, true, _rules, out GenomeError error));
            Assert.AreEqual(GenomeError.MirrorNotSupported, error);
            Assert.IsFalse(genome.GetPart(0).Mirrored);
        }

        [Test]
        public void SetPartMirrored_TurningOffNeverNeedsMirrorSupport()
        {
            // Switching symmetry off has to always be possible — otherwise a part with a mismatched
            // catalog would be locked into a pair forever.
            CreatureGenome genome = GenomeTestFixtures.Spine();
            genome.AddPart(new PartGene(GenomeTestFixtures.JawId, 0, Vector3.zero, Quaternion.identity, 1f, mirrored: true));

            Assert.IsTrue(GenomeEditOperations.TrySetPartMirrored(genome, 0, false, _rules, out GenomeError error), error.ToString());
            Assert.IsFalse(genome.GetPart(0).Mirrored);
        }

        private static float TotalSpineLength(CreatureGenome genome)
        {
            float total = 0f;
            for (int i = 1; i < genome.VertebraCount; i++)
                total += genome.GetVertebra(i).LocalOffset.magnitude;
            return total;
        }
    }
}
