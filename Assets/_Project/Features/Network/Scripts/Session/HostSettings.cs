using Leeway.Online;

namespace Leeway.Network.Session
{
    /// <summary>What the player chose on the "create lobby" screen.</summary>
    public readonly struct HostSettings
    {
        public readonly string LobbyName;
        public readonly GameMode Mode;
        public readonly int MaxPlayers;
        public readonly LobbyVisibility Visibility;

        public HostSettings(string lobbyName, GameMode mode, int maxPlayers, LobbyVisibility visibility)
        {
            LobbyName = lobbyName;
            Mode = mode;
            MaxPlayers = maxPlayers;
            Visibility = visibility;
        }
    }
}
