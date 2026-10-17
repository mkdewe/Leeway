using System;
using Cysharp.Threading.Tasks;
using Leeway.MainMenu.Navigation;
using Leeway.MainMenu.Views;
using Leeway.Network.Messages;
using Leeway.Network.Session;
using Leeway.Online;
using MessagePipe;
using UnityEngine;
using VContainer.Unity;

namespace Leeway.MainMenu.Presenters
{
    /// <summary>Drives the title page: identity line, what the platform allows, and where each button leads.</summary>
    public sealed class MainScreenPresenter : IStartable, IDisposable
    {
        private readonly MainScreenView _view;
        private readonly IScreenNavigator _navigator;
        private readonly IGameSession _session;
        private IDisposable _subscription;

        public MainScreenPresenter(MainScreenView view, IScreenNavigator navigator, IGameSession session)
        {
            _view = view;
            _navigator = navigator;
            _session = session;
        }

        public void Start()
        {
            _view.PlaySoloClicked += OnPlaySolo;
            _view.CreateLobbyClicked += OnCreateLobby;
            _view.BrowseLobbiesClicked += OnBrowse;
            _view.JoinClicked += OnJoin;
            _view.QuitClicked += OnQuit;

            _subscription = GlobalMessagePipe.GetSubscriber<OnlinePlatformReadyMessage>().Subscribe(_ => Refresh());
            Refresh();
        }

        private void Refresh()
        {
            var platform = _session.Platform;
            if (platform == null)
            {
                _view.SetIdentity("Connecting…", string.Empty);
                _view.SetBusy(true);
                return;
            }

            _view.SetBusy(false);
            var caps = platform.Capabilities;
            _view.SetIdentity(
                platform.Id == PlatformId.Offline ? "Offline — join others by IP"
                : !caps.Has(OnlineCapabilities.RemoteLobbies) ? $"{platform.DisplayName} — lobbies visible on this machine only, join others by IP"
                : $"Online via {platform.DisplayName}",
                platform.LocalUser.DisplayName);

            _view.SetAvailability(
                canHost: caps.Has(OnlineCapabilities.Lobbies),
                canBrowse: caps.Has(OnlineCapabilities.LobbySearch));
        }

        private void OnPlaySolo() => _session.PlaySoloAsync().Forget();
        private void OnCreateLobby() => _navigator.Push(MenuScreen.CreateLobby);
        private void OnBrowse() => _navigator.Push(MenuScreen.LobbyBrowser);
        private void OnJoin() => _navigator.Push(MenuScreen.Join);

        private static void OnQuit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        public void Dispose()
        {
            _subscription?.Dispose();
            _view.PlaySoloClicked -= OnPlaySolo;
            _view.CreateLobbyClicked -= OnCreateLobby;
            _view.BrowseLobbiesClicked -= OnBrowse;
            _view.JoinClicked -= OnJoin;
            _view.QuitClicked -= OnQuit;
        }
    }
}
