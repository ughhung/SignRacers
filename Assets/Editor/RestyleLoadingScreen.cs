#if UNITY_EDITOR
using SignRacers.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace SignRacers.EditorTools
{
    /// <summary>
    /// Restyles the loading cover (LoadingCanvas prefab) with the UI skin: gold background, the game's fonts, dark text,
    /// and a "SIGN RACERS" title above the status line. Edits the prefab in place, so your hand edits stay; it is safe to run again.
    /// Sign Racers > Restyle Loading Screen.
    /// </summary>
    public static class RestyleLoadingScreen
    {
        private const string PrefabPath = "Assets/Prefabs/UI/LoadingCanvas.prefab";

        [MenuItem("Sign Racers/Restyle Loading Screen")]
        public static void Run()
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var view = root.GetComponent<LoadingView>();
                if (view == null) { Debug.LogError("No LoadingView on " + PrefabPath + "."); return; }
                UiSkin skin = VotingBuilder.EnsureSkin();
                var body = skin.bodyFont != null ? skin.bodyFont : skin.font;

                // Background.
                var bgTransform = root.transform.Find("Background");
                var bg = bgTransform != null ? bgTransform.GetComponent<Image>() : null;
                if (bg != null)
                {
                    bg.sprite = skin.background;
                    bg.type = Image.Type.Simple;
                    bg.color = skin.background != null && !skin.tintSprites ? Color.white : skin.backgroundColor;
                }

                var so = new SerializedObject(view);
                var status = so.FindProperty("statusText").objectReferenceValue as TMP_Text;
                var detail = so.FindProperty("detailText").objectReferenceValue as TMP_Text;

                if (status != null)
                {
                    if (skin.font != null) status.font = skin.font;
                    status.color = skin.textColor;
                    status.fontSize = 90;
                    status.alignment = TextAlignmentOptions.Center;
                    status.rectTransform.sizeDelta = new Vector2(900, 130);
                }
                if (detail != null)
                {
                    if (body != null) detail.font = body;
                    detail.color = new Color(skin.textColor.r, skin.textColor.g, skin.textColor.b, 0.75f);
                    detail.fontSize = 48;
                }

                // Title above the status line.
                var existing = root.transform.Find("TitleText");
                if (existing == null)
                {
                    var go = new GameObject("TitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
                    go.transform.SetParent(root.transform, false);
                    var rect = (RectTransform)go.transform;
                    rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                    rect.anchoredPosition = new Vector2(0, 330);
                    rect.sizeDelta = new Vector2(1000, 220);
                    var title = go.GetComponent<TextMeshProUGUI>();
                    title.text = "SIGN RACERS";
                    title.fontSize = 150;
                    title.fontStyle = FontStyles.Bold;
                    title.alignment = TextAlignmentOptions.Center;
                    title.enableAutoSizing = true; title.fontSizeMax = 150; title.fontSizeMin = 60;
                    title.raycastTarget = false;
                    existing = go.transform;
                }
                var titleText = existing.GetComponent<TMP_Text>();
                if (titleText != null)
                {
                    if (skin.font != null) titleText.font = skin.font;
                    titleText.color = skin.textColor;
                }

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log("Loading screen restyled.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
#endif
