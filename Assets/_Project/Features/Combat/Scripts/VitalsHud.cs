using System;
using Leeway.CreatureEditor;
using Leeway.World;
using MessagePipe;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Leeway.Combat
{
    /// <summary>
    /// The bars of a creature's condition: hit points, stamina and how well fed it is - and, when it stands
    /// by a plant with fruit on it, what pressing the eat key would do.
    /// </summary>
    /// <remarks>
    /// <para>Built from code under its own overlay canvas so that it needs nothing in the scene but this
    /// component, and fits the screen whatever the canvas scaler of the build screen does. It listens for
    /// the local creature the same way <see cref="CombatHud"/> does, and reads three numbers off it - it
    /// never reaches into the creature's physics.</para>
    /// </remarks>
    public sealed class VitalsHud : MonoBehaviour
    {
        [SerializeField] private Color _health = new(0.55f, 0.78f, 0.45f, 1f);
        [SerializeField] private Color _hurt = new(0.9f, 0.35f, 0.3f, 1f);
        [SerializeField] private Color _stamina = new(0.95f, 0.78f, 0.3f, 1f);
        [SerializeField] private Color _exhausted = new(0.85f, 0.3f, 0.25f, 1f);
        [SerializeField] private Color _food = new(0.9f, 0.55f, 0.25f, 1f);
        [SerializeField] private Color _panel = new(0.07f, 0.1f, 0.12f, 0.62f);

        private sealed class Bar
        {
            public RectTransform Fill;
            public Image FillImage;
            public TextMeshProUGUI Label;
        }

        private IDisposable _subscription;
        private CreatureBody _body;
        private PlaygroundCreatureController _controller;
        private IslandFlora _flora;

        private Bar _hp, _energy, _hunger;
        private TextMeshProUGUI _prompt;
        private float _nextPrompt;

        private void Awake() => Build();

        // The message bus may come up after this component's first OnEnable, so Start tries again.
        private void Start() => Subscribe();

        private void OnEnable() => Subscribe();

        private void Subscribe()
        {
            if (_subscription != null || !GlobalMessagePipe.IsInitialized) return;

            _subscription = GlobalMessagePipe.GetSubscriber<LocalCreatureBodyChangedMessage>().Subscribe(message =>
            {
                _body = message.Body;
                _controller = _body != null ? _body.GetComponent<PlaygroundCreatureController>() : null;
            });
        }

        private void OnDisable()
        {
            _subscription?.Dispose();
            _subscription = null;
        }

        private void Update()
        {
            bool visible = _body != null && _body.IsAlive;
            _hp.Fill.parent.parent.gameObject.SetActive(visible);
            if (!visible)
            {
                _prompt.text = string.Empty;
                return;
            }

            float hp = Mathf.Clamp01(_body.Hp / Mathf.Max(1f, _body.MaxHp));
            Set(_hp, hp, hp < 0.35f ? _hurt : _health, $"HP {_body.Hp:0} / {_body.MaxHp:0}");

            if (_controller != null)
            {
                float max = Mathf.Max(1f, _controller.MaxStamina);
                bool tired = _controller.IsExhausted;
                float pulse = tired ? 0.65f + 0.35f * Mathf.Sin(Time.time * 9f) : 1f;
                Color c = tired ? Color.Lerp(_exhausted, Color.white, 1f - pulse) : _stamina;
                Set(_energy, _controller.Stamina / max, c, tired ? "Wyczerpany" : $"Wytrzymałość {_controller.Stamina:0}");
            }

            float fed = Mathf.Clamp01(_body.Satiety / CreatureBody.MaxSatiety);
            Set(_hunger, fed, _body.IsStarving ? _exhausted : _food, _body.IsStarving ? "Głodny!" : $"Sytość {_body.Satiety:0}");

            UpdatePrompt();
        }

        private void UpdatePrompt()
        {
            if (Time.time < _nextPrompt) return;
            _nextPrompt = Time.time + 0.2f;

            if (_flora == null) _flora = FindAnyObjectByType<IslandFlora>();

            if (_flora != null && _flora.TryFindRipe(_body.transform.position, out _, out var spec))
            {
                string effect = spec.Health < 0 ? "trujący!" : $"sytość +{spec.Satiety:0}" + (spec.Health > 0 ? $", HP +{spec.Health:0}" : string.Empty);
                _prompt.text = $"E — zjedz: {spec.Name} ({effect})";
            }
            else _prompt.text = string.Empty;
        }

        private static void Set(Bar bar, float fraction, Color colour, string text)
        {
            bar.Fill.anchorMax = new Vector2(Mathf.Clamp01(fraction), 1f);
            bar.FillImage.color = colour;
            bar.Label.text = text;
        }

        // ---------------------------------------------------------------- building

        private void Build()
        {
            var canvasObject = new GameObject("VitalsCanvas");
            canvasObject.transform.SetParent(transform, false);

            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;

            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

            // Bottom left, well inside the screen edge.
            var panel = new GameObject("Panel", typeof(RectTransform)).GetComponent<RectTransform>();
            panel.SetParent(canvasObject.transform, false);
            panel.anchorMin = panel.anchorMax = new Vector2(0f, 0f);
            panel.pivot = new Vector2(0f, 0f);
            panel.anchoredPosition = new Vector2(48f, 120f);
            panel.sizeDelta = new Vector2(360f, 128f);

            var background = panel.gameObject.AddComponent<Image>();
            background.color = _panel;
            background.raycastTarget = false;

            _hp = MakeBar(panel, 0, _health);
            _energy = MakeBar(panel, 1, _stamina);
            _hunger = MakeBar(panel, 2, _food);

            var prompt = new GameObject("EatPrompt", typeof(RectTransform)).GetComponent<RectTransform>();
            prompt.SetParent(canvasObject.transform, false);
            prompt.anchorMin = prompt.anchorMax = new Vector2(0.5f, 0f);
            prompt.pivot = new Vector2(0.5f, 0f);
            prompt.anchoredPosition = new Vector2(0f, 150f);
            prompt.sizeDelta = new Vector2(900f, 48f);

            _prompt = prompt.gameObject.AddComponent<TextMeshProUGUI>();
            _prompt.alignment = TextAlignmentOptions.Center;
            _prompt.fontSize = 30f;
            _prompt.color = new Color(0.98f, 0.9f, 0.7f, 1f);
            _prompt.raycastTarget = false;
            _prompt.text = string.Empty;
        }

        private static Bar MakeBar(RectTransform parent, int row, Color colour)
        {
            const float height = 30f, gap = 8f, margin = 12f;

            var track = new GameObject("Bar" + row, typeof(RectTransform)).GetComponent<RectTransform>();
            track.SetParent(parent, false);
            track.anchorMin = new Vector2(0f, 1f);
            track.anchorMax = new Vector2(1f, 1f);
            track.pivot = new Vector2(0.5f, 1f);
            track.offsetMin = new Vector2(margin, -(margin + (row + 1) * height + row * gap));
            track.offsetMax = new Vector2(-margin, -(margin + row * (height + gap)));

            var trackImage = track.gameObject.AddComponent<Image>();
            trackImage.color = new Color(0f, 0f, 0f, 0.45f);
            trackImage.raycastTarget = false;

            var fill = new GameObject("Fill", typeof(RectTransform)).GetComponent<RectTransform>();
            fill.SetParent(track, false);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = Vector2.one;
            fill.offsetMin = fill.offsetMax = Vector2.zero;

            var fillImage = fill.gameObject.AddComponent<Image>();
            fillImage.color = colour;
            fillImage.raycastTarget = false;

            var label = new GameObject("Label", typeof(RectTransform)).GetComponent<RectTransform>();
            label.SetParent(track, false);
            label.anchorMin = Vector2.zero;
            label.anchorMax = Vector2.one;
            label.offsetMin = new Vector2(10f, 0f);
            label.offsetMax = new Vector2(-10f, 0f);

            var text = label.gameObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = 19f;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.color = new Color(0.05f, 0.07f, 0.08f, 1f);
            text.raycastTarget = false;
            text.fontStyle = FontStyles.Bold;

            return new Bar { Fill = fill, FillImage = fillImage, Label = text };
        }
    }
}
