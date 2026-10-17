using System;
using System.Collections.Generic;
using FishNet.Managing;
using Leeway.Online;
using Leeway.Online.FishNet;
using UnityEngine;

namespace Leeway.Network.Session
{
    /// <summary>
    /// Everything about connecting that a designer may want to change without code: which backends
    /// to try, which transports to offer, which scene a match starts in.
    /// </summary>
    [CreateAssetMenu(menuName = "Leeway/Network/Network Session Settings", fileName = "NetworkSessionSettings")]
    public sealed class NetworkSessionSettings : ScriptableObject
    {
        [Serializable]
        public struct PlatformNetworkManager
        {
            [Tooltip("Platform id (\"steam\", \"eos\" …) this NetworkManager is for.")]
            public string PlatformId;
            public NetworkManager Prefab;
        }

        [Header("Online platforms")]
        [Tooltip("Tried in order; the first one that initializes wins. Offline is always the last resort.")]
        [SerializeField] private List<OnlinePlatformProvider> _platformProviders = new();

        [Tooltip("Editor and development builds only: try this platform id (\"ugs\", \"local\" …) first. Handy for " +
                 "testing several instances on one PC, where they cannot all be the same Steam user. " +
                 "The -online-platform <id> launch argument overrides it.")]
        [SerializeField] private string _developmentPreferredPlatform = string.Empty;

        [Header("Netcode")]
        [Tooltip("Endpoint schemes in order of preference. The host advertises all it can, clients dial the first they support.")]
        [SerializeField] private List<TransportAdapter> _transportAdapters = new();

        [Tooltip("The NetworkManager used when no platform-specific one is listed below.")]
        [SerializeField] private NetworkManager _defaultNetworkManager;

        [Tooltip("Platform-specific NetworkManagers — e.g. Steam needs FishySteamworks next to Tugboat.")]
        [SerializeField] private List<PlatformNetworkManager> _platformNetworkManagers = new();

        [Tooltip("Port for direct-IP play (Tugboat).")]
        [SerializeField] private ushort _defaultPort = 7770;

        [Tooltip("Give up dialling the host after this long.")]
        [Min(1f)]
        [SerializeField] private float _connectTimeoutSeconds = 15f;

        [Header("Lobbies")]
        [Tooltip("Players only see lobbies of the same protocol. Bump it whenever builds stop being network-compatible. Empty: Application.version.")]
        [SerializeField] private string _protocolVersion = string.Empty;

        [Range(2, 16)]
        [SerializeField] private int _maxPlayersLimit = 8;

        [Range(2, 16)]
        [SerializeField] private int _defaultMaxPlayers = 4;

        [Header("Scenes")]
        [Tooltip("Scene loaded for everyone when the host starts a match. Must be in the build profile.")]
        [SerializeField] private string _gameplayScene = "CreatureEditor";

        [Tooltip("Scene to return to after leaving a match.")]
        [SerializeField] private string _mainMenuScene = "MainMenu";

        /// <summary>The providers in the order to try them, with the development preference (if any) moved to the front.</summary>
        public IReadOnlyList<OnlinePlatformProvider> GetPlatformProviders()
        {
            var preferred = ReadPreferredPlatform();
            if (!preferred.IsValid) return _platformProviders;

            var ordered = new List<OnlinePlatformProvider>(_platformProviders.Count);
            foreach (var provider in _platformProviders)
                if (provider != null && provider.PlatformId == preferred) ordered.Add(provider);
            foreach (var provider in _platformProviders)
                if (provider == null || provider.PlatformId != preferred) ordered.Add(provider);
            return ordered;
        }

        private PlatformId ReadPreferredPlatform()
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], "-online-platform", StringComparison.OrdinalIgnoreCase))
                    return new PlatformId(args[i + 1].Trim());

            return Debug.isDebugBuild && !string.IsNullOrWhiteSpace(_developmentPreferredPlatform)
                ? new PlatformId(_developmentPreferredPlatform.Trim())
                : default;
        }
        public IReadOnlyList<TransportAdapter> TransportAdapters => _transportAdapters;
        public ushort DefaultPort => _defaultPort;
        public TimeSpan ConnectTimeout => TimeSpan.FromSeconds(_connectTimeoutSeconds);
        public string ProtocolVersion => string.IsNullOrWhiteSpace(_protocolVersion) ? Application.version : _protocolVersion.Trim();
        public int MaxPlayersLimit => _maxPlayersLimit;
        public int DefaultMaxPlayers => Mathf.Clamp(_defaultMaxPlayers, 2, _maxPlayersLimit);
        public string GameplayScene => _gameplayScene;
        public string MainMenuScene => _mainMenuScene;

        public NetworkManager GetNetworkManagerPrefab(PlatformId platform)
        {
            foreach (var entry in _platformNetworkManagers)
                if (entry.Prefab != null && new PlatformId(entry.PlatformId) == platform) return entry.Prefab;
            return _defaultNetworkManager;
        }
    }
}
