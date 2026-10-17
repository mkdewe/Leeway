using UnityEngine;

namespace Leeway.Online.Local
{
    [CreateAssetMenu(menuName = "Leeway/Online/Local Platform Provider", fileName = "LocalPlatformProvider")]
    public sealed class LocalPlatformProvider : OnlinePlatformProvider
    {
        [Tooltip("Player name shown in lobbies. Empty: the OS user name.")]
        [SerializeField] private string _playerName;

        // One hub per provider asset (not serialized): every platform this asset creates in a session
        // sees the same lobbies, which is what makes the backend useful in the editor.
        private LocalLobbyHub _hub;

        public override PlatformId PlatformId => PlatformId.Local;

        public override IOnlinePlatform CreatePlatform() => new LocalPlatform(_hub ??= new LocalLobbyHub(), _playerName);

        private void OnDisable() => _hub = null;
    }
}
