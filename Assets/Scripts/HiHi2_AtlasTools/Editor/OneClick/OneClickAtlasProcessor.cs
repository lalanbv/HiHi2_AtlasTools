using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;

namespace HiHi2.AtlasTools.Editor
{
    public static class OneClickAtlasProcessor
    {
        public struct ProcessPaths
        {
            public string textureSource;
            public string materialSource;
            public string atlasOutput;
            public string materialOutput;

            public bool IsValid()
            {
                return !string.IsNullOrEmpty(textureSource) &&
                       !string.IsNullOrEmpty(materialSource) &&
                       !string.IsNullOrEmpty(atlasOutput) &&
                       !string.IsNullOrEmpty(materialOutput);
            }
        }

        public delegate void ProgressCallback(string message, float progress);

        public static bool Execute(ProcessPaths paths, AtlasGeneratorSettings settings, ProgressCallback onProgress)
        {
            if (settings == null)
            {
                ReportError(onProgress, "AtlasGeneratorSettings is null");
                return false;
            }

            try
            {
                onProgress?.Invoke("开始一键处理流程...", 0f);

                if (!ValidatePaths(paths, onProgress))
                    return false;

                bool atlasSuccess = GenerateAtlases(paths, settings, onProgress);
                if (!atlasSuccess)
                    return false;

                bool materialSuccess = ProcessMaterials(paths, onProgress);

                if (materialSuccess)
                {
                    onProgress?.Invoke("✓ 一键处理完成！", 1f);
                    AtlasLogger.Log("<color=green>OneClick atlas generation and material replacement completed successfully!</color>");
                }
                
                return materialSuccess;
            }
            catch (Exception e)
            {
                ReportError(onProgress, $"处理异常: {e.Message}");
                AtlasLogger.LogError($"OneClick process exception: {e.Message}\n{e.StackTrace}");
                return false;
            }
        }

        private static bool ValidatePaths(ProcessPaths paths, ProgressCallback onProgress)
        {
            if (!AssetDatabase.IsValidFolder(paths.textureSource))
            {
                ReportError(onProgress, $"纹理源路径无效: {paths.textureSource}");
                return false;
            }

            if (!AssetDatabase.IsValidFolder(paths.materialSource))
            {
                ReportError(onProgress, $"材质源路径无效: {paths.materialSource}");
                return false;
            }

            return true;
        }

        private static bool GenerateAtlases(ProcessPaths paths, AtlasGeneratorSettings settings, ProgressCallback onProgress)
        {
            onProgress?.Invoke("Step 1/4: 收集纹理文件...", 0.1f);
            
            List<TextureInfo> textures = GenerateOptimizedAtlasEditor.CollectTextures(paths.textureSource, settings.padding);
            
            if (textures == null || textures.Count == 0)
            {
                ReportError(onProgress, $"未在 {paths.textureSource} 找到可用纹理");
                return false;
            }

            onProgress?.Invoke($"Step 1/4: 找到 {textures.Count} 张纹理", 0.2f);

            onProgress?.Invoke("Step 2/4: 准备纹理数据...", 0.25f);
            GenerateOptimizedAtlasEditor.PrepareReadableTextures(textures);

            onProgress?.Invoke("Step 2/4: 执行图集打包算法...", 0.3f);
            List<GenerateOptimizedAtlasEditor.AtlasResult> atlasResults = 
                GenerateOptimizedAtlasEditor.GenerateAtlases(textures, settings);

            if (atlasResults == null || atlasResults.Count == 0)
            {
                GenerateOptimizedAtlasEditor.CleanupTemporaryTextures(textures);
                ReportError(onProgress, "根据当前参数无法生成图集，请调整设置");
                return false;
            }

            onProgress?.Invoke($"Step 2/4: 成功生成 {atlasResults.Count} 张图集", 0.4f);

            onProgress?.Invoke("Step 3/4: 保存图集到磁盘...", 0.5f);
            GenerateOptimizedAtlasEditor.SaveAtlases(atlasResults, paths.atlasOutput, settings);

            GenerateOptimizedAtlasEditor.CleanupTemporaryTextures(textures);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            onProgress?.Invoke($"Step 3/4: 图集已保存到 {paths.atlasOutput}", 0.6f);

            return true;
        }

        private static bool ProcessMaterials(ProcessPaths paths, ProgressCallback onProgress)
        {
            onProgress?.Invoke("Step 4/4: 加载图集配置...", 0.65f);
            
            List<AtlasConfig> atlasConfigs = MaterialAtlasReplacer.CollectAtlasConfigsFromFolder(paths.atlasOutput);

            if (atlasConfigs == null || atlasConfigs.Count == 0)
            {
                onProgress?.Invoke("⚠ 警告: 未找到图集配置文件", 0.7f);
                AtlasLogger.LogWarning($"No atlas configs found in {paths.atlasOutput}");
                return false;
            }

            onProgress?.Invoke($"Step 4/4: 加载了 {atlasConfigs.Count} 个图集配置", 0.7f);

            List<Material> sourceMaterials = MaterialAtlasReplacer.CollectMaterialsFromFolder(paths.materialSource);

            if (sourceMaterials == null || sourceMaterials.Count == 0)
            {
                onProgress?.Invoke("⚠ 警告: 未找到材质文件", 0.75f);
                AtlasLogger.LogWarning($"No materials found in {paths.materialSource}");
                return false;
            }

            onProgress?.Invoke($"Step 4/4: 拷贝 {sourceMaterials.Count} 个材质...", 0.8f);

            AtlasPathUtility.EnsureFolderExists(paths.materialOutput);

            List<Material> copiedMaterials = MaterialCopyUtility.CopyMaterials(
                sourceMaterials, 
                paths.materialSource, 
                paths.materialOutput);

            if (copiedMaterials == null || copiedMaterials.Count == 0)
            {
                ReportError(onProgress, "材质拷贝失败");
                return false;
            }

            onProgress?.Invoke("Step 4/4: 分析材质纹理引用...", 0.85f);

            List<MaterialAtlasReplacer.MaterialProcessResult> results = 
                MaterialAtlasReplacer.AnalyzeMaterials(copiedMaterials, atlasConfigs);

            onProgress?.Invoke("Step 4/4: 执行纹理替换...", 0.9f);
            MaterialAtlasReplacer.ApplyReplacements(results, true);

            int replaceCount = CountReplaceableMaterials(results);
            int textureCount = CountReplaceableTextures(results);

            onProgress?.Invoke($"Step 4/4: 完成！替换了 {replaceCount} 个材质中的 {textureCount} 张纹理", 0.95f);

            return true;
        }

        private static int CountReplaceableMaterials(List<MaterialAtlasReplacer.MaterialProcessResult> results)
        {
            int count = 0;
            foreach (var result in results)
            {
                if (result.hasReplaceableTextures)
                    count++;
            }
            return count;
        }

        private static int CountReplaceableTextures(List<MaterialAtlasReplacer.MaterialProcessResult> results)
        {
            int count = 0;
            foreach (var result in results)
            {
                foreach (var info in result.replacements)
                {
                    if (info.canReplace)
                        count++;
                }
            }
            return count;
        }

        private static void ReportError(ProgressCallback onProgress, string message)
        {
            onProgress?.Invoke($"✗ 错误: {message}", 0f);
            AtlasLogger.LogError(message);
        }
    }
}