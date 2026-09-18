using FishNet;
using FishNet.Managing;
using FishNet.Transporting;
using UnityEngine;

namespace Leeway.Network
{
    /// <summary>
    /// A minimal network HUD (Host / Server / Client / Stop) built on OnGUI.
    /// It replaces FishNet's `NetworkHudCanvases` demo, which throws an NRE when the new Input System
    /// is active (its _serverIndicator / _clientIndicator are left unwired).
    /// </summary>
    public class LeewayNetworkHud : MonoBehaviour
    {
        private const float Margin = 12f;
        private const float PanelWidth = 220f;
        private const float PanelHeight = 156f;
        private const float CollapsedHeight = 56f;
        private const float ButtonHeight = 30f;
        private const float ToggleHeight = 22f;

        [SerializeField] private bool _autoStartHostInEditor;

        /// <summary>Whether the panel is showing its buttons. A connected session collapses it until the player expands it again.</summary>
        private bool _expanded;

        private NetworkManager _networkManager;
        private LocalConnectionState _serverState = LocalConnectionState.Stopped;
        private LocalConnectionState _clientState = LocalConnectionState.Stopped;

        private void Start()
        {
            _networkManager = InstanceFinder.NetworkManager;
            if (_networkManager == null)
            {
                Debug.LogError("[LeewayNetworkHud] No NetworkManager found.");
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

            bool serverStopped = _serverState == LocalConnectionState.Stopped;
            bool clientStopped = _clientState == LocalConnectionState.Stopped;

            // While nothing is running, this panel is the only way to start a session, so it shows
            // itself in full. Once connected it collapses to a single line: the buttons are then needed
            // once every quarter of an hour, but they would occupy the corner of the screen for the
            // whole time you are playing.
            bool idle = serverStopped && clientStopped;
            bool open = idle || _expanded;

            float height = open ? PanelHeight : CollapsedHeight;

            // Bottom-left, not top-left. The upper half of the screen belongs to the editor UI — stats
            // on the left, palette on the right — and this HUD draws in raw pixels with no way to join
            // the canvas layout and get out of its way.
            GUILayout.BeginArea(new Rect(Margin, Screen.height - height - Margin, PanelWidth, height));

            GUILayout.Label($"Network — server: {Short(_serverState)}, client: {Short(_clientState)}");

            if (!idle && GUILayout.Button(_expanded ? "Collapse" : "Expand", GUILayout.Height(ToggleHeight)))
                _expanded = !_expanded;

            if (!open)
            {
                GUILayout.EndArea();
                return;
            }

            if (idle && GUILayout.Button("Host (server + client)", GUILayout.Height(ButtonHeight)))
            {
                _networkManager.ServerManager.StartConnection();
                _networkManager.ClientManager.StartConnection();
            }

            string serverLabel = serverStopped ? "Start server" : "Stop server";
            if (GUILayout.Button(serverLabel, GUILayout.Height(ButtonHeight)))
            {
                if (serverStopped) _networkManager.ServerManager.StartConnection();
                else _networkManager.ServerManager.StopConnection(true);
            }

            string clientLabel = clientStopped ? "Start client" : "Stop client";
            if (GUILayout.Button(clientLabel, GUILayout.Height(ButtonHeight)))
            {
                if (clientStopped) _networkManager.ClientManager.StartConnection();
                else _networkManager.ClientManager.StopConnection();
            }

            GUILayout.EndArea();
        }

        /// <summary>A short name for the state — the full one does not fit on the collapsed line.</summary>
        private static string Short(LocalConnectionState state) => state switch
        {
            LocalConnectionState.Stopped => "stopped",
            LocalConnectionState.Starting => "starting…",
            LocalConnectionState.Started => "running",
            LocalConnectionState.Stopping => "stopping…",
            _ => state.ToString(),
        };
    }
}
