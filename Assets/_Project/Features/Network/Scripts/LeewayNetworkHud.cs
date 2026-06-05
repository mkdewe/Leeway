using FishNet;
using FishNet.Managing;
using FishNet.Transporting;
using UnityEngine;

namespace Leeway.Network
{
    /// <summary>
    /// Minimalny HUD sieciowy (Host / Server / Client / Stop) oparty na OnGUI.
    /// Zastępuje demo FishNet `NetworkHudCanvases`, które rzuca NRE przy aktywnym
    /// nowym Input System (niepodpięte _serverIndicator / _clientIndicator).
    /// </summary>
    public class LeewayNetworkHud : MonoBehaviour
    {
        [SerializeField] private bool _autoStartHostInEditor;

        private NetworkManager _networkManager;
        private LocalConnectionState _serverState = LocalConnectionState.Stopped;
        private LocalConnectionState _clientState = LocalConnectionState.Stopped;

        private void Start()
        {
            _networkManager = InstanceFinder.NetworkManager;
            if (_networkManager == null)
            {
                Debug.LogError("[LeewayNetworkHud] Nie znaleziono NetworkManager.");
                return;
            }

            _networkManager.ServerManager.OnServerConnectionState += OnServerState;
            _networkManager.ClientManager.OnClientConnectionState += OnClientState;

            if (_autoStartHostInEditor && Application.isEditor)
            {
                _networkManager.ServerManager.StartConnection();
                _networkManager.ClientManager.StartConnection();
            }
        }

        private void OnDestroy()
        {
            if (_networkManager == null) return;
            _networkManager.ServerManager.OnServerConnectionState -= OnServerState;
            _networkManager.ClientManager.OnClientConnectionState -= OnClientState;
        }

        private void OnServerState(ServerConnectionStateArgs args) => _serverState = args.ConnectionState;
        private void OnClientState(ClientConnectionStateArgs args) => _clientState = args.ConnectionState;

        private void OnGUI()
        {
            if (_networkManager == null) return;

            GUILayout.BeginArea(new Rect(10, 10, 220, 300));

            bool serverStopped = _serverState == LocalConnectionState.Stopped;
            bool clientStopped = _clientState == LocalConnectionState.Stopped;

            if (serverStopped && clientStopped)
            {
                if (GUILayout.Button("Host (Server + Client)", GUILayout.Height(36)))
                {
                    _networkManager.ServerManager.StartConnection();
                    _networkManager.ClientManager.StartConnection();
                }
            }

            string serverLabel = serverStopped ? "Start Server" : "Stop Server";
            if (GUILayout.Button(serverLabel, GUILayout.Height(36)))
            {
                if (serverStopped) _networkManager.ServerManager.StartConnection();
                else _networkManager.ServerManager.StopConnection(true);
            }

            string clientLabel = clientStopped ? "Start Client" : "Stop Client";
            if (GUILayout.Button(clientLabel, GUILayout.Height(36)))
            {
                if (clientStopped) _networkManager.ClientManager.StartConnection();
                else _networkManager.ClientManager.StopConnection();
            }

            GUILayout.Space(8);
            GUILayout.Label($"Server: {_serverState}");
            GUILayout.Label($"Client: {_clientState}");

            GUILayout.EndArea();
        }
    }
}
