using UnityEngine;

namespace HiHi2.AtlasTools.Editor
{
    public class TextureMaxSizeMatchInfo
    {
        public string lodTexturePath;
        public string sourceTexturePath;
        public string lodTextureName;
        public string sourceTextureName;
        public int lodTextureCurrentMaxSize;
        public int sourceLod0MaxSize;
        public int targetMaxSize;
        public bool isMatched;
        public bool needsSync;
        public bool isModified;
        public string errorMessage;
        public Texture2D lodTexturePreview;
        public Texture2D sourceTexturePreview;

        public bool HasPendingChanges => isModified || needsSync;

        public void ResetToLod0Value()
        {
            if (!isMatched) return;

            targetMaxSize = sourceLod0MaxSize;
            isModified = targetMaxSize != lodTextureCurrentMaxSize;
        }

        public void ApplyCurrentMaxSize(int newMaxSize)
        {
            lodTextureCurrentMaxSize = newMaxSize;
            isModified = false;
            needsSync = lodTextureCurrentMaxSize != sourceLod0MaxSize;
        }
    }

    public class LodMaxSizeScanResult
    {
        public bool success;
        public string message;
        public int totalCount;
        public int matchedCount;
        public int needsSyncCount;
    }
}