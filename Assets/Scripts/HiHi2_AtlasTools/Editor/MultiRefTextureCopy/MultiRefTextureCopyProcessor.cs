// HiHi2_AtlasTools\Editor\MultiRefTextureCopy\MultiRefTextureCopyProcessor.cs
// 处理逻辑
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace HiHi2.AtlasTools.Editor
{
    public static class MultiRefTextureCopyProcessor
    {
        public static TextureCopyScanResult ScanMultiRefTextures(string materialFolderPath)
        {
            var result = new TextureCopyScanResult();

            if (string.IsNullOrEmpty(materialFolderPath) || !AssetDatabase.IsValidFolder(materialFolderPath))
            {
                result.success = false;
                result.message = $"无效的材质文件夹路径: {materialFolderPath}";
                return result;
            }

            var scanResult = MaterialTextureScanner.ScanMaterialFolder(materialFolderPath);
            if (!scanResult.success)
            {
                result.success = false;
                result.message = scanResult.message;
                return result;
            }

            result.totalMaterialCount = scanResult.totalMaterialCount;
            result.totalTextureCount = scanResult.totalTextureCount;

            foreach (var texInfo in scanResult.multiReferencedTextures)
            {
                string absolutePath = Path.GetFullPath(texInfo.texturePath);
                string md5 = ComputeFileMD5(absolutePath);

                var copyInfo = new TextureCopyInfo
                {
                    sourceTexture = texInfo.texture,
                    sourcePath = texInfo.texturePath,
                    sourceName = Path.GetFileNameWithoutExtension(texInfo.texturePath),
                    sourceExtension = Path.GetExtension(texInfo.texturePath),
                    sourceMD5 = md5,
                    referenceCount = texInfo.referenceCount,
                    status = TextureCopyStatus.Pending
                };

                foreach (var matRef in texInfo.referencedByMaterials)
                {
                    copyInfo.referencedByMaterials.Add(matRef.materialName);
                }

                result.multiRefTextures.Add(copyInfo);
            }

            result.multiRefTextureCount = result.multiRefTextures.Count;
            result.success = true;
            result.message = $"扫描完成，发现 {result.multiRefTextureCount} 张多次引用纹理";

            return result;
        }

        public static TextureCopyResult CopyMultiRefTextures(List<TextureCopyInfo> texturesToCopy, string targetFolderPath)
        {
            var result = new TextureCopyResult();

            if (texturesToCopy == null || texturesToCopy.Count == 0)
            {
                result.success = false;
                result.message = "没有需要复制的纹理";
                return result;
            }

            if (string.IsNullOrEmpty(targetFolderPath) || !AssetDatabase.IsValidFolder(targetFolderPath))
            {
                result.success = false;
                result.message = $"无效的目标文件夹路径: {targetFolderPath}";
                return result;
            }

            string targetAbsolutePath = Path.GetFullPath(targetFolderPath);
            Dictionary<string, int> nameIndexMap = new Dictionary<string, int>();

            result.totalMultiRefCount = texturesToCopy.Count;

            foreach (var copyInfo in texturesToCopy)
            {
                try
                {
                    ProcessSingleTextureCopy(copyInfo, targetAbsolutePath, targetFolderPath, nameIndexMap);
                    result.copyInfoList.Add(copyInfo);

                    switch (copyInfo.status)
                    {
                        case TextureCopyStatus.Copied:
                            result.copiedCount++;
                            break;
                        case TextureCopyStatus.Skipped:
                            result.skippedCount++;
                            break;
                        case TextureCopyStatus.Renamed:
                            result.renamedCount++;
                            break;
                        case TextureCopyStatus.Failed:
                            result.failedCount++;
                            break;
                    }
                }
                catch (Exception ex)
                {
                    copyInfo.status = TextureCopyStatus.Failed;
                    copyInfo.statusMessage = ex.Message;
                    result.copyInfoList.Add(copyInfo);
                    result.failedCount++;
                    AtlasLogger.LogError($"复制纹理失败: {copyInfo.sourcePath}, 错误: {ex.Message}");
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            result.success = result.failedCount == 0;
            result.message = $"复制完成: 成功复制 {result.copiedCount} 张，跳过 {result.skippedCount} 张(MD5相同)，重命名 {result.renamedCount} 张，失败 {result.failedCount} 张";

            return result;
        }

        private static void ProcessSingleTextureCopy(TextureCopyInfo copyInfo, string targetAbsolutePath, string targetRelativePath, Dictionary<string, int> nameIndexMap)
        {
            string targetFileName = copyInfo.sourceName + copyInfo.sourceExtension;
            string targetFilePath = Path.Combine(targetAbsolutePath, targetFileName);

            if (File.Exists(targetFilePath))
            {
                string existingMD5 = ComputeFileMD5(targetFilePath);

                if (string.Equals(existingMD5, copyInfo.sourceMD5, StringComparison.OrdinalIgnoreCase))
                {
                    copyInfo.status = TextureCopyStatus.Skipped;
                    copyInfo.targetPath = Path.Combine(targetRelativePath, targetFileName).Replace("\\", "/");
                    copyInfo.targetName = copyInfo.sourceName;
                    copyInfo.statusMessage = "MD5相同，跳过复制";
                    return;
                }

                string baseName = copyInfo.sourceName;
                if (!nameIndexMap.ContainsKey(baseName))
                {
                    nameIndexMap[baseName] = 0;
                }

                string newName;
                string newFilePath;
                do
                {
                    newName = $"{baseName}_{nameIndexMap[baseName]}";
                    newFilePath = Path.Combine(targetAbsolutePath, newName + copyInfo.sourceExtension);
                    nameIndexMap[baseName]++;
                } while (File.Exists(newFilePath));

                string sourceAbsolutePath = Path.GetFullPath(copyInfo.sourcePath);
                File.Copy(sourceAbsolutePath, newFilePath, false);

                copyInfo.status = TextureCopyStatus.Renamed;
                copyInfo.targetPath = Path.Combine(targetRelativePath, newName + copyInfo.sourceExtension).Replace("\\", "/");
                copyInfo.targetName = newName;
                copyInfo.statusMessage = $"MD5不同，重命名为 {newName}";
            }
            else
            {
                string sourceAbsolutePath = Path.GetFullPath(copyInfo.sourcePath);
                File.Copy(sourceAbsolutePath, targetFilePath, false);

                copyInfo.status = TextureCopyStatus.Copied;
                copyInfo.targetPath = Path.Combine(targetRelativePath, targetFileName).Replace("\\", "/");
                copyInfo.targetName = copyInfo.sourceName;
                copyInfo.statusMessage = "复制成功";
            }
        }

        public static string ComputeFileMD5(string filePath)
        {
            if (!File.Exists(filePath))
                return string.Empty;

            using (var md5 = MD5.Create())
            using (var stream = File.OpenRead(filePath))
            {
                byte[] hash = md5.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }
    }
}
