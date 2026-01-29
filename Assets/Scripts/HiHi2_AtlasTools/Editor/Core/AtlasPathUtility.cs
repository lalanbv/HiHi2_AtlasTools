using UnityEditor;
using System.IO;

namespace HiHi2.AtlasTools.Editor
{
    public static class AtlasPathUtility
    {
        public static string NormalizePath(string path)
        {
            return string.IsNullOrEmpty(path) ? string.Empty : path.Replace("\\", "/");
        }

        public static bool EnsureFolderExists(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath))
            {
                AtlasLogger.LogError("Folder path is null or empty");
                return false;
            }

            if (AssetDatabase.IsValidFolder(folderPath))
                return true;

            string[] pathParts = folderPath.Split('/');
            if (pathParts.Length == 0)
            {
                AtlasLogger.LogError($"Invalid folder path format: {folderPath}");
                return false;
            }

            string currentPath = pathParts[0];
            
            for (int i = 1; i < pathParts.Length; i++)
            {
                string nextPath = $"{currentPath}/{pathParts[i]}";
                if (!AssetDatabase.IsValidFolder(nextPath))
                {
                    string guid = AssetDatabase.CreateFolder(currentPath, pathParts[i]);
                    if (string.IsNullOrEmpty(guid))
                    {
                        AtlasLogger.LogError($"Failed to create folder: {nextPath}");
                        return false;
                    }
                }
                currentPath = nextPath;
            }
            
            AssetDatabase.Refresh();
            return true;
        }

        public static string GetAutoOutputPath(string sourcePath)
        {
            if (string.IsNullOrEmpty(sourcePath))
            {
                AtlasLogger.LogError("Source path is null or empty");
                return null;
            }

            string parentPath = Path.GetDirectoryName(sourcePath);
            return NormalizePath(Path.Combine(parentPath, AtlasConstants.FOLDER_NAME_LOD, AtlasConstants.FOLDER_NAME_TEXTURE));
        }

        public static string GetAutoMaterialOutputPath(string materialSourcePath)
        {
            if (string.IsNullOrEmpty(materialSourcePath))
            {
                AtlasLogger.LogError("Material source path is null or empty");
                return null;
            }

            string parentPath = Path.GetDirectoryName(materialSourcePath);
            return NormalizePath(Path.Combine(parentPath, AtlasConstants.FOLDER_NAME_LOD, AtlasConstants.FOLDER_NAME_MATERIALS));
        }

        public static bool DeleteFolderIfExists(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath))
                return true;

            if (!AssetDatabase.IsValidFolder(folderPath))
                return true;

            bool result = AssetDatabase.DeleteAsset(folderPath);
            if (result)
            {
                AssetDatabase.Refresh();
                AtlasLogger.Log($"Deleted folder: {folderPath}");
            }
            else
            {
                AtlasLogger.LogWarning($"Failed to delete folder: {folderPath}");
            }
            
            return result;
        }

        public static bool IsValidAssetPath(string path)
        {
            return !string.IsNullOrEmpty(path) && path.StartsWith("Assets/");
        }
    }
}