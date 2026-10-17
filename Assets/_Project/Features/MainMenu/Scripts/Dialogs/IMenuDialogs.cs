using System;
using System.Collections.Generic;

namespace Leeway.MainMenu.Dialogs
{
    /// <summary>Modal feedback for presenters: a busy overlay and a queue of messages.</summary>
    public interface IMenuDialogs
    {
        bool IsBlocking { get; }

        void ShowBusy(string message, Action onCancel = null);
        void HideBusy();

        /// <summary>Queued: a second message waits until the first is closed instead of replacing it.</summary>
        void ShowMessage(string title, string body);

        /// <summary>Closes the top dialog. False when there was none.</summary>
        bool CloseTop();
    }

    public sealed class MenuDialogs : IMenuDialogs, IDisposable
    {
        private readonly BusyOverlayView _busy;
        private readonly MessageDialogView _message;
        private readonly Queue<(string Title, string Body)> _pending = new();
        private Action _onCancel;

        public MenuDialogs(BusyOverlayView busy, MessageDialogView message)
        {
            _busy = busy;
            _message = message;
            _busy.Hide();
            _message.gameObject.SetActive(false);
            _busy.CancelClicked += OnCancelClicked;
            _message.Closed += ShowNextMessage;
        }

        public bool IsBlocking => _busy.IsVisible || _message.IsVisible;

        public void ShowBusy(string message, Action onCancel = null)
        {
            _onCancel = onCancel;
            _busy.Show(message, onCancel != null);
            if (_message.IsVisible) _message.transform.SetAsLastSibling();
        }

        public void HideBusy()
        {
            _onCancel = null;
            _busy.Hide();
        }

        public void ShowMessage(string title, string body)
        {
            if (_message.IsVisible)
            {
                _pending.Enqueue((title, body));
                return;
            }
            _message.Show(title, body);
        }

        public bool CloseTop()
        {
            if (_message.IsVisible)
            {
                _message.Close();
                return true;
            }
            if (_busy.IsVisible && _onCancel != null)
            {
                OnCancelClicked();
                return true;
            }
            return false;
        }

        private void OnCancelClicked() => _onCancel?.Invoke();

        private void ShowNextMessage()
        {
            if (_pending.Count == 0) return;
            var (title, body) = _pending.Dequeue();
            _message.Show(title, body);
        }

        public void Dispose()
        {
            _busy.CancelClicked -= OnCancelClicked;
            _message.Closed -= ShowNextMessage;
        }
    }
}
