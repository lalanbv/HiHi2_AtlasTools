using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using HiHi2.AtlasTools;

public class MaterialAtlasReplacer
{
    public class ReplacementInfo
    {
        public Material material;
        public string propertyName;
        public Texture originalTexture;
        public SpriteInfoData spriteInfo;
        public AtlasConfig matchedAtlasConfig;
        public bool canReplace;
        public string message;
    }

    public class MaterialProcessResult
    {
        public Material material;
        public List<ReplacementInfo> replacements = new List<ReplacementInfo>();
        public bool hasReplaceableTextures;
        public int successCount;
        public int failCount;
    }

    public static List<Material> CollectMaterialsFromFolder(string folderPath)
    {
        if (string.IsNullOrEmpty(folderPath))
        {
            AtlasLogger.LogError("Material folder path is null or empty");
            return new List<Material>();
        }

        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            AtlasLogger.LogError($"Invalid folder path: {folderPath}");
            return new List<Material>();
        }

        List<Material> materials = new List<Material>();
        string[] guids = AssetDatabase.FindAssets("t:Material", new[] { folderPath });

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null)
            {
                materials.Add(mat);
            }
            else
            {
                AtlasLogger.LogWarning($"Failed to load material at path: {path}");
            }
        }

        AtlasLogger.Log($"Collected {materials.Count} materials from {folderPath}");
        return materials;
    }

    public static List<MaterialProcessResult> AnalyzeMaterials(
        List<Material> materials,
        List<AtlasConfig> atlasConfigs)
    {
        List<MaterialProcessResult> results = new List<MaterialProcessResult>();

        if (materials == null || materials.Count == 0)
        {
            AtlasLogger.LogWarning("No materials to analyze");
            return results;
        }

        if (atlasConfigs == null || atlasConfigs.Count == 0)
        {
            AtlasLogger.LogWarning("No atlas configs provided");
            return results;
        }

        int processedCount = 0;
        foreach (Material mat in materials)
        {
            if (mat == null)
            {
                AtlasLogger.LogWarning($"Skipping null material at index {processedCount}");
                continue;
            }

            MaterialProcessResult result = AnalyzeMaterial(mat, atlasConfigs);
            if (result.replacements.Count > 0)
            {
                results.Add(result);
            }

            processedCount++;
        }

        AtlasLogger.Log($"Analysis complete: {results.Count} materials with replaceable textures out of {processedCount} total");
        return results;
    }

    private static MaterialProcessResult AnalyzeMaterial(Material material, List<AtlasConfig> atlasConfigs)
    {
        MaterialProcessResult result = new MaterialProcessResult { material = material };

        Shader shader = material.shader;
        if (shader == null)
        {
            AtlasLogger.LogWarning($"Material [{material.name}] has no shader");
            return result;
        }

        int propertyCount = ShaderUtil.GetPropertyCount(shader);

        for (int i = 0; i < propertyCount; i++)
        {
            if (ShaderUtil.GetPropertyType(shader, i) != ShaderUtil.ShaderPropertyType.TexEnv)
                continue;

            string propName = ShaderUtil.GetPropertyName(shader, i);
            Texture tex = material.GetTexture(propName);

            if (tex != null && tex is Texture2D)
            {
                ReplacementInfo info = AnalyzeTextureProperty(material, propName, tex, atlasConfigs);
                result.replacements.Add(info);

                if (info.canReplace)
                {
                    result.hasReplaceableTextures = true;
                }
            }
        }

        return result;
    }

    private static ReplacementInfo AnalyzeTextureProperty(
        Material material,
        string propertyName,
        Texture texture,
        List<AtlasConfig> atlasConfigs)
    {
        ReplacementInfo info = new ReplacementInfo
        {
            material = material,
            propertyName = propertyName,
            originalTexture = texture
        };

        string textureName = texture.name;

        foreach (AtlasConfig atlasConfig in atlasConfigs)
        {
            if (atlasConfig == null || atlasConfig.spriteInfos == null)
                continue;

            SpriteInfoData spriteInfo = atlasConfig.GetSpriteInfo(textureName);
            if (spriteInfo != null)
            {
                info.spriteInfo = spriteInfo;
                info.matchedAtlasConfig = atlasConfig;
                info.canReplace = true;
                info.message = $"找到匹配: {textureName} -> {atlasConfig.name}";
                break;
            }
        }

        if (!info.canReplace)
        {
            info.message = $"图集中未找到: {textureName}";
        }

        return info;
    }

    public static void ApplyReplacements(
        List<MaterialProcessResult> results,
        bool onlyReplaceMatched = true)
    {
        if (results == null || results.Count == 0)
        {
            AtlasLogger.LogWarning("No results to apply");
            return;
        }

        int totalSuccess = 0;
        int totalFail = 0;

        foreach (MaterialProcessResult result in results)
        {
            if (result.material == null)
            {
                AtlasLogger.LogWarning("Skipping null material in results");
                continue;
            }

            foreach (ReplacementInfo info in result.replacements)
            {
                if (!info.canReplace)
                {
                    if (!onlyReplaceMatched)
                    {
                        AtlasLogger.LogWarning($"材质 [{result.material.name}] 的纹理 [{info.originalTexture.name}] 在图集中未找到，跳过");
                    }

                    totalFail++;
                    continue;
                }

                if (!ValidateReplacementInfo(info, result.material.name))
                {
                    totalFail++;
                    continue;
                }

                if (ApplySingleReplacement(result.material, info))
                {
                    totalSuccess++;
                }
                else
                {
                    totalFail++;
                }
            }

            EditorUtility.SetDirty(result.material);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        AtlasLogger.Log($"<color=green>材质替换完成！</color> 成功: {totalSuccess}, 失败: {totalFail}");
    }

    private static bool ValidateReplacementInfo(ReplacementInfo info, string materialName)
    {
        if (info.matchedAtlasConfig == null)
        {
            AtlasLogger.LogError($"材质 [{materialName}] 的图集配置为空");
            return false;
        }

        if (info.matchedAtlasConfig.atlasTexture == null)
        {
            AtlasLogger.LogError($"材质 [{materialName}] 的图集纹理为空");
            return false;
        }

        if (info.spriteInfo == null)
        {
            AtlasLogger.LogError($"材质 [{materialName}] 的精灵信息为空");
            return false;
        }

        return true;
    }

    private static bool ApplySingleReplacement(Material material, ReplacementInfo info)
    {
        try
        {
            AtlasConfig atlasConfig = info.matchedAtlasConfig;
            material.SetTexture(info.propertyName, atlasConfig.atlasTexture);

            Rect uvRect = info.spriteInfo.uvRect;
            Vector2 scale = new Vector2(uvRect.width, uvRect.height);
            Vector2 offset = new Vector2(uvRect.x, uvRect.y);

            string tilingProp = info.propertyName + "_ST";
            Vector4 stValue = new Vector4(scale.x, scale.y, offset.x, offset.y);
            material.SetVector(tilingProp, stValue);

            AtlasLogger.Log($"<color=cyan>已替换材质 [{material.name}] 属性 [{info.propertyName}]:</color>\n" +
                            $"  纹理: {info.originalTexture.name} -> {atlasConfig.atlasTexture.name}\n" +
                            $"  Tiling: ({scale.x:F4}, {scale.y:F4})\n" +
                            $"  Offset: ({offset.x:F4}, {offset.y:F4})");

            return true;
        }
        catch (System.Exception e)
        {
            AtlasLogger.LogError($"替换材质 [{material.name}] 属性 [{info.propertyName}] 时发生错误: {e.Message}");
            return false;
        }
    }

    public static string GetMaterialPath(Material mat)
    {
        if (mat == null)
        {
            AtlasLogger.LogWarning("Cannot get path of null material");
            return string.Empty;
        }

        return AssetDatabase.GetAssetPath(mat);
    }

    public static List<AtlasConfig> CollectAtlasConfigsFromFolder(string folderPath)
    {
        List<AtlasConfig> configs = new List<AtlasConfig>();

        if (string.IsNullOrEmpty(folderPath))
        {
            AtlasLogger.LogError("Atlas config folder path is null or empty");
            return configs;
        }

        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            AtlasLogger.LogError($"Invalid atlas config folder path: {folderPath}");
            return configs;
        }

        string[] guids = AssetDatabase.FindAssets("t:AtlasConfig", new[] { folderPath });

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            AtlasConfig config = AssetDatabase.LoadAssetAtPath<AtlasConfig>(path);

            if (config != null && config.atlasTexture != null)
            {
                configs.Add(config);
            }
            else if (config != null)
            {
                AtlasLogger.LogWarning($"Atlas config at {path} has no atlas texture");
            }
        }

        AtlasLogger.Log($"Collected {configs.Count} valid atlas configs from {folderPath}");
        return configs;
    }
}