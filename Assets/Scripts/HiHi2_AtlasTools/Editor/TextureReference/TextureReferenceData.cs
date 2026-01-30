using System.Collections.Generic;
using UnityEngine;

namespace HiHi2.AtlasTools.Editor
{
    public class TextureReferenceInfo
    {
        public Texture2D texture;
        public string texturePath;
        public string textureName;
        public int referenceCount;
        public List<MaterialReferenceDetail> referencedByMaterials = new List<MaterialReferenceDetail>();
    }

    public class MaterialReferenceDetail
    {
        public Material material;
        public string materialPath;
        public string materialName;
        public List<string> propertyNames = new List<string>();
    }

    public class MaterialTextureAnalysisResult
    {
        public Material material;
        public string materialPath;
        public string materialName;
        public List<TexturePropertyInfo> textureProperties = new List<TexturePropertyInfo>();
    }

    public class TexturePropertyInfo
    {
        public string propertyName;
        public Texture2D texture;
        public string texturePath;
        public int globalReferenceCount;
    }

    public class TextureReferenceScanResult
    {
        public bool success;
        public string message;
        public int totalMaterialCount;
        public int totalTextureCount;
        public int multiReferenceTextureCount;
        public int singleReferenceTextureCount;
        public List<TextureReferenceInfo> allTextures = new List<TextureReferenceInfo>();
        public List<TextureReferenceInfo> multiReferencedTextures = new List<TextureReferenceInfo>();
        public List<TextureReferenceInfo> singleReferencedTextures = new List<TextureReferenceInfo>();
        public List<MaterialTextureAnalysisResult> materialAnalysisResults = new List<MaterialTextureAnalysisResult>();
    }
}