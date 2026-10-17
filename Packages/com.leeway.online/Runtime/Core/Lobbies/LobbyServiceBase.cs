using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Leeway.Online
{
    /// <summary>
    /// Shared lifecycle for every backend's lobby service: the single current lobby, the
    /// entered/left events and the join-request replay queue.
    /// </summary>
    public abstract class LobbyServiceBase : ILobbyService
    {
        private readonly Queue<LobbyJoinRequest> _pendingJoinRequests = new();
        private Action<LobbyJoinRequest> _joinRequested;
        private LobbyBase _current;

        public ILobby Current => _current;

        protected LobbyBase CurrentLobby => _current;

        public event Action<ILobby> Entered;
        public event Action<LobbyId, LobbyLeaveReason> Left;

        public event Action<LobbyJoinRequest> JoinRequested
        {
            add
            {
                _joinRequested += value;
                while (_pendingJoinRequests.Count > 0 && _joinRequested != null)
                    _joinRequested.Invoke(_pendingJoinRequests.Dequeue());
            }
            remove => _joinRequested -= value;
        }

        public abstract UniTask<OnlineResult<ILobby>> CreateAsync(LobbyCreateOptions options, CancellationToken cancellationToken = default);
        public abstract UniTask<OnlineResult<ILobby>> JoinAsync(LobbyId lobbyId, CancellationToken cancellationToken = default);
        public abstract UniTask<OnlineResult<IReadOnlyList<LobbySummary>>> SearchAsync(LobbyQuery query, CancellationToken cancellationToken = default);

        public void Leave()
        {
            if (_current == null) return;
            NotifyBackendOfLeave(_current);
            Exit(LobbyLeaveReason.Left);
        }

        /// <summary>Tell the SDK the local user is leaving (it will not report it back).</summary>
        protected abstract void NotifyBackendOfLeave(LobbyBase lobby);

        /// <summary>Makes <paramref name="lobby"/> current, leaving any previous one first.</summary>
        protected void Enter(LobbyBase lobby)
        {
            if (_current != null && _current.Id != lobby.Id) Leave();
            _current = lobby;
            Entered?.Invoke(lobby);
        }

        /// <summary>The local user is out — by choice, kick or the lobby closing.</summary>
        protected void Exit(LobbyLeaveReason reason)
        {
            var lobby = _current;
            if (lobby == null) return;

            _current = null;
            lobby.Deactivate();
            Left?.Invoke(lobby.Id, reason);
        }

        protected void RaiseJoinRequest(LobbyJoinRequest request)
        {
            if (_joinRequested == null) _pendingJoinRequests.Enqueue(request);
            else _joinRequested.Invoke(request);
        }
    }
}
