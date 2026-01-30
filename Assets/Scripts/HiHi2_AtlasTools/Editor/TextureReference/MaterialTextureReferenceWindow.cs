using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using HiHi2.AtlasTools;

namespace HiHi2.AtlasTools.Editor
{
    public class MaterialTextureReferenceWindow : EditorWindow
    {
        private enum ScanMode
        {
            SingleProject,
            MultiProject
        }

        private enum DisplayTab
        {
            MultiReference,
            SingleReference,
            AllTextures
        }

        private ScanMode scanMode = ScanMode.SingleProject;
        private PathMode pathMode = PathMode.Auto;
        private DisplayTab currentTab = DisplayTab.MultiReference;

        private DefaultAsset targetFolder;
        private DefaultAsset customMaterialFolder;

        private TextureReferenceScanResult scanResult;
        private Vector2 scrollPos;

        private bool foldoutMultiRef = true;
        private bool foldoutSingleRef = true;
        private bool foldoutAllTextures = true;

        private Dictionary<string, bool> textureFoldouts = new Dictionary<string, bool>();

        [MenuItem("Assets/Lod图及相关工具/材质纹理引用查找/单项目扫描", false, 3100)]
        public static void ShowSingleProjectWindow()
        {
            string selectedPath = GetSelectedFolderPath();
            var window = GetWindow<MaterialTextureReferenceWindow>("材质纹理引用查找");
            window.minSize = new Vector2(600, 500);
            window.scanMode = ScanMode.SingleProject;

            if (!string.IsNullOrEmpty(selectedPath))
            {
                window.targetFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(selectedPath);
            }

            window.Show();
        }

        [MenuItem("Assets/Lod图及相关工具/材质纹理引用查找/批量项目扫描", false, 3101)]
        public static void ShowMultiProjectWindow()
        {
            string selectedPath = GetSelectedFolderPath();
            var window = GetWindow<MaterialTextureReferenceWindow>("材质纹理引用查找");
            window.minSize = new Vector2(600, 500);
            window.scanMode = ScanMode.MultiProject;
            window.pathMode = PathMode.Custom;

            if (!string.IsNullOrEmpty(selectedPath))
            {
                window.targetFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(selectedPath);
            }

            window.Show();
        }

        [MenuItem("Tools/Lod图及相关工具/图集生成替换/材质纹理引用查找", false, 3100)]
        public static void ShowWindowFromMenu()
        {
            var window = GetWindow<MaterialTextureReferenceWindow>("材质纹理引用查找");
            window.minSize = new Vector2(600, 500);
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
            DrawScanModeSelector();
            EditorGUILayout.Space(10);

            if (scanMode == ScanMode.SingleProject)
            {
                DrawSingleProjectModeUI();
            }
            else
            {
                DrawMultiProjectModeUI();
            }

            EditorGUILayout.Space(10);
            DrawActionButtons();
            EditorGUILayout.Space(10);

            if (scanResult != null && scanResult.success)
            {
                DrawResultTabs();
                DrawResultsSection();
            }
        }

        private void DrawScanModeSelector()
        {
            EditorGUILayout.BeginHorizontal("box");
            EditorGUILayout.LabelField("扫描模式", EditorStyles.boldLabel, GUILayout.Width(60));

            GUI.backgroundColor = scanMode == ScanMode.SingleProject ? Color.green : Color.white;
            if (GUILayout.Button("单项目扫描", GUILayout.Height(25)))
            {
                scanMode = ScanMode.SingleProject;
                scanResult = null;
            }

            GUI.backgroundColor = scanMode == ScanMode.MultiProject ? Color.cyan : Color.white;
            if (GUILayout.Button("批量项目扫描", GUILayout.Height(25)))
            {
                scanMode = ScanMode.MultiProject;
                pathMode = PathMode.Custom;
                scanResult = null;
            }

            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            string helpMsg = scanMode == ScanMode.SingleProject
                ? "单项目扫描：分析单个项目目录下的Material文件夹中的材质"
                : "批量项目扫描：递归扫描目录下所有子项目的Material文件夹中的材质（支持多层嵌套目录）";
            EditorGUILayout.HelpBox(helpMsg, MessageType.Info);
        }

        private void DrawSingleProjectModeUI()
        {
            EditorGUILayout.BeginHorizontal("box");
            EditorGUILayout.LabelField("路径模式", EditorStyles.boldLabel, GUILayout.Width(60));

            GUI.backgroundColor = pathMode == PathMode.Auto ? Color.green : Color.white;
            if (GUILayout.Button("自动模式", GUILayout.Height(25)))
            {
                pathMode = PathMode.Auto;
            }

            GUI.backgroundColor = pathMode == PathMode.Custom ? Color.cyan : Color.white;
            if (GUILayout.Button("自定义模式", GUILayout.Height(25)))
            {
                pathMode = PathMode.Custom;
            }

            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            if (pathMode == PathMode.Auto)
            {
                DrawAutoModeConfig();
            }
            else
            {
                DrawCustomModeConfig();
            }
        }

        private void DrawMultiProjectModeUI()
        {
            EditorGUILayout.LabelField("批量扫描配置", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.LabelField("根目录（支持多层嵌套，如 BasicAvatar → Head → fh_0001 → Material）", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            targetFolder = (DefaultAsset)EditorGUILayout.ObjectField(targetFolder, typeof(DefaultAsset), false);
            if (GUILayout.Button("选择", GUILayout.Width(50)))
            {
                string path = EditorUtility.OpenFolderPanel("选择根目录", "Assets", "");
                if (!string.IsNullOrEmpty(path))
                {
                    string relativePath = AtlasCommon.ConvertToRelativePath(path);
                    if (AssetDatabase.IsValidFolder(relativePath))
                    {
                        targetFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(relativePath);
                    }
                }
            }

            EditorGUILayout.EndHorizontal();

            if (targetFolder != null)
            {
                string folderPath = AssetDatabase.GetAssetPath(targetFolder);
                EditorGUILayout.LabelField("路径: " + folderPath, EditorStyles.miniLabel);

                int projectCount = MaterialTextureScanner.CountProjectsWithMaterial(folderPath);

                GUIStyle countStyle = new GUIStyle(EditorStyles.miniLabel);
                if (projectCount > 0)
                {
                    countStyle.normal.textColor = new Color(0.3f, 0.8f, 0.3f);
                    EditorGUILayout.LabelField($"✓ 递归检测到 {projectCount} 个包含Material文件夹的项目", countStyle);
                }
                else
                {
                    countStyle.normal.textColor = new Color(1f, 0.5f, 0.3f);
                    EditorGUILayout.LabelField("✗ 未检测到包含Material文件夹的项目", countStyle);
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawAutoModeConfig()
        {
            EditorGUILayout.LabelField("自动模式配置", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.LabelField("项目文件夹（包含Material子文件夹）", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            targetFolder = (DefaultAsset)EditorGUILayout.ObjectField(targetFolder, typeof(DefaultAsset), false);
            if (GUILayout.Button("使用当前选中", GUILayout.Width(100)))
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
                EditorGUILayout.LabelField("路径: " + folderPath, EditorStyles.miniLabel);

                string materialPath = MaterialTextureScanner.GetAutoMaterialFolderPath(folderPath);
                bool hasMaterialFolder = AssetDatabase.IsValidFolder(materialPath);

                GUIStyle statusStyle = new GUIStyle(EditorStyles.miniLabel);
                statusStyle.normal.textColor = hasMaterialFolder ? new Color(0.3f, 0.8f, 0.3f) : new Color(1f, 0.5f, 0.3f);

                EditorGUILayout.LabelField(
                    hasMaterialFolder ? $"✓ Material文件夹: {materialPath}" : "✗ 未找到Material文件夹",
                    statusStyle
                );
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawCustomModeConfig()
        {
            EditorGUILayout.LabelField("自定义模式配置", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.LabelField("Material文件夹", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            customMaterialFolder = (DefaultAsset)EditorGUILayout.ObjectField(customMaterialFolder, typeof(DefaultAsset), false);
            if (GUILayout.Button("选择", GUILayout.Width(50)))
            {
                string path = EditorUtility.OpenFolderPanel("选择Material文件夹", "Assets", "");
                if (!string.IsNullOrEmpty(path))
                {
                    string relativePath = AtlasCommon.ConvertToRelativePath(path);
                    if (AssetDatabase.IsValidFolder(relativePath))
                    {
                        customMaterialFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(relativePath);
                    }
                }
            }

            EditorGUILayout.EndHorizontal();

            if (customMaterialFolder != null)
            {
                string folderPath = AssetDatabase.GetAssetPath(customMaterialFolder);
                EditorGUILayout.LabelField("路径: " + folderPath, EditorStyles.miniLabel);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawActionButtons()
        {
            EditorGUILayout.BeginHorizontal();

            bool canScan = ValidateScanInput();
            GUI.enabled = canScan;

            if (GUILayout.Button("扫描分析", GUILayout.Height(30)))
            {
                PerformScan();
            }

            GUI.enabled = scanResult != null && scanResult.success;

            if (GUILayout.Button("输出到Console", GUILayout.Height(30)))
            {
                OutputToConsole();
            }

            GUI.enabled = true;

            EditorGUILayout.EndHorizontal();
        }

        private bool ValidateScanInput()
        {
            if (scanMode == ScanMode.SingleProject)
            {
                if (pathMode == PathMode.Auto)
                {
                    return targetFolder != null;
                }
                else
                {
                    return customMaterialFolder != null;
                }
            }
            else
            {
                return targetFolder != null && MaterialTextureScanner.CountProjectsWithMaterial(AssetDatabase.GetAssetPath(targetFolder)) > 0;
            }
        }

        private void PerformScan()
        {
            textureFoldouts.Clear();

            if (scanMode == ScanMode.SingleProject)
            {
                if (pathMode == PathMode.Auto)
                {
                    string projectPath = AssetDatabase.GetAssetPath(targetFolder);
                    scanResult = MaterialTextureScanner.ScanProjectFolder(projectPath);
                }
                else
                {
                    string materialPath = AssetDatabase.GetAssetPath(customMaterialFolder);
                    scanResult = MaterialTextureScanner.ScanMaterialFolder(materialPath);
                }
            }
            else
            {
                string rootPath = AssetDatabase.GetAssetPath(targetFolder);
                scanResult = MaterialTextureScanner.ScanMultipleProjectFolders(rootPath);
            }

            if (scanResult.success)
            {
                EditorUtility.DisplayDialog("扫描完成", scanResult.message, "确定");
            }
            else
            {
                EditorUtility.DisplayDialog("扫描失败", scanResult.message, "确定");
            }
        }

        private void OutputToConsole()
        {
            if (scanResult == null || !scanResult.success) return;
            MaterialTextureReferenceLogger.LogScanResult(scanResult);
        }

        private void DrawResultTabs()
        {
            EditorGUILayout.BeginHorizontal("box");

            GUI.backgroundColor = currentTab == DisplayTab.MultiReference ? new Color(1f, 0.5f, 0.5f) : Color.white;
            if (GUILayout.Button($"多次引用 ({scanResult.multiReferenceTextureCount})", GUILayout.Height(25)))
            {
                currentTab = DisplayTab.MultiReference;
            }

            GUI.backgroundColor = currentTab == DisplayTab.SingleReference ? new Color(0.5f, 1f, 0.5f) : Color.white;
            if (GUILayout.Button($"单次引用 ({scanResult.singleReferenceTextureCount})", GUILayout.Height(25)))
            {
                currentTab = DisplayTab.SingleReference;
            }

            GUI.backgroundColor = currentTab == DisplayTab.AllTextures ? new Color(0.5f, 0.5f, 1f) : Color.white;
            if (GUILayout.Button($"全部纹理 ({scanResult.totalTextureCount})", GUILayout.Height(25)))
            {
                currentTab = DisplayTab.AllTextures;
            }

            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
        }

        private void DrawResultsSection()
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField($"扫描结果 - 共 {scanResult.totalMaterialCount} 个材质", EditorStyles.boldLabel);

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            switch (currentTab)
            {
                case DisplayTab.MultiReference:
                    DrawTextureList(scanResult.multiReferencedTextures, "被多次引用的纹理", ref foldoutMultiRef, new Color(1f, 0.8f, 0.8f));
                    break;
                case DisplayTab.SingleReference:
                    DrawTextureList(scanResult.singleReferencedTextures, "只引用一次的纹理", ref foldoutSingleRef, new Color(0.8f, 1f, 0.8f));
                    break;
                case DisplayTab.AllTextures:
                    DrawTextureList(scanResult.allTextures, "全部纹理（按引用次数排序）", ref foldoutAllTextures, new Color(0.8f, 0.8f, 1f));
                    break;
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawTextureList(List<TextureReferenceInfo> textures, string title, ref bool foldout, Color headerColor)
        {
            if (textures == null || textures.Count == 0)
            {
                EditorGUILayout.HelpBox($"{title}: 无", MessageType.Info);
                return;
            }

            GUI.backgroundColor = headerColor;
            EditorGUILayout.BeginVertical("box");
            GUI.backgroundColor = Color.white;

            foldout = EditorGUILayout.Foldout(foldout, $"{title} ({textures.Count})", true, EditorStyles.foldoutHeader);

            if (foldout)
            {
                foreach (var texInfo in textures)
                {
                    DrawTextureInfo(texInfo);
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawTextureInfo(TextureReferenceInfo texInfo)
        {
            Color bgColor = texInfo.referenceCount > 1 ? new Color(1f, 0.9f, 0.9f) : new Color(0.95f, 0.95f, 0.95f);
            GUI.backgroundColor = bgColor;
            EditorGUILayout.BeginVertical("box");
            GUI.backgroundColor = Color.white;

            EditorGUILayout.BeginHorizontal();

            if (!textureFoldouts.ContainsKey(texInfo.texturePath))
            {
                textureFoldouts[texInfo.texturePath] = false;
            }

            textureFoldouts[texInfo.texturePath] = EditorGUILayout.Foldout(
                textureFoldouts[texInfo.texturePath],
                "",
                true
            );

            GUILayout.Space(-15);

            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.ObjectField(texInfo.texture, typeof(Texture2D), false, GUILayout.Width(200));
            EditorGUI.EndDisabledGroup();

            GUIStyle countStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = texInfo.referenceCount > 1 ? Color.red : Color.green }
            };
            EditorGUILayout.LabelField($"引用次数: {texInfo.referenceCount}", countStyle, GUILayout.Width(100));

            if (GUILayout.Button("定位", GUILayout.Width(50)))
            {
                EditorGUIUtility.PingObject(texInfo.texture);
                Selection.activeObject = texInfo.texture;
            }

            EditorGUILayout.EndHorizontal();

            if (textureFoldouts[texInfo.texturePath])
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField($"路径: {texInfo.texturePath}", EditorStyles.miniLabel);

                foreach (var matRef in texInfo.referencedByMaterials)
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Space(20);
                    EditorGUILayout.LabelField($"• {matRef.materialName}", GUILayout.Width(150));
                    EditorGUILayout.LabelField($"属性: {string.Join(", ", matRef.propertyNames)}", EditorStyles.miniLabel);

                    if (GUILayout.Button("定位", GUILayout.Width(50)))
                    {
                        EditorGUIUtility.PingObject(matRef.material);
                        Selection.activeObject = matRef.material;
                    }

                    EditorGUILayout.EndHorizontal();
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2);
        }
    }
}