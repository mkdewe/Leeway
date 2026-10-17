using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Leeway.Online
{
    /// <summary>
    /// Creating, finding and joining lobbies. One lobby at a time — joining another leaves the current one.
    /// </summary>
    public interface ILobbyService
    {
        /// <summary>The lobby the local user is in, or null.</summary>
        ILobby Current { get; }

        event Action<ILobby> Entered;
        event Action<LobbyId, LobbyLeaveReason> Left;

        /// <summary>
        /// The platform asks to join a lobby (accepted invite, launch argument). Requests that arrive
        /// before anyone subscribes are kept and replayed to the first subscriber, so a game launched
        /// from an invite does not lose it while the menu is still loading.
        /// </summary>
        event Action<LobbyJoinRequest> JoinRequested;

        UniTask<OnlineResult<ILobby>> CreateAsync(LobbyCreateOptions options, CancellationToken cancellationToken = default);

        UniTask<OnlineResult<ILobby>> JoinAsync(LobbyId lobbyId, CancellationToken cancellationToken = default);

        UniTask<OnlineResult<IReadOnlyList<LobbySummary>>> SearchAsync(LobbyQuery query, CancellationToken cancellationToken = default);

        /// <summary>Leaves the current lobby, if any. Never fails — leaving is always possible locally.</summary>
        void Leave();
    }
}
