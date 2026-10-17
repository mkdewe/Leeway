using Leeway.Network.Netcode;
using Leeway.Network.Online;
using Leeway.Network.Session;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Leeway.Core
{
    /// <summary>
    /// The root scope: services that live for the whole run, across every scene — the online
    /// platform, the network connection and the session that ties them together.
    /// </summary>
    /// <remarks>
    /// <para>Installed as VContainer's <c>RootLifetimeScope</c> (VContainerSettings), so it is created
    /// before the first scene and every scene scope becomes its child automatically. That is what lets
    /// the session survive the menu → match → menu round trip without a singleton.</para>
    ///
    /// <para>It also stands up the MessagePipe bus first, so the session can publish from the very
    /// first frame regardless of which scene was opened in the editor.</para>
    /// </remarks>
    public class ProjectLifetimeScope : LifetimeScope
    {
        [SerializeField] private NetworkSessionSettings _networkSettings;

        protected override void Configure(IContainerBuilder builder)
        {
            LeewayMessageBrokers.TryInstall();

            if (_networkSettings == null)
            {
                Debug.LogError("[ProjectLifetimeScope] Network session settings are not assigned — online features are disabled.", this);
                return;
            }

            builder.RegisterInstance(_networkSettings);

            builder.RegisterEntryPoint<OnlinePlatformService>().As<IOnlinePlatformAccessor>();
            builder.Register<FishNetConnector>(Lifetime.Singleton).As<INetcodeConnector>();
            builder.RegisterEntryPoint<GameSession>().As<IGameSession>();
        }
    }
}
