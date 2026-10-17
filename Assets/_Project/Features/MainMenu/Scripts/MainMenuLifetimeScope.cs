using Leeway.MainMenu.Dialogs;
using Leeway.MainMenu.Navigation;
using Leeway.MainMenu.Presenters;
using Leeway.MainMenu.Views;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Leeway.MainMenu
{
    /// <summary>
    /// Composition root of the main menu scene: binds the scene's views to their presenters.
    /// </summary>
    /// <remarks>
    /// The session, platform and settings come from the parent <c>ProjectLifetimeScope</c>; this scope
    /// only adds what lives and dies with the menu.
    /// </remarks>
    public sealed class MainMenuLifetimeScope : LifetimeScope
    {
        [Header("Screens")]
        [SerializeField] private MainScreenView _mainScreen;
        [SerializeField] private CreateLobbyScreenView _createLobbyScreen;
        [SerializeField] private LobbyBrowserScreenView _lobbyBrowserScreen;
        [SerializeField] private JoinScreenView _joinScreen;
        [SerializeField] private LobbyRoomScreenView _lobbyRoomScreen;

        [Header("Dialogs")]
        [SerializeField] private BusyOverlayView _busyOverlay;
        [SerializeField] private MessageDialogView _messageDialog;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterComponent(_mainScreen);
            builder.RegisterComponent(_createLobbyScreen);
            builder.RegisterComponent(_lobbyBrowserScreen);
            builder.RegisterComponent(_joinScreen);
            builder.RegisterComponent(_lobbyRoomScreen);
            builder.RegisterComponent(_busyOverlay);
            builder.RegisterComponent(_messageDialog);

            builder.Register<IScreenNavigator>(_ => new ScreenNavigator(new ScreenView[]
            {
                _mainScreen, _createLobbyScreen, _lobbyBrowserScreen, _joinScreen, _lobbyRoomScreen,
            }), Lifetime.Scoped);
            builder.Register<MenuDialogs>(Lifetime.Scoped).As<IMenuDialogs>();

            builder.RegisterEntryPoint<MenuFlowPresenter>();
            builder.RegisterEntryPoint<MainScreenPresenter>();
            builder.RegisterEntryPoint<CreateLobbyPresenter>();
            builder.RegisterEntryPoint<LobbyBrowserPresenter>();
            builder.RegisterEntryPoint<JoinPresenter>();
            builder.RegisterEntryPoint<LobbyRoomPresenter>();
        }
    }
}
