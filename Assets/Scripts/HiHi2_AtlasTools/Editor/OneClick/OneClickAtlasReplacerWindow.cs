using UnityEngine;
using UnityEditor;
using System.IO;

namespace HiHi2.AtlasTools.Editor
{
    public class OneClickAtlasReplacerWindow : EditorWindow
    {
        private PathMode currentMode = PathMode.Auto;
        
        private DefaultAsset targetFolder;
        private AtlasGeneratorSettings settings;

        private string materialFolderPath;
        private string textureFolderPath;

        private DefaultAsset customTextureFolder;
        private DefaultAsset customMaterialFolder;
        private DefaultAsset customAtlasOutputFolder;
        private DefaultAsset customMaterialOutputFolder;
        private string customAtlasOutputPath = "";
        private string customMaterialOutputPath = "";

        private bool foundMaterialFolder;
        private bool foundTextureFolder;

        private Vector2 scrollPos;
        private string logMessage = "";
        private MessageType logType = MessageType.Info;

        [MenuItem("Assets/图集生成替换/一键图集生成与材质替换", false, 0)]
        private static void ShowWindowFromAssets()
        {
            Object[] selectedObjects = Selection.objects;
            if (selectedObjects.Length == 0)
            {
                EditorUtility.DisplayDialog("提示", "请先选择服装项目根目录。", "确定");
                return;
            }

            string selectedPath = AssetDatabase.GetAssetPath(selectedObjects[0]);
            if (!AssetDatabase.IsValidFolder(selectedPath))
            {
                EditorUtility.DisplayDialog("提示", "请选择一个文件夹。", "确定");
                return;
            }

            ShowWindow(selectedPath);
        }

        [MenuItem("Assets/图集生成替换/一键图集生成与材质替换", true)]
        private static bool ValidateShowWindowFromAssets()
        {
            if (Selection.objects.Length == 0) return false;
            string selectedPath = AssetDatabase.GetAssetPath(Selection.objects[0]);
            return AssetDatabase.IsValidFolder(selectedPath);
        }

        [MenuItem("Tools/图集生成替换/一键图集生成与材质替换工具", false, 0)]
        public static void ShowWindowMenu() => ShowWindow(null);

        public static void ShowWindow(string folderPath)
        {
            var window = GetWindow<OneClickAtlasReplacerWindow>("一键图集替换");
            window.minSize = new Vector2(600, 500);
            if (!string.IsNullOrEmpty(folderPath)) window.InitializeWithFolder(folderPath);
            window.Show();
        }

        private void InitializeWithFolder(string folderPath)
        {
            currentMode = PathMode.Auto;
            targetFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(folderPath);
            AnalyzeStructure(folderPath);
            if (settings == null) settings = GenerateOptimizedAtlasEditor.LoadOrCreateSettings();
        }

        private void AnalyzeStructure(string rootPath)
        {
            materialFolderPath = AtlasPathUtility.NormalizePath(Path.Combine(rootPath, "Material"));
            textureFolderPath = AtlasPathUtility.NormalizePath(Path.Combine(rootPath, "Texture"));
            foundMaterialFolder = AssetDatabase.IsValidFolder(materialFolderPath);
            foundTextureFolder = AssetDatabase.IsValidFolder(textureFolderPath);

            if (foundMaterialFolder && foundTextureFolder)
            {
                logMessage = $"检测到标准结构！\n材质: {materialFolderPath}\n贴图: {textureFolderPath}";
                logType = MessageType.Info;
            }
            else
            {
                logMessage = "结构不完整：\n" + 
                    (!foundMaterialFolder ? "- 缺少 Material 文件夹\n" : "") + 
                    (!foundTextureFolder ? "- 缺少 Texture 文件夹\n" : "");
                logType = MessageType.Warning;
            }
        }

        private void OnGUI()
        {
            DrawHeader();
            EditorGUILayout.Space(5);
            DrawModeSelector();
            EditorGUILayout.Space(5);
            
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            DrawConfigSection();
            EditorGUILayout.Space(10);
            DrawActionButtons();
            EditorGUILayout.Space(10);
            DrawLogSection();
            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginVertical("helpBox");
            EditorGUILayout.LabelField("一键图集生成与材质替换工具", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("一键完成：生成图集 -> 拷贝材质 -> 贴图替换。", MessageType.None);
            EditorGUILayout.EndVertical();
        }

        private void DrawModeSelector()
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Toggle(currentMode == PathMode.Auto, "自动模式", "ButtonLeft", GUILayout.Height(25))) 
                currentMode = PathMode.Auto;
            if (GUILayout.Toggle(currentMode == PathMode.Custom, "自定义模式", "ButtonRight", GUILayout.Height(25))) 
                currentMode = PathMode.Custom;
            EditorGUILayout.EndHorizontal();
        }

        private void DrawConfigSection()
        {
            EditorGUILayout.LabelField("路径配置", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");
            if (currentMode == PathMode.Auto) DrawAutoConfigSection();
            else DrawCustomConfigSection();
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("图集参数", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");
            DrawAtlasSettings();
            EditorGUILayout.EndVertical();
        }

        private void DrawAutoConfigSection()
        {
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.ObjectField("项目根目录", targetFolder, typeof(DefaultAsset), false);
            EditorGUILayout.TextField("材质路径", materialFolderPath);
            EditorGUILayout.TextField("贴图路径", textureFolderPath);
            EditorGUI.EndDisabledGroup();
        }

        private void DrawCustomConfigSection()
        {
            customTextureFolder = DrawFolderSelector("Texture 源路径", customTextureFolder);
            customMaterialFolder = DrawFolderSelector("材质球源路径", customMaterialFolder);
            
            // 修改：使用 DrawFolderSelector 统一行为
            customAtlasOutputFolder = DrawFolderSelector("图集输出路径", customAtlasOutputFolder);
            if (customAtlasOutputFolder != null) customAtlasOutputPath = AssetDatabase.GetAssetPath(customAtlasOutputFolder);
            
            customMaterialOutputFolder = DrawFolderSelector("新材质保存路径", customMaterialOutputFolder);
            if (customMaterialOutputFolder != null) customMaterialOutputPath = AssetDatabase.GetAssetPath(customMaterialOutputFolder);
        }

        private DefaultAsset DrawFolderSelector(string label, DefaultAsset asset)
        {
            EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginHorizontal();
            var result = (DefaultAsset)EditorGUILayout.ObjectField(asset, typeof(DefaultAsset), false);
            if (GUILayout.Button("选择", GUILayout.Width(50)))
            {
                string path = EditorUtility.OpenFolderPanel($"选择 {label}", "Assets", "");
                if (!string.IsNullOrEmpty(path))
                {
                    string relative = AtlasPathUtility.NormalizePath("Assets" + path.Substring(Application.dataPath.Length));
                    result = AssetDatabase.LoadAssetAtPath<DefaultAsset>(relative);
                }
            }
            EditorGUILayout.EndHorizontal();
            return result;
        }

        private void DrawAtlasSettings()
        {
            settings = (AtlasGeneratorSettings)EditorGUILayout.ObjectField("配置资源", settings, typeof(AtlasGeneratorSettings), false);
            if (settings == null)
            {
                if (GUILayout.Button("自动加载/创建配置")) 
                    settings = GenerateOptimizedAtlasEditor.LoadOrCreateSettings();
                return;
            }

            EditorGUI.BeginChangeCheck();
            settings.padding = EditorGUILayout.IntSlider("间距 (Padding)", settings.padding, 0, 16);
            settings.maxWastagePercent = EditorGUILayout.Slider("最大空白率 %", settings.maxWastagePercent, 0f, 50f);
            settings.maxAtlasSize = EditorGUILayout.IntPopup("最大图集尺寸", settings.maxAtlasSize, 
                new[] { "512", "1024", "2048", "4096" }, new[] { 512, 1024, 2048, 4096 });
            settings.allowMultipleAtlases = EditorGUILayout.Toggle("允许多图集", settings.allowMultipleAtlases);
            if (EditorGUI.EndChangeCheck()) EditorUtility.SetDirty(settings);
        }

        private void DrawActionButtons()
        {
            GUI.enabled = ValidateInput() && settings != null;
            if (GUILayout.Button("执行一键处理", GUILayout.Height(40)))
            {
                if (EditorUtility.DisplayDialog("确认", "操作将生成图集并替换材质，是否继续？", "确定", "取消"))
                {
                    ExecuteProcess();
                }
            }
            GUI.enabled = true;
        }

        private void ExecuteProcess()
        {
            logMessage = "";
            
            var paths = new OneClickAtlasProcessor.ProcessPaths
            {
                textureSource = GetTextureSourcePath(),
                materialSource = GetMaterialSourcePath(),
                atlasOutput = GetAtlasOutputPath(),
                materialOutput = GetMaterialOutputPath()
            };

            bool success = OneClickAtlasProcessor.Execute(paths, settings, (msg, progress) => {
                logMessage += $"[{System.DateTime.Now:HH:mm:ss}] {msg}\n";
                logType = progress >= 1f ? MessageType.Info : 
                          (msg.Contains("错误") || msg.Contains("Error") ? MessageType.Error : MessageType.Info);
                Repaint();
            });

            if (success)
            {
                EditorUtility.DisplayDialog("完成", "一键处理已完成！\n请查看日志了解详情。", "确定");
            }
        }

        private bool ValidateInput()
        {
            if (currentMode == PathMode.Auto) 
                return targetFolder != null && foundMaterialFolder && foundTextureFolder;
            return customTextureFolder != null && customMaterialFolder != null && 
                   !string.IsNullOrEmpty(customAtlasOutputPath) && !string.IsNullOrEmpty(customMaterialOutputPath);
        }

        private string GetTextureSourcePath() => currentMode == PathMode.Auto ? textureFolderPath : AssetDatabase.GetAssetPath(customTextureFolder);
        private string GetMaterialSourcePath() => currentMode == PathMode.Auto ? materialFolderPath : AssetDatabase.GetAssetPath(customMaterialFolder);
        
        private string GetAtlasOutputPath()
        {
            if (currentMode == PathMode.Auto) 
                return GenerateOptimizedAtlasEditor.PrepareOutputFolder(GetTextureSourcePath());
            AtlasPathUtility.EnsureFolderExists(customAtlasOutputPath);
            return customAtlasOutputPath;
        }

        private string GetMaterialOutputPath() => currentMode == PathMode.Auto ? 
            AtlasPathUtility.GetAutoMaterialOutputPath(GetMaterialSourcePath()) : customMaterialOutputPath;

        private void DrawLogSection()
        {
            if (string.IsNullOrEmpty(logMessage)) return;
            EditorGUILayout.LabelField("执行日志", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(logMessage, logType);
        }
    }
}