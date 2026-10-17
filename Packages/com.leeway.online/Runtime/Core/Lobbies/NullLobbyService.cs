using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Leeway.Online
{
    /// <summary>The lobby service of a backend without lobbies: every action fails with <see cref="OnlineErrorCode.NotSupported"/>.</summary>
    public sealed class NullLobbyService : ILobbyService
    {
        public static readonly NullLobbyService Instance = new();

        private NullLobbyService() { }

        public ILobby Current => null;

        public event Action<ILobby> Entered { add { } remove { } }
        public event Action<LobbyId, LobbyLeaveReason> Left { add { } remove { } }
        public event Action<LobbyJoinRequest> JoinRequested { add { } remove { } }

        public UniTask<OnlineResult<ILobby>> CreateAsync(LobbyCreateOptions options, CancellationToken cancellationToken = default) =>
            UniTask.FromResult(OnlineResult<ILobby>.Failure(OnlineError.NotSupported("Lobbies")));

        public UniTask<OnlineResult<ILobby>> JoinAsync(LobbyId lobbyId, CancellationToken cancellationToken = default) =>
            UniTask.FromResult(OnlineResult<ILobby>.Failure(OnlineError.NotSupported("Lobbies")));

        public UniTask<OnlineResult<IReadOnlyList<LobbySummary>>> SearchAsync(LobbyQuery query, CancellationToken cancellationToken = default) =>
            UniTask.FromResult(OnlineResult<IReadOnlyList<LobbySummary>>.Failure(OnlineError.NotSupported("Lobby search")));

        public void Leave() { }
    }
}
