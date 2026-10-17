using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Leeway.MainMenu.Dialogs
{
    /// <summary>A modal message with one button — errors and notices.</summary>
    public sealed class MessageDialogView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _titleLabel;
        [SerializeField] private TextMeshProUGUI _bodyLabel;
        [SerializeField] private Button _closeButton;

        public event Action Closed;

        public bool IsVisible => gameObject.activeSelf;

        private void Awake() => _closeButton.onClick.AddListener(Close);

        public void Show(string title, string body)
        {
            _titleLabel.text = title;
            _bodyLabel.text = body;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            _closeButton.Select();
        }

        public void Close()
        {
            if (!IsVisible) return;
            gameObject.SetActive(false);
            Closed?.Invoke();
        }
    }
}
