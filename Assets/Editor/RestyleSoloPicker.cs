#if UNITY_EDITOR
using SignRacers.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace SignRacers.EditorTools
{
    /// <summary>
    /// Restyles the Solo word picker in the main menu with the UI skin (the same Kenney look as the voting and lobby screens):
    /// gold background, grey word rows (blue when picked), a framed search box, and blue / green / grey buttons.
    /// It changes looks only: positions and sizes are left as you have them. Edits the scene in place.
    /// Sign Racers > Restyle Solo Picker.
    /// </summary>
    public static class RestyleSoloPicker
    {
        private const string ScenePath = "Assets/Scenes/MainMenu.unity";

        [MenuItem("Sign Racers/Restyle Solo Picker")]
        public static void Run()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var menu = Object.FindFirstObjectByType<MenuController>();
            if (menu == null) { Debug.LogError("No MenuController in " + ScenePath); return; }

            UiSkin skin = VotingBuilder.EnsureSkin();
            var so = new SerializedObject(menu);
            var panel = so.FindProperty("pickerPanel").objectReferenceValue as GameObject;
            if (panel == null) { Debug.LogError("MenuController.pickerPanel is not assigned."); return; }

            // Background.
            var back = panel.GetComponent<Image>();
            if (back == null) back = panel.AddComponent<Image>();
            back.sprite = skin.background;
            back.type = Image.Type.Simple;
            back.color = skin.background != null && !skin.tintSprites ? Color.white : skin.backgroundColor;

            // Fonts and colours on all text. Text on buttons stays white.
            foreach (var label in panel.GetComponentsInChildren<TMP_Text>(true))
            {
                bool small = label.GetComponentInParent<Button>(true) == null && label.fontSize <= 50;
                var font = small && skin.bodyFont != null ? skin.bodyFont : skin.font;
                if (font != null) label.font = font;
                if (label.GetComponentInParent<Button>(true) == null && label.GetComponentInParent<TMP_InputField>(true) == null)
                    label.color = skin.textColor;
            }

            // Word rows (the hidden template is copied once per word when the menu starts).
            var template = so.FindProperty("wordButtonTemplate").objectReferenceValue as Button;
            if (template != null)
            {
                StyleButton(template, skin.row, skin.RowColor, skin);
                foreach (var label in template.GetComponentsInChildren<TMP_Text>(true))
                {
                    label.color = skin.textColor; // dark text on the light grey row
                    var font = skin.font; if (font != null) label.font = font;
                }
            }

            // Buttons.
            StyleButton(so.FindProperty("startButton").objectReferenceValue as Button, skin.buttonGo, skin.Tint(skin.buttonGo, skin.goColor), skin);
            StyleButton(so.FindProperty("randomButton").objectReferenceValue as Button, skin.buttonMain, skin.Tint(skin.buttonMain, skin.mainColor), skin);
            var backButton = so.FindProperty("backButton").objectReferenceValue as Button;
            StyleButton(backButton, skin.row, skin.RowColor, skin);
            if (backButton != null)
                foreach (var label in backButton.GetComponentsInChildren<TMP_Text>(true)) label.color = skin.textColor;

            // Search box: framed, dark text.
            var search = so.FindProperty("searchField").objectReferenceValue as TMP_InputField;
            if (search != null)
            {
                var image = search.GetComponent<Image>();
                if (image != null)
                {
                    if (skin.frame != null) image.sprite = skin.frame;
                    image.type = Image.Type.Sliced;
                    image.color = Color.white;
                }
                if (search.textComponent != null) { search.textComponent.color = skin.textColor; if (skin.bodyFont != null) search.textComponent.font = skin.bodyFont; }
                if (search.placeholder is TMP_Text ph)
                {
                    ph.color = new Color(skin.textColor.r, skin.textColor.g, skin.textColor.b, 0.45f);
                    if (skin.bodyFont != null) ph.font = skin.bodyFont;
                }
                search.caretColor = skin.textColor;
            }

            // Sizes: the word list takes all the spare room; the title, counter, search box and buttons get fixed heights.
            var panelRect = (RectTransform)panel.transform;
            var group = panel.GetComponent<VerticalLayoutGroup>();
            if (group != null)
            {
                group.childControlHeight = true; group.childControlWidth = true;
                group.childForceExpandHeight = false; group.childForceExpandWidth = true;
            }
            var scroll = panel.GetComponentInChildren<ScrollRect>(true);
            if (scroll != null) SizeRow(Direct(panelRect, scroll.transform), 400, 0, 1);
            if (search != null) SizeRow(Direct(panelRect, search.transform), 110, 110, 0);
            SizeRow(Direct(panelRect, (so.FindProperty("counterText").objectReferenceValue as Component)?.transform), 60, 60, 0);
            SizeRow(Direct(panelRect, (so.FindProperty("startButton").objectReferenceValue as Component)?.transform), 130, 130, 0);
            SizeRow(Direct(panelRect, (so.FindProperty("randomButton").objectReferenceValue as Component)?.transform), 130, 130, 0);
            SizeRow(Direct(panelRect, backButton != null ? backButton.transform : null), 100, 100, 0);
            if (template != null)
            {
                var le = template.GetComponent<LayoutElement>() ?? template.gameObject.AddComponent<LayoutElement>();
                le.minHeight = 100; le.preferredHeight = 100; le.flexibleHeight = 0;
            }

            EditorSceneManager.MarkSceneDirty(menu.gameObject.scene);
            EditorSceneManager.SaveScene(menu.gameObject.scene);
            Debug.Log("Solo picker restyled and resized. You can run this again any time.");
        }

        /// <summary>The child of the panel that contains this object (or the object itself if it is a direct child).</summary>
        private static Transform Direct(Transform panel, Transform item)
        {
            if (item == null) return null;
            while (item.parent != null && item.parent != panel) item = item.parent;
            return item.parent == panel ? item : null;
        }

        private static void SizeRow(Transform row, float min, float preferred, float flexible)
        {
            if (row == null) return;
            var le = row.GetComponent<LayoutElement>() ?? row.gameObject.AddComponent<LayoutElement>();
            le.minHeight = min;
            le.preferredHeight = flexible > 0 ? -1 : preferred;
            le.flexibleHeight = flexible;
        }

        private static void StyleButton(Button button, Sprite sprite, Color color, UiSkin skin)
        {
            if (button == null) return;
            var image = button.targetGraphic as Image;
            if (image == null) return;
            if (sprite != null) { image.sprite = sprite; image.type = Image.Type.Sliced; }
            image.color = color;
            foreach (var label in button.GetComponentsInChildren<TMP_Text>(true))
                if (skin.font != null) label.font = skin.font;
        }
    }
}
#endif
