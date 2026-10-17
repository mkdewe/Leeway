using System;
using Cysharp.Threading.Tasks;
using Leeway.MainMenu.Dialogs;
using Leeway.MainMenu.Views;
using Leeway.Network.Messages;
using Leeway.Network.Session;
using Leeway.Online;
using MessagePipe;
using UnityEngine;
using VContainer.Unity;

namespace Leeway.MainMenu.Presenters
{
    /// <summary>Renders <see cref="LobbySnapshot"/>s into the lobby room and forwards the player's actions to the session.</summary>
    public sealed class LobbyRoomPresenter : IStartable, IDisposable
    {
        private readonly LobbyRoomScreenView _view;
        private readonly IGameSession _session;
        private readonly IMenuDialogs _dialogs;
        private IDisposable _subscriptions;
        private LobbyId _shownLobby;

        public LobbyRoomPresenter(LobbyRoomScreenView view, IGameSession session, IMenuDialogs dialogs)
        {
            _view = view;
            _session = session;
            _dialogs = dialogs;
        }

        public void Start()
        {
            _view.CopyCodeClicked += OnCopyCode;
            _view.ReadyClicked += OnReady;
            _view.StartClicked += OnStart;
            _view.InviteClicked += OnInvite;
            _view.LeaveClicked += OnLeave;
            _view.ChatSubmitted += OnChatSubmitted;

            var bag = DisposableBag.CreateBuilder();
            GlobalMessagePipe.GetSubscriber<LobbySnapshotChangedMessage>().Subscribe(m => Render(m.Lobby)).AddTo(bag);
            GlobalMessagePipe.GetSubscriber<LobbyChatReceivedMessage>().Subscribe(OnChat).AddTo(bag);
            _subscriptions = bag.Build();

            Render(_session.Lobby);
        }

        private void Render(LobbySnapshot lobby)
        {
            if (lobby == null) return;

            if (lobby.Id != _shownLobby)
            {
                _shownLobby = lobby.Id;
                _view.ClearChat();
            }

            var caps = _session.Platform?.Capabilities ?? OnlineCapabilities.None;
            _view.SetHeader(lobby.Name, $"{MenuText.Mode(lobby.Mode)} · {MenuText.Visibility(lobby.Visibility)}", lobby.JoinCode);
            _view.SetMembers(lobby.Members, lobby.MaxMembers);
            _view.SetChatAvailable(caps.Has(OnlineCapabilities.LobbyChat));
            _view.SetActions(
                isHost: lobby.IsLocalOwner,
                localReady: lobby.LocalIsReady,
                canStart: lobby.CanStart,
                startHint: lobby.AllGuestsReady ? string.Empty : "Waiting for everyone to be ready",
                canInvite: caps.Has(OnlineCapabilities.InviteDialog) && (_session.Platform?.Social.CanOpenInviteDialog ?? false));
        }

        private void OnChat(LobbyChatReceivedMessage message)
        {
            string name = message.IsLocal ? "You" : message.Message.Sender.DisplayName;
            _view.AppendChat($"<b>{Escape(name)}:</b> {Escape(message.Message.Text)}");
        }

        /// <summary>Chat is player input: never let it inject rich-text tags into the log.</summary>
        private static string Escape(string text) => text.Replace("<", "<noparse><</noparse>");

        private void OnCopyCode()
        {
            if (_session.Lobby == null) return;
            GUIUtility.systemCopyBuffer = _session.Lobby.JoinCode;
            _view.AppendChat("<i><alpha=#88>Lobby code copied to clipboard.</i>");
        }

        private void OnReady()
        {
            if (_session.Lobby == null) return;
            var result = _session.SetReady(!_session.Lobby.LocalIsReady);
            if (!result.IsSuccess) Debug.LogWarning($"[Lobby] Ready toggle failed: {result.Error}");
        }

        private void OnStart() => _session.StartMatchAsync().Forget();

        private void OnInvite()
        {
            var result = _session.OpenInviteDialog();
            if (!result.IsSuccess)
                _dialogs.ShowMessage("Cannot open invites", "The platform overlay is not available. Share the lobby code instead.");
        }

        private void OnLeave() => _session.Leave();

        private void OnChatSubmitted(string text)
        {
            var result = _session.SendChat(text);
            if (!result.IsSuccess) Debug.LogWarning($"[Lobby] Chat failed: {result.Error}");
        }

        public void Dispose()
        {
            _subscriptions?.Dispose();
            _view.CopyCodeClicked -= OnCopyCode;
            _view.ReadyClicked -= OnReady;
            _view.StartClicked -= OnStart;
            _view.InviteClicked -= OnInvite;
            _view.LeaveClicked -= OnLeave;
            _view.ChatSubmitted -= OnChatSubmitted;
        }
    }
}
