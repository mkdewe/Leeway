using LitMotion;
using UnityEngine;

namespace Leeway.MainMenu.Navigation
{
    /// <summary>
    /// A full-screen page of the menu. Shows and hides itself with a short fade; it knows nothing
    /// about which page comes next — that is the navigator's job.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public abstract class ScreenView : MonoBehaviour
    {
        private const float FadeSeconds = 0.15f;

        [SerializeField] private MenuScreen _screen;

        private CanvasGroup _canvasGroup;
        private MotionHandle _fade;

        public MenuScreen Screen => _screen;
        public bool IsVisible { get; private set; }

        // Visibility is not touched here: a page that starts inactive only wakes up inside Show(), and
        // resetting it at that point would hide it again. The navigator sets the initial state.
        protected virtual void Awake() => _canvasGroup = GetComponent<CanvasGroup>();

        public void Show()
        {
            if (IsVisible) return;
            IsVisible = true;
            gameObject.SetActive(true);
            _canvasGroup.interactable = true;
            _canvasGroup.blocksRaycasts = true;
            FadeTo(1f, deactivateAtEnd: false);
            OnShown();
        }

        public void Hide()
        {
            if (!IsVisible) return;
            IsVisible = false;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
            FadeTo(0f, deactivateAtEnd: true);
        }

        public void SetVisibleImmediate(bool visible)
        {
            if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();
            _fade.TryCancel();
            IsVisible = visible;
            _canvasGroup.alpha = visible ? 1f : 0f;
            _canvasGroup.interactable = visible;
            _canvasGroup.blocksRaycasts = visible;
            gameObject.SetActive(visible);
        }

        /// <summary>Hook for views that refocus an input or reset scroll position when they appear.</summary>
        protected virtual void OnShown() { }

        private void FadeTo(float target, bool deactivateAtEnd)
        {
            _fade.TryCancel();
            var group = _canvasGroup;
            var go = gameObject;
            _fade = LMotion.Create(group.alpha, target, FadeSeconds)
                .WithEase(Ease.OutQuad)
                .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .WithOnComplete(() =>
                {
                    if (deactivateAtEnd && go != null) go.SetActive(false);
                })
                .Bind(alpha => group.alpha = alpha);
        }

        protected virtual void OnDestroy() => _fade.TryCancel();
    }
}
