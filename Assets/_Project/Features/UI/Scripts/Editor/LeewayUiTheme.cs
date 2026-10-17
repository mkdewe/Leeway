using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace Leeway.UI.Editor
{
    /// <summary>
    /// Leeway -> UI -> Apply Leeway Theme to Open Scene: restyles every canvas in the open scene to the
    /// game's look (GDD 4.2) - dark teal panels with rounded corners, the Baloo 2 typeface (rounded, with full Polish diacritics), and the
    /// warm orange accent the world uses for its glowing details - and fixes the layout so it fits
    /// any aspect ratio.
    /// </summary>
    /// <remarks>
    /// <para>A restyle rather than a rebuild: the scene's UI is authored by hand and its scripts hold
    /// references into it, so nothing is created or destroyed except the palette's scroll viewport.
    /// Running it twice gives the same result.</para>
    ///
    /// <para>Colour swatches (objects named <c>Tile_*</c>) keep their colour - they <b>are</b> the
    /// content. Buttons keep whatever colour a script gives them at runtime; the theme only sets the
    /// starting colour and the hover/press tint on top of it.</para>
    /// </remarks>
    public static class LeewayUiTheme
    {
        private const string FontPath = "Assets/_Project/Art/Fonts/Baloo2.ttf";
        private const string FontAssetPath = "Assets/_Project/Art/Fonts/Baloo2 SDF.asset";
        private const string FallbackFontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
        private const string SpriteDir = "Assets/_Project/Art/UI";

        // The palette from GDD 4.2, darkened for panels, plus the accent of the world's glowing details.
        public static readonly Color PanelColor = new(0.075f, 0.13f, 0.165f, 0.92f);
        public static readonly Color FieldColor = new(0.05f, 0.09f, 0.115f, 1f);
        public static readonly Color ButtonColor = new(0.165f, 0.29f, 0.315f, 1f);
        public static readonly Color AccentColor = new(0.88f, 0.48f, 0.18f, 1f);
        public static readonly Color TextColor = new(0.91f, 0.955f, 0.92f, 1f);
        public static readonly Color AccentTextColor = new(0.11f, 0.07f, 0.03f, 1f);
        public static readonly Color TitleColor = new(0.79f, 0.894f, 0.792f, 1f);

        public static readonly Color DangerColor = new(0.62f, 0.22f, 0.2f, 1f);
        public static readonly Color BackdropColor = new(0.05f, 0.09f, 0.115f, 1f);

        /// <summary>Buttons that commit something - they get the accent.</summary>
        private static readonly string[] PrimaryButtons =
            { "ApplyButton", "SaveButton", "PlaySolo", "Create", "JoinCode", "Connect", "Start", "Close" };

        /// <summary>Buttons that leave or destroy something.</summary>
        private static readonly string[] DangerButtons = { "Leave" };

        [MenuItem("Leeway/UI/Apply Leeway Theme to Open Scene")]
        public static void ApplyToOpenScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            ApplyToScene(scene);
            EditorSceneManager.SaveScene(scene);
        }

        /// <summary>Restyles every canvas in <paramref name="scene"/>. Does not save - the caller decides.</summary>
        public static void ApplyToScene(Scene scene)
        {
            Theme theme = LoadTheme();
            int canvases = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Canvas canvas in root.GetComponentsInChildren<Canvas>(true))
            {
                if (!canvas.isRootCanvas) continue;
                ApplyToCanvas(canvas, theme);
                canvases++;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[LeewayUiTheme] Restyled {canvases} canvas(es) in {scene.name}.");
        }

        /// <summary>Restyles a UI prefab (list rows, dialogs) the scene instantiates at runtime.</summary>
        public static void ApplyToPrefab(string prefabPath)
        {
            Theme theme = LoadTheme();
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            ApplyToHierarchy(root.transform, theme);
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            PrefabUtility.UnloadPrefabContents(root);
        }

        private readonly struct Theme
        {
            public readonly TMP_FontAsset Font;
            public readonly Sprite Panel;
            public readonly Sprite Button;

            public Theme(TMP_FontAsset font, Sprite panel, Sprite button)
            {
                Font = font;
                Panel = panel;
                Button = button;
            }
        }

        private static Theme LoadTheme() => new(
            EnsureFontAsset(),
            EnsureRoundedSprite("UI_Panel", size: 96, radius: 20, border: 3),
            EnsureRoundedSprite("UI_Button", size: 64, radius: 14, border: 0));

        private static void ApplyToCanvas(Canvas canvas, Theme theme)
        {
            // Expand: the whole 1920x1080 reference area always fits the window, whatever its aspect -
            // a wide screen gets room between the side columns, a tall or narrow one gets room below.
            // Matching only height still cut the columns off on narrow windows.
            if (canvas.TryGetComponent(out CanvasScaler scaler))
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
                EditorUtility.SetDirty(scaler);
            }

            ApplyToHierarchy(canvas.transform, theme);
            MakePaletteScrollable(canvas.transform);
            StackLeftColumn(canvas.transform);
        }

        private static void ApplyToHierarchy(Transform root, Theme theme)
        {
            foreach (Image image in root.GetComponentsInChildren<Image>(true))
                StyleImage(image, theme.Panel, theme.Button);

            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
                StyleText(text, theme.Font);

            foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                StyleSerializedColours(behaviour);
        }

        // ---------------------------------------------------------------- images and buttons

        private static void StyleImage(Image image, Sprite panel, Sprite button)
        {
            string name = image.gameObject.name;
            bool swatch = name.StartsWith("Tile_");
            var selectable = image.GetComponent<Selectable>();

            if (selectable != null && selectable.targetGraphic == image)
            {
                SetSprite(image, button);
                if (!swatch) image.color = IsPrimary(name) ? AccentColor : IsDanger(name) ? DangerColor : ButtonColor;
                SetTint(selectable);
            }
            else if (name == "Background")
            {
                // The menu backdrop: the darkest tone of the palette, not a panel.
                image.color = BackdropColor;
            }
            else if (name.StartsWith("Glow"))
            {
                // The soft blobs behind the menu: one in the world's teal, one in its warm accent.
                image.color = name.EndsWith("A") ? new Color(0.23f, 0.38f, 0.39f, 0.5f) : new Color(0.88f, 0.48f, 0.18f, 0.12f);
            }
            else if (image.GetComponentInParent<TMP_InputField>(true) is { } field && field.targetGraphic == image)
            {
                SetSprite(image, button);
                image.color = FieldColor;
            }
            else if (IsPanel(image))
            {
                SetSprite(image, panel);
                image.color = PanelColor;
            }
            else if (swatch)
            {
                SetSprite(image, button);
            }

            EditorUtility.SetDirty(image);
        }

        /// <summary>
        /// Panels go by name. Not "every direct child of the canvas": that would also catch the menu's
        /// full-screen backdrop and the dimmers behind its dialogs.
        /// </summary>
        private static bool IsPanel(Image image)
            => image.GetComponent<Selectable>() == null
               && (image.gameObject.name.EndsWith("Panel") || image.gameObject.name == "Card");

        private static bool IsPrimary(string name) => System.Array.IndexOf(PrimaryButtons, name) >= 0;

        private static bool IsDanger(string name) => System.Array.IndexOf(DangerButtons, name) >= 0;

        private static void SetSprite(Image image, Sprite sprite)
        {
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 1f;
        }

        /// <summary>
        /// Hover brightens and press darkens whatever colour the button has - including colours scripts
        /// set at runtime (tab highlights, preset rows). The multiplier puts "normal" back at 1.
        /// </summary>
        private static void SetTint(Selectable selectable)
        {
            const float multiplier = 1.3f;
            float normal = 1f / multiplier;

            selectable.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = selectable.colors;
            colors.colorMultiplier = multiplier;
            colors.normalColor = new Color(normal, normal, normal, 1f);
            colors.highlightedColor = Color.white;
            colors.selectedColor = new Color(normal, normal, normal, 1f);
            colors.pressedColor = new Color(0.6f, 0.6f, 0.6f, 1f);
            colors.disabledColor = new Color(0.4f, 0.4f, 0.4f, 0.55f);
            colors.fadeDuration = 0.08f;
            selectable.colors = colors;
            EditorUtility.SetDirty(selectable);
        }

        // ---------------------------------------------------------------- text

        private static void StyleText(TMP_Text text, TMP_FontAsset font)
        {
            text.font = font;
            text.fontSharedMaterial = font.material;

            // A box authored tight around another font's line would truncate this one's to nothing.
            if (text.overflowMode is TextOverflowModes.Ellipsis or TextOverflowModes.Truncate
                && ((RectTransform)text.transform).rect.height < text.fontSize * 1.3f)
                text.overflowMode = TextOverflowModes.Overflow;

            var button = text.GetComponentInParent<Button>(true);
            if (button != null && IsPrimary(button.gameObject.name)) text.color = AccentTextColor;
            else if (text.gameObject.name == "Title") text.color = TitleColor;
            else if (button != null || text.color.grayscale > 0.5f) text.color = TextColor;

            EditorUtility.SetDirty(text);
        }

        /// <summary>Colour fields on the UI scripts themselves (tab highlights, preset rows), by field name.</summary>
        private static void StyleSerializedColours(MonoBehaviour behaviour)
        {
            if (behaviour == null) return;

            var so = new SerializedObject(behaviour);
            bool changed = false;
            changed |= SetColour(so, "_activeTabColor", AccentColor);
            changed |= SetColour(so, "_inactiveTabColor", ButtonColor);
            // Preset rows: these two are the row's TEXT colour, not its background.
            changed |= SetColour(so, "_savedColor", TextColor);
            changed |= SetColour(so, "_builtInColor", new Color(0.98f, 0.78f, 0.52f, 1f));
            if (changed) so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static bool SetColour(SerializedObject so, string field, Color colour)
        {
            SerializedProperty property = so.FindProperty(field);
            if (property == null || property.propertyType != SerializedPropertyType.Color) return false;
            property.colorValue = colour;
            return true;
        }

        // ---------------------------------------------------------------- layout

        /// <summary>
        /// The part palette outgrew its panel once the artist's parts arrived: the grid ran past the
        /// panel's bottom and off the screen. The grid now sits in a masked, scrolling viewport.
        /// </summary>
        private static void MakePaletteScrollable(Transform canvasRoot)
        {
            Transform panel = canvasRoot.Find("PalettePanel");
            Transform content = panel != null ? panel.Find("Content") ?? panel.Find("Viewport/Content") : null;
            if (panel == null || content == null) return;

            Transform viewport = panel.Find("Viewport");
            if (viewport == null)
            {
                var go = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
                viewport = go.transform;
                viewport.SetParent(panel, false);
                viewport.SetSiblingIndex(content.GetSiblingIndex());
            }

            var viewportRect = (RectTransform)viewport;
            viewportRect.anchorMin = new Vector2(0f, 0f);
            viewportRect.anchorMax = new Vector2(1f, 1f);
            viewportRect.offsetMin = new Vector2(10f, 12f);
            viewportRect.offsetMax = new Vector2(-10f, -120f);

            content.SetParent(viewport, false);
            var contentRect = (RectTransform)content;
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0f, contentRect.sizeDelta.y);

            if (!content.TryGetComponent(out ContentSizeFitter fitter)) fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            if (!panel.TryGetComponent(out ScrollRect scroll)) scroll = panel.gameObject.AddComponent<ScrollRect>();
            scroll.content = contentRect;
            scroll.viewport = viewportRect;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;

            EditorUtility.SetDirty(panel.gameObject);
        }

        /// <summary>
        /// The stats panel and the skin panel used to sit at fixed offsets, with the stats lines at fixed
        /// heights too - so a longer line or a taller font ran one text into the next, and the skin panel
        /// into the stats. Now the stats panel lays its lines and buttons out by their real size, and a
        /// column stacks the two panels under each other.
        /// </summary>
        private static void StackLeftColumn(Transform canvasRoot)
        {
            Transform stats = canvasRoot.Find("StatsPanel") ?? canvasRoot.Find("LeftColumn/StatsPanel");
            Transform skin = canvasRoot.Find("SkinPanel") ?? canvasRoot.Find("LeftColumn/SkinPanel");
            if (stats == null || skin == null) return;

            Transform column = canvasRoot.Find("LeftColumn");
            if (column == null)
            {
                column = new GameObject("LeftColumn", typeof(RectTransform)).transform;
                column.SetParent(canvasRoot, false);
                column.SetSiblingIndex(stats.GetSiblingIndex());
            }

            var columnRect = (RectTransform)column;
            columnRect.anchorMin = columnRect.anchorMax = new Vector2(0f, 1f);
            columnRect.pivot = new Vector2(0f, 1f);
            columnRect.anchoredPosition = new Vector2(48f, -48f);
            columnRect.sizeDelta = new Vector2(420f, 0f);

            VerticalLayoutGroup columnLayout = Ensure<VerticalLayoutGroup>(column.gameObject);
            columnLayout.spacing = 14f;
            columnLayout.childControlWidth = true;
            columnLayout.childControlHeight = true;
            columnLayout.childForceExpandWidth = true;
            columnLayout.childForceExpandHeight = false;
            ContentSizeFitter columnFit = Ensure<ContentSizeFitter>(column.gameObject);
            columnFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // The skin panel keeps its authored size - its contents are placed absolutely inside it.
            float skinHeight = ((RectTransform)skin).rect.height > 1f ? ((RectTransform)skin).rect.height : 356f;
            stats.SetParent(column, false);
            skin.SetParent(column, false);
            stats.SetSiblingIndex(0);
            skin.SetSiblingIndex(1);
            Ensure<LayoutElement>(skin.gameObject).preferredHeight = skinHeight;

            VerticalLayoutGroup statsLayout = Ensure<VerticalLayoutGroup>(stats.gameObject);
            statsLayout.padding = new RectOffset(16, 16, 14, 16);
            statsLayout.spacing = 6f;
            statsLayout.childControlWidth = true;
            statsLayout.childControlHeight = true;
            statsLayout.childForceExpandWidth = true;
            statsLayout.childForceExpandHeight = false;

            foreach (Transform child in stats)
            {
                if (!child.TryGetComponent(out Button button)) continue;
                LayoutElement element = Ensure<LayoutElement>(button.gameObject);
                element.preferredHeight = 44f;
            }

            EditorUtility.SetDirty(column.gameObject);
        }

        private static T Ensure<T>(GameObject go) where T : Component
            => go.TryGetComponent(out T existing) ? existing : go.AddComponent<T>();

        // ---------------------------------------------------------------- assets

        private static TMP_FontAsset EnsureFontAsset()
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (existing != null)
            {
                TightenLatinMetrics(existing);
                return existing;
            }

            var source = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            TMP_FontAsset font = TMP_FontAsset.CreateFontAsset(source, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024,
                AtlasPopulationMode.Dynamic, enableMultiAtlasSupport: true);
            font.name = "Baloo2 SDF";

            AssetDatabase.CreateAsset(font, FontAssetPath);
            font.atlasTextures[0].name = "Baloo2 SDF Atlas";
            AssetDatabase.AddObjectToAsset(font.atlasTextures[0], font);
            font.material.name = "Baloo2 SDF Material";
            AssetDatabase.AddObjectToAsset(font.material, font);

            // Anything Baloo 2 lacks still renders.
            var fallback = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FallbackFontPath);
            if (fallback != null) font.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset> { fallback };

            TightenLatinMetrics(font);
            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssets();
            return font;
        }

        /// <summary>
        /// Baloo 2 also covers Devanagari, so its vertical metrics leave room for marks far above and below
        /// a Latin line: a line is 1.6 em tall. TMP sizes text boxes from these numbers, so every label
        /// authored for an ordinary font came out "too small" and truncated to nothing. Latin with Polish
        /// accents needs the cap height plus room for an ogonek or kreska - this only changes layout, the
        /// glyphs are drawn exactly as before.
        /// </summary>
        private static void TightenLatinMetrics(TMP_FontAsset font)
        {
            UnityEngine.TextCore.FaceInfo face = font.faceInfo;
            float em = face.pointSize;
            face.ascentLine = face.capLine + 0.28f * em;
            face.descentLine = -0.26f * em;
            face.lineHeight = face.ascentLine - face.descentLine + 0.06f * em;
            font.faceInfo = face;
            EditorUtility.SetDirty(font);
        }

        /// <summary>
        /// A white rounded rectangle for 9-slicing. With a border, the rim is pure white and the fill
        /// slightly darker, so the tint colour gives a panel a faint lighter edge for free.
        /// </summary>
        private static Sprite EnsureRoundedSprite(string name, int size, int radius, int border)
        {
            Directory.CreateDirectory(SpriteDir);
            string path = $"{SpriteDir}/{name}.png";

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = RoundedRectDistance(x + 0.5f, y + 0.5f, size, radius);
                float alpha = Mathf.Clamp01(0.5f - d);
                float shade = border > 0 && d > -border ? 1f : 0.82f;
                texture.SetPixel(x, y, new Color(shade, shade, shade, alpha));
            }
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.spriteBorder = new Vector4(radius + 2, radius + 2, radius + 2, radius + 2);
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        /// <summary>Signed distance to a rounded square's edge (negative inside), in pixels.</summary>
        private static float RoundedRectDistance(float x, float y, int size, int radius)
        {
            float half = size * 0.5f;
            float qx = Mathf.Abs(x - half) - (half - radius);
            float qy = Mathf.Abs(y - half) - (half - radius);
            float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
            return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
        }
    }
}
