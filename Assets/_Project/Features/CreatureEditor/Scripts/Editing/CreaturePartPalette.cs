using System.Collections.Generic;
using Leeway.Creature.Domain;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Leeway.CreatureEditor
{
    /// <summary>
    /// The part palette, with one tab per category. A part is taken from it by
    /// <b>dragging it onto the creature</b> — a click on its own attaches nothing.
    /// </summary>
    /// <remarks>
    /// The contents come straight from <see cref="CreaturePartCatalog"/>, so adding a part to the
    /// game needs no UI work at all — a new category creates its own tab. The item and tab templates
    /// are cloned rather than built in code, so the layout and styling stay in the hands of whoever
    /// builds the scene.
    /// </remarks>
    public class CreaturePartPalette : MonoBehaviour
    {
        /// <summary>Tab order. Kept here rather than in the enum's order so it can change without touching the domain.</summary>
        private static readonly PartCategory[] TabOrder =
        {
            PartCategory.Locomotion,
            PartCategory.Mouth,
            PartCategory.Sense,
            PartCategory.Grasper,
            PartCategory.Weapon,
            PartCategory.Detail,
        };

        [Header("Dependencies")]
        [SerializeField] private CreatureEditorController _controller;
        [SerializeField] private CreaturePartCatalog _catalog;

        [Header("Widgets")]
        [Tooltip("Inactive template for a part tile - cloned once per part.")]
        [SerializeField] private Button _itemTemplate;

        [Tooltip("Inactive template for a tab button - cloned once per category.")]
        [SerializeField] private Button _tabTemplate;

        [SerializeField] private Transform _itemContainer;
        [SerializeField] private Transform _tabContainer;

        [Tooltip("Photographs part prefabs into tile thumbnails. Left empty: a tile falls back to its name.")]
        [SerializeField] private PartThumbnailRenderer _thumbnails;

        [Header("Tab appearance")]
        [SerializeField] private Color _activeTabColor = new(1f, 0.82f, 0.25f, 1f);
        [SerializeField] private Color _inactiveTabColor = new(0.75f, 0.78f, 0.82f, 1f);

        [Header("Behaviour")]
        [Tooltip("Whether to attach parts mirrored whenever the definition allows it.")]
        [SerializeField] private bool _mirrorWhenPossible = true;

        private readonly List<GameObject> _items = new();
        private readonly List<Button> _tabs = new();
        private readonly List<PartCategory> _tabCategories = new();

        private PartCategory _active = PartCategory.Locomotion;

        private void Start() => Rebuild();

        [ContextMenu("Rebuild palette")]
        public void Rebuild()
        {
            ClearTabs();

            if (_catalog == null || _tabTemplate == null || _tabContainer == null) return;

            _tabTemplate.gameObject.SetActive(false);
            if (_itemTemplate != null) _itemTemplate.gameObject.SetActive(false);

            bool activeStillExists = false;

            foreach (PartCategory category in TabOrder)
            {
                // An empty category gets no tab — there is nothing for the player to click into.
                if (_catalog.GetByCategory(category).Count == 0) continue;

                SpawnTab(category);
                if (category == _active) activeStillExists = true;
            }

            if (!activeStillExists && _tabCategories.Count > 0) _active = _tabCategories[0];

            ShowCategory(_active);
        }

        public void ShowCategory(PartCategory category)
        {
            _active = category;

            ClearItems();

            if (_catalog == null || _itemTemplate == null || _itemContainer == null) return;

            foreach (CreaturePartDefinition definition in _catalog.GetByCategory(category))
            {
                if (definition != null) SpawnItem(definition);
            }

            RefreshTabColors();
        }

        private void SpawnTab(PartCategory category)
        {
            Button tab = Instantiate(_tabTemplate, _tabContainer);
            tab.gameObject.SetActive(true);
            tab.name = $"Tab_{category}";

            var label = tab.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null) label.text = TabName(category);

            // The clone carries the template's listeners — they have to go before we add our own.
            tab.onClick = new Button.ButtonClickedEvent();

            PartCategory captured = category;
            tab.onClick.AddListener(() => ShowCategory(captured));

            _tabs.Add(tab);
            _tabCategories.Add(category);
        }

        private void SpawnItem(CreaturePartDefinition definition)
        {
            Button item = Instantiate(_itemTemplate, _itemContainer);
            item.gameObject.SetActive(true);
            item.name = $"Part_{definition.PartKey}";

            // A tile is not a button — it works by dragging, so we clear the click inherited from the
            // template to stop it attaching parts by a shortcut.
            item.onClick = new Button.ButtonClickedEvent();

            Texture still = _thumbnails != null ? _thumbnails.Get(definition) : null;
            RawImage image = ApplyThumbnail(item, still, out bool ownsLayout);

            var label = item.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
            {
                // With a preview, the tile shows the cost alone, and <b>below</b> the image rather than
                // on it: text over the model was unreadable on light-coloured parts and covered the very
                // thing the player is looking at. The name lives in the tooltip — nothing longer than a
                // number fits across the square anyway.
                label.text = image != null
                    ? $"{definition.Cost}"
                    : $"{definition.DisplayName}\n<size=70%>{definition.Cost}</size>";

                if (ownsLayout) PlaceCostLabel(label);
            }

            if (image != null && _thumbnails != null)
                item.gameObject.AddComponent<CreaturePaletteThumbnail>().Bind(_thumbnails, definition, image, still);

            bool mirrored = _mirrorWhenPossible && definition.MirrorCapable;
            item.gameObject.AddComponent<CreaturePaletteItem>().Bind(_controller, definition.PartId, mirrored);

            _items.Add(item.gameObject);
        }

        /// <summary>The name of the preview object inside a tile — how we find it again on a rebuild.</summary>
        private const string ThumbnailName = "Thumbnail";

        /// <summary>The fraction of a tile's height reserved for the cost strip.</summary>
        private const float CostStripHeight = 0.24f;

        /// <summary>
        /// Puts a preview into a tile, building its objects if the template has none.
        /// </summary>
        /// <remarks>
        /// <para>The preview takes the <b>top</b> of the tile and the cost gets its own strip beneath
        /// it. Previously one lay over the other and the number disappeared into the model.</para>
        ///
        /// <para>Two objects, not one: the outer marks out the area above the cost strip, and the inner
        /// one — with an <see cref="AspectRatioFitter"/> — keeps a <b>square</b> inside it. The studio
        /// render is square, so without this a long leg would stretch horizontally on every tile that
        /// is not exactly square. <c>RawImage</c> has no equivalent of <c>preserveAspect</c>, but it has
        /// to be one: only it will take the live preview's <c>RenderTexture</c> without repacking it
        /// into a <c>Sprite</c> every frame.</para>
        ///
        /// <para>We build the objects in code so that adding previews did not require reworking the
        /// template in the scene. A template with its own <c>Thumbnail</c> child takes precedence —
        /// then whoever builds the scene decides the layout, and we leave the label alone too.</para>
        /// </remarks>
        private static RawImage ApplyThumbnail(Button item, Texture still, out bool ownsLayout)
        {
            ownsLayout = false;
            if (still == null) return null;

            Transform existing = item.transform.Find(ThumbnailName);
            if (existing != null)
            {
                RawImage authored = existing.GetComponentInChildren<RawImage>();
                if (authored == null) authored = existing.gameObject.AddComponent<RawImage>();

                authored.texture = still;
                authored.color = Color.white;
                return authored;
            }

            var slot = new GameObject(ThumbnailName, typeof(RectTransform));
            slot.transform.SetParent(item.transform, false);

            var slotRect = (RectTransform)slot.transform;
            slotRect.anchorMin = new Vector2(0f, CostStripHeight);
            slotRect.anchorMax = Vector2.one;
            slotRect.offsetMin = new Vector2(4f, 2f);
            slotRect.offsetMax = new Vector2(-4f, -4f);

            var view = new GameObject("Image", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
            view.transform.SetParent(slot.transform, false);

            var fitter = view.GetComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = 1f;

            RawImage image = view.GetComponent<RawImage>();
            image.texture = still;
            image.color = Color.white;

            // The preview must not eat clicks — dragging grabs the tile.
            image.raycastTarget = false;

            // The button background stays underneath, the cost label on top.
            slot.transform.SetSiblingIndex(0);

            ownsLayout = true;
            return image;
        }

        /// <summary>Seats the label in the strip below the preview.</summary>
        private static void PlaceCostLabel(TextMeshProUGUI label)
        {
            RectTransform rect = label.rectTransform;

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = new Vector2(1f, CostStripHeight);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);

            label.alignment = TextAlignmentOptions.Center;
            label.enableAutoSizing = true;
            label.fontSizeMin = 8f;
        }

        private void RefreshTabColors()
        {
            for (int i = 0; i < _tabs.Count; i++)
            {
                if (_tabs[i] == null) continue;

                var image = _tabs[i].GetComponent<Image>();
                if (image != null) image.color = _tabCategories[i] == _active ? _activeTabColor : _inactiveTabColor;
            }
        }

        private void ClearItems()
        {
            for (int i = 0; i < _items.Count; i++) DestroySafely(_items[i]);
            _items.Clear();
        }

        private void ClearTabs()
        {
            ClearItems();

            for (int i = 0; i < _tabs.Count; i++)
                if (_tabs[i] != null) DestroySafely(_tabs[i].gameObject);

            _tabs.Clear();
            _tabCategories.Clear();
        }

        private static void DestroySafely(GameObject target)
        {
            if (target == null) return;

            if (Application.isPlaying) Destroy(target);
            else DestroyImmediate(target);
        }

        private static string TabName(PartCategory category) => category switch
        {
            PartCategory.Locomotion => "Movement",
            PartCategory.Mouth => "Mouth",
            PartCategory.Sense => "Senses",
            PartCategory.Grasper => "Graspers",
            PartCategory.Weapon => "Weapons",
            PartCategory.Detail => "Details",
            _ => category.ToString(),
        };
    }
}
