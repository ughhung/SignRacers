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
    /// Uses the same UiSkin as the voting screen (Kenney sprites, gold background, fonts).
    /// NOTE: running it again REPLACES the prefab, so hand edits to the prefab would be lost.
    /// </summary>
    public static class LobbyBuilder
    {
        private const string PrefabPath = "Assets/Prefabs/UI/LobbyCanvas.prefab";
        private const int MaxTrackCards = 6;

        [MenuItem("Sign Racers/Build Lobby Prefab")]
        public static void Build()
        {
            if (EditorSceneManager.GetActiveScene().name != "Game"
                && !EditorUtility.DisplayDialog("Open the Game scene first",
                    "The open scene is '" + EditorSceneManager.GetActiveScene().name + "', not 'Game'. The screen would be put in that scene, " +
                    "and it would disappear whenever the track scene is swapped. Cancel, open the Game scene, and run this again.",
                    "Build anyway", "Cancel"))
                return;

            RaceHudBuilder.EnsureFolder("Assets/Prefabs/UI");
            UiSkin skin = VotingBuilder.EnsureSkin();
            Sprite panel = RaceHudBuilder.GetPanelSprite(); // plain fallback for any empty skin slot

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

            var background = RaceHudBuilder.MakeRect("Background", root.transform);
            RaceHudBuilder.Stretch(background);
            var bg = background.gameObject.AddComponent<Image>();
            bg.sprite = skin.background;
            bg.color = skin.background != null && !skin.tintSprites ? Color.white : skin.backgroundColor;

            var safe = RaceHudBuilder.MakeRect("SafeArea", root.transform);
            RaceHudBuilder.Stretch(safe);
            safe.gameObject.AddComponent<SafeArea>();

            // ---------- room code ----------

            var codeFrame = RaceHudBuilder.MakeRect("CodeFrame", safe);
            RaceHudBuilder.PinTop(codeFrame, -60, 800, 240);
            var codeFrameImage = codeFrame.gameObject.AddComponent<Image>();
            codeFrameImage.sprite = Pick(skin.frame, panel); codeFrameImage.type = Image.Type.Sliced;
            codeFrameImage.color = skin.frame != null ? Color.white : new Color(1f, 1f, 1f, 0.15f);
            codeFrameImage.raycastTarget = true; // the frame is the copy button: tap the code to copy it
            var copy = codeFrame.gameObject.AddComponent<Button>();
            copy.targetGraphic = codeFrameImage;
            var code = RaceHudBuilder.MakeText("CodeText", codeFrame, "ABCDE", 170, TextAlignmentOptions.Center, Color.white);
            code.fontStyle = FontStyles.Bold;
            code.enableAutoSizing = true; code.fontSizeMin = 60; code.fontSizeMax = 170; // "COPIED!" is longer than a code
            RaceHudBuilder.Stretch(code.rectTransform);

            // "tap to copy" sits in the bottom right corner of the code frame.
            var copyHint = RaceHudBuilder.MakeText("CopyHint", codeFrame, "tap to copy", 30, TextAlignmentOptions.BottomRight, Color.white);
            RaceHudBuilder.Stretch(copyHint.rectTransform);
            copyHint.rectTransform.offsetMin = new Vector2(20, 18);
            copyHint.rectTransform.offsetMax = new Vector2(-30, -20);
            copyHint.raycastTarget = false;

            // Host only: PRIVATE (code only) / PUBLIC (Quick Play players can find the room too).
            var visibility = LobbyBuilder.MakeButton("VisibilityButton", safe, Pick(skin.buttonSecondary, panel), skin.Tint(skin.buttonSecondary, skin.secondaryColor), "PRIVATE", 44, new Vector2(440, 84));
            RaceHudBuilder.PinTop((RectTransform)visibility.transform, -322, 440, 84);

            // ---------- players ----------
            var playersHeader = RaceHudBuilder.MakeText("PlayersHeader", safe, "Players  (1/4)", 56, TextAlignmentOptions.Center, Color.white);
            playersHeader.fontStyle = FontStyles.Bold;
            RaceHudBuilder.PinTop(playersHeader.rectTransform, -430, 900, 80);

            var players = new TMP_Text[4];
            var playerRows = new GameObject[4];
            for (int i = 0; i < 4; i++)
            {
                var row = RaceHudBuilder.MakeRect("PlayerRow" + (i + 1), safe);
                RaceHudBuilder.PinTop(row, -515 - i * 88, 760, 78);
                var rowImage = row.gameObject.AddComponent<Image>();
                rowImage.sprite = Pick(skin.row, panel); rowImage.type = Image.Type.Sliced;
                rowImage.color = skin.Tint(skin.row, new Color(1f, 1f, 1f, 0.2f));
                rowImage.raycastTarget = false;
                playerRows[i] = row.gameObject;

                players[i] = RaceHudBuilder.MakeText("Player" + (i + 1), row, "Player " + (i + 1), 46, TextAlignmentOptions.Left, Color.white);
                RaceHudBuilder.Stretch(players[i].rectTransform);
                players[i].rectTransform.offsetMin = new Vector2(36, 0);
            }

            // ---------- track cards (swipe) ----------
            var trackLabel = RaceHudBuilder.MakeText("TrackLabel", safe, "TRACK", 36, TextAlignmentOptions.Center, Color.white);
            RaceHudBuilder.PinTop(trackLabel.rectTransform, -890, 900, 50);

            const float pagerTop = 945f, pagerH = 470f;
            var viewport = RaceHudBuilder.MakeRect("TrackPager", safe);
            RaceHudBuilder.PinTop(viewport, -pagerTop, 1080, pagerH);
            viewport.gameObject.AddComponent<RectMask2D>();
            var catcher = viewport.gameObject.AddComponent<Image>(); // transparent: lets a swipe start anywhere
            catcher.color = new Color(0, 0, 0, 0);
            var pager = viewport.gameObject.AddComponent<SwipePager>();

            var pages = new RectTransform[MaxTrackCards];
            var cardNames = new TMP_Text[MaxTrackCards];
            var cardPreviews = new Image[MaxTrackCards];
            var cardVotes = new TMP_Text[MaxTrackCards];
            var cardButtons = new Button[MaxTrackCards];
            var cardFrames = new Image[MaxTrackCards];
            for (int i = 0; i < MaxTrackCards; i++)
            {
                var page = RaceHudBuilder.MakeRect("TrackCard" + (i + 1), viewport);
                RaceHudBuilder.Stretch(page);
                pages[i] = page;

                var frame = RaceHudBuilder.MakeRect("Frame", page);
                frame.anchorMin = frame.anchorMax = new Vector2(0.5f, 0.5f);
                frame.pivot = new Vector2(0.5f, 0.5f);
                frame.anchoredPosition = Vector2.zero;
                frame.sizeDelta = new Vector2(560, 430);
                var frameImage = frame.gameObject.AddComponent<Image>();
                frameImage.sprite = Pick(skin.frame, panel); frameImage.type = Image.Type.Sliced;
                frameImage.color = skin.frame != null ? Color.white : new Color(1f, 1f, 1f, 0.15f);
                frameImage.raycastTarget = true; // Quick Play: tapping the card votes for that track
                var cardButton = frame.gameObject.AddComponent<Button>();
                cardButton.targetGraphic = frameImage;
                cardButtons[i] = cardButton;
                cardFrames[i] = frameImage;

                // Picture of the track (a plain dark box until you add one to Resources/TrackThumbs).
                var preview = RaceHudBuilder.MakeRect("Preview", frame);
                RaceHudBuilder.PinTop(preview, -24, 480, 275);
                var previewImage = preview.gameObject.AddComponent<Image>();
                previewImage.color = new Color(0.08f, 0.08f, 0.08f, 1f);
                previewImage.raycastTarget = false;
                cardPreviews[i] = previewImage;

                // Quick Play: how many players voted for this track.
                var votes = RaceHudBuilder.MakeText("Votes", preview, "", 34, TextAlignmentOptions.BottomRight, Color.white);
                RaceHudBuilder.Stretch(votes.rectTransform);
                votes.rectTransform.offsetMin = new Vector2(16, 10);
                votes.rectTransform.offsetMax = new Vector2(-16, -10);
                cardVotes[i] = votes;

                var name = RaceHudBuilder.MakeText("Name", frame, "Track", 60, TextAlignmentOptions.Center, Color.white);
                name.fontStyle = FontStyles.Bold;
                name.rectTransform.anchorMin = new Vector2(0f, 0f);
                name.rectTransform.anchorMax = new Vector2(1f, 0f);
                name.rectTransform.pivot = new Vector2(0.5f, 0f);
                name.rectTransform.anchoredPosition = new Vector2(0, 14);
                name.rectTransform.sizeDelta = new Vector2(-40, 84);
                cardNames[i] = name;
            }
            var pso = new SerializedObject(pager);
            pso.FindProperty("pages").arraySize = MaxTrackCards;
            for (int i = 0; i < MaxTrackCards; i++) pso.FindProperty("pages").GetArrayElementAtIndex(i).objectReferenceValue = pages[i];
            pso.ApplyModifiedPropertiesWithoutUndo();

            // Arrow buttons beside the card (the host can tap instead of swiping).
            var prev = LobbyBuilder.MakeButton("PrevTrackButton", safe, Pick(skin.buttonMain, panel), skin.Tint(skin.buttonMain, skin.mainColor), "<", 64, new Vector2(120, 120));
            VotingBuilder.AddArrowIcon(prev, skin.arrowLeft);
            var prect = (RectTransform)prev.transform;
            prect.anchorMin = prect.anchorMax = new Vector2(0.5f, 1f);
            prect.pivot = new Vector2(0.5f, 0.5f);
            prect.anchoredPosition = new Vector2(-400, -(pagerTop + pagerH * 0.5f));
            var next = LobbyBuilder.MakeButton("NextTrackButton", safe, Pick(skin.buttonMain, panel), skin.Tint(skin.buttonMain, skin.mainColor), ">", 64, new Vector2(120, 120));
            VotingBuilder.AddArrowIcon(next, skin.arrowRight);
            var nrect = (RectTransform)next.transform;
            nrect.anchorMin = nrect.anchorMax = new Vector2(0.5f, 1f);
            nrect.pivot = new Vector2(0.5f, 0.5f);
            nrect.anchoredPosition = new Vector2(400, -(pagerTop + pagerH * 0.5f));

            // ---------- bottom ----------
            var status = RaceHudBuilder.MakeText("StatusText", safe, "", 40, TextAlignmentOptions.Center, Color.white);
            status.rectTransform.anchorMin = status.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            status.rectTransform.pivot = new Vector2(0.5f, 0f);
            status.rectTransform.anchoredPosition = new Vector2(0, 220);
            status.rectTransform.sizeDelta = new Vector2(900, 60);

            var leave = RaceHudBuilder.MakeRoundedButton("LeaveButton", safe, Pick(skin.row, panel), skin.Tint(skin.row, skin.secondaryColor), "LEAVE", new Vector2(-215, 60));
            var start = RaceHudBuilder.MakeRoundedButton("StartButton", safe, Pick(skin.buttonGo, panel), skin.Tint(skin.buttonGo, skin.goColor), "START", new Vector2(215, 60));
            foreach (var b in new[] { leave, start })
            {
                var image = b.GetComponent<Image>();
                image.type = Image.Type.Sliced; // RaceHudBuilder already sets Sliced; this keeps it explicit
            }

            // ---------- wire it up ----------
            var view = root.AddComponent<LobbyView>();
            var so = new SerializedObject(view);
            so.FindProperty("group").objectReferenceValue = group;
            so.FindProperty("codeText").objectReferenceValue = code;
            so.FindProperty("visibilityButton").objectReferenceValue = visibility;
            so.FindProperty("visibilityLabel").objectReferenceValue = visibility.GetComponentInChildren<TMP_Text>();
            so.FindProperty("copyButton").objectReferenceValue = copy;
            so.FindProperty("copyHint").objectReferenceValue = copyHint;
            so.FindProperty("playersHeader").objectReferenceValue = playersHeader;
            for (int i = 0; i < 4; i++)
            {
                so.FindProperty("playerTexts").GetArrayElementAtIndex(i).objectReferenceValue = players[i];
                so.FindProperty("playerRows").GetArrayElementAtIndex(i).objectReferenceValue = playerRows[i];
            }
            so.FindProperty("trackPager").objectReferenceValue = pager;
            so.FindProperty("trackCardNames").arraySize = MaxTrackCards;
            so.FindProperty("trackCardPreviews").arraySize = MaxTrackCards;
            so.FindProperty("trackCardVotes").arraySize = MaxTrackCards;
            so.FindProperty("trackCardButtons").arraySize = MaxTrackCards;
            so.FindProperty("trackCardFrames").arraySize = MaxTrackCards;
            so.FindProperty("frameNormal").objectReferenceValue = skin.frame;
            so.FindProperty("frameVoted").objectReferenceValue = skin.frameGood;
            so.FindProperty("trackLabelText").objectReferenceValue = trackLabel;
            for (int i = 0; i < MaxTrackCards; i++)
            {
                so.FindProperty("trackCardNames").GetArrayElementAtIndex(i).objectReferenceValue = cardNames[i];
                so.FindProperty("trackCardPreviews").GetArrayElementAtIndex(i).objectReferenceValue = cardPreviews[i];
                so.FindProperty("trackCardVotes").GetArrayElementAtIndex(i).objectReferenceValue = cardVotes[i];
                so.FindProperty("trackCardButtons").GetArrayElementAtIndex(i).objectReferenceValue = cardButtons[i];
                so.FindProperty("trackCardFrames").GetArrayElementAtIndex(i).objectReferenceValue = cardFrames[i];
            }
            so.FindProperty("prevTrackButton").objectReferenceValue = prev;
            so.FindProperty("nextTrackButton").objectReferenceValue = next;
            so.FindProperty("statusText").objectReferenceValue = status;
            so.FindProperty("startButton").objectReferenceValue = start.GetComponent<Button>();
            so.FindProperty("leaveButton").objectReferenceValue = leave.GetComponent<Button>();
            so.ApplyModifiedPropertiesWithoutUndo();

            // The skin's fonts and text colours, the same rules as the voting screen.
            foreach (var label in root.GetComponentsInChildren<TMP_Text>(true))
            {
                bool small = label.name == "TrackLabel" || label.name == "StatusText" || label.name == "CopyHint";
                var chosen = small && skin.bodyFont != null ? skin.bodyFont : skin.font;
                if (chosen != null) label.font = chosen;
                // Text on the blue/green/grey buttons stays white. Text on the white code frame and track cards (they are buttons too) gets the dark colour.
                var onButton = label.GetComponentInParent<Button>();
                bool onRealButton = onButton != null && onButton.name != "Frame" && onButton.name != "CodeFrame";
                if (!onRealButton && label.name != "Votes") label.color = skin.textColor; // vote counts stay white on the dark picture box
            }
            // The grey LEAVE button needs dark text; the blue and green ones keep white.
            var leaveLabel = leave.GetComponentInChildren<TMP_Text>();
            if (leaveLabel != null && skin.row != null) leaveLabel.color = skin.textColor;

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

        private static Sprite Pick(Sprite custom, Sprite fallback) => custom != null ? custom : fallback;

        /// <summary>A small rounded button with a centred label. Returns the Button.</summary>
        internal static Button MakeButton(string name, RectTransform parent, Sprite sprite, Color color, string label, float fontSize, Vector2 size)
        {
            var rect = RaceHudBuilder.MakeRect(name, parent);
            rect.sizeDelta = size;
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite; image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple; image.color = color;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var text = RaceHudBuilder.MakeText("Label", rect, label, fontSize, TextAlignmentOptions.Center, Color.white);
            RaceHudBuilder.Stretch(text.rectTransform);
            text.fontStyle = FontStyles.Bold;
            return button;
        }
    }
}
