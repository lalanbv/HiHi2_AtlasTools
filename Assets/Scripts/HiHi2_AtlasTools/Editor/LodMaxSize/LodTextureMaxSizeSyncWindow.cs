using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace HiHi2.AtlasTools.Editor
{
    public class LodTextureMaxSizeSyncWindow : EditorWindow
    {
        private PathMode currentMode = PathMode.Auto;

        private DefaultAsset projectFolderAsset;
        private string projectFolderPath = "";

        private DefaultAsset customLodTextureFolder;
        private DefaultAsset customSourceTextureFolder;

        private Vector2 scrollPosition;
        private List<TextureMaxSizeMatchInfo> matchInfoList = new List<TextureMaxSizeMatchInfo>();
        private bool hasScanned = false;
        private string statusMessage = "";
        private MessageType statusMessageType = MessageType.Info;

        private Rect folderDropRect = Rect.zero;
        private Rect customModeBoxRect = Rect.zero;

        [MenuItem(LodMaxSizeConstants.MENU_PATH_ASSETS, false, 3100)]
        public static void ShowWindowFromAssets()
        {
            var window = GetWindow<LodTextureMaxSizeSyncWindow>(LodMaxSizeConstants.WINDOW_TITLE);
            window.minSize = new Vector2(LodMaxSizeConstants.WINDOW_MIN_WIDTH, LodMaxSizeConstants.WINDOW_MIN_HEIGHT);
            window.TrySetFolderFromSelection();
        }

        [MenuItem(LodMaxSizeConstants.MENU_PATH_ASSETS, true)]
        private static bool ShowWindowValidation()
        {
            if (Selection.activeObject == null)
                return true;

            string path = AssetDatabase.GetAssetPath(Selection.activeObject);
            return string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path);
        }

        [MenuItem(LodMaxSizeConstants.MENU_PATH_TOOLS, false, 3100)]
        public static void ShowWindowFromMenu()
        {
            var window = GetWindow<LodTextureMaxSizeSyncWindow>(LodMaxSizeConstants.WINDOW_TITLE);
            window.minSize = new Vector2(LodMaxSizeConstants.WINDOW_MIN_WIDTH, LodMaxSizeConstants.WINDOW_MIN_HEIGHT);
        }

        private void TrySetFolderFromSelection()
        {
            if (Selection.activeObject == null)
                return;

            string path = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (string.IsNullOrEmpty(path) || !AssetDatabase.IsValidFolder(path))
                return;

            if (LodMaxSizeScanner.ValidateProjectFolder(path, out _, out _))
            {
                projectFolderAsset = Selection.activeObject as DefaultAsset;
                projectFolderPath = path;
                hasScanned = false;
                matchInfoList.Clear();

                EditorApplication.delayCall += () =>
                {
                    if (this != null)
                    {
                        ScanTextures();
                    }
                };
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            DrawModeSelector();
            EditorGUILayout.Space(10);
            DrawHeader();
            EditorGUILayout.Space(10);
            DrawFolderSelection();
            EditorGUILayout.Space(10);
            DrawActionButtons();
            EditorGUILayout.Space(10);
            DrawStatusMessage();
            DrawMatchInfoList();
        }

        private void DrawModeSelector()
        {
            EditorGUILayout.BeginHorizontal("box");
            EditorGUILayout.LabelField("路径模式", EditorStyles.boldLabel, GUILayout.Width(60));

            GUI.backgroundColor = currentMode == PathMode.Auto ? Color.green : Color.white;
            if (GUILayout.Button("自动模式", GUILayout.Height(25)))
            {
                if (currentMode != PathMode.Auto)
                {
                    currentMode = PathMode.Auto;
                    hasScanned = false;
                    matchInfoList.Clear();
                    SetStatus("", MessageType.Info);
                }
            }

            GUI.backgroundColor = currentMode == PathMode.Custom ? Color.cyan : Color.white;
            if (GUILayout.Button("自定义模式", GUILayout.Height(25)))
            {
                if (currentMode != PathMode.Custom)
                {
                    currentMode = PathMode.Custom;
                    hasScanned = false;
                    matchInfoList.Clear();
                    SetStatus("", MessageType.Info);
                }
            }

            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            string helpMessage = currentMode == PathMode.Auto
                ? "自动模式：选择项目根文件夹，自动查找 LOD/Texture 和 Texture 子目录"
                : "自定义模式：可以自由选择 LOD纹理路径 和 源纹理(Lod0)路径";

            EditorGUILayout.HelpBox(helpMessage, MessageType.Info);
        }

        private void DrawHeader()
        {
            EditorGUILayout.LabelField("LOD Texture MaxSize 同步工具", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "此工具用于同步 LOD 纹理的 MaxSize 与源 Lod0 纹理保持一致。\n\n" +
                "使用方式：\n" +
                "• 自动模式：选择包含 LOD/Texture 和 Texture 的项目根文件夹\n" +
                "• 自定义模式：分别选择 LOD纹理 和 源Lod0纹理 文件夹\n" +
                "• 扫描后可查看并修改目标MaxSize值\n" +
                "• 点击 \"应用所有修改\" 完成同步",
                MessageType.Info
            );
        }

        private void DrawFolderSelection()
        {
            if (currentMode == PathMode.Auto)
            {
                DrawAutoModeSelection();
            }
            else
            {
                DrawCustomModeSelection();
            }
        }

        private void DrawAutoModeSelection()
        {
            EditorGUILayout.LabelField("项目文件夹（自动模式）", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();

            EditorGUI.BeginChangeCheck();
            var newFolderAsset = EditorGUILayout.ObjectField(
                projectFolderAsset,
                typeof(DefaultAsset),
                false
            ) as DefaultAsset;

            if (EditorGUI.EndChangeCheck() && newFolderAsset != projectFolderAsset)
            {
                HandleFolderAssetChange(newFolderAsset);
            }

            if (GUILayout.Button("从Selection获取", GUILayout.Width(AtlasConstants.GUI_BUTTON_WIDTH_MEDIUM + 20)))
            {
                GetFolderFromSelection();
            }

            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(projectFolderPath))
            {
                EditorGUILayout.LabelField("路径: " + projectFolderPath, EditorStyles.miniLabel);

                string lodPath = AtlasPathUtility.NormalizePath(
                    Path.Combine(projectFolderPath, LodMaxSizeConstants.LOD_TEXTURE_SUBFOLDER));
                string texPath = AtlasPathUtility.NormalizePath(
                    Path.Combine(projectFolderPath, LodMaxSizeConstants.TEXTURE_SUBFOLDER));

                EditorGUILayout.LabelField($"LOD纹理: {lodPath}", EditorStyles.miniLabel);
                EditorGUILayout.LabelField($"源纹理: {texPath}", EditorStyles.miniLabel);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawCustomModeSelection()
        {
            EditorGUILayout.LabelField("自定义路径配置", EditorStyles.boldLabel);

            Rect boxStartRect = GUILayoutUtility.GetRect(0, 0);
            
            EditorGUILayout.BeginVertical("box");

            DrawFolderSelector("LOD纹理文件夹", ref customLodTextureFolder, "选择LOD纹理文件夹");
            EditorGUILayout.Space(5);
            DrawFolderSelector("源Lod0纹理文件夹", ref customSourceTextureFolder, "选择源Lod0纹理文件夹");

            EditorGUILayout.EndVertical();
            
            if (Event.current.type == EventType.Repaint)
            {
                Rect boxEndRect = GUILayoutUtility.GetLastRect();
                customModeBoxRect = new Rect(boxStartRect.x, boxStartRect.y, boxEndRect.width, boxEndRect.yMax - boxStartRect.y);
            }

            HandleCustomModeDragAndDrop();
        }

        private void DrawFolderSelector(string label, ref DefaultAsset folder, string dialogTitle)
        {
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();

            EditorGUI.BeginChangeCheck();
            folder = (DefaultAsset)EditorGUILayout.ObjectField(folder, typeof(DefaultAsset), false);
            if (EditorGUI.EndChangeCheck())
            {
                hasScanned = false;
                matchInfoList.Clear();
            }

            if (GUILayout.Button("选择", GUILayout.Width(AtlasConstants.GUI_BUTTON_WIDTH_SMALL)))
            {
                string path = EditorUtility.OpenFolderPanel(dialogTitle, "Assets", "");
                if (!string.IsNullOrEmpty(path))
                {
                    string relativePath = AtlasCommon.ConvertToRelativePath(path);
                    if (AssetDatabase.IsValidFolder(relativePath))
                    {
                        folder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(relativePath);
                        hasScanned = false;
                        matchInfoList.Clear();
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

        private void HandleCustomModeDragAndDrop()
        {
            Event evt = Event.current;
            if (evt.type != EventType.DragUpdated && evt.type != EventType.DragPerform)
                return;

            if (customModeBoxRect == Rect.zero)
                return;

            if (!customModeBoxRect.Contains(evt.mousePosition))
                return;

            bool hasValidFolder = false;
            DefaultAsset validFolder = null;

            if (DragAndDrop.objectReferences != null && DragAndDrop.objectReferences.Length > 0)
            {
                foreach (var draggedObject in DragAndDrop.objectReferences)
                {
                    if (draggedObject == null) continue;

                    string path = AssetDatabase.GetAssetPath(draggedObject);
                    if (!string.IsNullOrEmpty(path) && AssetDatabase.IsValidFolder(path))
                    {
                        hasValidFolder = true;
                        validFolder = draggedObject as DefaultAsset;
                        break;
                    }
                }
            }

            if (!hasValidFolder) return;

            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

            if (evt.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();

                bool shouldAutoScan = false;
                
                if (customLodTextureFolder == null)
                {
                    customLodTextureFolder = validFolder;
                    AtlasLogger.Log($"<color=green>已设置LOD纹理文件夹:</color> {AssetDatabase.GetAssetPath(validFolder)}");
                    shouldAutoScan = customSourceTextureFolder != null;
                }
                else if (customSourceTextureFolder == null)
                {
                    customSourceTextureFolder = validFolder;
                    AtlasLogger.Log($"<color=green>已设置源Lod0纹理文件夹:</color> {AssetDatabase.GetAssetPath(validFolder)}");
                    shouldAutoScan = true;
                }

                hasScanned = false;
                matchInfoList.Clear();
                
                if (shouldAutoScan)
                {
                    EditorApplication.delayCall += () =>
                    {
                        if (this != null && customLodTextureFolder != null && customSourceTextureFolder != null)
                        {
                            ScanTextures();
                        }
                    };
                }
            }

            evt.Use();
        }

        private void HandleFolderAssetChange(DefaultAsset newAsset)
        {
            if (newAsset == null)
            {
                projectFolderAsset = null;
                projectFolderPath = "";
                hasScanned = false;
                matchInfoList.Clear();
                SetStatus("", MessageType.Info);
                return;
            }

            string path = AssetDatabase.GetAssetPath(newAsset);

            if (!AssetDatabase.IsValidFolder(path))
            {
                SetStatus("请拖入有效的文件夹，而不是文件", MessageType.Warning);
                return;
            }

            try
            {
                if (LodMaxSizeScanner.ValidateProjectFolder(path, out string lodPath, out string texPath))
                {
                    projectFolderAsset = newAsset;
                    projectFolderPath = path;
                    hasScanned = false;
                    matchInfoList.Clear();
                    ScanTextures();
                }
                else
                {
                    SetStatus($"文件夹结构不正确，需要包含 {LodMaxSizeConstants.LOD_TEXTURE_SUBFOLDER} 和 {LodMaxSizeConstants.TEXTURE_SUBFOLDER} 子目录", MessageType.Error);
                }
            }
            catch (UnauthorizedAccessException)
            {
                SetStatus("无权限访问该路径，请检查文件夹权限", MessageType.Error);
            }
            catch (Exception ex)
            {
                SetStatus($"路径访问异常: {ex.Message}", MessageType.Error);
                AtlasLogger.LogError($"路径访问异常: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private void GetFolderFromSelection()
        {
            if (Selection.activeObject == null)
            {
                SetStatus("请先在Project窗口选择一个文件夹", MessageType.Warning);
                return;
            }

            string path = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (AssetDatabase.IsValidFolder(path))
            {
                HandleFolderAssetChange(Selection.activeObject as DefaultAsset);
            }
            else
            {
                SetStatus("请选择一个文件夹，而不是文件", MessageType.Warning);
            }
        }

        private void DrawActionButtons()
        {
            EditorGUILayout.BeginHorizontal();

            bool canScan = CanScan();
            GUI.enabled = canScan;
            if (GUILayout.Button("扫描纹理", GUILayout.Height(30)))
            {
                ScanTextures();
            }

            GUI.enabled = hasScanned && matchInfoList.Any(m => m.needsSync || m.isModified);
            if (GUILayout.Button("应用所有修改", GUILayout.Height(30)))
            {
                ApplyAllChanges();
            }

            GUI.enabled = hasScanned && matchInfoList.Any(m => m.isMatched);
            if (GUILayout.Button("重置为Lod0值", GUILayout.Height(30)))
            {
                ResetAllToLod0();
            }

            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            if (hasScanned)
            {
                DrawStatistics();
            }
        }

        private bool CanScan()
        {
            if (currentMode == PathMode.Auto)
            {
                return !string.IsNullOrEmpty(projectFolderPath);
            }
            else
            {
                return customLodTextureFolder != null && customSourceTextureFolder != null;
            }
        }

        private void DrawStatistics()
        {
            EditorGUILayout.BeginHorizontal();
            int totalCount = matchInfoList.Count;
            int matchedCount = matchInfoList.Count(m => m.isMatched);
            int needsSyncCount = matchInfoList.Count(m => m.needsSync);
            int modifiedCount = matchInfoList.Count(m => m.isModified);

            GUIStyle statsStyle = new GUIStyle(EditorStyles.label)
            {
                richText = true
            };

            string statsText = $"统计: 总计 <b>{totalCount}</b> | 已匹配 <b>{matchedCount}</b> | " +
                               $"需同步 <color=yellow><b>{needsSyncCount}</b></color> | " +
                               $"已修改待应用 <color=cyan><b>{modifiedCount}</b></color>";

            EditorGUILayout.LabelField(statsText, statsStyle);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawStatusMessage()
        {
            if (!string.IsNullOrEmpty(statusMessage))
            {
                EditorGUILayout.HelpBox(statusMessage, statusMessageType);
                EditorGUILayout.Space(5);
            }
        }

        private void ScanTextures()
        {
            matchInfoList.Clear();
            hasScanned = true;

            string scanPath = currentMode == PathMode.Auto ? projectFolderPath : null;

            if (currentMode == PathMode.Auto)
            {
                matchInfoList = LodMaxSizeScanner.ScanTextures(scanPath, out var result);
                SetStatus(result.message, result.needsSyncCount > 0 ? MessageType.Warning : MessageType.Info);
            }
            else
            {
                matchInfoList = ScanTexturesCustomMode();
            }
        }

        private List<TextureMaxSizeMatchInfo> ScanTexturesCustomMode()
        {
            if (customLodTextureFolder == null || customSourceTextureFolder == null)
            {
                SetStatus("请先选择LOD纹理文件夹和源Lod0纹理文件夹", MessageType.Warning);
                return new List<TextureMaxSizeMatchInfo>();
            }

            string lodTexturePath = AssetDatabase.GetAssetPath(customLodTextureFolder);
            string sourceTexturePath = AssetDatabase.GetAssetPath(customSourceTextureFolder);

            var results = LodMaxSizeScanner.ScanTexturesCustomMode(lodTexturePath, sourceTexturePath, out var result);
            SetStatus(result.message, result.needsSyncCount > 0 ? MessageType.Warning : MessageType.Info);

            return results;
        }

        private void ApplyAllChanges()
        {
            var result = LodMaxSizeSyncProcessor.ApplyAllChanges(matchInfoList);
            SetStatus(result.message, result.success ? MessageType.Info : MessageType.Error);
        }

        private void ResetAllToLod0()
        {
            int needApplyCount = LodMaxSizeSyncProcessor.ResetAllToLod0(matchInfoList);

            if (needApplyCount == 0)
            {
                SetStatus("所有纹理的MaxSize已与Lod0一致，无需修改", MessageType.Info);
                Repaint();
                return;
            }

            if (EditorUtility.DisplayDialog("确认应用",
                    $"将重置并应用 {needApplyCount} 个纹理的MaxSize为Lod0的值，是否继续？",
                    "确认应用", "取消"))
            {
                ApplyAllChanges();
            }
            else
            {
                Repaint();
                SetStatus($"已重置 {needApplyCount} 个目标值为Lod0，点击'应用所有修改'来保存", MessageType.Info);
            }
        }

        private void DrawMatchInfoList()
        {
            if (!hasScanned) return;

            if (matchInfoList.Count == 0)
            {
                EditorGUILayout.HelpBox("未找到任何匹配的纹理。请检查文件夹结构是否正确。", MessageType.Warning);
                return;
            }

            EditorGUILayout.LabelField("纹理匹配列表:", EditorStyles.boldLabel);

            DrawTableHeader();

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            foreach (var info in matchInfoList)
            {
                DrawMatchInfoRow(info);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawTableHeader()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            EditorGUILayout.LabelField("", GUILayout.Width(LodMaxSizeConstants.PREVIEW_SIZE));
            EditorGUILayout.LabelField("LOD纹理", GUILayout.Width(LodMaxSizeConstants.NAME_COLUMN_WIDTH));
            EditorGUILayout.LabelField("当前MaxSize", GUILayout.Width(LodMaxSizeConstants.MAXSIZE_COLUMN_WIDTH));
            EditorGUILayout.LabelField("", GUILayout.Width(LodMaxSizeConstants.PREVIEW_SIZE));
            EditorGUILayout.LabelField("Lod0纹理", GUILayout.Width(LodMaxSizeConstants.NAME_COLUMN_WIDTH));
            EditorGUILayout.LabelField("Lod0 MaxSize", GUILayout.Width(LodMaxSizeConstants.MAXSIZE_COLUMN_WIDTH));
            EditorGUILayout.LabelField("目标MaxSize", GUILayout.Width(LodMaxSizeConstants.TARGET_COLUMN_WIDTH));
            EditorGUILayout.LabelField("操作", GUILayout.Width(LodMaxSizeConstants.ACTION_COLUMN_WIDTH));
            EditorGUILayout.EndHorizontal();
        }

        private void DrawMatchInfoRow(TextureMaxSizeMatchInfo info)
        {
            Color originalColor = GUI.backgroundColor;
            Color originalContentColor = GUI.contentColor;

            GUI.backgroundColor = GetRowBackgroundColor(info);

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox, GUILayout.Height(LodMaxSizeConstants.ROW_HEIGHT));

            DrawTexturePreview(info.lodTexturePreview);
            DrawTextureName(info.lodTextureName, info.lodTexturePath);
            DrawCurrentMaxSize(info.lodTextureCurrentMaxSize);

            if (info.isMatched)
            {
                DrawTexturePreview(info.sourceTexturePreview);
                DrawTextureName(info.sourceTextureName, info.sourceTexturePath);
                DrawSourceMaxSize(info.sourceLod0MaxSize, originalContentColor);
                DrawTargetMaxSizeSelector(info, originalContentColor);
                DrawApplyButton(info);
            }
            else
            {
                GUILayout.Label("", GUILayout.Width(LodMaxSizeConstants.PREVIEW_SIZE), GUILayout.Height(LodMaxSizeConstants.PREVIEW_SIZE));
                EditorGUILayout.LabelField(info.errorMessage ?? "未匹配", GUILayout.Width(400));
            }

            EditorGUILayout.EndHorizontal();
            GUI.backgroundColor = originalColor;
        }

        private Color GetRowBackgroundColor(TextureMaxSizeMatchInfo info)
        {
            if (info.isModified)
                return new Color(0.6f, 0.8f, 1f);
            if (info.needsSync)
                return new Color(1f, 0.9f, 0.5f);
            if (!info.isMatched)
                return new Color(1f, 0.7f, 0.7f);
            return new Color(0.7f, 1f, 0.7f);
        }

        private void DrawTexturePreview(Texture2D texture)
        {
            if (texture != null)
            {
                GUILayout.Label(texture, GUILayout.Width(LodMaxSizeConstants.PREVIEW_SIZE), GUILayout.Height(LodMaxSizeConstants.PREVIEW_SIZE));
            }
            else
            {
                GUILayout.Label("", GUILayout.Width(LodMaxSizeConstants.PREVIEW_SIZE), GUILayout.Height(LodMaxSizeConstants.PREVIEW_SIZE));
            }
        }

        private void DrawTextureName(string name, string path)
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(LodMaxSizeConstants.NAME_COLUMN_WIDTH));
            if (GUILayout.Button(name, EditorStyles.linkLabel))
            {
                PingAsset(path);
            }

            EditorGUILayout.LabelField(GetShortPath(path), EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
        }

        private void DrawCurrentMaxSize(int maxSize)
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(LodMaxSizeConstants.MAXSIZE_COLUMN_WIDTH));
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField(maxSize.ToString(), EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();
        }

        private void DrawSourceMaxSize(int maxSize, Color originalContentColor)
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(LodMaxSizeConstants.MAXSIZE_COLUMN_WIDTH));
            GUILayout.FlexibleSpace();
            GUI.contentColor = Color.cyan;
            EditorGUILayout.LabelField(maxSize.ToString(), EditorStyles.boldLabel);
            GUI.contentColor = originalContentColor;
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();
        }

        private void DrawTargetMaxSizeSelector(TextureMaxSizeMatchInfo info, Color originalContentColor)
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(LodMaxSizeConstants.TARGET_COLUMN_WIDTH));
            GUILayout.FlexibleSpace();

            int[] maxSizeOptions = LodMaxSizeScanner.GetMaxSizeOptions();
            EditorGUI.BeginChangeCheck();
            int selectedIndex = Array.IndexOf(maxSizeOptions, info.targetMaxSize);
            if (selectedIndex < 0)
            {
                selectedIndex = Array.IndexOf(maxSizeOptions, 1024);
                if (selectedIndex < 0)
                {
                    selectedIndex = 0;
                }
            }

            int newIndex = EditorGUILayout.Popup(selectedIndex,
                maxSizeOptions.Select(x => x.ToString()).ToArray(),
                GUILayout.Width(100));

            if (EditorGUI.EndChangeCheck() && newIndex >= 0 && newIndex < maxSizeOptions.Length)
            {
                info.targetMaxSize = maxSizeOptions[newIndex];
                info.isModified = info.targetMaxSize != info.lodTextureCurrentMaxSize;
                info.needsSync = info.lodTextureCurrentMaxSize != info.sourceLod0MaxSize;
                Repaint();
            }

            if (info.targetMaxSize != info.lodTextureCurrentMaxSize)
            {
                GUI.contentColor = Color.yellow;
                EditorGUILayout.LabelField($"({info.lodTextureCurrentMaxSize}→{info.targetMaxSize})", EditorStyles.miniLabel);
                GUI.contentColor = originalContentColor;
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();
        }

        private void DrawApplyButton(TextureMaxSizeMatchInfo info)
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(LodMaxSizeConstants.ACTION_COLUMN_WIDTH));
            GUILayout.FlexibleSpace();
            GUI.enabled = info.isModified;
            if (GUILayout.Button("应用", GUILayout.Width(AtlasConstants.GUI_BUTTON_WIDTH_SMALL)))
            {
                var result = LodMaxSizeSyncProcessor.ApplySingleChange(info);
                SetStatus(result.message, result.success ? MessageType.Info : MessageType.Error);
            }

            GUI.enabled = true;
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();
        }

        private string GetShortPath(string fullPath)
        {
            if (string.IsNullOrEmpty(fullPath)) return "";
            string fileName = Path.GetFileName(fullPath);
            string dirName = Path.GetFileName(Path.GetDirectoryName(fullPath));
            return $".../{dirName}/{fileName}";
        }

        private void PingAsset(string assetPath)
        {
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
            if (asset != null)
            {
                EditorGUIUtility.PingObject(asset);
                Selection.activeObject = asset;
            }
        }

        private void SetStatus(string message, MessageType type)
        {
            statusMessage = message;
            statusMessageType = type;
            Repaint();
        }
    }
}