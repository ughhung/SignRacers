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
    /// Adds a "Quick play" button to the main menu's Multiplayer screen by copying the existing
    /// Create button, and wires it to MenuController. Edits the scene in place (your hand edits stay).
    /// </summary>
    public static class AddQuickPlayButton
    {
        private const string ScenePath = "Assets/Scenes/MainMenu.unity";

        [MenuItem("Sign Racers/Add Quick Play Button To Menu")]
        public static void Run()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var menu = Object.FindFirstObjectByType<MenuController>();
            if (menu == null) { Debug.LogError("No MenuController in " + ScenePath); return; }

            var so = new SerializedObject(menu);
            var existing = so.FindProperty("quickPlayButton").objectReferenceValue as Button;
            if (existing != null) { Debug.Log("Quick play button already exists."); return; }

            var create = so.FindProperty("createButton").objectReferenceValue as Button;
            if (create == null) { Debug.LogError("MenuController.createButton is not assigned."); return; }

            var copy = Object.Instantiate(create.gameObject, create.transform.parent);
            copy.name = "QuickPlayButton";
            copy.transform.SetSiblingIndex(create.transform.GetSiblingIndex()); // sits above Create
            var label = copy.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.text = "Quick play";
            var button = copy.GetComponent<Button>();
            button.onClick = new Button.ButtonClickedEvent(); // drop any copied listeners

            so.FindProperty("quickPlayButton").objectReferenceValue = button;
            so.ApplyModifiedPropertiesWithoutUndo();

            Undo.RegisterCreatedObjectUndo(copy, "Add Quick Play Button");
            EditorSceneManager.MarkSceneDirty(menu.gameObject.scene);
            EditorSceneManager.SaveScene(menu.gameObject.scene);
            Debug.Log("Quick play button added to the Multiplayer screen.");
        }
    }
}
#endif
