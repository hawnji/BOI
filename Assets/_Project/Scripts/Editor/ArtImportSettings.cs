using UnityEditor;
using UnityEngine;

namespace Hollowcrawl.EditorTools
{
    /// <summary>
    /// Import settings for the pixel art (Tools/PixelArt). Everything under Resources/Sprites and Resources/Tiles is
    /// a plain, uncompressed, point-filtered texture without mipmaps or power-of-two rescaling (animation strips
    /// have arbitrary widths). The game slices strips into sprites itself (PlaceholderArt.LoadFrames), and tiles stay
    /// readable because the room renderer composites them into one texture.
    /// </summary>
    public sealed class ArtImportSettings : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            string path = assetPath.Replace('\\', '/');
            bool sprite = path.StartsWith("Assets/Resources/Sprites/"), tile = path.StartsWith("Assets/Resources/Tiles/");
            if (!sprite && !tile) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Default;
            ti.alphaIsTransparency = true;
            ti.isReadable = tile;
            ti.mipmapEnabled = false;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.filterMode = FilterMode.Point;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
        }
    }
}
