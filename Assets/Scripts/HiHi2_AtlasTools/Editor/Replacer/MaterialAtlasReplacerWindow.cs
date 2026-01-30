using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using HiHi2.AtlasTools;
using HiHi2.AtlasTools.Editor;

public class MaterialAtlasReplacerWindow : EditorWindow
{
    private PathMode currentMode = PathMode.Auto;

    private DefaultAsset targetFolder;
    private DefaultAsset atlasConfigFolder;
    private List<AtlasConfig> atlasConfigs = new List<AtlasConfig>();

    private DefaultAsset customMaterialSourceFolder;
    private DefaultAsset customAtlasConfigFolder;
    private DefaultAsset customMaterialOutputFolder; // 修改：从 string 改为 DefaultAsset
    private string customMaterialOutputPath = "";

    private List<Material> collectedMaterials = new List<Material>();
    private List<MaterialAtlasReplacer.MaterialProcessResult> analysisResults;

    private Vector2 scrollPos;
    private bool showMaterialList = true;
    private bool showAnalysisResults = true;
    private bool showAtlasConfigs = true;
    private bool onlyShowReplaceable = true;

    private Dictionary<Material, bool> materialFoldouts = new Dictionary<Material, bool>();

    private bool autoAnalyzed = false;
    private Rect atlasConfigListRect = Rect.zero;

    [MenuItem("Assets/Lod图及相关工具/图集生成替换/材质图集替换", false, 3000)]
    public static void ShowWindowFromAssets()
    {
        Object[] selectedObjects = Selection.objects;
        if (selectedObjects.Length == 0)
        {
            EditorUtility.DisplayDialog("提示", "请先选择Material文件夹。", "确定");
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

    [MenuItem("Assets/Lod图及相关工具/图集生成替换/材质图集替换", true)]
    public static bool ValidateShowWindowFromAssets()
    {
        if (Selection.objects.Length == 0)
            return false;

        string selectedPath = AssetDatabase.GetAssetPath(Selection.objects[0]);

        if (!AssetDatabase.IsValidFolder(selectedPath))
            return false;

        return true;

        // 新增了自定义模式不在检测是否是Material文件夹
        // string folderName = System.IO.Path.GetFileName(selectedPath);
        // return folderName.Equals("Material", System.StringComparison.OrdinalIgnoreCase);
    }

    [MenuItem("Tools/Lod图及相关工具/图集生成替换/材质图集替换工具", false, 3000)]
    public static void ShowWindowMenu()
    {
        ShowWindow(null);
    }

    public static void ShowWindow(string materialFolderPath)
    {
        var window = GetWindow<MaterialAtlasReplacerWindow>("材质图集替换");
        window.minSize = new Vector2(600, 500);

        if (!string.IsNullOrEmpty(materialFolderPath))
        {
            window.InitializeWithFolder(materialFolderPath);
        }

        window.Show();
    }

    private void InitializeWithFolder(string materialFolderPath)
    {
        currentMode = PathMode.Auto;
        targetFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(materialFolderPath);

        if (targetFolder == null)
        {
            AtlasLogger.LogError($"Failed to load folder: {materialFolderPath}");
            return;
        }

        // 修改：不再手动获取父目录，直接传递 materialFolderPath 给工具函数
        // AtlasPathUtility.GetAutoOutputPath 会自动获取其父目录并拼接 Lod/Texture
        string lodTextureFolderPath = AtlasPathUtility.GetAutoOutputPath(materialFolderPath);

        if (AssetDatabase.IsValidFolder(lodTextureFolderPath))
        {
            atlasConfigFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(lodTextureFolderPath);
            FindAtlasConfigs();

            if (atlasConfigs.Count > 0)
            {
                autoAnalyzed = false;
                EditorApplication.delayCall += () =>
                {
                    if (!autoAnalyzed && this != null)
                    {
                        AnalyzeMaterials();
                        autoAnalyzed = true;
                    }
                };
            }
        }
        else
        {
            AtlasLogger.LogWarning($"Atlas config folder not found: {lodTextureFolderPath}");
            EditorUtility.DisplayDialog(
                "提示",
                $"未找到图集配置文件夹。\n期望路径: {lodTextureFolderPath}\n\n请手动选择图集配置文件夹。",
                "确定"
            );
        }
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(10);
        DrawModeSelector();
        EditorGUILayout.Space(10);
        DrawHeader();
        EditorGUILayout.Space(10);
        DrawConfigSection();
        EditorGUILayout.Space(10);
        DrawActionButtons();
        EditorGUILayout.Space(10);
        DrawResultsSection();
    }

    private void DrawModeSelector()
    {
        EditorGUILayout.BeginHorizontal("box");
        EditorGUILayout.LabelField("路径模式", EditorStyles.boldLabel, GUILayout.Width(60));

        GUI.backgroundColor = currentMode == PathMode.Auto ? Color.green : Color.white;
        if (GUILayout.Button("自动模式", GUILayout.Height(25)))
        {
            currentMode = PathMode.Auto;
        }

        GUI.backgroundColor = currentMode == PathMode.Custom ? Color.cyan : Color.white;
        if (GUILayout.Button("自定义模式", GUILayout.Height(25)))
        {
            currentMode = PathMode.Custom;
        }

        GUI.backgroundColor = Color.white;

        EditorGUILayout.EndHorizontal();

        string helpMessage = currentMode == PathMode.Auto
            ? "自动模式：材质源路径为选中的Material文件夹，输出路径为 [父级目录]/Lod/Materials"
            : "自定义模式：可以自由选择材质球源路径、图集配置路径和新材质球保存路径";

        EditorGUILayout.HelpBox(helpMessage, MessageType.Info);
    }

    private void DrawHeader()
    {
        EditorGUILayout.LabelField("材质图集替换工具", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "此工具用于批量将材质球上的贴图替换为图集贴图，并自动设置正确的Tiling和Offset。\n\n" +
            "使用方式：\n" +
            "• 自动模式：在Project窗口中右键Material文件夹 → 材质图集替换\n" +
            "• 自定义模式：切换到自定义模式，手动选择各个路径\n" +
            "• 工具会自动找到图集配置并进行材质替换\n" +
            "• 点击\"应用替换\"完成操作",
            MessageType.Info
        );
    }

    private void DrawConfigSection()
    {
        EditorGUILayout.LabelField("配置", EditorStyles.boldLabel);

        EditorGUILayout.BeginVertical("box");

        if (currentMode == PathMode.Auto)
        {
            DrawAutoConfigSection();
        }
        else
        {
            DrawCustomConfigSection();
        }

        EditorGUILayout.Space(5);

        showAtlasConfigs = EditorGUILayout.Foldout(
            showAtlasConfigs,
            $"图集配置列表 ({atlasConfigs.Count})",
            true,
            EditorStyles.foldoutHeader
        );

        if (showAtlasConfigs)
        {
            DrawAtlasConfigList();
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawAutoConfigSection()
    {
        EditorGUI.BeginDisabledGroup(true);
        EditorGUILayout.ObjectField("材质文件夹", targetFolder, typeof(DefaultAsset), false);
        EditorGUI.EndDisabledGroup();

        if (targetFolder != null)
        {
            string path = AssetDatabase.GetAssetPath(targetFolder);
            EditorGUILayout.LabelField("路径:", path, EditorStyles.miniLabel);

            // 修改：确保显示正确的自动输出路径
            string outputPath = AtlasPathUtility.GetAutoMaterialOutputPath(path);
            EditorGUILayout.LabelField("输出路径:", outputPath, EditorStyles.miniLabel);
        }

        EditorGUILayout.Space(5);

        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginDisabledGroup(true);
        EditorGUILayout.ObjectField("图集配置文件夹", atlasConfigFolder, typeof(DefaultAsset), false);
        EditorGUI.EndDisabledGroup();

        if (GUILayout.Button("重新查找", GUILayout.Width(AtlasConstants.GUI_BUTTON_WIDTH_MEDIUM)))
        {
            FindAtlasConfigs();
        }

        EditorGUILayout.EndHorizontal();

        if (atlasConfigFolder != null)
        {
            string path = AssetDatabase.GetAssetPath(atlasConfigFolder);
            EditorGUILayout.LabelField("路径:", path, EditorStyles.miniLabel);
        }
    }

    private void DrawCustomConfigSection()
    {
        EditorGUILayout.LabelField("自定义路径配置", EditorStyles.boldLabel);

        EditorGUILayout.BeginVertical("box");

        DrawFolderSelector("材质球源路径", ref customMaterialSourceFolder, "选择材质球源文件夹");

        EditorGUILayout.Space(5);

        DrawAtlasConfigSelector();

        EditorGUILayout.Space(5);

        DrawMaterialOutputPathSelector();

        EditorGUILayout.EndVertical();
    }

    private void DrawFolderSelector(string label, ref DefaultAsset folder, string dialogTitle)
    {
        EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        folder = (DefaultAsset)EditorGUILayout.ObjectField(folder, typeof(DefaultAsset), false);
        if (GUILayout.Button("选择", GUILayout.Width(50)))
        {
            string path = EditorUtility.OpenFolderPanel(dialogTitle, "Assets", "");
            if (!string.IsNullOrEmpty(path))
            {
                string relativePath = ConvertToRelativePath(path);
                if (AssetDatabase.IsValidFolder(relativePath))
                {
                    folder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(relativePath);
                }
            }
        }

        EditorGUILayout.EndHorizontal();

        if (folder != null)
        {
            string folderPath = AssetDatabase.GetAssetPath(folder);
            EditorGUILayout.LabelField("路径: " + folderPath, EditorStyles.miniLabel);
        }
    }

    private void DrawAtlasConfigSelector()
    {
        EditorGUILayout.LabelField("图集配置文件夹", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();

        EditorGUI.BeginChangeCheck();
        DefaultAsset newAtlasConfigFolder = (DefaultAsset)EditorGUILayout.ObjectField(customAtlasConfigFolder, typeof(DefaultAsset), false);
        if (EditorGUI.EndChangeCheck())
        {
            customAtlasConfigFolder = newAtlasConfigFolder;
            RefreshAtlasConfigsOnFolderChange();
        }

        if (GUILayout.Button("选择", GUILayout.Width(50)))
        {
            string path = EditorUtility.OpenFolderPanel("选择图集配置文件夹", "Assets", "");
            if (!string.IsNullOrEmpty(path))
            {
                string relativePath = ConvertToRelativePath(path);
                if (AssetDatabase.IsValidFolder(relativePath))
                {
                    customAtlasConfigFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(relativePath);
                    RefreshAtlasConfigsOnFolderChange();
                }
            }
        }

        if (GUILayout.Button("重新查找", GUILayout.Width(AtlasConstants.GUI_BUTTON_WIDTH_MEDIUM)))
        {
            RefreshAtlasConfigsOnFolderChange();
        }

        EditorGUILayout.EndHorizontal();

        if (customAtlasConfigFolder != null)
        {
            string configPath = AssetDatabase.GetAssetPath(customAtlasConfigFolder);
            EditorGUILayout.LabelField("路径: " + configPath, EditorStyles.miniLabel);
        }
    }

    private void DrawMaterialOutputPathSelector()
    {
        EditorGUILayout.LabelField("新材质球保存路径", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();

        // 修改：使用原生的 ObjectField
        customMaterialOutputFolder = (DefaultAsset)EditorGUILayout.ObjectField(customMaterialOutputFolder, typeof(DefaultAsset), false);
        if (customMaterialOutputFolder != null)
        {
            customMaterialOutputPath = AssetDatabase.GetAssetPath(customMaterialOutputFolder);
        }

        if (GUILayout.Button("选择", GUILayout.Width(50)))
        {
            string path = EditorUtility.OpenFolderPanel("选择新材质球保存文件夹", "Assets", "");
            if (!string.IsNullOrEmpty(path))
            {
                customMaterialOutputPath = ConvertToRelativePath(path);
                customMaterialOutputFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(customMaterialOutputPath);
            }
        }

        EditorGUILayout.EndHorizontal();

        if (!string.IsNullOrEmpty(customMaterialOutputPath))
        {
            EditorGUILayout.LabelField("路径: " + customMaterialOutputPath, EditorStyles.miniLabel);
        }
    }

    private string ConvertToRelativePath(string absolutePath)
    {
        return AtlasCommon.ConvertToRelativePath(absolutePath);
    }

    private void DrawAtlasConfigList()
    {
        EditorGUI.indentLevel++;
        EditorGUILayout.BeginVertical("box");

        // 判断是否允许拖拽：仅当图集配置文件夹为空或没有找到配置时允许
        bool allowDragDrop = customAtlasConfigFolder == null || atlasConfigs.Count == 0;

        if (atlasConfigs.Count == 0)
        {
            DrawEmptyAtlasConfigsUI(allowDragDrop);
        }
        else
        {
            DrawAtlasConfigItems();
        }

        EditorGUILayout.EndVertical();

        // 获取刚刚绘制的区域
        if (Event.current.type == EventType.Repaint)
        {
            atlasConfigListRect = GUILayoutUtility.GetLastRect();
        }

        // 仅在自定义模式下且允许拖拽时处理拖拽
        if (currentMode == PathMode.Custom && allowDragDrop)
        {
            HandleAtlasConfigDragAndDrop();
        }

        EditorGUI.indentLevel--;
    }

    private void HandleAtlasConfigDragAndDrop()
    {
        Event evt = Event.current;
        EventType eventType = evt.type;

        // 只处理拖拽相关事件
        if (eventType != EventType.DragUpdated && eventType != EventType.DragPerform)
            return;

        // 检查鼠标是否在拖拽区域内
        if (atlasConfigListRect == Rect.zero || !atlasConfigListRect.Contains(evt.mousePosition))
            return;

        // 检查拖拽的对象是否包含有效文件夹
        bool hasValidFolder = false;
        string validFolderPath = null;

        if (DragAndDrop.objectReferences != null && DragAndDrop.objectReferences.Length > 0)
        {
            foreach (Object draggedObject in DragAndDrop.objectReferences)
            {
                if (draggedObject == null)
                    continue;

                string path = AssetDatabase.GetAssetPath(draggedObject);
                if (!string.IsNullOrEmpty(path) && AssetDatabase.IsValidFolder(path))
                {
                    hasValidFolder = true;
                    validFolderPath = path;
                    break;
                }
            }
        }

        if (!hasValidFolder)
            return;

        // 显示拖拽视觉反馈
        DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

        // 处理拖拽完成事件
        if (eventType == EventType.DragPerform)
        {
            DragAndDrop.AcceptDrag();

            customAtlasConfigFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(validFolderPath);
            RefreshAtlasConfigsOnFolderChange();

            AtlasLogger.Log($"<color=green>已通过拖拽设置图集配置文件夹:</color> {validFolderPath}");
        }

        evt.Use();
    }

    private void DrawEmptyAtlasConfigsUI(bool allowDragDrop)
    {
        if (allowDragDrop && currentMode == PathMode.Custom)
        {
            EditorGUILayout.LabelField("未找到图集配置，可拖拽文件夹到此处", EditorStyles.centeredGreyMiniLabel);
        }
        else
        {
            EditorGUILayout.LabelField("未找到图集配置", EditorStyles.centeredGreyMiniLabel);
        }

        EditorGUILayout.Space(5);
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();

        if (GUILayout.Button("手动选择图集配置文件夹", GUILayout.Width(180)))
        {
            string path = EditorUtility.OpenFolderPanel("选择图集配置文件夹", "Assets", "");
            if (!string.IsNullOrEmpty(path))
            {
                string relativePath = ConvertToRelativePath(path);
                if (AssetDatabase.IsValidFolder(relativePath))
                {
                    if (currentMode == PathMode.Auto)
                    {
                        atlasConfigFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(relativePath);
                        FindAtlasConfigs();
                    }
                    else
                    {
                        customAtlasConfigFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(relativePath);
                        RefreshAtlasConfigsOnFolderChange();
                    }
                }
            }
        }

        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
    }

    private void DrawAtlasConfigItems()
    {
        for (int i = 0; i < atlasConfigs.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();

            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.ObjectField($"图集 {i}", atlasConfigs[i], typeof(AtlasConfig), false);
            EditorGUI.EndDisabledGroup();

            if (GUILayout.Button("定位", GUILayout.Width(AtlasConstants.GUI_BUTTON_WIDTH_SMALL)))
            {
                EditorGUIUtility.PingObject(atlasConfigs[i]);
                Selection.activeObject = atlasConfigs[i];
            }

            EditorGUILayout.EndHorizontal();

            if (atlasConfigs[i] != null)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField($"图集: {(atlasConfigs[i].atlasTexture ? atlasConfigs[i].atlasTexture.name : "未设置")}", EditorStyles.miniLabel);
                EditorGUILayout.LabelField($"包含: {atlasConfigs[i].spriteCount} 张图片", EditorStyles.miniLabel);
                EditorGUI.indentLevel--;
            }
        }
    }

    private void DrawActionButtons()
    {
        EditorGUILayout.BeginHorizontal();

        bool canAnalyze = ValidateAnalyzeInput();
        GUI.enabled = canAnalyze;

        if (GUILayout.Button("分析材质", GUILayout.Height(30)))
        {
            AnalyzeMaterials();
        }

        GUI.enabled = analysisResults != null && analysisResults.Count > 0 && GetReplaceableCount() > 0;

        if (GUILayout.Button("应用替换", GUILayout.Height(30)))
        {
            ShowApplyConfirmation();
        }

        GUI.enabled = true;

        EditorGUILayout.EndHorizontal();
    }

    private void ShowApplyConfirmation()
    {
        int replaceableCount = GetReplaceableCount();
        int totalTextureCount = GetTotalReplaceableTextureCount();

        if (EditorUtility.DisplayDialog(
                "确认替换",
                $"确定要替换 {replaceableCount} 个材质中的 {totalTextureCount} 个贴图吗？\n\n此操作会修改材质文件。",
                "确定",
                "取消"))
        {
            ApplyReplacements();
        }
    }

    private bool ValidateAnalyzeInput()
    {
        if (currentMode == PathMode.Auto)
        {
            return targetFolder != null && atlasConfigs.Count > 0 && HasValidAtlasConfig();
        }
        else
        {
            return customMaterialSourceFolder != null &&
                   customAtlasConfigFolder != null &&
                   atlasConfigs.Count > 0 &&
                   HasValidAtlasConfig() &&
                   !string.IsNullOrEmpty(customMaterialOutputPath);
        }
    }

    private string GetMaterialSourcePath()
    {
        if (currentMode == PathMode.Auto)
        {
            return targetFolder != null ? AssetDatabase.GetAssetPath(targetFolder) : null;
        }
        else
        {
            return customMaterialSourceFolder != null ? AssetDatabase.GetAssetPath(customMaterialSourceFolder) : null;
        }
    }

    private string GetMaterialOutputPath()
    {
        if (currentMode == PathMode.Auto)
        {
            return AtlasPathUtility.GetAutoMaterialOutputPath(GetMaterialSourcePath());
        }
        else
        {
            return customMaterialOutputPath;
        }
    }

    private void FindAtlasConfigs()
    {
        DefaultAsset configFolder = currentMode == PathMode.Auto ? atlasConfigFolder : customAtlasConfigFolder;

        if (configFolder == null)
        {
            AtlasLogger.LogWarning("Atlas config folder is null");
            return;
        }

        string folderPath = AssetDatabase.GetAssetPath(configFolder);
        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            EditorUtility.DisplayDialog("错误", "图集配置文件夹无效。", "确定");
            return;
        }

        atlasConfigs = MaterialAtlasReplacer.CollectAtlasConfigsFromFolder(folderPath);

        if (atlasConfigs.Count == 0)
        {
            AtlasLogger.LogWarning($"No valid atlas configs found in {folderPath}");
        }
    }

    private void AnalyzeMaterials()
    {
        collectedMaterials.Clear();
        analysisResults = null;
        materialFoldouts.Clear();

        string folderPath = GetMaterialSourcePath();
        if (string.IsNullOrEmpty(folderPath))
        {
            EditorUtility.DisplayDialog("错误", "请先选择材质文件夹。", "确定");
            return;
        }

        if (!HasValidAtlasConfig())
        {
            EditorUtility.DisplayDialog("错误", "请至少添加一个有效的图集配置。", "确定");
            return;
        }

        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            EditorUtility.DisplayDialog("错误", "目标对象不是有效文件夹。", "确定");
            return;
        }

        collectedMaterials = MaterialAtlasReplacer.CollectMaterialsFromFolder(folderPath);

        if (collectedMaterials.Count == 0)
        {
            EditorUtility.DisplayDialog("提示", "该文件夹中没有找到材质文件。", "确定");
            return;
        }

        List<AtlasConfig> validConfigs = GetValidAtlasConfigs();

        analysisResults = MaterialAtlasReplacer.AnalyzeMaterials(collectedMaterials, validConfigs);

        ShowAnalysisResults();
    }

    private List<AtlasConfig> GetValidAtlasConfigs()
    {
        List<AtlasConfig> validConfigs = new List<AtlasConfig>();
        foreach (var config in atlasConfigs)
        {
            if (config != null && config.atlasTexture != null)
            {
                validConfigs.Add(config);
            }
        }

        return validConfigs;
    }

    private void ShowAnalysisResults()
    {
        int replaceableCount = GetReplaceableCount();
        int totalTextureCount = GetTotalReplaceableTextureCount();

        string message = $"共找到 {collectedMaterials.Count} 个材质\n" +
                         $"其中 {replaceableCount} 个材质包含可替换的纹理\n" +
                         $"共有 {totalTextureCount} 个贴图可以替换\n" +
                         $"使用了 {GetValidAtlasConfigs().Count} 个图集配置";

        EditorUtility.DisplayDialog("分析完成", message, "确定");
        AtlasLogger.Log($"<color=green>Analysis complete</color>\n{message}");
    }

    private void ApplyReplacements()
    {
        if (analysisResults == null || analysisResults.Count == 0)
        {
            EditorUtility.DisplayDialog("错误", "请先进行分析。", "确定");
            return;
        }

        string materialSourcePath = GetMaterialSourcePath();
        string materialOutputPath = GetMaterialOutputPath();

        List<Material> copiedMaterials = MaterialCopyUtility.CopyMaterials(
            collectedMaterials,
            materialSourcePath,
            materialOutputPath);

        if (copiedMaterials == null || copiedMaterials.Count == 0)
        {
            EditorUtility.DisplayDialog("错误", "材质拷贝失败。", "确定");
            return;
        }

        List<MaterialAtlasReplacer.MaterialProcessResult> copiedResults =
            MaterialAtlasReplacer.AnalyzeMaterials(copiedMaterials, atlasConfigs);

        MaterialAtlasReplacer.ApplyReplacements(copiedResults, true);

        EditorUtility.DisplayDialog(
            "完成",
            $"材质替换已完成！\n已拷贝并修改 {copiedMaterials.Count} 个材质。\n输出路径: {materialOutputPath}\n请查看Console了解详情。",
            "确定");

        AnalyzeMaterials();
    }

    private void DrawResultsSection()
    {
        if (collectedMaterials.Count > 0)
        {
            EditorGUILayout.Space(5);
            showMaterialList = EditorGUILayout.Foldout(
                showMaterialList,
                $"收集到的材质 ({collectedMaterials.Count})",
                true,
                EditorStyles.foldoutHeader
            );

            if (showMaterialList)
            {
                DrawMaterialList();
            }
        }

        if (analysisResults != null && analysisResults.Count > 0)
        {
            EditorGUILayout.Space(10);

            EditorGUILayout.BeginHorizontal();
            showAnalysisResults = EditorGUILayout.Foldout(
                showAnalysisResults,
                $"分析结果 ({GetReplaceableCount()} 个材质可替换，共 {GetTotalReplaceableTextureCount()} 个贴图)",
                true,
                EditorStyles.foldoutHeader
            );

            GUILayout.FlexibleSpace();
            onlyShowReplaceable = GUILayout.Toggle(onlyShowReplaceable, "仅显示可替换", GUILayout.Width(100));
            EditorGUILayout.EndHorizontal();

            if (showAnalysisResults)
            {
                DrawAnalysisResults();
            }
        }
    }

    private void DrawMaterialList()
    {
        EditorGUI.indentLevel++;
        EditorGUILayout.BeginVertical("box");

        foreach (Material mat in collectedMaterials)
        {
            if (mat == null)
                continue;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.ObjectField(mat, typeof(Material), false);

            if (GUILayout.Button("定位", GUILayout.Width(AtlasConstants.GUI_BUTTON_WIDTH_SMALL)))
            {
                EditorGUIUtility.PingObject(mat);
                Selection.activeObject = mat;
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(2);
        }

        EditorGUILayout.EndVertical();
        EditorGUI.indentLevel--;
    }

    private void DrawAnalysisResults()
    {
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        foreach (var result in analysisResults)
        {
            if (result.material == null)
                continue;

            if (onlyShowReplaceable && !result.hasReplaceableTextures)
                continue;

            DrawMaterialResult(result);
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawMaterialResult(MaterialAtlasReplacer.MaterialProcessResult result)
    {
        Color originalBgColor = GUI.backgroundColor;
        GUI.backgroundColor = result.hasReplaceableTextures
            ? new Color(0.3f, 0.6f, 0.3f, 0.3f)
            : new Color(0.5f, 0.5f, 0.5f, 0.2f);

        EditorGUILayout.BeginVertical("box");
        GUI.backgroundColor = originalBgColor;

        if (!materialFoldouts.ContainsKey(result.material))
        {
            materialFoldouts[result.material] = false;
        }

        EditorGUILayout.BeginHorizontal();

        materialFoldouts[result.material] = EditorGUILayout.Foldout(
            materialFoldouts[result.material],
            "",
            true
        );

        GUILayout.Space(-15);

        GUIStyle nameLabelStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12 };
        EditorGUILayout.LabelField(result.material.name, nameLabelStyle, GUILayout.ExpandWidth(false));

        GUILayout.Space(10);

        string statusText = result.hasReplaceableTextures
            ? $"✓ {GetReplaceableCountForMaterial(result)} 个可替换"
            : "无可替换";

        GUIStyle labelStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            normal = { textColor = result.hasReplaceableTextures ? new Color(0.4f, 1f, 0.4f) : Color.gray },
            fontStyle = FontStyle.Bold
        };

        EditorGUILayout.LabelField(statusText, labelStyle, GUILayout.Width(100));

        GUILayout.FlexibleSpace();

        if (GUILayout.Button("定位", GUILayout.Width(AtlasConstants.GUI_BUTTON_WIDTH_SMALL)))
        {
            EditorGUIUtility.PingObject(result.material);
            Selection.activeObject = result.material;
        }

        EditorGUILayout.EndHorizontal();

        if (materialFoldouts[result.material])
        {
            EditorGUILayout.Space(3);
            EditorGUI.indentLevel++;

            foreach (var info in result.replacements)
            {
                DrawReplacementInfo(info);
                EditorGUILayout.Space(3);
            }

            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(8);
    }

    private void DrawReplacementInfo(MaterialAtlasReplacer.ReplacementInfo info)
    {
        Color originalBgColor = GUI.backgroundColor;
        GUI.backgroundColor = info.canReplace
            ? new Color(0.2f, 0.5f, 0.8f, 0.2f)
            : new Color(0.6f, 0.4f, 0.2f, 0.2f);

        EditorGUILayout.BeginVertical("box");
        GUI.backgroundColor = originalBgColor;

        EditorGUILayout.BeginHorizontal();
        GUIStyle propLabelStyle = new GUIStyle(EditorStyles.label) { fontStyle = FontStyle.Bold };
        EditorGUILayout.LabelField($"属性: {info.propertyName}", propLabelStyle, GUILayout.Width(150));

        EditorGUI.BeginDisabledGroup(true);
        EditorGUILayout.ObjectField(info.originalTexture, typeof(Texture2D), false);
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(3);

        if (info.canReplace)
        {
            DrawSuccessInfo(info);
        }
        else
        {
            DrawWarningInfo(info);
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawSuccessInfo(MaterialAtlasReplacer.ReplacementInfo info)
    {
        EditorGUILayout.BeginVertical("box");

        GUIStyle successStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            normal = { textColor = new Color(0.3f, 0.8f, 0.3f) },
            fontStyle = FontStyle.Bold
        };
        EditorGUILayout.LabelField($"✓ {info.message}", successStyle);

        if (info.spriteInfo != null)
        {
            EditorGUILayout.Space(2);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Offset (偏移):", GUILayout.Width(100));
            EditorGUILayout.LabelField($"X: {info.spriteInfo.uvRect.x:F4}  Y: {info.spriteInfo.uvRect.y:F4}", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Tiling (缩放):", GUILayout.Width(100));
            EditorGUILayout.LabelField($"W: {info.spriteInfo.uvRect.width:F4}  H: {info.spriteInfo.uvRect.height:F4}", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawWarningInfo(MaterialAtlasReplacer.ReplacementInfo info)
    {
        EditorGUILayout.BeginVertical("box");

        GUIStyle warningStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            normal = { textColor = new Color(1f, 0.7f, 0.3f) },
            fontStyle = FontStyle.Bold
        };
        EditorGUILayout.LabelField($"⚠ {info.message}", warningStyle);

        EditorGUILayout.EndVertical();
    }

    private void RefreshAtlasConfigsOnFolderChange()
    {
        if (customAtlasConfigFolder != null)
        {
            string folderPath = AssetDatabase.GetAssetPath(customAtlasConfigFolder);
            if (AssetDatabase.IsValidFolder(folderPath))
            {
                atlasConfigs = MaterialAtlasReplacer.CollectAtlasConfigsFromFolder(folderPath);
            }
            else
            {
                atlasConfigs.Clear();
                customAtlasConfigFolder = null;
            }
        }
        else
        {
            atlasConfigs.Clear();
        }
        Repaint();
    }

    private bool HasValidAtlasConfig()
    {
        foreach (var config in atlasConfigs)
        {
            if (config != null && config.atlasTexture != null)
                return true;
        }

        return false;
    }

    private int GetReplaceableCount()
    {
        if (analysisResults == null)
            return 0;

        int count = 0;
        foreach (var result in analysisResults)
        {
            if (result.hasReplaceableTextures)
                count++;
        }

        return count;
    }

    private int GetTotalReplaceableTextureCount()
    {
        if (analysisResults == null)
            return 0;

        int count = 0;
        foreach (var result in analysisResults)
        {
            foreach (var info in result.replacements)
            {
                if (info.canReplace)
                    count++;
            }
        }

        return count;
    }

    private int GetReplaceableCountForMaterial(MaterialAtlasReplacer.MaterialProcessResult result)
    {
        int count = 0;
        foreach (var info in result.replacements)
        {
            if (info.canReplace)
                count++;
        }

        return count;
    }
}
