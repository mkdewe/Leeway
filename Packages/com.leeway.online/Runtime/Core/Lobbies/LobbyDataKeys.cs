namespace Leeway.Online
{
    /// <summary>
    /// Lobby data keys the online layer itself reads and writes. The <c>__</c> prefix is reserved —
    /// games should use their own unprefixed keys.
    /// </summary>
    public static class LobbyDataKeys
    {
        public const string ReservedPrefix = "__";

        /// <summary>The lobby's display name.</summary>
        public const string Name = "__name";

        /// <summary>
        /// The <see cref="LobbyVisibility"/> the lobby was created with. Stored as data because some
        /// backends (Steam) cannot report a lobby's type back to members.
        /// </summary>
        public const string Visibility = "__visibility";

        /// <summary>Where the host's game server can be reached — see <see cref="NetworkEndpointList"/>.</summary>
        public const string Endpoints = "__endpoints";

        /// <summary>
        /// The build/protocol the lobby was created with. Search filters on it so players never see
        /// lobbies they could not play in.
        /// </summary>
        public const string ProtocolVersion = "__protocol";
    }
}
