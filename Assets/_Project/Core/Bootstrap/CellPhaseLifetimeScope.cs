using VContainer;
using VContainer.Unity;

namespace Leeway.Core
{
    /// <summary>
    /// Bootstrap for the cell phase. It initialises the MessagePipe bus used for loose communication
    /// between systems (player → camera/HUD).
    ///
    /// We use <see cref="GlobalMessagePipe"/> because networked objects are created at runtime by
    /// FishNet and never go through VContainer's injection — this is MessagePipe's official route for
    /// such cases. Once the MessagePipe.VContainer package is added, we can move to full DI
    /// (RegisterMessagePipe).
    /// </summary>
    public class CellPhaseLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder) => LeewayMessageBrokers.TryInstall();
    }
}
