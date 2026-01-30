using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using UnityEngine.Profiling;
using HiHi2.AtlasTools;
using HiHi2.AtlasTools.Editor;

public class AtlasGeneratorWindow : EditorWindow
{
    private PathMode currentMode = PathMode.Auto;

    private DefaultAsset targetFolder;
    private AtlasGeneratorSettings settings;

    private DefaultAsset customTextureFolder;
    private DefaultAsset customOutputFolder;
    private string customOutputPath = "";

    private List<GenerateOptimizedAtlasEditor.AtlasResult> previewResults;
    private Vector2 scrollPos;
    private string statusMessage;
    private bool hasPreview;

    private Dictionary<int, bool> atlasSpriteFoldouts = new Dictionary<int, bool>();
    private Dictionary<int, TexturePreviewController> previewControllers = new Dictionary<int, TexturePreviewController>();
    private Dictionary<int, bool> advancedPreviewFoldouts = new Dictionary<int, bool>();

    [MenuItem("Tools/Lod图及相关工具/图集生成替换/图集生成器窗口", false, 3000)]
    public static void OpenWindowMenu()
    {
        ShowWindow(null);
    }

    public static void ShowWindow(string folderPath)
    {
        var window = GetWindow<AtlasGeneratorWindow>("图集生成器");
        window.minSize = new Vector2(520, 400);

        if (!string.IsNullOrEmpty(folderPath))
        {
            var folderAsset = AssetDatabase.LoadAssetAtPath<DefaultAsset>(folderPath);
            if (folderAsset != null)
            {
                window.targetFolder = folderAsset;
            }
        }

        if (window.settings == null)
        {
            window.settings = GenerateOptimizedAtlasEditor.LoadOrCreateSettings();
        }

        window.Focus();
    }

    private void OnDestroy()
    {
        CleanupPreviewControllers();
    }

    private void CleanupPreviewControllers()
    {
        foreach (var controller in previewControllers.Values)
        {
            if (controller != null)
            {
                controller.Dispose();
            }
        }

        previewControllers.Clear();
    }

    private void OnGUI()
    {
        DrawModeSelector();
        EditorGUILayout.Space();
        DrawFolderSection();
        EditorGUILayout.Space();
        DrawSettingsSection();
        EditorGUILayout.Space();
        DrawButtonsSection();
        EditorGUILayout.Space();
        DrawStatusSection();
        DrawPreviewSection();
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

        if (currentMode == PathMode.Auto)
        {
            EditorGUILayout.HelpBox("自动模式：输出路径为 [父级目录]/Lod/Texture", MessageType.Info);
        }
        else
        {
            EditorGUILayout.HelpBox("自定义模式：可以自由选择 Texture 源路径和图集输出路径", MessageType.Info);
        }
    }

    private void DrawFolderSection()
    {
        if (currentMode == PathMode.Auto)
        {
            DrawAutoFolderSection();
        }
        else
        {
            DrawCustomFolderSection();
        }
    }

    private void DrawAutoFolderSection()
    {
        EditorGUILayout.LabelField("目标文件夹（自动模式）", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        targetFolder = (DefaultAsset)EditorGUILayout.ObjectField(targetFolder, typeof(DefaultAsset), false);
        if (GUILayout.Button("使用当前选中", GUILayout.Width(100)))
        {
            var obj = Selection.activeObject as DefaultAsset;
            if (obj != null && AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(obj)))
            {
                targetFolder = obj;
            }
            else
            {
                EditorUtility.DisplayDialog("提示", "当前选中对象不是有效文件夹。", "确定");
            }
        }

        EditorGUILayout.EndHorizontal();

        string folderPath = targetFolder != null ? AssetDatabase.GetAssetPath(targetFolder) : "(未选择)";
        EditorGUILayout.LabelField("路径: " + folderPath);

        if (targetFolder != null)
        {
            string outputPath = AtlasPathUtility.GetAutoOutputPath(folderPath);
            EditorGUILayout.LabelField("输出路径: " + outputPath, EditorStyles.miniLabel);
        }
    }

    private void DrawCustomFolderSection()
    {
        EditorGUILayout.LabelField("自定义路径配置", EditorStyles.boldLabel);

        EditorGUILayout.BeginVertical("box");

        EditorGUILayout.LabelField("Texture 源路径", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        customTextureFolder = (DefaultAsset)EditorGUILayout.ObjectField(customTextureFolder, typeof(DefaultAsset), false);
        if (GUILayout.Button("选择", GUILayout.Width(50)))
        {
            string path = EditorUtility.OpenFolderPanel("选择 Texture 文件夹", "Assets", "");
            if (!string.IsNullOrEmpty(path))
            {
                string relativePath = "Assets" + path.Substring(Application.dataPath.Length);
                relativePath = AtlasPathUtility.NormalizePath(relativePath);
                if (AssetDatabase.IsValidFolder(relativePath))
                {
                    customTextureFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(relativePath);
                }
            }
        }

        EditorGUILayout.EndHorizontal();

        if (customTextureFolder != null)
        {
            string texPath = AssetDatabase.GetAssetPath(customTextureFolder);
            EditorGUILayout.LabelField("路径: " + texPath, EditorStyles.miniLabel);
        }

        EditorGUILayout.Space(5);

        EditorGUILayout.LabelField("图集输出路径", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();

        // 修改：使用原生的 ObjectField 替代 TextField
        customOutputFolder = (DefaultAsset)EditorGUILayout.ObjectField(customOutputFolder, typeof(DefaultAsset), false);
        if (customOutputFolder != null)
        {
            customOutputPath = AssetDatabase.GetAssetPath(customOutputFolder);
        }

        if (GUILayout.Button("选择", GUILayout.Width(50)))
        {
            string path = EditorUtility.OpenFolderPanel("选择输出文件夹", "Assets", "");
            if (!string.IsNullOrEmpty(path))
            {
                string relativePath = "Assets" + path.Substring(Application.dataPath.Length);
                customOutputPath = AtlasPathUtility.NormalizePath(relativePath);
                customOutputFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(customOutputPath);

                if (customOutputFolder == null && !AssetDatabase.IsValidFolder(customOutputPath))
                {
                    EditorUtility.DisplayDialog("提示", "路径不存在，将在生成时自动创建。", "确定");
                }
            }
        }

        EditorGUILayout.EndHorizontal();

        if (!string.IsNullOrEmpty(customOutputPath))
        {
            EditorGUILayout.LabelField("路径: " + customOutputPath, EditorStyles.miniLabel);
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawSettingsSection()
    {
        EditorGUILayout.LabelField("图集参数 (AtlasGeneratorSettings)", EditorStyles.boldLabel);

        settings = (AtlasGeneratorSettings)EditorGUILayout.ObjectField("Settings Asset", settings, typeof(AtlasGeneratorSettings), false);
        if (settings == null)
        {
            if (GUILayout.Button("自动加载或创建临时设置"))
            {
                settings = GenerateOptimizedAtlasEditor.LoadOrCreateSettings();
            }

            return;
        }

        EditorGUI.BeginChangeCheck();

        settings.padding = EditorGUILayout.IntSlider(new GUIContent("Padding", "图片间距（必须为偶数）"), settings.padding, 0, 16);
        settings.maxWastagePercent = EditorGUILayout.Slider(new GUIContent("最大空白率 %"), settings.maxWastagePercent, 0f, 50f);

        settings.minAtlasSize = EditorGUILayout.IntPopup(
            "最小图集尺寸",
            settings.minAtlasSize,
            new[] { "32", "64", "128", "256", "512", "1024", "2048", "4096" },
            new[] { 32, 64, 128, 256, 512, 1024, 2048, 4096 });

        settings.maxAtlasSize = EditorGUILayout.IntPopup(
            "最大图集尺寸",
            settings.maxAtlasSize,
            new[] { "32", "64", "128", "256", "512", "1024", "2048", "4096" },
            new[] { 32, 64, 128, 256, 512, 1024, 2048, 4096 });

        settings.allowMultipleAtlases = EditorGUILayout.Toggle("允许多张图集", settings.allowMultipleAtlases);
        settings.atlasNamePrefix = EditorGUILayout.TextField("图集名前缀", settings.atlasNamePrefix);

        if (EditorGUI.EndChangeCheck())
        {
            EditorUtility.SetDirty(settings);
        }
    }

    private void DrawButtonsSection()
    {
        EditorGUILayout.BeginHorizontal();

        bool isValid = ValidateInput();
        GUI.enabled = isValid && settings != null;

        if (GUILayout.Button("预览图集"))
        {
            PreviewAtlases();
        }

        if (GUILayout.Button("生成图集"))
        {
            GenerateAtlasesToDisk();
        }

        GUI.enabled = true;

        EditorGUILayout.EndHorizontal();
    }

    private bool ValidateInput()
    {
        if (currentMode == PathMode.Auto)
        {
            return targetFolder != null;
        }
        else
        {
            return customTextureFolder != null && !string.IsNullOrEmpty(customOutputPath);
        }
    }

    private string GetSourceFolderPath()
    {
        if (currentMode == PathMode.Auto)
        {
            return targetFolder != null ? AssetDatabase.GetAssetPath(targetFolder) : null;
        }
        else
        {
            return customTextureFolder != null ? AssetDatabase.GetAssetPath(customTextureFolder) : null;
        }
    }

    private string GetOutputFolderPath()
    {
        if (currentMode == PathMode.Auto)
        {
            string sourcePath = GetSourceFolderPath();
            if (string.IsNullOrEmpty(sourcePath)) return null;
            return GenerateOptimizedAtlasEditor.PrepareOutputFolder(sourcePath);
        }
        else
        {
            if (string.IsNullOrEmpty(customOutputPath)) return null;

            customOutputPath = AtlasPathUtility.NormalizePath(customOutputPath);

            if (!AtlasPathUtility.EnsureFolderExists(customOutputPath))
            {
                Debug.LogError($"Failed to create output folder: {customOutputPath}");
                return null;
            }

            return customOutputPath;
        }
    }

    private void DrawStatusSection()
    {
        if (!string.IsNullOrEmpty(statusMessage))
        {
            EditorGUILayout.HelpBox(statusMessage, hasPreview ? MessageType.Info : MessageType.Warning);
        }
    }

    private void DrawPreviewSection()
    {
        if (previewResults == null || previewResults.Count == 0)
            return;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("预览结果", EditorStyles.boldLabel);

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
        for (int i = 0; i < previewResults.Count; i++)
        {
            var result = previewResults[i];
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField($"图集 {i}", EditorStyles.boldLabel);

            if (result.texture != null)
            {
                DrawTexturePreview(result, i);
            }

            EditorGUILayout.LabelField($"尺寸: {result.width} x {result.height}");
            EditorGUILayout.LabelField($"空白率: {result.wastage:F2}%");
            EditorGUILayout.LabelField($"图片数量: {result.packedSprites.Count}");

            EditorGUILayout.Space(5);

            if (!atlasSpriteFoldouts.ContainsKey(i))
            {
                atlasSpriteFoldouts[i] = false;
            }

            atlasSpriteFoldouts[i] = EditorGUILayout.Foldout(
                atlasSpriteFoldouts[i],
                $"包含的图片列表 ({result.packedSprites.Count})",
                true,
                EditorStyles.foldoutHeader
            );

            if (atlasSpriteFoldouts[i])
            {
                EditorGUI.indentLevel++;

                for (int j = 0; j < result.packedSprites.Count; j++)
                {
                    var sprite = result.packedSprites[j];
                    var texInfo = sprite.textureInfo;

                    EditorGUILayout.BeginHorizontal("box");

                    Texture2D sourceTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texInfo.texturePath);

                    EditorGUI.BeginDisabledGroup(true);
                    EditorGUILayout.ObjectField(
                        $"{j}. {texInfo.name}",
                        sourceTexture,
                        typeof(Texture2D),
                        false,
                        GUILayout.ExpandWidth(true)
                    );
                    EditorGUI.EndDisabledGroup();

                    if (GUILayout.Button("定位", GUILayout.Width(50)))
                    {
                        if (sourceTexture != null)
                        {
                            EditorGUIUtility.PingObject(sourceTexture);
                            Selection.activeObject = sourceTexture;
                        }
                    }

                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Space(20);
                    EditorGUILayout.LabelField(
                        $"原始尺寸: {texInfo.originalWidth} x {texInfo.originalHeight}",
                        GUILayout.Width(220)
                    );
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Space(20);
                    EditorGUILayout.LabelField(
                        $"缩放后无Padding: {texInfo.width} x {texInfo.height}",
                        GUILayout.Width(220)
                    );
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Space(20);
                    int paddedW = texInfo.PaddedWidth(settings != null ? settings.padding : 0);
                    int paddedH = texInfo.PaddedHeight(settings != null ? settings.padding : 0);
                    EditorGUILayout.LabelField(
                        $"含Padding占用: {paddedW} x {paddedH}",
                        GUILayout.Width(220)
                    );
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawTexturePreview(GenerateOptimizedAtlasEditor.AtlasResult result, int index)
    {
        if (!advancedPreviewFoldouts.ContainsKey(index))
        {
            advancedPreviewFoldouts[index] = false;
        }

        advancedPreviewFoldouts[index] = EditorGUILayout.Foldout(
            advancedPreviewFoldouts[index],
            "高级预览选项",
            true,
            EditorStyles.foldoutHeader
        );

        if (advancedPreviewFoldouts[index])
        {
            DrawInspectorStylePreview(result, index);
        }
        else
        {
            DrawSimplePreview(result);
        }
    }

    private void DrawSimplePreview(GenerateOptimizedAtlasEditor.AtlasResult result)
    {
        float maxPreviewSize = 256f;
        float aspect = (float)result.width / result.height;
        float previewWidth = maxPreviewSize;
        float previewHeight = maxPreviewSize;

        if (aspect > 1f)
        {
            previewHeight = maxPreviewSize / aspect;
        }
        else
        {
            previewWidth = maxPreviewSize * aspect;
        }

        Rect rect = GUILayoutUtility.GetRect(previewWidth, previewHeight, GUILayout.ExpandWidth(false));
        EditorGUI.DrawTextureTransparent(rect, result.texture, ScaleMode.ScaleToFit);

        string sizeInfo = $"{result.width}x{result.height}  {result.texture.format}";
        Rect labelRect = new Rect(rect.x, rect.yMax + 2, rect.width, 18);

        GUIStyle centeredStyle = new GUIStyle(EditorStyles.miniLabel);
        centeredStyle.alignment = TextAnchor.MiddleCenter;
        centeredStyle.normal.background = MakeTex(2, 2, new Color(0.2f, 0.2f, 0.2f, 0.8f));

        GUI.Label(labelRect, sizeInfo, centeredStyle);
    }

    private void DrawInspectorStylePreview(GenerateOptimizedAtlasEditor.AtlasResult result, int index)
    {
        if (!previewControllers.TryGetValue(index, out var controller) || controller == null)
        {
            controller = new TexturePreviewController(result.texture);
            previewControllers[index] = controller;
        }

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        GUILayout.Label("纹理信息", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("格式", GUILayout.Width(80));
        EditorGUILayout.LabelField(result.texture.format.ToString());
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("尺寸", GUILayout.Width(80));
        EditorGUILayout.LabelField($"{result.width} x {result.height}");
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Mipmap", GUILayout.Width(80));
        EditorGUILayout.LabelField(result.texture.mipmapCount > 1 ? $"是 ({result.texture.mipmapCount} 级)" : "否");
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("内存占用", GUILayout.Width(80));
        float memorySize = GetTextureMemorySize(result.texture) / (1024f * 1024f);
        EditorGUILayout.LabelField($"{memorySize:F2} MB");
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(10);

        controller.DrawToolbar();

        EditorGUILayout.Space(5);

        float maxWidth = EditorGUIUtility.currentViewWidth - 40f;
        maxWidth = Mathf.Max(64f, maxWidth);

        float aspect = (float)result.width / Mathf.Max(1, result.height);
        float maxHeight = 512f;

        float previewWidth = maxWidth;
        float previewHeight = previewWidth / Mathf.Max(0.01f, aspect);
        if (previewHeight > maxHeight)
        {
            previewHeight = maxHeight;
            previewWidth = previewHeight * aspect;
        }

        Rect previewRect = GUILayoutUtility.GetRect(
            previewWidth,
            previewHeight,
            GUILayout.ExpandWidth(true)
        );

        controller.DrawPreview(previewRect);

        Rect footerRect = new Rect(
            previewRect.x,
            previewRect.yMax - 30f,
            previewRect.width,
            30f
        );

        DrawTextureFooter(footerRect, result, index);

        EditorGUILayout.EndVertical();
    }

    private void DrawTextureFooter(Rect rect, GenerateOptimizedAtlasEditor.AtlasResult result, int index)
    {
        GUIStyle footerStyle = new GUIStyle(GUI.skin.box);
        footerStyle.alignment = TextAnchor.MiddleCenter;
        footerStyle.normal.textColor = new Color(0.8f, 0.8f, 0.8f);
        footerStyle.normal.background = MakeTex(2, 2, new Color(0.2f, 0.2f, 0.2f, 0.9f));
        footerStyle.fontSize = 11;

        string colorSpace = QualitySettings.activeColorSpace == ColorSpace.Linear ? "sRGB" : "Gamma";
        float sizeMB = GetTextureMemorySize(result.texture) / (1024f * 1024f);

        string footerText = $"Atlas_{index}\n{result.width}x{result.height}  {result.texture.format} {colorSpace}  {sizeMB:F2} MB";

        GUI.Box(rect, footerText, footerStyle);
    }

    private long GetTextureMemorySize(Texture2D texture)
    {
        if (texture == null)
            return 0;

        return Profiler.GetRuntimeMemorySizeLong(texture);
    }

    private Texture2D MakeTex(int width, int height, Color col)
    {
        Color[] pix = new Color[width * height];
        for (int i = 0; i < pix.Length; i++)
            pix[i] = col;

        Texture2D result = new Texture2D(width, height);
        result.SetPixels(pix);
        result.Apply();

        return result;
    }

    private class TexturePreviewController : System.IDisposable
    {
        private Texture2D texture;
        private PreviewStyles styles;

        private bool showAlpha;
        private float mipLevel;
        private int mipCount;

        private class PreviewStyles
        {
            public GUIStyle preToolbar;
            public GUIStyle preSlider;
            public GUIStyle preSliderThumb;
            public GUIStyle preLabel;
            public GUIStyle preBackground;
            public GUIStyle preButton;

            public GUIContent alphaIcon;
            public GUIContent rgbIcon;
            public GUIContent smallMipIcon;
            public GUIContent largeMipIcon;

            public PreviewStyles()
            {
                preToolbar = "preToolbar";
                preSlider = "preSlider";
                preSliderThumb = "preSliderThumb";
                preLabel = "preLabel";
                preBackground = "preBackground";
                preButton = "preButton";

                alphaIcon = EditorGUIUtility.IconContent("PreTextureAlpha");
                rgbIcon = EditorGUIUtility.IconContent("PreTextureRGB");
                smallMipIcon = EditorGUIUtility.IconContent("PreTextureMipMapLow");
                largeMipIcon = EditorGUIUtility.IconContent("PreTextureMipMapHigh");
            }
        }

        public TexturePreviewController(Texture2D tex)
        {
            texture = tex;
            showAlpha = false;
            mipLevel = 0;
            mipCount = tex != null ? tex.mipmapCount : 1;
        }

        private void InitStyles()
        {
            if (styles == null)
            {
                styles = new PreviewStyles();
            }
        }

        public void DrawToolbar()
        {
            if (texture == null)
                return;

            InitStyles();

            EditorGUILayout.BeginHorizontal(styles.preToolbar);

            GUILayout.FlexibleSpace();

            GUIContent toggleIcon = showAlpha ? styles.alphaIcon : styles.rgbIcon;
            if (GUILayout.Toggle(showAlpha, toggleIcon, styles.preButton) != showAlpha)
            {
                showAlpha = !showAlpha;
            }

            GUILayout.Space(5);

            using (new EditorGUI.DisabledScope(mipCount <= 1))
            {
                GUILayout.Box(styles.smallMipIcon, styles.preLabel);

                mipLevel = GUILayout.HorizontalSlider(
                    mipLevel,
                    mipCount - 1,
                    0,
                    styles.preSlider,
                    styles.preSliderThumb,
                    GUILayout.Width(64)
                );
                mipLevel = Mathf.Round(mipLevel);

                GUILayout.Box(styles.largeMipIcon, styles.preLabel);
            }

            EditorGUILayout.EndHorizontal();
        }

        public void DrawPreview(Rect rect)
        {
            if (texture == null)
                return;

            InitStyles();

            if (Event.current.type == EventType.Repaint)
            {
                styles.preBackground.Draw(rect, false, false, false, false);
            }

            float currentMip = Mathf.Min(mipLevel, mipCount - 1);

            if (showAlpha)
            {
                EditorGUI.DrawTextureAlpha(rect, texture, ScaleMode.ScaleToFit, 0, currentMip);
            }
            else
            {
                EditorGUI.DrawTextureTransparent(rect, texture, ScaleMode.ScaleToFit, 0, currentMip);
            }
        }

        public void Dispose()
        {
            texture = null;
            styles = null;
        }
    }

    private void PreviewAtlases()
    {
        hasPreview = false;
        statusMessage = string.Empty;
        previewResults = null;
        atlasSpriteFoldouts.Clear();
        advancedPreviewFoldouts.Clear();
        CleanupPreviewControllers();

        string folderPath = GetSourceFolderPath();
        if (string.IsNullOrEmpty(folderPath))
        {
            statusMessage = "请先选择一个有效的目标文件夹。";
            return;
        }

        if (settings == null)
        {
            statusMessage = "请先指定 AtlasGeneratorSettings。";
            return;
        }

        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            statusMessage = "目标对象不是一个有效文件夹。";
            return;
        }

        List<TextureInfo> textures = GenerateOptimizedAtlasEditor.CollectTextures(folderPath, settings.padding);
        if (textures.Count == 0)
        {
            statusMessage = "该文件夹中没有可用于生成图集的纹理（已排除法线贴图）。";
            return;
        }

        GenerateOptimizedAtlasEditor.PrepareReadableTextures(textures);

        var oldLogHandler = Debug.unityLogger.logHandler;
        var logCapture = new LogCapture();
        Debug.unityLogger.logHandler = logCapture;

        previewResults = GenerateOptimizedAtlasEditor.GenerateAtlases(textures, settings);

        Debug.unityLogger.logHandler = oldLogHandler;

        GenerateOptimizedAtlasEditor.CleanupTemporaryTextures(textures);

        if (previewResults == null || previewResults.Count == 0)
        {
            statusMessage = "根据当前参数无法生成任何图集。\n\n";
            if (!string.IsNullOrEmpty(logCapture.lastError))
            {
                statusMessage += logCapture.lastError;
            }
            else
            {
                statusMessage += "请尝试减小 Padding 或提高最大空白率。";
            }

            hasPreview = false;
            return;
        }

        statusMessage = $"预览成功，将生成 {previewResults.Count} 张图集。";
        hasPreview = true;
    }

    private void GenerateAtlasesToDisk()
    {
        if (settings == null)
        {
            EditorUtility.DisplayDialog("错误", "请先配置参数。", "确定");
            return;
        }

        string folderPath = GetSourceFolderPath();
        if (string.IsNullOrEmpty(folderPath))
        {
            EditorUtility.DisplayDialog("错误", "请先选择目标文件夹。", "确定");
            return;
        }

        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            EditorUtility.DisplayDialog("错误", "目标对象不是一个有效文件夹。", "确定");
            return;
        }

        List<TextureInfo> textures = GenerateOptimizedAtlasEditor.CollectTextures(folderPath, settings.padding);
        if (textures.Count == 0)
        {
            EditorUtility.DisplayDialog("提示", "该文件夹中没有可用于生成图集的纹理。", "确定");
            return;
        }

        GenerateOptimizedAtlasEditor.PrepareReadableTextures(textures);
        List<GenerateOptimizedAtlasEditor.AtlasResult> finalResults = GenerateOptimizedAtlasEditor.GenerateAtlases(textures, settings);

        if (finalResults == null || finalResults.Count == 0)
        {
            GenerateOptimizedAtlasEditor.CleanupTemporaryTextures(textures);
            EditorUtility.DisplayDialog("提示", "根据当前参数无法生成任何图集，请调整参数后重试。", "确定");
            return;
        }

        string dataFolderPath = GetOutputFolderPath();
        if (string.IsNullOrEmpty(dataFolderPath))
        {
            GenerateOptimizedAtlasEditor.CleanupTemporaryTextures(textures);
            EditorUtility.DisplayDialog("错误", "无法确定输出路径。", "确定");
            return;
        }

        GenerateOptimizedAtlasEditor.SaveAtlases(finalResults, dataFolderPath, settings);

        GenerateOptimizedAtlasEditor.CleanupTemporaryTextures(textures);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "完成",
            $"图集生成完成！\n生成了 {finalResults.Count} 张图集。\n输出目录：{dataFolderPath}",
            "确定"
        );

        hasPreview = false;
        previewResults = null;
        CleanupPreviewControllers();
    }

    private class LogCapture : ILogHandler
    {
        public string lastError = string.Empty;

        public void LogFormat(LogType logType, UnityEngine.Object context, string format, params object[] args)
        {
            if (logType == LogType.Error || logType == LogType.Exception)
            {
                lastError = string.Format(format, args);
            }
        }

        public void LogException(System.Exception exception, UnityEngine.Object context)
        {
            lastError = exception.ToString();
        }
    }
}
