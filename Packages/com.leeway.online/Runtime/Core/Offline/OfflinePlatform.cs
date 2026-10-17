using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Leeway.Online
{
    /// <summary>
    /// The null object at the end of every platform chain: no lobbies, no friends, an anonymous local
    /// user. Single-player and direct-IP play still work, because neither needs a platform.
    /// </summary>
    public sealed class OfflinePlatform : IOnlinePlatform
    {
        private readonly string _playerName;

        public OfflinePlatform(string playerName = null) => _playerName = playerName;

        public PlatformId Id => PlatformId.Offline;
        public string DisplayName => "Offline";
        public OnlineCapabilities Capabilities => OnlineCapabilities.None;
        public PlatformStatus Status { get; private set; } = PlatformStatus.NotInitialized;
        public UserProfile LocalUser { get; private set; } = UserProfile.Anonymous;
        public ILobbyService Lobbies => NullLobbyService.Instance;
        public ISocialService Social => NullSocialService.Instance;

        public event Action<OnlineError> Disconnected { add { } remove { } }

        public UniTask<OnlineResult> InitializeAsync(CancellationToken cancellationToken = default)
        {
            LocalUser = new UserProfile(new UserId("offline-" + Environment.UserName), _playerName ?? Environment.UserName);
            Status = PlatformStatus.Ready;
            return UniTask.FromResult(OnlineResult.Success);
        }

        public void Dispose() => Status = PlatformStatus.ShutDown;
    }
}
