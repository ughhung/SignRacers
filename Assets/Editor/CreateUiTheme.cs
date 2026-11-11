using SignRacers.UI;
using UnityEditor;
using UnityEngine;

namespace SignRacers.EditorTools
{
    /// <summary>Menu item: Sign Racers / Create UI Theme. Makes Assets/Resources/UiTheme.asset (once) and selects it.</summary>
    public static class CreateUiTheme
    {
        private const string Path = "Assets/Resources/UiTheme.asset";

        [MenuItem("Sign Racers/Create UI Theme")]
        private static void Create()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");

            var existing = AssetDatabase.LoadAssetAtPath<UiTheme>(Path);
            if (existing == null)
            {
                existing = ScriptableObject.CreateInstance<UiTheme>();
                AssetDatabase.CreateAsset(existing, Path);
                AssetDatabase.SaveAssets();
            }

            Selection.activeObject = existing;
            EditorGUIUtility.PingObject(existing);
        }
    }
}
