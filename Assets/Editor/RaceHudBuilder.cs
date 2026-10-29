using System.IO;
using SignRacers.Race;
using SignRacers.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace SignRacers.EditorTools
{
    /// <summary>
    /// One click: builds the in-race HUD as a prefab (Assets/Prefabs/UI/RaceHudCanvas.prefab) out of
    /// ordinary Unity UI objects, wires it to RaceHudView and MinimapHud, and puts a copy in the
    /// open scene if there isn't one. Run it from the menu: Sign Racers > Build Race HUD Prefab.
    /// Afterwards everything is a normal object you can open the prefab and move or restyle.
    /// NOTE: running it again REPLACES the prefab, so hand edits to it would be lost.
    /// </summary>
    public static class RaceHudBuilder
    {
        private const string PrefabPath = "Assets/Prefabs/UI/RaceHudCanvas.prefab";
        private const string SpriteFolder = "Assets/Art/UI/Generated";

        private static readonly Color PanelColor = new Color(0f, 0f, 0f, 0.45f);

        [MenuItem("Sign Racers/Build Race HUD Prefab")]
        public static void Build()
        {
            if (!EditorUtility.DisplayDialog("Build Race HUD",
                    "This creates " + PrefabPath + " (replacing it if it exists, so any hand edits to it are lost). Continue?",
                    "Build", "Cancel"))
                return;

            EnsureFolder("Assets/Prefabs/UI");
            EnsureFolder(SpriteFolder);

            Sprite circle = EnsureSprite("Circle", MakeCircle(256), Vector4.zero);
            Sprite panel = EnsureSprite("RoundedPanel", MakeRoundedRect(64, 20), new Vector4(24, 24, 24, 24));

            // ---------- root canvas ----------
            var root = new GameObject("RaceHudCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
                                      typeof(GraphicRaycaster), typeof(CanvasGroup));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            var group = root.GetComponent<CanvasGroup>();
            group.alpha = 0f; // RaceHudView fades it in when a race starts

            // Everything lives inside the safe area.
            var safe = MakeRect("SafeArea", root.transform);
            Stretch(safe);
            safe.gameObject.AddComponent<SafeArea>();

            // ---------- top left: map + lap ----------
            var mapBox = MakeRect("MapBox", safe);
            Anchor(mapBox, new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -24), new Vector2(420, 440));
            var mapBoxImage = mapBox.gameObject.AddComponent<Image>();
            mapBoxImage.sprite = panel; mapBoxImage.type = Image.Type.Sliced; mapBoxImage.color = PanelColor;
            mapBoxImage.raycastTarget = false;

            var mapArea = MakeRect("MapArea", mapBox);
            mapArea.anchorMin = Vector2.zero; mapArea.anchorMax = Vector2.one;
            mapArea.offsetMin = new Vector2(20, 100);   // leave room for the lap text at the bottom
            mapArea.offsetMax = new Vector2(-20, -20);

            var mapRect = MakeRect("MapImage", mapArea);
            Stretch(mapRect);
            var mapImage = mapRect.gameObject.AddComponent<RawImage>();
            mapImage.raycastTarget = false;
            mapImage.color = new Color(1, 1, 1, 0); // invisible until MinimapHud draws the track into it
            mapRect.gameObject.AddComponent<AspectRatioFitter>().aspectMode = AspectRatioFitter.AspectMode.FitInParent;

            var lapText = MakeText("LapText", mapBox, "LAP 1/3", 54, TextAlignmentOptions.Center, Color.white);
            lapText.rectTransform.anchorMin = new Vector2(0, 0);
            lapText.rectTransform.anchorMax = new Vector2(1, 0);
            lapText.rectTransform.pivot = new Vector2(0.5f, 0);
            lapText.rectTransform.anchoredPosition = new Vector2(0, 16);
            lapText.rectTransform.sizeDelta = new Vector2(0, 76);

            // ---------- top right: position ----------
            var posBox = MakeRect("PositionBox", safe);
            Anchor(posBox, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-24, -24), new Vector2(250, 170));
            var posImage = posBox.gameObject.AddComponent<Image>();
            posImage.sprite = panel; posImage.type = Image.Type.Sliced; posImage.color = PanelColor;
            posImage.raycastTarget = false;

            var positionText = MakeText("PositionText", posBox, "1<size=55%>st</size>", 120, TextAlignmentOptions.Center, Color.white);
            Stretch(positionText.rectTransform);
            positionText.fontStyle = FontStyles.Bold | FontStyles.Italic;

            // ---------- bottom right: buttons (two items on top, BOOST between and below) ----------
            const float size = 240f;
            var item1 = MakeButton("Item1Button", safe, circle, new Vector2(-430, 380), size, "EMPTY", 40);
            var item2 = MakeButton("Item2Button", safe, circle, new Vector2(-160, 380), size, "EMPTY", 40);
            var boost = MakeButton("BoostButton", safe, circle, new Vector2(-295, 140), size, "BOOST", 46);

            // ---------- scripts ----------
            var view = root.AddComponent<RaceHudView>();
            var viewObject = new SerializedObject(view);
            viewObject.FindProperty("group").objectReferenceValue = group;
            viewObject.FindProperty("lapText").objectReferenceValue = lapText;
            viewObject.FindProperty("positionText").objectReferenceValue = positionText;
            WireButton(viewObject.FindProperty("boost"), boost);
            var itemsProperty = viewObject.FindProperty("items");
            itemsProperty.arraySize = 2;
            WireButton(itemsProperty.GetArrayElementAtIndex(0), item1);
            WireButton(itemsProperty.GetArrayElementAtIndex(1), item2);
            viewObject.ApplyModifiedPropertiesWithoutUndo();

            var minimap = root.AddComponent<MinimapHud>();
            var minimapObject = new SerializedObject(minimap);
            minimapObject.FindProperty("mapImage").objectReferenceValue = mapImage;
            minimapObject.ApplyModifiedPropertiesWithoutUndo();

            AddSignView(root);
            AddHudExtras(root);

            // ---------- save ----------
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            string where = "";
            if (Object.FindFirstObjectByType<RaceHudView>() == null && prefab != null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                Undo.RegisterCreatedObjectUndo(instance, "Add Race HUD");
                Selection.activeGameObject = instance;
                where = "\n\nA copy was added to the open scene. Save the scene (Cmd+S).";
            }
            else if (prefab != null)
            {
                where = "\n\nThe open scene already has a Race HUD instance. Delete it and drag the new prefab in to pick up the changes.";
            }

            EditorGUIUtility.PingObject(prefab);
            EditorUtility.DisplayDialog("Race HUD built", "Saved " + PrefabPath + "." + where, "OK");
        }

        [MenuItem("Sign Racers/Add Sign View To Race HUD")]
        public static void AddSignViewToPrefab()
        {
            if (!File.Exists(PrefabPath))
            {
                EditorUtility.DisplayDialog("Race HUD missing", "Build the Race HUD prefab first (Sign Racers > Build Race HUD Prefab).", "OK");
                return;
            }

            // Edits the saved prefab in place, so anything you changed by hand is kept.
            var contents = PrefabUtility.LoadPrefabContents(PrefabPath);
            bool added;
            try
            {
                added = AddSignView(contents);
                if (added) PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }

            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog(added ? "Sign view added" : "Nothing to do",
                added ? "Added the camera box, word and timer to " + PrefabPath + ". The copy in your scene picks the changes up automatically. Save the scene."
                      : "The prefab already has a sign view (or its MapBox is missing).", "OK");
        }

        /// <summary>Adds the camera box, word panel and timer to a Race HUD root. Returns false if it's already there.</summary>
        private static bool AddSignView(GameObject root)
        {
            var safe = root.transform.Find("SafeArea") as RectTransform;
            var mapBox = safe != null ? safe.Find("MapBox") as RectTransform : null;
            if (mapBox == null || safe.Find("SignPanel") != null) return false;

            Sprite panel = GetPanelSprite();

            // The map and lap text move into one container so they can fade together.
            var mapContent = mapBox.Find("MapContent") as RectTransform;
            if (mapContent == null)
            {
                mapContent = MakeRect("MapContent", mapBox);
                Stretch(mapContent);
                mapContent.SetAsFirstSibling();
                var mapArea = mapBox.Find("MapArea");
                var lap = mapBox.Find("LapText");
                if (mapArea != null) mapArea.SetParent(mapContent, false);
                if (lap != null) lap.SetParent(mapContent, false);
            }
            var mapGroup = mapContent.gameObject.GetComponent<CanvasGroup>() ?? mapContent.gameObject.AddComponent<CanvasGroup>();

            // Camera area inside the same box, hidden until you sign.
            var cameraRect = MakeRect("CameraArea", mapBox);
            cameraRect.anchorMin = Vector2.zero; cameraRect.anchorMax = Vector2.one;
            cameraRect.offsetMin = new Vector2(20, 20); cameraRect.offsetMax = new Vector2(-20, -20);
            var cameraGroup = cameraRect.gameObject.AddComponent<CanvasGroup>();
            cameraGroup.alpha = 0f;
            cameraGroup.blocksRaycasts = false;
            cameraRect.gameObject.AddComponent<RectMask2D>();

            var cameraImageRect = MakeRect("CameraImage", cameraRect);
            Stretch(cameraImageRect);
            var cameraImage = cameraImageRect.gameObject.AddComponent<RawImage>();
            cameraImage.raycastTarget = false;

            var flashRect = MakeRect("CameraFlash", cameraRect);
            Stretch(flashRect);
            var flash = flashRect.gameObject.AddComponent<Image>();
            flash.color = new Color(1, 1, 1, 0);
            flash.raycastTarget = false;

            // Word panel in the middle of the screen.
            var signPanel = MakeRect("SignPanel", safe);
            signPanel.anchorMin = signPanel.anchorMax = new Vector2(0.5f, 0.60f);
            signPanel.pivot = new Vector2(0.5f, 0.5f);
            signPanel.anchoredPosition = Vector2.zero;
            signPanel.sizeDelta = new Vector2(940, 380);
            var panelGroup = signPanel.gameObject.AddComponent<CanvasGroup>();
            panelGroup.alpha = 0f;
            panelGroup.blocksRaycasts = false;
            panelGroup.interactable = false;

            var backdrop = MakeRect("Backdrop", signPanel);
            Stretch(backdrop);
            var backdropImage = backdrop.gameObject.AddComponent<Image>();
            backdropImage.sprite = panel; backdropImage.type = Image.Type.Sliced;
            backdropImage.color = new Color(0f, 0f, 0f, 0.35f);
            backdropImage.raycastTarget = false;

            var wordRoot = MakeRect("WordRoot", signPanel);
            wordRoot.anchorMin = wordRoot.anchorMax = new Vector2(0.5f, 0.5f);
            wordRoot.pivot = new Vector2(0.5f, 0.5f);
            wordRoot.anchoredPosition = new Vector2(0, 40);
            wordRoot.sizeDelta = new Vector2(900, 220);

            var ghost = MakeText("Ghost", wordRoot, "WORD", 190, TextAlignmentOptions.Center, new Color(1, 1, 1, 0.35f));
            Stretch(ghost.rectTransform);
            ghost.fontStyle = FontStyles.Bold;

            var maskRect = MakeRect("FillMask", wordRoot);
            Stretch(maskRect);
            var mask = maskRect.gameObject.AddComponent<RectMask2D>();
            var fill = MakeText("Fill", maskRect, "WORD", 190, TextAlignmentOptions.Center, new Color(0.35f, 0.95f, 0.45f));
            Stretch(fill.rectTransform);
            fill.fontStyle = FontStyles.Bold;

            var timerBack = MakeRect("TimerBack", signPanel);
            timerBack.anchorMin = timerBack.anchorMax = new Vector2(0.5f, 0f);
            timerBack.pivot = new Vector2(0.5f, 0.5f);
            timerBack.anchoredPosition = new Vector2(0, 90);
            timerBack.sizeDelta = new Vector2(640, 30);
            var timerBackImage = timerBack.gameObject.AddComponent<Image>();
            timerBackImage.sprite = panel; timerBackImage.type = Image.Type.Sliced;
            timerBackImage.color = new Color(0, 0, 0, 0.5f);
            timerBackImage.raycastTarget = false;

            var timerFill = MakeRect("TimerFill", timerBack);
            timerFill.anchorMin = Vector2.zero; timerFill.anchorMax = Vector2.one;
            timerFill.offsetMin = timerFill.offsetMax = Vector2.zero;
            var timerFillImage = timerFill.gameObject.AddComponent<Image>();
            timerFillImage.sprite = panel; timerFillImage.type = Image.Type.Sliced;
            timerFillImage.raycastTarget = false;

            var hint = MakeText("Hint", signPanel, "tap the glowing button again to check early", 34, TextAlignmentOptions.Center, new Color(1, 1, 1, 0.7f));
            hint.rectTransform.anchorMin = new Vector2(0, 0);
            hint.rectTransform.anchorMax = new Vector2(1, 0);
            hint.rectTransform.pivot = new Vector2(0.5f, 0);
            hint.rectTransform.anchoredPosition = new Vector2(0, 16);
            hint.rectTransform.sizeDelta = new Vector2(0, 46);

            // Script on the root, wired up.
            var view = root.GetComponent<SignViewHud>() ?? root.AddComponent<SignViewHud>();
            var so = new SerializedObject(view);
            so.FindProperty("box").objectReferenceValue = mapBox;
            so.FindProperty("mapContent").objectReferenceValue = mapGroup;
            so.FindProperty("cameraArea").objectReferenceValue = cameraGroup;
            so.FindProperty("cameraFlash").objectReferenceValue = flash;
            so.FindProperty("signPanel").objectReferenceValue = panelGroup;
            so.FindProperty("wordRoot").objectReferenceValue = wordRoot;
            so.FindProperty("ghostText").objectReferenceValue = ghost;
            so.FindProperty("fillText").objectReferenceValue = fill;
            so.FindProperty("fillMask").objectReferenceValue = mask;
            so.FindProperty("timerFill").objectReferenceValue = timerFill;
            so.FindProperty("timerFillImage").objectReferenceValue = timerFillImage;
            so.FindProperty("hint").objectReferenceValue = hint.gameObject;
            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        internal static Sprite GetPanelSprite()
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteFolder + "/RoundedPanel.png");
            if (sprite != null) return sprite;
            EnsureFolder(SpriteFolder);
            return EnsureSprite("RoundedPanel", MakeRoundedRect(64, 20), new Vector4(24, 24, 24, 24));
        }

        private const string ResultsPrefabPath = "Assets/Prefabs/UI/RaceResultsCanvas.prefab";

        [MenuItem("Sign Racers/Add Countdown, Effects And Results To HUD")]
        public static void AddStepThree()
        {
            if (!File.Exists(PrefabPath))
            {
                EditorUtility.DisplayDialog("Race HUD missing", "Build the Race HUD prefab first (Sign Racers > Build Race HUD Prefab).", "OK");
                return;
            }

            EnsureFolder("Assets/Prefabs/UI");
            string report = "";

            // 1) Countdown message and effect chips go into the existing Race HUD prefab, in place.
            var contents = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                if (AddHudExtras(contents))
                {
                    PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
                    report += "Added the countdown message and effect chips to the Race HUD prefab.\n";
                }
                else report += "The Race HUD prefab already has the countdown message and effect chips.\n";
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }

            // 2) The results screen is its own prefab, built once.
            if (!File.Exists(ResultsPrefabPath))
            {
                BuildResultsPrefab();
                report += "Built " + ResultsPrefabPath + ".\n";
            }
            else report += "The results prefab already exists (delete it first to rebuild it).\n";

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // 3) Put a copy in the open scene if there isn't one.
            if (Object.FindFirstObjectByType<ResultsHud>() == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ResultsPrefabPath);
                if (prefab != null)
                {
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    Undo.RegisterCreatedObjectUndo(instance, "Add Race Results");
                    report += "Added a results screen to the open scene.\n";
                }
            }

            EditorUtility.DisplayDialog("Done", report + "\nSave the scene (Cmd+S).", "OK");
        }

        /// <summary>Adds the center message and the effect chips to a Race HUD root. Returns false if already there.</summary>
        private static bool AddHudExtras(GameObject root)
        {
            var safe = root.transform.Find("SafeArea") as RectTransform;
            if (safe == null || safe.Find("CenterMessage") != null) return false;
            Sprite panel = GetPanelSprite();

            // Big message in the middle: waiting text, 3-2-1, GO!
            var message = MakeText("CenterMessage", safe, "", 340, TextAlignmentOptions.Center, Color.white);
            message.rectTransform.anchorMin = message.rectTransform.anchorMax = new Vector2(0.5f, 0.55f);
            message.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            message.rectTransform.anchoredPosition = Vector2.zero;
            message.rectTransform.sizeDelta = new Vector2(1000, 500);
            message.fontStyle = FontStyles.Bold;

            // Effect chips stacked above the button cluster, right aligned.
            var stack = MakeRect("EffectsStack", safe);
            stack.anchorMin = stack.anchorMax = new Vector2(1, 0);
            stack.pivot = new Vector2(1, 0);
            stack.anchoredPosition = new Vector2(-24, 540);
            stack.sizeDelta = new Vector2(320, 330);
            var layout = stack.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.LowerRight;
            layout.spacing = 10;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var good = new Color(0.25f, 0.70f, 0.40f, 0.92f);
            var bad = new Color(0.90f, 0.45f, 0.20f, 0.92f);
            var boostChip = MakeChip("BoostChip", stack, panel, "BOOST", good);
            var superChip = MakeChip("SuperBoostChip", stack, panel, "SUPER BOOST", good);
            var penaltyChip = MakeChip("PenaltyChip", stack, panel, "PENALTY", bad);
            var slowedChip = MakeChip("SlowedChip", stack, panel, "SLOWED", bad);
            boostChip.SetActive(false); superChip.SetActive(false); penaltyChip.SetActive(false); slowedChip.SetActive(false);

            var messages = root.GetComponent<RaceMessagesHud>() ?? root.AddComponent<RaceMessagesHud>();
            var messagesObject = new SerializedObject(messages);
            messagesObject.FindProperty("message").objectReferenceValue = message;
            messagesObject.ApplyModifiedPropertiesWithoutUndo();

            var effects = root.GetComponent<EffectsHud>() ?? root.AddComponent<EffectsHud>();
            var effectsObject = new SerializedObject(effects);
            effectsObject.FindProperty("boostChip").objectReferenceValue = boostChip;
            effectsObject.FindProperty("superBoostChip").objectReferenceValue = superChip;
            effectsObject.FindProperty("penaltyChip").objectReferenceValue = penaltyChip;
            effectsObject.FindProperty("slowedChip").objectReferenceValue = slowedChip;
            effectsObject.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        private static GameObject MakeChip(string name, RectTransform parent, Sprite panel, string label, Color color)
        {
            var rect = MakeRect(name, parent);
            rect.sizeDelta = new Vector2(300, 60);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = panel; image.type = Image.Type.Sliced; image.color = color;
            image.raycastTarget = false;
            var text = MakeText("Label", rect, label, 36, TextAlignmentOptions.Center, Color.white);
            Stretch(text.rectTransform);
            text.fontStyle = FontStyles.Bold;
            return rect.gameObject;
        }

        /// <summary>The end-of-race screen: a separate canvas so it can show after the race HUD hides.</summary>
        private static void BuildResultsPrefab()
        {
            Sprite panelSprite = GetPanelSprite();

            var root = new GameObject("RaceResultsCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
                                      typeof(GraphicRaycaster), typeof(CanvasGroup));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            var group = root.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            var safe = MakeRect("SafeArea", root.transform);
            Stretch(safe);
            safe.gameObject.AddComponent<SafeArea>();

            var panel = MakeRect("Panel", safe);
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.anchoredPosition = Vector2.zero;
            panel.sizeDelta = new Vector2(940, 1180);
            var panelImage = panel.gameObject.AddComponent<Image>();
            panelImage.sprite = panelSprite; panelImage.type = Image.Type.Sliced;
            panelImage.color = new Color(0.06f, 0.08f, 0.14f, 0.94f);

            var title = MakeText("Title", panel, "FINISHED!", 110, TextAlignmentOptions.Center, Color.white);
            PinTop(title.rectTransform, -50, 900, 150);
            title.fontStyle = FontStyles.Bold;

            var placeText = MakeText("PlaceText", panel, "You came 1st", 58, TextAlignmentOptions.Center, new Color(1f, 0.84f, 0.25f));
            PinTop(placeText.rectTransform, -210, 900, 90);

            var rows = new GameObject[4];
            var names = new TMP_Text[4];
            var times = new TMP_Text[4];
            for (int i = 0; i < 4; i++)
            {
                var row = MakeRect("Row" + (i + 1), panel);
                PinTop(row, -340 - i * 100, 820, 90);
                rows[i] = row.gameObject;

                names[i] = MakeText("Name", row, "1.  You", 54, TextAlignmentOptions.Left, Color.white);
                Stretch(names[i].rectTransform);
                times[i] = MakeText("Time", row, "0:00.00", 54, TextAlignmentOptions.Right, Color.white);
                Stretch(times[i].rectTransform);
            }

            var status = MakeText("StatusText", panel, "", 38, TextAlignmentOptions.Center, new Color(1, 1, 1, 0.7f));
            status.rectTransform.anchorMin = status.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            status.rectTransform.pivot = new Vector2(0.5f, 0f);
            status.rectTransform.anchoredPosition = new Vector2(0, 220);
            status.rectTransform.sizeDelta = new Vector2(860, 60);

            var again = MakeRoundedButton("AgainButton", panel, panelSprite, new Color(0.25f, 0.55f, 0.95f), "RACE AGAIN", new Vector2(-215, 60));
            var menu = MakeRoundedButton("MenuButton", panel, panelSprite, new Color(0.30f, 0.34f, 0.46f), "MENU", new Vector2(215, 60));

            var view = root.AddComponent<ResultsHud>();
            var so = new SerializedObject(view);
            so.FindProperty("group").objectReferenceValue = group;
            so.FindProperty("title").objectReferenceValue = title;
            so.FindProperty("placeText").objectReferenceValue = placeText;
            so.FindProperty("statusText").objectReferenceValue = status;
            so.FindProperty("againButton").objectReferenceValue = again.GetComponent<Button>();
            so.FindProperty("menuButton").objectReferenceValue = menu.GetComponent<Button>();
            for (int i = 0; i < 4; i++)
            {
                so.FindProperty("rows").GetArrayElementAtIndex(i).objectReferenceValue = rows[i];
                so.FindProperty("nameTexts").GetArrayElementAtIndex(i).objectReferenceValue = names[i];
                so.FindProperty("timeTexts").GetArrayElementAtIndex(i).objectReferenceValue = times[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, ResultsPrefabPath);
            Object.DestroyImmediate(root);
        }

        internal static void PinTop(RectTransform rect, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0, y);
            rect.sizeDelta = new Vector2(width, height);
        }

        internal static GameObject MakeRoundedButton(string name, RectTransform parent, Sprite sprite, Color color, string label, Vector2 position)
        {
            var rect = MakeRect(name, parent);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(400, 120);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite; image.type = Image.Type.Sliced; image.color = color;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var text = MakeText("Label", rect, label, 50, TextAlignmentOptions.Center, Color.white);
            Stretch(text.rectTransform);
            text.fontStyle = FontStyles.Bold;
            return rect.gameObject;
        }

        // ---------- wiring ----------

        private static void WireButton(SerializedProperty parts, GameObject button)
        {
            parts.FindPropertyRelative("button").objectReferenceValue = button.GetComponent<Button>();
            parts.FindPropertyRelative("face").objectReferenceValue = button.GetComponent<Image>();
            parts.FindPropertyRelative("cooldownRing").objectReferenceValue = button.transform.Find("Ring").GetComponent<Image>();
            parts.FindPropertyRelative("label").objectReferenceValue = button.transform.Find("Label").GetComponent<TMP_Text>();
        }

        // ---------- UI helpers ----------

        private static GameObject MakeButton(string name, RectTransform parent, Sprite circle, Vector2 position, float size, string label, float fontSize)
        {
            var rect = MakeRect(name, parent);
            Anchor(rect, new Vector2(1, 0), new Vector2(1, 0), position, new Vector2(size, size));
            rect.pivot = new Vector2(0.5f, 0.5f);
            // Anchored to the bottom right with a centered pivot, so the position is the circle's center.
            rect.anchoredPosition = position;

            var face = rect.gameObject.AddComponent<Image>();
            face.sprite = circle;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = face;

            // Cooldown ring: a dark pie that empties as the cooldown runs out.
            var ringRect = MakeRect("Ring", rect);
            Stretch(ringRect);
            var ring = ringRect.gameObject.AddComponent<Image>();
            ring.sprite = circle;
            ring.color = new Color(0f, 0f, 0f, 0.55f);
            ring.type = Image.Type.Filled;
            ring.fillMethod = Image.FillMethod.Radial360;
            ring.fillOrigin = (int)Image.Origin360.Top;
            ring.fillClockwise = true;
            ring.fillAmount = 0f;
            ring.raycastTarget = false;

            var text = MakeText("Label", rect, label, fontSize, TextAlignmentOptions.Center, Color.white);
            Stretch(text.rectTransform);
            text.fontStyle = FontStyles.Bold;
            return rect.gameObject;
        }

        internal static RectTransform MakeRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        internal static TextMeshProUGUI MakeText(string name, Transform parent, string content, float fontSize, TextAlignmentOptions alignment, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.GetComponent<RectTransform>().SetParent(parent, false);
            var text = go.GetComponent<TextMeshProUGUI>();
            text.text = content;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            return text;
        }

        internal static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>Anchors a rect to a corner/point; pivot matches the anchor so the position is measured from that corner.</summary>
        private static void Anchor(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = anchorMin;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        // ---------- generated sprites ----------

        internal static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static Sprite EnsureSprite(string name, Texture2D texture, Vector4 border)
        {
            string assetPath = SpriteFolder + "/" + name + ".png";
            File.WriteAllBytes(assetPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.spriteBorder = border;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        }

        private static Texture2D MakeCircle(int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float r = size * 0.5f;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(r - d) * 255f));
                }
            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        private static Texture2D MakeRoundedRect(int size, float radius)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    // Distance outside the rounded shape (negative inside), measured from the inner box.
                    float dx = Mathf.Max(Mathf.Abs(x + 0.5f - size * 0.5f) - (size * 0.5f - radius), 0f);
                    float dy = Mathf.Max(Mathf.Abs(y + 0.5f - size * 0.5f) - (size * 0.5f - radius), 0f);
                    float d = Mathf.Sqrt(dx * dx + dy * dy) - radius;
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(0.5f - d) * 255f));
                }
            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }
    }
}
