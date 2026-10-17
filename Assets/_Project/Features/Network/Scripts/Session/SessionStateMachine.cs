using System;
using System.Collections.Generic;

namespace Leeway.Network.Session
{
    /// <summary>
    /// The legal transitions between <see cref="SessionState"/>s, enforced in one place.
    /// </summary>
    /// <remarks>
    /// The session is driven from three directions at once — UI clicks, platform callbacks (invites,
    /// kicks) and netcode events (disconnects) — and most bugs in this kind of flow are one of them
    /// acting in a state where it makes no sense ("join" while already starting a match). Every
    /// change goes through <see cref="TryTransition"/>, so such a request is refused instead of
    /// corrupting the flow.
    /// </remarks>
    internal sealed class SessionStateMachine
    {
        private static readonly Dictionary<SessionState, SessionState[]> Allowed = new()
        {
            [SessionState.Initializing] = new[] { SessionState.Idle },
            [SessionState.Idle] = new[] { SessionState.CreatingLobby, SessionState.JoiningLobby, SessionState.StartingGame, SessionState.Connecting },
            [SessionState.CreatingLobby] = new[] { SessionState.InLobby, SessionState.Idle, SessionState.Leaving },
            [SessionState.JoiningLobby] = new[] { SessionState.InLobby, SessionState.Idle, SessionState.Leaving },
            [SessionState.InLobby] = new[] { SessionState.StartingGame, SessionState.Connecting, SessionState.JoiningLobby, SessionState.Leaving },
            [SessionState.StartingGame] = new[] { SessionState.InGame, SessionState.Leaving },
            [SessionState.Connecting] = new[] { SessionState.InGame, SessionState.Leaving },
            [SessionState.InGame] = new[] { SessionState.Leaving },
            [SessionState.Leaving] = new[] { SessionState.Idle },
        };

        public SessionState Current { get; private set; } = SessionState.Initializing;

        public event Action<SessionState, SessionState> Changed;

        public bool CanTransition(SessionState to) => Array.IndexOf(Allowed[Current], to) >= 0;

        public bool TryTransition(SessionState to)
        {
            if (to == Current || !CanTransition(to)) return false;

            var previous = Current;
            Current = to;
            Changed?.Invoke(previous, to);
            return true;
        }

        public bool IsIn(params SessionState[] states) => Array.IndexOf(states, Current) >= 0;
    }
}
