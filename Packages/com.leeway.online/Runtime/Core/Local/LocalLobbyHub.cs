using System;
using System.Collections.Generic;

namespace Leeway.Online.Local
{
    /// <summary>
    /// The "server" of the Local backend: lobbies kept in memory and shared by every
    /// <see cref="LocalPlatform"/> created with the same hub.
    /// </summary>
    /// <remarks>
    /// Several platforms on one hub behave like several players on one lobby service — which is what
    /// the tests use to exercise join/leave/ownership flows without Steam. In a running game there is
    /// one platform per hub, so its lobbies are private to the process
    /// (no <see cref="OnlineCapabilities.RemoteLobbies"/>): good for solo play and UI work, and
    /// remote players come in over direct IP.
    /// </remarks>
    public sealed class LocalLobbyHub
    {
        private readonly Dictionary<LobbyId, LocalLobbyState> _lobbies = new();
        private int _nextId = 1;

        public IEnumerable<LocalLobbyState> Lobbies => _lobbies.Values;

        internal LocalLobbyState Create(LobbyCreateOptions options, UserProfile owner)
        {
            var id = new LobbyId($"L{_nextId++:D4}");
            var state = new LocalLobbyState(id, options, owner);
            state.Emptied += () => _lobbies.Remove(id);
            _lobbies.Add(id, state);
            return state;
        }

        internal bool TryGet(LobbyId id, out LocalLobbyState state) => _lobbies.TryGetValue(id, out state);
    }

    /// <summary>One lobby's authoritative state inside a <see cref="LocalLobbyHub"/>.</summary>
    public sealed class LocalLobbyState
    {
        private readonly Dictionary<string, string> _data = new();
        private readonly Dictionary<UserId, Dictionary<string, string>> _memberData = new();
        private readonly List<LobbyMember> _members = new();

        internal LocalLobbyState(LobbyId id, LobbyCreateOptions options, UserProfile owner)
        {
            Id = id;
            MaxMembers = options.MaxMembers;
            Visibility = options.Visibility;
            Owner = owner.Id;
            foreach (var pair in options.Data) _data[pair.Key] = pair.Value;
        }

        public LobbyId Id { get; }
        public int MaxMembers { get; }
        public LobbyVisibility Visibility { get; }
        public UserId Owner { get; private set; }
        public bool Joinable { get; private set; } = true;
        public IReadOnlyList<LobbyMember> Members => _members;
        public IReadOnlyDictionary<string, string> Data => _data;

        internal event Action<LobbyMember> MemberJoined;
        internal event Action<LobbyMember, LobbyLeaveReason> MemberLeft;
        internal event Action DataChanged;
        internal event Action<UserId> MemberDataChanged;
        internal event Action<UserId, string> Chat;
        internal event Action<UserId> OwnerChanged;
        internal event Action Emptied;

        internal OnlineError TryAdmit(UserProfile user)
        {
            foreach (var m in _members)
                if (m.Id == user.Id) return OnlineError.None;
            if (!Joinable) return new OnlineError(OnlineErrorCode.AccessDenied, "The lobby is not accepting new members.");
            if (_members.Count >= MaxMembers) return new OnlineError(OnlineErrorCode.LobbyFull, "The lobby is full.");

            var member = new LobbyMember(user.Id, user.DisplayName);
            _members.Add(member);
            MemberJoined?.Invoke(member);
            return OnlineError.None;
        }

        internal void Remove(UserId user, LobbyLeaveReason reason)
        {
            int index = _members.FindIndex(m => m.Id == user);
            if (index < 0) return;

            var member = _members[index];
            _members.RemoveAt(index);
            _memberData.Remove(user);
            MemberLeft?.Invoke(member, reason);

            if (_members.Count == 0)
            {
                Emptied?.Invoke();
                return;
            }

            // Ownership migrates to the longest-standing member, as on Steam.
            if (Owner == user)
            {
                Owner = _members[0].Id;
                OwnerChanged?.Invoke(Owner);
            }
        }

        internal string GetData(string key) => _data.TryGetValue(key, out var v) ? v : string.Empty;

        internal void SetData(string key, string value)
        {
            if (_data.TryGetValue(key, out var old) && old == value) return;
            _data[key] = value;
            DataChanged?.Invoke();
        }

        internal string GetMemberData(UserId user, string key) =>
            _memberData.TryGetValue(user, out var data) && data.TryGetValue(key, out var v) ? v : string.Empty;

        internal void SetMemberData(UserId user, string key, string value)
        {
            if (!_memberData.TryGetValue(user, out var data)) _memberData[user] = data = new Dictionary<string, string>();
            if (data.TryGetValue(key, out var old) && old == value) return;
            data[key] = value;
            MemberDataChanged?.Invoke(user);
        }

        internal void SetJoinable(bool joinable) => Joinable = joinable;

        internal void Say(UserId user, string text) => Chat?.Invoke(user, text);

        internal LobbySummary ToSummary() => new(Id, _members.Count, MaxMembers, new Dictionary<string, string>(_data));
    }
}
