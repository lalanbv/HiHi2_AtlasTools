using UnityEngine;
using UnityEditor;

namespace HiHi2.AtlasTools.Editor
{
    public enum PathMode
    {
        Auto,
        Custom
    }

    public static class AtlasCommon
    {
        public static string ConvertToRelativePath(string absolutePath)
        {
            if (string.IsNullOrEmpty(absolutePath))
                return string.Empty;
                
            if (!absolutePath.StartsWith(Application.dataPath))
            {
                AtlasLogger.LogWarning($"Path is not inside Assets folder: {absolutePath}");
                return string.Empty;
            }
                
            string relativePath = "Assets" + absolutePath.Substring(Application.dataPath.Length);
            return AtlasPathUtility.NormalizePath(relativePath);
        }

        public static bool IsValidAssetFolder(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;
                
            return AssetDatabase.IsValidFolder(path);
        }
    }
}