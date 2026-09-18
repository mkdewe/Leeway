using Leeway.Creature.Domain;

namespace Leeway.CreatureEditor
{
    /// <summary>
    /// Published when the local player's creature in the playground changes
    /// (spawn → != null, despawn → null). It lets the camera and the HUD react without depending
    /// directly on the controller.
    /// </summary>
    public readonly struct LocalCreatureBodyChangedMessage
    {
        public readonly CreatureBody Body;
        public LocalCreatureBodyChangedMessage(CreatureBody body) => Body = body;
    }

    /// <summary>Published when the editor opens or closes a sculpting session.</summary>
    public readonly struct CreatureEditorSessionChangedMessage
    {
        public readonly CreatureEditorSession Session;
        public CreatureEditorSessionChangedMessage(CreatureEditorSession session) => Session = session;
    }

    /// <summary>
    /// Published when the editor moves between sculpting and controlling the creature.
    /// </summary>
    /// <remarks>
    /// It exists so the sculpting UI can be switched off without the controller knowing which panels
    /// lie on the canvas — the controller announces the mode and everyone interested decides for
    /// themselves what to do with it.
    /// </remarks>
    public readonly struct CreatureEditorModeChangedMessage
    {
        public readonly CreatureEditorMode Mode;
        public CreatureEditorModeChangedMessage(CreatureEditorMode mode) => Mode = mode;

        /// <summary>Whether the player is sculpting — the only question the UI actually asks.</summary>
        public bool IsSculpting => Mode == CreatureEditorMode.Sculpt;
    }

    /// <summary>Published when the selected vertebra changes. -1 means no selection.</summary>
    public readonly struct CreatureEditorSelectionChangedMessage
    {
        public readonly int VertebraIndex;
        public CreatureEditorSelectionChangedMessage(int vertebraIndex) => VertebraIndex = vertebraIndex;
    }

    /// <summary>
    /// Published when the selected part changes. A <see cref="PartIndex"/> of -1 means no selection —
    /// the HUD then greys out the symmetry toggle.
    /// </summary>
    public readonly struct CreatureEditorPartSelectionChangedMessage
    {
        public readonly int PartIndex;
        public readonly bool Mirrored;

        /// <summary>Whether this part has a mirrored version at all — without that the symmetry toggle makes no sense.</summary>
        public readonly bool CanMirror;

        public CreatureEditorPartSelectionChangedMessage(int partIndex, bool mirrored, bool canMirror)
        {
            PartIndex = partIndex;
            Mirrored = mirrored;
            CanMirror = canMirror;
        }
    }

    /// <summary>The server's answer to a genome commit attempt, addressed to the owner.</summary>
    public readonly struct GenomeCommitResultMessage
    {
        public readonly bool Accepted;
        public readonly GenomeError Error;

        public GenomeCommitResultMessage(bool accepted, GenomeError error)
        {
            Accepted = accepted;
            Error = error;
        }
    }

    /// <summary>Published after every body rebuild — the HUD hangs its stat readout off this.</summary>
    public readonly struct CreatureBodyRebuiltMessage
    {
        public readonly CreatureBody Body;
        public readonly CreatureStats Stats;

        public CreatureBodyRebuiltMessage(CreatureBody body, CreatureStats stats)
        {
            Body = body;
            Stats = stats;
        }
    }
}
