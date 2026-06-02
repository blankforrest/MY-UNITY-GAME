using UnityEngine;
using UnityEditor;

/// <summary>
/// Automatically run on import — sets any PNG in Assets/Sprites/ to Sprite type.
/// No manual action needed.
/// </summary>
public class SpriteImportFixer : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith("Assets/Sprites/") &&
            !assetPath.StartsWith("Assets/Resources/Sprites/")) return;
        if (!assetPath.EndsWith(".png") && !assetPath.EndsWith(".jpg")) return;

        TextureImporter ti = (TextureImporter)assetImporter;
        if (ti.textureType != TextureImporterType.Sprite)
        {
            ti.textureType        = TextureImporterType.Sprite;
            ti.spriteImportMode   = SpriteImportMode.Single;
            ti.alphaIsTransparency = true;
            ti.filterMode         = FilterMode.Point; // crisp pixel art
            ti.maxTextureSize     = 128;
        }
    }
}
