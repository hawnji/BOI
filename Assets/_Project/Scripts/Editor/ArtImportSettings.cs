using UnityEditor;
using UnityEngine;

namespace Hollowcrawl.EditorTools
{
    /// <summary>
    /// Import settings for the pre-rendered art (Tools/ArtGen). Sprites under Resources/Sprites become smooth,
    /// high-quality sprites (PlaceholderArt rescales them so their width is one world unit); tiles under Resources/Tiles stay plain textures
    /// with Read/Write on, because the room renderer composites them into one texture.
    /// </summary>
    public sealed class ArtImportSettings : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            var ti = (TextureImporter)assetImporter;
            string path = assetPath.Replace('\\', '/');
            if (path.StartsWith("Assets/Resources/Sprites/"))
            {
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.alphaIsTransparency = true;
                ti.mipmapEnabled = false;
                ti.filterMode = FilterMode.Bilinear;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.textureCompression = TextureImporterCompression.CompressedHQ;
                var settings = new TextureImporterSettings();
                ti.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.Center;
                settings.spriteMeshType = SpriteMeshType.FullRect;
                ti.SetTextureSettings(settings);
            }
            else if (path.StartsWith("Assets/Resources/Tiles/"))
            {
                ti.textureType = TextureImporterType.Default;
                ti.isReadable = true;
                ti.mipmapEnabled = false;
                ti.npotScale = TextureImporterNPOTScale.None;
                ti.filterMode = FilterMode.Bilinear;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
            }
        }
    }
}
