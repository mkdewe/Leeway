using Leeway.Creature;
using Leeway.Combat;
using Leeway.CreatureEditor;
using MessagePipe;

namespace Leeway.Core
{
    /// <summary>
    /// The single place where the whole game's MessagePipe brokers are registered.
    /// </summary>
    /// <remarks>
    /// <para><b>Why the full set rather than one phase's brokers:</b> every <c>LifetimeScope</c> bails
    /// out early on <c>GlobalMessagePipe.IsInitialized</c>, so <b>whichever scene loads first wins</b>.
    /// If each scope registered only its own messages, loading the editor additively after the cell
    /// phase would leave its brokers unregistered and the first <c>GetPublisher</c> would throw at
    /// runtime — a long way from where the cause lies.</para>
    ///
    /// <para>Registering the full set from every scope costs a few dictionaries at startup and removes
    /// that entire class of bugs, without introducing a separate bootstrap scene.</para>
    /// </remarks>
    public static class LeewayMessageBrokers
    {
        /// <summary>Returns false when the bus is already up — the caller should then do nothing.</summary>
        public static bool TryInstall()
        {
            if (GlobalMessagePipe.IsInitialized) return false;

            var builder = new BuiltinContainerBuilder();
            builder.AddMessagePipe();

            // Cell phase
            builder.AddMessageBroker<LocalCellChangedMessage>();

            // Creature phase
            builder.AddMessageBroker<LocalCreatureChangedMessage>();

            // Creature editor
            builder.AddMessageBroker<LocalCreatureBodyChangedMessage>();
            builder.AddMessageBroker<CreatureEditorSessionChangedMessage>();
            builder.AddMessageBroker<CreatureEditorModeChangedMessage>();
            builder.AddMessageBroker<CreatureEditorSelectionChangedMessage>();
            builder.AddMessageBroker<CreatureEditorPartSelectionChangedMessage>();
            builder.AddMessageBroker<GenomeCommitResultMessage>();
            builder.AddMessageBroker<CreatureBodyRebuiltMessage>();

            // Combat
            builder.AddMessageBroker<CreatureStruckMessage>();
            builder.AddMessageBroker<CreatureDiedMessage>();

            GlobalMessagePipe.SetProvider(builder.BuildServiceProvider());
            return true;
        }
    }
}
