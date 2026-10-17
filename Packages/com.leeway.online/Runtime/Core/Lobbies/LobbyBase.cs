using System;
using System.Collections.Generic;

namespace Leeway.Online
{
    /// <summary>
    /// The bookkeeping every backend's lobby shares: the member list, owner tracking, the
    /// owner-only guards and event raising. A backend fills in the template methods
    /// (<c>Read…</c>/<c>Write…</c>) and calls the <c>On…</c> hooks when its SDK reports a change.
    /// </summary>
    /// <remarks>
    /// Keeping the diffing here means "member joined" fires exactly once per member on every backend,
    /// even when an SDK reports the same change twice (Steam does, on reconnects).
    /// </remarks>
    public abstract class LobbyBase : ILobby
    {
        private readonly List<LobbyMember> _members = new();

        protected LobbyBase(LobbyId id, UserId localUserId)
        {
            Id = id;
            LocalUserId = localUserId;
        }

        public LobbyId Id { get; }
        public UserId OwnerId { get; private set; }
        public bool IsLocalUserOwner => OwnerId == LocalUserId;
        public IReadOnlyList<LobbyMember> Members => _members;

        public abstract int MaxMembers { get; }
        public abstract LobbyVisibility Visibility { get; }

        /// <summary>False once the local user has left; writes then fail with <see cref="OnlineErrorCode.NotInLobby"/>.</summary>
        public bool IsActive { get; private set; } = true;

        protected UserId LocalUserId { get; }

        public string Name => GetData(LobbyDataKeys.Name) is { Length: > 0 } name ? name : Id.ToString();

        public virtual string JoinCode => Id.ToString();

        public event Action<LobbyMember> MemberJoined;
        public event Action<LobbyMember, LobbyLeaveReason> MemberLeft;
        public event Action DataChanged;
        public event Action<LobbyMember> MemberDataChanged;
        public event Action<LobbyChatMessage> ChatMessageReceived;
        public event Action<UserId> OwnerChanged;

        public string GetData(string key) => string.IsNullOrEmpty(key) ? string.Empty : ReadData(key) ?? string.Empty;

        public string GetMemberData(UserId member, string key) =>
            string.IsNullOrEmpty(key) || !member.IsValid ? string.Empty : ReadMemberData(member, key) ?? string.Empty;

        public OnlineResult SetData(string key, string value)
        {
            if (!IsActive) return OnlineError.NotInLobby;
            if (!IsLocalUserOwner) return OnlineError.NotOwner;
            if (string.IsNullOrEmpty(key)) return OnlineResult.Failure(OnlineErrorCode.InvalidArgument, "Lobby data key must not be empty.");
            return WriteData(key, value ?? string.Empty);
        }

        public OnlineResult SetLocalMemberData(string key, string value)
        {
            if (!IsActive) return OnlineError.NotInLobby;
            if (string.IsNullOrEmpty(key)) return OnlineResult.Failure(OnlineErrorCode.InvalidArgument, "Member data key must not be empty.");
            return WriteLocalMemberData(key, value ?? string.Empty);
        }

        public OnlineResult SetJoinable(bool joinable)
        {
            if (!IsActive) return OnlineError.NotInLobby;
            if (!IsLocalUserOwner) return OnlineError.NotOwner;
            return WriteJoinable(joinable);
        }

        public OnlineResult SendChatMessage(string text)
        {
            if (!IsActive) return OnlineError.NotInLobby;
            if (string.IsNullOrWhiteSpace(text)) return OnlineResult.Failure(OnlineErrorCode.InvalidArgument, "Empty chat message.");
            return WriteChatMessage(text.Trim());
        }

        public LobbyMember FindMember(UserId id)
        {
            foreach (var member in _members)
                if (member.Id == id) return member;
            return new LobbyMember(id, ResolveDisplayName(id));
        }

        public bool Contains(UserId id)
        {
            foreach (var member in _members)
                if (member.Id == id) return true;
            return false;
        }

        // ---- Template methods ----------------------------------------------------------------

        protected abstract string ReadData(string key);
        protected abstract string ReadMemberData(UserId member, string key);
        protected abstract OnlineResult WriteData(string key, string value);
        protected abstract OnlineResult WriteLocalMemberData(string key, string value);
        protected abstract OnlineResult WriteJoinable(bool joinable);
        protected abstract OnlineResult WriteChatMessage(string text);

        /// <summary>A name for a user the lobby has not reported yet (used for late chat lines and the like).</summary>
        protected virtual string ResolveDisplayName(UserId id) => id.ToString();

        // ---- Hooks for the backend -----------------------------------------------------------

        /// <summary>Replaces the member list wholesale, raising joined/left for the difference.</summary>
        protected void OnMembersSnapshot(IReadOnlyList<LobbyMember> members)
        {
            for (int i = _members.Count - 1; i >= 0; i--)
            {
                var existing = _members[i];
                bool stillThere = false;
                foreach (var m in members)
                    if (m.Id == existing.Id) { stillThere = true; break; }
                if (!stillThere) OnMemberLeft(existing.Id, LobbyLeaveReason.Left);
            }

            foreach (var m in members) OnMemberJoined(m);
        }

        protected void OnMemberJoined(LobbyMember member)
        {
            if (Contains(member.Id)) return;
            _members.Add(member);
            MemberJoined?.Invoke(member);
        }

        protected void OnMemberLeft(UserId id, LobbyLeaveReason reason)
        {
            for (int i = 0; i < _members.Count; i++)
            {
                if (_members[i].Id != id) continue;
                var member = _members[i];
                _members.RemoveAt(i);
                MemberLeft?.Invoke(member, reason);
                return;
            }
        }

        protected void OnOwnerChanged(UserId owner)
        {
            if (owner == OwnerId) return;
            OwnerId = owner;
            OwnerChanged?.Invoke(owner);
        }

        protected void OnDataChanged() => DataChanged?.Invoke();

        protected void OnMemberDataChanged(UserId member) => MemberDataChanged?.Invoke(FindMember(member));

        protected void OnChatMessage(UserId sender, string text) =>
            ChatMessageReceived?.Invoke(new LobbyChatMessage(FindMember(sender), text, DateTime.UtcNow));

        /// <summary>Called by <see cref="LobbyServiceBase"/> once the local user is out.</summary>
        internal void Deactivate() => IsActive = false;
    }
}
