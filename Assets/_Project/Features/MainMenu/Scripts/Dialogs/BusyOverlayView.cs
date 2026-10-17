using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Leeway.MainMenu.Dialogs
{
    /// <summary>A modal "working on it" layer over the whole menu, optionally with Cancel.</summary>
    public sealed class BusyOverlayView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _messageLabel;
        [SerializeField] private Button _cancelButton;
        [SerializeField] private RectTransform _spinner;
        [SerializeField] private float _spinnerDegreesPerSecond = 270f;

        public event Action CancelClicked;

        public bool IsVisible => gameObject.activeSelf;

        private void Awake() => _cancelButton.onClick.AddListener(() => CancelClicked?.Invoke());

        public void Show(string message, bool cancellable)
        {
            _messageLabel.text = message;
            _cancelButton.gameObject.SetActive(cancellable);
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        public void Hide() => gameObject.SetActive(false);

        private void Update()
        {
            if (_spinner != null) _spinner.Rotate(0f, 0f, -_spinnerDegreesPerSecond * Time.unscaledDeltaTime);
        }
    }
}
