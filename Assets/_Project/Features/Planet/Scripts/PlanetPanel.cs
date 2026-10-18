using System;
using Leeway.World.Domain;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Leeway.Planet
{
    /// <summary>
    /// The panel that appears with the planet view: sliders for the planet's size, the share of sea and the
    /// number of islands, a button to shuffle the islands and one to go back down to the ground.
    /// </summary>
    /// <remarks>
    /// <para>Like the other overlays it is built from code under its own canvas. The sliders show the
    /// numbers in force and are locked for a peer that is not the host. A change is applied a moment after
    /// the last movement of a slider, not on every pixel of it, because every change rebuilds the planet.</para>
    /// </remarks>
    public sealed class PlanetPanel : MonoBehaviour
    {
        [SerializeField] private PlanetView _planet;
        [SerializeField] private PlanetCameraDirector _director;
        [SerializeField, Min(0f)] private float _applyDelay = 0.35f;

        [SerializeField] private Color _panel = new(0.07f, 0.1f, 0.12f, 0.9f);
        [SerializeField] private Color _text = new(0.95f, 0.92f, 0.8f, 1f);
        [SerializeField] private Color _accent = new(0.55f, 0.85f, 0.5f, 1f);

        private GameObject _root;
        private Slider _radius, _water, _islands;
        private TextMeshProUGUI _radiusLabel, _waterLabel, _islandsLabel, _status;
        private Button _reroll;
        private float _applyAt = -1f;

        private void Awake() => Build();

        private void OnEnable()
        {
            if (_director != null) _director.ViewChanged += OnViewChanged;
        }

        private void OnDisable()
        {
            if (_director != null) _director.ViewChanged -= OnViewChanged;
        }

        private void Start()
        {
            _root.SetActive(false);
        }

        private void OnViewChanged(bool inPlanetView)
        {
            _root.SetActive(inPlanetView);
            if (inPlanetView) Pull();
        }

        private void Update()
        {
            if (!_root.activeSelf || _planet == null) return;

            bool editable = _planet.CanEdit;
            _radius.interactable = _water.interactable = _islands.interactable = _reroll.interactable = editable;

            if (_applyAt >= 0f && Time.unscaledTime >= _applyAt)
            {
                _applyAt = -1f;
                _planet.Apply(_radius.value, _water.value, Mathf.RoundToInt(_islands.value));
            }
            else if (_applyAt < 0f)
            {
                Pull();
            }

            PlanetModel model = _planet.Model;
            _status.text = (editable ? string.Empty : "Tylko gospodarz może zmieniać planetę\n")
                + (model != null ? $"Ląd: {model.LandFraction * 100f:0}%  ·  Wysp: {model.IslandCount}" : string.Empty);
        }

        // Shows the numbers in force without counting that as a change by the player.
        private void Pull()
        {
            if (_planet == null) return;

            PlanetSettings now = _planet.Current();
            _radius.SetValueWithoutNotify(now.Radius);
            _water.SetValueWithoutNotify(now.WaterCoverage);
            _islands.SetValueWithoutNotify(now.IslandCount);
            Labels();
        }

        private void Labels()
        {
            _radiusLabel.text = $"Rozmiar planety: {_radius.value:0} m";
            _waterLabel.text = $"Pokrycie wodą: {_water.value * 100f:0}%";
            _islandsLabel.text = $"Liczba wysp: {Mathf.RoundToInt(_islands.value)}";
        }

        private void OnSlider(float _)
        {
            Labels();
            _applyAt = Time.unscaledTime + _applyDelay;
        }

        // ---------------------------------------------------------------- building

        // A strip across the top of the screen: the sides belong to the editor's panels.
        private void Build()
        {
            var canvasObject = new GameObject("PlanetCanvas");
            canvasObject.transform.SetParent(transform, false);

            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 25;

            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            canvasObject.AddComponent<GraphicRaycaster>();

            RectTransform panel = NewRect("Panel", canvasObject.transform);
            _root = panel.gameObject;
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 1f);
            panel.pivot = new Vector2(0.5f, 1f);
            panel.anchoredPosition = new Vector2(0f, -32f);
            panel.sizeDelta = new Vector2(1180f, 176f);
            panel.gameObject.AddComponent<Image>().color = _panel;

            Text(panel, "Planeta", 32f, FontStyles.Bold, 24f, -10f, 200f, 44f);
            Text(panel, "Przeciągnij, by obracać  ·  kółko: przybliż / oddal", 18f, FontStyles.Normal, 190f, -18f, 640f, 30f).color = _text * 0.8f;

            _radiusLabel = Text(panel, string.Empty, 21f, FontStyles.Normal, 24f, -58f, 320f, 28f);
            _radius = MakeSlider(panel, PlanetSettings.MinRadius, PlanetSettings.MaxRadius, false, 24f, -88f, 320f);
            _waterLabel = Text(panel, string.Empty, 21f, FontStyles.Normal, 380f, -58f, 320f, 28f);
            _water = MakeSlider(panel, PlanetSettings.MinWater, PlanetSettings.MaxWater, false, 380f, -88f, 320f);
            _islandsLabel = Text(panel, string.Empty, 21f, FontStyles.Normal, 736f, -58f, 280f, 28f);
            _islands = MakeSlider(panel, PlanetSettings.MinIslands, PlanetSettings.MaxIslands, true, 736f, -88f, 280f);

            _status = Text(panel, string.Empty, 18f, FontStyles.Italic, 24f, -124f, 900f, 44f);
            _status.color = _accent;

            _reroll = MakeButton(panel, "Przetasuj wyspy", 1040f, -14f, 120f, 70f, () => _planet?.Reroll());
            MakeButton(panel, "Wróć (Esc)", 1040f, -92f, 120f, 70f, () => _director?.ExitPlanetView());
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        // Positions are from the panel's top-left corner, y going down as negative.
        private static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
        }

        private TextMeshProUGUI Text(RectTransform parent, string value, float size, FontStyles style, float x, float y, float width, float height)
        {
            RectTransform rect = NewRect("Text", parent);
            Place(rect, x, y, width, height);

            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = _text;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.raycastTarget = false;
            return text;
        }

        private Slider MakeSlider(RectTransform parent, float min, float max, bool whole, float x, float y, float width)
        {
            RectTransform root = NewRect("Slider", parent);
            Place(root, x, y, width, 28f);

            RectTransform track = NewRect("Track", root);
            track.anchorMin = new Vector2(0f, 0.5f);
            track.anchorMax = new Vector2(1f, 0.5f);
            track.sizeDelta = new Vector2(0f, 8f);
            track.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.5f);

            RectTransform fillArea = NewRect("Fill Area", root);
            fillArea.anchorMin = new Vector2(0f, 0.5f);
            fillArea.anchorMax = new Vector2(1f, 0.5f);
            fillArea.sizeDelta = new Vector2(-14f, 8f);
            RectTransform fill = NewRect("Fill", fillArea);
            fill.gameObject.AddComponent<Image>().color = _accent;
            fill.sizeDelta = Vector2.zero;

            RectTransform handleArea = NewRect("Handle Slide Area", root);
            handleArea.anchorMin = Vector2.zero;
            handleArea.anchorMax = Vector2.one;
            handleArea.offsetMin = new Vector2(7f, 0f);
            handleArea.offsetMax = new Vector2(-7f, 0f);
            RectTransform handle = NewRect("Handle", handleArea);
            handle.sizeDelta = new Vector2(16f, 26f);
            var handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.color = _text;

            var slider = root.gameObject.AddComponent<Slider>();
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleImage;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = whole;
            slider.onValueChanged.AddListener(OnSlider);
            return slider;
        }

        private Button MakeButton(RectTransform parent, string label, float x, float y, float width, float height, Action onClick)
        {
            RectTransform rect = NewRect("Button", parent);
            Place(rect, x, y, width, height);

            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.2f, 0.28f, 0.3f, 1f);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => onClick());

            RectTransform textRect = NewRect("Label", rect);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(4f, 0f);
            textRect.offsetMax = new Vector2(-4f, 0f);
            var text = textRect.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = label;
            text.fontSize = 20f;
            text.color = _text;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            return button;
        }
    }
}
