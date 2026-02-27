using System.Collections.Generic;
using System.IO;
using UnityEditor;

namespace HiHi2.AtlasTools.Editor
{
    public static class MiniGameMigratorScanner
    {
        public const string SOURCE_BASIC_AVATAR_PATH = "Assets/Art/Avatar/BasicAvatar";
        public const string TARGET_BASIC_AVATAR_PATH = "Assets/Art_MiniGame/Avatar/BasicAvatar";

        public const string LOD_FOLDER_NAME = "LOD";
        public const string MESH_FOLDER_NAME = "Mesh";
        public const string MATERIAL_FOLDER_NAME = "Material";
        public const string ATLAS_MATERIAL_FOLDER_NAME = "AtlasMaterial";
        public const string ATLAS_TEXTURE_FOLDER_NAME = "AtlasTexture";
        public const string LOD_TEXTURE_FOLDER_NAME = "Texture";
        public const string LOD_MESH_SUFFIX = "_70";

        public const string TARGET_MATERIAL_FOLDER = "Material";
        public const string TARGET_TEXTURE_FOLDER = "Texture";
        public const string TARGET_MESH_FOLDER = "Mesh";

        /// <summary>
        /// 结构性文件夹名称
        /// </summary>
        private static readonly HashSet<string> StructuralFolderNames = new HashSet<string>
        {
            LOD_FOLDER_NAME,
            MESH_FOLDER_NAME,
            MATERIAL_FOLDER_NAME,
            ATLAS_MATERIAL_FOLDER_NAME,
            ATLAS_TEXTURE_FOLDER_NAME,
            "Texture",
            "Debug"
        };

        /// <summary>
        /// 已知类别名称
        /// </summary>
        private static readonly string[] KnownCategories = { "Head", "Jacket", "Face", "Bone" };

        /// <summary>
        /// 扫描文件夹
        /// </summary>
        public static List<AvatarObjectInfo> ScanFolder(string folderPath)
        {
            List<AvatarObjectInfo> results = new List<AvatarObjectInfo>();

            if (string.IsNullOrEmpty(folderPath) || !AssetDatabase.IsValidFolder(folderPath))
            {
                AtlasLogger.LogError($"Invalid folder path: {folderPath}");
                return results;
            }

            folderPath = AtlasPathUtility.NormalizePath(folderPath);

            string folderName = Path.GetFileName(folderPath);
            if (StructuralFolderNames.Contains(folderName))
                return results;

            if (IsObjectFolder(folderPath))
            {
                var info = ScanObjectFolder(folderPath);
                if (info != null)
                {
                    results.Add(info);
                }
            }
            else
            {
                string[] subFolders = AssetDatabase.GetSubFolders(folderPath);
                for (int i = 0; i < subFolders.Length; i++)
                {
                    results.AddRange(ScanFolder(subFolders[i]));
                }

                if (results.Count == 0 && HasDirectAssets(folderPath))
                {
                    var info = ScanObjectFolder(folderPath);
                    if (info != null)
                    {
                        results.Add(info);
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// 判断是否为物件文件夹
        /// </summary>
        public static bool IsObjectFolder(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath)) return false;

            string folderName = Path.GetFileName(folderPath);

            if (StructuralFolderNames.Contains(folderName))
            {
                return false;
            }

            string[] subFolders = AssetDatabase.GetSubFolders(folderPath);

            bool hasStructuralSubfolder = false;
            bool hasNonStructuralSubfolder = false;

            for (int i = 0; i < subFolders.Length; i++)
            {
                string subFolderName = Path.GetFileName(subFolders[i]);
                if (StructuralFolderNames.Contains(subFolderName))
                {
                    hasStructuralSubfolder = true;
                }
                else
                {
                    hasNonStructuralSubfolder = true;
                }
            }

            if (hasStructuralSubfolder)
                return true;

            if (hasNonStructuralSubfolder)
                return false;

            return HasDirectAssets(folderPath);
        }

        /// <summary>
        /// 判断文件夹下是否有直接的资源文件
        /// </summary>
        private static bool HasDirectAssets(string folderPath)
        {
            folderPath = AtlasPathUtility.NormalizePath(folderPath);

            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { folderPath });
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                string dir = AtlasPathUtility.NormalizePath(Path.GetDirectoryName(path));
                if (dir == folderPath)
                    return true;
            }

            string[] modelGuids = AssetDatabase.FindAssets("t:Model", new[] { folderPath });
            for (int i = 0; i < modelGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(modelGuids[i]);
                string dir = AtlasPathUtility.NormalizePath(Path.GetDirectoryName(path));
                if (dir == folderPath)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 扫描物件文件夹
        /// </summary>
        public static AvatarObjectInfo ScanObjectFolder(string folderPath)
        {
            folderPath = AtlasPathUtility.NormalizePath(folderPath);

            AvatarObjectInfo info = new AvatarObjectInfo
            {
                sourcePath = folderPath,
                objectName = Path.GetFileName(folderPath)
            };

            info.category = GetCategoryFromPath(folderPath);

            string lodPath = AtlasPathUtility.NormalizePath(Path.Combine(folderPath, LOD_FOLDER_NAME));
            info.hasLodFolder = AssetDatabase.IsValidFolder(lodPath);

            if (info.hasLodFolder)
            {
                info.lodAtlasMaterialPath = AtlasPathUtility.NormalizePath(
                    Path.Combine(lodPath, ATLAS_MATERIAL_FOLDER_NAME));
                info.lodAtlasTexturePath = AtlasPathUtility.NormalizePath(
                    Path.Combine(lodPath, ATLAS_TEXTURE_FOLDER_NAME));

                string lodTexturePath = AtlasPathUtility.NormalizePath(
                    Path.Combine(lodPath, LOD_TEXTURE_FOLDER_NAME));
                string lodMaterialPath = AtlasPathUtility.NormalizePath(
                    Path.Combine(lodPath, MATERIAL_FOLDER_NAME));

                AtlasLogger.Log($"扫描物件: {info.objectName}");
                AtlasLogger.Log($"  LOD Atlas材质路径: {info.lodAtlasMaterialPath}");
                AtlasLogger.Log($"  LOD Atlas纹理路径: {info.lodAtlasTexturePath}");
                AtlasLogger.Log($"  LOD 纹理路径: {lodTexturePath}");
                AtlasLogger.Log($"  LOD 材质路径: {lodMaterialPath}");

                if (AssetDatabase.IsValidFolder(info.lodAtlasMaterialPath))
                {
                    string[] matGuids = AssetDatabase.FindAssets("t:Material", new[] { info.lodAtlasMaterialPath });
                    AddMaterialPaths(info, matGuids, "Atlas");
                }
                else if (AssetDatabase.IsValidFolder(lodMaterialPath))
                {
                    string[] matGuids = AssetDatabase.FindAssets("t:Material", new[] { lodMaterialPath });
                    AddMaterialPaths(info, matGuids, "LOD/Material");
                }
                else
                {
                    AtlasLogger.LogWarning($"  材质文件夹不存在: {info.lodAtlasMaterialPath} 和 {lodMaterialPath}");
                }

                if (AssetDatabase.IsValidFolder(info.lodAtlasTexturePath))
                {
                    string[] texGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { info.lodAtlasTexturePath });
                    ProcessAtlasTextures(info, texGuids);
                }

                if (AssetDatabase.IsValidFolder(lodTexturePath))
                {
                    string[] texGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { lodTexturePath });
                    ProcessLodTextures(info, texGuids);
                }

                if (info.atlasTextures.Count == 0)
                {
                    AtlasLogger.LogWarning($"  未找到任何纹理");
                }
            }

            string meshPath = AtlasPathUtility.NormalizePath(Path.Combine(folderPath, MESH_FOLDER_NAME));
            info.hasMeshFolder = AssetDatabase.IsValidFolder(meshPath);

            if (info.hasMeshFolder)
            {
                string[] meshGuids = AssetDatabase.FindAssets("t:Mesh", new[] { meshPath });
                ProcessMeshes(info, meshGuids);
            }

            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { folderPath });
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                string prefabDir = AtlasPathUtility.NormalizePath(Path.GetDirectoryName(prefabPath));

                if (prefabDir == folderPath)
                {
                    info.hasPrefab = true;
                    info.prefabPath = prefabPath;
                    AtlasLogger.Log($"  找到Prefab: {Path.GetFileName(prefabPath)}");
                    break;
                }
            }

            ValidateObjectInfo(info);

            return info;
        }

        /// <summary>
        /// 添加材质路径到信息对象
        /// </summary>
        private static void AddMaterialPaths(AvatarObjectInfo info, string[] matGuids, string sourceName)
        {
            if (info.atlasMaterials == null)
            {
                info.atlasMaterials = new List<string>();
            }

            for (int i = 0; i < matGuids.Length; i++)
            {
                info.atlasMaterials.Add(AssetDatabase.GUIDToAssetPath(matGuids[i]));
            }

            AtlasLogger.Log($"  从{sourceName}找到材质: {matGuids.Length} 个");
            for (int i = 0; i < info.atlasMaterials.Count; i++)
            {
                AtlasLogger.Log($"    - {Path.GetFileName(info.atlasMaterials[i])}");
            }
        }

        /// <summary>
        /// 处理Atlas纹理
        /// </summary>
        private static void ProcessAtlasTextures(AvatarObjectInfo info, string[] texGuids)
        {
            AtlasLogger.Log($"  找到Atlas纹理: {texGuids.Length} 个");

            for (int i = 0; i < texGuids.Length; i++)
            {
                string texPath = AssetDatabase.GUIDToAssetPath(texGuids[i]);
                string texName = Path.GetFileNameWithoutExtension(texPath);
                info.atlasTextures.Add(texPath);

                AtlasLogger.Log($"    - {Path.GetFileName(texPath)}");

                if (texName.Contains("_lod"))
                {
                    info.lodTextures.Add(texPath);
                }
            }
        }

        /// <summary>
        /// 处理LOD纹理
        /// </summary>
        private static void ProcessLodTextures(AvatarObjectInfo info, string[] texGuids)
        {
            AtlasLogger.Log($"  找到LOD/Texture纹理: {texGuids.Length} 个");

            for (int i = 0; i < texGuids.Length; i++)
            {
                string texPath = AssetDatabase.GUIDToAssetPath(texGuids[i]);
                if (!info.atlasTextures.Contains(texPath))
                {
                    info.atlasTextures.Add(texPath);
                    AtlasLogger.Log($"    - {Path.GetFileName(texPath)} (from LOD/Texture)");
                }
            }
        }

        /// <summary>
        /// 处理Mesh资源
        /// </summary>
        private static void ProcessMeshes(AvatarObjectInfo info, string[] meshGuids)
        {
            AtlasLogger.Log($"  找到Mesh: {meshGuids.Length} 个");

            for (int i = 0; i < meshGuids.Length; i++)
            {
                string meshAssetPath = AssetDatabase.GUIDToAssetPath(meshGuids[i]);
                string meshName = Path.GetFileNameWithoutExtension(meshAssetPath);

                if (meshName.EndsWith(LOD_MESH_SUFFIX))
                {
                    info.hasLodMesh = true;
                    info.lodMeshPath = meshAssetPath;
                    AtlasLogger.Log($"    - {Path.GetFileName(meshAssetPath)} (LOD Mesh)");
                }
                else
                {
                    info.hasOriginalMesh = true;
                    info.originalMeshPath = meshAssetPath;
                    AtlasLogger.Log($"    - {Path.GetFileName(meshAssetPath)} (Original Mesh)");
                }
            }
        }

        /// <summary>
        /// 验证物件信息
        /// </summary>
        private static void ValidateObjectInfo(AvatarObjectInfo info)
        {
            if (!info.hasLodFolder)
            {
                info.isValid = false;
                info.invalidReason = "缺少LOD文件夹";
                return;
            }

            if (!info.hasMeshFolder)
            {
                info.isValid = false;
                info.invalidReason = "缺少Mesh文件夹";
                return;
            }

            if (!info.hasOriginalMesh && !info.hasLodMesh)
            {
                info.isValid = false;
                info.invalidReason = "Mesh文件夹中没有找到有效的Mesh";
                return;
            }

            info.isValid = true;
            info.invalidReason = null;
        }

        /// <summary>
        /// 从路径获取类别
        /// </summary>
        private static string GetCategoryFromPath(string folderPath)
        {
            string normalizedPath = AtlasPathUtility.NormalizePath(folderPath);

            for (int i = 0; i < KnownCategories.Length; i++)
            {
                string category = KnownCategories[i];
                if (normalizedPath.Contains("/" + category + "/") ||
                    normalizedPath.EndsWith("/" + category))
                    return category;
            }

            string parentPath = AtlasPathUtility.NormalizePath(Path.GetDirectoryName(folderPath));
            return Path.GetFileName(parentPath);
        }

        /// <summary>
        /// 获取目标路径
        /// </summary>
        public static string GetTargetPath(AvatarObjectInfo info)
        {
            string relativePath = info.sourcePath;
            if (relativePath.StartsWith(SOURCE_BASIC_AVATAR_PATH))
            {
                relativePath = relativePath.Substring(SOURCE_BASIC_AVATAR_PATH.Length);
            }

            return AtlasPathUtility.NormalizePath(TARGET_BASIC_AVATAR_PATH + relativePath);
        }

        /// <summary>
        /// 获取类别
        /// </summary>
        public static List<string> GetCategories(string rootPath)
        {
            List<string> categories = new List<string>();

            if (!AssetDatabase.IsValidFolder(rootPath))
                return categories;

            string[] subFolders = AssetDatabase.GetSubFolders(rootPath);
            for (int i = 0; i < subFolders.Length; i++)
            {
                categories.Add(Path.GetFileName(subFolders[i]));
            }

            return categories;
        }
    }
}
