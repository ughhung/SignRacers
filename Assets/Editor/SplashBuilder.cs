#if UNITY_EDITOR
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
    /// Builds the splash screen scene (Assets/Scenes/Splash.unity): your background picture scrolling sideways forever (so the
    /// road looks like it is moving), the game logo zooming in from the right, and "TAP TO START". It becomes the first scene in the build.
    /// Needs two pictures in the project: "gamelogo" (your logo) and "splashscreenbackground" (your background with the road).
    /// Sign Racers > Build Splash Screen. Running it again replaces Splash.unity (the old one is kept as Splash_old.unity).
    /// </summary>
    public static class SplashBuilder
    {
        private const string ScenePath = "Assets/Scenes/Splash.unity";
        private const string BackupPath = "Assets/Scenes/Splash_old.unity";
        private static readonly Color Gold = new Color32(240, 211, 104, 255);

        // The road in your background art spans rows 1325-1692 of 1920, so the logo area starts just above it (as a fraction of the screen).
        private const float LogoAreaBottom = 0.33f;

        [MenuItem("Sign Racers/Build Splash Screen")]
        public static void Build()
        {
            string logoPath = FindTexture("gamelogo");
            string roadPath = FindTexture("splashscreenbackground");
            if (logoPath == null || roadPath == null)
            {
                EditorUtility.DisplayDialog("Pictures missing",
                    "I need two pictures somewhere in your Assets folder:\n\n" +
                    (logoPath == null ? "  - gamelogo  (your logo PNG)  [NOT FOUND]\n" : "  - gamelogo  found\n") +
                    (roadPath == null ? "  - splashscreenbackground  (your background PNG)  [NOT FOUND]\n" : "  - splashscreenbackground  found\n") +
                    "\nDrag them into Assets/Art/UI (create the folders if needed), keep those file names, then run this again.", "OK");
                return;
            }
            if (!EditorUtility.DisplayDialog("Build Splash Screen",
                    "This creates " + ScenePath + " (an older Splash scene is first copied to " + BackupPath + ") and makes it the FIRST scene in the build. Continue?",
                    "Build", "Cancel"))
                return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            SetupLogoImport(logoPath);
            SetupRoadImport(roadPath);
            var logoSprite = AssetDatabase.LoadAssetAtPath<Sprite>(logoPath);
            var roadTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(roadPath);
            UiSkin skin = VotingBuilder.EnsureSkin();

            if (!AssetDatabase.IsValidFolder("Assets/Scenes")) AssetDatabase.CreateFolder("Assets", "Scenes");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            {
                AssetDatabase.DeleteAsset(BackupPath);
                AssetDatabase.CopyAsset(ScenePath, BackupPath);
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camObj = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camObj.tag = "MainCamera";
            var cam = camObj.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Gold;

            var es = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            es.AddComponent<InputSystemUIInputModule>();
#else
            es.AddComponent<StandaloneInputModule>();
#endif

            var canvasObj = new GameObject("SplashCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObj.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObj.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            // Your background picture, filling the screen. The controller slides it sideways forever.
            var roadRect = NewRect("Background", canvasObj.transform);
            Stretch(roadRect);
            var road = roadRect.gameObject.AddComponent<RawImage>();
            road.texture = roadTexture; road.raycastTarget = false;

            // Logo area: the space above the road. The logo sits in the middle of it.
            var area = NewRect("LogoArea", canvasObj.transform);
            area.anchorMin = Vector2.zero; area.anchorMax = Vector2.one;
            area.anchorMin = new Vector2(0f, LogoAreaBottom);
            area.offsetMin = new Vector2(20f, 0f);
            area.offsetMax = new Vector2(-20f, -120f);
            var logoRect = NewRect("Logo", area);
            Stretch(logoRect);
            var logoImage = logoRect.gameObject.AddComponent<Image>();
            logoImage.sprite = logoSprite; logoImage.preserveAspect = true; logoImage.raycastTarget = false;

            // "TAP TO START".
            var promptRect = NewRect("Prompt", canvasObj.transform);
            promptRect.anchorMin = new Vector2(0f, 0.05f); promptRect.anchorMax = new Vector2(1f, 0.12f);
            promptRect.offsetMin = new Vector2(40f, 0f); promptRect.offsetMax = new Vector2(-40f, 0f);
            var promptGroup = promptRect.gameObject.AddComponent<CanvasGroup>();
            promptGroup.blocksRaycasts = false; promptGroup.interactable = false;
            var promptText = promptRect.gameObject.AddComponent<TextMeshProUGUI>();
            promptText.text = "TAP TO START";
            promptText.fontSize = 64; promptText.alignment = TextAlignmentOptions.Center;
            promptText.color = skin.textColor; promptText.raycastTarget = false;
            if (skin.font != null) promptText.font = skin.font;

            // Tap anywhere.
            var tap = NewRect("TapCatcher", canvasObj.transform);
            Stretch(tap);
            var tapImage = tap.gameObject.AddComponent<Image>();
            tapImage.color = new Color(0, 0, 0, 0);
            var tapButton = tap.gameObject.AddComponent<Button>();
            tapButton.targetGraphic = tapImage; tapButton.transition = Selectable.Transition.None;

            // "Credits & licenses" link at the bottom. It opens the web address set on the SplashController (Credits Url).
            var body = skin.bodyFont != null ? skin.bodyFont : skin.font;
            var creditsRect = NewRect("CreditsButton", canvasObj.transform);
            creditsRect.anchorMin = new Vector2(0.2f, 0.012f); creditsRect.anchorMax = new Vector2(0.8f, 0.05f);
            creditsRect.offsetMin = creditsRect.offsetMax = Vector2.zero;
            var creditsHit = creditsRect.gameObject.AddComponent<Image>();
            creditsHit.color = new Color(0, 0, 0, 0);
            var creditsButton = creditsRect.gameObject.AddComponent<Button>();
            creditsButton.targetGraphic = creditsHit; creditsButton.transition = Selectable.Transition.None;
            var creditsLabelRect = NewRect("Label", creditsRect);
            Stretch(creditsLabelRect);
            var creditsLabel = creditsLabelRect.gameObject.AddComponent<TextMeshProUGUI>();
            creditsLabel.text = "Credits & licenses";
            creditsLabel.fontSize = 40; creditsLabel.alignment = TextAlignmentOptions.Center;
            creditsLabel.fontStyle = FontStyles.Underline;
            creditsLabel.color = new Color(skin.textColor.r, skin.textColor.g, skin.textColor.b, 0.8f);
            creditsLabel.raycastTarget = false;
            if (body != null) creditsLabel.font = body;

            // Fade to black when leaving.
            var fadeRect = NewRect("Fade", canvasObj.transform);
            Stretch(fadeRect);
            var fadeImage = fadeRect.gameObject.AddComponent<Image>();
            fadeImage.color = Color.black; fadeImage.raycastTarget = false;
            var fadeGroup = fadeRect.gameObject.AddComponent<CanvasGroup>();
            fadeGroup.alpha = 0f; fadeGroup.blocksRaycasts = false;

            // Controller.
            var controllerObj = new GameObject("SplashController");
            var controller = controllerObj.AddComponent<SplashController>();
            var so = new SerializedObject(controller);
            so.FindProperty("road").objectReferenceValue = road;
            so.FindProperty("roadTileWidth").floatValue = 0f; // 0 = one copy of the picture fills the screen width
            so.FindProperty("logo").objectReferenceValue = logoRect;
            so.FindProperty("prompt").objectReferenceValue = promptGroup;
            so.FindProperty("tapCatcher").objectReferenceValue = tapButton;
            so.FindProperty("fade").objectReferenceValue = fadeGroup;
            so.FindProperty("creditsButton").objectReferenceValue = creditsButton;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            MakeFirstInBuild(ScenePath);

            bool menuInBuild = false;
            foreach (var s in EditorBuildSettings.scenes) if (s.path == "Assets/Scenes/MainMenu.unity" && s.enabled) menuInBuild = true;
            Debug.Log("[SplashBuilder] Built " + ScenePath + " and put it first in the build. " +
                      (menuInBuild ? "MainMenu is in the build too." : "WARNING: MainMenu is not in the build list: add it (File > Build Profiles > Scene List)."));
        }

        private static RectTransform NewRect(string name, Transform parent)
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

        private static string FindTexture(string name)
        {
            foreach (var guid in AssetDatabase.FindAssets(name + " t:Texture2D"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.Equals(System.IO.Path.GetFileNameWithoutExtension(path), name, System.StringComparison.OrdinalIgnoreCase))
                    return path;
            }
            return null;
        }

        private static void SetupLogoImport(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
        }

        private static void SetupRoadImport(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;
            importer.textureType = TextureImporterType.Default;
            importer.wrapMode = TextureWrapMode.Repeat;   // so it can scroll forever
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        private static void MakeFirstInBuild(string path)
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => s.path == path);
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
#endif
