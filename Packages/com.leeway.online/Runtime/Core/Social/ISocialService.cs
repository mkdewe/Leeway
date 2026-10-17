namespace Leeway.Online
{
    /// <summary>Friends and invites, as far as the platform exposes them.</summary>
    public interface ISocialService
    {
        /// <summary>Whether <see cref="OpenInviteDialog"/> can show anything right now (e.g. the Steam overlay is enabled).</summary>
        bool CanOpenInviteDialog { get; }

        /// <summary>Opens the platform's own "invite friends to this lobby" UI.</summary>
        OnlineResult OpenInviteDialog(LobbyId lobbyId);

        /// <summary>The name the platform shows for a user — best effort, falls back to the id.</summary>
        string GetDisplayName(UserId userId);
    }

    public sealed class NullSocialService : ISocialService
    {
        public static readonly NullSocialService Instance = new();

        private NullSocialService() { }

        public bool CanOpenInviteDialog => false;
        public OnlineResult OpenInviteDialog(LobbyId lobbyId) => OnlineError.NotSupported("Invites");
        public string GetDisplayName(UserId userId) => userId.ToString();
    }
}
