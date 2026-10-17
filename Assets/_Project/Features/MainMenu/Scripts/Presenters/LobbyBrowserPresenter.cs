using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Leeway.MainMenu.Navigation;
using Leeway.MainMenu.Views;
using Leeway.Network.Session;
using Leeway.Online;
using VContainer.Unity;

namespace Leeway.MainMenu.Presenters
{
    public sealed class LobbyBrowserPresenter : IStartable, IDisposable
    {
        // Index 0 of the mode filter means "any".
        private static readonly GameMode?[] ModeFilter = { null, GameMode.Coop, GameMode.Versus };

        private readonly LobbyBrowserScreenView _view;
        private readonly IScreenNavigator _navigator;
        private readonly IGameSession _session;
        private CancellationTokenSource _search;

        public LobbyBrowserPresenter(LobbyBrowserScreenView view, IScreenNavigator navigator, IGameSession session)
        {
            _view = view;
            _navigator = navigator;
            _session = session;
        }

        public void Start()
        {
            _view.SetModeOptions(new[] { "Any mode", MenuText.Mode(GameMode.Coop), MenuText.Mode(GameMode.Versus) });
            _view.Shown += Refresh;
            _view.RefreshClicked += Refresh;
            _view.FiltersChanged += Refresh;
            _view.JoinClicked += OnJoin;
            _view.BackClicked += OnBack;
        }

        private void Refresh() => RefreshAsync().Forget();

        private async UniTaskVoid RefreshAsync()
        {
            // A newer search supersedes the running one — fast filter clicks must not render stale results.
            _search?.Cancel();
            _search?.Dispose();
            _search = new CancellationTokenSource();
            var token = _search.Token;

            _view.SetSearching(true);
            var filter = new LobbyBrowserFilter(
                ModeFilter[Math.Clamp(_view.ModeIndex, 0, ModeFilter.Length - 1)],
                _view.OnlyWithFreeSlots,
                _view.SearchText);

            var result = await _session.SearchLobbiesAsync(filter, token);
            if (token.IsCancellationRequested) return;

            _view.SetSearching(false);
            if (result.IsSuccess) _view.ShowLobbies(result.Value, MenuText.Mode);
            else
            {
                _view.ShowLobbies(Array.Empty<LobbyListing>(), MenuText.Mode);
                _view.SetStatus(result.Error.Code == OnlineErrorCode.NotSupported
                    ? "Lobby search is not available on this platform."
                    : "Could not reach the lobby service. Try refreshing.");
            }
        }

        private void OnJoin(LobbyId lobbyId) => _session.JoinLobbyAsync(lobbyId).Forget();

        private void OnBack()
        {
            _search?.Cancel();
            _navigator.Back();
        }

        public void Dispose()
        {
            _search?.Cancel();
            _search?.Dispose();
            _view.Shown -= Refresh;
            _view.RefreshClicked -= Refresh;
            _view.FiltersChanged -= Refresh;
            _view.JoinClicked -= OnJoin;
            _view.BackClicked -= OnBack;
        }
    }
}
