using SignRacers.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace SignRacers.EditorTools
{
    /// <summary>
    /// One click: builds the loading cover as a prefab (Assets/Prefabs/UI/LoadingCanvas.prefab) and puts a copy
    /// in the open scene if there isn't one. Open the Game scene first. Sign Racers > Build Loading Prefab.
    /// NOTE: running it again REPLACES the prefab, so hand edits to the prefab would be lost.
    /// </summary>
    public static class LoadingBuilder
    {
        private const string PrefabPath = "Assets/Prefabs/UI/LoadingCanvas.prefab";

        [MenuItem("Sign Racers/Build Loading Prefab")]
        public static void Build()
        {
            if (EditorSceneManager.GetActiveScene().name != "Game"
                && !EditorUtility.DisplayDialog("Open the Game scene first",
                    "The open scene is '" + EditorSceneManager.GetActiveScene().name + "', not 'Game'. The screen would be put in that scene, " +
                    "and it would disappear whenever the track scene is swapped. Cancel, open the Game scene, and run this again.",
                    "Build anyway", "Cancel"))
                return;

            RaceHudBuilder.EnsureFolder("Assets/Prefabs/UI");

            var root = new GameObject("LoadingCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
                                      typeof(GraphicRaycaster), typeof(CanvasGroup));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100; // in front of everything
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            var group = root.GetComponent<CanvasGroup>();

            var background = RaceHudBuilder.MakeRect("Background", root.transform);
            RaceHudBuilder.Stretch(background);
            background.gameObject.AddComponent<Image>().color = new Color(0.05f, 0.06f, 0.12f, 1f);

            var status = RaceHudBuilder.MakeText("StatusText", root.transform, "Loading", 80, TextAlignmentOptions.Center, Color.white);
            status.fontStyle = FontStyles.Bold;
            status.rectTransform.anchorMin = status.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            status.rectTransform.anchoredPosition = new Vector2(0, 40);
            status.rectTransform.sizeDelta = new Vector2(900, 120);

            var detail = RaceHudBuilder.MakeText("DetailText", root.transform, "", 44, TextAlignmentOptions.Center, new Color(1f, 1f, 1f, 0.65f));
            detail.rectTransform.anchorMin = detail.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            detail.rectTransform.anchoredPosition = new Vector2(0, -60);
            detail.rectTransform.sizeDelta = new Vector2(900, 80);

            var view = root.AddComponent<LoadingView>();
            var so = new SerializedObject(view);
            so.FindProperty("group").objectReferenceValue = group;
            so.FindProperty("statusText").objectReferenceValue = status;
            so.FindProperty("detailText").objectReferenceValue = detail;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

            string report = "Built " + PrefabPath + ".\n";
            if (Object.FindFirstObjectByType<LoadingView>() == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                Undo.RegisterCreatedObjectUndo(instance, "Add Loading");
                EditorSceneManager.MarkSceneDirty(instance.scene);
                report += "Added a LoadingCanvas to the open scene (it should be the Game scene). Save the scene.";
            }
            else report += "The open scene already has a LoadingCanvas, and it updates from the prefab.";
            EditorUtility.DisplayDialog("Loading prefab built", report, "OK");
        }
    }
}
