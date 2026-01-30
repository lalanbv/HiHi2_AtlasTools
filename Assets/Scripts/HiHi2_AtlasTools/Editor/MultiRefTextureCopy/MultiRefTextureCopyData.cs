// HiHi2_AtlasTools\Editor\MultiRefTextureCopy\MultiRefTextureCopyData.cs
// 数据结构定义
using System.Collections.Generic;
using UnityEngine;

namespace HiHi2.AtlasTools.Editor
{
    public enum TextureCopyStatus
    {
        Pending,
        Copied,
        Skipped,
        Renamed,
        Failed
    }

    public class TextureCopyInfo
    {
        public Texture2D sourceTexture;
        public string sourcePath;
        public string sourceName;
        public string sourceExtension;
        public string sourceMD5;
        public int referenceCount;
        public string targetPath;
        public string targetName;
        public TextureCopyStatus status;
        public string statusMessage;
        public List<string> referencedByMaterials = new List<string>();
    }

    public class TextureCopyResult
    {
        public bool success;
        public string message;
        public int totalMultiRefCount;
        public int copiedCount;
        public int skippedCount;
        public int renamedCount;
        public int failedCount;
        public List<TextureCopyInfo> copyInfoList = new List<TextureCopyInfo>();
    }

    public class TextureCopyScanResult
    {
        public bool success;
        public string message;
        public int totalMaterialCount;
        public int totalTextureCount;
        public int multiRefTextureCount;
        public List<TextureCopyInfo> multiRefTextures = new List<TextureCopyInfo>();
    }
}
