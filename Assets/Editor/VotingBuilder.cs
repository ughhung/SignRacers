using SignRacers.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace SignRacers.EditorTools
{
    /// <summary>
    /// One click: builds the word vote and review screens as one prefab (Assets/Prefabs/UI/VotingCanvas.prefab) and puts
    /// a copy in the open scene if there isn't one. Open the Game scene first. Sign Racers > Build Voting And Review Prefab.
    /// NOTE: running it again REPLACES the prefab, so hand edits to the prefab would be lost.
    /// </summary>
    public static class VotingBuilder
    {
        private const string PrefabPath = "Assets/Prefabs/UI/VotingCanvas.prefab";
        private static readonly Color Dim = new Color(1f, 1f, 1f, 0.65f);
        private static readonly Color Slate = new Color(0.30f, 0.34f, 0.46f);
        private static readonly Color Blue = new Color(0.25f, 0.55f, 0.95f);

        [MenuItem("Sign Racers/Build Voting And Review Prefab")]
        public static void Build()
        {
            RaceHudBuilder.EnsureFolder("Assets/Prefabs/UI");
            Sprite panel = RaceHudBuilder.GetPanelSprite();

            var root = new GameObject("VotingCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
                                      typeof(GraphicRaycaster), typeof(CanvasGroup));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 31;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            var group = root.GetComponent<CanvasGroup>();

            var background = RaceHudBuilder.MakeRect("Background", root.transform);
            RaceHudBuilder.Stretch(background);
            background.gameObject.AddComponent<Image>().color = new Color(0.05f, 0.06f, 0.12f, 1f);

            var safe = RaceHudBuilder.MakeRect("SafeArea", root.transform);
            RaceHudBuilder.Stretch(safe);
            safe.gameObject.AddComponent<SafeArea>();

            // ---------- voting panel ----------
            var vote = RaceHudBuilder.MakeRect("VotePanel", safe);
            RaceHudBuilder.Stretch(vote);

            var voteTitle = RaceHudBuilder.MakeText("Title", vote, "VOTE FOR 5 SIGNS", 70, TextAlignmentOptions.Center, Color.white);
            voteTitle.fontStyle = FontStyles.Bold;
            RaceHudBuilder.PinTop(voteTitle.rectTransform, -40, 1000, 110);
            var voteInfo = RaceHudBuilder.MakeText("InfoText", vote, "0:45      0 / 5 chosen", 44, TextAlignmentOptions.Center, Color.white);
            RaceHudBuilder.PinTop(voteInfo.rectTransform, -150, 1000, 70);

            // Scrolling list of words.
            var scrollRect = RaceHudBuilder.MakeRect("WordScroll", vote);
            scrollRect.anchorMin = Vector2.zero; scrollRect.anchorMax = Vector2.one;
            scrollRect.offsetMin = new Vector2(30, 330); scrollRect.offsetMax = new Vector2(-30, -240);
            var scroll = scrollRect.gameObject.AddComponent<ScrollRect>();
            var viewport = RaceHudBuilder.MakeRect("Viewport", scrollRect);
            RaceHudBuilder.Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            var catcher = viewport.gameObject.AddComponent<Image>(); // lets drags start anywhere in the list
            catcher.color = new Color(0, 0, 0, 0);
            var content = RaceHudBuilder.MakeRect("Content", viewport);
            content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero; content.sizeDelta = Vector2.zero;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 12;
            layout.childControlWidth = true; layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport; scroll.content = content;
            scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40;

            // One row to copy for every word (kept inactive).
            var rowTemplate = RaceHudBuilder.MakeRect("WordRow", content);
            var rowImage = rowTemplate.gameObject.AddComponent<Image>();
            rowImage.sprite = panel; rowImage.type = Image.Type.Sliced; rowImage.color = new Color(0.20f, 0.24f, 0.37f, 1f);
            var rowButton = rowTemplate.gameObject.AddComponent<Button>();
            rowButton.targetGraphic = rowImage;
            rowTemplate.gameObject.AddComponent<LayoutElement>().preferredHeight = 110;
            var rowLabel = RaceHudBuilder.MakeText("Label", rowTemplate, "Word", 52, TextAlignmentOptions.Left, Color.white);
            RaceHudBuilder.Stretch(rowLabel.rectTransform);
            rowLabel.rectTransform.offsetMin = new Vector2(36, 0);
            var rowVotes = RaceHudBuilder.MakeText("Votes", rowTemplate, "", 36, TextAlignmentOptions.Right, Dim);
            RaceHudBuilder.Stretch(rowVotes.rectTransform);
            rowVotes.rectTransform.offsetMax = new Vector2(-36, 0);
            rowTemplate.gameObject.SetActive(false);

            var voteStatus = RaceHudBuilder.MakeText("StatusText", vote, "", 36, TextAlignmentOptions.Center, Dim);
            BottomText(voteStatus, 195);
            var doneButton = MakeBottomButton("DoneButton", vote, panel, Blue, "DONE VOTING");

            // ---------- review panel ----------
            var review = RaceHudBuilder.MakeRect("ReviewPanel", safe);
            RaceHudBuilder.Stretch(review);

            var reviewTitle = RaceHudBuilder.MakeText("Title", review, "YOUR 5 SIGNS", 70, TextAlignmentOptions.Center, Color.white);
            reviewTitle.fontStyle = FontStyles.Bold;
            RaceHudBuilder.PinTop(reviewTitle.rectTransform, -40, 1000, 110);
            var reviewInfo = RaceHudBuilder.MakeText("InfoText", review, "1:00      practice, then tap READY", 44, TextAlignmentOptions.Center, Color.white);
            RaceHudBuilder.PinTop(reviewInfo.rectTransform, -150, 1000, 70);

            var rows = new GameObject[5];
            var wordTexts = new TMP_Text[5];
            var tryButtons = new Button[5];
            for (int i = 0; i < 5; i++)
            {
                var row = RaceHudBuilder.MakeRect("Row" + (i + 1), review);
                RaceHudBuilder.PinTop(row, -250 - i * 190, 960, 170);
                var img = row.gameObject.AddComponent<Image>();
                img.sprite = panel; img.type = Image.Type.Sliced; img.color = new Color(1f, 1f, 1f, 0.08f);
                rows[i] = row.gameObject;

                var word = RaceHudBuilder.MakeText("Word", row, "Word", 62, TextAlignmentOptions.Left, Color.white);
                word.fontStyle = FontStyles.Bold;
                word.rectTransform.anchorMin = new Vector2(0, 0.35f); word.rectTransform.anchorMax = new Vector2(0.65f, 1f);
                word.rectTransform.offsetMin = new Vector2(36, 0); word.rectTransform.offsetMax = new Vector2(0, -6);
                wordTexts[i] = word;

                var demo = RaceHudBuilder.MakeText("DemoNote", row, "demo coming soon", 32, TextAlignmentOptions.Left, Dim);
                demo.rectTransform.anchorMin = new Vector2(0, 0); demo.rectTransform.anchorMax = new Vector2(0.65f, 0.4f);
                demo.rectTransform.offsetMin = new Vector2(36, 8); demo.rectTransform.offsetMax = Vector2.zero;

                var tryButton = LobbyBuilder.MakeButton("TryButton", row, panel, Blue, "TRY IT", 46, new Vector2(270, 110));
                var trect = (RectTransform)tryButton.transform;
                trect.anchorMin = trect.anchorMax = new Vector2(1f, 0.5f);
                trect.pivot = new Vector2(1f, 0.5f);
                trect.anchoredPosition = new Vector2(-30, 0);
                tryButtons[i] = tryButton;
            }

            var practice = RaceHudBuilder.MakeText("PracticeText", review, "", 44, TextAlignmentOptions.Center, Color.white);
            RaceHudBuilder.PinTop(practice.rectTransform, -1230, 960, 130);
            var reviewStatus = RaceHudBuilder.MakeText("StatusText", review, "", 36, TextAlignmentOptions.Center, Dim);
            BottomText(reviewStatus, 195);
            var readyButton = MakeBottomButton("ReadyButton", review, panel, new Color(0.25f, 0.65f, 0.35f), "READY");

            // ---------- wire it up ----------
            var view = root.AddComponent<VotingView>();
            var so = new SerializedObject(view);
            so.FindProperty("group").objectReferenceValue = group;
            so.FindProperty("votePanel").objectReferenceValue = vote.gameObject;
            so.FindProperty("reviewPanel").objectReferenceValue = review.gameObject;
            so.FindProperty("voteInfoText").objectReferenceValue = voteInfo;
            so.FindProperty("voteStatusText").objectReferenceValue = voteStatus;
            so.FindProperty("doneButton").objectReferenceValue = doneButton;
            so.FindProperty("doneLabel").objectReferenceValue = doneButton.GetComponentInChildren<TMP_Text>();
            so.FindProperty("wordScroll").objectReferenceValue = scroll;
            so.FindProperty("wordContent").objectReferenceValue = content;
            so.FindProperty("rowTemplate").objectReferenceValue = rowTemplate.gameObject;
            so.FindProperty("reviewInfoText").objectReferenceValue = reviewInfo;
            for (int i = 0; i < 5; i++)
            {
                so.FindProperty("reviewRows").GetArrayElementAtIndex(i).objectReferenceValue = rows[i];
                so.FindProperty("reviewWordTexts").GetArrayElementAtIndex(i).objectReferenceValue = wordTexts[i];
                so.FindProperty("tryButtons").GetArrayElementAtIndex(i).objectReferenceValue = tryButtons[i];
            }
            so.FindProperty("practiceText").objectReferenceValue = practice;
            so.FindProperty("reviewStatusText").objectReferenceValue = reviewStatus;
            so.FindProperty("readyButton").objectReferenceValue = readyButton;
            so.FindProperty("readyLabel").objectReferenceValue = readyButton.GetComponentInChildren<TMP_Text>();
            so.ApplyModifiedPropertiesWithoutUndo();

            review.gameObject.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

            string report = "Built " + PrefabPath + ".\n";
            if (Object.FindFirstObjectByType<VotingView>() == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                Undo.RegisterCreatedObjectUndo(instance, "Add Voting");
                EditorSceneManager.MarkSceneDirty(instance.scene);
                report += "Added a VotingCanvas to the open scene (it should be the Game scene). Save the scene.";
            }
            else report += "The open scene already has a VotingCanvas, and it updates from the prefab.";
            EditorUtility.DisplayDialog("Voting prefab built", report, "OK");
        }

        private static void BottomText(TMP_Text text, float y)
        {
            text.rectTransform.anchorMin = text.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            text.rectTransform.pivot = new Vector2(0.5f, 0f);
            text.rectTransform.anchoredPosition = new Vector2(0, y);
            text.rectTransform.sizeDelta = new Vector2(960, 70);
        }

        private static Button MakeBottomButton(string name, RectTransform parent, Sprite sprite, Color color, string label)
        {
            var button = LobbyBuilder.MakeButton(name, parent, sprite, color, label, 54, new Vector2(700, 120));
            var rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0, 60);
            return button;
        }
    }
}
