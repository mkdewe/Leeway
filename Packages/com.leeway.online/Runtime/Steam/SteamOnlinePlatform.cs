using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Steamworks;
using UnityEngine;

namespace Leeway.Online.Steam
{
    /// <summary>Steam through Steamworks.NET: identity, lobbies (ISteamMatchmaking), the overlay and relay networking.</summary>
    /// <remarks>
    /// <para>Owns the whole Steam API lifetime: <c>SteamAPI.InitEx</c> on initialize, a player-loop
    /// pump for callbacks, <c>SteamAPI.Shutdown</c> on dispose. Nothing else in the game may call
    /// those — FishySteamworks included, which only uses the API once it is up.</para>
    ///
    /// <para>Outside a Steam launch (the editor, a build started by hand) Steam needs to be told which
    /// app this is. Instead of a <c>steam_appid.txt</c> next to the executable, the platform sets the
    /// <c>SteamAppId</c> environment variable for its own process — the file tends to leak into
    /// shipped builds.</para>
    /// </remarks>
    public sealed class SteamOnlinePlatform : IOnlinePlatform
    {
        private readonly SteamPlatformOptions _options;
        private SteamCallbackPump _pump;
        private SteamLobbyService _lobbies;
        private SteamSocialService _social;
        private Callback<SteamServersDisconnected_t> _serversDisconnected;

        public SteamOnlinePlatform(SteamPlatformOptions options) => _options = options;

        public PlatformId Id => PlatformId.Steam;
        public string DisplayName => "Steam";

        public OnlineCapabilities Capabilities =>
            OnlineCapabilities.Lobbies | OnlineCapabilities.RemoteLobbies | OnlineCapabilities.LobbySearch |
            OnlineCapabilities.LobbyChat | OnlineCapabilities.MemberData | OnlineCapabilities.InviteDialog |
            OnlineCapabilities.JoinByCode | OnlineCapabilities.RelayTransport;

        public PlatformStatus Status { get; private set; } = PlatformStatus.NotInitialized;
        public UserProfile LocalUser { get; private set; } = UserProfile.Anonymous;
        public ILobbyService Lobbies => (ILobbyService)_lobbies ?? NullLobbyService.Instance;
        public ISocialService Social => (ISocialService)_social ?? NullSocialService.Instance;

        public event Action<OnlineError> Disconnected;

        public UniTask<OnlineResult> InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (Status == PlatformStatus.Ready) return UniTask.FromResult(OnlineResult.Success);
            if (Status != PlatformStatus.NotInitialized)
                return UniTask.FromResult(OnlineResult.Failure(OnlineErrorCode.InvalidArgument, $"Steam platform cannot initialize from state {Status}."));

            Status = PlatformStatus.Initializing;
            var result = Initialize();
            Status = result.IsSuccess ? PlatformStatus.Ready : PlatformStatus.Failed;
            return UniTask.FromResult(result);
        }

        private OnlineResult Initialize()
        {
            if (!Packsize.Test())
                return OnlineResult.Failure(OnlineErrorCode.PlatformUnavailable, "Steamworks.NET was built for the wrong platform (Packsize test failed).");
            if (!DllCheck.Test())
                return OnlineResult.Failure(OnlineErrorCode.PlatformUnavailable, "steam_api library is missing or the wrong version.");

            if (_options.AppId == 0)
                return OnlineResult.Failure(OnlineErrorCode.InvalidArgument, "Steam App ID is not set.");

#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
            if (_options.RestartThroughSteamIfNecessary && SteamAPI.RestartAppIfNecessary(new AppId_t(_options.AppId)))
            {
                Application.Quit();
                return OnlineResult.Failure(OnlineErrorCode.PlatformUnavailable, "Relaunching through Steam.");
            }
#endif
            Environment.SetEnvironmentVariable("SteamAppId", _options.AppId.ToString());
            Environment.SetEnvironmentVariable("SteamGameId", _options.AppId.ToString());

            ESteamAPIInitResult init;
            string message;
            try
            {
                init = SteamAPI.InitEx(out message);
            }
            catch (DllNotFoundException exception)
            {
                return OnlineResult.Failure(OnlineErrorCode.PlatformUnavailable, $"steam_api library could not be loaded: {exception.Message}");
            }

            if (init != ESteamAPIInitResult.k_ESteamAPIInitResult_OK)
            {
                var code = init == ESteamAPIInitResult.k_ESteamAPIInitResult_VersionMismatch
                    ? OnlineErrorCode.VersionMismatch
                    : OnlineErrorCode.PlatformUnavailable;
                return OnlineResult.Failure(code, $"SteamAPI.InitEx failed ({init}): {message}");
            }

            _pump = SteamCallbackPump.Start();

            var steamId = SteamUser.GetSteamID();
            LocalUser = new UserProfile(steamId.ToUserId(), SteamFriends.GetPersonaName());

            _social = new SteamSocialService();
            _lobbies = new SteamLobbyService(steamId);
            _serversDisconnected = Callback<SteamServersDisconnected_t>.Create(e =>
                Disconnected?.Invoke(e.m_eResult.ToError("Steam connection")));

            return OnlineResult.Success;
        }

        public void Dispose()
        {
            if (Status == PlatformStatus.ShutDown) return;
            bool wasRunning = Status == PlatformStatus.Ready;
            Status = PlatformStatus.ShutDown;

            if (!wasRunning) return;

            _lobbies?.Dispose();
            _serversDisconnected?.Dispose();
            _pump?.Stop();
            SteamAPI.Shutdown();
        }
    }
}
