using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HiHi2.AtlasTools.Editor
{
    public static class MaterialTextureScanner
    {
        private static readonly string[] TEXTURE_PROPERTY_NAMES = new string[]
        {
            "_MainTex", "_BaseMap", "_BumpMap", "_NormalMap", "_MetallicGlossMap",
            "_SpecGlossMap", "_OcclusionMap", "_EmissionMap", "_DetailAlbedoMap",
            "_DetailNormalMap", "_DetailMask", "_ParallaxMap", "_Tex", "_Texture",
            "_AlbedoMap", "_MaskMap", "_DiffuseMap", "_SpecularMap"
        };

        private static readonly HashSet<string> SKIP_FOLDER_NAMES = new HashSet<string>
        {
            "Material", "Texture", "Mesh", "Debug", "LOD", "Lod",
            "AtlasMaterial", "AtlasTexture", "Editor", "Resources"
        };

        public static TextureReferenceScanResult ScanMaterialFolder(string materialFolderPath)
        {
            var result = new TextureReferenceScanResult();

            if (string.IsNullOrEmpty(materialFolderPath) || !AssetDatabase.IsValidFolder(materialFolderPath))
            {
                result.success = false;
                result.message = $"无效的材质文件夹路径: {materialFolderPath}";
                return result;
            }

            List<Material> materials = CollectMaterialsFromFolder(materialFolderPath);
            if (materials.Count == 0)
            {
                result.success = false;
                result.message = $"文件夹中未找到材质文件: {materialFolderPath}";
                return result;
            }

            return AnalyzeMaterials(materials);
        }

        public static TextureReferenceScanResult ScanProjectFolder(string projectFolderPath)
        {
            var result = new TextureReferenceScanResult();

            if (string.IsNullOrEmpty(projectFolderPath) || !AssetDatabase.IsValidFolder(projectFolderPath))
            {
                result.success = false;
                result.message = $"无效的项目文件夹路径: {projectFolderPath}";
                return result;
            }

            string materialFolderPath = Path.Combine(projectFolderPath, "Material").Replace("\\", "/");

            if (!AssetDatabase.IsValidFolder(materialFolderPath))
            {
                result.success = false;
                result.message = $"项目目录下未找到Material文件夹: {materialFolderPath}";
                return result;
            }

            return ScanMaterialFolder(materialFolderPath);
        }

        public static TextureReferenceScanResult ScanMultipleProjectFolders(string rootFolderPath)
        {
            var result = new TextureReferenceScanResult();

            if (string.IsNullOrEmpty(rootFolderPath) || !AssetDatabase.IsValidFolder(rootFolderPath))
            {
                result.success = false;
                result.message = $"无效的根目录路径: {rootFolderPath}";
                return result;
            }

            List<string> materialFolders = FindAllMaterialFolders(rootFolderPath);

            if (materialFolders.Count == 0)
            {
                result.success = false;
                result.message = $"目录下未找到任何Material文件夹（已递归扫描所有子目录）";
                return result;
            }

            List<Material> allMaterials = new List<Material>();
            foreach (string materialFolderPath in materialFolders)
            {
                var materials = CollectMaterialsFromFolder(materialFolderPath);
                allMaterials.AddRange(materials);
            }

            if (allMaterials.Count == 0)
            {
                result.success = false;
                result.message = $"在 {materialFolders.Count} 个Material文件夹中未找到任何材质文件";
                return result;
            }

            var analysisResult = AnalyzeMaterials(allMaterials);
            analysisResult.message = $"扫描完成！在 {materialFolders.Count} 个项目中找到 {analysisResult.totalMaterialCount} 个材质，{analysisResult.totalTextureCount} 张纹理";

            return analysisResult;
        }

        public static List<string> FindAllMaterialFolders(string rootPath)
        {
            List<string> materialFolders = new List<string>();
            FindMaterialFoldersRecursive(rootPath, materialFolders);
            return materialFolders;
        }

        private static void FindMaterialFoldersRecursive(string folderPath, List<string> materialFolders)
        {
            if (string.IsNullOrEmpty(folderPath) || !AssetDatabase.IsValidFolder(folderPath))
                return;

            string materialPath = Path.Combine(folderPath, "Material").Replace("\\", "/");
            if (AssetDatabase.IsValidFolder(materialPath))
            {
                materialFolders.Add(materialPath);
            }

            string[] subFolders = AssetDatabase.GetSubFolders(folderPath);
            foreach (string subFolder in subFolders)
            {
                string folderName = Path.GetFileName(subFolder);

                if (SKIP_FOLDER_NAMES.Contains(folderName))
                    continue;

                FindMaterialFoldersRecursive(subFolder, materialFolders);
            }
        }

        public static int CountProjectsWithMaterial(string rootPath)
        {
            if (string.IsNullOrEmpty(rootPath) || !AssetDatabase.IsValidFolder(rootPath))
                return 0;

            return FindAllMaterialFolders(rootPath).Count;
        }

        public static TextureReferenceScanResult AnalyzeMaterials(List<Material> materials)
        {
            var result = new TextureReferenceScanResult();
            Dictionary<string, TextureReferenceInfo> textureDict = new Dictionary<string, TextureReferenceInfo>();

            result.totalMaterialCount = materials.Count;

            foreach (var material in materials)
            {
                if (material == null) continue;

                string materialPath = AssetDatabase.GetAssetPath(material);
                var materialAnalysis = new MaterialTextureAnalysisResult
                {
                    material = material,
                    materialPath = materialPath,
                    materialName = material.name
                };

                Shader shader = material.shader;
                if (shader == null) continue;

                int propertyCount = ShaderUtil.GetPropertyCount(shader);
                for (int i = 0; i < propertyCount; i++)
                {
                    if (ShaderUtil.GetPropertyType(shader, i) != ShaderUtil.ShaderPropertyType.TexEnv)
                        continue;

                    string propertyName = ShaderUtil.GetPropertyName(shader, i);
                    Texture tex = material.GetTexture(propertyName);
                    Texture2D tex2D = tex as Texture2D;

                    if (tex2D == null) continue;

                    string texturePath = AssetDatabase.GetAssetPath(tex2D);
                    if (string.IsNullOrEmpty(texturePath)) continue;

                    if (!textureDict.TryGetValue(texturePath, out TextureReferenceInfo texInfo))
                    {
                        texInfo = new TextureReferenceInfo
                        {
                            texture = tex2D,
                            texturePath = texturePath,
                            textureName = tex2D.name,
                            referenceCount = 0
                        };
                        textureDict[texturePath] = texInfo;
                    }

                    var existingMaterialRef = texInfo.referencedByMaterials.Find(m => m.materialPath == materialPath);
                    if (existingMaterialRef == null)
                    {
                        existingMaterialRef = new MaterialReferenceDetail
                        {
                            material = material,
                            materialPath = materialPath,
                            materialName = material.name
                        };
                        texInfo.referencedByMaterials.Add(existingMaterialRef);
                        texInfo.referenceCount++;
                    }

                    existingMaterialRef.propertyNames.Add(propertyName);

                    materialAnalysis.textureProperties.Add(new TexturePropertyInfo
                    {
                        propertyName = propertyName,
                        texture = tex2D,
                        texturePath = texturePath,
                        globalReferenceCount = 0
                    });
                }

                result.materialAnalysisResults.Add(materialAnalysis);
            }

            result.allTextures = textureDict.Values
                .OrderByDescending(t => t.referenceCount)
                .ThenBy(t => t.textureName)
                .ToList();

            foreach (var matResult in result.materialAnalysisResults)
            {
                foreach (var texProp in matResult.textureProperties)
                {
                    if (textureDict.TryGetValue(texProp.texturePath, out TextureReferenceInfo texInfo))
                    {
                        texProp.globalReferenceCount = texInfo.referenceCount;
                    }
                }
            }

            result.multiReferencedTextures = result.allTextures.Where(t => t.referenceCount > 1).ToList();
            result.singleReferencedTextures = result.allTextures.Where(t => t.referenceCount == 1).ToList();

            result.totalTextureCount = result.allTextures.Count;
            result.multiReferenceTextureCount = result.multiReferencedTextures.Count;
            result.singleReferenceTextureCount = result.singleReferencedTextures.Count;

            result.success = true;
            result.message = $"扫描完成！共 {result.totalMaterialCount} 个材质，{result.totalTextureCount} 张纹理";

            return result;
        }

        public static List<Material> CollectMaterialsFromFolder(string folderPath)
        {
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
            }

            return materials;
        }

        public static string GetAutoMaterialFolderPath(string projectFolderPath)
        {
            if (string.IsNullOrEmpty(projectFolderPath))
                return string.Empty;

            return Path.Combine(projectFolderPath, "Material").Replace("\\", "/");
        }
    }
}