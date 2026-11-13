using UnityEditor;
using UnityEngine;

namespace SignRacers.EditorTools
{
    /// <summary>
    /// Put the ui_*.png files in Assets/Art/UI/ and this sets them up automatically: Sprite (2D and UI),
    /// no compression, and the 9-slice border for the stretchy ones.
    /// If you imported them before adding this script, select the files, right-click > Reimport.
    /// (Replaces any earlier UiSpriteImporter.cs. Delete the old one.)
    /// </summary>
    public class UiSpriteImporter : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/Art/UI/")) return;
            string name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            if (!name.StartsWith("ui_")) return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = name == "ui_car_background" ? TextureImporterCompression.Compressed : TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Bilinear;

            // left, bottom, right, top (pixels)
            int b = 0;
            if (name == "ui_panel") b = 56;
            else if (name == "ui_card" || name == "ui_car_card") b = 64;
            else if (name == "ui_button") b = 44;
            else if (name == "ui_car_frame" || name.StartsWith("ui_car_button")) b = 48;
            else if (name == "ui_car_row") b = 36;
            else if (name.StartsWith("ui_kit_frame") || name.StartsWith("ui_pal_frame")) b = 60;
            else if (name.StartsWith("ui_pal_button")) b = 40;
            else if (name.StartsWith("ui_kit_button") || name == "ui_kit_card" || name == "ui_kit_card_alt") b = 40;
            else if (name == "ui_kit_bar") b = 20;
            else if (name.StartsWith("ui_kn_button") || name.StartsWith("ui_kn_frame")) b = 16;
            importer.spriteBorder = new Vector4(b, b, b, b);
        }
    }
}
