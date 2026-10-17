using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using FishNet.Managing;
using FishNet.Managing.Scened;
using FishNet.Transporting;
using Leeway.Network.Online;
using Leeway.Network.Session;
using Leeway.Online;
using Leeway.Online.FishNet;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Leeway.Network.Netcode
{
    /// <summary>
    /// <see cref="INetcodeConnector"/> on FishNet. Owns the menu-spawned NetworkManager and turns its
    /// connection-state events into awaitable start/connect calls.
    /// </summary>
    /// <remarks>
    /// <para><b>Which NetworkManager.</b> The menu has none in its scene: one is spawned on first use
    /// from the prefab the settings list for the running platform (Steam needs FishySteamworks next to
    /// Tugboat; Offline does not). It is DontDestroyOnLoad, so the gameplay scene's own
    /// NetworkManager — kept there so phases can still be launched directly from the editor — destroys
    /// itself as the newer duplicate.</para>
    ///
    /// <para><b>Transports.</b> Which transport dials which endpoint is decided by the
    /// <see cref="TransportRouter"/> built from the settings' adapters.</para>
    /// </remarks>
    public sealed class FishNetConnector : INetcodeConnector, IDisposable
    {
        private readonly NetworkSessionSettings _settings;
        private readonly IOnlinePlatformAccessor _online;
        private readonly TransportRouter _router;

        private NetworkManager _networkManager;
        private LocalConnectionState _serverState = LocalConnectionState.Stopped;
        private LocalConnectionState _clientState = LocalConnectionState.Stopped;
        private bool _stopping;

        public FishNetConnector(NetworkSessionSettings settings, IOnlinePlatformAccessor online)
        {
            _settings = settings;
            _online = online;
            _router = new TransportRouter(settings.TransportAdapters);
        }

        public bool IsServerRunning => _serverState == LocalConnectionState.Started;
        public bool IsClientRunning => _clientState == LocalConnectionState.Started;

        public event Action ConnectionLost;

        public async UniTask<OnlineResult> StartHostAsync(int maxPlayers, CancellationToken cancellationToken)
        {
            var networkManager = EnsureNetworkManager();
            if (networkManager == null) return NoNetworkManager();

            _stopping = false;
            networkManager.TransportManager.Transport.SetMaximumClients(Mathf.Max(1, maxPlayers));

            if (!networkManager.ServerManager.StartConnection())
                return OnlineResult.Failure(OnlineErrorCode.ConnectionFailed, "The server transport refused to start.");

            var server = await WaitForStateAsync(() => _serverState, _settings.ConnectTimeout, cancellationToken);
            if (!server.IsSuccess)
            {
                Stop();
                return server;
            }

            if (!networkManager.ClientManager.StartConnection())
            {
                Stop();
                return OnlineResult.Failure(OnlineErrorCode.ConnectionFailed, "The host's local client refused to start.");
            }

            var client = await WaitForStateAsync(() => _clientState, _settings.ConnectTimeout, cancellationToken);
            if (!client.IsSuccess) Stop();
            return client;
        }

        public async UniTask<OnlineResult> ConnectAsync(IReadOnlyList<NetworkEndpoint> endpoints, TimeSpan timeout, CancellationToken cancellationToken)
        {
            var networkManager = EnsureNetworkManager();
            if (networkManager == null) return NoNetworkManager();

            var route = _router.ConfigureClient(networkManager, endpoints);
            if (!route.IsSuccess) return route.AsResult();

            Debug.Log($"[Netcode] Connecting to {route.Value}.");
            _stopping = false;

            if (!networkManager.ClientManager.StartConnection())
                return OnlineResult.Failure(OnlineErrorCode.ConnectionFailed, $"The client transport refused to dial {route.Value}.");

            var result = await WaitForStateAsync(() => _clientState, timeout, cancellationToken);
            if (!result.IsSuccess) Stop();
            return result;
        }

        public IReadOnlyList<NetworkEndpoint> GetHostEndpoints() => _router.GetHostEndpoints(_networkManager);

        public void LoadSceneForAll(string sceneName)
        {
            if (_networkManager == null || !IsServerRunning)
            {
                Debug.LogError("[Netcode] Cannot load a scene for everyone without a running server.");
                return;
            }

            _networkManager.SceneManager.LoadGlobalScenes(new SceneLoadData(sceneName) { ReplaceScenes = ReplaceOption.All });
        }

        public void Stop()
        {
            if (_networkManager == null) return;

            _stopping = true;
            if (_clientState != LocalConnectionState.Stopped) _networkManager.ClientManager.StopConnection();
            if (_serverState != LocalConnectionState.Stopped) _networkManager.ServerManager.StopConnection(true);
        }

        public void Dispose()
        {
            Stop();
            Unbind();
        }

        /// <summary>Waits until a connection leaves Starting. Started → success; Stopped, timeout or cancel → failure.</summary>
        private static async UniTask<OnlineResult> WaitForStateAsync(Func<LocalConnectionState> state, TimeSpan timeout, CancellationToken cancellationToken)
        {
            using var timeoutSource = new CancellationTokenSource();
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);
            // The slim timer outlives this method unless stopped; disposed before the source (reverse
            // using order), so it never calls Cancel() on a disposed CancellationTokenSource.
            using var timer = timeoutSource.CancelAfterSlim(timeout);

            try
            {
                // A freshly started transport reports Stopped until its first state event arrives on
                // the next tick, so wait for it to begin before treating Stopped as a failure.
                await UniTask.WaitUntil(() => state() != LocalConnectionState.Stopped, cancellationToken: linked.Token)
                    .Timeout(TimeSpan.FromSeconds(2));
                await UniTask.WaitUntil(() => state() is LocalConnectionState.Started or LocalConnectionState.Stopped, cancellationToken: linked.Token);
            }
            catch (TimeoutException)
            {
                return OnlineResult.Failure(OnlineErrorCode.ConnectionFailed, "The transport never started.");
            }
            catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                return OnlineResult.Failure(OnlineErrorCode.Timeout, $"No answer within {timeout.TotalSeconds:0} s.");
            }
            catch (OperationCanceledException)
            {
                return OnlineResult.Failure(OnlineError.Cancelled);
            }

            return state() == LocalConnectionState.Started
                ? OnlineResult.Success
                : OnlineResult.Failure(OnlineErrorCode.ConnectionFailed, "The connection was refused or dropped while starting.");
        }

        private NetworkManager EnsureNetworkManager()
        {
            if (_networkManager != null) return _networkManager;

            // A scene-placed NetworkManager (a phase launched straight from the editor) is reused as is.
            // Instances, not InstanceFinder: the latter logs a warning whenever none exists, which in
            // the menu is the normal case.
            var existing = NetworkManager.Instances.Count > 0 ? NetworkManager.Instances[0] : null;
            if (existing == null)
            {
                var platform = _online.Platform?.Id ?? PlatformId.Offline;
                var prefab = _settings.GetNetworkManagerPrefab(platform);
                if (prefab == null) return null;

                existing = Object.Instantiate(prefab);
                existing.name = $"NetworkManager ({platform})";
            }

            Bind(existing);
            return _networkManager;
        }

        private void Bind(NetworkManager networkManager)
        {
            _networkManager = networkManager;
            _serverState = networkManager.ServerManager.Started ? LocalConnectionState.Started : LocalConnectionState.Stopped;
            _clientState = networkManager.ClientManager.Started ? LocalConnectionState.Started : LocalConnectionState.Stopped;
            networkManager.ServerManager.OnServerConnectionState += OnServerConnectionState;
            networkManager.ClientManager.OnClientConnectionState += OnClientConnectionState;
        }

        private void Unbind()
        {
            if (_networkManager == null) return;
            _networkManager.ServerManager.OnServerConnectionState -= OnServerConnectionState;
            _networkManager.ClientManager.OnClientConnectionState -= OnClientConnectionState;
            _networkManager = null;
        }

        private void OnServerConnectionState(ServerConnectionStateArgs args) => _serverState = args.ConnectionState;

        private void OnClientConnectionState(ClientConnectionStateArgs args)
        {
            var previous = _clientState;
            _clientState = args.ConnectionState;

            if (args.ConnectionState == LocalConnectionState.Stopped && previous == LocalConnectionState.Started && !_stopping)
                ConnectionLost?.Invoke();
        }

        private static OnlineResult NoNetworkManager() =>
            OnlineResult.Failure(OnlineErrorCode.ConnectionFailed, "No NetworkManager prefab is configured for this platform.");
    }
}
