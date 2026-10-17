using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Leeway.Online.Local
{
    internal sealed class LocalLobbyService : LobbyServiceBase
    {
        private readonly LocalLobbyHub _hub;
        private readonly LocalPlatform _platform;

        public LocalLobbyService(LocalLobbyHub hub, LocalPlatform platform)
        {
            _hub = hub;
            _platform = platform;
        }

        public override UniTask<OnlineResult<ILobby>> CreateAsync(LobbyCreateOptions options, CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested) return UniTask.FromResult(OnlineResult<ILobby>.Failure(OnlineError.Cancelled));
            if (options == null) return UniTask.FromResult(OnlineResult<ILobby>.Failure(OnlineErrorCode.InvalidArgument, "No lobby options."));

            Leave();
            var state = _hub.Create(options, _platform.LocalUser);
            return UniTask.FromResult(Attach(state));
        }

        public override UniTask<OnlineResult<ILobby>> JoinAsync(LobbyId lobbyId, CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested) return UniTask.FromResult(OnlineResult<ILobby>.Failure(OnlineError.Cancelled));
            if (!_hub.TryGet(lobbyId, out var state))
                return UniTask.FromResult(OnlineResult<ILobby>.Failure(OnlineErrorCode.NotFound, $"Lobby {lobbyId} does not exist."));
            if (CurrentLobby != null && CurrentLobby.Id == lobbyId)
                return UniTask.FromResult(OnlineResult<ILobby>.Success(CurrentLobby));

            Leave();
            return UniTask.FromResult(Attach(state));
        }

        public override UniTask<OnlineResult<IReadOnlyList<LobbySummary>>> SearchAsync(LobbyQuery query, CancellationToken cancellationToken = default)
        {
            query ??= LobbyQuery.Create().Build();
            IReadOnlyList<LobbySummary> found = _hub.Lobbies
                .Where(s => s.Visibility == LobbyVisibility.Public && s.Joinable)
                .Select(s => s.ToSummary())
                .Where(query.Matches)
                .Take(query.MaxResults)
                .ToList();
            return UniTask.FromResult(OnlineResult<IReadOnlyList<LobbySummary>>.Success(found));
        }

        protected override void NotifyBackendOfLeave(LobbyBase lobby)
        {
            var local = (LocalLobby)lobby;
            local.Detach();
            local.State.Remove(_platform.LocalUser.Id, LobbyLeaveReason.Left);
        }

        private OnlineResult<ILobby> Attach(LocalLobbyState state)
        {
            var error = state.TryAdmit(_platform.LocalUser);
            if (error.IsError) return OnlineResult<ILobby>.Failure(error);

            var lobby = new LocalLobby(state, _platform.LocalUser.Id);
            lobby.LocalUserRemoved += reason =>
            {
                lobby.Detach();
                Exit(reason);
            };
            Enter(lobby);
            return OnlineResult<ILobby>.Success(lobby);
        }

        /// <summary>Test/debug hook: simulate the platform delivering an invite.</summary>
        public void SimulateJoinRequest(LobbyId id, UserId from) =>
            RaiseJoinRequest(new LobbyJoinRequest(id, from, LobbyJoinRequestSource.PlatformOverlay));
    }
}
