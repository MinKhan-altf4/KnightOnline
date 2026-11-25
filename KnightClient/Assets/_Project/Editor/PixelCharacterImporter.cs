using UnityEditor;
using UnityEngine;

namespace KnightOnline.Client.Editor
{
    /// <summary>
    /// Import rules for PixelDoll character sprites under
    /// Resources/Characters/: one 128x128 cell per direction, feet pivot at
    /// (64, 116) from the top-left (PixelDoll export.bakedPivot), crisp pixels.
    /// Applied on every (re)import, so new characters dropped in the folder
    /// get the same settings without manual Inspector work.
    /// </summary>
    public sealed class PixelCharacterImporter : AssetPostprocessor
    {
        private const string Folder = "Assets/_Project/Resources/Characters/";

        /// <summary>128 px cell = 2 world units; the figure is about 1.4 units tall.</summary>
        public const float PixelsPerUnit = 64f;

        // PixelDoll pivot is measured from the top-left; Unity's from the bottom-left.
        private static readonly Vector2 FeetPivot = new(64f / 128f, 1f - 116f / 128f);

        /// <summary>Bump when the rules change so Unity reimports affected textures.</summary>
        public override uint GetVersion() => 2;

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Folder, System.StringComparison.Ordinal) ||
                !assetPath.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase))
                return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            // Plain 2D texture: a minimal .meta can otherwise default to Cube,
            // which yields a Cubemap and no Sprite.
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = FeetPivot;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
        }
    }
}
