using System;

namespace Leeway.Online.Local
{
    /// <summary>One user's live view of a <see cref="LocalLobbyState"/>.</summary>
    internal sealed class LocalLobby : LobbyBase
    {
        private readonly LocalLobbyState _state;

        public LocalLobby(LocalLobbyState state, UserId localUser) : base(state.Id, localUser)
        {
            _state = state;
            OnOwnerChanged(state.Owner);
            OnMembersSnapshot(state.Members);

            _state.MemberJoined += OnMemberJoined;
            _state.MemberLeft += HandleMemberLeft;
            _state.DataChanged += OnDataChanged;
            _state.MemberDataChanged += OnMemberDataChanged;
            _state.Chat += OnChatMessage;
            _state.OwnerChanged += OnOwnerChanged;
        }

        public override int MaxMembers => _state.MaxMembers;
        public override LobbyVisibility Visibility => _state.Visibility;

        internal LocalLobbyState State => _state;

        /// <summary>Raised when the local user is removed by someone else (kick, lobby closed).</summary>
        public event Action<LobbyLeaveReason> LocalUserRemoved;

        public void Detach()
        {
            _state.MemberJoined -= OnMemberJoined;
            _state.MemberLeft -= HandleMemberLeft;
            _state.DataChanged -= OnDataChanged;
            _state.MemberDataChanged -= OnMemberDataChanged;
            _state.Chat -= OnChatMessage;
            _state.OwnerChanged -= OnOwnerChanged;
        }

        private void HandleMemberLeft(LobbyMember member, LobbyLeaveReason reason)
        {
            if (member.Id == LocalUserId)
            {
                LocalUserRemoved?.Invoke(reason);
                return;
            }
            OnMemberLeft(member.Id, reason);
        }

        protected override string ReadData(string key) => _state.GetData(key);
        protected override string ReadMemberData(UserId member, string key) => _state.GetMemberData(member, key);

        protected override OnlineResult WriteData(string key, string value)
        {
            _state.SetData(key, value);
            return OnlineResult.Success;
        }

        protected override OnlineResult WriteLocalMemberData(string key, string value)
        {
            _state.SetMemberData(LocalUserId, key, value);
            return OnlineResult.Success;
        }

        protected override OnlineResult WriteJoinable(bool joinable)
        {
            _state.SetJoinable(joinable);
            return OnlineResult.Success;
        }

        protected override OnlineResult WriteChatMessage(string text)
        {
            _state.Say(LocalUserId, text);
            return OnlineResult.Success;
        }
    }
}
