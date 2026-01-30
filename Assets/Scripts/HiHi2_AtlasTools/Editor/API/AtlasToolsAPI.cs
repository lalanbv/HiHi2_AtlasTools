using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;

namespace HiHi2.AtlasTools.Editor
{
    /// <summary>
    /// 图集工具外部API接口
    /// 提供图集生成、材质替换的静态方法供外部工具调用
    /// </summary>
    public static class AtlasToolsAPI
    {
        #region 结果类定义

        /// <summary>
        /// 图集生成结果
        /// </summary>
        public class AtlasGenerationResult
        {
            public bool success;
            public string message;
            public int atlasCount;
            public int textureCount;
            public string outputPath;
            public List<string> atlasFiles = new List<string>();
        }

        /// <summary>
        /// 材质替换结果
        /// </summary>
        public class MaterialReplacementResult
        {
            public bool success;
            public string message;
            public int materialCount;
            public int replacedTextureCount;
            public string outputPath;
            public List<string> processedMaterials = new List<string>();
        }

        /// <summary>
        /// 完整处理结果
        /// </summary>
        public class ProcessAllResult
        {
            public bool success;
            public string message;
            public AtlasGenerationResult atlasResult;
            public MaterialReplacementResult materialResult;
        }

        #endregion

        #region 方法一：图集生成

        /// <summary>
        /// 生成图集和配置文件
        /// </summary>
        /// <param name="sourceTexturePath">原始纹理文件夹路径（Assets/...格式）</param>
        /// <param name="outputAtlasPath">图集输出路径（Assets/...格式）</param>
        /// <param name="settings">图集生成设置，为null时使用默认设置</param>
        /// <returns>图集生成结果</returns>
        public static AtlasGenerationResult GenerateAtlas(
            string sourceTexturePath,
            string outputAtlasPath,
            AtlasGeneratorSettings settings = null)
        {
            var result = new AtlasGenerationResult();

            try
            {
                if (!ValidateAtlasGenerationInput(sourceTexturePath, outputAtlasPath, out string errorMsg))
                {
                    result.success = false;
                    result.message = errorMsg;
                    AtlasLogger.LogError(errorMsg);
                    return result;
                }

                sourceTexturePath = AtlasPathUtility.NormalizePath(sourceTexturePath);
                outputAtlasPath = AtlasPathUtility.NormalizePath(outputAtlasPath);

                if (settings == null)
                {
                    settings = CreateDefaultSettings();
                }

                List<TextureInfo> textures = GenerateOptimizedAtlasEditor.CollectTextures(sourceTexturePath, settings.padding);
                if (textures == null || textures.Count == 0)
                {
                    result.success = false;
                    result.message = $"源路径中未找到可用纹理: {sourceTexturePath}";
                    AtlasLogger.LogError(result.message);
                    return result;
                }

                result.textureCount = textures.Count;
                AtlasLogger.Log($"收集到 {textures.Count} 张纹理");

                GenerateOptimizedAtlasEditor.PrepareReadableTextures(textures);

                List<GenerateOptimizedAtlasEditor.AtlasResult> atlasResults =
                    GenerateOptimizedAtlasEditor.GenerateAtlases(textures, settings);

                if (atlasResults == null || atlasResults.Count == 0)
                {
                    GenerateOptimizedAtlasEditor.CleanupTemporaryTextures(textures);
                    result.success = false;
                    result.message = "根据当前参数无法生成图集，请调整设置";
                    AtlasLogger.LogError(result.message);
                    return result;
                }

                if (!PrepareOutputFolder(outputAtlasPath))
                {
                    GenerateOptimizedAtlasEditor.CleanupTemporaryTextures(textures);
                    result.success = false;
                    result.message = $"无法创建输出目录: {outputAtlasPath}";
                    AtlasLogger.LogError(result.message);
                    return result;
                }

                GenerateOptimizedAtlasEditor.SaveAtlases(atlasResults, outputAtlasPath, settings);

                GenerateOptimizedAtlasEditor.CleanupTemporaryTextures(textures);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                result.success = true;
                result.atlasCount = atlasResults.Count;
                result.outputPath = outputAtlasPath;
                result.message = $"图集生成完成！生成了 {atlasResults.Count} 张图集，包含 {textures.Count} 张纹理";

                for (int i = 0; i < atlasResults.Count; i++)
                {
                    string projectName = GetProjectNameFromPath(outputAtlasPath);
                    result.atlasFiles.Add($"{projectName}_{i}.png");
                    result.atlasFiles.Add($"{projectName}_{i}.asset");
                }

                AtlasLogger.Log($"<color=green>{result.message}</color>");
            }
            catch (Exception e)
            {
                result.success = false;
                result.message = $"图集生成异常: {e.Message}";
                AtlasLogger.LogError($"{result.message}\n{e.StackTrace}");
            }

            return result;
        }

        #endregion

        #region 方法二：材质替换

        /// <summary>
        /// 执行材质图集替换
        /// </summary>
        /// <param name="sourceMaterialPath">原始材质文件夹路径（Assets/...格式）</param>
        /// <param name="outputMaterialPath">材质输出路径（Assets/...格式）</param>
        /// <param name="atlasConfigPath">图集配置文件夹路径（Assets/...格式），用于查找AtlasConfig</param>
        /// <returns>材质替换结果</returns>
        public static MaterialReplacementResult ReplaceMaterialTextures(
            string sourceMaterialPath,
            string outputMaterialPath,
            string atlasConfigPath)
        {
            var result = new MaterialReplacementResult();

            try
            {
                if (!ValidateMaterialReplacementInput(sourceMaterialPath, outputMaterialPath, atlasConfigPath, out string errorMsg))
                {
                    result.success = false;
                    result.message = errorMsg;
                    AtlasLogger.LogError(errorMsg);
                    return result;
                }

                sourceMaterialPath = AtlasPathUtility.NormalizePath(sourceMaterialPath);
                outputMaterialPath = AtlasPathUtility.NormalizePath(outputMaterialPath);
                atlasConfigPath = AtlasPathUtility.NormalizePath(atlasConfigPath);

                List<AtlasConfig> atlasConfigs = MaterialAtlasReplacer.CollectAtlasConfigsFromFolder(atlasConfigPath);
                if (atlasConfigs == null || atlasConfigs.Count == 0)
                {
                    result.success = false;
                    result.message = $"未找到图集配置文件: {atlasConfigPath}";
                    AtlasLogger.LogError(result.message);
                    return result;
                }

                AtlasLogger.Log($"加载了 {atlasConfigs.Count} 个图集配置");

                List<Material> sourceMaterials = MaterialAtlasReplacer.CollectMaterialsFromFolder(sourceMaterialPath);
                if (sourceMaterials == null || sourceMaterials.Count == 0)
                {
                    result.success = false;
                    result.message = $"未找到材质文件: {sourceMaterialPath}";
                    AtlasLogger.LogError(result.message);
                    return result;
                }

                AtlasLogger.Log($"收集到 {sourceMaterials.Count} 个材质");

                if (!AtlasPathUtility.EnsureFolderExists(outputMaterialPath))
                {
                    result.success = false;
                    result.message = $"无法创建输出目录: {outputMaterialPath}";
                    AtlasLogger.LogError(result.message);
                    return result;
                }

                List<Material> copiedMaterials = MaterialCopyUtility.CopyMaterials(
                    sourceMaterials,
                    sourceMaterialPath,
                    outputMaterialPath);

                if (copiedMaterials == null || copiedMaterials.Count == 0)
                {
                    result.success = false;
                    result.message = "材质拷贝失败";
                    AtlasLogger.LogError(result.message);
                    return result;
                }

                List<MaterialAtlasReplacer.MaterialProcessResult> processResults =
                    MaterialAtlasReplacer.AnalyzeMaterials(copiedMaterials, atlasConfigs);

                MaterialAtlasReplacer.ApplyReplacements(processResults, true);

                int replacedCount = CountReplacedTextures(processResults);

                result.success = true;
                result.materialCount = copiedMaterials.Count;
                result.replacedTextureCount = replacedCount;
                result.outputPath = outputMaterialPath;
                result.message = $"材质替换完成！处理了 {copiedMaterials.Count} 个材质，替换了 {replacedCount} 张纹理";

                foreach (var mat in copiedMaterials)
                {
                    if (mat != null)
                    {
                        result.processedMaterials.Add(mat.name);
                    }
                }

                AtlasLogger.Log($"<color=green>{result.message}</color>");
            }
            catch (Exception e)
            {
                result.success = false;
                result.message = $"材质替换异常: {e.Message}";
                AtlasLogger.LogError($"{result.message}\n{e.StackTrace}");
            }

            return result;
        }

        #endregion

        #region 方法三：完整处理

        /// <summary>
        /// 执行完整的图集生成和材质替换流程
        /// </summary>
        /// <param name="sourceTexturePath">原始纹理文件夹路径（Assets/...格式）</param>
        /// <param name="sourceMaterialPath">原始材质文件夹路径（Assets/...格式）</param>
        /// <param name="outputAtlasPath">图集输出路径（Assets/...格式）</param>
        /// <param name="outputMaterialPath">材质输出路径（Assets/...格式）</param>
        /// <param name="settings">图集生成设置，为null时使用默认设置</param>
        /// <returns>完整处理结果</returns>
        public static ProcessAllResult ProcessAll(
            string sourceTexturePath,
            string sourceMaterialPath,
            string outputAtlasPath,
            string outputMaterialPath,
            AtlasGeneratorSettings settings = null)
        {
            var result = new ProcessAllResult();

            try
            {
                AtlasLogger.Log("开始执行完整的图集生成和材质替换流程...");

                result.atlasResult = GenerateAtlas(sourceTexturePath, outputAtlasPath, settings);

                if (!result.atlasResult.success)
                {
                    result.success = false;
                    result.message = $"图集生成失败: {result.atlasResult.message}";
                    AtlasLogger.LogError(result.message);
                    return result;
                }

                AtlasLogger.Log("图集生成完成，开始材质替换...");

                result.materialResult = ReplaceMaterialTextures(
                    sourceMaterialPath,
                    outputMaterialPath,
                    outputAtlasPath);

                if (!result.materialResult.success)
                {
                    result.success = false;
                    result.message = $"材质替换失败: {result.materialResult.message}";
                    AtlasLogger.LogError(result.message);
                    return result;
                }

                result.success = true;
                result.message = $"处理完成！生成了 {result.atlasResult.atlasCount} 张图集，替换了 {result.materialResult.materialCount} 个材质中的 {result.materialResult.replacedTextureCount} 张纹理";

                AtlasLogger.Log($"<color=green>{result.message}</color>");
            }
            catch (Exception e)
            {
                result.success = false;
                result.message = $"处理异常: {e.Message}";
                AtlasLogger.LogError($"{result.message}\n{e.StackTrace}");
            }

            return result;
        }

        #endregion

        #region 方法四：LOD MaxSize 扫描

        /// <summary>
        /// LOD MaxSize 扫描结果
        /// </summary>
        public class MaxSizeScanResult
        {
            public bool success;
            public string message;
            public int totalCount;
            public int matchedCount;
            public int needsSyncCount;
            public List<TextureMaxSizeMatchInfo> matchInfoList = new List<TextureMaxSizeMatchInfo>();
        }

        /// <summary>
        /// LOD MaxSize 同步结果
        /// </summary>
        public class MaxSizeSyncResult
        {
            public bool success;
            public string message;
            public int syncedCount;
        }

        /// <summary>
        /// LOD MaxSize 完整处理结果
        /// </summary>
        public class MaxSizeProcessResult
        {
            public bool success;
            public string message;
            public MaxSizeScanResult scanResult;
            public MaxSizeSyncResult syncResult;
        }

        /// <summary>
        /// 一键扫描并同步 LOD 纹理的 MaxSize（推荐使用）
        /// 直接传入 LOD 纹理路径和源纹理路径，自动完成扫描和同步
        /// </summary>
        /// <param name="lodTexturePath">LOD 纹理文件夹路径（Assets/...格式），如 Assets/MyProject/LOD/Texture</param>
        /// <param name="sourceTexturePath">源纹理文件夹路径（Assets/...格式），如 Assets/MyProject/Texture</param>
        /// <returns>完整处理结果，包含扫描和同步信息</returns>
        public static MaxSizeProcessResult ScanAndSyncMaxSize(string lodTexturePath, string sourceTexturePath)
        {
            var result = new MaxSizeProcessResult();

            try
            {
                if (string.IsNullOrEmpty(lodTexturePath))
                {
                    result.success = false;
                    result.message = "LOD纹理路径不能为空";
                    AtlasLogger.LogError(result.message);
                    return result;
                }

                if (string.IsNullOrEmpty(sourceTexturePath))
                {
                    result.success = false;
                    result.message = "源纹理路径不能为空";
                    AtlasLogger.LogError(result.message);
                    return result;
                }

                AtlasLogger.Log("开始执行 LOD MaxSize 扫描和同步...");

                result.scanResult = ScanMaxSizeCustom(lodTexturePath, sourceTexturePath);

                if (!result.scanResult.success)
                {
                    result.success = false;
                    result.message = $"扫描失败: {result.scanResult.message}";
                    AtlasLogger.LogError(result.message);
                    return result;
                }

                if (result.scanResult.needsSyncCount == 0)
                {
                    result.success = true;
                    result.message = $"扫描完成，共 {result.scanResult.totalCount} 个纹理，所有 MaxSize 已同步，无需修改";
                    result.syncResult = new MaxSizeSyncResult
                    {
                        success = true,
                        message = "无需同步",
                        syncedCount = 0
                    };
                    AtlasLogger.Log($"<color=green>{result.message}</color>");
                    return result;
                }

                AtlasLogger.Log($"扫描完成，{result.scanResult.needsSyncCount} 个纹理需要同步，开始同步...");

                result.syncResult = SyncAllMaxSize(result.scanResult.matchInfoList);

                if (!result.syncResult.success)
                {
                    result.success = false;
                    result.message = $"同步失败: {result.syncResult.message}";
                    AtlasLogger.LogError(result.message);
                    return result;
                }

                result.success = true;
                result.message = $"处理完成！扫描了 {result.scanResult.totalCount} 个纹理，同步了 {result.syncResult.syncedCount} 个 MaxSize 设置";

                AtlasLogger.Log($"<color=green>{result.message}</color>");
            }
            catch (Exception e)
            {
                result.success = false;
                result.message = $"处理异常: {e.Message}";
                AtlasLogger.LogError($"{result.message}\n{e.StackTrace}");
            }

            return result;
        }

        /// <summary>
        /// 扫描项目纹理的 MaxSize 匹配信息（自动模式）
        /// 自动识别 [projectPath]/LOD/Texture 和 [projectPath]/Texture 路径
        /// </summary>
        /// <param name="projectPath">项目根文件夹路径（Assets/...格式）</param>
        /// <returns>扫描结果，包含所有纹理的匹配信息</returns>
        public static MaxSizeScanResult ScanProjectMaxSize(string projectPath)
        {
            var result = new MaxSizeScanResult();

            try
            {
                if (string.IsNullOrEmpty(projectPath))
                {
                    result.success = false;
                    result.message = "项目路径不能为空";
                    AtlasLogger.LogError(result.message);
                    return result;
                }

                projectPath = AtlasPathUtility.NormalizePath(projectPath);

                if (!LodMaxSizeScanner.ValidateProjectFolder(projectPath, out string lodTexturePath, out string texturePath))
                {
                    result.success = false;
                    result.message = $"项目文件夹结构不正确，需要包含 LOD/Texture 和 Texture 子目录: {projectPath}";
                    AtlasLogger.LogError(result.message);
                    return result;
                }

                var matchInfoList = LodMaxSizeScanner.ScanTextures(projectPath, out LodMaxSizeScanResult scanResult);

                result.success = scanResult.success;
                result.message = scanResult.message;
                result.totalCount = scanResult.totalCount;
                result.matchedCount = scanResult.matchedCount;
                result.needsSyncCount = scanResult.needsSyncCount;
                result.matchInfoList = matchInfoList;

                if (result.success)
                {
                    AtlasLogger.Log($"<color=green>{result.message}</color>");
                }
            }
            catch (Exception e)
            {
                result.success = false;
                result.message = $"扫描异常: {e.Message}";
                AtlasLogger.LogError($"{result.message}\n{e.StackTrace}");
            }

            return result;
        }

        /// <summary>
        /// 扫描纹理的 MaxSize 匹配信息（自定义模式）
        /// 可指定任意 LOD 纹理目录和源纹理目录
        /// </summary>
        /// <param name="lodTexturePath">LOD 纹理文件夹路径（Assets/...格式）</param>
        /// <param name="sourceTexturePath">源纹理文件夹路径（Assets/...格式）</param>
        /// <returns>扫描结果，包含所有纹理的匹配信息</returns>
        public static MaxSizeScanResult ScanMaxSizeCustom(string lodTexturePath, string sourceTexturePath)
        {
            var result = new MaxSizeScanResult();

            try
            {
                if (string.IsNullOrEmpty(lodTexturePath))
                {
                    result.success = false;
                    result.message = "LOD纹理路径不能为空";
                    AtlasLogger.LogError(result.message);
                    return result;
                }

                if (string.IsNullOrEmpty(sourceTexturePath))
                {
                    result.success = false;
                    result.message = "源纹理路径不能为空";
                    AtlasLogger.LogError(result.message);
                    return result;
                }

                lodTexturePath = AtlasPathUtility.NormalizePath(lodTexturePath);
                sourceTexturePath = AtlasPathUtility.NormalizePath(sourceTexturePath);

                if (!AtlasPathUtility.IsValidAssetPath(lodTexturePath))
                {
                    result.success = false;
                    result.message = $"LOD纹理路径格式无效，必须以Assets/开头: {lodTexturePath}";
                    AtlasLogger.LogError(result.message);
                    return result;
                }

                if (!AtlasPathUtility.IsValidAssetPath(sourceTexturePath))
                {
                    result.success = false;
                    result.message = $"源纹理路径格式无效，必须以Assets/开头: {sourceTexturePath}";
                    AtlasLogger.LogError(result.message);
                    return result;
                }

                var matchInfoList = LodMaxSizeScanner.ScanTexturesCustomMode(
                    lodTexturePath, sourceTexturePath, out LodMaxSizeScanResult scanResult);

                result.success = scanResult.success;
                result.message = scanResult.message;
                result.totalCount = scanResult.totalCount;
                result.matchedCount = scanResult.matchedCount;
                result.needsSyncCount = scanResult.needsSyncCount;
                result.matchInfoList = matchInfoList;

                if (result.success)
                {
                    AtlasLogger.Log($"<color=green>{result.message}</color>");
                }
            }
            catch (Exception e)
            {
                result.success = false;
                result.message = $"扫描异常: {e.Message}";
                AtlasLogger.LogError($"{result.message}\n{e.StackTrace}");
            }

            return result;
        }

        #endregion

        #region 方法五：LOD MaxSize 同步

        /// <summary>
        /// 同步所有需要修改的纹理 MaxSize
        /// </summary>
        /// <param name="matchInfoList">从扫描方法获取的匹配信息列表</param>
        /// <returns>同步结果</returns>
        public static MaxSizeSyncResult SyncAllMaxSize(List<TextureMaxSizeMatchInfo> matchInfoList)
        {
            var result = new MaxSizeSyncResult();

            try
            {
                if (matchInfoList == null || matchInfoList.Count == 0)
                {
                    result.success = false;
                    result.message = "匹配信息列表为空，请先执行扫描";
                    AtlasLogger.LogError(result.message);
                    return result;
                }

                var syncResult = LodMaxSizeSyncProcessor.ApplyAllChanges(matchInfoList);

                result.success = syncResult.success;
                result.message = syncResult.message;
                result.syncedCount = syncResult.syncedCount;

                if (result.success)
                {
                    AtlasLogger.Log($"<color=green>{result.message}</color>");
                }
            }
            catch (Exception e)
            {
                result.success = false;
                result.message = $"同步异常: {e.Message}";
                AtlasLogger.LogError($"{result.message}\n{e.StackTrace}");
            }

            return result;
        }

        #endregion

        #region 方法六：LOD MaxSize 一键处理

        /// <summary>
        /// 一键执行 LOD MaxSize 扫描和同步（自动模式）
        /// </summary>
        /// <param name="projectPath">项目根文件夹路径（Assets/...格式）</param>
        /// <returns>完整处理结果</returns>
        public static MaxSizeProcessResult ProcessMaxSizeSync(string projectPath)
        {
            var result = new MaxSizeProcessResult();

            try
            {
                AtlasLogger.Log("开始执行 LOD MaxSize 扫描和同步...");

                result.scanResult = ScanProjectMaxSize(projectPath);

                if (!result.scanResult.success)
                {
                    result.success = false;
                    result.message = $"扫描失败: {result.scanResult.message}";
                    AtlasLogger.LogError(result.message);
                    return result;
                }

                if (result.scanResult.needsSyncCount == 0)
                {
                    result.success = true;
                    result.message = $"扫描完成，所有纹理 MaxSize 已同步，无需修改";
                    result.syncResult = new MaxSizeSyncResult
                    {
                        success = true,
                        message = "无需同步",
                        syncedCount = 0
                    };
                    AtlasLogger.Log($"<color=green>{result.message}</color>");
                    return result;
                }

                AtlasLogger.Log($"扫描完成，{result.scanResult.needsSyncCount} 个纹理需要同步，开始同步...");

                result.syncResult = SyncAllMaxSize(result.scanResult.matchInfoList);

                if (!result.syncResult.success)
                {
                    result.success = false;
                    result.message = $"同步失败: {result.syncResult.message}";
                    AtlasLogger.LogError(result.message);
                    return result;
                }

                result.success = true;
                result.message = $"处理完成！扫描了 {result.scanResult.totalCount} 个纹理，同步了 {result.syncResult.syncedCount} 个 MaxSize 设置";

                AtlasLogger.Log($"<color=green>{result.message}</color>");
            }
            catch (Exception e)
            {
                result.success = false;
                result.message = $"处理异常: {e.Message}";
                AtlasLogger.LogError($"{result.message}\n{e.StackTrace}");
            }

            return result;
        }

        /// <summary>
        /// 一键执行 LOD MaxSize 扫描和同步（自定义模式）
        /// </summary>
        /// <param name="lodTexturePath">LOD 纹理文件夹路径（Assets/...格式）</param>
        /// <param name="sourceTexturePath">源纹理文件夹路径（Assets/...格式）</param>
        /// <returns>完整处理结果</returns>
        public static MaxSizeProcessResult ProcessMaxSizeSyncCustom(string lodTexturePath, string sourceTexturePath)
        {
            var result = new MaxSizeProcessResult();

            try
            {
                AtlasLogger.Log("开始执行 LOD MaxSize 扫描和同步（自定义模式）...");

                result.scanResult = ScanMaxSizeCustom(lodTexturePath, sourceTexturePath);

                if (!result.scanResult.success)
                {
                    result.success = false;
                    result.message = $"扫描失败: {result.scanResult.message}";
                    AtlasLogger.LogError(result.message);
                    return result;
                }

                if (result.scanResult.needsSyncCount == 0)
                {
                    result.success = true;
                    result.message = $"扫描完成，所有纹理 MaxSize 已同步，无需修改";
                    result.syncResult = new MaxSizeSyncResult
                    {
                        success = true,
                        message = "无需同步",
                        syncedCount = 0
                    };
                    AtlasLogger.Log($"<color=green>{result.message}</color>");
                    return result;
                }

                AtlasLogger.Log($"扫描完成，{result.scanResult.needsSyncCount} 个纹理需要同步，开始同步...");

                result.syncResult = SyncAllMaxSize(result.scanResult.matchInfoList);

                if (!result.syncResult.success)
                {
                    result.success = false;
                    result.message = $"同步失败: {result.syncResult.message}";
                    AtlasLogger.LogError(result.message);
                    return result;
                }

                result.success = true;
                result.message = $"处理完成！扫描了 {result.scanResult.totalCount} 个纹理，同步了 {result.syncResult.syncedCount} 个 MaxSize 设置";

                AtlasLogger.Log($"<color=green>{result.message}</color>");
            }
            catch (Exception e)
            {
                result.success = false;
                result.message = $"处理异常: {e.Message}";
                AtlasLogger.LogError($"{result.message}\n{e.StackTrace}");
            }

            return result;
        }

        #endregion

        #region 方法七：材质纹理引用扫描

        /// <summary>
        /// 材质纹理引用扫描结果
        /// </summary>
        public class TextureReferenceScanAPIResult
        {
            public bool success;
            public string message;
            public int totalMaterialCount;
            public int totalTextureCount;
            public int multiReferenceTextureCount;
            public int singleReferenceTextureCount;
            public List<TextureReferenceAPIInfo> allTextures = new List<TextureReferenceAPIInfo>();
            public List<TextureReferenceAPIInfo> multiReferencedTextures = new List<TextureReferenceAPIInfo>();
            public List<TextureReferenceAPIInfo> singleReferencedTextures = new List<TextureReferenceAPIInfo>();
        }

        /// <summary>
        /// 纹理引用信息（API版本）
        /// </summary>
        public class TextureReferenceAPIInfo
        {
            public string texturePath;
            public string textureName;
            public int referenceCount;
            public List<MaterialReferenceAPIDetail> referencedByMaterials = new List<MaterialReferenceAPIDetail>();
        }

        /// <summary>
        /// 材质引用详情（API版本）
        /// </summary>
        public class MaterialReferenceAPIDetail
        {
            public string materialPath;
            public string materialName;
            public List<string> propertyNames = new List<string>();
        }

        /// <summary>
        /// 扫描材质纹理引用
        /// 自动识别传入路径类型并选择合适的扫描模式：
        /// - 如果路径是 Material 文件夹，直接扫描该文件夹内的材质
        /// - 如果路径下包含 Material 子文件夹，扫描该子文件夹
        /// - 否则递归扫描所有子目录中的 Material 文件夹
        /// </summary>
        /// <param name="folderPath">文件夹路径（Assets/...格式）</param>
        /// <returns>扫描结果，包含所有纹理引用信息</returns>
        public static TextureReferenceScanAPIResult ScanMaterialTextureReferences(string folderPath)
        {
            var result = new TextureReferenceScanAPIResult();

            try
            {
                if (string.IsNullOrEmpty(folderPath))
                {
                    result.success = false;
                    result.message = "文件夹路径不能为空";
                    AtlasLogger.LogError(result.message);
                    return result;
                }

                folderPath = AtlasPathUtility.NormalizePath(folderPath);

                if (!AtlasPathUtility.IsValidAssetPath(folderPath))
                {
                    result.success = false;
                    result.message = $"路径格式无效，必须以Assets/开头: {folderPath}";
                    AtlasLogger.LogError(result.message);
                    return result;
                }

                if (!UnityEditor.AssetDatabase.IsValidFolder(folderPath))
                {
                    result.success = false;
                    result.message = $"指定路径不是有效文件夹: {folderPath}";
                    AtlasLogger.LogError(result.message);
                    return result;
                }

                TextureReferenceScanResult scanResult;
                string folderName = System.IO.Path.GetFileName(folderPath);

                if (folderName == "Material")
                {
                    AtlasLogger.Log($"检测到 Material 文件夹，直接扫描...");
                    scanResult = MaterialTextureScanner.ScanMaterialFolder(folderPath);
                }
                else
                {
                    string materialSubFolder = System.IO.Path.Combine(folderPath, "Material").Replace("\\", "/");
                    if (UnityEditor.AssetDatabase.IsValidFolder(materialSubFolder))
                    {
                        AtlasLogger.Log($"检测到项目文件夹，扫描其 Material 子目录...");
                        scanResult = MaterialTextureScanner.ScanMaterialFolder(materialSubFolder);
                    }
                    else
                    {
                        AtlasLogger.Log($"递归扫描所有子目录中的 Material 文件夹...");
                        scanResult = MaterialTextureScanner.ScanMultipleProjectFolders(folderPath);
                    }
                }

                result.success = scanResult.success;
                result.message = scanResult.message;
                result.totalMaterialCount = scanResult.totalMaterialCount;
                result.totalTextureCount = scanResult.totalTextureCount;
                result.multiReferenceTextureCount = scanResult.multiReferenceTextureCount;
                result.singleReferenceTextureCount = scanResult.singleReferenceTextureCount;

                foreach (var texInfo in scanResult.allTextures)
                {
                    result.allTextures.Add(ConvertToAPIInfo(texInfo));
                }

                foreach (var texInfo in scanResult.multiReferencedTextures)
                {
                    result.multiReferencedTextures.Add(ConvertToAPIInfo(texInfo));
                }

                foreach (var texInfo in scanResult.singleReferencedTextures)
                {
                    result.singleReferencedTextures.Add(ConvertToAPIInfo(texInfo));
                }

                if (result.success)
                {
                    AtlasLogger.Log($"<color=green>{result.message}</color>");
                }
            }
            catch (Exception e)
            {
                result.success = false;
                result.message = $"扫描异常: {e.Message}";
                AtlasLogger.LogError($"{result.message}\n{e.StackTrace}");
            }

            return result;
        }

        private static TextureReferenceAPIInfo ConvertToAPIInfo(TextureReferenceInfo texInfo)
        {
            var apiInfo = new TextureReferenceAPIInfo
            {
                texturePath = texInfo.texturePath,
                textureName = texInfo.textureName,
                referenceCount = texInfo.referenceCount
            };

            foreach (var matRef in texInfo.referencedByMaterials)
            {
                apiInfo.referencedByMaterials.Add(new MaterialReferenceAPIDetail
                {
                    materialPath = matRef.materialPath,
                    materialName = matRef.materialName,
                    propertyNames = new List<string>(matRef.propertyNames)
                });
            }

            return apiInfo;
        }

        #endregion

        #region 私有辅助方法

        private static bool ValidateAtlasGenerationInput(string sourceTexturePath, string outputAtlasPath, out string errorMsg)
        {
            errorMsg = string.Empty;

            if (string.IsNullOrEmpty(sourceTexturePath))
            {
                errorMsg = "源纹理路径不能为空";
                return false;
            }

            if (string.IsNullOrEmpty(outputAtlasPath))
            {
                errorMsg = "输出路径不能为空";
                return false;
            }

            sourceTexturePath = AtlasPathUtility.NormalizePath(sourceTexturePath);

            if (!AtlasPathUtility.IsValidAssetPath(sourceTexturePath))
            {
                errorMsg = $"源纹理路径格式无效，必须以Assets/开头: {sourceTexturePath}";
                return false;
            }

            if (!AssetDatabase.IsValidFolder(sourceTexturePath))
            {
                errorMsg = $"源纹理路径不是有效文件夹: {sourceTexturePath}";
                return false;
            }

            return true;
        }

        private static bool ValidateMaterialReplacementInput(string sourceMaterialPath, string outputMaterialPath, string atlasConfigPath, out string errorMsg)
        {
            errorMsg = string.Empty;

            if (string.IsNullOrEmpty(sourceMaterialPath))
            {
                errorMsg = "源材质路径不能为空";
                return false;
            }

            if (string.IsNullOrEmpty(outputMaterialPath))
            {
                errorMsg = "材质输出路径不能为空";
                return false;
            }

            if (string.IsNullOrEmpty(atlasConfigPath))
            {
                errorMsg = "图集配置路径不能为空";
                return false;
            }

            sourceMaterialPath = AtlasPathUtility.NormalizePath(sourceMaterialPath);
            atlasConfigPath = AtlasPathUtility.NormalizePath(atlasConfigPath);

            if (!AtlasPathUtility.IsValidAssetPath(sourceMaterialPath))
            {
                errorMsg = $"源材质路径格式无效，必须以Assets/开头: {sourceMaterialPath}";
                return false;
            }

            if (!AssetDatabase.IsValidFolder(sourceMaterialPath))
            {
                errorMsg = $"源材质路径不是有效文件夹: {sourceMaterialPath}";
                return false;
            }

            if (!AtlasPathUtility.IsValidAssetPath(atlasConfigPath))
            {
                errorMsg = $"图集配置路径格式无效，必须以Assets/开头: {atlasConfigPath}";
                return false;
            }

            if (!AssetDatabase.IsValidFolder(atlasConfigPath))
            {
                errorMsg = $"图集配置路径不是有效文件夹: {atlasConfigPath}";
                return false;
            }

            return true;
        }

        private static bool PrepareOutputFolder(string outputPath)
        {
            AtlasPathUtility.DeleteFolderIfExists(outputPath);
            return AtlasPathUtility.EnsureFolderExists(outputPath);
        }

        private static AtlasGeneratorSettings CreateDefaultSettings()
        {
            var settings = ScriptableObject.CreateInstance<AtlasGeneratorSettings>();
            settings.padding = 2;
            settings.maxWastagePercent = 25f;
            settings.minAtlasSize = 32;
            settings.maxAtlasSize = 4096;
            settings.allowMultipleAtlases = true;
            settings.atlasNamePrefix = "Atlas";
            return settings;
        }

        private static string GetProjectNameFromPath(string outputPath)
        {
            string lodPath = System.IO.Path.GetDirectoryName(outputPath);
            string projectPath = System.IO.Path.GetDirectoryName(lodPath);
            return System.IO.Path.GetFileName(projectPath);
        }

        private static int CountReplacedTextures(List<MaterialAtlasReplacer.MaterialProcessResult> results)
        {
            int count = 0;
            if (results == null) return count;

            foreach (var result in results)
            {
                if (result?.replacements == null) continue;
                foreach (var info in result.replacements)
                {
                    if (info.canReplace)
                        count++;
                }
            }
            return count;
        }

        #endregion
    }
}
