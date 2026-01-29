using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace HiHi2.AtlasTools.Editor
{
    public static class LodMaxSizeScanner
    {
        private static readonly int[] MaxSizeOptions = { 32, 64, 128, 256, 512, 1024, 2048, 4096, 8192, 16384 };

        public static int[] GetMaxSizeOptions() => MaxSizeOptions;

        public static bool ValidateProjectFolder(string projectPath, out string lodTexturePath, out string texturePath)
        {
            lodTexturePath = null;
            texturePath = null;

            if (string.IsNullOrEmpty(projectPath))
            {
                AtlasLogger.LogWarning("项目路径为空");
                return false;
            }

            projectPath = AtlasPathUtility.NormalizePath(projectPath);

            if (!AssetDatabase.IsValidFolder(projectPath))
            {
                AtlasLogger.LogWarning($"无效的项目文件夹: {projectPath}");
                return false;
            }

            lodTexturePath = AtlasPathUtility.NormalizePath(
                Path.Combine(projectPath, LodMaxSizeConstants.LOD_TEXTURE_SUBFOLDER));
            texturePath = AtlasPathUtility.NormalizePath(
                Path.Combine(projectPath, LodMaxSizeConstants.TEXTURE_SUBFOLDER));

            if (!AssetDatabase.IsValidFolder(lodTexturePath))
            {
                AtlasLogger.LogWarning($"未找到 LOD/Texture 文件夹: {lodTexturePath}");
                return false;
            }

            if (!AssetDatabase.IsValidFolder(texturePath))
            {
                AtlasLogger.LogWarning($"未找到 Texture 文件夹: {texturePath}");
                return false;
            }

            return true;
        }

        public static List<TextureMaxSizeMatchInfo> ScanTextures(string projectPath, out LodMaxSizeScanResult result)
        {
            result = new LodMaxSizeScanResult();
            var matchInfoList = new List<TextureMaxSizeMatchInfo>();

            if (!ValidateProjectFolder(projectPath, out string lodTexturePath, out string texturePath))
            {
                result.success = false;
                result.message = "项目文件夹结构不正确";
                return matchInfoList;
            }

            string[] lodTextureGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { lodTexturePath });

            if (lodTextureGuids.Length == 0)
            {
                result.success = false;
                result.message = "LOD/Texture 文件夹中未找到任何纹理";
                AtlasLogger.LogWarning(result.message);
                return matchInfoList;
            }

            Dictionary<string, string> lod0TextureMap = BuildLod0TextureMap(texturePath);

            foreach (string guid in lodTextureGuids)
            {
                string lodAssetPath = AssetDatabase.GUIDToAssetPath(guid);
                string lodFileName = Path.GetFileNameWithoutExtension(lodAssetPath);

                var matchInfo = CreateMatchInfo(lodAssetPath, lodFileName, lod0TextureMap);
                matchInfoList.Add(matchInfo);
            }

            matchInfoList = matchInfoList
                .OrderBy(m => !m.needsSync)
                .ThenBy(m => !m.isMatched)
                .ThenBy(m => m.lodTextureName)
                .ToList();

            result.success = true;
            result.totalCount = matchInfoList.Count;
            result.matchedCount = matchInfoList.Count(m => m.isMatched);
            result.needsSyncCount = matchInfoList.Count(m => m.needsSync);
            result.message = $"扫描完成！找到 {result.totalCount} 个纹理，{result.needsSyncCount} 个需要同步";

            AtlasLogger.Log($"<color=green>{result.message}</color>");

            return matchInfoList;
        }

        public static List<TextureMaxSizeMatchInfo> ScanTexturesCustomMode(
            string lodTexturePath, 
            string sourceTexturePath, 
            out LodMaxSizeScanResult result)
        {
            result = new LodMaxSizeScanResult();
            var matchInfoList = new List<TextureMaxSizeMatchInfo>();

            if (string.IsNullOrEmpty(lodTexturePath) || string.IsNullOrEmpty(sourceTexturePath))
            {
                result.success = false;
                result.message = "LOD纹理路径或源纹理路径为空";
                return matchInfoList;
            }

            lodTexturePath = AtlasPathUtility.NormalizePath(lodTexturePath);
            sourceTexturePath = AtlasPathUtility.NormalizePath(sourceTexturePath);

            if (!AssetDatabase.IsValidFolder(lodTexturePath))
            {
                result.success = false;
                result.message = $"无效的LOD纹理文件夹: {lodTexturePath}";
                return matchInfoList;
            }

            if (!AssetDatabase.IsValidFolder(sourceTexturePath))
            {
                result.success = false;
                result.message = $"无效的源纹理文件夹: {sourceTexturePath}";
                return matchInfoList;
            }

            string[] lodTextureGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { lodTexturePath });

            if (lodTextureGuids.Length == 0)
            {
                result.success = false;
                result.message = "LOD纹理文件夹中未找到任何纹理";
                AtlasLogger.LogWarning(result.message);
                return matchInfoList;
            }

            Dictionary<string, string> sourceTextureMap = BuildSourceTextureMapFlat(sourceTexturePath);

            foreach (string guid in lodTextureGuids)
            {
                string lodAssetPath = AssetDatabase.GUIDToAssetPath(guid);
                string lodFileName = Path.GetFileNameWithoutExtension(lodAssetPath);

                var matchInfo = CreateMatchInfoFromMap(lodAssetPath, lodFileName, sourceTextureMap);
                matchInfoList.Add(matchInfo);
            }

            matchInfoList = matchInfoList
                .OrderBy(m => !m.needsSync)
                .ThenBy(m => !m.isMatched)
                .ThenBy(m => m.lodTextureName)
                .ToList();

            result.success = true;
            result.totalCount = matchInfoList.Count;
            result.matchedCount = matchInfoList.Count(m => m.isMatched);
            result.needsSyncCount = matchInfoList.Count(m => m.needsSync);
            result.message = $"扫描完成！找到 {result.totalCount} 个纹理，{result.needsSyncCount} 个需要同步";

            AtlasLogger.Log($"<color=green>{result.message}</color>");

            return matchInfoList;
        }

        private static Dictionary<string, string> BuildLod0TextureMap(string texturePath)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            string[] subFolders = AssetDatabase.GetSubFolders(texturePath);
            var foldersToScan = new List<string> { texturePath };
            foldersToScan.AddRange(subFolders);

            foreach (string folder in foldersToScan)
            {
                string[] textureGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });

                foreach (string guid in textureGuids)
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                    string fileName = Path.GetFileNameWithoutExtension(assetPath);

                    if (fileName.EndsWith(LodMaxSizeConstants.LOD0_SUFFIX, StringComparison.OrdinalIgnoreCase))
                    {
                        string baseName = fileName.Substring(0, fileName.Length - LodMaxSizeConstants.LOD0_SUFFIX.Length);
                        if (!map.ContainsKey(baseName))
                        {
                            map[baseName] = assetPath;
                        }
                    }
                }
            }

            return map;
        }

        private static TextureMaxSizeMatchInfo CreateMatchInfo(string lodAssetPath, string lodFileName,
            Dictionary<string, string> lod0TextureMap)
        {
            var matchInfo = new TextureMaxSizeMatchInfo
            {
                lodTexturePath = lodAssetPath,
                lodTextureName = lodFileName,
                lodTexturePreview = AssetDatabase.LoadAssetAtPath<Texture2D>(lodAssetPath)
            };

            string upperCaseName = FindMatchingSourceName(lodFileName, lod0TextureMap.Keys.ToList());

            if (!string.IsNullOrEmpty(upperCaseName) && lod0TextureMap.TryGetValue(upperCaseName, out string sourcePath))
            {
                matchInfo.sourceTexturePath = sourcePath;
                matchInfo.sourceTextureName = Path.GetFileNameWithoutExtension(sourcePath);
                matchInfo.sourceTexturePreview = AssetDatabase.LoadAssetAtPath<Texture2D>(sourcePath);
                matchInfo.isMatched = true;

                TextureImporter lodImporter = AssetImporter.GetAtPath(lodAssetPath) as TextureImporter;
                TextureImporter sourceImporter = AssetImporter.GetAtPath(sourcePath) as TextureImporter;

                if (lodImporter != null && sourceImporter != null)
                {
                    matchInfo.lodTextureCurrentMaxSize = lodImporter.maxTextureSize;
                    matchInfo.sourceLod0MaxSize = sourceImporter.maxTextureSize;
                    matchInfo.targetMaxSize = sourceImporter.maxTextureSize;
                    matchInfo.needsSync = lodImporter.maxTextureSize != sourceImporter.maxTextureSize;
                }
                else
                {
                    matchInfo.errorMessage = "无法获取TextureImporter";
                }
            }
            else
            {
                matchInfo.isMatched = false;
                matchInfo.errorMessage = "未找到匹配的lod0纹理";
            }

            return matchInfo;
        }

        private static Dictionary<string, string> BuildSourceTextureMapFlat(string sourceTexturePath)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            string[] textureGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { sourceTexturePath });

            foreach (string guid in textureGuids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                string fileName = Path.GetFileNameWithoutExtension(assetPath);

                string baseName = fileName;
                if (fileName.EndsWith(LodMaxSizeConstants.LOD0_SUFFIX, StringComparison.OrdinalIgnoreCase))
                {
                    baseName = fileName.Substring(0, fileName.Length - LodMaxSizeConstants.LOD0_SUFFIX.Length);
                }

                if (!map.ContainsKey(baseName))
                {
                    map[baseName] = assetPath;
                }
            }

            return map;
        }

        private static TextureMaxSizeMatchInfo CreateMatchInfoFromMap(string lodAssetPath, string lodFileName,
            Dictionary<string, string> sourceTextureMap)
        {
            var matchInfo = new TextureMaxSizeMatchInfo
            {
                lodTexturePath = lodAssetPath,
                lodTextureName = lodFileName,
                lodTexturePreview = AssetDatabase.LoadAssetAtPath<Texture2D>(lodAssetPath)
            };

            string matchKey = FindMatchingSourceName(lodFileName, sourceTextureMap.Keys.ToList());

            if (!string.IsNullOrEmpty(matchKey) && sourceTextureMap.TryGetValue(matchKey, out string sourcePath))
            {
                matchInfo.sourceTexturePath = sourcePath;
                matchInfo.sourceTextureName = Path.GetFileNameWithoutExtension(sourcePath);
                matchInfo.sourceTexturePreview = AssetDatabase.LoadAssetAtPath<Texture2D>(sourcePath);
                matchInfo.isMatched = true;

                TextureImporter lodImporter = AssetImporter.GetAtPath(lodAssetPath) as TextureImporter;
                TextureImporter sourceImporter = AssetImporter.GetAtPath(sourcePath) as TextureImporter;

                if (lodImporter != null && sourceImporter != null)
                {
                    matchInfo.lodTextureCurrentMaxSize = lodImporter.maxTextureSize;
                    matchInfo.sourceLod0MaxSize = sourceImporter.maxTextureSize;
                    matchInfo.targetMaxSize = sourceImporter.maxTextureSize;
                    matchInfo.needsSync = lodImporter.maxTextureSize != sourceImporter.maxTextureSize;
                }
                else
                {
                    matchInfo.errorMessage = "无法获取TextureImporter";
                }
            }
            else
            {
                matchInfo.isMatched = false;
                matchInfo.errorMessage = "未找到匹配的源纹理";
            }

            return matchInfo;
        }

        private static string FindMatchingSourceName(string lodTextureName, List<string> sourceNames)
        {
            foreach (string sourceName in sourceNames)
            {
                if (string.Equals(lodTextureName, sourceName, StringComparison.OrdinalIgnoreCase))
                {
                    return sourceName;
                }
            }

            return null;
        }
    }
}