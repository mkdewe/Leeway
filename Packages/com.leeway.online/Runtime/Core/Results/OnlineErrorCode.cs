namespace Leeway.Online
{
    /// <summary>
    /// The backend-neutral failure vocabulary. Backends translate their own result codes into these so
    /// the game can react (retry, show a message, fall back) without knowing who failed.
    /// </summary>
    public enum OnlineErrorCode
    {
        None = 0,

        /// <summary>The platform has not finished <c>InitializeAsync</c>, or it failed.</summary>
        NotInitialized,

        /// <summary>The platform client is missing or not running (no Steam client, no PSN sign-in …).</summary>
        PlatformUnavailable,

        /// <summary>The backend does not offer this feature — check <see cref="OnlineCapabilities"/> first.</summary>
        NotSupported,

        InvalidArgument,
        Cancelled,
        Timeout,

        /// <summary>The lobby (or user) no longer exists.</summary>
        NotFound,

        LobbyFull,

        /// <summary>Private lobby, banned, wrong password, age restriction …</summary>
        AccessDenied,

        AlreadyInLobby,
        NotInLobby,

        /// <summary>Only the lobby owner may do that.</summary>
        NotLobbyOwner,

        /// <summary>The other side runs an incompatible build.</summary>
        VersionMismatch,

        /// <summary>The netcode connection could not be established or was lost.</summary>
        ConnectionFailed,

        /// <summary>The backend's service itself failed (rate limit, outage, I/O failure).</summary>
        ServiceError,

        Unknown,
    }
}
