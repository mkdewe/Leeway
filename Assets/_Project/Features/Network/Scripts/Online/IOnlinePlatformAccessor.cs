using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Leeway.Online;

namespace Leeway.Network.Online
{
    /// <summary>
    /// The one active <see cref="IOnlinePlatform"/>, available once it has started.
    /// </summary>
    /// <remarks>
    /// Consumers depend on this rather than on the platform itself because the platform is chosen at
    /// runtime, asynchronously — it does not exist yet when the container is built.
    /// </remarks>
    public interface IOnlinePlatformAccessor
    {
        /// <summary>The running platform, or null while it is still starting.</summary>
        IOnlinePlatform Platform { get; }

        bool IsReady { get; }

        /// <summary>Completes once a platform (possibly the Offline fallback) is running.</summary>
        UniTask<IOnlinePlatform> WhenReadyAsync(CancellationToken cancellationToken = default);

        event Action<IOnlinePlatform> Ready;
    }
}
