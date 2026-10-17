using System;
using System.Collections.Generic;
using System.Text;
using Unity.Services.Lobbies.Models;

namespace Leeway.Online.UnityServices
{
    /// <summary>
    /// The local user's live view of a UGS lobby. The SDK's <see cref="Lobby"/> model is the state;
    /// every update (event, poll, write reply) replaces it and the difference is raised.
    /// </summary>
    /// <remarks>
    /// UGS writes are HTTP requests, but <see cref="ILobby"/> writes are synchronous. Writes are
    /// therefore applied to the local model at once (so reads see them) and queued; the service sends
    /// everything queued in one request per frame. That keeps the host's "endpoints, then phase"
    /// pair atomic for guests and stays well inside the Lobby rate limits.
    /// </remarks>
    internal sealed class UgsLobby : LobbyBase
    {
        /// <summary>Member data key carrying the player's display name.</summary>
        public const string PlayerNameKey = "__player";

        private readonly UgsDataIndex _index;
        private readonly Action<UgsLobby> _requestFlush;
        private readonly Dictionary<string, string> _memberDataSignatures = new();
        private Lobby _model;
        private string _dataSignature = string.Empty;

        public UgsLobby(Lobby model, UserId localUser, UgsDataIndex index, Action<UgsLobby> requestFlush)
            : base(new LobbyId(model.Id), localUser)
        {
            _index = index;
            _requestFlush = requestFlush;
            Apply(model);
        }

        public string UgsId => Id.Value;
        public override string JoinCode => string.IsNullOrEmpty(_model.LobbyCode) ? base.JoinCode : _model.LobbyCode;
        public Lobby Model => _model;

        public override int MaxMembers => _model.MaxPlayers;

        public override LobbyVisibility Visibility =>
            Enum.TryParse(GetData(LobbyDataKeys.Visibility), out LobbyVisibility stored) ? stored
            : _model.IsPrivate ? LobbyVisibility.Private : LobbyVisibility.Public;

        // ---- Pending writes (drained by the service) ---------------------------------------------

        public Dictionary<string, DataObject> PendingData { get; } = new();
        public Dictionary<string, PlayerDataObject> PendingMemberData { get; } = new();
        public bool? PendingLocked { get; set; }

        public bool HasPendingLobbyWrite => PendingData.Count > 0 || PendingLocked.HasValue;
        public bool HasPendingMemberWrite => PendingMemberData.Count > 0;

        // ---- State from the backend --------------------------------------------------------------

        /// <summary>Takes a fresh model from the service and raises whatever changed.</summary>
        public void Apply(Lobby model)
        {
            if (model == null) return;
            _model = model;

            // Writes not yet confirmed would be undone by an older snapshot; keep them visible.
            ReapplyPending();

            OnOwnerChanged(new UserId(model.HostId));

            var players = model.Players ?? new List<Player>();
            var members = new List<LobbyMember>(players.Count);
            foreach (var player in players) members.Add(new LobbyMember(new UserId(player.Id), NameOf(player)));
            OnMembersSnapshot(members);

            var dataSignature = Signature(_model.Data);
            if (dataSignature != _dataSignature)
            {
                _dataSignature = dataSignature;
                OnDataChanged();
            }

            foreach (var player in _model.Players ?? players)
            {
                var signature = Signature(player.Data);
                if (_memberDataSignatures.TryGetValue(player.Id, out var previous) && previous == signature) continue;
                bool known = _memberDataSignatures.ContainsKey(player.Id);
                _memberDataSignatures[player.Id] = signature;
                if (known) OnMemberDataChanged(new UserId(player.Id));
            }
        }

        // ---- Template methods ------------------------------------------------------------------

        protected override string ReadData(string key) =>
            _model.Data != null && _model.Data.TryGetValue(key, out var value) ? value.Value : null;

        protected override string ReadMemberData(UserId member, string key)
        {
            var player = FindPlayer(member.Value);
            return player?.Data != null && player.Data.TryGetValue(key, out var value) ? value.Value : null;
        }

        protected override string ResolveDisplayName(UserId id)
        {
            var player = FindPlayer(id.Value);
            return player != null ? NameOf(player) : base.ResolveDisplayName(id);
        }

        protected override OnlineResult WriteData(string key, string value)
        {
            // Endpoints are for members only; everything else is public so the browser can show it.
            var visibility = key == LobbyDataKeys.Endpoints ? DataObject.VisibilityOptions.Member : DataObject.VisibilityOptions.Public;
            var data = visibility == DataObject.VisibilityOptions.Public && _index.TryGetIndex(key, out var slot)
                ? new DataObject(visibility, value, slot)
                : new DataObject(visibility, value);

            PendingData[key] = data;
            ReapplyPending();
            _dataSignature = Signature(_model.Data);
            OnDataChanged();
            _requestFlush(this);
            return OnlineResult.Success;
        }

        protected override OnlineResult WriteLocalMemberData(string key, string value)
        {
            PendingMemberData[key] = new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, value);
            ReapplyPending();
            if (FindPlayer(LocalUserId.Value) is { } local) _memberDataSignatures[local.Id] = Signature(local.Data);
            OnMemberDataChanged(LocalUserId);
            _requestFlush(this);
            return OnlineResult.Success;
        }

        protected override OnlineResult WriteJoinable(bool joinable)
        {
            PendingLocked = !joinable;
            _requestFlush(this);
            return OnlineResult.Success;
        }

        protected override OnlineResult WriteChatMessage(string text) => OnlineError.NotSupported("Lobby chat");

        // ---- Helpers -----------------------------------------------------------------------------

        private void ReapplyPending()
        {
            if (PendingData.Count > 0)
            {
                var data = _model.Data != null ? new Dictionary<string, DataObject>(_model.Data) : new Dictionary<string, DataObject>();
                foreach (var pair in PendingData) data[pair.Key] = pair.Value;
                _model = WithData(_model, data);
            }

            if (PendingMemberData.Count > 0 && FindPlayer(LocalUserId.Value) is { } local)
            {
                local.Data ??= new Dictionary<string, PlayerDataObject>();
                foreach (var pair in PendingMemberData) local.Data[pair.Key] = pair.Value;
            }
        }

        private Player FindPlayer(string id)
        {
            if (_model.Players == null) return null;
            foreach (var player in _model.Players)
                if (player.Id == id) return player;
            return null;
        }

        private static string NameOf(Player player) =>
            player.Data != null && player.Data.TryGetValue(PlayerNameKey, out var name) && !string.IsNullOrWhiteSpace(name.Value)
                ? name.Value
                : player.Profile?.Name ?? player.Id;

        // Lobby.Data has no setter; rebuild the model around the new dictionary.
        private static Lobby WithData(Lobby m, Dictionary<string, DataObject> data) =>
            new(m.Id, m.LobbyCode, m.Upid, m.EnvironmentId, m.Name, m.MaxPlayers, m.AvailableSlots, m.IsPrivate, m.IsLocked,
                m.Players, data, m.HostId, m.Created, m.LastUpdated, m.Version, m.HasPassword);

        private static string Signature<T>(Dictionary<string, T> data) where T : class
        {
            if (data == null || data.Count == 0) return string.Empty;
            var keys = new List<string>(data.Keys);
            keys.Sort(StringComparer.Ordinal);
            var builder = new StringBuilder();
            foreach (var key in keys)
            {
                string value = data[key] switch
                {
                    DataObject d => d.Value,
                    PlayerDataObject p => p.Value,
                    _ => null,
                };
                builder.Append(key).Append('=').Append(value).Append('\n');
            }
            return builder.ToString();
        }
    }
}
