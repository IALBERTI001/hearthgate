// The HUD icons (tools/blender/hg_icons.py renders them from the game's models into Assets/Resources/Icons): imported as
// crisp sprites with true transparency, no mipmaps (they are drawn at about their own size).
using UnityEditor;

namespace Hearthgate.EditorTools
{
    public class HGIconImport : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.Contains("Assets/Resources/Icons/")) return;
            var t = (TextureImporter)assetImporter;
            t.textureType = TextureImporterType.Sprite; t.spriteImportMode = SpriteImportMode.Single;
            t.alphaIsTransparency = true; t.mipmapEnabled = false; t.textureCompression = TextureImporterCompression.Uncompressed;
            t.wrapMode = UnityEngine.TextureWrapMode.Clamp; t.filterMode = UnityEngine.FilterMode.Bilinear;
        }
    }
}
