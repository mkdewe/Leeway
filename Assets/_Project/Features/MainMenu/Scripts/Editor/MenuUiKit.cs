using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Leeway.MainMenu.Editor
{
    /// <summary>
    /// The menu's visual vocabulary for the generators: colours, sizes and factory methods for every
    /// widget, so all screens are built from the same few parts and restyling happens in one file.
    /// </summary>
    internal static class MenuUiKit
    {
        public static readonly Color Background = Hex("0B1119");
        public static readonly Color BackgroundGlow = Hex("12303A");
        public static readonly Color CardColor = Hex("141C27F2");
        public static readonly Color Row = Hex("1C2633");
        public static readonly Color RowAlt = Hex("18212D");
        public static readonly Color Field = Hex("0F151E");
        public static readonly Color Accent = Hex("3DD6B0");
        public static readonly Color AccentText = Hex("06231C");
        public static readonly Color Secondary = Hex("263243");
        public static readonly Color Danger = Hex("C8575A");
        public static readonly Color TextColor = Hex("E8EEF5");
        public static readonly Color TextMuted = Hex("8896A8");
        public static readonly Color Overlay = Hex("05080CC8");

        public enum ButtonStyle { Primary, Secondary, Danger, Ghost }

        private static Sprite _rounded;
        private static Sprite _circle;
        private static Sprite _field;

        public static Sprite Rounded => _rounded ??= AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        public static Sprite Circle => _circle ??= AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        public static Sprite FieldSprite => _field ??= AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/InputFieldBackground.psd");

        public static TMP_DefaultControls.Resources TmpResources => new()
        {
            standard = Rounded,
            background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
            inputField = FieldSprite,
            knob = Circle,
            checkmark = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd"),
            dropdown = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/DropdownArrow.psd"),
            mask = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UIMask.psd"),
        };

        public static DefaultControls.Resources UguiResources
        {
            get
            {
                var r = TmpResources;
                return new DefaultControls.Resources
                {
                    standard = r.standard, background = r.background, inputField = r.inputField,
                    knob = r.knob, checkmark = r.checkmark, dropdown = r.dropdown, mask = r.mask,
                };
            }
        }

        // ---- Structure ---------------------------------------------------------------------------

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        public static RectTransform Stretch(this RectTransform rt, float left = 0, float top = 0, float right = 0, float bottom = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        public static RectTransform Center(this RectTransform rt, float width, float height)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(width, height);
            rt.anchoredPosition = Vector2.zero;
            return rt;
        }

        public static Image Panel(string name, Transform parent, Color color, Sprite sprite = null)
        {
            var rt = Rect(name, parent);
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = sprite ?? Rounded;
            image.type = Image.Type.Sliced;
            image.color = color;
            return image;
        }

        public static VerticalLayoutGroup Vertical(GameObject go, float spacing, RectOffset padding = null, TextAnchor align = TextAnchor.UpperLeft, bool expandHeight = false)
        {
            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = padding ?? new RectOffset();
            layout.childAlignment = align;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = expandHeight;
            return layout;
        }

        public static HorizontalLayoutGroup Horizontal(GameObject go, float spacing, RectOffset padding = null, TextAnchor align = TextAnchor.MiddleLeft)
        {
            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = padding ?? new RectOffset();
            layout.childAlignment = align;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            return layout;
        }

        public static LayoutElement Size(this Component c, float width = -1, float height = -1, float flexWidth = -1, float flexHeight = -1)
        {
            var e = c.GetComponent<LayoutElement>() ?? c.gameObject.AddComponent<LayoutElement>();
            if (width >= 0) { e.preferredWidth = width; e.minWidth = width; }
            if (height >= 0) { e.preferredHeight = height; e.minHeight = height; }
            if (flexWidth >= 0) e.flexibleWidth = flexWidth;
            if (flexHeight >= 0) e.flexibleHeight = flexHeight;
            return e;
        }

        public static RectTransform Spacer(Transform parent, float flexWidth = 1, float flexHeight = 0)
        {
            var rt = Rect("Spacer", parent);
            rt.Size(flexWidth: flexWidth, flexHeight: flexHeight);
            return rt;
        }

        // ---- Widgets -----------------------------------------------------------------------------

        public static TextMeshProUGUI MakeLabel(string name, Transform parent, string text, float size = 26, Color? color = null,
            TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft, FontStyles style = FontStyles.Normal)
        {
            var rt = Rect(name, parent);
            var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color ?? TextColor;
            tmp.alignment = align;
            tmp.fontStyle = style;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            tmp.raycastTarget = false;
            return tmp;
        }

        public static UnityEngine.UI.Button MakeButton(string name, Transform parent, string text, ButtonStyle style, out TextMeshProUGUI label, float height = 60, float fontSize = 26)
        {
            var image = Panel(name, parent, Color.white);
            var button = image.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;

            var (normal, textColor) = style switch
            {
                ButtonStyle.Primary => (Accent, AccentText),
                ButtonStyle.Danger => (Danger, TextColor),
                ButtonStyle.Ghost => (new Color(1, 1, 1, 0.04f), TextColor),
                _ => (Secondary, TextColor),
            };
            image.color = normal;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
            colors.selectedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.5f);
            colors.colorMultiplier = 1.2f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            label = MakeLabel("Label", image.transform, text, fontSize, textColor, TextAlignmentOptions.Center, FontStyles.Bold);
            label.rectTransform.Stretch(16, 0, 16, 0);
            image.Size(height: height);
            return button;
        }

        public static UnityEngine.UI.Button MakeButton(string name, Transform parent, string text, ButtonStyle style, float height = 60, float fontSize = 26) =>
            MakeButton(name, parent, text, style, out _, height, fontSize);

        public static TMP_InputField MakeInput(string name, Transform parent, string placeholder, float height = 56)
        {
            var go = TMP_DefaultControls.CreateInputField(TmpResources);
            go.name = name;
            go.transform.SetParent(parent, false);
            SetLayerRecursively(go);

            var field = go.GetComponent<TMP_InputField>();
            var image = go.GetComponent<Image>();
            image.sprite = Rounded;
            image.color = Field;

            field.textComponent.fontSize = 24;
            field.textComponent.color = TextColor;
            var ph = (TextMeshProUGUI)field.placeholder;
            ph.text = placeholder;
            ph.fontSize = 24;
            ph.color = TextMuted;
            ph.fontStyle = FontStyles.Italic;
            field.caretColor = Accent;
            field.selectionColor = new Color(Accent.r, Accent.g, Accent.b, 0.35f);
            field.customCaretColor = true;

            var area = (RectTransform)field.textViewport;
            area.offsetMin = new Vector2(18, 6);
            area.offsetMax = new Vector2(-18, -6);

            field.Size(height: height);
            return field;
        }

        public static TMP_Dropdown MakeDropdown(string name, Transform parent, float height = 56)
        {
            var go = TMP_DefaultControls.CreateDropdown(TmpResources);
            go.name = name;
            go.transform.SetParent(parent, false);
            SetLayerRecursively(go);

            var dropdown = go.GetComponent<TMP_Dropdown>();
            go.GetComponent<Image>().color = Field;
            dropdown.captionText.fontSize = 24;
            dropdown.captionText.color = TextColor;
            ((RectTransform)dropdown.captionText.transform).offsetMin = new Vector2(18, 0);

            var template = dropdown.template;
            template.GetComponent<Image>().color = Row;
            template.sizeDelta = new Vector2(template.sizeDelta.x, 220);
            dropdown.itemText.fontSize = 24;
            dropdown.itemText.color = TextColor;
            var item = dropdown.itemText.transform.parent;
            var itemBg = item.Find("Item Background")?.GetComponent<Image>();
            if (itemBg != null) itemBg.color = Secondary;
            ((RectTransform)item).sizeDelta = new Vector2(0, 44);
            var arrow = go.transform.Find("Arrow")?.GetComponent<Image>();
            if (arrow != null) arrow.color = TextMuted;

            dropdown.Size(height: height);
            return dropdown;
        }

        public static UnityEngine.UI.Slider MakeSlider(string name, Transform parent)
        {
            var go = DefaultControls.CreateSlider(UguiResources);
            go.name = name;
            go.transform.SetParent(parent, false);
            SetLayerRecursively(go);

            var slider = go.GetComponent<UnityEngine.UI.Slider>();
            go.transform.Find("Background").GetComponent<Image>().color = Field;
            slider.fillRect.GetComponent<Image>().color = Accent;
            slider.handleRect.GetComponent<Image>().color = TextColor;
            slider.handleRect.sizeDelta = new Vector2(28, 0);
            slider.Size(height: 28);
            return slider;
        }

        public static UnityEngine.UI.Toggle MakeToggle(string name, Transform parent, string text, bool isOn)
        {
            var root = Rect(name, parent);
            Horizontal(root.gameObject, 12);
            root.Size(height: 56);

            var box = Panel("Box", root, Field);
            box.Size(width: 34, height: 34);
            var check = Panel("Check", box.transform, Accent);
            check.rectTransform.Stretch(7, 7, 7, 7);

            var label = MakeLabel("Label", root, text, 24, TextColor);
            label.Size(flexWidth: 1);

            var toggle = root.gameObject.AddComponent<UnityEngine.UI.Toggle>();
            toggle.targetGraphic = box;
            toggle.graphic = check;
            toggle.isOn = isOn;
            return toggle;
        }

        /// <summary>A vertical scroll list whose content grows with its children.</summary>
        public static ScrollRect ScrollList(string name, Transform parent, out RectTransform content, float spacing = 8)
        {
            var go = DefaultControls.CreateScrollView(UguiResources);
            go.name = name;
            go.transform.SetParent(parent, false);
            SetLayerRecursively(go);

            var scroll = go.GetComponent<ScrollRect>();
            go.GetComponent<Image>().color = new Color(0, 0, 0, 0.18f);
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30;

            Object.DestroyImmediate(scroll.horizontalScrollbar.gameObject);
            scroll.horizontalScrollbar = null;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
            var vbar = scroll.verticalScrollbar;
            vbar.GetComponent<Image>().color = new Color(0, 0, 0, 0.2f);
            vbar.handleRect.GetComponent<Image>().color = Secondary;
            ((RectTransform)vbar.transform).sizeDelta = new Vector2(12, 0);

            content = scroll.content;
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.sizeDelta = Vector2.zero;
            Vertical(content.gameObject, spacing, new RectOffset(8, 8, 8, 8));
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return scroll;
        }

        public static void SetLayerRecursively(GameObject go)
        {
            int layer = LayerMask.NameToLayer("UI");
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        }

        // ---- Serialized wiring ---------------------------------------------------------------------

        public static void Wire(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var property = so.FindProperty(field);
            if (property == null)
            {
                Debug.LogError($"[MenuUiKit] {target.GetType().Name} has no serialized field '{field}'.", target);
                return;
            }
            property.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void WireEnum(Object target, string field, int value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).enumValueIndex = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out var color);
            return color;
        }
    }
}
