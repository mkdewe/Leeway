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

        /// <summary>
        /// Puts a <b>different</b> creature on the bench — a preset, or one of the built-in showcases.
        /// </summary>
        /// <remarks>
        /// <para>Deliberately not <see cref="Begin"/>. Beginning a session means "this is the creature
        /// that is already in the game", and it therefore declares nothing to apply. Loading a preset
        /// is the opposite: the game still has the old creature, and everything about this one is an
        /// unapplied change. Going through <c>Begin</c> made Apply decide there was nothing to commit,
        /// so the player chose a creature, pressed Apply, and watched their previous one spawn.</para>
        ///
        /// <para>The <b>baseline stays put</b> for the same reason: Revert should hand back the
        /// creature that is in the game, not the preset that was being tried on.</para>
        /// </remarks>
        public void Load(CreatureGenome source)
        {
            if (source == null) return;

            _working = source.Clone();
            HasUnappliedChanges = true;

            Select(_working.VertebraCount > 0 ? 0 : -1);
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

            // A limb arrives with its default foot already in it. Dragging a leg out of the palette and
            // getting a creature on stumps would read as a bug, and the player can swap the foot for
            // another the moment they want to.
            int fitting = Rules.TryGetRule(partId, out PartRule rule) && rule.AcceptsFitting ? rule.DefaultFittingId : 0;

            var gene = new PartGene(partId, (byte)boneIndex, localPosition, Quaternion.identity, 1f, mirrored,
                false, default, default, 0, fitting);

            return Apply(GenomeEditOperations.TryAttachPart(_working, gene, Rules, out GenomeError e), e);
        }

        /// <summary>Fits a foot or a hand into a limb already on the creature. Zero takes it out again.</summary>
        public bool FitPart(int partIndex, int fittingId)
            => Apply(GenomeEditOperations.TrySetPartFitting(_working, partIndex, fittingId, Rules, out GenomeError e), e);

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
            => Apply(GenomeEditOperations.TrySetPartRotation(_working, partIndex, rotation, Rules, out GenomeError e), e);

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

        /// <summary>Paints one part. Both sides of a mirrored pair change together — they are one gene.</summary>
        public bool PaintPart(int partIndex, Color32 tint)
            => Apply(GenomeEditOperations.TrySetPartTint(_working, partIndex, tint, out GenomeError e), e);

        /// <summary>Puts a coat pattern on one part.</summary>
        public bool SetPartPattern(int partIndex, byte patternId)
            => Apply(GenomeEditOperations.TrySetPartPattern(_working, partIndex, patternId, out GenomeError e), e);

        /// <summary>Paints the skin — the whole carcass, which is one mesh.</summary>
        public bool PaintBody(Color32 tint)
            => Apply(GenomeEditOperations.TrySetBodyTint(_working, tint, out GenomeError e), e);

        /// <summary>Puts a coat pattern on the skin.</summary>
        public bool SetBodyPattern(byte patternId)
            => Apply(GenomeEditOperations.TrySetBodyPattern(_working, patternId, out GenomeError e), e);

        /// <summary>Sets the colour every unpainted part follows.</summary>
        public bool PaintParts(Color32 tint)
            => Apply(GenomeEditOperations.TrySetSecondaryTint(_working, tint, out GenomeError e), e);

        /// <summary>Checks the genome locally before sending — fast feedback instead of waiting for a rejection from the server.</summary>
        public GenomeValidationResult ValidateForCommit() => GenomeValidator.Validate(_working, Rules);

        /// <summary>A copy of what is being sculpted — what a preset is saved from.</summary>
        /// <remarks>A copy, not the working genome: whoever saves it must not be able to edit the session through it.</remarks>
        public CreatureGenome Capture() => _working?.Clone();

        /// <summary>
        /// Replaces what is being sculpted with a saved creature.
        /// </summary>
        /// <remarks>
        /// <para>Through the same gate as everything else: a preset is validated against <b>this</b>
        /// build's rules before it is let in. A file can carry a part that no longer exists, a spine
        /// longer than the limit, or a body nobody could afford — and the first the player should hear
        /// of it is here, not from the server rejecting the commit afterwards.</para>
        ///
        /// <para>It lands as an unapplied change, like any other edit: loading a preset sculpts,
        /// it does not commit. "Revert" still goes back to the creature the session opened on.</para>
        /// </remarks>
        public bool LoadPreset(CreatureGenome preset)
        {
            if (preset == null)
            {
                EditRejected?.Invoke(GenomeError.NullGenome);
                return false;
            }

            GenomeValidationResult result = GenomeValidator.Validate(preset, Rules);
            if (!result.IsValid)
            {
                EditRejected?.Invoke(result.Error);
                return false;
            }

            _working = preset.Clone();
            HasUnappliedChanges = true;

            Select(_working.VertebraCount > 0 ? Mathf.Min(Mathf.Max(_selectedVertebra, 0), _working.VertebraCount - 1) : -1);
            PushToPreview();

            return true;
        }

        /// <summary>Loads a preset asset shipped with the game.</summary>
        public bool LoadPreset(CreaturePreset preset) => preset != null && LoadPreset(preset.ToGenome());

        /// <summary>
        /// Saves what is being sculpted as one of the player's presets.
        /// </summary>
        /// <remarks>
        /// The genome is saved as it stands, valid or not: sculpting is allowed to pass through states
        /// the rules would reject, and a save that refused them would lose work over a body the player
        /// was still halfway through building. Loading is where the rules apply.
        /// </remarks>
        public bool SavePreset(string name, out string error)
            => CreaturePresetStore.TrySave(name, _working, KeyResolver(), SkinPng(), out error);

        /// <summary>
        /// Loads one of the player's presets by name, paintwork and all.
        /// </summary>
        /// <remarks>
        /// The order is forced: the genome goes in first, which rebuilds the body and hangs a fresh
        /// canvas on the new renderer — and only then can the saved skin be laid over it. Restoring the
        /// texture first would paint a carcass that is about to be thrown away.
        /// </remarks>
        public bool LoadPreset(string name, out string error)
        {
            if (!CreaturePresetStore.TryLoadDocument(name, out GenomeDocument document, out error)) return false;

            if (!LoadPreset(document.ToGenome()))
            {
                error = $"\"{name}\" does not fit this build's rules — it was made with a different part catalog.";
                return false;
            }

            byte[] skin = CreaturePresetStore.SkinOf(document);
            if (skin != null) SkinCanvas?.LoadBundle(skin);

            return true;
        }

        /// <summary>The painted skin, when there is a canvas to take it from.</summary>
        private byte[] SkinPng() => SkinCanvas != null ? SkinCanvas.ToBundle() : null;

        private CreatureSkinCanvas SkinCanvas => _preview != null ? _preview.SkinCanvas : null;

        /// <summary>Names the parts in a saved file, when the preview knows the catalog they come from.</summary>
        private Func<int, string> KeyResolver()
        {
            CreaturePartCatalog catalog = _preview != null ? _preview.Catalog : null;
            return catalog != null ? catalog.KeyOf : null;
        }

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
