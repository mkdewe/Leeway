using Leeway.Network.Session;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Leeway.MainMenu.Views.Widgets
{
    /// <summary>One player in the lobby room: name, host badge, ready state.</summary>
    public sealed class LobbyMemberItemView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _nameLabel;
        [SerializeField] private GameObject _hostBadge;
        [SerializeField] private TextMeshProUGUI _statusLabel;
        [SerializeField] private Image _statusDot;
        [SerializeField] private Color _readyColor = new(0.36f, 0.85f, 0.55f);
        [SerializeField] private Color _waitingColor = new(0.55f, 0.58f, 0.62f);

        public void Bind(LobbyMemberSnapshot member)
        {
            _nameLabel.text = member.IsLocal ? $"{member.DisplayName} <alpha=#88>(you)" : member.DisplayName;
            _hostBadge.SetActive(member.IsOwner);

            // The host has no ready toggle — pressing Start is their "ready".
            bool ready = member.IsOwner || member.IsReady;
            _statusLabel.text = member.IsOwner ? "Host" : member.IsReady ? "Ready" : "Not ready";
            _statusDot.color = ready ? _readyColor : _waitingColor;
        }
    }
}
