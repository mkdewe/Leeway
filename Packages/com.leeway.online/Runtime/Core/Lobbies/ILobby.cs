using System;
using System.Collections.Generic;

namespace Leeway.Online
{
    /// <summary>
    /// The lobby the local user is currently in — a live view that raises events as the backend
    /// reports changes.
    /// </summary>
    /// <remarks>
    /// The object is only valid while the user is in the lobby. After
    /// <see cref="ILobbyService.Left"/> fires it is inert: reads return the last known state and
    /// writes fail with <see cref="OnlineErrorCode.NotInLobby"/>.
    /// </remarks>
    public interface ILobby
    {
        LobbyId Id { get; }
        UserId OwnerId { get; }
        bool IsLocalUserOwner { get; }
        int MaxMembers { get; }
        LobbyVisibility Visibility { get; }
        IReadOnlyList<LobbyMember> Members { get; }

        string Name { get; }

        /// <summary>What a player types to join (<see cref="ILobbyService.JoinAsync"/> accepts it). The id itself unless the backend has a shorter code.</summary>
        string JoinCode { get; }

        string GetData(string key);

        /// <summary>Owner only. Keys starting with <see cref="LobbyDataKeys.ReservedPrefix"/> are reserved for the online layer.</summary>
        OnlineResult SetData(string key, string value);

        string GetMemberData(UserId member, string key);

        /// <summary>Sets data on the local user's own membership (ready flags and the like).</summary>
        OnlineResult SetLocalMemberData(string key, string value);

        /// <summary>Owner only. Closes the lobby to newcomers without kicking anyone (used once a match starts).</summary>
        OnlineResult SetJoinable(bool joinable);

        OnlineResult SendChatMessage(string text);

        event Action<LobbyMember> MemberJoined;
        event Action<LobbyMember, LobbyLeaveReason> MemberLeft;

        /// <summary>Lobby-level data changed. Backends do not always say which key, so listeners re-read what they need.</summary>
        event Action DataChanged;

        event Action<LobbyMember> MemberDataChanged;
        event Action<LobbyChatMessage> ChatMessageReceived;
        event Action<UserId> OwnerChanged;
    }
}
