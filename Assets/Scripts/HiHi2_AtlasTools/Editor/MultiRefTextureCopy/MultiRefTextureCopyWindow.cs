// HiHi2_AtlasTools\Editor\MultiRefTextureCopy\MultiRefTextureCopyWindow.cs
// EditorWindow界面
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace HiHi2.AtlasTools.Editor
{
    public class MultiRefTextureCopyWindow : EditorWindow
    {
        private DefaultAsset materialFolder;
        private DefaultAsset targetFolder;

        private TextureCopyScanResult scanResult;
        private TextureCopyResult copyResult;

        private Vector2 scrollPos;
        private Dictionary<string, bool> textureFoldouts = new Dictionary<string, bool>();

        [MenuItem("Assets/Lod图及相关工具/多引用纹理复制", false, 3200)]
        public static void ShowWindowFromAssets()
        {
            string selectedPath = GetSelectedFolderPath();
            var window = GetWindow<MultiRefTextureCopyWindow>("多引用纹理复制");
            window.minSize = new Vector2(650, 550);

            if (!string.IsNullOrEmpty(selectedPath))
            {
                window.materialFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(selectedPath);
            }

            window.Show();
        }

        [MenuItem("Tools/Lod图及相关工具/图集生成替换/多引用纹理复制", false, 3200)]
        public static void ShowWindowFromMenu()
        {
            var window = GetWindow<MultiRefTextureCopyWindow>("多引用纹理复制");
            window.minSize = new Vector2(650, 550);
            window.Show();
        }

        private static string GetSelectedFolderPath()
        {
            if (Selection.objects.Length == 0) return null;
            string path = AssetDatabase.GetAssetPath(Selection.objects[0]);
            return AssetDatabase.IsValidFolder(path) ? path : null;
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);

            DrawHeader();
            EditorGUILayout.Space(10);

            DrawFolderConfiguration();
            EditorGUILayout.Space(10);

            DrawActionButtons();
            EditorGUILayout.Space(10);

            if (scanResult != null && scanResult.success)
            {
                DrawScanResults();
            }

            if (copyResult != null)
            {
                DrawCopyResults();
            }
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("多引用纹理复制工具", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "功能说明：\n" +
                "1. 指定材质文件夹，扫描其中所有材质球的纹理引用\n" +
                "2. 找出被多次引用的纹理（引用次数 > 1）\n" +
                "3. 将这些纹理复制到指定的目标文件夹\n" +
                "4. 如果目标文件夹已存在同名文件，通过MD5比较处理：\n" +
                "   - MD5相同：跳过复制\n" +
                "   - MD5不同：使用 名称_0、名称_1... 的格式重命名",
                MessageType.Info
            );
            EditorGUILayout.EndVertical();
        }

        private void DrawFolderConfiguration()
        {
            EditorGUILayout.LabelField("文件夹配置", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");

            DrawMaterialFolderField();
            EditorGUILayout.Space(5);
            DrawTargetFolderField();

            EditorGUILayout.EndVertical();
        }

        private void DrawMaterialFolderField()
        {
            EditorGUILayout.LabelField("① 材质文件夹（扫描纹理引用）", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();

            materialFolder = (DefaultAsset)EditorGUILayout.ObjectField(materialFolder, typeof(DefaultAsset), false);

            if (GUILayout.Button("选择", GUILayout.Width(50)))
            {
                string path = EditorUtility.OpenFolderPanel("选择材质文件夹", "Assets", "");
                if (!string.IsNullOrEmpty(path))
                {
                    string relativePath = AtlasCommon.ConvertToRelativePath(path);
                    if (AssetDatabase.IsValidFolder(relativePath))
                    {
                        materialFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(relativePath);
                        ClearResults();
                    }
                }
            }

            if (GUILayout.Button("当前选中", GUILayout.Width(70)))
            {
                var obj = Selection.activeObject as DefaultAsset;
                if (obj != null && AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(obj)))
                {
                    materialFolder = obj;
                    ClearResults();
                }
            }

            EditorGUILayout.EndHorizontal();

            if (materialFolder != null)
            {
                string folderPath = AssetDatabase.GetAssetPath(materialFolder);
                EditorGUILayout.LabelField($"路径: {folderPath}", EditorStyles.miniLabel);

                var materials = MaterialTextureScanner.CollectMaterialsFromFolder(folderPath);
                GUIStyle statusStyle = new GUIStyle(EditorStyles.miniLabel);
                if (materials.Count > 0)
                {
                    statusStyle.normal.textColor = new Color(0.3f, 0.8f, 0.3f);
                    EditorGUILayout.LabelField($"✓ 检测到 {materials.Count} 个材质文件", statusStyle);
                }
                else
                {
                    statusStyle.normal.textColor = new Color(1f, 0.5f, 0.3f);
                    EditorGUILayout.LabelField("✗ 未检测到材质文件", statusStyle);
                }
            }
        }

        private void DrawTargetFolderField()
        {
            EditorGUILayout.LabelField("② 目标文件夹（存放多引用纹理）", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();

            targetFolder = (DefaultAsset)EditorGUILayout.ObjectField(targetFolder, typeof(DefaultAsset), false);

            if (GUILayout.Button("选择", GUILayout.Width(50)))
            {
                string path = EditorUtility.OpenFolderPanel("选择目标文件夹", "Assets", "");
                if (!string.IsNullOrEmpty(path))
                {
                    string relativePath = AtlasCommon.ConvertToRelativePath(path);
                    if (AssetDatabase.IsValidFolder(relativePath))
                    {
                        targetFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(relativePath);
                    }
                }
            }

            if (GUILayout.Button("当前选中", GUILayout.Width(70)))
            {
                var obj = Selection.activeObject as DefaultAsset;
                if (obj != null && AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(obj)))
                {
                    targetFolder = obj;
                }
            }

            EditorGUILayout.EndHorizontal();

            if (targetFolder != null)
            {
                string folderPath = AssetDatabase.GetAssetPath(targetFolder);
                EditorGUILayout.LabelField($"路径: {folderPath}", EditorStyles.miniLabel);
            }
        }

        private void DrawActionButtons()
        {
            EditorGUILayout.BeginHorizontal();

            bool canScan = materialFolder != null;
            GUI.enabled = canScan;
            GUI.backgroundColor = new Color(0.6f, 0.9f, 0.6f);
            if (GUILayout.Button("扫描多引用纹理", GUILayout.Height(30)))
            {
                PerformScan();
            }

            GUI.backgroundColor = Color.white;

            bool canCopy = scanResult != null && scanResult.success && scanResult.multiRefTextureCount > 0 && targetFolder != null;
            GUI.enabled = canCopy;
            GUI.backgroundColor = new Color(0.9f, 0.7f, 0.5f);
            if (GUILayout.Button("执行复制", GUILayout.Height(30)))
            {
                PerformCopy();
            }

            GUI.backgroundColor = Color.white;
            GUI.enabled = true;

            EditorGUILayout.EndHorizontal();

            if (scanResult != null && scanResult.success && scanResult.multiRefTextureCount > 0 && targetFolder == null)
            {
                EditorGUILayout.HelpBox("请指定目标文件夹后执行复制", MessageType.Warning);
            }
        }

        private void PerformScan()
        {
            textureFoldouts.Clear();
            copyResult = null;

            string materialPath = AssetDatabase.GetAssetPath(materialFolder);
            scanResult = MultiRefTextureCopyProcessor.ScanMultiRefTextures(materialPath);

            if (scanResult.success)
            {
                EditorUtility.DisplayDialog("扫描完成",
                    $"扫描完成！\n材质数量: {scanResult.totalMaterialCount}\n总纹理数: {scanResult.totalTextureCount}\n多引用纹理: {scanResult.multiRefTextureCount}",
                    "确定");
            }
            else
            {
                EditorUtility.DisplayDialog("扫描失败", scanResult.message, "确定");
            }
        }

        private void PerformCopy()
        {
            if (scanResult == null || !scanResult.success || scanResult.multiRefTextures.Count == 0)
            {
                EditorUtility.DisplayDialog("错误", "没有可复制的多引用纹理", "确定");
                return;
            }

            if (targetFolder == null)
            {
                EditorUtility.DisplayDialog("错误", "请指定目标文件夹", "确定");
                return;
            }

            string targetPath = AssetDatabase.GetAssetPath(targetFolder);
            copyResult = MultiRefTextureCopyProcessor.CopyMultiRefTextures(scanResult.multiRefTextures, targetPath);

            EditorUtility.DisplayDialog(copyResult.success ? "复制完成" : "复制完成（有错误）", copyResult.message, "确定");
        }

        private void ClearResults()
        {
            scanResult = null;
            copyResult = null;
            textureFoldouts.Clear();
        }

        private void DrawScanResults()
        {
            EditorGUILayout.LabelField($"扫描结果 - 共 {scanResult.multiRefTextureCount} 张多引用纹理", EditorStyles.boldLabel);

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            if (scanResult.multiRefTextures.Count == 0)
            {
                EditorGUILayout.HelpBox("未发现多次引用的纹理", MessageType.Info);
            }
            else
            {
                GUI.backgroundColor = new Color(1f, 0.9f, 0.8f);
                EditorGUILayout.BeginVertical("box");
                GUI.backgroundColor = Color.white;

                foreach (var texInfo in scanResult.multiRefTextures)
                {
                    DrawTextureInfo(texInfo);
                }

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawTextureInfo(TextureCopyInfo texInfo)
        {
            Color bgColor = GetStatusColor(texInfo.status);
            GUI.backgroundColor = bgColor;
            EditorGUILayout.BeginVertical("box");
            GUI.backgroundColor = Color.white;

            EditorGUILayout.BeginHorizontal();

            if (!textureFoldouts.ContainsKey(texInfo.sourcePath))
            {
                textureFoldouts[texInfo.sourcePath] = false;
            }

            textureFoldouts[texInfo.sourcePath] = EditorGUILayout.Foldout(
                textureFoldouts[texInfo.sourcePath],
                "",
                true
            );

            GUILayout.Space(-15);

            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.ObjectField(texInfo.sourceTexture, typeof(Texture2D), false, GUILayout.Width(180));
            EditorGUI.EndDisabledGroup();

            GUIStyle countStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = Color.red }
            };
            EditorGUILayout.LabelField($"引用: {texInfo.referenceCount}次", countStyle, GUILayout.Width(80));

            if (texInfo.status != TextureCopyStatus.Pending)
            {
                GUIStyle statusStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    normal = { textColor = GetStatusTextColor(texInfo.status) }
                };
                EditorGUILayout.LabelField(GetStatusText(texInfo.status), statusStyle, GUILayout.Width(80));
            }

            if (GUILayout.Button("定位", GUILayout.Width(50)))
            {
                EditorGUIUtility.PingObject(texInfo.sourceTexture);
                Selection.activeObject = texInfo.sourceTexture;
            }

            EditorGUILayout.EndHorizontal();

            if (textureFoldouts[texInfo.sourcePath])
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField($"源路径: {texInfo.sourcePath}", EditorStyles.miniLabel);
                EditorGUILayout.LabelField($"MD5: {texInfo.sourceMD5}", EditorStyles.miniLabel);

                if (!string.IsNullOrEmpty(texInfo.targetPath))
                {
                    EditorGUILayout.LabelField($"目标路径: {texInfo.targetPath}", EditorStyles.miniLabel);
                }

                if (!string.IsNullOrEmpty(texInfo.statusMessage))
                {
                    EditorGUILayout.LabelField($"状态: {texInfo.statusMessage}", EditorStyles.miniLabel);
                }

                EditorGUILayout.LabelField("引用材质:", EditorStyles.miniLabel);
                foreach (var matName in texInfo.referencedByMaterials)
                {
                    EditorGUILayout.LabelField($"  • {matName}", EditorStyles.miniLabel);
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2);
        }

        private void DrawCopyResults()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("复制结果统计", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.LabelField($"总计: {copyResult.totalMultiRefCount} 张");

            GUIStyle copiedStyle = new GUIStyle(EditorStyles.label) { normal = { textColor = new Color(0.2f, 0.7f, 0.2f) } };
            EditorGUILayout.LabelField($"成功复制: {copyResult.copiedCount} 张", copiedStyle);

            GUIStyle skippedStyle = new GUIStyle(EditorStyles.label) { normal = { textColor = new Color(0.5f, 0.5f, 0.8f) } };
            EditorGUILayout.LabelField($"跳过(MD5相同): {copyResult.skippedCount} 张", skippedStyle);

            GUIStyle renamedStyle = new GUIStyle(EditorStyles.label) { normal = { textColor = new Color(0.9f, 0.6f, 0.2f) } };
            EditorGUILayout.LabelField($"重命名复制: {copyResult.renamedCount} 张", renamedStyle);

            if (copyResult.failedCount > 0)
            {
                GUIStyle failedStyle = new GUIStyle(EditorStyles.label) { normal = { textColor = Color.red } };
                EditorGUILayout.LabelField($"失败: {copyResult.failedCount} 张", failedStyle);
            }

            EditorGUILayout.EndVertical();
        }

        private Color GetStatusColor(TextureCopyStatus status)
        {
            switch (status)
            {
                case TextureCopyStatus.Copied: return new Color(0.8f, 1f, 0.8f);
                case TextureCopyStatus.Skipped: return new Color(0.85f, 0.85f, 1f);
                case TextureCopyStatus.Renamed: return new Color(1f, 0.9f, 0.7f);
                case TextureCopyStatus.Failed: return new Color(1f, 0.8f, 0.8f);
                default: return new Color(0.95f, 0.95f, 0.95f);
            }
        }

        private Color GetStatusTextColor(TextureCopyStatus status)
        {
            switch (status)
            {
                case TextureCopyStatus.Copied: return new Color(0.2f, 0.7f, 0.2f);
                case TextureCopyStatus.Skipped: return new Color(0.4f, 0.4f, 0.8f);
                case TextureCopyStatus.Renamed: return new Color(0.9f, 0.6f, 0.2f);
                case TextureCopyStatus.Failed: return Color.red;
                default: return Color.gray;
            }
        }

        private string GetStatusText(TextureCopyStatus status)
        {
            switch (status)
            {
                case TextureCopyStatus.Copied: return "已复制";
                case TextureCopyStatus.Skipped: return "已跳过";
                case TextureCopyStatus.Renamed: return "已重命名";
                case TextureCopyStatus.Failed: return "失败";
                default: return "待处理";
            }
        }
    }
}
