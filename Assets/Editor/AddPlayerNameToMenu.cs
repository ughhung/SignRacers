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
    /// Adds the player's name to the top of the main menu's Home screen, by copying the Solo button. Tapping it rolls a new
    /// "colour + animal" name. Edits the scene in place (your hand edits stay).
    /// </summary>
    public static class AddPlayerNameToMenu
    {
        private const string ScenePath = "Assets/Scenes/MainMenu.unity";

        [MenuItem("Sign Racers/Add Player Name To Menu")]
        public static void Run()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var menu = Object.FindFirstObjectByType<MenuController>();
            if (menu == null) { Debug.LogError("No MenuController in " + ScenePath); return; }

            var so = new SerializedObject(menu);
            if (so.FindProperty("nameButton").objectReferenceValue != null) { Debug.Log("Name button already exists."); return; }

            var solo = so.FindProperty("soloButton").objectReferenceValue as Button;
            if (solo == null) { Debug.LogError("MenuController.soloButton is not assigned."); return; }

            var copy = Object.Instantiate(solo.gameObject, solo.transform.parent);
            copy.name = "NameButton";
            copy.transform.SetSiblingIndex(0);
            var label = copy.GetComponentInChildren<TMP_Text>(true);
            if (label != null) { label.text = "Blue Fox  (tap to change)"; label.fontSize *= 0.7f; }
            var button = copy.GetComponent<Button>();
            button.onClick = new Button.ButtonClickedEvent();
            var layout = copy.GetComponent<LayoutElement>();
            if (layout != null) { layout.preferredHeight *= 0.7f; layout.minHeight *= 0.7f; }

            so.FindProperty("nameButton").objectReferenceValue = button;
            so.ApplyModifiedPropertiesWithoutUndo();

            Undo.RegisterCreatedObjectUndo(copy, "Add Player Name");
            EditorSceneManager.MarkSceneDirty(menu.gameObject.scene);
            EditorSceneManager.SaveScene(menu.gameObject.scene);
            Debug.Log("Name button added at the top of the Home screen.");
        }
    }
}
#endif
