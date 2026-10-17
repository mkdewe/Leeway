using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Leeway.Online;

namespace Leeway.Network.Session
{
    /// <summary>
    /// The game's one entry point for getting into a match: solo, hosting, browsing, joining, leaving.
    /// </summary>
    /// <remarks>
    /// A facade over three moving parts — the online platform (lobbies), the netcode connection and
    /// scene loading — so the menus never coordinate them themselves. Every call returns a result
    /// instead of throwing; state changes and lobby updates are also published on MessagePipe
    /// (<c>Leeway.Network.Messages</c>) for UI that just listens.
    /// </remarks>
    public interface IGameSession
    {
        SessionState State { get; }

        /// <summary>The current lobby, or null.</summary>
        LobbySnapshot Lobby { get; }

        /// <summary>The running platform's name and capabilities — null until it is ready.</summary>
        IOnlinePlatform Platform { get; }

        UniTask<OnlineResult> PlaySoloAsync(CancellationToken cancellationToken = default);

        UniTask<OnlineResult> CreateLobbyAsync(HostSettings settings, CancellationToken cancellationToken = default);

        UniTask<OnlineResult<IReadOnlyList<LobbyListing>>> SearchLobbiesAsync(LobbyBrowserFilter filter, CancellationToken cancellationToken = default);

        UniTask<OnlineResult> JoinLobbyAsync(LobbyId lobbyId, CancellationToken cancellationToken = default);

        /// <summary>Connects straight to a server by address, bypassing lobbies (LAN, dedicated servers).</summary>
        UniTask<OnlineResult> JoinDirectAsync(string address, ushort port, CancellationToken cancellationToken = default);

        /// <summary>Host only: starts the match for everyone in the lobby.</summary>
        UniTask<OnlineResult> StartMatchAsync(CancellationToken cancellationToken = default);

        OnlineResult SetReady(bool ready);

        OnlineResult SendChat(string text);

        OnlineResult OpenInviteDialog();

        /// <summary>Leaves whatever the player is in — lobby, match or both — and returns to <see cref="SessionState.Idle"/>.</summary>
        void Leave();

        /// <summary>Cancels the operation in progress (creating, joining, connecting).</summary>
        void CancelPending();
    }
}
