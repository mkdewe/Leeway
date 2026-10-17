using System.Collections.Generic;
using System.Text;
using Steamworks;

namespace Leeway.Online.Steam
{
    /// <summary>The local user's live view of a Steam lobby.</summary>
    internal sealed class SteamLobby : LobbyBase
    {
        private const int MaxChatBytes = 4096;

        private readonly LobbyVisibility _visibilityAtCreation;

        public SteamLobby(CSteamID steamId, CSteamID localUser, LobbyVisibility? knownVisibility = null)
            : base(steamId.ToLobbyId(), localUser.ToUserId())
        {
            SteamId = steamId;
            _visibilityAtCreation = knownVisibility ?? LobbyVisibility.Public;
            Refresh();
        }

        public CSteamID SteamId { get; }

        public override int MaxMembers => SteamMatchmaking.GetLobbyMemberLimit(SteamId);

        public override LobbyVisibility Visibility =>
            System.Enum.TryParse(GetData(LobbyDataKeys.Visibility), out LobbyVisibility stored) ? stored : _visibilityAtCreation;

        /// <summary>Re-reads members and owner from Steam and raises the difference.</summary>
        public void Refresh()
        {
            OnOwnerChanged(SteamMatchmaking.GetLobbyOwner(SteamId).ToUserId());

            int count = SteamMatchmaking.GetNumLobbyMembers(SteamId);
            var members = new List<LobbyMember>(count);
            for (int i = 0; i < count; i++)
            {
                var member = SteamMatchmaking.GetLobbyMemberByIndex(SteamId, i);
                members.Add(new LobbyMember(member.ToUserId(), SteamFriends.GetFriendPersonaName(member)));
            }
            OnMembersSnapshot(members);
        }

        // Steam reports joins/leaves through LobbyChatUpdate_t with an explicit reason; these keep the
        // reason instead of collapsing everything into a snapshot diff.
        public void HandleMemberEntered(CSteamID user) =>
            OnMemberJoined(new LobbyMember(user.ToUserId(), SteamFriends.GetFriendPersonaName(user)));

        public void HandleMemberLeft(CSteamID user, LobbyLeaveReason reason) => OnMemberLeft(user.ToUserId(), reason);

        public void HandleOwnerMaybeChanged() => OnOwnerChanged(SteamMatchmaking.GetLobbyOwner(SteamId).ToUserId());

        public void HandleDataChanged() => OnDataChanged();

        public void HandleMemberDataChanged(CSteamID user) => OnMemberDataChanged(user.ToUserId());

        public void HandleChat(int chatId)
        {
            var buffer = new byte[MaxChatBytes];
            int length = SteamMatchmaking.GetLobbyChatEntry(SteamId, chatId, out var sender, buffer, buffer.Length, out var type);
            if (type != EChatEntryType.k_EChatEntryTypeChatMsg || length <= 0) return;

            // Senders include the terminating zero; strip it rather than trusting every client to.
            while (length > 0 && buffer[length - 1] == 0) length--;
            OnChatMessage(sender.ToUserId(), Encoding.UTF8.GetString(buffer, 0, length));
        }

        protected override string ResolveDisplayName(UserId id) =>
            id.TryToSteamId(out var steamId) ? SteamFriends.GetFriendPersonaName(steamId) : base.ResolveDisplayName(id);

        protected override string ReadData(string key) => SteamMatchmaking.GetLobbyData(SteamId, key);

        protected override string ReadMemberData(UserId member, string key) =>
            member.TryToSteamId(out var steamId) ? SteamMatchmaking.GetLobbyMemberData(SteamId, steamId, key) : string.Empty;

        protected override OnlineResult WriteData(string key, string value) =>
            SteamMatchmaking.SetLobbyData(SteamId, key, value)
                ? OnlineResult.Success
                : OnlineResult.Failure(OnlineErrorCode.ServiceError, $"Steam refused lobby data '{key}'.");

        protected override OnlineResult WriteLocalMemberData(string key, string value)
        {
            SteamMatchmaking.SetLobbyMemberData(SteamId, key, value);
            return OnlineResult.Success;
        }

        protected override OnlineResult WriteJoinable(bool joinable) =>
            SteamMatchmaking.SetLobbyJoinable(SteamId, joinable)
                ? OnlineResult.Success
                : OnlineResult.Failure(OnlineErrorCode.ServiceError, "Steam refused to change joinability.");

        protected override OnlineResult WriteChatMessage(string text)
        {
            var bytes = Encoding.UTF8.GetBytes(text + "\0");
            if (bytes.Length > MaxChatBytes)
                return OnlineResult.Failure(OnlineErrorCode.InvalidArgument, "Chat message is too long.");

            return SteamMatchmaking.SendLobbyChatMsg(SteamId, bytes, bytes.Length)
                ? OnlineResult.Success
                : OnlineResult.Failure(OnlineErrorCode.ServiceError, "Steam refused the chat message.");
        }
    }
}
