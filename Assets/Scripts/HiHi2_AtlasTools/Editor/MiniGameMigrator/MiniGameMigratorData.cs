using System.Collections.Generic;

namespace HiHi2.AtlasTools.Editor
{
    public enum LodLevel
    {
        Lod1 = 1,
        Lod2 = 2,
        Lod3 = 3
    }

    public enum MeshType
    {
        Original,
        Lod
    }

    /// <summary>
    /// 图集纹理模式
    /// </summary>
    public enum AtlasTextureMode
    {
        /// <summary>
        /// 使用原始图集（无LOD后缀）
        /// </summary>
        Original,

        /// <summary>
        /// 使用LOD图集（带_lod后缀）
        /// </summary>
        Lod
    }

    public class AvatarObjectInfo
    {
        public string objectName;
        public string sourcePath;
        public string category;

        public bool hasLodFolder;
        public bool hasMeshFolder;
        public bool hasPrefab;
        public bool hasOriginalMesh;
        public bool hasLodMesh;

        public string prefabPath;
        public string originalMeshPath;
        public string lodMeshPath;

        public string lodAtlasMaterialPath;
        public string lodAtlasTexturePath;

        public List<string> atlasMaterials = new List<string>();
        public List<string> atlasTextures = new List<string>();
        public List<string> lodTextures = new List<string>();

        public bool isValid;
        public string invalidReason;

        public bool CanMigrate => isValid && hasLodFolder && hasMeshFolder && (hasOriginalMesh || hasLodMesh);
    }

    public class MigrationResult
    {
        public bool success;
        public string message;
        public string objectName;
        public int copiedMaterialCount;
        public int copiedTextureCount;
        public int replacedTextureCount;
        public bool prefabCreated;
        public string outputPrefabPath;
        public string outputMeshPath;
    }

    public class BatchMigrationResult
    {
        public bool success;
        public string message;
        public int totalCount;
        public int successCount;
        public int failedCount;
        public int skippedCount;
        public List<MigrationResult> results = new List<MigrationResult>();
    }

    public class MigrationOptions
    {
        public LodLevel lodLevel = LodLevel.Lod1;
        public MeshType meshType = MeshType.Original;
        public AtlasTextureMode atlasTextureMode = AtlasTextureMode.Lod;
        public bool createPrefab = true;
        public bool overwriteExisting = false;
    }
}
