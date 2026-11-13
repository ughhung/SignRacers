using Gtk.Unity;
using SignRacers.Signs;
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
        private static UiSkin skin;
        private static Color Slate, Card, Green, Blue;

        [MenuItem("Sign Racers/Build Voting And Review Prefab")]
        public static void Build()
        {
            if (EditorSceneManager.GetActiveScene().name != "Game"
                && !EditorUtility.DisplayDialog("Open the Game scene first",
                    "The open scene is '" + EditorSceneManager.GetActiveScene().name + "', not 'Game'. The screen would be put in that scene, " +
                    "and it would disappear whenever the track scene is swapped. Cancel, open the Game scene, and run this again.",
                    "Build anyway", "Cancel"))
                return;

            RaceHudBuilder.EnsureFolder("Assets/Prefabs/UI");
            skin = EnsureSkin(); // creates Resources/UiSkin if needed and fills empty slots from Assets/Art/UI and your fonts
            Slate = skin.secondaryColor; Card = skin.panelColor; Green = skin.goColor; Blue = skin.mainColor;
            Sprite panel = RaceHudBuilder.GetPanelSprite(); // the built-in rounded shape, used for any empty slot

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
            var backgroundImage = background.gameObject.AddComponent<Image>();
            backgroundImage.sprite = skin.background;
            backgroundImage.color = skin.background != null && !skin.tintSprites ? Color.white : skin.backgroundColor;

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

            // Search box above the list.
            var searchField = AddSearchBar(vote, scrollRect, skin, panel, out var noMatch);

            // One row to copy for every word (kept inactive).
            var rowTemplate = RaceHudBuilder.MakeRect("WordRow", content);
            var rowImage = rowTemplate.gameObject.AddComponent<Image>();
            rowImage.sprite = Pick(skin.row, panel); rowImage.type = Image.Type.Sliced; rowImage.color = skin.RowColor;
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
            var doneButton = MakeBottomButton("DoneButton", vote, Pick(skin.buttonMain, panel), skin.Tint(skin.buttonMain, Blue), "DONE VOTING");

            // ---------- review panel ----------
            var review = RaceHudBuilder.MakeRect("ReviewPanel", safe);
            RaceHudBuilder.Stretch(review);

            var reviewTitle = RaceHudBuilder.MakeText("Title", review, "YOUR 5 SIGNS", 70, TextAlignmentOptions.Center, Color.white);
            reviewTitle.fontStyle = FontStyles.Bold;
            RaceHudBuilder.PinTop(reviewTitle.rectTransform, -40, 1000, 110);
            var reviewInfo = RaceHudBuilder.MakeText("InfoText", review, "1:00      swipe to practice, then tap READY", 44, TextAlignmentOptions.Center, Color.white);
            RaceHudBuilder.PinTop(reviewInfo.rectTransform, -150, 1000, 70);

            // Card layout: one wide (16:9) box for both the demo video and the camera.
            Vector2 mediaSize = new Vector2(900f, 506f);
            const float pad = 28f; // frame border around the media
            float frameW = mediaSize.x + pad * 2f, frameH = mediaSize.y + pad * 2f;
            float frameTopY = 190f;
            float statusTopY = frameTopY + frameH + 10f;
            float buttonsTopY = frameTopY + frameH + 120f;
            float pagerH = buttonsTopY + 150f + 20f;
            float pagerTopY = 240f + Mathf.Max(0f, (1260f - (pagerH + 140f)) * 0.5f); // centre the block a little
            float dotsY = pagerTopY + pagerH + 80f; // a little lower, away from the buttons

            // Swipeable cards: a clipped viewport with five pages stacked side by side.
            var cardViewport = RaceHudBuilder.MakeRect("CardPager", review);
            RaceHudBuilder.PinTop(cardViewport, -pagerTopY, 1080, pagerH);
            cardViewport.gameObject.AddComponent<RectMask2D>();
            var cardCatcher = cardViewport.gameObject.AddComponent<Image>(); // transparent: lets a swipe start anywhere
            cardCatcher.color = new Color(0, 0, 0, 0);
            var pager = cardViewport.gameObject.AddComponent<SwipePager>();

            var pageRects = new RectTransform[5];
            var cards = new PracticeCard[5];
            for (int i = 0; i < 5; i++)
            {
                var page = RaceHudBuilder.MakeRect("Card" + (i + 1), cardViewport);
                RaceHudBuilder.Stretch(page);
                pageRects[i] = page;

                var word = RaceHudBuilder.MakeText("Word", page, "Word", 96, TextAlignmentOptions.Center, Color.white);
                word.fontStyle = FontStyles.Bold;
                RaceHudBuilder.PinTop(word.rectTransform, -25, 1000, 150);

                // Frame (turns green / red after a review) with the media area inside it.
                var frameRect = RaceHudBuilder.MakeRect("Frame", page);
                RaceHudBuilder.PinTop(frameRect, -frameTopY, frameW, frameH);
                var frameImage = frameRect.gameObject.AddComponent<Image>();
                frameImage.sprite = Pick(skin.frame, panel); frameImage.type = Image.Type.Sliced;
                frameImage.color = skin.frame != null ? Color.white : new Color(1f, 1f, 1f, 0.15f);
                frameImage.raycastTarget = false;

                var media = RaceHudBuilder.MakeRect("Media", frameRect);
                media.anchorMin = media.anchorMax = new Vector2(0.5f, 0.5f);
                media.pivot = new Vector2(0.5f, 0.5f);
                media.anchoredPosition = Vector2.zero;
                media.sizeDelta = mediaSize;
                media.gameObject.AddComponent<RectMask2D>(); // anything bigger than the box is clipped, never spills over the frame
                var mediaBg = media.gameObject.AddComponent<Image>();
                mediaBg.color = new Color(0.08f, 0.08f, 0.08f, 1f);
                mediaBg.raycastTarget = false;

                var hint = RaceHudBuilder.MakeText("Hint", media, "Tap LEARN to see the sign\nor REVIEW to try it", 32, TextAlignmentOptions.Center, Dim);
                RaceHudBuilder.Stretch(hint.rectTransform);
                hint.raycastTarget = false;

                var camRect = RaceHudBuilder.MakeRect("Camera", media);
                RaceHudBuilder.Stretch(camRect);
                var cam = camRect.gameObject.AddComponent<RawImage>();
                cam.raycastTarget = false;
                camRect.gameObject.SetActive(false);

                var status = RaceHudBuilder.MakeText("Status", page, "", 50, TextAlignmentOptions.Center, Color.white);
                status.fontStyle = FontStyles.Bold;
                status.raycastTarget = false;
                RaceHudBuilder.PinTop(status.rectTransform, -statusTopY, 1000, 100);

                var learnBtn = LobbyBuilder.MakeButton("LearnButton", page, Pick(skin.buttonMain, panel), skin.Tint(skin.buttonMain, Blue), "LEARN", 52, new Vector2(440, 150));
                var lrect = (RectTransform)learnBtn.transform;
                lrect.anchorMin = lrect.anchorMax = new Vector2(0.5f, 1f);
                lrect.pivot = new Vector2(0.5f, 1f);
                lrect.anchoredPosition = new Vector2(-235, -buttonsTopY);

                var reviewBtn = LobbyBuilder.MakeButton("ReviewButton", page, Pick(skin.buttonMain, panel), skin.Tint(skin.buttonMain, Blue), "REVIEW", 52, new Vector2(440, 150));
                var rrect = (RectTransform)reviewBtn.transform;
                rrect.anchorMin = rrect.anchorMax = new Vector2(0.5f, 1f);
                rrect.pivot = new Vector2(0.5f, 1f);
                rrect.anchoredPosition = new Vector2(235, -buttonsTopY);

                var card = page.gameObject.AddComponent<PracticeCard>();
                var cso = new SerializedObject(card);
                cso.FindProperty("wordText").objectReferenceValue = word;
                cso.FindProperty("mediaSlot").objectReferenceValue = media;
                cso.FindProperty("hintText").objectReferenceValue = hint;
                cso.FindProperty("cameraImage").objectReferenceValue = cam;
                cso.FindProperty("frame").objectReferenceValue = frameImage;
                cso.FindProperty("statusText").objectReferenceValue = status;
                cso.FindProperty("learnButton").objectReferenceValue = learnBtn;
                cso.FindProperty("reviewButton").objectReferenceValue = reviewBtn;
                cso.ApplyModifiedPropertiesWithoutUndo();
                cards[i] = card;
            }
            var pso = new SerializedObject(pager);
            for (int i = 0; i < 5; i++) pso.FindProperty("pages").arraySize = 5;
            for (int i = 0; i < 5; i++) pso.FindProperty("pages").GetArrayElementAtIndex(i).objectReferenceValue = pageRects[i];
            pso.ApplyModifiedPropertiesWithoutUndo();

            // Page dots under the cards.
            var dotsRect = RaceHudBuilder.MakeRect("Dots", review);
            RaceHudBuilder.PinTop(dotsRect, -dotsY, 400, 40);
            var dots = new Image[5];
            for (int i = 0; i < 5; i++)
            {
                var dot = RaceHudBuilder.MakeRect("Dot" + (i + 1), dotsRect);
                dot.anchorMin = dot.anchorMax = new Vector2(0.5f, 0.5f);
                dot.sizeDelta = new Vector2(44, 44);
                dot.anchoredPosition = new Vector2((i - 2) * 64, 0);
                dots[i] = dot.gameObject.AddComponent<Image>();
                dots[i].raycastTarget = false;
                dots[i].sprite = skin.dotOff;
            }

            // Arrow buttons beside the dots: tap instead of swiping.
            var prevButton = LobbyBuilder.MakeButton("PrevButton", review, Pick(skin.buttonSecondary, panel), skin.Tint(skin.buttonSecondary, Slate), "<", 64, new Vector2(150, 100));
            AddArrowIcon(prevButton, skin.arrowLeft);
            var prect = (RectTransform)prevButton.transform;
            prect.anchorMin = prect.anchorMax = new Vector2(0.5f, 1f);
            prect.pivot = new Vector2(0.5f, 1f);
            prect.anchoredPosition = new Vector2(-300, -(dotsY - 30f));
            var nextButton = LobbyBuilder.MakeButton("NextButton", review, Pick(skin.buttonSecondary, panel), skin.Tint(skin.buttonSecondary, Slate), ">", 64, new Vector2(150, 100));
            AddArrowIcon(nextButton, skin.arrowRight);
            var nrect = (RectTransform)nextButton.transform;
            nrect.anchorMin = nrect.anchorMax = new Vector2(0.5f, 1f);
            nrect.pivot = new Vector2(0.5f, 1f);
            nrect.anchoredPosition = new Vector2(300, -(dotsY - 30f));

            // The one reference video, hidden until LEARN moves it into a card's media area.
            var videoRect = RaceHudBuilder.MakeRect("Video", review);
            RaceHudBuilder.Stretch(videoRect);
            var videoView = videoRect.gameObject.AddComponent<VideoView>();
            videoView.raycastTarget = false;
            var videoFit = videoRect.gameObject.AddComponent<AspectRatioFitter>(); // keep the clip 16:9 inside the box
            videoFit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            videoFit.aspectRatio = 16f / 9f;
            var referenceVideo = videoRect.gameObject.AddComponent<ReferenceVideo>();
            videoRect.gameObject.SetActive(false);

            var reviewStatus = RaceHudBuilder.MakeText("StatusText", review, "", 36, TextAlignmentOptions.Center, Dim);
            BottomText(reviewStatus, 195);
            var readyButton = MakeBottomButton("ReadyButton", review, Pick(skin.buttonGo, panel), skin.Tint(skin.buttonGo, Green), "READY");

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
            so.FindProperty("searchField").objectReferenceValue = searchField;
            so.FindProperty("noMatchText").objectReferenceValue = noMatch.gameObject;
            so.FindProperty("wordContent").objectReferenceValue = content;
            so.FindProperty("rowTemplate").objectReferenceValue = rowTemplate.gameObject;
            so.FindProperty("reviewInfoText").objectReferenceValue = reviewInfo;
            so.FindProperty("pager").objectReferenceValue = pager;
            so.FindProperty("referenceVideo").objectReferenceValue = referenceVideo;
            so.FindProperty("prevButton").objectReferenceValue = prevButton;
            so.FindProperty("nextButton").objectReferenceValue = nextButton;
            for (int i = 0; i < 5; i++)
            {
                so.FindProperty("cards").GetArrayElementAtIndex(i).objectReferenceValue = cards[i];
                so.FindProperty("dots").GetArrayElementAtIndex(i).objectReferenceValue = dots[i];
            }
            so.FindProperty("reviewStatusText").objectReferenceValue = reviewStatus;
            so.FindProperty("readyButton").objectReferenceValue = readyButton;
            so.FindProperty("readyLabel").objectReferenceValue = readyButton.GetComponentInChildren<TMP_Text>();
            so.ApplyModifiedPropertiesWithoutUndo();

            // The skin's fonts: the main font on titles, words and buttons; the body font on the small text.
            foreach (var label in root.GetComponentsInChildren<TMP_Text>(true))
            {
                bool small = label.name == "InfoText" || label.name == "StatusText" || label.name == "Status"
                             || label.name == "Hint" || label.name == "Votes";
                var chosen = small && skin.bodyFont != null ? skin.bodyFont : skin.font;
                if (chosen != null) label.font = chosen;

                // Dark text on the light background. Text on buttons (white) and the hint inside the dark camera box keep their colours.
                if (label.GetComponentInParent<Button>() == null && label.name != "Hint" && label.name != "Placeholder") label.color = skin.textColor;
            }

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

        /// <summary>The size of the race HUD's camera box (so the practice camera matches it). Falls back to 380 x 400.</summary>
        private static Vector2 HudCameraSize()
        {
            var fallback = new Vector2(380f, 400f);
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab"))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (prefab == null || prefab.GetComponentInChildren<RaceHudView>(true) == null) continue;
                foreach (var t in prefab.GetComponentsInChildren<RectTransform>(true))
                {
                    if (t.name != "CameraArea") continue;
                    var parent = t.parent as RectTransform;
                    if (parent == null) continue;
                    Vector2 size = parent.rect.size;
                    if (size.x < 1f || size.y < 1f) size = parent.sizeDelta;
                    size += t.offsetMax + t.offsetMin; // margins are negative on the max side, so this shrinks the box
                    if (size.x >= 100f && size.y >= 100f)
                    {
                        Debug.Log("Voting screen: camera box matched to the race HUD (" + Mathf.RoundToInt(size.x) + " x " + Mathf.RoundToInt(size.y) + ").");
                        return size;
                    }
                }
            }
            Debug.LogWarning("Voting screen: couldn't read the race HUD's camera box size, using " + fallback.x + " x " + fallback.y + ".");
            return fallback;
        }

        private const string SpriteFolder = "Assets/Art/UI/";

        /// <summary>Loads (or creates) Resources/UiSkin.asset and fills every EMPTY slot it can find. Never overwrites a slot you set.</summary>
        internal static UiSkin EnsureSkin()
        {
            const string path = "Assets/Resources/UiSkin.asset";
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
            var s = AssetDatabase.LoadAssetAtPath<UiSkin>(path);
            if (s == null)
            {
                s = ScriptableObject.CreateInstance<UiSkin>();
                AssetDatabase.CreateAsset(s, path);
            }

            var missing = new System.Collections.Generic.List<string>();
            Sprite Find(string name)
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteFolder + name + ".png");
                if (sprite == null) missing.Add(name);
                return sprite;
            }
            // Palette sprites replace the earlier ui_kit_* ones; anything you set yourself is left alone.
            void Fill(ref Sprite slot, string name)
            {
                bool replaceable = slot == null || (((slot.name.StartsWith("ui_kit_") || slot.name.StartsWith("ui_pal_")) && name.StartsWith("ui_kn_")) || (slot.name == "ui_kn_button_yellow" && name == "ui_kn_button_blue"));
                if (!replaceable) return;
                var found = Find(name);
                if (found != null) slot = found;
            }
            Fill(ref s.row, "ui_kn_button_grey");
            Fill(ref s.rowChosen, "ui_kn_button_blue");
            Fill(ref s.frame, "ui_kn_frame");
            Fill(ref s.frameGood, "ui_kn_frame_good");
            Fill(ref s.frameBad, "ui_kn_frame_bad");
            Fill(ref s.buttonMain, "ui_kn_button_blue");
            Fill(ref s.buttonSecondary, "ui_kn_button_blue");
            Fill(ref s.buttonGo, "ui_kn_button_green");
            Fill(ref s.arrowLeft, "ui_kn_arrow_left");
            Fill(ref s.arrowRight, "ui_kn_arrow_right");
            Fill(ref s.dotOn, "ui_kn_dot_on");
            Fill(ref s.dotOff, "ui_kn_dot_off");

            // One-time: switch an existing skin asset to the palette colours (its old colours were saved in the asset).
            if (s.paletteVersion < 1)
            {
                var fresh = ScriptableObject.CreateInstance<UiSkin>();
                s.backgroundColor = fresh.backgroundColor; s.panelColor = fresh.panelColor; s.chosenColor = fresh.chosenColor;
                s.mainColor = fresh.mainColor; s.secondaryColor = fresh.secondaryColor; s.goColor = fresh.goColor;
                s.textColor = fresh.textColor; s.correctTextColor = fresh.correctTextColor; s.wrongTextColor = fresh.wrongTextColor;
                s.paletteVersion = 1;
                Object.DestroyImmediate(fresh);
            }

            if (s.font == null) s.font = FindFont("Londrina", "Black", "Bold", "Regular");
            if (s.bodyFont == null) s.bodyFont = FindFont("Exo2", "SemiBold", "Medium", "Regular");

            EditorUtility.SetDirty(s);
            AssetDatabase.SaveAssets();
            if (missing.Count > 0)
                Debug.LogWarning("UI Skin: these sprites were not found in " + SpriteFolder + " (those parts use the plain built-in shapes): " + string.Join(", ", missing));
            Debug.Log("UI Skin: font = " + (s.font != null ? s.font.name : "none") + ", body font = " + (s.bodyFont != null ? s.bodyFont.name : "none"));
            return s;
        }

        /// <summary>Finds a TMP font asset whose name contains the family, preferring the first weight word that matches.</summary>
        private static TMP_FontAsset FindFont(string family, params string[] preferredWeights)
        {
            var found = new System.Collections.Generic.List<TMP_FontAsset>();
            foreach (var guid in AssetDatabase.FindAssets("t:TMP_FontAsset"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null && asset.name.Replace(" ", "").Replace("_", "").ToLowerInvariant().Contains(family.ToLowerInvariant())) found.Add(asset);
            }
            foreach (var weight in preferredWeights)
                foreach (var asset in found)
                    if (asset.name.ToLowerInvariant().Contains(weight.ToLowerInvariant())) return asset;
            return found.Count > 0 ? found[0] : null;
        }

        /// <summary>
        /// Adds the search box (and a "No signs match" message) to the voting panel, above the word list, and moves the list down
        /// to make room. Used by the builder, and by "Sign Racers > Add Search To Voting Screen" (which edits the existing prefab).
        /// </summary>
        internal static TMP_InputField AddSearchBar(RectTransform vote, RectTransform scrollRect, UiSkin skin, Sprite panel, out TMP_Text noMatch)
        {
            var bar = RaceHudBuilder.MakeRect("SearchBar", vote);
            RaceHudBuilder.PinTop(bar, -235, 940, 100);
            var back = bar.gameObject.AddComponent<Image>();
            back.sprite = Pick(skin.frame, panel); back.type = Image.Type.Sliced;
            back.color = skin.frame != null ? Color.white : new Color(1f, 1f, 1f, 0.15f);

            var area = RaceHudBuilder.MakeRect("TextArea", bar);
            RaceHudBuilder.Stretch(area);
            area.offsetMin = new Vector2(34, 10); area.offsetMax = new Vector2(-34, -10);
            area.gameObject.AddComponent<RectMask2D>();

            var font = skin.bodyFont != null ? skin.bodyFont : skin.font;
            var placeholder = RaceHudBuilder.MakeText("Placeholder", area, "Search signs...", 46, TextAlignmentOptions.Left, new Color(skin.textColor.r, skin.textColor.g, skin.textColor.b, 0.45f));
            RaceHudBuilder.Stretch(placeholder.rectTransform);
            placeholder.fontStyle = FontStyles.Italic;
            var text = RaceHudBuilder.MakeText("Text", area, "", 46, TextAlignmentOptions.Left, skin.textColor);
            RaceHudBuilder.Stretch(text.rectTransform);
            if (font != null) { placeholder.font = font; text.font = font; }

            var input = bar.gameObject.AddComponent<TMP_InputField>();
            input.targetGraphic = back;
            input.textViewport = area;
            input.textComponent = text;
            input.placeholder = placeholder;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.characterLimit = 20;
            input.caretColor = skin.textColor;
            input.selectionColor = new Color(0.23f, 0.63f, 0.88f, 0.4f);

            // Make room: the list starts below the search box.
            scrollRect.offsetMax = new Vector2(scrollRect.offsetMax.x, -350);

            noMatch = RaceHudBuilder.MakeText("NoMatchText", vote, "No signs match", 46, TextAlignmentOptions.Center, skin.textColor);
            RaceHudBuilder.PinTop(noMatch.rectTransform, -420, 900, 80);
            if (font != null) noMatch.font = font;
            noMatch.gameObject.SetActive(false);
            return input;
        }

        internal static Sprite Pick(Sprite custom, Sprite fallback) => custom != null ? custom : fallback;

        /// <summary>If the skin has an arrow icon, draw it on the button and hide the < or > text.</summary>
        internal static void AddArrowIcon(Button button, Sprite icon)
        {
            if (icon == null) return;
            var label = button.GetComponentInChildren<TMP_Text>();
            if (label != null) label.text = "";
            var rect = RaceHudBuilder.MakeRect("Icon", button.transform);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(64, 64);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = icon; image.preserveAspect = true; image.raycastTarget = false;
            rect.sizeDelta = new Vector2(96, 96);

            // Arrow only: hide the button's own background and let the arrow itself react to presses / disabled.
            var background = button.GetComponent<Image>();
            if (background != null) background.color = new Color(1f, 1f, 1f, 0f); // still catches taps
            button.targetGraphic = image;
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
