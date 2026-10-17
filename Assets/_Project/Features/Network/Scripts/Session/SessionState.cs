namespace Leeway.Network.Session
{
    /// <summary>Where the local player is in the connect flow. Exactly one at a time.</summary>
    public enum SessionState
    {
        /// <summary>The online platform is still coming up.</summary>
        Initializing,

        /// <summary>In the main menu, not in a lobby or a match.</summary>
        Idle,

        CreatingLobby,
        JoiningLobby,

        /// <summary>In a lobby, waiting for the host to start.</summary>
        InLobby,

        /// <summary>The host is bringing up the server and loading the match.</summary>
        StartingGame,

        /// <summary>A client is dialling the host.</summary>
        Connecting,

        InGame,

        /// <summary>Tearing down lobby and connection on the way back to <see cref="Idle"/>.</summary>
        Leaving,
    }
}
