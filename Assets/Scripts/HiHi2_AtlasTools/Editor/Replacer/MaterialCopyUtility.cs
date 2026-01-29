using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

namespace HiHi2.AtlasTools.Editor
{
    public static class MaterialCopyUtility
    {
        public static List<Material> CopyMaterials(List<Material> sourceMaterials, string sourcePath, string outputPath)
        {
            if (!ValidateInput(sourceMaterials, outputPath))
                return null;

            outputPath = AtlasPathUtility.NormalizePath(outputPath);

            if (!PrepareOutputFolder(outputPath))
                return null;

            List<Material> copiedMaterials = CopyMaterialsInternal(sourceMaterials, outputPath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            AtlasLogger.Log($"<color=green>Material copy completed!</color> Copied {copiedMaterials.Count}/{sourceMaterials.Count} materials to {outputPath}");

            return copiedMaterials;
        }

        private static bool ValidateInput(List<Material> sourceMaterials, string outputPath)
        {
            if (sourceMaterials == null || sourceMaterials.Count == 0)
            {
                AtlasLogger.LogError("No materials to copy");
                return false;
            }

            if (string.IsNullOrEmpty(outputPath))
            {
                AtlasLogger.LogError("Output path is empty");
                return false;
            }

            return true;
        }

        private static bool PrepareOutputFolder(string outputPath)
        {
            AtlasPathUtility.DeleteFolderIfExists(outputPath);

            if (!AtlasPathUtility.EnsureFolderExists(outputPath))
            {
                AtlasLogger.LogError($"Failed to create output folder: {outputPath}");
                return false;
            }

            return true;
        }

        private static List<Material> CopyMaterialsInternal(List<Material> sourceMaterials, string outputPath)
        {
            List<Material> copiedMaterials = new List<Material>();
            int successCount = 0;
            int failCount = 0;

            foreach (Material originalMat in sourceMaterials)
            {
                if (originalMat == null)
                {
                    AtlasLogger.LogWarning("Skipping null material in source list");
                    failCount++;
                    continue;
                }

                Material copiedMat = CopySingleMaterial(originalMat, outputPath);
                if (copiedMat != null)
                {
                    copiedMaterials.Add(copiedMat);
                    successCount++;
                }
                else
                {
                    failCount++;
                }
            }

            AtlasLogger.Log($"Material copy result: Success={successCount}, Failed={failCount}");
            return copiedMaterials;
        }

        private static Material CopySingleMaterial(Material originalMat, string outputPath)
        {
            try
            {
                string originalPath = AssetDatabase.GetAssetPath(originalMat);
                if (string.IsNullOrEmpty(originalPath))
                {
                    AtlasLogger.LogError($"Failed to get path for material: {originalMat.name}");
                    return null;
                }

                string fileName = Path.GetFileName(originalPath);
                string newPath = Path.Combine(outputPath, fileName);
                newPath = AtlasPathUtility.NormalizePath(newPath);

                if (!AssetDatabase.CopyAsset(originalPath, newPath))
                {
                    AtlasLogger.LogError($"Failed to copy material: {originalPath} -> {newPath}");
                    return null;
                }

                Material copiedMat = AssetDatabase.LoadAssetAtPath<Material>(newPath);
                if (copiedMat != null)
                {
                    AtlasLogger.Log($"<color=cyan>Copied material:</color> {originalMat.name} -> {newPath}");
                    return copiedMat;
                }
                else
                {
                    AtlasLogger.LogError($"Failed to load copied material from: {newPath}");
                    return null;
                }
            }
            catch (System.Exception e)
            {
                AtlasLogger.LogError($"Exception while copying material {originalMat.name}: {e.Message}");
                return null;
            }
        }
    }
}