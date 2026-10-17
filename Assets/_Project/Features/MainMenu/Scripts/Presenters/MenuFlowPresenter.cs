using System;
using Leeway.MainMenu.Dialogs;
using Leeway.MainMenu.Navigation;
using Leeway.Network.Messages;
using Leeway.Network.Session;
using MessagePipe;
using UnityEngine.InputSystem;
using VContainer.Unity;

namespace Leeway.MainMenu.Presenters
{
    /// <summary>
    /// The mediator between the session and the menu as a whole: which page is shown, when the busy
    /// overlay covers it, and where errors surface.
    /// </summary>
    /// <remarks>
    /// Page presenters only ask the session to do things; they never navigate on success
    /// themselves. The session can enter a lobby for reasons no button caused — an accepted Steam
    /// invite, a launch argument — and routing every state change through here keeps those paths
    /// identical to a click.
    /// </remarks>
    public sealed class MenuFlowPresenter : IStartable, ITickable, IDisposable
    {
        private readonly IGameSession _session;
        private readonly IScreenNavigator _navigator;
        private readonly IMenuDialogs _dialogs;
        private IDisposable _subscriptions;

        public MenuFlowPresenter(IGameSession session, IScreenNavigator navigator, IMenuDialogs dialogs)
        {
            _session = session;
            _navigator = navigator;
            _dialogs = dialogs;
        }

        public void Start()
        {
            var bag = DisposableBag.CreateBuilder();
            GlobalMessagePipe.GetSubscriber<SessionStateChangedMessage>().Subscribe(m => Apply(m.Current)).AddTo(bag);
            GlobalMessagePipe.GetSubscriber<SessionErrorMessage>().Subscribe(m => _dialogs.ShowMessage(m.Title, m.Description)).AddTo(bag);
            _subscriptions = bag.Build();

            Apply(_session.State);
        }

        private void Apply(SessionState state)
        {
            switch (state)
            {
                case SessionState.Initializing:
                    _dialogs.ShowBusy("Connecting to online services…");
                    break;
                case SessionState.CreatingLobby:
                    _dialogs.ShowBusy("Creating lobby…", _session.CancelPending);
                    break;
                case SessionState.JoiningLobby:
                    _dialogs.ShowBusy("Joining lobby…", _session.CancelPending);
                    break;
                case SessionState.Connecting:
                    _dialogs.ShowBusy("Connecting to the game…", _session.Leave);
                    break;
                case SessionState.StartingGame:
                case SessionState.InGame:
                    _dialogs.ShowBusy("Starting…");
                    break;
                case SessionState.Leaving:
                    _dialogs.ShowBusy("Leaving…");
                    break;
                case SessionState.InLobby:
                    _dialogs.HideBusy();
                    _navigator.Push(MenuScreen.LobbyRoom);
                    break;
                case SessionState.Idle:
                    _dialogs.HideBusy();
                    if (_navigator.Current == MenuScreen.LobbyRoom) _navigator.ResetTo(MenuScreen.Main);
                    break;
            }
        }

        public void Tick()
        {
            var keyboard = Keyboard.current;
            bool back = (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) ||
                        (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame);
            if (!back) return;

            if (_dialogs.CloseTop()) return;
            if (_dialogs.IsBlocking) return;

            // In the lobby room "back" means leaving the lobby — make that an explicit button press.
            if (_navigator.Current != MenuScreen.LobbyRoom) _navigator.Back();
        }

        public void Dispose() => _subscriptions?.Dispose();
    }
}
