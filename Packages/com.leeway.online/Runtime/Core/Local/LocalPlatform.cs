using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Leeway.Online.Local
{
    /// <summary>
    /// An in-process backend: real lobby semantics (ownership, member data, chat) with no network
    /// service behind them. Use it to develop the menus without a Steam client, and in tests.
    /// </summary>
    public sealed class LocalPlatform : IOnlinePlatform
    {
        private readonly LocalLobbyHub _hub;
        private readonly string _displayName;
        private LocalLobbyService _lobbies;

        public LocalPlatform(LocalLobbyHub hub, string displayName)
        {
            _hub = hub ?? throw new ArgumentNullException(nameof(hub));
            _displayName = string.IsNullOrWhiteSpace(displayName) ? Environment.UserName : displayName;
        }

        public PlatformId Id => PlatformId.Local;
        public string DisplayName => "Local";

        public OnlineCapabilities Capabilities =>
            OnlineCapabilities.Lobbies | OnlineCapabilities.LobbySearch | OnlineCapabilities.LobbyChat |
            OnlineCapabilities.MemberData | OnlineCapabilities.JoinByCode;

        public PlatformStatus Status { get; private set; } = PlatformStatus.NotInitialized;
        public UserProfile LocalUser { get; private set; } = UserProfile.Anonymous;
        public ILobbyService Lobbies => (ILobbyService)_lobbies ?? NullLobbyService.Instance;
        public ISocialService Social => NullSocialService.Instance;

        public event Action<OnlineError> Disconnected { add { } remove { } }

        public UniTask<OnlineResult> InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (Status == PlatformStatus.Ready) return UniTask.FromResult(OnlineResult.Success);

            LocalUser = new UserProfile(new UserId("local-" + Guid.NewGuid().ToString("N").Substring(0, 8)), _displayName);
            _lobbies = new LocalLobbyService(_hub, this);
            Status = PlatformStatus.Ready;
            return UniTask.FromResult(OnlineResult.Success);
        }

        /// <summary>Test/debug hook: pretend the platform overlay asked to join a lobby.</summary>
        public void SimulateInvite(LobbyId lobbyId, UserId from) => _lobbies?.SimulateJoinRequest(lobbyId, from);

        public void Dispose()
        {
            _lobbies?.Leave();
            Status = PlatformStatus.ShutDown;
        }
    }
}
