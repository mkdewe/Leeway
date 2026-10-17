using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using UnityEngine;
using UnityServicesApi = Unity.Services.Core.UnityServices;

namespace Leeway.Online.UnityServices
{
    /// <summary>
    /// Unity Gaming Services: anonymous Authentication for identity and the Lobby service for lobbies.
    /// </summary>
    /// <remarks>
    /// <para>The project must be linked to a Unity Cloud project (Project Settings → Services) with
    /// Lobby enabled in the Dashboard. Without that, initialization fails and the bootstrapper moves
    /// on to the next platform.</para>
    ///
    /// <para>Game traffic is not relayed: hosts publish their LAN/direct endpoints as on any other
    /// backend. That covers several instances on one machine and players on one network; across the
    /// internet it needs a reachable host (or a Relay-capable transport adapter).</para>
    /// </remarks>
    public sealed class UgsOnlinePlatform : IOnlinePlatform
    {
        private readonly UgsPlatformOptions _options;
        private UgsLobbyService _lobbies;
        private bool _subscribedToAuth;

        public UgsOnlinePlatform(UgsPlatformOptions options) => _options = options ?? throw new ArgumentNullException(nameof(options));

        public PlatformId Id => PlatformId.UnityGamingServices;
        public string DisplayName => "Unity Gaming Services";

        public OnlineCapabilities Capabilities =>
            OnlineCapabilities.Lobbies | OnlineCapabilities.RemoteLobbies | OnlineCapabilities.LobbySearch |
            OnlineCapabilities.MemberData | OnlineCapabilities.JoinByCode;

        public PlatformStatus Status { get; private set; } = PlatformStatus.NotInitialized;
        public UserProfile LocalUser { get; private set; } = UserProfile.Anonymous;
        public ILobbyService Lobbies => (ILobbyService)_lobbies ?? NullLobbyService.Instance;
        public ISocialService Social => NullSocialService.Instance;

        public event Action<OnlineError> Disconnected;

        public async UniTask<OnlineResult> InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (Status == PlatformStatus.Ready) return OnlineResult.Success;
            if (Status != PlatformStatus.NotInitialized)
                return OnlineResult.Failure(OnlineErrorCode.InvalidArgument, $"UGS platform cannot initialize from state {Status}.");

            Status = PlatformStatus.Initializing;
            OnlineResult result;
            try
            {
                result = await InitializeCoreAsync(cancellationToken).Timeout(_options.InitializeTimeout);
            }
            catch (Exception exception)
            {
                result = OnlineResult.Failure(UgsCalls.ToError(exception, "UGS initialization"));
            }

            Status = result.IsSuccess ? PlatformStatus.Ready : PlatformStatus.Failed;
            return result;
        }

        private async UniTask<OnlineResult> InitializeCoreAsync(CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(Application.cloudProjectId))
                return OnlineResult.Failure(OnlineErrorCode.PlatformUnavailable, "The project is not linked to a Unity Cloud project (Project Settings → Services).");

            // With domain reload off the SDK survives between play sessions; reuse it as it is.
            if (UnityServicesApi.State == ServicesInitializationState.Uninitialized)
            {
                var init = new InitializationOptions();
                if (_options.Profile != null) init.SetProfile(_options.Profile);
                if (_options.Environment != null) init.SetEnvironmentName(_options.Environment);

                var started = await UgsCalls.Run(UnityServicesApi.InitializeAsync(init), "UGS initialization", cancellationToken);
                if (!started.IsSuccess) return started;
            }
            else
            {
                await UniTask.WaitUntil(() => UnityServicesApi.State != ServicesInitializationState.Initializing, cancellationToken: cancellationToken);
                if (UnityServicesApi.State != ServicesInitializationState.Initialized)
                    return OnlineResult.Failure(OnlineErrorCode.PlatformUnavailable, "Unity Services failed to initialize earlier in this session.");
            }

            var auth = AuthenticationService.Instance;
            if (!auth.IsSignedIn)
            {
                if (_options.Profile != null && auth.Profile != _options.Profile) auth.SwitchProfile(_options.Profile);

                var signIn = await UgsCalls.Run(auth.SignInAnonymouslyAsync(), "UGS sign-in", cancellationToken);
                if (!signIn.IsSuccess) return signIn;
            }

            var name = await UgsCalls.Run(auth.GetPlayerNameAsync(autoGenerate: true), "UGS player name", cancellationToken);
            var displayName = name.IsSuccess && !string.IsNullOrWhiteSpace(name.Value)
                ? name.Value
                : "Player-" + auth.PlayerId.Substring(0, Math.Min(4, auth.PlayerId.Length));

            LocalUser = new UserProfile(new UserId(auth.PlayerId), displayName);
            _lobbies = new UgsLobbyService(LocalUser, _options.Index);

            auth.Expired += OnSessionExpired;
            auth.SignedOut += OnSignedOut;
            _subscribedToAuth = true;

            Debug.Log($"[Online] UGS signed in as {LocalUser} (profile '{auth.Profile}', environment '{_options.Environment ?? "default"}').");
            return OnlineResult.Success;
        }

        private void OnSessionExpired() =>
            Disconnected?.Invoke(new OnlineError(OnlineErrorCode.ConnectionFailed, "The Unity Gaming Services session expired."));

        private void OnSignedOut()
        {
            if (Status == PlatformStatus.Ready)
                Disconnected?.Invoke(new OnlineError(OnlineErrorCode.NotInitialized, "Signed out of Unity Gaming Services."));
        }

        public void Dispose()
        {
            if (Status == PlatformStatus.ShutDown) return;
            Status = PlatformStatus.ShutDown;

            _lobbies?.Dispose();
            _lobbies = null;

            if (_subscribedToAuth)
            {
                // Signed in stays signed in: the session token is kept so the next run is the same player.
                var auth = AuthenticationService.Instance;
                auth.Expired -= OnSessionExpired;
                auth.SignedOut -= OnSignedOut;
                _subscribedToAuth = false;
            }
        }
    }
}
