using SignRacers.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace SignRacers.EditorTools
{
    /// <summary>
    /// One click: builds the MainMenu scene out of ordinary Unity UI objects (Canvas, panels,
    /// buttons, a scroll list, input fields), wires them to MenuController, saves the scene
    /// and adds it to the build. Run it from the menu: Sign Racers > Build Main Menu Scene.
    /// Afterwards everything is a normal object you can select, move and restyle.
    /// NOTE: running it again REPLACES the scene, so hand edits would be lost.
    /// </summary>
    public static class MainMenuBuilder
    {
        private static readonly Color Background = new Color(0.08f, 0.10f, 0.18f);
        private static readonly Color PrimaryButton = new Color(0.25f, 0.55f, 0.95f);
        private static readonly Color SecondaryButton = new Color(0.30f, 0.34f, 0.46f);
        private static readonly Color WordNormal = new Color(0.20f, 0.24f, 0.36f);
        private static readonly Color WordSelected = new Color(0.25f, 0.70f, 0.40f);
        private static readonly Color FieldColor = new Color(1f, 1f, 1f, 0.12f);
        private static readonly Color ErrorColor = new Color(1f, 0.45f, 0.40f);

        private const string ScenePath = "Assets/Scenes/MainMenu.unity";

        [MenuItem("Sign Racers/Build Main Menu Scene")]
        public static void Build()
        {
            if (!EditorUtility.DisplayDialog("Build Main Menu",
                    "This creates a new scene at " + ScenePath + " (replacing it if it exists, so any hand edits to it are lost). Continue?",
                    "Build", "Cancel"))
                return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Camera (solid color background).
            var camObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camObject.tag = "MainCamera";
            var camera = camObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Background;

            // Event system, so buttons receive touches and clicks.
            var eventSystem = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            var module = eventSystem.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
#else
            eventSystem.AddComponent<StandaloneInputModule>();
#endif

            // Canvas that scales to any phone, designed for portrait.
            var canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            // Everything lives inside the safe area.
            var safeArea = MakeRect("SafeArea", canvasObject.transform);
            Stretch(safeArea);
            safeArea.gameObject.AddComponent<SafeArea>();

            // ---------- Home panel ----------
            var home = MakeRect("HomePanel", safeArea);
            Stretch(home);
            var homeLayout = AddVerticalLayout(home.gameObject, 50, 60);
            homeLayout.childAlignment = TextAnchor.MiddleCenter;

            var title = MakeText(home, "Title", "Sign Racers", 130, TextAlignmentOptions.Center, Color.white);
            SetHeight(title.gameObject, 360);
            var soloButton = MakeButton(home, "SoloButton", "Solo", 200, PrimaryButton, 70);
            var multiplayerButton = MakeButton(home, "MultiplayerButton", "Multiplayer", 200, PrimaryButton, 70);

            // ---------- Word picker panel ----------
            var picker = MakeRect("PickerPanel", safeArea);
            Stretch(picker);
            AddVerticalLayout(picker.gameObject, 28, 50);

            var header = MakeText(picker, "Header", "Pick your 5 signs", 80, TextAlignmentOptions.Center, Color.white);
            SetHeight(header.gameObject, 130);
            var counter = MakeText(picker, "Counter", "0 / 5 picked", 52, TextAlignmentOptions.Center, new Color(1f, 1f, 1f, 0.8f));
            SetHeight(counter.gameObject, 80);

            var search = MakeInputField(picker, "SearchField", "Search signs...");
            SetHeight(search.gameObject, 120);

            // Scrolling list of words.
            var scrollRect = MakeRect("WordScroll", picker);
            var scrollBackground = scrollRect.gameObject.AddComponent<Image>();
            scrollBackground.color = new Color(1f, 1f, 1f, 0.05f);
            var scroll = scrollRect.gameObject.AddComponent<ScrollRect>();
            var scrollLayout = scrollRect.gameObject.AddComponent<LayoutElement>();
            scrollLayout.flexibleHeight = 1;
            scrollLayout.minHeight = 400;

            var viewport = MakeRect("Viewport", scrollRect);
            Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();

            var content = MakeRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            var contentLayout = AddVerticalLayout(content.gameObject, 14, 20);
            contentLayout.childAlignment = TextAnchor.UpperCenter;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 40f;

            // Hidden template that MenuController copies once per word.
            var template = MakeButton(content, "WordButtonTemplate", "Word", 130, WordNormal, 54);
            template.gameObject.SetActive(false);

            // Bottom buttons.
            var row = MakeRect("ButtonRow", picker);
            var rowLayout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 24;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = true;
            rowLayout.childForceExpandHeight = true;
            SetHeight(row.gameObject, 170);
            var randomButton = MakeButton(row, "RandomButton", "Fill the rest for me", 170, SecondaryButton, 44);
            var startButton = MakeButton(row, "StartButton", "Start", 170, PrimaryButton, 60);

            var backButton = MakeButton(picker, "BackButton", "Back", 120, SecondaryButton, 48);

            // ---------- Multiplayer panel ----------
            var multiplayer = MakeRect("MultiplayerPanel", safeArea);
            Stretch(multiplayer);
            var multiplayerLayout = AddVerticalLayout(multiplayer.gameObject, 50, 60);
            multiplayerLayout.childAlignment = TextAnchor.MiddleCenter;

            var multiplayerTitle = MakeText(multiplayer, "Title", "Multiplayer", 100, TextAlignmentOptions.Center, Color.white);
            SetHeight(multiplayerTitle.gameObject, 300);
            var createButton = MakeButton(multiplayer, "CreateButton", "Create game", 200, PrimaryButton, 64);
            var joinButton = MakeButton(multiplayer, "JoinButton", "Join game", 200, PrimaryButton, 64);
            var multiplayerBackButton = MakeButton(multiplayer, "BackButton", "Back", 140, SecondaryButton, 50);

            // ---------- Join panel ----------
            var join = MakeRect("JoinPanel", safeArea);
            Stretch(join);
            var joinLayout = AddVerticalLayout(join.gameObject, 36, 60);
            joinLayout.childAlignment = TextAnchor.MiddleCenter;

            var joinTitle = MakeText(join, "Title", "Join a game", 100, TextAlignmentOptions.Center, Color.white);
            SetHeight(joinTitle.gameObject, 220);
            var joinHint = MakeText(join, "Hint", "Enter the 5-character room code", 48, TextAlignmentOptions.Center, new Color(1f, 1f, 1f, 0.8f));
            SetHeight(joinHint.gameObject, 100);
            var codeField = MakeInputField(join, "CodeField", "ROOM CODE");
            codeField.characterLimit = 5;
            SetHeight(codeField.gameObject, 160);
            var joinError = MakeText(join, "ErrorText", "", 44, TextAlignmentOptions.Center, ErrorColor);
            SetHeight(joinError.gameObject, 170);
            var joinConfirmButton = MakeButton(join, "JoinConfirmButton", "Join", 180, PrimaryButton, 64);
            var joinBackButton = MakeButton(join, "BackButton", "Back", 140, SecondaryButton, 50);

            // ---------- Controller ----------
            var controllerObject = new GameObject("MenuController");
            var controller = controllerObject.AddComponent<MenuController>();
            var so = new SerializedObject(controller);
            so.FindProperty("homePanel").objectReferenceValue = home.gameObject;
            so.FindProperty("pickerPanel").objectReferenceValue = picker.gameObject;
            so.FindProperty("multiplayerPanel").objectReferenceValue = multiplayer.gameObject;
            so.FindProperty("joinPanel").objectReferenceValue = join.gameObject;
            so.FindProperty("soloButton").objectReferenceValue = soloButton;
            so.FindProperty("multiplayerButton").objectReferenceValue = multiplayerButton;
            so.FindProperty("counterText").objectReferenceValue = counter;
            so.FindProperty("searchField").objectReferenceValue = search;
            so.FindProperty("listContent").objectReferenceValue = content;
            so.FindProperty("wordButtonTemplate").objectReferenceValue = template;
            so.FindProperty("randomButton").objectReferenceValue = randomButton;
            so.FindProperty("startButton").objectReferenceValue = startButton;
            so.FindProperty("backButton").objectReferenceValue = backButton;
            so.FindProperty("normalColor").colorValue = WordNormal;
            so.FindProperty("selectedColor").colorValue = WordSelected;
            so.FindProperty("createButton").objectReferenceValue = createButton;
            so.FindProperty("joinButton").objectReferenceValue = joinButton;
            so.FindProperty("multiplayerBackButton").objectReferenceValue = multiplayerBackButton;
            so.FindProperty("joinCodeField").objectReferenceValue = codeField;
            so.FindProperty("joinConfirmButton").objectReferenceValue = joinConfirmButton;
            so.FindProperty("joinBackButton").objectReferenceValue = joinBackButton;
            so.FindProperty("joinErrorText").objectReferenceValue = joinError;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Start on the Home panel.
            picker.gameObject.SetActive(false);
            multiplayer.gameObject.SetActive(false);
            join.gameObject.SetActive(false);

            // Save and add to the build.
            if (!AssetDatabase.IsValidFolder("Assets/Scenes")) AssetDatabase.CreateFolder("Assets", "Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings(ScenePath);

            Debug.Log("[MainMenuBuilder] Built " + ScenePath + ". Check Race Scene Name on MenuController, and that your race scene is in the build.");
        }

        // ---------- helpers ----------

        private static void AddToBuildSettings(string path)
        {
            var scenes = EditorBuildSettings.scenes;
            foreach (var s in scenes) if (s.path == path) return;

            var updated = new EditorBuildSettingsScene[scenes.Length + 1];
            updated[0] = new EditorBuildSettingsScene(path, true); // menu first
            for (int i = 0; i < scenes.Length; i++) updated[i + 1] = scenes[i];
            EditorBuildSettings.scenes = updated;
        }

        private static RectTransform MakeRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void SetHeight(GameObject go, float height)
        {
            var element = go.GetComponent<LayoutElement>();
            if (element == null) element = go.AddComponent<LayoutElement>();
            element.preferredHeight = height;
            element.minHeight = height;
        }

        private static VerticalLayoutGroup AddVerticalLayout(GameObject go, float spacing, int padding)
        {
            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = new RectOffset(padding, padding, padding, padding);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.UpperCenter;
            return layout;
        }

        private static TextMeshProUGUI MakeText(Transform parent, string name, string text, float size,
            TextAlignmentOptions alignment, Color color)
        {
            var rt = MakeRect(name, parent);
            var label = rt.gameObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size;
            label.alignment = alignment;
            label.color = color;
            label.raycastTarget = false;
            return label;
        }

        private static Button MakeButton(Transform parent, string name, string label, float height, Color color, float fontSize)
        {
            var rt = MakeRect(name, parent);
            var image = rt.gameObject.AddComponent<Image>();
            image.color = color;
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            var text = MakeText(rt, "Label", label, fontSize, TextAlignmentOptions.Center, Color.white);
            Stretch(text.rectTransform);

            SetHeight(rt.gameObject, height);
            return button;
        }

        private static TMP_InputField MakeInputField(Transform parent, string name, string placeholderText)
        {
            var root = MakeRect(name, parent);
            var background = root.gameObject.AddComponent<Image>();
            background.color = FieldColor;

            var textArea = MakeRect("Text Area", root);
            Stretch(textArea);
            textArea.offsetMin = new Vector2(30, 8);
            textArea.offsetMax = new Vector2(-30, -8);
            textArea.gameObject.AddComponent<RectMask2D>();

            var placeholder = MakeText(textArea, "Placeholder", placeholderText, 48, TextAlignmentOptions.MidlineLeft, new Color(1f, 1f, 1f, 0.4f));
            placeholder.fontStyle = FontStyles.Italic;
            Stretch(placeholder.rectTransform);

            var text = MakeText(textArea, "Text", "", 48, TextAlignmentOptions.MidlineLeft, Color.white);
            Stretch(text.rectTransform);

            var input = root.gameObject.AddComponent<TMP_InputField>();
            input.targetGraphic = background;
            input.textViewport = textArea;
            input.textComponent = text;
            input.placeholder = placeholder;
            return input;
        }
    }
}
