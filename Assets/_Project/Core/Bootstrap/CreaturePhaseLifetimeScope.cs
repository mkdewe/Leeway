using VContainer;
using VContainer.Unity;

namespace Leeway.Core
{
    /// <summary>
    /// Bootstrap for the creature phase. It initialises the MessagePipe bus used for loose
    /// communication between systems (player → camera/HUD), just as
    /// <see cref="CellPhaseLifetimeScope"/> does.
    /// </summary>
    public class CreaturePhaseLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder) => LeewayMessageBrokers.TryInstall();
    }
}
