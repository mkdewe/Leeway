using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Leeway.Online
{
    /// <summary>
    /// One online backend — Steam, EOS, PSN … — seen through a single facade.
    /// </summary>
    /// <remarks>
    /// <para>The facade is deliberately thin: identity, lobbies and social. Netcode stays out — the
    /// platform only hands out <see cref="NetworkEndpoint"/>s, and a netcode bridge (see
    /// <c>Leeway.Online.FishNet</c>) turns those into transport configuration.</para>
    ///
    /// <para>Services that a backend does not support are still present as null objects that fail
    /// with <see cref="OnlineErrorCode.NotSupported"/>, so callers never null-check — they check
    /// <see cref="Capabilities"/> when they want to hide UI, and the result when they act.</para>
    ///
    /// <para>All members are main-thread only.</para>
    /// </remarks>
    public interface IOnlinePlatform : IDisposable
    {
        PlatformId Id { get; }

        /// <summary>A human-readable name ("Steam", "Offline") for status labels.</summary>
        string DisplayName { get; }

        OnlineCapabilities Capabilities { get; }

        PlatformStatus Status { get; }

        /// <summary>The signed-in local user. <see cref="UserProfile.Anonymous"/> before initialization.</summary>
        UserProfile LocalUser { get; }

        ILobbyService Lobbies { get; }

        ISocialService Social { get; }

        /// <summary>Raised when the platform drops after a successful start (Steam client closed, signed out).</summary>
        event Action<OnlineError> Disconnected;

        /// <summary>
        /// Brings the backend up. Safe to call once; a failed platform should be disposed and the next
        /// candidate tried — see <see cref="OnlinePlatformBootstrapper"/>.
        /// </summary>
        UniTask<OnlineResult> InitializeAsync(CancellationToken cancellationToken = default);
    }
}
