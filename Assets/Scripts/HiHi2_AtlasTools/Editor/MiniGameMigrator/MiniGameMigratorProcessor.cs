using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace HiHi2.AtlasTools.Editor
{
    /// <summary>
    /// 小游戏资源迁移处理器
    /// 负责将Avatar物件的资源（纹理、材质、Mesh、Prefab）迁移到目标路径
    /// 支持LOD纹理选择、图集模式切换、材质纹理替换等功能
    /// </summary>
    public static class MiniGameMigratorProcessor
    {
        // 资源缓存字典，用于避免重复加载相同资源，提升性能
        private static Dictionary<string, Texture2D> textureCache = new Dictionary<string, Texture2D>();
        private static Dictionary<string, Material> materialCache = new Dictionary<string, Material>();
        private static Dictionary<string, AtlasConfig> atlasConfigCache = new Dictionary<string, AtlasConfig>();
        private static Dictionary<string, Mesh> meshCache = new Dictionary<string, Mesh>();

        /// <summary>
        /// 迁移单个Avatar物件
        /// </summary>
        /// <param name="objectInfo">物件信息，包含源路径、材质、纹理等</param>
        /// <param name="options">迁移选项，包含LOD级别、图集模式等配置</param>
        /// <returns>迁移结果，包含成功状态、复制数量等信息</returns>
        public static MigrationResult MigrateObject(AvatarObjectInfo objectInfo, MigrationOptions options)
        {
            MigrationResult result = new MigrationResult
            {
                objectName = objectInfo.objectName
            };

            // 检查物件是否满足迁移条件
            if (!objectInfo.CanMigrate)
            {
                result.success = false;
                result.message = objectInfo.invalidReason ?? "无法迁移此物件";
                return result;
            }

            try
            {
                // 计算目标路径并记录日志
                string targetPath = MiniGameMigratorScanner.GetTargetPath(objectInfo);
                AtlasLogger.Log($"<color=cyan>开始迁移: {objectInfo.objectName}</color>");
                AtlasLogger.Log($"  源路径: {objectInfo.sourcePath}");
                AtlasLogger.Log($"  目标路径: {targetPath}");

                // 准备目标文件夹（创建或清空）
                if (!PrepareTargetFolder(targetPath, options.overwriteExisting))
                {
                    result.success = false;
                    result.message = $"无法创建目标目录: {targetPath}";
                    return result;
                }

                // 构建各资源类型的目标子路径
                string targetMaterialPath = AtlasPathUtility.NormalizePath(
                    Path.Combine(targetPath, MiniGameMigratorScanner.TARGET_MATERIAL_FOLDER));
                string targetTexturePath = AtlasPathUtility.NormalizePath(
                    Path.Combine(targetPath, MiniGameMigratorScanner.TARGET_TEXTURE_FOLDER));
                string targetMeshPath = AtlasPathUtility.NormalizePath(
                    Path.Combine(targetPath, MiniGameMigratorScanner.TARGET_MESH_FOLDER));

                AtlasLogger.Log($"  材质目标路径: {targetMaterialPath}");
                AtlasLogger.Log($"  纹理目标路径: {targetTexturePath}");
                AtlasLogger.Log($"  Mesh目标路径: {targetMeshPath}");

                // 确保各资源目录存在
                AtlasPathUtility.EnsureFolderExists(targetMaterialPath);
                AtlasPathUtility.EnsureFolderExists(targetTexturePath);
                AtlasPathUtility.EnsureFolderExists(targetMeshPath);

                // 纹理映射表：记录原始纹理名称 -> 目标纹理路径，用于后续材质替换
                Dictionary<string, string> textureMapping = new Dictionary<string, string>();

                // 步骤1: 复制纹理（根据LOD级别和图集模式筛选）
                result.copiedTextureCount = CopyLodTextures(objectInfo, targetTexturePath, options.lodLevel, options.atlasTextureMode, textureMapping);
                AtlasLogger.Log($"  复制纹理数量: {result.copiedTextureCount}");

                // 步骤2: 复制材质并替换其中的纹理引用
                List<Material> copiedMaterials = CopyAndProcessMaterials(objectInfo, targetMaterialPath, textureMapping);
                result.copiedMaterialCount = copiedMaterials.Count;
                result.replacedTextureCount = textureMapping.Count;
                AtlasLogger.Log($"  复制材质数量: {result.copiedMaterialCount}");

                // 步骤3: 复制Mesh（根据MeshType选择原始Mesh或LOD Mesh）
                string copiedMeshPath = CopyMesh(objectInfo, targetMeshPath, options.meshType);
                if (!string.IsNullOrEmpty(copiedMeshPath))
                {
                    result.outputMeshPath = copiedMeshPath;
                    AtlasLogger.Log($"  复制Mesh: {copiedMeshPath}");
                }

                // 步骤4: 创建优化后的Prefab（可选）
                if (options.createPrefab && objectInfo.hasPrefab)
                {
                    // 确定Prefab使用的Mesh路径
                    string meshPathForPrefab = copiedMeshPath;
                    if (string.IsNullOrEmpty(meshPathForPrefab))
                    {
                        meshPathForPrefab = options.meshType == MeshType.Lod && objectInfo.hasLodMesh
                            ? objectInfo.lodMeshPath
                            : objectInfo.originalMeshPath;
                    }

                    result.prefabCreated = CreateOptimizedPrefab(
                        objectInfo,
                        targetPath,
                        copiedMaterials,
                        meshPathForPrefab,
                        out string prefabPath);
                    result.outputPrefabPath = prefabPath;
                    AtlasLogger.Log($"  Prefab创建: {(result.prefabCreated ? "成功" : "失败")}");
                }

                // 保存并刷新资源数据库
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                result.success = true;
                result.message = $"迁移完成: 复制了 {result.copiedMaterialCount} 个材质, {result.copiedTextureCount} 张纹理";

                AtlasLogger.Log($"<color=green>[{objectInfo.objectName}] {result.message}</color>");
            }
            catch (System.Exception e)
            {
                result.success = false;
                result.message = $"迁移异常: {e.Message}";
                AtlasLogger.LogError($"[{objectInfo.objectName}] {e.Message}\n{e.StackTrace}");
            }

            return result;
        }

        /// <summary>
        /// 准备目标文件夹
        /// 如果文件夹已存在且允许覆盖，则删除后重建
        /// </summary>
        /// <param name="targetPath">目标路径</param>
        /// <param name="overwrite">是否覆盖已存在的文件夹</param>
        /// <returns>是否准备成功</returns>
        private static bool PrepareTargetFolder(string targetPath, bool overwrite)
        {
            targetPath = AtlasPathUtility.NormalizePath(targetPath);

            if (AssetDatabase.IsValidFolder(targetPath))
            {
                if (overwrite)
                {
                    AtlasPathUtility.DeleteFolderIfExists(targetPath);
                }
                else
                {
                    return true;
                }
            }

            // 创建目标文件夹及其父目录
            string parentPath = AtlasPathUtility.NormalizePath(Path.GetDirectoryName(targetPath));
            string folderName = Path.GetFileName(targetPath);

            AtlasPathUtility.EnsureFolderExists(parentPath);

            return AtlasPathUtility.EnsureFolderExists(targetPath);
        }

        /// <summary>
        /// 复制Mesh资源
        /// 根据MeshType选择复制原始Mesh或LOD Mesh，文件名添加_mini后缀
        /// </summary>
        /// <param name="objectInfo">物件信息</param>
        /// <param name="targetMeshPath">目标Mesh路径</param>
        /// <param name="meshType">Mesh类型（原始或LOD）</param>
        /// <returns>复制的Mesh路径，失败返回null</returns>
        private static string CopyMesh(AvatarObjectInfo objectInfo, string targetMeshPath, MeshType meshType)
        {
            // 根据MeshType选择源Mesh路径
            string sourceMeshPath = meshType == MeshType.Lod && objectInfo.hasLodMesh
                ? objectInfo.lodMeshPath
                : objectInfo.originalMeshPath;

            if (string.IsNullOrEmpty(sourceMeshPath))
            {
                AtlasLogger.LogWarning("  没有可用的Mesh源路径");
                return null;
            }

            // 生成带_mini后缀的文件名
            string fileNameWithoutExt = Path.GetFileNameWithoutExtension(sourceMeshPath);
            string extension = Path.GetExtension(sourceMeshPath);
            string meshFileName = $"{fileNameWithoutExt}_mini{extension}";

            string targetMeshFilePath = AtlasPathUtility.NormalizePath(
                Path.Combine(targetMeshPath, meshFileName));

            // 如果目标文件已存在，先删除
            if (File.Exists(targetMeshFilePath.Replace("Assets", Application.dataPath.Replace("/Assets", ""))))
            {
                if (!AssetDatabase.DeleteAsset(targetMeshFilePath))
                {
                    AtlasLogger.LogWarning($"    无法覆盖已存在的Mesh: {targetMeshFilePath}");
                }
            }

            // 复制Mesh资源
            if (!AssetDatabase.CopyAsset(sourceMeshPath, targetMeshFilePath))
            {
                AtlasLogger.LogWarning($"    无法复制Mesh: {sourceMeshPath} -> {targetMeshFilePath}");
                return null;
            }

            AtlasLogger.Log($"    <color=green>✓</color> 复制Mesh: {meshFileName}");
            return targetMeshFilePath;
        }

        /// <summary>
        /// 复制LOD纹理
        /// 根据LOD级别和图集模式筛选并复制纹理，跳过已打包进图集的散图
        /// </summary>
        /// <param name="objectInfo">物件信息</param>
        /// <param name="targetTexturePath">目标纹理路径</param>
        /// <param name="lodLevel">LOD级别</param>
        /// <param name="atlasTextureMode">图集纹理模式（LOD或原始）</param>
        /// <param name="textureMapping">输出参数，记录纹理名称到目标路径的映射</param>
        /// <returns>复制的纹理数量</returns>
        private static int CopyLodTextures(AvatarObjectInfo objectInfo, string targetTexturePath, LodLevel lodLevel, AtlasTextureMode atlasTextureMode, Dictionary<string, string> textureMapping)
        {
            int count = 0;
            string lodSuffix = $"_lod{(int)lodLevel}";

            AtlasLogger.Log($"  开始复制纹理，图集模式: {atlasTextureMode}, LOD后缀: {lodSuffix}");
            AtlasLogger.Log($"  可用纹理总数: {objectInfo.atlasTextures.Count}");

            // 收集所有图集配置文件中的散图路径（这些散图不需要单独复制）
            HashSet<string> packedTexturePaths = new HashSet<string>();
            Dictionary<string, string> atlasConfigToTextureMap = new Dictionary<string, string>(); // 图集配置 -> 图集纹理路径
            // 预扫描：收集所有LOD纹理的基础名称（如 fh_0001_0_lod1.png -> fh_0001_0）
            HashSet<string> lodTextureBaseNames = new HashSet<string>();

            Dictionary<string, string> normalizedPathCache = new Dictionary<string, string>();

            // 预扫描：识别哪些基础纹理有对应的LOD版本
            foreach (string sourceTexPath in objectInfo.atlasTextures)
            {
                string texName = Path.GetFileNameWithoutExtension(sourceTexPath);

                // 检查是否是LOD纹理（包含_lod后缀）
                if (texName.Contains("_lod"))
                {
                    // 提取基础名称（移除 _lodX 后缀）
                    string baseName = System.Text.RegularExpressions.Regex.Replace(texName, @"_lod\d+$", "");
                    lodTextureBaseNames.Add(baseName);
                }
            }

            AtlasLogger.Log($"  发现 {lodTextureBaseNames.Count} 个基础纹理有LOD版本");

            // 收集需要加载的图集配置文件路径
            List<string> configPathsToLoad = new List<string>();
            foreach (string sourceTexPath in objectInfo.atlasTextures)
            {
                string configPath = GetAtlasConfigPath(sourceTexPath);
                if (!string.IsNullOrEmpty(configPath) && !atlasConfigToTextureMap.ContainsKey(configPath))
                {
                    configPathsToLoad.Add(configPath);
                    atlasConfigToTextureMap[configPath] = sourceTexPath;
                }
            }

            // 加载图集配置，收集其中包含的散图路径
            foreach (string configPath in configPathsToLoad)
            {
                AtlasConfig atlasConfig = LoadAtlasConfigCached(configPath);
                if (atlasConfig != null && atlasConfig.spriteInfos != null)
                {
                    foreach (var spriteInfo in atlasConfig.spriteInfos)
                    {
                        if (!string.IsNullOrEmpty(spriteInfo.sourceTexturePath))
                        {
                            string normalizedPath;
                            if (!normalizedPathCache.TryGetValue(spriteInfo.sourceTexturePath, out normalizedPath))
                            {
                                normalizedPath = AtlasPathUtility.NormalizePath(spriteInfo.sourceTexturePath);
                                normalizedPathCache[spriteInfo.sourceTexturePath] = normalizedPath;
                            }

                            packedTexturePaths.Add(normalizedPath);
                            AtlasLogger.Log($"    图集 '{Path.GetFileName(configPath)}' 包含散图: {spriteInfo.sourceTexturePath}");
                        }
                    }
                }
            }

            AtlasLogger.Log($"  发现 {packedTexturePaths.Count} 张散图已被打包进图集");

            // 收集需要复制的纹理
            List<TextureCopyInfo> texturesToCopy = new List<TextureCopyInfo>();

            // 遍历所有纹理，根据规则筛选
            foreach (string sourceTexPath in objectInfo.atlasTextures)
            {
                string texName = Path.GetFileNameWithoutExtension(sourceTexPath);
                string texExt = Path.GetExtension(sourceTexPath);

                bool isLodTexture = texName.Contains("_lod");
                bool matchesLod = texName.Contains(lodSuffix);

                // 跳过图集配置文件（.asset）
                if (texExt.Equals(".asset", System.StringComparison.OrdinalIgnoreCase))
                {
                    AtlasLogger.Log($"    跳过图集配置文件: {texName}");
                    continue;
                }

                // 根据图集模式决定处理逻辑
                if (atlasTextureMode == AtlasTextureMode.Lod)
                {
                    // LOD模式：使用带_lod后缀的图集

                    // 如果是LOD纹理但不匹配所选级别，跳过
                    if (isLodTexture && !matchesLod)
                    {
                        AtlasLogger.Log($"    跳过纹理（不匹配LOD后缀）: {texName}");
                        continue;
                    }

                    // 如果当前纹理是原始图（非LOD），且有对应的LOD版本，则跳过（使用LOD版本替代）
                    if (!isLodTexture)
                    {
                        if (lodTextureBaseNames.Contains(texName))
                        {
                            AtlasLogger.Log($"    跳过原始纹理（有对应LOD版本）: {texName}");
                            continue;
                        }
                    }
                }
                else
                {
                    // 原始图集模式：使用无_lod后缀的图集

                    // 跳过所有带LOD后缀的纹理
                    if (isLodTexture)
                    {
                        AtlasLogger.Log($"    跳过LOD纹理（使用原始图集模式）: {texName}");
                        continue;
                    }
                }

                // 检查是否是散图（非图集纹理）
                string normalizedTexPath;
                if (!normalizedPathCache.TryGetValue(sourceTexPath, out normalizedTexPath))
                {
                    normalizedTexPath = AtlasPathUtility.NormalizePath(sourceTexPath);
                    normalizedPathCache[sourceTexPath] = normalizedTexPath;
                }

                // 检查该纹理是否是已被打包进图集的散图
                // 散图的特征：在 packedTexturePaths 中，且不是图集纹理（图集纹理包含 _0_lod, _1_lod 等模式）
                bool isAtlasTexture = System.Text.RegularExpressions.Regex.IsMatch(texName, @"_\d+_lod\d+$");
                bool isPackedSprite = packedTexturePaths.Contains(normalizedTexPath) && !isAtlasTexture;

                if (isPackedSprite)
                {
                    AtlasLogger.Log($"    跳过散图（已打包进图集）: {texName}");
                    continue;
                }

                string targetTexPath = AtlasPathUtility.NormalizePath(
                    Path.Combine(targetTexturePath, texName + texExt));

                texturesToCopy.Add(new TextureCopyInfo
                {
                    sourcePath = sourceTexPath,
                    targetPath = targetTexPath,
                    texName = texName,
                    texExt = texExt
                });
            }

            // 执行纹理复制
            foreach (var copyInfo in texturesToCopy)
            {
                if (!AssetDatabase.CopyAsset(copyInfo.sourcePath, copyInfo.targetPath))
                {
                    AtlasLogger.LogWarning($"    无法复制纹理: {copyInfo.sourcePath} -> {copyInfo.targetPath}");
                    continue;
                }

                // 构建纹理映射（移除LOD后缀后的基础名称）
                string baseNameWithoutLod = copyInfo.texName;
                if (baseNameWithoutLod.Contains("_lod"))
                {
                    baseNameWithoutLod = System.Text.RegularExpressions.Regex.Replace(baseNameWithoutLod, @"_lod\d+$", "");
                }

                // 记录映射关系，用于后续材质纹理替换
                textureMapping[baseNameWithoutLod] = copyInfo.targetPath;
                textureMapping[baseNameWithoutLod + copyInfo.texExt] = copyInfo.targetPath;

                AtlasLogger.Log($"    <color=green>✓</color> 复制纹理: {Path.GetFileName(copyInfo.sourcePath)} -> {Path.GetFileName(copyInfo.targetPath)}");
                AtlasLogger.Log($"      映射: [{baseNameWithoutLod}] -> [{copyInfo.texName}]");
                count++;
            }

            AtlasLogger.Log($"  纹理复制完成，总计: {count} 个");
            return count;
        }

        /// <summary>
        /// 获取图集纹理对应的配置文件路径
        /// 图集纹理命名格式: xxx_0_lod1.png -> 配置文件: xxx_0.asset
        /// </summary>
        /// <param name="texturePath">纹理路径</param>
        /// <returns>图集配置文件路径，如果不符合格式返回null</returns>
        private static string GetAtlasConfigPath(string texturePath)
        {
            // 图集纹理命名格式: xxx_0_lod1.png -> 配置文件: xxx_0.asset
            string texName = Path.GetFileNameWithoutExtension(texturePath);

            // 移除 _lodX 后缀来获取基础名称
            string baseName = System.Text.RegularExpressions.Regex.Replace(texName, @"_lod\d+$", "");

            // 检查是否符合图集命名格式（以 _数字 结尾）
            if (!System.Text.RegularExpressions.Regex.IsMatch(baseName, @"_\d+$"))
            {
                return null;
            }

            string directory = Path.GetDirectoryName(texturePath);
            string configName = baseName + ".asset";
            string configPath = AtlasPathUtility.NormalizePath(Path.Combine(directory, configName));

            if (atlasConfigCache.ContainsKey(configPath) || AssetDatabase.LoadAssetAtPath<AtlasConfig>(configPath) != null)
            {
                return configPath;
            }

            return null;
        }

        /// <summary>
        /// 复制并处理材质
        /// 复制材质到目标路径，并根据纹理映射表替换材质中的纹理引用
        /// </summary>
        /// <param name="objectInfo">物件信息</param>
        /// <param name="targetMaterialPath">目标材质路径</param>
        /// <param name="textureMapping">纹理名称到目标路径的映射</param>
        /// <returns>复制并处理后的材质列表</returns>
        private static List<Material> CopyAndProcessMaterials(AvatarObjectInfo objectInfo, string targetMaterialPath, Dictionary<string, string> textureMapping)
        {
            List<Material> copiedMaterials = new List<Material>();

            AtlasLogger.Log($"  开始复制材质，材质总数: {objectInfo.atlasMaterials.Count}");

            List<MaterialCopyInfo> materialsToCopy = new List<MaterialCopyInfo>();

            // 收集需要复制的材质信息
            foreach (string sourceMatPath in objectInfo.atlasMaterials)
            {
                string matName = Path.GetFileName(sourceMatPath);
                string targetMatPath = AtlasPathUtility.NormalizePath(
                    Path.Combine(targetMaterialPath, matName));

                materialsToCopy.Add(new MaterialCopyInfo
                {
                    sourcePath = sourceMatPath,
                    targetPath = targetMatPath,
                    matName = matName
                });
            }

            // 第一步：复制所有材质文件
            foreach (var copyInfo in materialsToCopy)
            {
                if (!AssetDatabase.CopyAsset(copyInfo.sourcePath, copyInfo.targetPath))
                {
                    AtlasLogger.LogWarning($"    无法复制材质: {copyInfo.sourcePath} -> {copyInfo.targetPath}");
                    continue;
                }
            }

            // 强制同步导入，确保材质可以被正确加载
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            // 第二步：加载复制的材质并替换纹理引用
            foreach (var copyInfo in materialsToCopy)
            {
                Material mat = LoadMaterialCached(copyInfo.targetPath);
                if (mat == null)
                {
                    AtlasLogger.LogWarning($"    无法加载材质: {copyInfo.targetPath}");
                    continue;
                }

                // 替换材质中的纹理引用
                int replacedCount = ReplaceMaterialTextures(mat, textureMapping);

                EditorUtility.SetDirty(mat);
                copiedMaterials.Add(mat);

                AtlasLogger.Log($"    <color=green>✓</color> 复制材质: {copyInfo.matName}, 替换了 {replacedCount} 张纹理");
            }

            AssetDatabase.SaveAssets();
            AtlasLogger.Log($"  材质复制完成，总计: {copiedMaterials.Count} 个");
            return copiedMaterials;
        }

        /// <summary>
        /// 替换材质中的纹理引用
        /// 根据纹理映射表，将材质中的原始纹理替换为复制后的新纹理
        /// </summary>
        /// <param name="material">目标材质</param>
        /// <param name="textureMapping">纹理名称到目标路径的映射</param>
        /// <returns>替换的纹理数量</returns>
        private static int ReplaceMaterialTextures(Material material, Dictionary<string, string> textureMapping)
        {
            if (material == null || material.shader == null)
                return 0;

            int replacedCount = 0;
            Shader shader = material.shader;
            int propertyCount = ShaderUtil.GetPropertyCount(shader);

            List<TexturePropertyInfo> textureProperties = new List<TexturePropertyInfo>();

            // 收集材质中所有纹理属性
            for (int i = 0; i < propertyCount; i++)
            {
                if (ShaderUtil.GetPropertyType(shader, i) != ShaderUtil.ShaderPropertyType.TexEnv)
                    continue;

                string propName = ShaderUtil.GetPropertyName(shader, i);
                Texture tex = material.GetTexture(propName);

                if (tex == null)
                    continue;

                textureProperties.Add(new TexturePropertyInfo
                {
                    propertyName = propName,
                    textureName = tex.name
                });
            }

            // 遍历纹理属性，尝试替换为新纹理
            foreach (var propInfo in textureProperties)
            {
                string texName = propInfo.textureName;
                string newTexPath = FindTextureMapping(texName, textureMapping);

                if (!string.IsNullOrEmpty(newTexPath))
                {
                    Texture2D newTex = LoadTextureCached(newTexPath);
                    if (newTex != null)
                    {
                        material.SetTexture(propInfo.propertyName, newTex);
                        AtlasLogger.Log($"      替换属性 [{propInfo.propertyName}]: {texName} -> {newTex.name}");
                        replacedCount++;
                    }
                    else
                    {
                        AtlasLogger.LogWarning($"      无法加载纹理: {newTexPath}");
                    }
                }
                else
                {
                    AtlasLogger.LogWarning($"      未找到纹理映射: {texName}");
                }
            }

            return replacedCount;
        }

        /// <summary>
        /// 创建优化后的Prefab
        /// 实例化源Prefab，替换其中的材质和Mesh引用，保存为新的Prefab
        /// </summary>
        /// <param name="objectInfo">物件信息</param>
        /// <param name="targetPath">目标路径</param>
        /// <param name="materials">已复制的材质列表</param>
        /// <param name="meshPath">Mesh路径</param>
        /// <param name="prefabPath">输出参数，新Prefab的路径</param>
        /// <returns>是否创建成功</returns>
        private static bool CreateOptimizedPrefab(
            AvatarObjectInfo objectInfo,
            string targetPath,
            List<Material> materials,
            string meshPath,
            out string prefabPath)
        {
            prefabPath = null;

            // 加载源Prefab
            GameObject sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(objectInfo.prefabPath);
            if (sourcePrefab == null)
            {
                AtlasLogger.LogWarning($"无法加载源Prefab: {objectInfo.prefabPath}");
                return false;
            }

            // 实例化Prefab
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(sourcePrefab);
            if (instance == null)
            {
                AtlasLogger.LogWarning($"无法实例化Prefab: {objectInfo.prefabPath}");
                return false;
            }

            try
            {
                // 构建材质映射字典（路径和名称两种索引方式）
                Dictionary<string, Material> materialPathDict = new Dictionary<string, Material>();
                Dictionary<string, Material> materialNameDict = new Dictionary<string, Material>();

                foreach (Material mat in materials)
                {
                    string matPath = AssetDatabase.GetAssetPath(mat);
                    materialPathDict[matPath] = mat;

                    string matBaseName = mat.name.ToLower().Replace(" (instance)", "");
                    materialNameDict[matBaseName] = mat;
                    materialNameDict[mat.name.ToLower()] = mat;

                    AtlasLogger.Log($"  准备材质映射: {mat.name} (路径: {matPath})");
                }

                // 替换实例中的材质引用
                int replacedMatCount = ReplaceInstanceMaterials(instance, objectInfo, materialPathDict, materialNameDict);
                AtlasLogger.Log($"  替换了 {replacedMatCount} 个材质引用");

                // 替换实例中的Mesh引用
                if (!string.IsNullOrEmpty(meshPath))
                {
                    int replacedMeshCount = ReplaceInstanceMesh(instance, meshPath);
                    AtlasLogger.Log($"  替换了 {replacedMeshCount} 个Mesh引用");
                }

                // 保存新的Prefab
                string newPrefabName = objectInfo.objectName + ".prefab";
                prefabPath = AtlasPathUtility.NormalizePath(Path.Combine(targetPath, newPrefabName));

                GameObject newPrefab = PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);

                AtlasLogger.Log($"  创建Prefab: {prefabPath}");

                return newPrefab != null;
            }
            finally
            {
                // 清理实例对象
                Object.DestroyImmediate(instance);
            }
        }

        /// <summary>
        /// 替换Prefab实例中的材质引用
        /// 遍历所有Renderer，将原始材质替换为复制后的新材质
        /// </summary>
        /// <param name="instance">Prefab实例</param>
        /// <param name="objectInfo">物件信息</param>
        /// <param name="materialPathDict">材质路径映射</param>
        /// <param name="materialNameDict">材质名称映射</param>
        /// <returns>替换的材质数量</returns>
        private static int ReplaceInstanceMaterials(GameObject instance, AvatarObjectInfo objectInfo, Dictionary<string, Material> materialPathDict, Dictionary<string, Material> materialNameDict)
        {
            int replacedCount = 0;
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);

            foreach (Renderer renderer in renderers)
            {
                Material[] sharedMats = renderer.sharedMaterials;
                bool changed = false;

                for (int i = 0; i < sharedMats.Length; i++)
                {
                    if (sharedMats[i] == null)
                        continue;

                    Material originalMat = sharedMats[i];

                    string currentMatPath = AssetDatabase.GetAssetPath(originalMat);

                    Material matchedMaterial = null;

                    // 首先尝试通过源材质列表匹配
                    for (int j = 0; j < objectInfo.atlasMaterials.Count; j++)
                    {
                        string sourceMatPath = objectInfo.atlasMaterials[j];
                        if (currentMatPath == sourceMatPath ||
                            Path.GetFileNameWithoutExtension(currentMatPath) == Path.GetFileNameWithoutExtension(sourceMatPath))
                        {
                            if (j < materialPathDict.Count)
                            {
                                string sourceMatName = Path.GetFileNameWithoutExtension(sourceMatPath).ToLower();
                                if (materialNameDict.TryGetValue(sourceMatName, out matchedMaterial))
                                {
                                    break;
                                }
                            }
                        }
                    }

                    // 如果未匹配，尝试通过名称匹配
                    if (matchedMaterial == null)
                    {
                        string matName = originalMat.name.ToLower().Replace(" (instance)", "");

                        if (materialNameDict.TryGetValue(matName, out matchedMaterial))
                        {
                            // 找到精确匹配
                        }
                        else
                        {
                            // 尝试模糊匹配（包含关系）
                            foreach (var kvp in materialNameDict)
                            {
                                string keyLower = kvp.Key;
                                if (matName == keyLower || matName.Contains(keyLower) || keyLower.Contains(matName))
                                {
                                    matchedMaterial = kvp.Value;
                                    break;
                                }
                            }
                        }
                    }

                    // 执行替换
                    if (matchedMaterial != null)
                    {
                        sharedMats[i] = matchedMaterial;
                        changed = true;
                        replacedCount++;
                        AtlasLogger.Log($"    匹配材质: {originalMat.name} -> {matchedMaterial.name}");
                    }
                    else
                    {
                        AtlasLogger.LogWarning($"    未找到材质匹配: {originalMat.name} (路径: {currentMatPath})");
                    }
                }

                if (changed)
                {
                    renderer.sharedMaterials = sharedMats;
                }
            }

            return replacedCount;
        }

        /// <summary>
        /// 替换Prefab实例中的Mesh引用
        /// 遍历所有MeshFilter和SkinnedMeshRenderer，替换为新的Mesh
        /// </summary>
        /// <param name="instance">Prefab实例</param>
        /// <param name="meshPath">新Mesh的路径</param>
        /// <returns>替换的Mesh数量</returns>
        private static int ReplaceInstanceMesh(GameObject instance, string meshPath)
        {
            int replacedCount = 0;

            Mesh newMesh = LoadMeshCached(meshPath);
            if (newMesh == null)
            {
                AtlasLogger.LogWarning($"无法加载Mesh: {meshPath}");
                return 0;
            }

            // 替换MeshFilter中的Mesh
            MeshFilter[] meshFilters = instance.GetComponentsInChildren<MeshFilter>(true);
            foreach (MeshFilter mf in meshFilters)
            {
                if (mf.sharedMesh != null)
                {
                    mf.sharedMesh = newMesh;
                    replacedCount++;
                }
            }

            // 替换SkinnedMeshRenderer中的Mesh
            SkinnedMeshRenderer[] skinnedRenderers = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (SkinnedMeshRenderer smr in skinnedRenderers)
            {
                if (smr.sharedMesh != null)
                {
                    smr.sharedMesh = newMesh;
                    replacedCount++;
                }
            }

            return replacedCount;
        }

        /// <summary>
        /// 批量迁移多个物件
        /// </summary>
        /// <param name="objectInfos">物件信息列表</param>
        /// <param name="options">迁移选项</param>
        /// <returns>批量迁移结果</returns>
        public static BatchMigrationResult MigrateBatch(List<AvatarObjectInfo> objectInfos, MigrationOptions options)
        {
            ClearAllCaches();

            BatchMigrationResult result = new BatchMigrationResult
            {
                totalCount = objectInfos.Count
            };

            bool needsAssetRefresh = false;

            // 遍历处理每个物件
            foreach (var info in objectInfos)
            {
                if (!info.CanMigrate)
                {
                    result.skippedCount++;
                    result.results.Add(new MigrationResult
                    {
                        objectName = info.objectName,
                        success = false,
                        message = info.invalidReason ?? "跳过: 不满足迁移条件"
                    });
                    continue;
                }

                MigrationResult singleResult = MigrateObject(info, options);
                result.results.Add(singleResult);

                if (singleResult.success)
                {
                    result.successCount++;
                    needsAssetRefresh = true;
                }
                else
                    result.failedCount++;
            }

            // 批量操作结束后统一刷新资源数据库
            if (needsAssetRefresh)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            result.success = result.failedCount == 0;
            result.message = $"批量迁移完成: 成功 {result.successCount}, 失败 {result.failedCount}, 跳过 {result.skippedCount}";

            AtlasLogger.Log($"<color=green>{result.message}</color>");

            ClearAllCaches();

            return result;
        }

        #region 资源缓存加载方法

        /// <summary>
        /// 从缓存加载纹理，如果不存在则从AssetDatabase加载
        /// </summary>
        private static Texture2D LoadTextureCached(string path)
        {
            if (textureCache.TryGetValue(path, out Texture2D cachedTexture))
            {
                return cachedTexture;
            }

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture != null)
            {
                textureCache[path] = texture;
            }

            return texture;
        }

        /// <summary>
        /// 从缓存加载材质，如果不存在则从AssetDatabase加载
        /// </summary>
        private static Material LoadMaterialCached(string path)
        {
            if (materialCache.TryGetValue(path, out Material cachedMaterial))
            {
                return cachedMaterial;
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
            {
                materialCache[path] = material;
            }

            return material;
        }

        /// <summary>
        /// 从缓存加载图集配置，如果不存在则从AssetDatabase加载
        /// </summary>
        private static AtlasConfig LoadAtlasConfigCached(string path)
        {
            if (atlasConfigCache.TryGetValue(path, out AtlasConfig cachedConfig))
            {
                return cachedConfig;
            }

            AtlasConfig config = AssetDatabase.LoadAssetAtPath<AtlasConfig>(path);
            if (config != null)
            {
                atlasConfigCache[path] = config;
            }

            return config;
        }

        /// <summary>
        /// 从缓存加载Mesh，如果不存在则从AssetDatabase加载
        /// </summary>
        private static Mesh LoadMeshCached(string path)
        {
            if (meshCache.TryGetValue(path, out Mesh cachedMesh))
            {
                return cachedMesh;
            }

            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh != null)
            {
                meshCache[path] = mesh;
            }

            return mesh;
        }

        /// <summary>
        /// 清空所有资源缓存
        /// </summary>
        private static void ClearAllCaches()
        {
            textureCache.Clear();
            materialCache.Clear();
            atlasConfigCache.Clear();
            meshCache.Clear();
        }

        #endregion

        #region 内部数据结构

        /// <summary>
        /// 纹理复制信息
        /// </summary>
        private class TextureCopyInfo
        {
            public string sourcePath;
            public string targetPath;
            public string texName;
            public string texExt;
        }

        /// <summary>
        /// 材质复制信息
        /// </summary>
        private class MaterialCopyInfo
        {
            public string sourcePath;
            public string targetPath;
            public string matName;
        }

        /// <summary>
        /// 纹理属性信息
        /// </summary>
        private class TexturePropertyInfo
        {
            public string propertyName;
            public string textureName;
        }

        #endregion

        /// <summary>
        /// 查找纹理映射，支持直接匹配、扩展名匹配和LOD基础名称匹配
        /// </summary>
        /// <param name="texName">纹理名称</param>
        /// <param name="textureMapping">纹理映射表</param>
        /// <returns>目标纹理路径，未找到返回null</returns>
        private static string FindTextureMapping(string texName, Dictionary<string, string> textureMapping)
        {
            // 尝试直接匹配
            if (textureMapping.TryGetValue(texName, out string path))
                return path;

            // 尝试带扩展名匹配
            if (textureMapping.TryGetValue(texName + ".png", out path))
                return path;

            // 尝试LOD基础名称匹配
            if (texName.Contains("_lod"))
            {
                string baseTexName = System.Text.RegularExpressions.Regex.Replace(texName, @"_lod\d+$", "");

                if (baseTexName != texName)
                {
                    // 基础名称直接匹配
                    if (textureMapping.TryGetValue(baseTexName, out path))
                        return path;

                    // 基础名称+扩展名匹配
                    if (textureMapping.TryGetValue(baseTexName + ".png", out path))
                        return path;
                }
            }

            return null;
        }
    }
}
