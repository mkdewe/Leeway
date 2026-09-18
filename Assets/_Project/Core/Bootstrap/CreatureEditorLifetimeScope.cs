using VContainer;
using VContainer.Unity;

namespace Leeway.Core
{
    /// <summary>
    /// Bootstrap for the creature editor scene. It stands up the MessagePipe bus used for loose
    /// communication between systems (player → camera/HUD).
    /// </summary>
    /// <remarks>
    /// The full set of brokers lives in <see cref="LeewayMessageBrokers"/> — shared by every phase,
    /// because the bus is stood up by whichever scene loads first. The details are with that class.
    /// </remarks>
    public class CreatureEditorLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder) => LeewayMessageBrokers.TryInstall();
    }
}
