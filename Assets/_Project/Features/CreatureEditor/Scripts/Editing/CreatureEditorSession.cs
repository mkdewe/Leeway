using System;
using Leeway.Creature.Domain;
using UnityEngine;

namespace Leeway.CreatureEditor
{
    /// <summary>
    /// The client-side sculpting session. It holds the working genome, exposes the edit operations and
    /// keeps the preview up to date.
    /// </summary>
    /// <remarks>
    /// All editing is local — <b>not a single packet goes out while sculpting</b>. Only "Apply" sends
    /// the complete genome for server-side validation. That is the heart of the commit model and the
    /// reason individual operations do not need versioning.
    /// </remarks>
    public class CreatureEditorSession : MonoBehaviour
    {
        [SerializeField] private CreatureBodyPreview _preview;

        private CreatureGenome _working;
        private CreatureGenome _baseline;
        private int _selectedVertebra = -1;

        public CreatureGenome Working => _working;
        public PartRuleSet Rules => _preview != null ? _preview.Rules : PartRuleSet.Empty;
        public int SelectedVertebra => _selectedVertebra;
        public bool HasUnappliedChanges { get; private set; }

        public event Action<CreatureGenome> WorkingGenomeChanged;
        public event Action<int> SelectionChanged;
        public event Action<GenomeError> EditRejected;

        /// <summary>Opens a session on a copy of the genome — the original stays untouched until it is applied.</summary>
        public void Begin(CreatureGenome source)
        {
            _baseline = source?.Clone();
            _working = source?.Clone();
            HasUnappliedChanges = false;

            Select(_working != null && _working.VertebraCount > 0 ? 0 : -1);
            PushToPreview();
        }

        /// <summary>Discards the changes and returns to the state the session opened in.</summary>
        public void Revert()
        {
            if (_baseline == null) return;

            _working = _baseline.Clone();
            HasUnappliedChanges = false;

            if (_selectedVertebra >= _working.VertebraCount)
                Select(_working.VertebraCount - 1);

            PushToPreview();
        }

        /// <summary>Records the genome the server accepted as the new reference point.</summary>
        public void MarkApplied()
        {
            _baseline = _working?.Clone();
            HasUnappliedChanges = false;
        }

        public void Select(int vertebraIndex)
        {
            if (_selectedVertebra == vertebraIndex) return;

            _selectedVertebra = vertebraIndex;
            SelectionChanged?.Invoke(vertebraIndex);
        }

        public bool AddVertebra() => Apply(GenomeEditOperations.TryAddVertebra(_working, ResolveTarget(), Rules, out GenomeError e), e);

        /// <summary>Lengthens the spine by a vertebra at the given end — driven by the arrows in the preview.</summary>
        public bool ExtendSpine(bool atHead)
            => Apply(GenomeEditOperations.TryExtendSpine(_working, atHead, Rules, out GenomeError e), e);

        /// <summary>
        /// Removes the end vertebra — the inverse of <see cref="ExtendSpine"/>, driven by dragging the
        /// same arrow towards the middle of the body.
        /// </summary>
        /// <remarks>
        /// Parts on the removed vertebra move onto the new end of the body — taking back a grown head
        /// must not cost the player their eyes and mouth.
        /// </remarks>
        public bool ShrinkSpine(bool atHead)
        {
            if (_working == null) return false;

            bool ok = Apply(GenomeEditOperations.TryShrinkSpine(_working, atHead, Rules, out GenomeError e), e);

            if (ok && _selectedVertebra >= _working.VertebraCount)
                Select(_working.VertebraCount - 1);

            return ok;
        }

        public bool RemoveSelectedVertebra()
        {
            int target = ResolveTarget();
            bool ok = Apply(GenomeEditOperations.TryRemoveVertebra(_working, target, Rules, out GenomeError e), e);

            if (ok && _selectedVertebra >= _working.VertebraCount)
                Select(_working.VertebraCount - 1);

            return ok;
        }

        public bool AttachPart(int partId, bool mirrored)
            => Apply(GenomeEditOperations.TryAttachPart(_working, ResolveTarget(), partId, mirrored, Rules, out GenomeError e), e);

        /// <summary>
        /// Attaches a part at a specific spot on the body — the entry point for a drop from the palette,
        /// where the vertebra and the direction come from the point under the cursor.
        /// </summary>
        public bool AttachPartAt(int partId, int boneIndex, Vector3 localPosition, bool mirrored)
        {
            if (_working == null || boneIndex < 0 || boneIndex >= _working.VertebraCount) return false;

            var gene = new PartGene(partId, (byte)boneIndex, localPosition, Quaternion.identity, 1f, mirrored);
            return Apply(GenomeEditOperations.TryAttachPart(_working, gene, Rules, out GenomeError e), e);
        }

        public bool DetachPart(int partIndex)
            => Apply(GenomeEditOperations.TryDetachPart(_working, partIndex, out GenomeError e), e);

        /// <summary>Moves a part relative to its bone — driven by dragging the part handle.</summary>
        public bool MovePart(int partIndex, Vector3 localPosition)
            => Apply(GenomeEditOperations.TrySetPartLocalPosition(_working, partIndex, localPosition, out GenomeError e), e);

        /// <summary>
        /// Moves a part onto a different vertebra — dragging along the body crosses from vertebra to
        /// vertebra instead of stopping at the edge of one vertebra's skin.
        /// </summary>
        public bool MovePartToBone(int partIndex, int boneIndex, Vector3 localPosition)
            => Apply(GenomeEditOperations.TrySetPartBone(_working, partIndex, boneIndex, localPosition, Rules, out GenomeError e), e);

        /// <summary>Sets a part's rotation correction — driven by the rotation gizmo.</summary>
        public bool RotatePart(int partIndex, Quaternion rotation)
            => Apply(GenomeEditOperations.TrySetPartRotation(_working, partIndex, rotation, out GenomeError e), e);

        /// <summary>Reshapes an attached part's leg — driven by the joint handles.</summary>
        public bool SetPartLeg(int partIndex, LegSpec leg)
            => Apply(GenomeEditOperations.TrySetPartLeg(_working, partIndex, leg, Rules, out GenomeError e), e);

        /// <summary>Toggles a part between a single piece and a mirrored pair.</summary>
        public bool SetPartMirrored(int partIndex, bool mirrored)
            => Apply(GenomeEditOperations.TrySetPartMirrored(_working, partIndex, mirrored, Rules, out GenomeError e), e);

        public bool SetSelectedOffset(Vector3 offset)
            => Apply(GenomeEditOperations.TrySetVertebraOffset(_working, ResolveTarget(), offset, out GenomeError e), e);

        public bool SetSelectedRadius(float radius)
            => Apply(GenomeEditOperations.TrySetVertebraRadius(_working, ResolveTarget(), radius, out GenomeError e), e);

        /// <summary>Grows a specific vertebra by index — used by the scroll wheel over a hovered bone, independently of the click selection.</summary>
        public bool SetVertebraRadius(int vertebraIndex, float radius)
            => Apply(GenomeEditOperations.TrySetVertebraRadius(_working, vertebraIndex, radius, out GenomeError e), e);

        /// <summary>Checks the genome locally before sending — fast feedback instead of waiting for a rejection from the server.</summary>
        public GenomeValidationResult ValidateForCommit() => GenomeValidator.Validate(_working, Rules);

        private int ResolveTarget() => _selectedVertebra >= 0 ? _selectedVertebra : Mathf.Max(0, (_working?.VertebraCount ?? 0) - 1);

        private bool Apply(bool succeeded, GenomeError error)
        {
            if (!succeeded)
            {
                EditRejected?.Invoke(error);
                return false;
            }

            HasUnappliedChanges = true;
            PushToPreview();
            return true;
        }

        /// <summary>
        /// A full preview rebuild on every edit. At ~400 vertices it is free, and it removes a whole
        /// class of incremental-update bugs.
        /// </summary>
        private void PushToPreview()
        {
            if (_preview != null) _preview.SetGenome(_working);
            WorkingGenomeChanged?.Invoke(_working);
        }
    }
}
