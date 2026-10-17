namespace Leeway.Network.Session
{
    /// <summary>The game's own lobby/member data keys (the online layer reserves the <c>__</c> prefix).</summary>
    public static class GameLobbyKeys
    {
        /// <summary>Lobby: <see cref="GameMode"/>.</summary>
        public const string Mode = "mode";

        /// <summary>Lobby: <see cref="LobbyPhase"/> — the host flips it to start the match.</summary>
        public const string Phase = "phase";

        /// <summary>Member: "1" when the player is ready.</summary>
        public const string Ready = "ready";
    }

    /// <summary>Where a lobby is in its life. Clients watch for <see cref="InGame"/> to connect to the host.</summary>
    public enum LobbyPhase
    {
        Gathering,
        InGame,
    }
}
