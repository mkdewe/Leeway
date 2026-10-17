using System;
using Cysharp.Threading.Tasks;
using Leeway.MainMenu.Dialogs;
using Leeway.MainMenu.Navigation;
using Leeway.MainMenu.Views;
using Leeway.Network.Session;
using Leeway.Online;
using UnityEngine;
using VContainer.Unity;

namespace Leeway.MainMenu.Presenters
{
    public sealed class JoinPresenter : IStartable, IDisposable
    {
        private readonly JoinScreenView _view;
        private readonly IScreenNavigator _navigator;
        private readonly IGameSession _session;
        private readonly IMenuDialogs _dialogs;
        private readonly NetworkSessionSettings _settings;

        public JoinPresenter(JoinScreenView view, IScreenNavigator navigator, IGameSession session, IMenuDialogs dialogs, NetworkSessionSettings settings)
        {
            _view = view;
            _navigator = navigator;
            _session = session;
            _dialogs = dialogs;
            _settings = settings;
        }

        public void Start()
        {
            _view.SetDefaults("127.0.0.1", _settings.DefaultPort);
            _view.JoinByCodeClicked += OnJoinByCode;
            _view.PasteCodeClicked += OnPaste;
            _view.ConnectClicked += OnConnect;
            _view.BackClicked += OnBack;
            _navigator.Changed += OnScreenChanged;
        }

        private void OnScreenChanged(MenuScreen screen)
        {
            if (screen != MenuScreen.Join) return;
            var caps = _session.Platform?.Capabilities ?? OnlineCapabilities.None;
            _view.SetCodeSectionVisible(caps.Has(OnlineCapabilities.JoinByCode));
        }

        private void OnJoinByCode()
        {
            var code = new LobbyId(_view.Code);
            if (!code.IsValid)
            {
                _dialogs.ShowMessage("No lobby code", "Paste the code your friend sees in their lobby.");
                return;
            }
            _session.JoinLobbyAsync(code).Forget();
        }

        private void OnPaste() => _view.Code = GUIUtility.systemCopyBuffer?.Trim() ?? string.Empty;

        private void OnConnect()
        {
            if (!ushort.TryParse(_view.Port, out ushort port))
            {
                _dialogs.ShowMessage("Invalid port", "The port must be a number between 1 and 65535.");
                return;
            }
            _session.JoinDirectAsync(_view.Address, port).Forget();
        }

        private void OnBack() => _navigator.Back();

        public void Dispose()
        {
            _view.JoinByCodeClicked -= OnJoinByCode;
            _view.PasteCodeClicked -= OnPaste;
            _view.ConnectClicked -= OnConnect;
            _view.BackClicked -= OnBack;
            _navigator.Changed -= OnScreenChanged;
        }
    }
}
