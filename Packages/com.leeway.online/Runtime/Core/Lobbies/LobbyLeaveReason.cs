namespace Leeway.Online
{
    public enum LobbyLeaveReason
    {
        /// <summary>The member left on purpose.</summary>
        Left,

        /// <summary>The member lost their connection to the platform.</summary>
        Disconnected,

        Kicked,

        /// <summary>The lobby stopped existing (owner closed it, backend shut down).</summary>
        LobbyClosed,
    }
}
