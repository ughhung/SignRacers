#if UNITY_EDITOR
using SignRacers.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace SignRacers.EditorTools
{
    /// <summary>
    /// Restyles the end-of-race screen (RaceResultsCanvas prefab) with the UI skin: a gold panel, the game's fonts, dark text,
    /// a row behind each finisher (blue for you, grey for the others), a green RACE AGAIN / NEW GAME button and a grey MENU button.
    /// Edits the prefab in place, so your other hand edits stay; it is safe to run again.
    /// Sign Racers > Restyle Results Screen.
    /// </summary>
    public static class RestyleResultsScreen
    {
        [MenuItem("Sign Racers/Restyle Results Screen")]
        public static void Run()
        {
            string path = null;
            foreach (var guid in AssetDatabase.FindAssets("RaceResultsCanvas t:Prefab"))
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(p) == "RaceResultsCanvas") { path = p; break; }
            }
            if (path == null) { Debug.LogError("Couldn't find a RaceResultsCanvas prefab."); return; }

            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var view = root.GetComponent<ResultsHud>();
                if (view == null) { Debug.LogError("No ResultsHud on " + path + "."); return; }
                UiSkin skin = VotingBuilder.EnsureSkin();
                var body = skin.bodyFont != null ? skin.bodyFont : skin.font;
                var so = new SerializedObject(view);

                // Panel: opaque gold.
                var panel = root.transform.Find("SafeArea/Panel");
                var panelImage = panel != null ? panel.GetComponent<Image>() : null;
                if (panelImage != null)
                {
                    if (skin.card != null) { panelImage.sprite = skin.card; panelImage.type = Image.Type.Sliced; }
                    panelImage.color = skin.card != null && !skin.tintSprites ? Color.white : skin.backgroundColor;
                }

                Style(so.FindProperty("title").objectReferenceValue as TMP_Text, skin.font, skin.textColor);
                Style(so.FindProperty("placeText").objectReferenceValue as TMP_Text, skin.font, skin.textColor);
                Style(so.FindProperty("statusText").objectReferenceValue as TMP_Text, body, new Color(skin.textColor.r, skin.textColor.g, skin.textColor.b, 0.75f));

                // One row behind each finisher.
                var rows = so.FindProperty("rows");
                var names = so.FindProperty("nameTexts");
                var times = so.FindProperty("timeTexts");
                for (int i = 0; i < rows.arraySize; i++)
                {
                    var row = rows.GetArrayElementAtIndex(i).objectReferenceValue as GameObject;
                    if (row == null) continue;
                    var image = row.GetComponent<Image>() ?? row.AddComponent<Image>();
                    image.sprite = skin.row;
                    image.type = skin.row != null ? Image.Type.Sliced : Image.Type.Simple;
                    image.color = skin.RowColor;
                    image.raycastTarget = false;

                    var name = i < names.arraySize ? names.GetArrayElementAtIndex(i).objectReferenceValue as TMP_Text : null;
                    var time = i < times.arraySize ? times.GetArrayElementAtIndex(i).objectReferenceValue as TMP_Text : null;
                    if (name != null) { Style(name, skin.font, skin.textColor); name.rectTransform.offsetMin = new Vector2(30, 0); name.rectTransform.offsetMax = new Vector2(-250, 0); }
                    if (time != null) { Style(time, skin.font, skin.textColor); time.rectTransform.offsetMin = new Vector2(250, 0); time.rectTransform.offsetMax = new Vector2(-30, 0); }
                }

                // Buttons.
                StyleButton(so.FindProperty("againButton").objectReferenceValue as Button, skin.buttonGo, skin.Tint(skin.buttonGo, skin.goColor), Color.white, skin);
                StyleButton(so.FindProperty("menuButton").objectReferenceValue as Button, skin.row, skin.RowColor, skin.textColor, skin);

                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log("Results screen restyled (" + path + ").");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void Style(TMP_Text text, TMP_FontAsset font, Color color)
        {
            if (text == null) return;
            if (font != null) text.font = font;
            text.color = color;
        }

        private static void StyleButton(Button button, Sprite sprite, Color color, Color textColor, UiSkin skin)
        {
            if (button == null) return;
            var image = button.targetGraphic as Image;
            if (image != null)
            {
                if (sprite != null) { image.sprite = sprite; image.type = Image.Type.Sliced; }
                image.color = color;
            }
            foreach (var label in button.GetComponentsInChildren<TMP_Text>(true))
            {
                if (skin.font != null) label.font = skin.font;
                label.color = textColor;
            }
        }
    }
}
#endif
