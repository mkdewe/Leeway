using Leeway.Creature;
using MessagePipe;
using VContainer;
using VContainer.Unity;

namespace Leeway.Core
{
    /// <summary>
    /// Bootstrap fazy komórki. Inicjalizuje magistralę MessagePipe używaną do
    /// luźnej komunikacji między systemami (gracz → kamera/HUD).
    ///
    /// Używamy <see cref="GlobalMessagePipe"/>, bo obiekty sieciowe są tworzone
    /// w runtime przez FishNet i nie przechodzą przez wstrzykiwanie VContainera —
    /// to oficjalna ścieżka MessagePipe dla takich przypadków. Po dodaniu pakietu
    /// MessagePipe.VContainer można przejść na pełne DI (RegisterMessagePipe).
    /// </summary>
    public class CellPhaseLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            if (GlobalMessagePipe.IsInitialized) return;

            var messagePipe = new BuiltinContainerBuilder();
            messagePipe.AddMessagePipe();
            messagePipe.AddMessageBroker<LocalCellChangedMessage>();
            GlobalMessagePipe.SetProvider(messagePipe.BuildServiceProvider());
        }
    }
}
