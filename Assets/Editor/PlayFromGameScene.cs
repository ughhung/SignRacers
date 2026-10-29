using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace SignRacers.EditorTools
{
    /// <summary>
    /// Sign Racers > Always Press Play From Game Scene. When ticked (the default), pressing Play always starts
    /// the Game scene, which loads the track by itself, whichever scene you have open.
    /// </summary>
    [InitializeOnLoad]
    public static class PlayFromGameScene
    {
        private const string MenuPath = "Sign Racers/Always Press Play From Game Scene";
        private const string Key = "SignRacers.PlayFromGame";

        static PlayFromGameScene() => EditorApplication.delayCall += Apply;

        [MenuItem(MenuPath)]
        private static void Toggle()
        {
            EditorPrefs.SetBool(Key, !EditorPrefs.GetBool(Key, true));
            Apply();
        }

        private static void Apply()
        {
            bool on = EditorPrefs.GetBool(Key, true);
            Menu.SetChecked(MenuPath, on);
            EditorSceneManager.playModeStartScene = null;
            if (!on) return;

            foreach (string guid in AssetDatabase.FindAssets("Game t:SceneAsset"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) != "Game") continue;
                EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
                return;
            }
        }
    }
}
