using SignRacers.UI;
using UnityEditor;
using UnityEngine;

namespace SignRacers.EditorTools
{
    /// <summary>Menu item: Sign Racers / Create Item Visuals. Makes Assets/Resources/ItemVisuals.asset (once) and selects it.</summary>
    public static class CreateItemVisuals
    {
        private const string Path = "Assets/Resources/ItemVisuals.asset";

        [MenuItem("Sign Racers/Create Item Visuals")]
        private static void Create()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");

            var existing = AssetDatabase.LoadAssetAtPath<ItemVisuals>(Path);
            if (existing == null)
            {
                existing = ScriptableObject.CreateInstance<ItemVisuals>();
                AssetDatabase.CreateAsset(existing, Path);
                AssetDatabase.SaveAssets();
            }

            Selection.activeObject = existing;
            EditorGUIUtility.PingObject(existing);
        }
    }
}
