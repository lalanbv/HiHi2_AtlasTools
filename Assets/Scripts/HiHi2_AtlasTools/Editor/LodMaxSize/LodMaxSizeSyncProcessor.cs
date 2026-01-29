using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace HiHi2.AtlasTools.Editor
{
    public static class LodMaxSizeSyncProcessor
    {
        public class SyncResult
        {
            public bool success;
            public string message;
            public int syncedCount;
        }

        public static SyncResult ApplySingleChange(TextureMaxSizeMatchInfo info)
        {
            var result = new SyncResult();

            if (info == null || !info.isModified)
            {
                result.success = false;
                result.message = "无需修改";
                return result;
            }

            TextureImporter lodImporter = AssetImporter.GetAtPath(info.lodTexturePath) as TextureImporter;
            if (lodImporter == null)
            {
                result.success = false;
                result.message = $"无法获取 TextureImporter: {info.lodTexturePath}";
                AtlasLogger.LogError(result.message);
                return result;
            }

            lodImporter.maxTextureSize = info.targetMaxSize;
            lodImporter.SaveAndReimport();

            info.ApplyCurrentMaxSize(info.targetMaxSize);

            result.success = true;
            result.syncedCount = 1;
            result.message = $"已应用 {info.lodTextureName} 的MaxSize为 {info.targetMaxSize}";

            AtlasLogger.Log($"<color=green>{result.message}</color>");

            return result;
        }

        public static SyncResult ApplyAllChanges(List<TextureMaxSizeMatchInfo> matchInfoList)
        {
            var result = new SyncResult();

            var itemsToSync = matchInfoList?.Where(m => m.isModified || m.needsSync).ToList();

            if (itemsToSync == null || itemsToSync.Count == 0)
            {
                result.success = true;
                result.message = "没有需要应用的修改";
                return result;
            }

            int syncCount = 0;
            var processedImporters = new List<TextureImporter>();

            try
            {
                AssetDatabase.StartAssetEditing();

                for (int i = 0; i < itemsToSync.Count; i++)
                {
                    var info = itemsToSync[i];

                    if (EditorUtility.DisplayCancelableProgressBar("应用MaxSize修改",
                            $"正在处理: {info.lodTextureName}", (float)i / itemsToSync.Count))
                    {
                        break;
                    }

                    TextureImporter lodImporter = AssetImporter.GetAtPath(info.lodTexturePath) as TextureImporter;
                    if (lodImporter != null)
                    {
                        lodImporter.maxTextureSize = info.targetMaxSize;
                        EditorUtility.SetDirty(lodImporter);
                        processedImporters.Add(lodImporter);

                        info.ApplyCurrentMaxSize(info.targetMaxSize);
                        syncCount++;
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                EditorUtility.ClearProgressBar();
            }

            foreach (var importer in processedImporters)
            {
                importer.SaveAndReimport();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            result.success = true;
            result.syncedCount = syncCount;
            result.message = $"应用完成！共修改 {syncCount} 个纹理的MaxSize";

            AtlasLogger.Log($"<color=green>{result.message}</color>");

            return result;
        }

        public static int ResetAllToLod0(List<TextureMaxSizeMatchInfo> matchInfoList)
        {
            if (matchInfoList == null) return 0;

            var matchedItems = matchInfoList.Where(m => m.isMatched).ToList();

            foreach (var info in matchedItems)
            {
                info.ResetToLod0Value();
            }

            return matchedItems.Count(m => m.isModified);
        }
    }
}