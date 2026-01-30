using UnityEngine;

namespace HiHi2.AtlasTools.Editor
{
    public static class MaterialTextureReferenceLogger
    {
        private const string LOG_PREFIX = "[MaterialTextureRef]";
        private const string SEPARATOR = "════════════════════════════════════════════════════════════";

        public static void LogScanResult(TextureReferenceScanResult result)
        {
            if (result == null || !result.success)
            {
                Debug.LogError($"{LOG_PREFIX} 扫描结果无效");
                return;
            }

            Debug.Log($"{LOG_PREFIX} {SEPARATOR}");
            Debug.Log($"{LOG_PREFIX} <color=cyan>【材质纹理引用扫描开始】</color>");
            Debug.Log($"{LOG_PREFIX} 共扫描 {result.totalMaterialCount} 个材质，{result.totalTextureCount} 张纹理");
            Debug.Log($"{LOG_PREFIX} 多次引用: {result.multiReferenceTextureCount} 张，单次引用: {result.singleReferenceTextureCount} 张");
            Debug.Log($"{LOG_PREFIX} {SEPARATOR}");

            foreach (var matResult in result.materialAnalysisResults)
            {
                LogMaterialAnalysis(matResult, result);
            }

            Debug.Log($"{LOG_PREFIX} {SEPARATOR}");
            Debug.Log($"{LOG_PREFIX} <color=cyan>【材质纹理引用扫描结束】</color>");
            Debug.Log($"{LOG_PREFIX} {SEPARATOR}");
        }

        private static void LogMaterialAnalysis(MaterialTextureAnalysisResult matResult, TextureReferenceScanResult scanResult)
        {
            if (matResult.textureProperties.Count == 0) return;

            foreach (var texProp in matResult.textureProperties)
            {
                var texInfo = scanResult.allTextures.Find(t => t.texturePath == texProp.texturePath);
                int refCount = texInfo?.referenceCount ?? 1;

                string message = $"{matResult.materialName} ({matResult.materialPath}):\n" +
                                 $"  纹理: {texProp.texture.name} ({texProp.texturePath})\n" +
                                 $"  属性: {texProp.propertyName}, Count: {refCount}";

                if (refCount > 1)
                {
                    Debug.LogError($"{LOG_PREFIX} <color=red>[多次引用]</color>\n{message}", texProp.texture);
                }
                else
                {
                    Debug.Log($"{LOG_PREFIX} <color=green>[单次引用]</color>\n{message}", texProp.texture);
                }
            }
        }

        public static void LogSingleProject(string projectFolderPath)
        {
            var result = MaterialTextureScanner.ScanProjectFolder(projectFolderPath);
            if (result.success)
            {
                LogScanResult(result);
            }
            else
            {
                Debug.LogError($"{LOG_PREFIX} 扫描失败: {result.message}");
            }
        }

        public static void LogMaterialFolder(string materialFolderPath)
        {
            var result = MaterialTextureScanner.ScanMaterialFolder(materialFolderPath);
            if (result.success)
            {
                LogScanResult(result);
            }
            else
            {
                Debug.LogError($"{LOG_PREFIX} 扫描失败: {result.message}");
            }
        }

        public static void LogMultipleProjects(string rootFolderPath)
        {
            var result = MaterialTextureScanner.ScanMultipleProjectFolders(rootFolderPath);
            if (result.success)
            {
                LogScanResult(result);
            }
            else
            {
                Debug.LogError($"{LOG_PREFIX} 扫描失败: {result.message}");
            }
        }
    }
}