using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using HiHi2.AtlasTools;
using HiHi2.AtlasTools.Editor;

public static class GenerateOptimizedAtlasEditor
{
    private static AtlasGeneratorSettings cachedSettings;

    [MenuItem("Assets/图集生成替换/生成优化图集", false, 3000)]
    private static void GenerateAtlasFromSelection()
    {
        if (Selection.objects.Length == 0)
        {
            EditorUtility.DisplayDialog("错误", "请先选择一个文件夹", "确定");
            return;
        }

        if (Selection.objects.Length > 1)
        {
            EditorUtility.DisplayDialog("错误", "不允许选择多个文件夹，请只选择一个文件夹", "确定");
            return;
        }

        string sourceFolderPath = AssetDatabase.GetAssetPath(Selection.activeObject);
        if (!AssetDatabase.IsValidFolder(sourceFolderPath))
        {
            EditorUtility.DisplayDialog("错误", "所选对象不是文件夹", "确定");
            return;
        }

        AtlasGeneratorWindow.ShowWindow(sourceFolderPath);
    }

    public static AtlasGeneratorSettings LoadOrCreateSettings()
    {
        if (cachedSettings != null)
            return cachedSettings;

        string[] guids = AssetDatabase.FindAssets("t:AtlasGeneratorSettings");
        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            cachedSettings = AssetDatabase.LoadAssetAtPath<AtlasGeneratorSettings>(path);
        }

        if (cachedSettings == null)
        {
            cachedSettings = ScriptableObject.CreateInstance<AtlasGeneratorSettings>();
        }

        return cachedSettings;
    }

    public static string PrepareOutputFolder(string sourceFolderPath)
    {
        string outputPath = AtlasPathUtility.GetAutoOutputPath(sourceFolderPath);

        if (string.IsNullOrEmpty(outputPath))
        {
            Debug.LogError("Failed to determine output path");
            return null;
        }

        AtlasPathUtility.DeleteFolderIfExists(outputPath);

        string lodFolderPath = Path.GetDirectoryName(outputPath);
        lodFolderPath = AtlasPathUtility.NormalizePath(lodFolderPath);

        if (!AtlasPathUtility.EnsureFolderExists(lodFolderPath))
        {
            Debug.LogError($"Failed to create Lod folder: {lodFolderPath}");
            return null;
        }

        string parentFolder = Path.GetDirectoryName(lodFolderPath);
        string textureFolderName = Path.GetFileName(outputPath);

        string guid = AssetDatabase.CreateFolder(lodFolderPath, textureFolderName);
        if (string.IsNullOrEmpty(guid))
        {
            Debug.LogError($"Failed to create Texture folder: {outputPath}");
            return null;
        }

        AssetDatabase.Refresh();
        Debug.Log($"Created output folder: {outputPath}");

        return outputPath;
    }

    public static List<TextureInfo> CollectTextures(string folderPath)
    {
        return CollectTextures(folderPath, 0);
    }

    public static List<TextureInfo> CollectTextures(string folderPath, int padding)
    {
        if (string.IsNullOrEmpty(folderPath) || !AssetDatabase.IsValidFolder(folderPath))
        {
            Debug.LogError($"Invalid folder path: {folderPath}");
            return new List<TextureInfo>();
        }

        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folderPath });
        List<TextureInfo> textures = new List<TextureInfo>();

        foreach (string guid in guids)
        {
            string texPath = AssetDatabase.GUIDToAssetPath(guid);

            string texDir = Path.GetDirectoryName(texPath);
            texDir = AtlasPathUtility.NormalizePath(texDir);

            if (texDir != folderPath)
                continue;

            TextureImporter importer = AssetImporter.GetAtPath(texPath) as TextureImporter;
            if (importer == null)
                continue;

            if (importer.textureType == TextureImporterType.NormalMap)
            {
                Debug.Log($"Skipping normal map: {Path.GetFileName(texPath)}");
                continue;
            }

            bool needReimport = false;
            if (!importer.isReadable)
            {
                importer.isReadable = true;
                needReimport = true;
            }

            if (importer.alphaSource != TextureImporterAlphaSource.FromInput)
            {
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                needReimport = true;
            }

            if (!importer.alphaIsTransparency)
            {
                importer.alphaIsTransparency = true;
                needReimport = true;
            }

            if (needReimport)
            {
                importer.SaveAndReimport();
            }

            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            if (tex == null)
                continue;

            Vector2Int actualSize = GetActualTextureSize(tex, importer);

            int finalWidth = actualSize.x;
            int finalHeight = actualSize.y;

            if (padding > 0)
            {
                int paddingTotal = padding * 2;
                finalWidth = Mathf.Max(1, actualSize.x - paddingTotal);
                finalHeight = Mathf.Max(1, actualSize.y - paddingTotal);

                Debug.Log($"Texture {Path.GetFileName(texPath)}: {actualSize.x}x{actualSize.y} → {finalWidth}x{finalHeight} (padding={padding})");
            }

            TextureInfo info = new TextureInfo
            {
                name = Path.GetFileNameWithoutExtension(texPath),
                texture = tex,
                texturePath = texPath,
                width = finalWidth,
                height = finalHeight,
                area = finalWidth * finalHeight,
                originalWidth = actualSize.x,
                originalHeight = actualSize.y,
                isDownscaled = (finalWidth != actualSize.x || finalHeight != actualSize.y)
            };

            textures.Add(info);
        }

        textures.Sort((a, b) => b.area.CompareTo(a.area));

        return textures;
    }

    private static Vector2Int GetActualTextureSize(Texture2D texture, TextureImporter importer)
    {
        int maxSize = importer.maxTextureSize;

        importer.GetSourceTextureWidthAndHeight(out int sourceWidth, out int sourceHeight);

        if (sourceWidth == 0 || sourceHeight == 0)
        {
            sourceWidth = texture.width;
            sourceHeight = texture.height;
        }

        if (sourceWidth > maxSize || sourceHeight > maxSize)
        {
            float scale = Mathf.Min((float)maxSize / sourceWidth, (float)maxSize / sourceHeight);
            int scaledWidth = Mathf.FloorToInt(sourceWidth * scale);
            int scaledHeight = Mathf.FloorToInt(sourceHeight * scale);

            return new Vector2Int(scaledWidth, scaledHeight);
        }

        return new Vector2Int(sourceWidth, sourceHeight);
    }

    public static void PrepareReadableTextures(List<TextureInfo> textures)
    {
        if (textures == null)
            return;

        foreach (var texInfo in textures)
        {
            if (texInfo == null)
                continue;

            texInfo.readableTexture = AtlasTextureUtility.CreateReadableTexture(texInfo.texture, texInfo.width, texInfo.height);
        }
    }

    public class AtlasResult
    {
        public Texture2D texture;
        public List<MaxRectsAtlasPacker.PackedSprite> packedSprites;
        public int width;
        public int height;
        public float wastage;
    }

    public static List<AtlasResult> GenerateAtlases(List<TextureInfo> textures, AtlasGeneratorSettings settings)
    {
        if (textures == null || textures.Count == 0)
        {
            Debug.LogWarning("No textures to pack");
            return new List<AtlasResult>();
        }

        if (settings == null)
        {
            Debug.LogError("Settings cannot be null");
            return new List<AtlasResult>();
        }

        List<AtlasResult> results = new List<AtlasResult>();
        List<TextureInfo> remaining = new List<TextureInfo>(textures);

        int atlasIndex = 0;

        while (remaining.Count > 0)
        {
            Debug.Log($"Generating atlas {atlasIndex + 1}, {remaining.Count} textures remaining...");

            AtlasResult result = GenerateSingleAtlas(remaining, settings, out List<TextureInfo> packed);

            if (result == null)
            {
                Debug.LogError($"Unable to generate atlas for remaining {remaining.Count} textures");
                break;
            }

            results.Add(result);

            foreach (var packedTex in packed)
            {
                remaining.Remove(packedTex);
            }

            atlasIndex++;

            if (!settings.allowMultipleAtlases && remaining.Count > 0)
            {
                Debug.LogWarning("Multiple atlases not allowed, remaining textures will be ignored");
                break;
            }
        }

        return results;
    }

    private static AtlasResult GenerateSingleAtlas(List<TextureInfo> textures, AtlasGeneratorSettings settings, out List<TextureInfo> packedTextures)
    {
        packedTextures = new List<TextureInfo>();

        for (int size = settings.maxAtlasSize; size >= settings.minAtlasSize; size /= 2)
        {
            if (TryGenerateAtlas(textures, size, size, settings, out AtlasResult result, out packedTextures))
            {
                return result;
            }

            if (size * 2 <= settings.maxAtlasSize)
            {
                if (TryGenerateAtlas(textures, size * 2, size, settings, out result, out packedTextures))
                {
                    return result;
                }
            }

            if (size * 2 <= settings.maxAtlasSize)
            {
                if (TryGenerateAtlas(textures, size, size * 2, settings, out result, out packedTextures))
                {
                    return result;
                }
            }
        }

        return TryGeneratePartialAtlas(textures, settings, out packedTextures);
    }

    private static bool TryGenerateAtlas(List<TextureInfo> textures, int width, int height, AtlasGeneratorSettings settings, out AtlasResult result, out List<TextureInfo> packedTextures)
    {
        MaxRectsAtlasPacker packer = new MaxRectsAtlasPacker();

        if (packer.TryPack(textures, width, height, settings.padding, out List<MaxRectsAtlasPacker.PackedSprite> packed))
        {
            float wastage = packer.CalculateWastage(width, height);

            if (wastage <= settings.maxWastagePercent)
            {
                Texture2D atlasTexture = CreateAtlasTexture(packed, width, height, settings.padding);

                if (atlasTexture != null)
                {
                    result = new AtlasResult
                    {
                        texture = atlasTexture,
                        packedSprites = packed,
                        width = width,
                        height = height,
                        wastage = wastage
                    };

                    packedTextures = packed.Select(p => p.textureInfo).ToList();
                    return true;
                }
            }
        }

        result = null;
        packedTextures = null;
        return false;
    }

    private static AtlasResult TryGeneratePartialAtlas(List<TextureInfo> textures, AtlasGeneratorSettings settings, out List<TextureInfo> packedTextures)
    {
        for (int count = textures.Count; count > 0; count--)
        {
            List<TextureInfo> subset = textures.Take(count).ToList();

            for (int size = settings.maxAtlasSize; size >= settings.minAtlasSize; size /= 2)
            {
                if (TryGenerateAtlas(subset, size, size, settings, out AtlasResult result, out packedTextures))
                {
                    return result;
                }
            }
        }

        packedTextures = null;
        return null;
    }

    private static Texture2D CreateAtlasTexture(List<MaxRectsAtlasPacker.PackedSprite> packed, int width, int height, int padding)
    {
        if (packed == null || packed.Count == 0 || width <= 0 || height <= 0)
            return null;

        Texture2D atlas = new Texture2D(width, height, TextureFormat.RGBA32, false, false);

        Color[] clearColors = new Color[width * height];
        for (int i = 0; i < clearColors.Length; i++)
            clearColors[i] = Color.clear;
        atlas.SetPixels(clearColors);

        foreach (var sprite in packed)
        {
            if (sprite == null || sprite.textureInfo == null || sprite.textureInfo.readableTexture == null)
                continue;

            Texture2D sourceTex = sprite.textureInfo.readableTexture;
            Rect rect = sprite.rect;

            int x = (int)rect.x;
            int y = (int)rect.y;
            int w = (int)rect.width;
            int h = (int)rect.height;

            if (x < 0 || y < 0 || x + w > width || y + h > height)
            {
                Debug.LogWarning($"Sprite rect out of bounds: {rect}");
                continue;
            }

            Color[] pixels = sourceTex.GetPixels(0, 0, w, h);
            atlas.SetPixels(x, y, w, h, pixels);

            AtlasTextureUtility.ApplyPaddingFillOptimized(atlas, rect, sourceTex, padding);
        }

        atlas.Apply(false, false);
        return atlas;
    }

    public static void SaveAtlases(List<AtlasResult> atlasResults, string dataFolderPath, AtlasGeneratorSettings settings)
    {
        if (atlasResults == null || atlasResults.Count == 0)
        {
            Debug.LogWarning("No atlases to save");
            return;
        }

        if (string.IsNullOrEmpty(dataFolderPath) || settings == null)
        {
            Debug.LogError("Invalid parameters for SaveAtlases");
            return;
        }

        string lodFolderPath = Path.GetDirectoryName(dataFolderPath);
        string projectFolderName = Path.GetFileName(Path.GetDirectoryName(lodFolderPath));

        for (int i = 0; i < atlasResults.Count; i++)
        {
            AtlasResult result = atlasResults[i];
            if (result == null || result.texture == null)
                continue;

            string atlasName = $"{projectFolderName}_{i}";

            string pngPath = Path.Combine(dataFolderPath, $"{atlasName}.png");
            pngPath = AtlasPathUtility.NormalizePath(pngPath);

            byte[] pngBytes = result.texture.EncodeToPNG();
            File.WriteAllBytes(pngPath, pngBytes);
            AssetDatabase.ImportAsset(pngPath);

            TextureImporter importer = AssetImporter.GetAtPath(pngPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
                importer.isReadable = false;
                importer.mipmapEnabled = true;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = Mathf.Max(result.width, result.height);
                importer.ignoreMipmapLimit = false;

                importer.SaveAndReimport();
            }

            AtlasConfig config = ScriptableObject.CreateInstance<AtlasConfig>();
            config.atlasTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(pngPath);
            config.atlasWidth = result.width;
            config.atlasHeight = result.height;
            config.padding = settings.padding;
            config.spriteCount = result.packedSprites.Count;
            config.wastagePercent = result.wastage;

            foreach (var packed in result.packedSprites)
            {
                if (packed == null || packed.textureInfo == null)
                    continue;

                Rect uvRect = new Rect(
                    packed.rect.x / result.width,
                    packed.rect.y / result.height,
                    packed.rect.width / result.width,
                    packed.rect.height / result.height
                );

                var texInfo = packed.textureInfo;
                int padding = settings.padding;

                config.spriteInfos.Add(new SpriteInfoData
                {
                    spriteName = texInfo.name,
                    uvRect = uvRect,
                    originalSize = new Vector2Int(texInfo.originalWidth, texInfo.originalHeight),
                    resizedSize = new Vector2Int(texInfo.width, texInfo.height),
                    paddedSize = new Vector2Int(texInfo.PaddedWidth(padding), texInfo.PaddedHeight(padding)),
                    padding = padding,
                    sourceTexturePath = texInfo.texturePath
                });
            }

            string configPath = Path.Combine(dataFolderPath, $"{atlasName}.asset");
            configPath = AtlasPathUtility.NormalizePath(configPath);
            AssetDatabase.CreateAsset(config, configPath);

            Debug.Log($"已保存图集 {atlasName}：{result.width}x{result.height}，空白率：{result.wastage:F2}%，包含 {result.packedSprites.Count} 张图片");
        }
    }

    public static void CleanupTemporaryTextures(List<TextureInfo> textures)
    {
        if (textures == null)
            return;

        foreach (var texInfo in textures)
        {
            if (texInfo != null)
            {
                texInfo.Dispose();
            }
        }

        AtlasTextureUtility.CleanupCache();
    }
}