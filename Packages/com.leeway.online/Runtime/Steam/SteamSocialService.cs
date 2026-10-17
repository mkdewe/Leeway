using Steamworks;

namespace Leeway.Online.Steam
{
    internal sealed class SteamSocialService : ISocialService
    {
        public bool CanOpenInviteDialog => SteamUtils.IsOverlayEnabled();

        public OnlineResult OpenInviteDialog(LobbyId lobbyId)
        {
            if (!lobbyId.TryToSteamId(out var steamLobby))
                return OnlineResult.Failure(OnlineErrorCode.InvalidArgument, $"'{lobbyId}' is not a Steam lobby id.");
            if (!SteamUtils.IsOverlayEnabled())
                return OnlineResult.Failure(OnlineErrorCode.NotSupported, "The Steam overlay is disabled.");

            SteamFriends.ActivateGameOverlayInviteDialog(steamLobby);
            return OnlineResult.Success;
        }

        public string GetDisplayName(UserId userId) =>
            userId.TryToSteamId(out var steamId) ? SteamFriends.GetFriendPersonaName(steamId) : userId.ToString();
    }
}
