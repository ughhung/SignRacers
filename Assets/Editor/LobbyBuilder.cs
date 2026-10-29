using SignRacers.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace SignRacers.EditorTools
{
    /// <summary>
    /// One click: builds the online lobby screen as a prefab (Assets/Prefabs/UI/LobbyCanvas.prefab) and puts a copy
    /// in the open scene if there isn't one. Open the Game scene first. Sign Racers > Build Lobby Prefab.
    /// NOTE: running it again REPLACES the prefab, so hand edits to the prefab would be lost.
    /// </summary>
    public static class LobbyBuilder
    {
        private const string PrefabPath = "Assets/Prefabs/UI/LobbyCanvas.prefab";
        private static readonly Color Dim = new Color(1f, 1f, 1f, 0.65f);

        [MenuItem("Sign Racers/Build Lobby Prefab")]
        public static void Build()
        {
            RaceHudBuilder.EnsureFolder("Assets/Prefabs/UI");
            Sprite panel = RaceHudBuilder.GetPanelSprite();

            var root = new GameObject("LobbyCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
                                      typeof(GraphicRaycaster), typeof(CanvasGroup));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30; // in front of the race HUD and the results screen
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            var group = root.GetComponent<CanvasGroup>();

            // The plain background. Change its colour here.
            var background = RaceHudBuilder.MakeRect("Background", root.transform);
            RaceHudBuilder.Stretch(background);
            var bg = background.gameObject.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.06f, 0.12f, 1f);

            var safe = RaceHudBuilder.MakeRect("SafeArea", root.transform);
            RaceHudBuilder.Stretch(safe);
            safe.gameObject.AddComponent<SafeArea>();

            var codeLabel = RaceHudBuilder.MakeText("CodeLabel", safe, "ROOM CODE", 40, TextAlignmentOptions.Center, Dim);
            RaceHudBuilder.PinTop(codeLabel.rectTransform, -70, 900, 60);

            var code = RaceHudBuilder.MakeText("CodeText", safe, "ABCDE", 200, TextAlignmentOptions.Center, Color.white);
            code.fontStyle = FontStyles.Bold;
            RaceHudBuilder.PinTop(code.rectTransform, -130, 1000, 240);

            var copy = MakeButton("CopyButton", safe, panel, new Color(0.30f, 0.34f, 0.46f), "COPY CODE", 46, new Vector2(460, 110));
            RaceHudBuilder.PinTop((RectTransform)copy.transform, -400, 460, 110);

            var playersHeader = RaceHudBuilder.MakeText("PlayersHeader", safe, "Players  (1/4)", 56, TextAlignmentOptions.Center, Color.white);
            playersHeader.fontStyle = FontStyles.Bold;
            RaceHudBuilder.PinTop(playersHeader.rectTransform, -570, 900, 80);

            var players = new TMP_Text[4];
            for (int i = 0; i < 4; i++)
            {
                players[i] = RaceHudBuilder.MakeText("Player" + (i + 1), safe, "Player " + (i + 1), 52, TextAlignmentOptions.Left, Color.white);
                RaceHudBuilder.PinTop(players[i].rectTransform, -665 - i * 85, 760, 75);
            }

            var trackLabel = RaceHudBuilder.MakeText("TrackLabel", safe, "TRACK", 36, TextAlignmentOptions.Center, Dim);
            RaceHudBuilder.PinTop(trackLabel.rectTransform, -1060, 900, 50);

            var trackName = RaceHudBuilder.MakeText("TrackName", safe, "Hand", 72, TextAlignmentOptions.Center, new Color(1f, 0.84f, 0.25f));
            trackName.fontStyle = FontStyles.Bold;
            RaceHudBuilder.PinTop(trackName.rectTransform, -1115, 560, 120);

            var prev = MakeButton("PrevTrackButton", safe, panel, new Color(0.30f, 0.34f, 0.46f), "<", 70, new Vector2(120, 120));
            RaceHudBuilder.PinTop((RectTransform)prev.transform, -1115, 120, 120);
            ((RectTransform)prev.transform).anchoredPosition = new Vector2(-360, -1115);
            var next = MakeButton("NextTrackButton", safe, panel, new Color(0.30f, 0.34f, 0.46f), ">", 70, new Vector2(120, 120));
            RaceHudBuilder.PinTop((RectTransform)next.transform, -1115, 120, 120);
            ((RectTransform)next.transform).anchoredPosition = new Vector2(360, -1115);

            var status = RaceHudBuilder.MakeText("StatusText", safe, "", 40, TextAlignmentOptions.Center, Dim);
            status.rectTransform.anchorMin = status.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            status.rectTransform.pivot = new Vector2(0.5f, 0f);
            status.rectTransform.anchoredPosition = new Vector2(0, 220);
            status.rectTransform.sizeDelta = new Vector2(900, 60);

            var leave = RaceHudBuilder.MakeRoundedButton("LeaveButton", safe, panel, new Color(0.30f, 0.34f, 0.46f), "LEAVE", new Vector2(-215, 60));
            var start = RaceHudBuilder.MakeRoundedButton("StartButton", safe, panel, new Color(0.25f, 0.65f, 0.35f), "START", new Vector2(215, 60));

            var view = root.AddComponent<LobbyView>();
            var so = new SerializedObject(view);
            so.FindProperty("group").objectReferenceValue = group;
            so.FindProperty("codeText").objectReferenceValue = code;
            so.FindProperty("copyButton").objectReferenceValue = copy;
            so.FindProperty("copyLabel").objectReferenceValue = copy.GetComponentInChildren<TMP_Text>();
            so.FindProperty("playersHeader").objectReferenceValue = playersHeader;
            for (int i = 0; i < 4; i++) so.FindProperty("playerTexts").GetArrayElementAtIndex(i).objectReferenceValue = players[i];
            so.FindProperty("trackNameText").objectReferenceValue = trackName;
            so.FindProperty("prevTrackButton").objectReferenceValue = prev;
            so.FindProperty("nextTrackButton").objectReferenceValue = next;
            so.FindProperty("statusText").objectReferenceValue = status;
            so.FindProperty("startButton").objectReferenceValue = start.GetComponent<Button>();
            so.FindProperty("leaveButton").objectReferenceValue = leave.GetComponent<Button>();
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

            string report = "Built " + PrefabPath + ".\n";
            if (Object.FindFirstObjectByType<LobbyView>() == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                Undo.RegisterCreatedObjectUndo(instance, "Add Lobby");
                EditorSceneManager.MarkSceneDirty(instance.scene);
                report += "Added a LobbyCanvas to the open scene (it should be the Game scene). Save the scene.";
            }
            else report += "The open scene already has a LobbyCanvas, and it updates from the prefab.";
            EditorUtility.DisplayDialog("Lobby prefab built", report, "OK");
        }

        /// <summary>A small rounded button with a centred label. Returns the Button.</summary>
        internal static Button MakeButton(string name, RectTransform parent, Sprite sprite, Color color, string label, float fontSize, Vector2 size)
        {
            var rect = RaceHudBuilder.MakeRect(name, parent);
            rect.sizeDelta = size;
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite; image.type = Image.Type.Sliced; image.color = color;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var text = RaceHudBuilder.MakeText("Label", rect, label, fontSize, TextAlignmentOptions.Center, Color.white);
            RaceHudBuilder.Stretch(text.rectTransform);
            text.fontStyle = FontStyles.Bold;
            return button;
        }
    }
}
