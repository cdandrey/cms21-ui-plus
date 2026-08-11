using UnityEngine;

namespace Cms21UiPlus
{
    /// <summary>Loads PNG/JPG data into Unity sprites.</summary>
    public static class TextureLoader
    {
        public static Sprite LoadSpriteFromFile(string filePath,
            bool highQualityCompression = true)
        {
            if (!System.IO.File.Exists(filePath)) {
                ModLogger.Log("File not found " + filePath,
                    Types.LoggingLevels.Warning);
                return null;
            }

            return LoadSpriteFromBytes(System.IO.File.ReadAllBytes(filePath),
                System.IO.Path.GetFileNameWithoutExtension(filePath),
                highQualityCompression);
        }

        public static Sprite LoadSpriteFromBytes(byte[] imageData)
        {
            return LoadSpriteFromBytes(imageData, null, true);
        }

        private static Sprite LoadSpriteFromBytes(byte[] imageData,
            string textureName, bool highQualityCompression)
        {
            if (imageData == null || imageData.Length == 0)
                return null;

            Texture2D texture = new Texture2D(2, 2);
            if (!string.IsNullOrEmpty(textureName))
                texture.name = textureName;
            if (!ImageConversion.LoadImage(texture, imageData)) {
                ModLogger.Log("LoadImage error", Types.LoggingLevels.Warning);
                UnityEngine.Object.Destroy(texture);
                return null;
            }

            texture.Compress(highQualityCompression);
            return Sprite.Create(texture,
                new Rect(0f, 0f, texture.width, texture.height),
                Vector2.zero, 100f, 0U, SpriteMeshType.Tight);
        }
    }
}
