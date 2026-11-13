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
    /// Adds the "Browse public games" screen to the main menu: a "Browse public games" button on the Multiplayer screen,
    /// and a new screen with a scrolling list, a Refresh button and a Back button. It is made by copying your existing
    /// Multiplayer screen, so it uses the same look. Edits the scene in place (your hand edits stay).
    /// Run it once, after "Add Quick Play Button To Menu".
    /// </summary>
    public static class AddBrowseGamesScreen
    {
        private const string ScenePath = "Assets/Scenes/MainMenu.unity";

        [MenuItem("Sign Racers/Add Browse Games Screen To Menu")]
        public static void Run()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var menu = Object.FindFirstObjectByType<MenuController>();
            if (menu == null) { Debug.LogError("No MenuController in " + ScenePath); return; }

            var so = new SerializedObject(menu);
            if (so.FindProperty("browsePanel").objectReferenceValue != null) { Debug.Log("Browse screen already exists."); return; }

            var multiplayerPanel = so.FindProperty("multiplayerPanel").objectReferenceValue as GameObject;
            var create = so.FindProperty("createButton").objectReferenceValue as Button;
            var back = so.FindProperty("multiplayerBackButton").objectReferenceValue as Button;
            if (multiplayerPanel == null || create == null || back == null)
            {
                Debug.LogError("MenuController needs multiplayerPanel, createButton and multiplayerBackButton assigned.");
                return;
            }

            // 1) The button on the Multiplayer screen (above Create).
            var browseButtonObj = Object.Instantiate(create.gameObject, create.transform.parent);
            browseButtonObj.name = "BrowseButton";
            browseButtonObj.transform.SetSiblingIndex(create.transform.GetSiblingIndex());
            SetLabel(browseButtonObj, "Browse public games");
            var browseButton = browseButtonObj.GetComponent<Button>();
            browseButton.onClick = new Button.ButtonClickedEvent();

            // 2) The new screen: a copy of the Multiplayer screen with its buttons removed.
            var panel = Object.Instantiate(multiplayerPanel, multiplayerPanel.transform.parent);
            panel.name = "BrowsePanel";
            panel.SetActive(true);

            Button rowTemplate = null, backBtn = null;
            foreach (var b in panel.GetComponentsInChildren<Button>(true))
            {
                if (b.name == create.name && rowTemplate == null) rowTemplate = b;
                else if (b.name == back.name && backBtn == null) backBtn = b;
            }
            foreach (var b in panel.GetComponentsInChildren<Button>(true))
                if (b != rowTemplate && b != backBtn) Object.DestroyImmediate(b.gameObject);
            if (rowTemplate == null || backBtn == null) { Debug.LogError("Could not build the browse screen from the Multiplayer screen."); return; }

            rowTemplate.onClick = new Button.ButtonClickedEvent();
            backBtn.onClick = new Button.ButtonClickedEvent();
            rowTemplate.name = "RowTemplate";
            var rowLayout = rowTemplate.GetComponent<LayoutElement>() ?? rowTemplate.gameObject.AddComponent<LayoutElement>();
            rowLayout.minHeight = 110; rowLayout.preferredHeight = 110;

            // A refresh button: a copy of Back.
            var refreshObj = Object.Instantiate(backBtn.gameObject, backBtn.transform.parent);
            refreshObj.name = "RefreshButton";
            refreshObj.transform.SetSiblingIndex(backBtn.transform.GetSiblingIndex());
            SetLabel(refreshObj, "Refresh");
            var refresh = refreshObj.GetComponent<Button>();
            refresh.onClick = new Button.ButtonClickedEvent();
            SetLabel(backBtn.gameObject, "Back");

            // Title (any existing non-button text on the copied screen) and a status line.
            TMP_Text title = null;
            foreach (var t in panel.GetComponentsInChildren<TMP_Text>(true))
                if (t.GetComponentInParent<Button>(true) == null) { title = t; break; }
            if (title != null) title.text = "Public games";

            var labelSource = rowTemplate.GetComponentInChildren<TMP_Text>(true);
            var statusObj = new GameObject("StatusText", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            statusObj.transform.SetParent(panel.transform, false);
            var status = statusObj.GetComponent<TextMeshProUGUI>();
            if (labelSource != null) { status.font = labelSource.font; status.color = labelSource.color; }
            status.fontSize = 40; status.alignment = TextAlignmentOptions.Center; status.text = "";
            var statusLayout = statusObj.GetComponent<LayoutElement>(); statusLayout.minHeight = 120; statusLayout.preferredHeight = 120;

            // The scrolling list.
            var scrollObj = new GameObject("RoomList", typeof(RectTransform), typeof(ScrollRect), typeof(LayoutElement));
            scrollObj.transform.SetParent(panel.transform, false);
            var scrollLayout = scrollObj.GetComponent<LayoutElement>();
            scrollLayout.flexibleHeight = 1; scrollLayout.minHeight = 500; scrollLayout.preferredHeight = 800;

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewport.transform.SetParent(scrollObj.transform, false);
            var vpRect = (RectTransform)viewport.transform;
            vpRect.anchorMin = Vector2.zero; vpRect.anchorMax = Vector2.one; vpRect.offsetMin = vpRect.offsetMax = Vector2.zero;

            var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            var contentRect = (RectTransform)content.transform;
            contentRect.anchorMin = new Vector2(0, 1); contentRect.anchorMax = new Vector2(1, 1); contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.offsetMin = contentRect.offsetMax = Vector2.zero;
            var vlg = content.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 16; vlg.childControlWidth = true; vlg.childControlHeight = true; vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = scrollObj.GetComponent<ScrollRect>();
            scroll.viewport = vpRect; scroll.content = contentRect; scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;

            // Order on screen: title, status, list, Refresh, Back.
            scrollObj.transform.SetSiblingIndex(title != null ? title.transform.GetSiblingIndex() + 1 : 0);
            statusObj.transform.SetSiblingIndex(scrollObj.transform.GetSiblingIndex());
            rowTemplate.transform.SetParent(contentRect, false); // the template lives inside the list; hidden at runtime

            // 3) Wire everything.
            var browser = panel.AddComponent<PublicGameBrowser>();
            var bso = new SerializedObject(browser);
            bso.FindProperty("listContent").objectReferenceValue = contentRect;
            bso.FindProperty("rowTemplate").objectReferenceValue = rowTemplate;
            bso.FindProperty("statusText").objectReferenceValue = status;
            bso.FindProperty("refreshButton").objectReferenceValue = refresh;
            bso.ApplyModifiedPropertiesWithoutUndo();

            so.FindProperty("browseButton").objectReferenceValue = browseButton;
            so.FindProperty("browsePanel").objectReferenceValue = panel;
            so.FindProperty("browser").objectReferenceValue = browser;
            so.FindProperty("browseBackButton").objectReferenceValue = backBtn;
            so.ApplyModifiedPropertiesWithoutUndo();

            panel.SetActive(false);
            EditorSceneManager.MarkSceneDirty(menu.gameObject.scene);
            EditorSceneManager.SaveScene(menu.gameObject.scene);
            Debug.Log("Browse public games screen added. Restyle it in the editor if you like (the screen is BrowsePanel).");
        }

        private static void SetLabel(GameObject button, string text)
        {
            var label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.text = text;
        }
    }
}
#endif
