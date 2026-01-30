using UnityEditor;

namespace HiHi2.AtlasTools.Editor
{
    public static class MaterialTextureReferenceMenuItems
    {
        [MenuItem("Assets/Lod图及相关工具/材质纹理引用查找/Console输出 - 单项目(某一个部位的某一个物件)", false, 3110)]
        public static void LogSingleProjectToConsole()
        {
            string selectedPath = GetSelectedFolderPath();
            if (string.IsNullOrEmpty(selectedPath))
            {
                EditorUtility.DisplayDialog("提示", "请先选择一个项目文件夹（包含Material子文件夹）。", "确定");
                return;
            }

            MaterialTextureReferenceLogger.LogSingleProject(selectedPath);
        }

        [MenuItem("Assets/Lod图及相关工具/材质纹理引用查找/Console输出 - 单项目(某一个部位的某一个物件)", true)]
        public static bool ValidateLogSingleProjectToConsole()
        {
            return !string.IsNullOrEmpty(GetSelectedFolderPath());
        }

        [MenuItem("Assets/Lod图及相关工具/材质纹理引用查找/Console输出 - Material文件夹", false, 3111)]
        public static void LogMaterialFolderToConsole()
        {
            string selectedPath = GetSelectedFolderPath();
            if (string.IsNullOrEmpty(selectedPath))
            {
                EditorUtility.DisplayDialog("提示", "请先选择Material文件夹。", "确定");
                return;
            }

            MaterialTextureReferenceLogger.LogMaterialFolder(selectedPath);
        }

        [MenuItem("Assets/Lod图及相关工具/材质纹理引用查找/Console输出 - Material文件夹", true)]
        public static bool ValidateLogMaterialFolderToConsole()
        {
            return !string.IsNullOrEmpty(GetSelectedFolderPath());
        }

        [MenuItem("Assets/Lod图及相关工具/材质纹理引用查找/Console输出 - 批量项目", false, 3112)]
        public static void LogMultipleProjectsToConsole()
        {
            string selectedPath = GetSelectedFolderPath();
            if (string.IsNullOrEmpty(selectedPath))
            {
                EditorUtility.DisplayDialog("提示", "请先选择包含多个项目的根目录。", "确定");
                return;
            }

            MaterialTextureReferenceLogger.LogMultipleProjects(selectedPath);
        }

        [MenuItem("Assets/Lod图及相关工具/材质纹理引用查找/Console输出 - 批量项目", true)]
        public static bool ValidateLogMultipleProjectsToConsole()
        {
            return !string.IsNullOrEmpty(GetSelectedFolderPath());
        }

        private static string GetSelectedFolderPath()
        {
            if (Selection.objects.Length == 0) return null;
            string path = AssetDatabase.GetAssetPath(Selection.objects[0]);
            return AssetDatabase.IsValidFolder(path) ? path : null;
        }
    }
}