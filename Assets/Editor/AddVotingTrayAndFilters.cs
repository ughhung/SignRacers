#if UNITY_EDITOR
using SignRacers.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace SignRacers.EditorTools
{
    /// <summary>
    /// Makes the VOTING screen look and work like the Solo picker: a tray of 5 slots showing your votes (tap one to remove it),
    /// the search box, a row of filter chips (Animals, Food, ...), and the word list, all lined up with the same side margins.
    /// It edits the existing VotingCanvas prefab in place, so your hand edits (like the reference video's Autoplay and Loop) stay.
    /// Needs Assets/Resources/SignCategories.txt (the same file the Solo picker uses).
    /// Sign Racers > Add Tray And Filters To Voting Screen.
    /// </summary>
    public static class AddVotingTrayAndFilters
    {
        private const string PrefabPath = "Assets/Prefabs/UI/VotingCanvas.prefab";
        private const float Side = 60f;

        [MenuItem("Sign Racers/Add Tray And Filters To Voting Screen")]
        public static void Run()
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var view = root.GetComponent<VotingView>();
                var vote = root.transform.Find("SafeArea/VotePanel") as RectTransform;
                var scroll = vote != null ? vote.Find("WordScroll") as RectTransform : null;
                var searchBar = vote != null ? vote.Find("SearchBar") as RectTransform : null;
                if (view == null || vote == null || scroll == null)
                {
                    Debug.LogError("Couldn't find SafeArea/VotePanel/WordScroll in " + PrefabPath + ".");
                    return;
                }
                if (searchBar == null)
                {
                    Debug.LogError("The voting screen has no search box yet. Run Sign Racers > Add Search To Voting Screen first.");
                    return;
                }
                if (vote.Find("Tray") != null)
                {
                    Debug.Log("The voting screen already has the tray and filters. Nothing changed.");
                    return;
                }

                UiSkin skin = VotingBuilder.EnsureSkin();
                var body = skin.bodyFont != null ? skin.bodyFont : skin.font;

                // ---- layout (distance from the top): title, info, tray, search, chips, list ----
                StretchTop(searchBar, 350, 100);

                // Tray of 5 slots.
                var tray = NewRect("Tray", vote);
                StretchTop(tray, 235, 100);
                var trayLayout = tray.gameObject.AddComponent<HorizontalLayoutGroup>();
                trayLayout.spacing = 10;
                trayLayout.childControlWidth = true; trayLayout.childControlHeight = true;
                trayLayout.childForceExpandWidth = true; trayLayout.childForceExpandHeight = true;
                var slots = new Button[5];
                for (int i = 0; i < slots.Length; i++)
                    slots[i] = MakeButton("TraySlot" + (i + 1), tray, skin.rowChosen != null ? skin.rowChosen : skin.row, skin.RowChosenColor, "empty", 36, Color.white, skin.font);

                // Filter chips (scroll sideways).
                var chipNames = new System.Collections.Generic.List<string> { "All" };
                var catFile = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Resources/SignCategories.txt");
                if (catFile != null)
                    foreach (string raw in catFile.text.Split('\n'))
                        if (raw.StartsWith("#chips:"))
                            foreach (string c in raw.Substring(7).Split(',')) if (c.Trim().Length > 0) chipNames.Add(c.Trim());
                if (catFile == null) Debug.LogWarning("Assets/Resources/SignCategories.txt not found: only the 'All' chip was added.");

                var chipRoot = NewRect("FilterChips", vote);
                StretchTop(chipRoot, 465, 84);
                var chipScroll = chipRoot.gameObject.AddComponent<ScrollRect>();
                var hit = chipRoot.gameObject.AddComponent<Image>(); hit.color = new Color(0, 0, 0, 0);
                var viewport = NewRect("Viewport", chipRoot);
                viewport.anchorMin = Vector2.zero; viewport.anchorMax = Vector2.one; viewport.offsetMin = viewport.offsetMax = Vector2.zero;
                viewport.gameObject.AddComponent<RectMask2D>();
                var content = NewRect("Content", viewport);
                content.anchorMin = new Vector2(0, 0); content.anchorMax = new Vector2(0, 1); content.pivot = new Vector2(0, 0.5f);
                content.offsetMin = content.offsetMax = Vector2.zero;
                var chipLayout = content.gameObject.AddComponent<HorizontalLayoutGroup>();
                chipLayout.spacing = 12; chipLayout.padding = new RectOffset(4, 4, 4, 4);
                chipLayout.childControlWidth = true; chipLayout.childControlHeight = true;
                chipLayout.childForceExpandWidth = false; chipLayout.childForceExpandHeight = true;
                content.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                chipScroll.viewport = viewport; chipScroll.content = content;
                chipScroll.horizontal = true; chipScroll.vertical = false;
                chipScroll.movementType = ScrollRect.MovementType.Elastic;

                var chips = new Button[chipNames.Count];
                for (int i = 0; i < chips.Length; i++)
                {
                    chips[i] = MakeButton("Chip_" + chipNames[i], content, skin.row, skin.RowColor, chipNames[i], 38, skin.textColor, skin.font);
                    var size = chips[i].gameObject.AddComponent<LayoutElement>();
                    size.minWidth = size.preferredWidth = Mathf.Max(130, 60 + 22 * chipNames[i].Length);
                }

                // The word list: same side margins, below the chips.
                scroll.offsetMin = new Vector2(Side, scroll.offsetMin.y);
                scroll.offsetMax = new Vector2(-Side, -575);

                // "No signs match" under the chips.
                var noMatch = vote.Find("NoMatchText") as RectTransform;
                if (noMatch != null) noMatch.anchoredPosition = new Vector2(noMatch.anchoredPosition.x, -640);

                // Tidy the title and info line a little for the new layout.
                var info = vote.Find("InfoText") as RectTransform;
                if (info != null) info.anchoredPosition = new Vector2(info.anchoredPosition.x, -150);

                // ---- wire ----
                var so = new SerializedObject(view);
                var slotsProp = so.FindProperty("traySlots");
                slotsProp.arraySize = slots.Length;
                for (int i = 0; i < slots.Length; i++) slotsProp.GetArrayElementAtIndex(i).objectReferenceValue = slots[i];
                var chipsProp = so.FindProperty("filterChips");
                chipsProp.arraySize = chips.Length;
                for (int i = 0; i < chips.Length; i++) chipsProp.GetArrayElementAtIndex(i).objectReferenceValue = chips[i];
                so.ApplyModifiedPropertiesWithoutUndo();

                // Keep the search box and chips above the list in the hierarchy order too (draw order doesn't matter, they don't overlap).
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log("Voting screen: tray and filter chips added.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        /// <summary>Full width minus the side margins, pinned a distance from the top.</summary>
        private static void StretchTop(RectTransform r, float y, float height)
        {
            r.anchorMin = new Vector2(0f, 1f); r.anchorMax = new Vector2(1f, 1f);
            r.pivot = new Vector2(0.5f, 1f);
            r.offsetMin = new Vector2(Side, 0); r.offsetMax = new Vector2(-Side, 0);
            r.sizeDelta = new Vector2(-2f * Side, height);
            r.anchoredPosition = new Vector2(0, -y);
        }

        private static Button MakeButton(string name, RectTransform parent, Sprite sprite, Color color, string label, float size, Color textColor, TMP_FontAsset font)
        {
            var r = NewRect(name, parent);
            var img = r.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            img.color = color;
            var b = r.gameObject.AddComponent<Button>();
            b.targetGraphic = img;

            var t = NewRect("Label", r);
            t.anchorMin = Vector2.zero; t.anchorMax = Vector2.one;
            t.offsetMin = new Vector2(20, 8); t.offsetMax = new Vector2(-20, -8);
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
    }
}
#endif
