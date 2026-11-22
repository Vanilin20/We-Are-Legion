using UnityEditor;
using UnityEngine;

// Піксель-арт у Assets/Resources/Art імпортується однаково: спрайт, без згладжування й стиснення,
// 100 пікселів на одиницю (як мураха: 32 px = 0.32 = одна клітинка мурашника), опорна точка в центрі.
public class ArtImportSettings : AssetPostprocessor
{
    private const string Folder = "Assets/Resources/Art/";

    // Змінив налаштування нижче — збільш версію, і Unity переімпортує арт.
    public override uint GetVersion() => 2;

    void OnPreprocessTexture()
    {
        if (!assetPath.Replace('\\', '/').StartsWith(Folder)) return;

        var ti = (TextureImporter)assetImporter;
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.spritePixelsPerUnit = 100f;
        ti.filterMode = FilterMode.Point;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.mipmapEnabled = false;
        ti.alphaIsTransparency = true;
        ti.wrapMode = TextureWrapMode.Clamp;
        ti.isReadable = true; // для білого силуету (червоний спалах при ударі)

        var settings = new TextureImporterSettings();
        ti.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Center;
        settings.spriteMeshType = SpriteMeshType.FullRect;
        ti.SetTextureSettings(settings);
    }
}
