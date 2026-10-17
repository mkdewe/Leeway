using System;
using System.Linq;
using Cysharp.Threading.Tasks;
using Leeway.MainMenu.Navigation;
using Leeway.MainMenu.Views;
using Leeway.Network.Session;
using VContainer.Unity;

namespace Leeway.MainMenu.Presenters
{
    public sealed class CreateLobbyPresenter : IStartable, IDisposable
    {
        private readonly CreateLobbyScreenView _view;
        private readonly IScreenNavigator _navigator;
        private readonly IGameSession _session;
        private readonly NetworkSessionSettings _settings;

        public CreateLobbyPresenter(CreateLobbyScreenView view, IScreenNavigator navigator, IGameSession session, NetworkSessionSettings settings)
        {
            _view = view;
            _navigator = navigator;
            _session = session;
            _settings = settings;
        }

        public void Start()
        {
            _view.SetOptions(
                MenuText.HostableModes.Select(MenuText.Mode).ToArray(),
                MenuText.Visibilities.Select(MenuText.Visibility).ToArray(),
                minPlayers: 2,
                maxPlayers: _settings.MaxPlayersLimit);
            _view.SetValues("New lobby", 0, 0, _settings.DefaultMaxPlayers);

            _view.CreateClicked += OnCreate;
            _view.BackClicked += OnBack;
            _navigator.Changed += OnScreenChanged;
        }

        private void OnScreenChanged(MenuScreen screen)
        {
            // Prefill with the player's name the first time it is known — "Ania's lobby" reads better
            // in a browser than twenty identical "New lobby" rows.
            if (screen != MenuScreen.CreateLobby || _session.Platform == null) return;
            if (_view.LobbyName == "New lobby")
                _view.SetValues($"{_session.Platform.LocalUser.DisplayName}'s lobby", _view.ModeIndex, _view.VisibilityIndex, _view.MaxPlayers);
        }

        private void OnCreate()
        {
            var settings = new HostSettings(
                _view.LobbyName,
                MenuText.HostableModes[Math.Clamp(_view.ModeIndex, 0, MenuText.HostableModes.Length - 1)],
                _view.MaxPlayers,
                MenuText.Visibilities[Math.Clamp(_view.VisibilityIndex, 0, MenuText.Visibilities.Length - 1)]);

            // Navigation to the room happens when the session reports InLobby (MenuFlowPresenter).
            _session.CreateLobbyAsync(settings).Forget();
        }

        private void OnBack() => _navigator.Back();

        public void Dispose()
        {
            _view.CreateClicked -= OnCreate;
            _view.BackClicked -= OnBack;
            _navigator.Changed -= OnScreenChanged;
        }
    }
}
