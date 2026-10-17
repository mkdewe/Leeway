using Leeway.Network.Session;
using Leeway.Online;

namespace Leeway.Network.Messages
{
    /// <summary>The session moved between <see cref="SessionState"/>s.</summary>
    public readonly struct SessionStateChangedMessage
    {
        public readonly SessionState Previous;
        public readonly SessionState Current;

        public SessionStateChangedMessage(SessionState previous, SessionState current)
        {
            Previous = previous;
            Current = current;
        }
    }

    /// <summary>The current lobby changed (members, ready flags, owner, data). <c>Lobby</c> is null when the player left it.</summary>
    public readonly struct LobbySnapshotChangedMessage
    {
        public readonly LobbySnapshot Lobby;
        public LobbySnapshotChangedMessage(LobbySnapshot lobby) => Lobby = lobby;
    }

    public readonly struct LobbyChatReceivedMessage
    {
        public readonly LobbyChatMessage Message;
        public readonly bool IsLocal;

        public LobbyChatReceivedMessage(LobbyChatMessage message, bool isLocal)
        {
            Message = message;
            IsLocal = isLocal;
        }
    }

    /// <summary>
    /// Something the player should be told about went wrong. <see cref="Title"/> and
    /// <see cref="Description"/> are player-facing; the error itself is for logs.
    /// </summary>
    public readonly struct SessionErrorMessage
    {
        public readonly string Title;
        public readonly string Description;
        public readonly OnlineError Error;

        public SessionErrorMessage(string title, string description, OnlineError error)
        {
            Title = title;
            Description = description;
            Error = error;
        }
    }

    /// <summary>The online platform finished starting (possibly as the Offline fallback).</summary>
    public readonly struct OnlinePlatformReadyMessage
    {
        public readonly PlatformId Platform;
        public readonly string PlatformName;
        public readonly UserProfile LocalUser;
        public readonly OnlineCapabilities Capabilities;

        public OnlinePlatformReadyMessage(PlatformId platform, string platformName, UserProfile localUser, OnlineCapabilities capabilities)
        {
            Platform = platform;
            PlatformName = platformName;
            LocalUser = localUser;
            Capabilities = capabilities;
        }
    }
}
