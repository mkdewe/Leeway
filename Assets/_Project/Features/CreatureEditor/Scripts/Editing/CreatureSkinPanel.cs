using System;
using System.Collections.Generic;
using MessagePipe;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Leeway.CreatureEditor
{
    /// <summary>
    /// The paint panel: a grid of colours and a grid of coat patterns, down the left-hand side of the
    /// editor.
    /// </summary>
    /// <remarks>
    /// <para><b>What a click paints is whatever is selected</b> — the part the player last clicked on,
    /// or the creature's skin when none is. That is the whole interaction: pick a part, pick a colour.
    /// The heading says which of the two is about to be painted, because a palette that silently
    /// painted the wrong thing would be worse than no palette.</para>
    ///
    /// <para>The tiles are built from the palette asset, so adding a colour or a coat is art work
    /// rather than UI work. A template tile may be handed to it from the scene — the styling then
    /// stays in the hands of whoever builds the scene; without one it makes a plain square, so the
    /// panel is never empty just because nobody has drawn a button yet.</para>
    /// </remarks>
    public class CreatureSkinPanel : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private CreatureEditorController _controller;
        [SerializeField] private CreatureSkinPalette _palette;

        [Tooltip("The brush. Left empty: the panel only fills whole parts, with no free painting.")]
        [SerializeField] private CreatureSkinBrush _brush;

        [Tooltip("Picks the brush up and puts it down. Optional — the tabs below do the same thing better.")]
        [SerializeField] private Button _brushToggle;

        [Header("Tabs")]
        [Tooltip("Sculpting: the spine and the parts answer to the mouse, and a swatch fills what is selected.")]
        [SerializeField] private Button _bodyTab;

        [Tooltip("Painting: dragging on the creature paints it, and sculpting stands down.")]
        [SerializeField] private Button _paintTab;

        [Tooltip("Everything the panel shows. Hidden outside the building screen, where there is nothing to paint on.")]
        [SerializeField] private GameObject _contents;

        [SerializeField] private Color _activeTabColor = new(1f, 0.82f, 0.25f, 1f);
        [SerializeField] private Color _inactiveTabColor = new(0.75f, 0.78f, 0.82f, 1f);

        [Tooltip("Brush size, from a fine line to a broad wash. Optional.")]
        [SerializeField] private Slider _brushSize;

        [Tooltip("The range the slider spans, in metres of brush radius.")]
        [SerializeField] private Vector2 _brushSizeRange = new(0.03f, 0.3f);

        [Header("Widgets")]
        [Tooltip("Says what the next click will paint. Optional.")]
        [SerializeField] private TextMeshProUGUI _targetLabel;

        [Tooltip("Inactive template for a swatch — cloned once per colour and once per pattern. Optional.")]
        [SerializeField] private Button _tileTemplate;

        [SerializeField] private Transform _colorContainer;
        [SerializeField] private Transform _patternContainer;

        [Header("Appearance")]
        [SerializeField] private float _tileSize = 34f;
        [SerializeField] private Color _patternTileColor = new(0.85f, 0.85f, 0.85f, 1f);

        private readonly List<GameObject> _tiles = new();

        private IDisposable _subscription;
        private int _selectedPart = -1;

        /// <summary>What the last frame decided about being on the building screen — so the switch happens once, not every frame.</summary>
        private bool _wasBuilding = true;

        private void Start()
        {
            Rebuild();
            Subscribe();

            if (_brushToggle != null) _brushToggle.onClick.AddListener(ToggleBrush);
            if (_bodyTab != null) _bodyTab.onClick.AddListener(() => SelectTab(painting: false));
            if (_paintTab != null) _paintTab.onClick.AddListener(() => SelectTab(painting: true));

            if (_brushSize != null && _brush != null)
            {
                // The slider runs 0..1 and is mapped onto the range here rather than being configured
                // with it, so the scene keeps a plain slider and the sizes stay a tuning value.
                _brushSize.minValue = 0f;
                _brushSize.maxValue = 1f;
                _brushSize.SetValueWithoutNotify(Mathf.InverseLerp(_brushSizeRange.x, _brushSizeRange.y, _brush.Radius));
                _brushSize.onValueChanged.AddListener(OnBrushSizeChanged);
            }

            UpdateTargetLabel();
        }

        private void OnBrushSizeChanged(float normalised)
        {
            if (_brush == null) return;

            _brush.Radius = Mathf.Lerp(_brushSizeRange.x, _brushSizeRange.y, normalised);
        }

        /// <summary>Swaps between filling whole parts and painting on the skin by hand.</summary>
        public void ToggleBrush() => SelectTab(_brush != null && !_brush.IsActive);

        /// <summary>
        /// Chooses what the mouse does on the creature: sculpt it, or paint it.
        /// </summary>
        /// <remarks>
        /// The two cannot share the left button over the same carcass — a drag would both drag a
        /// vertebra and draw a line on it. So they are tabs, and the one that is not chosen stands down:
        /// in painting mode the editor's own click handling is suppressed
        /// (<see cref="CreatureEditorController.IsPainting"/>), and in sculpting mode the brush is put
        /// away.
        /// </remarks>
        public void SelectTab(bool painting)
        {
            if (_brush != null) _brush.SetActive(painting);

            Tint(_bodyTab, !painting);
            Tint(_paintTab, painting);

            UpdateTargetLabel();
        }

        private void Tint(Button tab, bool active)
        {
            if (tab == null) return;

            var image = tab.targetGraphic as Image;
            if (image != null) image.color = active ? _activeTabColor : _inactiveTabColor;
        }

        /// <summary>
        /// Keeps the panel to the building screen.
        /// </summary>
        /// <remarks>
        /// There is nothing to paint on once the player is driving their creature around the playground:
        /// the preview is hidden, the brush would be raycasting at an empty pad, and a palette on screen
        /// would only say that something is missing. Leaving the screen also puts the brush away, so
        /// coming back does not start mid-stroke.
        /// </remarks>
        private void Update()
        {
            bool building = _controller == null || _controller.Mode == CreatureEditorMode.Sculpt;
            if (building == _wasBuilding) return;

            _wasBuilding = building;

            if (!building) SelectTab(painting: false);
            if (_contents != null) _contents.SetActive(building);
        }

        private void OnDestroy() => _subscription?.Dispose();

        /// <summary>
        /// Follows the selection through the same message the HUD listens to, rather than polling the
        /// controller — the panel then has nothing to do on the frames when nothing is selected.
        /// </summary>
        private void Subscribe()
        {
            if (!GlobalMessagePipe.IsInitialized) return;

            _subscription = GlobalMessagePipe.GetSubscriber<CreatureEditorPartSelectionChangedMessage>()
                .Subscribe(message =>
                {
                    _selectedPart = message.PartIndex;
                    UpdateTargetLabel();
                });
        }

        private void UpdateTargetLabel()
        {
            if (_targetLabel == null) return;

            if (_brush != null && _brush.IsActive)
            {
                _targetLabel.text = "Brush — drag across the creature";
                return;
            }

            _targetLabel.text = _selectedPart >= 0 ? "Filling: selected part" : "Filling: body";
        }

        [ContextMenu("Rebuild palette")]
        public void Rebuild()
        {
            foreach (GameObject tile in _tiles)
                if (tile != null) Destroy(tile);
            _tiles.Clear();

            if (_palette == null) return;

            if (_tileTemplate != null) _tileTemplate.gameObject.SetActive(false);

            if (_colorContainer != null)
            {
                foreach (SkinSwatch swatch in _palette.Colors)
                {
                    Color32 color = swatch.Color;
                    BuildTile(_colorContainer, swatch.Name, color, null, () => PickColor(color));
                }

                // The way back: a part painted by hand returns to following the creature's colour.
                BuildTile(_colorContainer, "No colour of its own", new Color(0.2f, 0.2f, 0.22f, 1f), null,
                    () => _controller?.ClearSelectionPaint());
            }

            if (_patternContainer == null) return;

            for (int i = 0; i < _palette.Patterns.Count; i++)
            {
                byte id = (byte)i;
                SkinPattern pattern = _palette.Patterns[i];
                if (pattern == null) continue;

                BuildTile(_patternContainer, pattern.Name, _patternTileColor, pattern.BaseMap,
                    () => _controller?.SetSelectionPattern(id));
            }
        }

        /// <summary>
        /// What a colour swatch does depends on which tool is in hand.
        /// </summary>
        /// <remarks>
        /// With the brush out it loads the brush and paints nothing yet — the player then draws with it.
        /// Otherwise it does what the palette always did: fills the selected part, or the whole skin
        /// when nothing is selected.
        /// </remarks>
        private void PickColor(Color32 color)
        {
            if (_brush != null && _brush.IsActive)
            {
                _brush.Color = color;
                return;
            }

            _controller?.PaintSelection(color);
        }

        /// <summary>
        /// One tile. A pattern tile shows its own mask as the picture, so the player picks a coat by
        /// looking at it rather than by reading its name.
        /// </summary>
        private void BuildTile(Transform parent, string label, Color color, Texture2D picture, UnityEngine.Events.UnityAction onClick)
        {
            Button button = _tileTemplate != null ? Instantiate(_tileTemplate, parent) : CreateTile(parent);

            button.gameObject.name = "Tile_" + label;
            button.gameObject.SetActive(true);
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(onClick);

            var image = button.targetGraphic as Image;
            if (image == null) image = button.GetComponent<Image>();

            if (image != null)
            {
                image.color = color;

                if (picture != null)
                {
                    image.sprite = Sprite.Create(picture, new Rect(0f, 0f, picture.width, picture.height), new Vector2(0.5f, 0.5f));
                    image.type = Image.Type.Simple;
                }
            }

            // A tooltip is not worth a system of its own here — the name lives on the object, where
            // the hierarchy shows it, and on the label when the template has one.
            var text = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (text != null) text.text = picture != null ? label : string.Empty;

            _tiles.Add(button.gameObject);
        }

        /// <summary>A plain square, for when the scene has no styled template to clone.</summary>
        private Button CreateTile(Transform parent)
        {
            var go = new GameObject("Tile", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(_tileSize, _tileSize);

            var button = go.GetComponent<Button>();
            button.targetGraphic = go.GetComponent<Image>();

            return button;
        }
    }
}
