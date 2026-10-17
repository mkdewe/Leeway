namespace Leeway.Online
{
    /// <summary>Where a request to join a lobby came from outside the game's own UI.</summary>
    public enum LobbyJoinRequestSource
    {
        /// <summary>The player accepted an invite or clicked "Join game" in the platform overlay.</summary>
        PlatformOverlay,

        /// <summary>The game was launched with a lobby on the command line (e.g. Steam's <c>+connect_lobby</c>).</summary>
        LaunchArguments,
    }

    /// <summary>
    /// The platform asks the game to join a lobby. The game decides whether to honour it — e.g. it may
    /// first need to leave a running match.
    /// </summary>
    public readonly struct LobbyJoinRequest
    {
        public readonly LobbyId LobbyId;
        public readonly UserId InvitedBy;
        public readonly LobbyJoinRequestSource Source;

        public LobbyJoinRequest(LobbyId lobbyId, UserId invitedBy, LobbyJoinRequestSource source)
        {
            LobbyId = lobbyId;
            InvitedBy = invitedBy;
            Source = source;
        }
    }
}
