using System;
using Leeway.MainMenu.Navigation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Leeway.MainMenu.Views
{
    /// <summary>The title page: play solo, host, browse, join, quit — plus who and where you are signed in.</summary>
    public sealed class MainScreenView : ScreenView
    {
        [SerializeField] private Button _playSoloButton;
        [SerializeField] private Button _createLobbyButton;
        [SerializeField] private Button _browseLobbiesButton;
        [SerializeField] private Button _joinButton;
        [SerializeField] private Button _quitButton;
        [SerializeField] private TextMeshProUGUI _platformLabel;
        [SerializeField] private TextMeshProUGUI _playerLabel;

        public event Action PlaySoloClicked;
        public event Action CreateLobbyClicked;
        public event Action BrowseLobbiesClicked;
        public event Action JoinClicked;
        public event Action QuitClicked;

        protected override void Awake()
        {
            base.Awake();
            _playSoloButton.onClick.AddListener(() => PlaySoloClicked?.Invoke());
            _createLobbyButton.onClick.AddListener(() => CreateLobbyClicked?.Invoke());
            _browseLobbiesButton.onClick.AddListener(() => BrowseLobbiesClicked?.Invoke());
            _joinButton.onClick.AddListener(() => JoinClicked?.Invoke());
            _quitButton.onClick.AddListener(() => QuitClicked?.Invoke());
        }

        public void SetIdentity(string platform, string player)
        {
            _platformLabel.text = platform;
            _playerLabel.text = player;
        }

        /// <summary>Greys out what the running platform cannot do instead of letting the player hit an error.</summary>
        public void SetAvailability(bool canHost, bool canBrowse)
        {
            _createLobbyButton.interactable = canHost;
            _browseLobbiesButton.interactable = canBrowse;
        }

        /// <summary>While the platform is still starting, nothing but Quit makes sense.</summary>
        public void SetBusy(bool busy)
        {
            _playSoloButton.interactable = !busy;
            _joinButton.interactable = !busy;
            if (busy)
            {
                _createLobbyButton.interactable = false;
                _browseLobbiesButton.interactable = false;
            }
        }
    }
}
