using System;
using Leeway.Network.Session;
using Leeway.Online;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Leeway.MainMenu.Views.Widgets
{
    /// <summary>One row in the lobby browser.</summary>
    public sealed class LobbyListItemView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _nameLabel;
        [SerializeField] private TextMeshProUGUI _modeLabel;
        [SerializeField] private TextMeshProUGUI _playersLabel;
        [SerializeField] private Button _joinButton;

        private LobbyId _lobbyId;

        public event Action<LobbyId> JoinClicked;

        private void Awake() => _joinButton.onClick.AddListener(() => JoinClicked?.Invoke(_lobbyId));

        public void Bind(LobbyListing lobby, string modeName)
        {
            _lobbyId = lobby.Id;
            _nameLabel.text = lobby.Name;
            _modeLabel.text = modeName;
            _playersLabel.text = $"{lobby.Players}/{lobby.MaxPlayers}";
            _joinButton.interactable = !lobby.IsFull;
        }
    }
}
