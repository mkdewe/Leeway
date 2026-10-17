using System;
using System.Collections.Generic;
using Leeway.Online;

namespace Leeway.Network.Session
{
    /// <summary>One row of the lobby room's member list.</summary>
    public readonly struct LobbyMemberSnapshot
    {
        public readonly UserId Id;
        public readonly string DisplayName;
        public readonly bool IsOwner;
        public readonly bool IsReady;
        public readonly bool IsLocal;

        public LobbyMemberSnapshot(UserId id, string displayName, bool isOwner, bool isReady, bool isLocal)
        {
            Id = id;
            DisplayName = displayName;
            IsOwner = isOwner;
            IsReady = isReady;
            IsLocal = isLocal;
        }
    }

    /// <summary>
    /// An immutable picture of the current lobby — the view model the lobby room renders.
    /// </summary>
    /// <remarks>
    /// The UI never holds the live <see cref="ILobby"/>: a snapshot can be rendered, compared and
    /// tested without a backend, and it cannot be mutated behind the session's back.
    /// </remarks>
    public sealed class LobbySnapshot
    {
        public LobbyId Id { get; }
        public string Name { get; }
        public GameMode Mode { get; }
        public LobbyPhase Phase { get; }
        public LobbyVisibility Visibility { get; }
        public int MaxMembers { get; }
        public IReadOnlyList<LobbyMemberSnapshot> Members { get; }
        public bool IsLocalOwner { get; }

        /// <summary>What others type to join — see <see cref="ILobby.JoinCode"/>.</summary>
        public string JoinCode { get; }

        public LobbySnapshot(LobbyId id, string name, GameMode mode, LobbyPhase phase, LobbyVisibility visibility,
            int maxMembers, IReadOnlyList<LobbyMemberSnapshot> members, bool isLocalOwner, string joinCode = null)
        {
            Id = id;
            JoinCode = string.IsNullOrEmpty(joinCode) ? id.ToString() : joinCode;
            Name = name;
            Mode = mode;
            Phase = phase;
            Visibility = visibility;
            MaxMembers = maxMembers;
            Members = members;
            IsLocalOwner = isLocalOwner;
        }

        /// <summary>Everyone except the owner has pressed ready (the owner readies by pressing Start).</summary>
        public bool AllGuestsReady
        {
            get
            {
                foreach (var member in Members)
                    if (!member.IsOwner && !member.IsReady) return false;
                return true;
            }
        }

        public bool LocalIsReady
        {
            get
            {
                foreach (var member in Members)
                    if (member.IsLocal) return member.IsReady;
                return false;
            }
        }

        public bool CanStart => IsLocalOwner && Phase == LobbyPhase.Gathering && AllGuestsReady;

        /// <summary>Builds the snapshot from a live lobby. Unknown or missing data falls back to safe defaults.</summary>
        internal static LobbySnapshot From(ILobby lobby, UserId localUser)
        {
            var members = new List<LobbyMemberSnapshot>(lobby.Members.Count);
            foreach (var member in lobby.Members)
            {
                members.Add(new LobbyMemberSnapshot(
                    member.Id,
                    member.DisplayName,
                    isOwner: member.Id == lobby.OwnerId,
                    isReady: lobby.GetMemberData(member.Id, GameLobbyKeys.Ready) == "1",
                    isLocal: member.Id == localUser));
            }

            // Owner first, then join order — the order players expect from every lobby they have seen.
            members.Sort((a, b) => a.IsOwner == b.IsOwner ? 0 : a.IsOwner ? -1 : 1);

            return new LobbySnapshot(
                lobby.Id,
                lobby.Name,
                ParseEnum(lobby.GetData(GameLobbyKeys.Mode), GameMode.Coop),
                ParseEnum(lobby.GetData(GameLobbyKeys.Phase), LobbyPhase.Gathering),
                lobby.Visibility,
                lobby.MaxMembers,
                members,
                lobby.IsLocalUserOwner,
                lobby.JoinCode);
        }

        internal static T ParseEnum<T>(string value, T fallback) where T : struct, Enum =>
            Enum.TryParse(value, ignoreCase: true, out T parsed) && Enum.IsDefined(typeof(T), parsed) ? parsed : fallback;
    }
}
