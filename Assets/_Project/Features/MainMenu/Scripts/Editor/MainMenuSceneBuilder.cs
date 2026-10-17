using System.Linq;
using Leeway.MainMenu.Dialogs;
using Leeway.MainMenu.Navigation;
using Leeway.MainMenu.Views;
using Leeway.MainMenu.Views.Widgets;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static Leeway.MainMenu.Editor.MenuUiKit;

namespace Leeway.MainMenu.Editor
{
    /// <summary>
    /// Generates the main menu: widget prefabs, dialog prefabs and the <c>MainMenu</c> scene with every
    /// screen wired to <see cref="MainMenuLifetimeScope"/>.
    /// </summary>
    /// <remarks>
    /// <para>The menu is code-generated so its structure lives in review-able C# rather than in
    /// thousands of lines of scene YAML. Running it again <b>overwrites</b> the scene and the menu
    /// prefabs — tweak the layout here, or stop using the generator once the menu gets an art pass.</para>
    ///
    /// <para>The network assets (<see cref="NetworkAssetsSetup"/>) are set up first; they are never
    /// overwritten.</para>
    /// </remarks>
    internal static class MainMenuSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/MainMenu.unity";
        public const string PrefabFolder = "Assets/_Project/Features/MainMenu/Prefabs";

        [MenuItem("Leeway/Online/Build Main Menu Scene", priority = 101)]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            NetworkAssetsSetup.Run();
            NetworkAssetsSetup.EnsureFolder(PrefabFolder);

            // New scene first: opening a scene unloads unreferenced assets, which would invalidate
            // the freshly saved prefab handles below.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var lobbyItem = BuildLobbyListItemPrefab();
            var memberItem = BuildMemberItemPrefab();
            var busyPrefab = BuildBusyOverlayPrefab();
            var dialogPrefab = BuildMessageDialogPrefab();
            BuildSceneContents(lobbyItem, memberItem, busyPrefab, dialogPrefab);

            // The game's look (rounded teal panels, Baloo 2, orange accent) over the generated layout,
            // so rebuilding the menu does not throw the theme away.
            Leeway.UI.Editor.LeewayUiTheme.ApplyToScene(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            foreach (Object prefab in new Object[] { lobbyItem, memberItem, busyPrefab, dialogPrefab })
                Leeway.UI.Editor.LeewayUiTheme.ApplyToPrefab(AssetDatabase.GetAssetPath(prefab));
            AddSceneToBuildFirst(ScenePath);

            AssetDatabase.SaveAssets();
            Debug.Log($"[MainMenuSceneBuilder] Built {ScenePath}.");
        }

        // ---- Scene -------------------------------------------------------------------------------

        private static void BuildSceneContents(LobbyListItemView lobbyItem, LobbyMemberItemView memberItem, BusyOverlayView busyPrefab, MessageDialogView dialogPrefab)
        {
            var cameraGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraGo.tag = "MainCamera";
            var camera = cameraGo.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Background;
            camera.orthographic = true;

            var lightGo = new GameObject("Directional Light", typeof(Light));
            lightGo.GetComponent<Light>().type = LightType.Directional;
            lightGo.transform.rotation = Quaternion.Euler(50, -30, 0);

            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.layer = LayerMask.NameToLayer("UI");
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            var root = canvasGo.transform;

            BuildBackground(root);

            var screens = Rect("Screens", root).Stretch();
            var main = BuildMainScreen(screens);
            var create = BuildCreateLobbyScreen(screens);
            var browser = BuildLobbyBrowserScreen(screens, lobbyItem);
            var join = BuildJoinScreen(screens);
            var room = BuildLobbyRoomScreen(screens, memberItem);

            var busy = ((GameObject)PrefabUtility.InstantiatePrefab(busyPrefab.gameObject, root)).GetComponent<BusyOverlayView>();
            var dialog = ((GameObject)PrefabUtility.InstantiatePrefab(dialogPrefab.gameObject, root)).GetComponent<MessageDialogView>();

            var scopeGo = new GameObject("MainMenuLifetimeScope");
            var scope = scopeGo.AddComponent<MainMenuLifetimeScope>();
            Wire(scope, "_mainScreen", main);
            Wire(scope, "_createLobbyScreen", create);
            Wire(scope, "_lobbyBrowserScreen", browser);
            Wire(scope, "_joinScreen", join);
            Wire(scope, "_lobbyRoomScreen", room);
            Wire(scope, "_busyOverlay", busy);
            Wire(scope, "_messageDialog", dialog);
        }

        private static void BuildBackground(Transform root)
        {
            var bg = Panel("Background", root, Background, sprite: null);
            bg.sprite = null;
            bg.type = Image.Type.Simple;
            bg.rectTransform.Stretch();
            bg.raycastTarget = false;

            // Two soft glows — a hint of something alive under a microscope, without needing art.
            var glowA = Panel("Glow A", bg.transform, new Color(BackgroundGlow.r, BackgroundGlow.g, BackgroundGlow.b, 0.55f), Circle);
            glowA.type = Image.Type.Simple;
            glowA.raycastTarget = false;
            glowA.rectTransform.anchorMin = glowA.rectTransform.anchorMax = new Vector2(0.78f, 0.62f);
            glowA.rectTransform.sizeDelta = new Vector2(1100, 1100);

            var glowB = Panel("Glow B", bg.transform, new Color(0.10f, 0.16f, 0.30f, 0.45f), Circle);
            glowB.type = Image.Type.Simple;
            glowB.raycastTarget = false;
            glowB.rectTransform.anchorMin = glowB.rectTransform.anchorMax = new Vector2(0.12f, 0.08f);
            glowB.rectTransform.sizeDelta = new Vector2(900, 900);
        }

        private static T Screen<T>(Transform parent, MenuScreen id) where T : ScreenView
        {
            var rt = Rect(id + "Screen", parent).Stretch();
            rt.gameObject.AddComponent<CanvasGroup>();
            var view = rt.gameObject.AddComponent<T>();
            WireEnum(view, "_screen", (int)id);
            return view;
        }

        /// <summary>A centred card with a title, a body that fills it, and a footer row.</summary>
        private static (RectTransform body, RectTransform footer) Card(Transform screen, string title, float width, float height)
        {
            var card = Panel("Card", screen, CardColor);
            card.rectTransform.Center(width, height);
            Vertical(card.gameObject, 24, new RectOffset(48, 48, 40, 40));

            var heading = MakeLabel("Title", card.transform, title, 44, TextColor, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            heading.Size(height: 56);

            var body = Rect("Body", card.transform);
            body.Size(flexHeight: 1);

            var footer = Rect("Footer", card.transform);
            Horizontal(footer.gameObject, 16);
            footer.Size(height: 60);
            return (body, footer);
        }

        private static RectTransform FormRow(Transform parent, string label, float height = 56)
        {
            var row = Rect(label + " Row", parent);
            Horizontal(row.gameObject, 24);
            row.Size(height: height);
            MakeLabel("Label", row, label, 24, TextMuted).Size(width: 220, height: height);
            return row;
        }

        private static MainScreenView BuildMainScreen(Transform parent)
        {
            var view = Screen<MainScreenView>(parent, MenuScreen.Main);

            var column = Rect("Column", view.transform);
            column.anchorMin = new Vector2(0, 0);
            column.anchorMax = new Vector2(0, 1);
            column.pivot = new Vector2(0, 0.5f);
            column.sizeDelta = new Vector2(620, 0);
            column.anchoredPosition = new Vector2(150, 0);
            Vertical(column.gameObject, 18, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleLeft);

            MakeLabel("Title", column, "LEEWAY", 132, TextColor, TextAlignmentOptions.MidlineLeft, FontStyles.Bold).Size(height: 150);
            MakeLabel("Tagline", column, "Evolve together.", 32, Accent).Size(height: 48);
            Spacer(column, 0, 0).Size(height: 36);

            var solo = MakeButton("PlaySolo", column, "Play solo", ButtonStyle.Primary, height: 68, fontSize: 28);
            var create = MakeButton("CreateLobby", column, "Create lobby", ButtonStyle.Secondary, height: 68, fontSize: 28);
            var browse = MakeButton("BrowseLobbies", column, "Browse lobbies", ButtonStyle.Secondary, height: 68, fontSize: 28);
            var join = MakeButton("Join", column, "Join by code or IP", ButtonStyle.Secondary, height: 68, fontSize: 28);
            Spacer(column, 0, 0).Size(height: 12);
            var quit = MakeButton("Quit", column, "Quit", ButtonStyle.Ghost, height: 60, fontSize: 24);

            var footer = Rect("Identity", view.transform);
            // Bottom-right, away from the button column, which is centred vertically and reaches the
            // bottom edge on short (ultrawide) screens.
            footer.anchorMin = footer.anchorMax = footer.pivot = new Vector2(1, 0);
            footer.anchoredPosition = new Vector2(-80, 56);
            footer.sizeDelta = new Vector2(900, 80);
            Vertical(footer.gameObject, 4);
            var player = MakeLabel("Player", footer, "", 28, TextColor, TextAlignmentOptions.BottomRight, FontStyles.Bold);
            var platform = MakeLabel("Platform", footer, "", 22, TextMuted, TextAlignmentOptions.TopRight);

            Wire(view, "_playSoloButton", solo);
            Wire(view, "_createLobbyButton", create);
            Wire(view, "_browseLobbiesButton", browse);
            Wire(view, "_joinButton", join);
            Wire(view, "_quitButton", quit);
            Wire(view, "_platformLabel", platform);
            Wire(view, "_playerLabel", player);
            return view;
        }

        private static CreateLobbyScreenView BuildCreateLobbyScreen(Transform parent)
        {
            var view = Screen<CreateLobbyScreenView>(parent, MenuScreen.CreateLobby);
            var (body, footer) = Card(view.transform, "Create lobby", 860, 620);
            Vertical(body.gameObject, 18);

            var name = MakeInput("NameInput", FormRow(body, "Name"), "Lobby name");
            name.characterLimit = 40;
            name.Size(flexWidth: 1);
            var mode = MakeDropdown("ModeDropdown", FormRow(body, "Mode"));
            mode.Size(flexWidth: 1);
            var visibility = MakeDropdown("VisibilityDropdown", FormRow(body, "Who can join"));
            visibility.Size(flexWidth: 1);

            var playersRow = FormRow(body, "Max players");
            var slider = MakeSlider("MaxPlayersSlider", playersRow);
            slider.Size(flexWidth: 1);
            var count = MakeLabel("MaxPlayersValue", playersRow, "4", 28, TextColor, TextAlignmentOptions.Center, FontStyles.Bold);
            count.Size(width: 60);

            var back = MakeButton("Back", footer, "Back", ButtonStyle.Secondary);
            back.Size(width: 200);
            Spacer(footer);
            var create = MakeButton("Create", footer, "Create lobby", ButtonStyle.Primary);
            create.Size(width: 280);

            Wire(view, "_nameInput", name);
            Wire(view, "_modeDropdown", mode);
            Wire(view, "_visibilityDropdown", visibility);
            Wire(view, "_maxPlayersSlider", slider);
            Wire(view, "_maxPlayersLabel", count);
            Wire(view, "_createButton", create);
            Wire(view, "_backButton", back);
            return view;
        }

        private static LobbyBrowserScreenView BuildLobbyBrowserScreen(Transform parent, LobbyListItemView itemPrefab)
        {
            var view = Screen<LobbyBrowserScreenView>(parent, MenuScreen.LobbyBrowser);
            var (body, footer) = Card(view.transform, "Browse lobbies", 1300, 860);
            Vertical(body.gameObject, 14);

            var filters = Rect("Filters", body);
            Horizontal(filters.gameObject, 14);
            filters.Size(height: 56);
            var search = MakeInput("SearchInput", filters, "Search by name…");
            search.Size(flexWidth: 1);
            var mode = MakeDropdown("ModeDropdown", filters);
            mode.Size(width: 240);
            var freeSlots = MakeToggle("FreeSlotsToggle", filters, "Has free slots", true);
            freeSlots.Size(width: 240);
            var refresh = MakeButton("Refresh", filters, "Refresh", ButtonStyle.Secondary, height: 56);
            refresh.Size(width: 170);

            var header = Rect("Columns", body);
            Horizontal(header.gameObject, 16, new RectOffset(28, 28, 0, 0));
            header.Size(height: 32);
            MakeLabel("Name", header, "LOBBY", 18, TextMuted, style: FontStyles.Bold).Size(flexWidth: 1);
            MakeLabel("Mode", header, "MODE", 18, TextMuted, style: FontStyles.Bold).Size(width: 180);
            MakeLabel("Players", header, "PLAYERS", 18, TextMuted, style: FontStyles.Bold).Size(width: 120);
            Spacer(header, 0).Size(width: 150);

            var listArea = Rect("ListArea", body);
            listArea.Size(flexHeight: 1);
            var scroll = ScrollList("LobbyList", listArea, out var content);
            ((RectTransform)scroll.transform).Stretch();
            var status = MakeLabel("Status", listArea, "Searching…", 26, TextMuted, TextAlignmentOptions.Center);
            status.rectTransform.Stretch(40, 40, 40, 40);

            var back = MakeButton("Back", footer, "Back", ButtonStyle.Secondary);
            back.Size(width: 200);
            Spacer(footer);

            Wire(view, "_searchInput", search);
            Wire(view, "_modeDropdown", mode);
            Wire(view, "_freeSlotsToggle", freeSlots);
            Wire(view, "_refreshButton", refresh);
            Wire(view, "_backButton", back);
            Wire(view, "_listContent", content);
            Wire(view, "_itemPrefab", itemPrefab);
            Wire(view, "_statusLabel", status);
            return view;
        }

        private static JoinScreenView BuildJoinScreen(Transform parent)
        {
            var view = Screen<JoinScreenView>(parent, MenuScreen.Join);
            var (body, footer) = Card(view.transform, "Join a game", 900, 680);
            Vertical(body.gameObject, 14);

            var codeSection = Rect("CodeSection", body);
            Vertical(codeSection.gameObject, 12);
            MakeLabel("CodeTitle", codeSection, "With a lobby code", 26, TextColor, style: FontStyles.Bold).Size(height: 36);
            MakeLabel("CodeHint", codeSection, "Ask the host for the code shown in their lobby.", 20, TextMuted).Size(height: 28);
            var codeRow = Rect("CodeRow", codeSection);
            Horizontal(codeRow.gameObject, 14);
            codeRow.Size(height: 56);
            var code = MakeInput("CodeInput", codeRow, "Lobby code");
            code.Size(flexWidth: 1);
            var paste = MakeButton("Paste", codeRow, "Paste", ButtonStyle.Secondary, height: 56);
            paste.Size(width: 140);
            var joinCode = MakeButton("JoinCode", codeRow, "Join", ButtonStyle.Primary, height: 56);
            joinCode.Size(width: 160);

            var divider = Panel("Divider", body, new Color(1, 1, 1, 0.06f));
            divider.Size(height: 2);

            MakeLabel("DirectTitle", body, "Direct connection", 26, TextColor, style: FontStyles.Bold).Size(height: 36);
            MakeLabel("DirectHint", body, "LAN or a dedicated server. The host needs the game port open.", 20, TextMuted).Size(height: 28);
            var directRow = Rect("DirectRow", body);
            Horizontal(directRow.gameObject, 14);
            directRow.Size(height: 56);
            var address = MakeInput("AddressInput", directRow, "IP address or host name");
            address.Size(flexWidth: 1);
            var port = MakeInput("PortInput", directRow, "Port");
            port.characterLimit = 5;
            port.Size(width: 140);
            var connect = MakeButton("Connect", directRow, "Connect", ButtonStyle.Primary, height: 56);
            connect.Size(width: 160);

            var back = MakeButton("Back", footer, "Back", ButtonStyle.Secondary);
            back.Size(width: 200);
            Spacer(footer);

            Wire(view, "_codeSection", codeSection.gameObject);
            Wire(view, "_codeInput", code);
            Wire(view, "_pasteCodeButton", paste);
            Wire(view, "_joinCodeButton", joinCode);
            Wire(view, "_addressInput", address);
            Wire(view, "_portInput", port);
            Wire(view, "_connectButton", connect);
            Wire(view, "_backButton", back);
            return view;
        }

        private static LobbyRoomScreenView BuildLobbyRoomScreen(Transform parent, LobbyMemberItemView memberPrefab)
        {
            var view = Screen<LobbyRoomScreenView>(parent, MenuScreen.LobbyRoom);

            var card = Panel("Card", view.transform, CardColor);
            card.rectTransform.Center(1560, 900);
            Vertical(card.gameObject, 24, new RectOffset(48, 48, 40, 40));

            // Header: name and details on the left, the shareable code on the right.
            var header = Rect("Header", card.transform);
            Horizontal(header.gameObject, 24);
            header.Size(height: 96);
            var titles = Rect("Titles", header);
            Vertical(titles.gameObject, 4);
            titles.Size(flexWidth: 1);
            var title = MakeLabel("Title", titles, "Lobby", 44, TextColor, style: FontStyles.Bold);
            title.Size(height: 56);
            var details = MakeLabel("Details", titles, "", 22, TextMuted);
            details.Size(height: 30);

            var codeBox = Panel("CodeBox", header, Field);
            Horizontal(codeBox.gameObject, 12, new RectOffset(20, 10, 8, 8));
            codeBox.Size(width: 520, height: 64);
            MakeLabel("CodeCaption", codeBox.transform, "CODE", 18, TextMuted, style: FontStyles.Bold).Size(width: 60);
            var code = MakeLabel("Code", codeBox.transform, "", 24, Accent, style: FontStyles.Bold);
            code.Size(flexWidth: 1);
            var copy = MakeButton("Copy", codeBox.transform, "Copy", ButtonStyle.Secondary, height: 46, fontSize: 22);
            copy.Size(width: 110);

            // Body: players | chat.
            var columns = Rect("Columns", card.transform);
            Horizontal(columns.gameObject, 28).childForceExpandHeight = true;
            columns.Size(flexHeight: 1);

            var players = Rect("Players", columns);
            Vertical(players.gameObject, 12, expandHeight: false);
            players.Size(flexWidth: 1);
            var membersCount = MakeLabel("Count", players, "Players", 22, TextMuted, style: FontStyles.Bold);
            membersCount.Size(height: 32);
            var membersScroll = ScrollList("Members", players, out var membersContent, spacing: 8);
            membersScroll.Size(flexHeight: 1);

            var chat = Rect("Chat", columns);
            Vertical(chat.gameObject, 12);
            chat.Size(flexWidth: 1.2f);
            MakeLabel("ChatTitle", chat, "CHAT", 22, TextMuted, style: FontStyles.Bold).Size(height: 32);
            var chatScroll = ScrollList("ChatLog", chat, out var chatContent, spacing: 0);
            chatScroll.Size(flexHeight: 1);
            var log = MakeLabel("Log", chatContent, "", 22, TextColor, TextAlignmentOptions.BottomLeft);
            log.richText = true;
            log.overflowMode = TextOverflowModes.Overflow;
            var chatRow = Rect("Input", chat);
            Horizontal(chatRow.gameObject, 12);
            chatRow.Size(height: 56);
            var chatInput = MakeInput("ChatInput", chatRow, "Say something…");
            chatInput.characterLimit = 200;
            chatInput.Size(flexWidth: 1);
            var send = MakeButton("Send", chatRow, "Send", ButtonStyle.Secondary, height: 56);
            send.Size(width: 130);

            // Footer: leave on the left, the primary action on the right.
            var footer = Rect("Footer", card.transform);
            Horizontal(footer.gameObject, 16);
            footer.Size(height: 64);
            var leave = MakeButton("Leave", footer, "Leave lobby", ButtonStyle.Danger, height: 64);
            leave.Size(width: 230);
            Spacer(footer);
            var hint = MakeLabel("StartHint", footer, "", 22, TextMuted, TextAlignmentOptions.MidlineRight);
            hint.Size(width: 420);
            var invite = MakeButton("Invite", footer, "Invite friends", ButtonStyle.Secondary, height: 64);
            invite.Size(width: 240);
            var ready = MakeButton("Ready", footer, "Ready", ButtonStyle.Secondary, out var readyLabel, height: 64);
            ready.Size(width: 240);
            var start = MakeButton("Start", footer, "Start match", ButtonStyle.Primary, height: 64);
            start.Size(width: 260);

            Wire(view, "_titleLabel", title);
            Wire(view, "_detailsLabel", details);
            Wire(view, "_codeLabel", code);
            Wire(view, "_copyCodeButton", copy);
            Wire(view, "_membersContent", membersContent);
            Wire(view, "_memberPrefab", memberPrefab);
            Wire(view, "_membersCountLabel", membersCount);
            Wire(view, "_chatPanel", chat.gameObject);
            Wire(view, "_chatLog", log);
            Wire(view, "_chatScroll", chatScroll);
            Wire(view, "_chatInput", chatInput);
            Wire(view, "_sendButton", send);
            Wire(view, "_readyButton", ready);
            Wire(view, "_readyButtonLabel", readyLabel);
            Wire(view, "_startButton", start);
            Wire(view, "_startHintLabel", hint);
            Wire(view, "_inviteButton", invite);
            Wire(view, "_leaveButton", leave);
            return view;
        }

        // ---- Prefabs -----------------------------------------------------------------------------

        private static LobbyListItemView BuildLobbyListItemPrefab()
        {
            var row = Panel("LobbyListItem", null, Row);
            row.rectTransform.sizeDelta = new Vector2(1100, 68);
            Horizontal(row.gameObject, 16, new RectOffset(20, 10, 6, 6));
            row.Size(height: 68);

            var name = MakeLabel("Name", row.transform, "Lobby", 26, TextColor, style: FontStyles.Bold);
            name.Size(flexWidth: 1);
            var mode = MakeLabel("Mode", row.transform, "Co-op", 22, TextMuted);
            mode.Size(width: 180);
            var players = MakeLabel("Players", row.transform, "1/4", 22, TextColor);
            players.Size(width: 120);
            var join = MakeButton("Join", row.transform, "Join", ButtonStyle.Primary, height: 52, fontSize: 22);
            join.Size(width: 150);

            var view = row.gameObject.AddComponent<LobbyListItemView>();
            Wire(view, "_nameLabel", name);
            Wire(view, "_modeLabel", mode);
            Wire(view, "_playersLabel", players);
            Wire(view, "_joinButton", join);
            return SavePrefab(row.gameObject, "LobbyListItem").GetComponent<LobbyListItemView>();
        }

        private static LobbyMemberItemView BuildMemberItemPrefab()
        {
            var row = Panel("LobbyMemberItem", null, Row);
            row.rectTransform.sizeDelta = new Vector2(600, 60);
            Horizontal(row.gameObject, 14, new RectOffset(18, 18, 6, 6));
            row.Size(height: 60);

            var dot = Panel("StatusDot", row.transform, TextMuted, Circle);
            dot.type = Image.Type.Simple;
            dot.Size(width: 16, height: 16);
            var name = MakeLabel("Name", row.transform, "Player", 24, TextColor);
            name.richText = true;
            name.Size(flexWidth: 1);
            var badge = Panel("HostBadge", row.transform, Accent);
            badge.Size(width: 76, height: 30);
            var badgeText = MakeLabel("Text", badge.transform, "HOST", 16, AccentText, TextAlignmentOptions.Center, FontStyles.Bold);
            badgeText.rectTransform.Stretch();
            var status = MakeLabel("Status", row.transform, "Not ready", 20, TextMuted, TextAlignmentOptions.MidlineRight);
            status.Size(width: 130);

            var view = row.gameObject.AddComponent<LobbyMemberItemView>();
            Wire(view, "_nameLabel", name);
            Wire(view, "_hostBadge", badge.gameObject);
            Wire(view, "_statusLabel", status);
            Wire(view, "_statusDot", dot);
            return SavePrefab(row.gameObject, "LobbyMemberItem").GetComponent<LobbyMemberItemView>();
        }

        private static BusyOverlayView BuildBusyOverlayPrefab()
        {
            var overlay = Panel("BusyOverlay", null, Overlay);
            overlay.sprite = null;
            overlay.type = Image.Type.Simple;
            overlay.rectTransform.Stretch();

            var card = Panel("Card", overlay.transform, CardColor);
            card.rectTransform.Center(560, 280);
            Vertical(card.gameObject, 20, new RectOffset(40, 40, 36, 32), TextAnchor.MiddleCenter);

            var spinnerHolder = Rect("SpinnerHolder", card.transform);
            spinnerHolder.Size(height: 56);
            var spinner = Panel("Spinner", spinnerHolder, Accent);
            spinner.rectTransform.Center(40, 40);
            var message = MakeLabel("Message", card.transform, "Working…", 26, TextColor, TextAlignmentOptions.Center);
            message.Size(height: 64);
            var cancel = MakeButton("Cancel", card.transform, "Cancel", ButtonStyle.Secondary, height: 54, fontSize: 22);
            cancel.Size(height: 54);

            var view = overlay.gameObject.AddComponent<BusyOverlayView>();
            Wire(view, "_messageLabel", message);
            Wire(view, "_cancelButton", cancel);
            Wire(view, "_spinner", spinner.rectTransform);
            overlay.gameObject.SetActive(false);
            return SavePrefab(overlay.gameObject, "BusyOverlay").GetComponent<BusyOverlayView>();
        }

        private static MessageDialogView BuildMessageDialogPrefab()
        {
            var overlay = Panel("MessageDialog", null, Overlay);
            overlay.sprite = null;
            overlay.type = Image.Type.Simple;
            overlay.rectTransform.Stretch();

            var card = Panel("Card", overlay.transform, CardColor);
            card.rectTransform.Center(680, 340);
            Vertical(card.gameObject, 18, new RectOffset(44, 44, 36, 36));

            var title = MakeLabel("Title", card.transform, "Title", 32, TextColor, style: FontStyles.Bold);
            title.Size(height: 44);
            var body = MakeLabel("Body", card.transform, "Message", 24, TextMuted, TextAlignmentOptions.TopLeft);
            body.Size(flexHeight: 1);
            var footer = Rect("Footer", card.transform);
            Horizontal(footer.gameObject, 16);
            footer.Size(height: 56);
            Spacer(footer);
            var ok = MakeButton("Close", footer, "OK", ButtonStyle.Primary, height: 56);
            ok.Size(width: 180);

            var view = overlay.gameObject.AddComponent<MessageDialogView>();
            Wire(view, "_titleLabel", title);
            Wire(view, "_bodyLabel", body);
            Wire(view, "_closeButton", ok);
            overlay.gameObject.SetActive(false);
            return SavePrefab(overlay.gameObject, "MessageDialog").GetComponent<MessageDialogView>();
        }

        private static GameObject SavePrefab(GameObject go, string name)
        {
            SetLayerRecursively(go);
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabFolder}/{name}.prefab");
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static void AddSceneToBuildFirst(string path)
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != path).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
