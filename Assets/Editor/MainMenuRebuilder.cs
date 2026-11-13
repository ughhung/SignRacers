#if UNITY_EDITOR
using SignRacers.Core;
using SignRacers.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace SignRacers.EditorTools
{
    /// <summary>
    /// Rebuilds the WHOLE main menu scene in the Kenney / UiSkin look (gold background, blue / green / grey buttons,
    /// framed boxes): Home (with your name), Solo word picker, Multiplayer (Quick play / Browse / Create / Join),
    /// Join with a code, and Browse public games. Everything is wired to MenuController for you.
    /// Sign Racers > Rebuild Main Menu (Kenney Style).
    /// Your old scene is first copied to Assets/Scenes/MainMenu_old.unity, then Assets/Scenes/MainMenu.unity is replaced.
    /// After this you do NOT need Restyle Solo Picker / Add Quick Play / Add Browse / Add Player Name: they are built in.
    /// </summary>
    public static class MainMenuRebuilder
    {
        private const string ScenePath = "Assets/Scenes/MainMenu.unity";
        private const string BackupPath = "Assets/Scenes/MainMenu_old.unity";

        [MenuItem("Sign Racers/Rebuild Main Menu (Kenney Style)")]
        public static void Build()
        {
            if (!EditorUtility.DisplayDialog("Rebuild Main Menu",
                    "This replaces " + ScenePath + " with a fresh menu in the Kenney style. Your current scene is first copied to " +
                    BackupPath + ". Continue?", "Rebuild", "Cancel"))
                return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            UiSkin skin = VotingBuilder.EnsureSkin();

            if (!AssetDatabase.IsValidFolder("Assets/Scenes")) AssetDatabase.CreateFolder("Assets", "Scenes");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            {
                AssetDatabase.DeleteAsset(BackupPath);
                AssetDatabase.CopyAsset(ScenePath, BackupPath);
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Camera (only paints the background colour; the menu itself is a screen-space canvas).
            var camObj = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camObj.tag = "MainCamera";
            var cam = camObj.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = skin.backgroundColor;

            // Event system.
            var es = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            es.AddComponent<InputSystemUIInputModule>();
#else
            es.AddComponent<StandaloneInputModule>();
#endif

            // Canvas.
            var canvasObj = new GameObject("MenuCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObj.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObj.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            var bgRect = Rect("Background", canvasObj.transform);
            Stretch(bgRect);
            var bg = bgRect.gameObject.AddComponent<Image>();
            bg.sprite = skin.background;
            bg.color = skin.background != null && !skin.tintSprites ? Color.white : skin.backgroundColor;
            bg.raycastTarget = false;

            var safe = Rect("SafeArea", canvasObj.transform);
            Stretch(safe);
            safe.gameObject.AddComponent<SafeArea>();

            // ============================== HOME ==============================
            var home = Panel("HomePanel", safe);
            Label(home, "Title", "SIGN RACERS", 150, TextAlignmentOptions.Center, skin.textColor, skin.font, true, PinTop(180, 1000, 190));
            Label(home, "Tagline", "Sign it. Boost it. Win.", 48, TextAlignmentOptions.Center, skin.textColor, Body(skin), false, PinTop(380, 900, 70));

            var nameButton = Btn(home, "NameButton", Pick(skin.frame), Color.white, "Blue Fox  (tap to change)", 46, skin.textColor, Body(skin), PinTop(560, 820, 120));
            if (skin.frame == null) nameButton.targetGraphic.color = new Color(1f, 1f, 1f, 0.35f);

            var soloButton = Btn(home, "SoloButton", Pick(skin.buttonGo), skin.Tint(skin.buttonGo, skin.goColor), "SOLO", 84, Color.white, skin.font, PinBottom(560, 860, 200));
            var multiplayerButton = Btn(home, "MultiplayerButton", Pick(skin.buttonMain), skin.Tint(skin.buttonMain, skin.mainColor), "MULTIPLAYER", 84, Color.white, skin.font, PinBottom(300, 860, 200));
            Label(home, "SoloHint", "Practice alone with bots", 36, TextAlignmentOptions.Center, skin.textColor, Body(skin), false, PinBottom(470, 860, 60));
            Label(home, "MultiHint", "Race friends or random players", 36, TextAlignmentOptions.Center, skin.textColor, Body(skin), false, PinBottom(210, 860, 60));

            // ============================== SOLO PICKER ==============================
            var picker = Panel("PickerPanel", safe);
            // Concept 1: title, a tray of 5 slots (your picks), framed search, counter, plain list, BACK | RANDOM, START.
            Label(picker, "Title", "PICK 5 SIGNS", 110, TextAlignmentOptions.Center, skin.textColor, skin.font, true, PinTop(30, 1000, 130));

            var trayRect = Rect("Tray", picker);
            Place(trayRect, PinTop(170, 960, 100));
            var trayLayout = trayRect.gameObject.AddComponent<HorizontalLayoutGroup>();
            trayLayout.spacing = 10; trayLayout.childControlWidth = true; trayLayout.childControlHeight = true;
            trayLayout.childForceExpandWidth = true; trayLayout.childForceExpandHeight = true;
            var traySlots = new Button[5];
            for (int i = 0; i < traySlots.Length; i++)
                traySlots[i] = Btn(trayRect, "TraySlot" + (i + 1), skin.rowChosen, skin.RowChosenColor, "empty", 36, Color.white, skin.font, null);

            var search = Field(picker, "SearchField", "Search signs...", skin, PinTop(290, 900, 110), false);

            // Filter chips (a row that scrolls sideways). Categories come from Assets/Resources/SignCategories.txt ("#chips:" line).
            var chipNames = new System.Collections.Generic.List<string> { "All" };
            var catFile = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Resources/SignCategories.txt");
            if (catFile != null)
                foreach (string raw in catFile.text.Split('\n'))
                    if (raw.StartsWith("#chips:"))
                        foreach (string c in raw.Substring(7).Split(',')) if (c.Trim().Length > 0) chipNames.Add(c.Trim());
            if (catFile == null) Debug.LogWarning("[MainMenuRebuilder] Assets/Resources/SignCategories.txt not found: the filter chips will only show 'All'. Add the file and run this again.");

            var chipScrollRect = Rect("FilterChips", picker);
            Place(chipScrollRect, PinTop(412, 960, 84));
            var chipScroll = chipScrollRect.gameObject.AddComponent<ScrollRect>();
            var chipHit = chipScrollRect.gameObject.AddComponent<Image>(); chipHit.color = new Color(0, 0, 0, 0);
            var chipViewport = Rect("Viewport", chipScrollRect);
            Stretch(chipViewport);
            chipViewport.gameObject.AddComponent<RectMask2D>();
            var chipContent = Rect("Content", chipViewport);
            chipContent.anchorMin = new Vector2(0, 0); chipContent.anchorMax = new Vector2(0, 1);
            chipContent.pivot = new Vector2(0, 0.5f);
            chipContent.offsetMin = Vector2.zero; chipContent.offsetMax = Vector2.zero;
            var chipLayout = chipContent.gameObject.AddComponent<HorizontalLayoutGroup>();
            chipLayout.spacing = 12; chipLayout.padding = new RectOffset(4, 4, 4, 4);
            chipLayout.childControlWidth = true; chipLayout.childControlHeight = true;
            chipLayout.childForceExpandWidth = false; chipLayout.childForceExpandHeight = true;
            chipContent.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            chipScroll.viewport = chipViewport; chipScroll.content = chipContent;
            chipScroll.horizontal = true; chipScroll.vertical = false;
            chipScroll.movementType = ScrollRect.MovementType.Elastic;

            var filterChips = new Button[chipNames.Count];
            for (int i = 0; i < chipNames.Count; i++)
            {
                filterChips[i] = Btn(chipContent, "Chip_" + chipNames[i], skin.row, skin.RowColor, chipNames[i], 38, skin.textColor, skin.font, null);
                var chipSize = filterChips[i].gameObject.AddComponent<LayoutElement>();
                chipSize.minWidth = chipSize.preferredWidth = Mathf.Max(130, 60 + 22 * chipNames[i].Length);
            }

            Scroll(picker, "WordList", out var pickerContent, 60, 350, 60, 515, 14);
            var template = Btn(pickerContent, "WordTemplate", Pick(skin.row), skin.RowColor, "Word", 52, skin.textColor, skin.font, null);
            var tLabel = template.GetComponentInChildren<TMP_Text>(true);
            tLabel.alignment = TextAlignmentOptions.Left;
            tLabel.rectTransform.offsetMin = new Vector2(44, 8);
            var tLayout = template.gameObject.AddComponent<LayoutElement>();
            tLayout.minHeight = 100; tLayout.preferredHeight = 100; tLayout.flexibleHeight = 0;
            template.gameObject.SetActive(false);

            var startButton = Btn(picker, "StartButton", Pick(skin.buttonGo), skin.Tint(skin.buttonGo, skin.goColor), "START", 64, Color.white, skin.font, PinBottom(40, 900, 140));
            var pickerBack = Btn(picker, "BackButton", skin.row, skin.RowColor, "BACK", 50, skin.textColor, skin.font, PinBottom(200, 438, 120));
            var randomButton = Btn(picker, "RandomButton", Pick(skin.buttonMain), skin.Tint(skin.buttonMain, skin.mainColor), "RANDOM", 50, Color.white, skin.font, PinBottom(200, 438, 120));
            Half(pickerBack, true);
            Half(randomButton, false);

            // ============================== MULTIPLAYER ==============================
            var multi = Panel("MultiplayerPanel", safe);
            Label(multi, "Title", "MULTIPLAYER", 120, TextAlignmentOptions.Center, skin.textColor, skin.font, true, PinTop(160, 1000, 150));

            var quickPlay = Btn(multi, "QuickPlayButton", Pick(skin.buttonGo), skin.Tint(skin.buttonGo, skin.goColor), "QUICK PLAY", 80, Color.white, skin.font, PinTop(420, 860, 190));
            Label(multi, "QuickHint", "Jump into a random public game", 36, TextAlignmentOptions.Center, skin.textColor, Body(skin), false, PinTop(625, 860, 56));
            var browse = Btn(multi, "BrowseButton", Pick(skin.buttonMain), skin.Tint(skin.buttonMain, skin.mainColor), "BROWSE PUBLIC GAMES", 52, Color.white, skin.font, PinTop(760, 860, 140));
            var create = Btn(multi, "CreateButton", Pick(skin.buttonMain), skin.Tint(skin.buttonMain, skin.mainColor), "CREATE GAME", 60, Color.white, skin.font, PinTop(930, 860, 140));
            var join = Btn(multi, "JoinButton", Pick(skin.buttonMain), skin.Tint(skin.buttonMain, skin.mainColor), "JOIN WITH CODE", 60, Color.white, skin.font, PinTop(1100, 860, 140));
            var multiBack = BackBtn(multi, skin, "BACK");

            // ============================== JOIN ==============================
            var joinPanel = Panel("JoinPanel", safe);
            Label(joinPanel, "Title", "JOIN A GAME", 120, TextAlignmentOptions.Center, skin.textColor, skin.font, true, PinTop(160, 1000, 150));
            Label(joinPanel, "Hint", "Type the 5-letter code from your friend", 42, TextAlignmentOptions.Center, skin.textColor, Body(skin), false, PinTop(400, 940, 60));
            var codeField = Field(joinPanel, "CodeField", "CODE", skin, PinTop(520, 700, 190), true);
            codeField.characterLimit = GameSession.RoomCodeLength;
            var joinError = Label(joinPanel, "ErrorText", "", 42, TextAlignmentOptions.Center, skin.wrongTextColor, Body(skin), false, PinTop(740, 940, 120));
            var joinConfirm = Btn(joinPanel, "JoinConfirmButton", Pick(skin.buttonGo), skin.Tint(skin.buttonGo, skin.goColor), "JOIN", 72, Color.white, skin.font, PinTop(900, 860, 170));
            var joinBack = BackBtn(joinPanel, skin, "BACK");

            // ============================== BROWSE ==============================
            var browsePanel = Panel("BrowsePanel", safe);
            Label(browsePanel, "Title", "PUBLIC GAMES", 110, TextAlignmentOptions.Center, skin.textColor, skin.font, true, PinTop(30, 1000, 130));
            var status = Label(browsePanel, "StatusText", "Looking for games...", 42, TextAlignmentOptions.Center, skin.textColor, Body(skin), false, PinTop(170, 900, 130));

            Scroll(browsePanel, "RoomList", out var browseContent, 60, 330, 60, 320, 16);
            var rowTemplate = Btn(browseContent, "RowTemplate", Pick(skin.row), skin.RowColor, "Room ABCDE      1/4", 50, skin.textColor, skin.font, null);
            var rLayout = rowTemplate.gameObject.AddComponent<LayoutElement>();
            rLayout.minHeight = 120; rLayout.preferredHeight = 120; rLayout.flexibleHeight = 0;
            rowTemplate.gameObject.SetActive(false);

            var refresh = Btn(browsePanel, "RefreshButton", Pick(skin.buttonMain), skin.Tint(skin.buttonMain, skin.mainColor), "REFRESH", 56, Color.white, skin.font, PinBottom(165, 860, 120));
            var browseBack = BackBtn(browsePanel, skin, "BACK");

            var browser = browsePanel.gameObject.AddComponent<PublicGameBrowser>();
            var bso = new SerializedObject(browser);
            bso.FindProperty("listContent").objectReferenceValue = browseContent;
            bso.FindProperty("rowTemplate").objectReferenceValue = rowTemplate;
            bso.FindProperty("statusText").objectReferenceValue = status;
            bso.FindProperty("refreshButton").objectReferenceValue = refresh;
            bso.ApplyModifiedPropertiesWithoutUndo();

            // ============================== CONTROLLER ==============================
            var controllerObj = new GameObject("MenuController");
            var menu = controllerObj.AddComponent<MenuController>();
            var so = new SerializedObject(menu);
            so.FindProperty("homePanel").objectReferenceValue = home.gameObject;
            so.FindProperty("pickerPanel").objectReferenceValue = picker.gameObject;
            so.FindProperty("multiplayerPanel").objectReferenceValue = multi.gameObject;
            so.FindProperty("joinPanel").objectReferenceValue = joinPanel.gameObject;
            so.FindProperty("browsePanel").objectReferenceValue = browsePanel.gameObject;

            so.FindProperty("nameButton").objectReferenceValue = nameButton;
            so.FindProperty("soloButton").objectReferenceValue = soloButton;
            so.FindProperty("multiplayerButton").objectReferenceValue = multiplayerButton;

            var chipsProp = so.FindProperty("filterChips");
            chipsProp.arraySize = filterChips.Length;
            for (int i = 0; i < filterChips.Length; i++) chipsProp.GetArrayElementAtIndex(i).objectReferenceValue = filterChips[i];
            so.FindProperty("searchField").objectReferenceValue = search;
            so.FindProperty("listContent").objectReferenceValue = pickerContent;
            so.FindProperty("wordButtonTemplate").objectReferenceValue = template;
            so.FindProperty("randomButton").objectReferenceValue = randomButton;
            so.FindProperty("startButton").objectReferenceValue = startButton;
            so.FindProperty("backButton").objectReferenceValue = pickerBack;
            var traySlotsProp = so.FindProperty("traySlots");
            traySlotsProp.arraySize = traySlots.Length;
            for (int i = 0; i < traySlots.Length; i++) traySlotsProp.GetArrayElementAtIndex(i).objectReferenceValue = traySlots[i];

            so.FindProperty("quickPlayButton").objectReferenceValue = quickPlay;
            so.FindProperty("browseButton").objectReferenceValue = browse;
            so.FindProperty("createButton").objectReferenceValue = create;
            so.FindProperty("joinButton").objectReferenceValue = join;
            so.FindProperty("multiplayerBackButton").objectReferenceValue = multiBack;

            so.FindProperty("joinCodeField").objectReferenceValue = codeField;
            so.FindProperty("joinConfirmButton").objectReferenceValue = joinConfirm;
            so.FindProperty("joinBackButton").objectReferenceValue = joinBack;
            so.FindProperty("joinErrorText").objectReferenceValue = joinError;

            so.FindProperty("browser").objectReferenceValue = browser;
            so.FindProperty("browseBackButton").objectReferenceValue = browseBack;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Start on Home.
            picker.gameObject.SetActive(false);
            multi.gameObject.SetActive(false);
            joinPanel.gameObject.SetActive(false);
            browsePanel.gameObject.SetActive(false);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings(ScenePath);
            Debug.Log("[MainMenuRebuilder] Built " + ScenePath + " (old one saved as " + BackupPath + "). Press Play to try it. " +
                      "Check 'Race Scene Name' on the MenuController object is your race scene (default: Game).");
        }

        // ---------------------------------------------------------------- helpers

        private struct Anchor { public bool top; public float y, w, h; }
        private static Anchor PinTop(float y, float w, float h) => new Anchor { top = true, y = y, w = w, h = h };
        private static Anchor PinBottom(float y, float w, float h) => new Anchor { top = false, y = y, w = w, h = h };

        private static TMP_FontAsset Body(UiSkin skin) => skin.bodyFont != null ? skin.bodyFont : skin.font;
        private static Sprite Pick(Sprite s) => s; // null = the plain built-in shape

        private static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static void Stretch(RectTransform r)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
        }

        private const float SideMargin = 60f;

        /// <summary>Wide items (850+ px) stretch with the same 60 px side margin as the lists, so every box lines up on any screen width.</summary>
        private static void Place(RectTransform r, Anchor a)
        {
            float ay = a.top ? 1f : 0f;
            r.pivot = new Vector2(0.5f, ay);
            if (a.w >= 850f)
            {
                r.anchorMin = new Vector2(0f, ay); r.anchorMax = new Vector2(1f, ay);
                r.offsetMin = new Vector2(SideMargin, 0); r.offsetMax = new Vector2(-SideMargin, 0);
                r.sizeDelta = new Vector2(-2f * SideMargin, a.h);
            }
            else
            {
                r.anchorMin = r.anchorMax = new Vector2(0.5f, ay);
                r.sizeDelta = new Vector2(a.w, a.h);
            }
            r.anchoredPosition = new Vector2(0, a.top ? -a.y : a.y);
        }

        /// <summary>Puts a bottom-pinned button in the left or right half of the row, with the same side margin and a small gap.</summary>
        private static void Half(Button b, bool left)
        {
            var r = (RectTransform)b.transform;
            float x0 = left ? 0f : 0.5f, x1 = left ? 0.5f : 1f;
            r.anchorMin = new Vector2(x0, 0f); r.anchorMax = new Vector2(x1, 0f);
            r.pivot = new Vector2(0.5f, 0f);
            r.offsetMin = new Vector2(left ? SideMargin : 8f, 0f);
            r.offsetMax = new Vector2(left ? -8f : -SideMargin, 0f);
            r.sizeDelta = new Vector2(r.sizeDelta.x, 120f);
            r.anchoredPosition = new Vector2(r.anchoredPosition.x, 200f);
        }

        private static RectTransform Panel(string name, RectTransform parent)
        {
            var r = Rect(name, parent);
            Stretch(r);
            return r;
        }

        private static TextMeshProUGUI Label(Transform parent, string name, string text, float size, TextAlignmentOptions align,
                                             Color color, TMP_FontAsset font, bool bold, Anchor a)
        {
            var r = Rect(name, parent);
            Place(r, a);
            var t = r.gameObject.AddComponent<TextMeshProUGUI>();
            t.text = text; t.fontSize = size; t.alignment = align; t.color = color;
            t.enableAutoSizing = true; t.fontSizeMax = size; t.fontSizeMin = Mathf.Max(18, size * 0.45f);
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Truncate;
            t.raycastTarget = false;
            if (font != null) t.font = font;
            if (bold) t.fontStyle = FontStyles.Bold;
            return t;
        }

        /// <summary>A 9-sliced button with a centred label. Pass null for the anchor when a layout group will place it.</summary>
        private static Button Btn(Transform parent, string name, Sprite sprite, Color color, string label, float size,
                                  Color textColor, TMP_FontAsset font, Anchor? a)
        {
            var r = Rect(name, parent);
            if (a.HasValue) Place(r, a.Value);
            var img = r.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            img.color = color;
            var b = r.gameObject.AddComponent<Button>();
            b.targetGraphic = img;

            var t = Rect("Label", r);
            Stretch(t);
            t.offsetMin = new Vector2(24, 8); t.offsetMax = new Vector2(-24, -8);
            var text = t.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = label; text.fontSize = size; text.color = textColor;
            text.alignment = TextAlignmentOptions.Center;
            text.enableAutoSizing = true; text.fontSizeMax = size; text.fontSizeMin = Mathf.Max(18, size * 0.4f);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Truncate;
            text.raycastTarget = false;
            if (font != null) text.font = font;
            return b;
        }

        private static Button BackBtn(RectTransform panel, UiSkin skin, string label)
        {
            var b = Btn(panel, "BackButton", skin.row, skin.RowColor, label, 50, skin.textColor, skin.font, PinBottom(30, 860, 110));
            return b;
        }

        private static TMP_InputField Field(RectTransform parent, string name, string placeholder, UiSkin skin, Anchor a, bool big)
        {
            var r = Rect(name, parent);
            Place(r, a);
            var img = r.gameObject.AddComponent<Image>();
            img.sprite = skin.frame;
            img.type = skin.frame != null ? Image.Type.Sliced : Image.Type.Simple;
            img.color = skin.frame != null ? Color.white : new Color(1f, 1f, 1f, 0.55f);
            var field = r.gameObject.AddComponent<TMP_InputField>();

            var area = Rect("Text Area", r);
            Stretch(area);
            area.offsetMin = new Vector2(30, 10); area.offsetMax = new Vector2(-30, -10);
            area.gameObject.AddComponent<RectMask2D>();

            float size = big ? 120 : 46;
            var font = big ? skin.font : Body(skin);

            var ph = Rect("Placeholder", area);
            Stretch(ph);
            var phText = ph.gameObject.AddComponent<TextMeshProUGUI>();
            phText.text = placeholder; phText.fontSize = size; phText.fontStyle = FontStyles.Italic;
            phText.color = new Color(skin.textColor.r, skin.textColor.g, skin.textColor.b, 0.4f);
            phText.alignment = big ? TextAlignmentOptions.Center : TextAlignmentOptions.Left;
            phText.raycastTarget = false;
            if (font != null) phText.font = font;

            var tx = Rect("Text", area);
            Stretch(tx);
            var text = tx.gameObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = size; text.color = skin.textColor;
            text.alignment = big ? TextAlignmentOptions.Center : TextAlignmentOptions.Left;
            text.raycastTarget = false;
            if (font != null) text.font = font;

            field.textViewport = area;
            field.textComponent = text;
            field.placeholder = phText;
            field.caretColor = skin.textColor;
            field.selectionColor = new Color(skin.mainColor.r, skin.mainColor.g, skin.mainColor.b, 0.4f);
            field.customCaretColor = true;
            return field;
        }

        /// <summary>A vertical scroll list. Margins are from the panel's left / bottom / right / top edges.</summary>
        private static RectTransform Scroll(RectTransform parent, string name, out RectTransform content,
                                            float left, float bottom, float right, float top, float spacing)
        {
            var root = Rect(name, parent);
            root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one;
            root.offsetMin = new Vector2(left, bottom); root.offsetMax = new Vector2(-right, -top);
            var hit = root.gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0); // lets the whole area be dragged
            var scroll = root.gameObject.AddComponent<ScrollRect>();

            var viewport = Rect("Viewport", root);
            Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();

            content = Rect("Content", viewport);
            content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.offsetMin = content.offsetMax = Vector2.zero;
            var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = spacing;
            vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            vlg.padding = new RectOffset(0, 0, 4, 20);
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewport; scroll.content = content;
            scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 40;
            return root;
        }

        private static void AddToBuildSettings(string path)
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var s in scenes)
            {
                if (s.path == path) { s.enabled = true; EditorBuildSettings.scenes = scenes.ToArray(); return; }
            }
            scenes.Insert(0, new EditorBuildSettingsScene(path, true)); // the menu is the first scene
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
#endif
