using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Leeway.Network.Messages;
using Leeway.Network.Netcode;
using Leeway.Network.Online;
using Leeway.Online;
using MessagePipe;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer.Unity;

namespace Leeway.Network.Session
{
    /// <summary>
    /// <see cref="IGameSession"/>: the lobby → match flow on top of an <see cref="IOnlinePlatform"/>
    /// and an <see cref="INetcodeConnector"/>.
    /// </summary>
    /// <remarks>
    /// <para><b>The flow.</b> Lobbies are pure platform state — nobody is connected while gathering.
    /// When the host starts, it brings the server up, publishes its endpoints into the lobby and flips
    /// <see cref="GameLobbyKeys.Phase"/> to <see cref="LobbyPhase.InGame"/>; every member sees the data
    /// change and dials the host. A player who joins a lobby already in game dials straight away, so
    /// late joining and invites into a running match work the same way.</para>
    ///
    /// <para><b>Why connect late.</b> Holding netcode connections open in the lobby would mean every
    /// player hitting NAT/relay problems before the match even exists, and the host paying for a
    /// server nobody plays on. Lobby data is cheap and always reachable.</para>
    ///
    /// <para><b>Concurrency.</b> Every operation passes through <see cref="SessionStateMachine"/>, so
    /// a request that makes no sense in the current state (a second click, an invite during a match)
    /// is refused with a result instead of interleaving with the one in flight.</para>
    /// </remarks>
    public sealed class GameSession : IGameSession, IInitializable, IDisposable
    {
        private const int SoloPlayers = 1;

        private readonly IOnlinePlatformAccessor _online;
        private readonly INetcodeConnector _netcode;
        private readonly NetworkSessionSettings _settings;
        private readonly SessionStateMachine _state = new();

        private CancellationTokenSource _pending;
        private ILobby _lobby;

        public GameSession(IOnlinePlatformAccessor online, INetcodeConnector netcode, NetworkSessionSettings settings)
        {
            _online = online;
            _netcode = netcode;
            _settings = settings;
        }

        public SessionState State => _state.Current;
        public LobbySnapshot Lobby { get; private set; }
        public IOnlinePlatform Platform => _online.Platform;

        private ILobbyService Lobbies => _online.Platform?.Lobbies ?? NullLobbyService.Instance;
        private UserId LocalUser => _online.Platform?.LocalUser.Id ?? UserId.None;

        public void Initialize()
        {
            _state.Changed += (previous, current) =>
            {
                Debug.Log($"[Session] {previous} → {current}");
                GlobalMessagePipe.GetPublisher<SessionStateChangedMessage>().Publish(new SessionStateChangedMessage(previous, current));
            };

            _netcode.ConnectionLost += OnConnectionLost;

            if (_online.IsReady) OnPlatformReady(_online.Platform);
            else _online.Ready += OnPlatformReady;
        }

        private void OnPlatformReady(IOnlinePlatform platform)
        {
            _online.Ready -= OnPlatformReady;

            platform.Lobbies.Left += OnLobbyLeft;
            platform.Lobbies.JoinRequested += OnJoinRequested;
            _state.TryTransition(SessionState.Idle);
        }

        // ---- Commands ------------------------------------------------------------------------

        public async UniTask<OnlineResult> PlaySoloAsync(CancellationToken cancellationToken = default)
        {
            if (!_state.TryTransition(SessionState.StartingGame)) return Refused("start a solo game");

            using var scope = BeginPending(cancellationToken, out var token);
            var host = await _netcode.StartHostAsync(SoloPlayers, token);
            if (!host.IsSuccess) return Fail("Could not start the game", "The local server failed to start.", host.Error);

            _state.TryTransition(SessionState.InGame);
            _netcode.LoadSceneForAll(_settings.GameplayScene);
            return OnlineResult.Success;
        }

        public async UniTask<OnlineResult> CreateLobbyAsync(HostSettings settings, CancellationToken cancellationToken = default)
        {
            if (!_state.TryTransition(SessionState.CreatingLobby)) return Refused("create a lobby");

            int maxPlayers = Mathf.Clamp(settings.MaxPlayers, 1, _settings.MaxPlayersLimit);
            var options = new LobbyCreateOptions(settings.LobbyName, maxPlayers, settings.Visibility)
                .WithProtocolVersion(_settings.ProtocolVersion)
                .WithData(GameLobbyKeys.Mode, settings.Mode.ToString())
                .WithData(GameLobbyKeys.Phase, LobbyPhase.Gathering.ToString());

            using var scope = BeginPending(cancellationToken, out var token);
            var created = await Lobbies.CreateAsync(options, token);

            // Leave() ran while we were waiting: the player no longer wants this lobby.
            if (_state.Current != SessionState.CreatingLobby)
            {
                if (created.IsSuccess) Lobbies.Leave();
                return OnlineError.Cancelled;
            }
            if (!created.IsSuccess)
            {
                _state.TryTransition(SessionState.Idle);
                return Report("Could not create the lobby", created.Error);
            }

            AttachLobby(created.Value);
            _state.TryTransition(SessionState.InLobby);
            return OnlineResult.Success;
        }

        public async UniTask<OnlineResult<IReadOnlyList<LobbyListing>>> SearchLobbiesAsync(LobbyBrowserFilter filter, CancellationToken cancellationToken = default)
        {
            var builder = LobbyQuery.Create()
                .WithProtocolVersion(_settings.ProtocolVersion)
                .WhereEquals(GameLobbyKeys.Phase, LobbyPhase.Gathering.ToString())
                .Within(LobbySearchDistance.Worldwide);
            if (filter.Mode.HasValue) builder.WhereEquals(GameLobbyKeys.Mode, filter.Mode.Value.ToString());
            if (filter.OnlyWithFreeSlots) builder.WithFreeSlots(1);

            var found = await Lobbies.SearchAsync(builder.Build(), cancellationToken);
            if (!found.IsSuccess) return OnlineResult<IReadOnlyList<LobbyListing>>.Failure(found.Error);

            var listings = new List<LobbyListing>(found.Value.Count);
            foreach (var summary in found.Value)
            {
                var listing = new LobbyListing(summary);
                if (!string.IsNullOrWhiteSpace(filter.NameContains) &&
                    listing.Name.IndexOf(filter.NameContains.Trim(), StringComparison.OrdinalIgnoreCase) < 0) continue;
                listings.Add(listing);
            }
            return OnlineResult<IReadOnlyList<LobbyListing>>.Success(listings);
        }

        public async UniTask<OnlineResult> JoinLobbyAsync(LobbyId lobbyId, CancellationToken cancellationToken = default)
        {
            if (!lobbyId.IsValid) return OnlineResult.Failure(OnlineErrorCode.InvalidArgument, "Empty lobby code.");
            if (!_state.TryTransition(SessionState.JoiningLobby)) return Refused("join a lobby");

            DetachLobby(leave: true);

            using var scope = BeginPending(cancellationToken, out var token);
            var joined = await Lobbies.JoinAsync(lobbyId, token);

            if (_state.Current != SessionState.JoiningLobby)
            {
                if (joined.IsSuccess) Lobbies.Leave();
                return OnlineError.Cancelled;
            }
            if (!joined.IsSuccess)
            {
                _state.TryTransition(SessionState.Idle);
                return Report("Could not join the lobby", joined.Error);
            }

            var lobby = joined.Value;
            string protocol = lobby.GetData(LobbyDataKeys.ProtocolVersion);
            if (protocol != _settings.ProtocolVersion)
            {
                Lobbies.Leave();
                _state.TryTransition(SessionState.Idle);
                return Fail("Incompatible version",
                    $"This lobby runs version {(string.IsNullOrEmpty(protocol) ? "unknown" : protocol)}, you have {_settings.ProtocolVersion}.",
                    new OnlineError(OnlineErrorCode.VersionMismatch, $"Lobby protocol '{protocol}' ≠ '{_settings.ProtocolVersion}'."));
            }

            AttachLobby(lobby);
            _state.TryTransition(SessionState.InLobby);

            // Joined a match already in progress (late join or invite) — dial the host straight away.
            if (Lobby.Phase == LobbyPhase.InGame) ConnectToLobbyHostAsync().Forget();
            return OnlineResult.Success;
        }

        public async UniTask<OnlineResult> JoinDirectAsync(string address, ushort port, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(address)) return OnlineResult.Failure(OnlineErrorCode.InvalidArgument, "Empty address.");
            if (!_state.TryTransition(SessionState.Connecting)) return Refused("connect");

            var endpoint = new NetworkEndpoint(NetworkEndpoint.UdpScheme, address.Trim(), port == 0 ? _settings.DefaultPort : port);

            using var scope = BeginPending(cancellationToken, out var token);
            var connected = await _netcode.ConnectAsync(new[] { endpoint }, _settings.ConnectTimeout, token);
            if (!connected.IsSuccess) return Fail("Could not connect", $"No game server answered at {endpoint.Address}:{endpoint.Port}.", connected.Error);

            _state.TryTransition(SessionState.InGame);
            return OnlineResult.Success;
        }

        public async UniTask<OnlineResult> StartMatchAsync(CancellationToken cancellationToken = default)
        {
            if (_lobby == null || Lobby == null) return OnlineError.NotInLobby;
            if (!_lobby.IsLocalUserOwner) return OnlineError.NotOwner;
            if (!Lobby.AllGuestsReady) return OnlineResult.Failure(OnlineErrorCode.InvalidArgument, "Not everyone is ready.");
            if (!_state.TryTransition(SessionState.StartingGame)) return Refused("start the match");

            using var scope = BeginPending(cancellationToken, out var token);
            var host = await _netcode.StartHostAsync(_lobby.MaxMembers, token);
            if (!host.IsSuccess) return Fail("Could not start the match", "The server failed to start.", host.Error);

            var endpoints = _netcode.GetHostEndpoints();
            if (endpoints.Count == 0)
                return Fail("Could not start the match", "The server is not reachable by any transport other players could use.",
                    new OnlineError(OnlineErrorCode.ConnectionFailed, "No host endpoints."));

            // Endpoints first, phase last: the phase flip is what makes clients dial, so the address
            // must already be there when they read it.
            _lobby.Publish(endpoints);
            _lobby.SetData(GameLobbyKeys.Phase, LobbyPhase.InGame.ToString());

            _state.TryTransition(SessionState.InGame);
            _netcode.LoadSceneForAll(_settings.GameplayScene);
            return OnlineResult.Success;
        }

        public OnlineResult SetReady(bool ready) =>
            _lobby?.SetLocalMemberData(GameLobbyKeys.Ready, ready ? "1" : "0") ?? OnlineError.NotInLobby;

        public OnlineResult SendChat(string text) => _lobby?.SendChatMessage(text) ?? OnlineError.NotInLobby;

        public OnlineResult OpenInviteDialog() =>
            _lobby == null ? OnlineError.NotInLobby : (_online.Platform?.Social ?? NullSocialService.Instance).OpenInviteDialog(_lobby.Id);

        public void Leave()
        {
            if (_state.Current is SessionState.Idle or SessionState.Initializing or SessionState.Leaving) return;

            CancelPending();
            bool wasInGame = _state.Current is SessionState.InGame or SessionState.StartingGame or SessionState.Connecting;

            // Leaving goes through its own state so the "connection lost" and "lobby left" callbacks
            // fired by the teardown below recognise it as intentional.
            if (!_state.TryTransition(SessionState.Leaving)) return;

            _netcode.Stop();
            DetachLobby(leave: true);
            _state.TryTransition(SessionState.Idle);

            if (wasInGame) ReturnToMainMenu();
        }

        public void CancelPending() => _pending?.Cancel();

        public void Dispose()
        {
            CancelPending();
            _netcode.ConnectionLost -= OnConnectionLost;
            _online.Ready -= OnPlatformReady;
            if (_online.Platform != null)
            {
                _online.Platform.Lobbies.Left -= OnLobbyLeft;
                _online.Platform.Lobbies.JoinRequested -= OnJoinRequested;
            }
            DetachLobby(leave: true);
        }

        // ---- Lobby wiring --------------------------------------------------------------------

        private void AttachLobby(ILobby lobby)
        {
            _lobby = lobby;
            lobby.MemberJoined += OnMemberChanged;
            lobby.MemberLeft += OnMemberLeft;
            lobby.MemberDataChanged += OnMemberChanged;
            lobby.OwnerChanged += OnOwnerChanged;
            lobby.DataChanged += OnLobbyDataChanged;
            lobby.ChatMessageReceived += OnChatMessage;

            // Everyone starts unready — a stale flag from a previous lobby on the same account must not carry over.
            lobby.SetLocalMemberData(GameLobbyKeys.Ready, "0");
            PublishSnapshot();
        }

        private void DetachLobby(bool leave)
        {
            if (_lobby == null) return;

            var lobby = _lobby;
            _lobby = null;
            lobby.MemberJoined -= OnMemberChanged;
            lobby.MemberLeft -= OnMemberLeft;
            lobby.MemberDataChanged -= OnMemberChanged;
            lobby.OwnerChanged -= OnOwnerChanged;
            lobby.DataChanged -= OnLobbyDataChanged;
            lobby.ChatMessageReceived -= OnChatMessage;

            if (leave && Lobbies.Current == lobby) Lobbies.Leave();

            Lobby = null;
            GlobalMessagePipe.GetPublisher<LobbySnapshotChangedMessage>().Publish(new LobbySnapshotChangedMessage(null));
        }

        private void PublishSnapshot()
        {
            if (_lobby == null) return;
            Lobby = LobbySnapshot.From(_lobby, LocalUser);
            GlobalMessagePipe.GetPublisher<LobbySnapshotChangedMessage>().Publish(new LobbySnapshotChangedMessage(Lobby));
        }

        private void OnMemberChanged(LobbyMember _) => PublishSnapshot();
        private void OnMemberLeft(LobbyMember _, LobbyLeaveReason __) => PublishSnapshot();
        private void OnOwnerChanged(UserId _) => PublishSnapshot();

        private void OnLobbyDataChanged()
        {
            PublishSnapshot();

            // The host flipped the lobby to InGame — that is the guests' signal to dial in.
            if (Lobby != null && Lobby.Phase == LobbyPhase.InGame && !Lobby.IsLocalOwner && _state.Current == SessionState.InLobby)
                ConnectToLobbyHostAsync().Forget();
        }

        private void OnChatMessage(LobbyChatMessage message) =>
            GlobalMessagePipe.GetPublisher<LobbyChatReceivedMessage>().Publish(new LobbyChatReceivedMessage(message, message.Sender.Id == LocalUser));

        private async UniTaskVoid ConnectToLobbyHostAsync()
        {
            if (_lobby == null || !_state.TryTransition(SessionState.Connecting)) return;

            var endpoints = _lobby.ReadEndpoints();
            using var scope = BeginPending(CancellationToken.None, out var token);
            var connected = await _netcode.ConnectAsync(endpoints, _settings.ConnectTimeout, token);
            if (!connected.IsSuccess)
            {
                Fail("Could not join the match", "The host's game server did not answer.", connected.Error);
                return;
            }

            _state.TryTransition(SessionState.InGame);
        }

        // ---- Platform / netcode callbacks ----------------------------------------------------

        private void OnLobbyLeft(LobbyId lobbyId, LobbyLeaveReason reason)
        {
            if (_lobby == null || _lobby.Id != lobbyId) return;

            DetachLobby(leave: false);

            // In a match the lobby is only a beacon; losing it does not end the game.
            if (_state.Current != SessionState.InLobby) return;

            _state.TryTransition(SessionState.Leaving);
            _state.TryTransition(SessionState.Idle);

            if (reason != LobbyLeaveReason.Left)
                Publish("You left the lobby", reason == LobbyLeaveReason.Kicked ? "You were removed from the lobby." : "The lobby was closed.",
                    new OnlineError(OnlineErrorCode.NotInLobby, reason.ToString()));
        }

        private void OnJoinRequested(LobbyJoinRequest request)
        {
            if (_state.Current is SessionState.Idle or SessionState.InLobby)
            {
                JoinLobbyAsync(request.LobbyId).Forget();
                return;
            }

            Publish("Invite ignored", "Leave the current game before accepting an invite.",
                new OnlineError(OnlineErrorCode.AlreadyInLobby, $"Join request for {request.LobbyId} in state {_state.Current}."));
        }

        private void OnConnectionLost()
        {
            if (_state.Current is not (SessionState.InGame or SessionState.Connecting)) return;

            Leave();
            Publish("Disconnected", "The connection to the game server was lost.", new OnlineError(OnlineErrorCode.ConnectionFailed, "Client connection stopped."));
        }

        // ---- Helpers -------------------------------------------------------------------------

        private void ReturnToMainMenu()
        {
            if (string.IsNullOrEmpty(_settings.MainMenuScene) || SceneManager.GetActiveScene().name == _settings.MainMenuScene) return;
            SceneManager.LoadScene(_settings.MainMenuScene);
        }

        /// <summary>Tracks the operation in flight so <see cref="CancelPending"/> can stop it.</summary>
        private IDisposable BeginPending(CancellationToken external, out CancellationToken token)
        {
            _pending?.Dispose();
            var source = CancellationTokenSource.CreateLinkedTokenSource(external);
            _pending = source;
            token = source.Token;
            return new PendingScope(this, source);
        }

        private sealed class PendingScope : IDisposable
        {
            private readonly GameSession _owner;
            private readonly CancellationTokenSource _source;

            public PendingScope(GameSession owner, CancellationTokenSource source)
            {
                _owner = owner;
                _source = source;
            }

            public void Dispose()
            {
                if (_owner._pending == _source) _owner._pending = null;
                _source.Dispose();
            }
        }

        /// <summary>A failed step after the session left Idle: tear everything down, tell the player, return the error.</summary>
        private OnlineResult Fail(string title, string description, OnlineError error)
        {
            if (error.Code != OnlineErrorCode.Cancelled)
                Publish(title, description, error);

            if (_state.Current is not (SessionState.Idle or SessionState.Leaving))
            {
                bool wasInGame = _state.Current == SessionState.InGame;
                _state.TryTransition(SessionState.Leaving);
                _netcode.Stop();
                DetachLobby(leave: true);
                _state.TryTransition(SessionState.Idle);
                if (wasInGame) ReturnToMainMenu();
            }
            return OnlineResult.Failure(error);
        }

        private OnlineResult Report(string title, OnlineError error)
        {
            if (error.Code != OnlineErrorCode.Cancelled) Publish(title, Describe(error), error);
            return OnlineResult.Failure(error);
        }

        private OnlineResult Refused(string what) =>
            OnlineResult.Failure(OnlineErrorCode.InvalidArgument, $"Cannot {what} while {_state.Current}.");

        private static void Publish(string title, string description, OnlineError error)
        {
            Debug.LogWarning($"[Session] {title}: {error}");
            GlobalMessagePipe.GetPublisher<SessionErrorMessage>().Publish(new SessionErrorMessage(title, description, error));
        }

        /// <summary>Player-facing text for the codes a player can actually do something about.</summary>
        internal static string Describe(OnlineError error) => error.Code switch
        {
            OnlineErrorCode.LobbyFull => "The lobby is full.",
            OnlineErrorCode.NotFound => "The lobby no longer exists.",
            OnlineErrorCode.AccessDenied => "You are not allowed to join this lobby.",
            OnlineErrorCode.Timeout => "The platform did not answer in time. Check your connection and try again.",
            OnlineErrorCode.PlatformUnavailable => "The online platform is not available. Is the Steam client running?",
            OnlineErrorCode.NotSupported => "This is not available on the current platform.",
            OnlineErrorCode.VersionMismatch => "The game versions do not match.",
            OnlineErrorCode.InvalidArgument => "That lobby code is not valid.",
            _ => "Something went wrong. Please try again.",
        };
    }
}
