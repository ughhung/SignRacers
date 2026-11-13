using SignRacers.UI;
using UnityEditor;
using UnityEngine;

namespace SignRacers.EditorTools
{
    /// <summary>Menu item: Sign Racers / Create UI Skin. Makes Assets/Resources/UiSkin.asset (once) and selects it.</summary>
    public static class CreateUiSkin
    {
        private const string Path = "Assets/Resources/UiSkin.asset";

        [MenuItem("Sign Racers/Create UI Skin")]
        private static void Create()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");

            var existing = AssetDatabase.LoadAssetAtPath<UiSkin>(Path);
            if (existing == null)
            {
                existing = ScriptableObject.CreateInstance<UiSkin>();
                AssetDatabase.CreateAsset(existing, Path);
                AssetDatabase.SaveAssets();
            }

            Selection.activeObject = existing;
            EditorGUIUtility.PingObject(existing);
        }
    }
}
