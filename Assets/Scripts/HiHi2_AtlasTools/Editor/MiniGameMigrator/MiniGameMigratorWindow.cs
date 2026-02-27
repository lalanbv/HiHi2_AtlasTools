using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HiHi2.AtlasTools.Editor
{
    public class MiniGameMigratorWindow : EditorWindow
    {
        private const string MENU_PATH = "Tools/Lod图及相关工具/小程序资源迁移/";
        private const string WINDOW_TITLE = "小程序资源迁移工具";
        private const float WINDOW_MIN_WIDTH = 900f;
        private const float WINDOW_MIN_HEIGHT = 600f;

        private const float ROW_HEIGHT = 28f;
        private const float PREVIEW_SIZE = 24f;
        private const float CHECKBOX_WIDTH = 30f;
        private const float NAME_WIDTH = 150f;
        private const float STATUS_WIDTH = 120f;
        private const float CATEGORY_WIDTH = 80f;

        private DefaultAsset sourceFolderAsset;
        private string sourceFolderPath = "";

        private List<AvatarObjectInfo> scannedObjects = new List<AvatarObjectInfo>();
        private HashSet<string> selectedObjects = new HashSet<string>();
        private bool hasScanned = false;
        private Vector2 scrollPosition;

        private MigrationOptions options = new MigrationOptions();

        private string statusMessage = "";
        private MessageType statusType = MessageType.Info;

        private bool selectAll = false;

        [MenuItem(MENU_PATH + "迁移工具窗口", false, 3200)]
        public static void ShowWindow()
        {
            var window = GetWindow<MiniGameMigratorWindow>(WINDOW_TITLE);
            window.minSize = new Vector2(WINDOW_MIN_WIDTH, WINDOW_MIN_HEIGHT);
            window.Show();
        }

        [MenuItem("Assets/Lod图及相关工具/小程序资源迁移/迁移选中文件夹", false, 3200)]
        public static void MigrateFromSelection()
        {
            var window = GetWindow<MiniGameMigratorWindow>(WINDOW_TITLE);
            window.minSize = new Vector2(WINDOW_MIN_WIDTH, WINDOW_MIN_HEIGHT);
            window.TrySetFolderFromSelection();
            window.Show();
        }

        [MenuItem("Assets/Lod图及相关工具/小程序资源迁移/迁移选中文件夹", true)]
        private static bool MigrateFromSelectionValidation()
        {
            if (Selection.activeObject == null)
                return false;

            string path = AssetDatabase.GetAssetPath(Selection.activeObject);
            return !string.IsNullOrEmpty(path) && AssetDatabase.IsValidFolder(path);
        }

        /// <summary>
        /// 尝试从选择获取文件夹
        /// </summary>
        private void TrySetFolderFromSelection()
        {
            if (Selection.activeObject == null)
                return;

            string path = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (string.IsNullOrEmpty(path) || !AssetDatabase.IsValidFolder(path))
                return;

            sourceFolderAsset = Selection.activeObject as DefaultAsset;
            sourceFolderPath = path;

            EditorApplication.delayCall += () =>
            {
                if (this != null)
                {
                    ScanObjects();
                }
            };
        }

        /// <summary>
        /// 绘制工具窗口
        /// </summary>
        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            DrawHeader();
            EditorGUILayout.Space(10);
            DrawSourceSelection();
            EditorGUILayout.Space(10);
            DrawOptions();
            EditorGUILayout.Space(10);
            DrawActionButtons();
            EditorGUILayout.Space(5);
            DrawStatusMessage();
            EditorGUILayout.Space(10);
            DrawObjectList();
        }

        /// <summary>
        /// 绘制工具标题
        /// </summary>
        private void DrawHeader()
        {
            EditorGUILayout.LabelField("小程序资源迁移工具", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "此工具用于将主项目的Avatar资源迁移到小程序项目目录。\n\n" +
                "功能说明：\n" +
                "• 自动检测物件是否满足迁移条件（LOD文件夹、Mesh文件夹）\n" +
                "• 可选择LOD级别（lod1/lod2/lod3）优化纹理\n" +
                "• 可选择使用原始Mesh或LOD Mesh\n" +
                "• 可选择是否创建优化后的Prefab\n\n" +
                "目录结构：\n" +
                "• 源目录: Assets/Art/Avatar/BasicAvatar/[分类]/[物件]\n" +
                "• 目标目录: Assets/Art_MiniGame/Avatar/BasicAvatar/[分类]/[物件]",
                MessageType.Info
            );
        }

        /// <summary>
        /// 绘制源文件夹选择
        /// </summary>
        private void DrawSourceSelection()
        {
            EditorGUILayout.LabelField("源文件夹", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();

            EditorGUI.BeginChangeCheck();
            sourceFolderAsset = (DefaultAsset)EditorGUILayout.ObjectField(
                sourceFolderAsset,
                typeof(DefaultAsset),
                false
            );

            if (EditorGUI.EndChangeCheck())
            {
                if (sourceFolderAsset != null)
                {
                    sourceFolderPath = AssetDatabase.GetAssetPath(sourceFolderAsset);
                    hasScanned = false;
                    scannedObjects.Clear();
                    selectedObjects.Clear();
                }
                else
                {
                    sourceFolderPath = "";
                }
            }

            if (GUILayout.Button("从选择获取", GUILayout.Width(80)))
            {
                GetFolderFromSelection();
            }

            if (GUILayout.Button("扫描", GUILayout.Width(60)))
            {
                ScanObjects();
            }

            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(sourceFolderPath))
            {
                EditorGUILayout.LabelField("路径: " + sourceFolderPath, EditorStyles.miniLabel);
            }

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// 绘制迁移选项
        /// </summary>
        private void DrawOptions()
        {
            EditorGUILayout.LabelField("迁移选项", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("图集模式:", GUILayout.Width(80));
            options.atlasTextureMode = (AtlasTextureMode)EditorGUILayout.EnumPopup(options.atlasTextureMode, GUILayout.Width(120));
            EditorGUILayout.LabelField(
                options.atlasTextureMode == AtlasTextureMode.Original ? "(使用原始图集)" : $"(使用 _lod{(int)options.lodLevel} 图集)",
                EditorStyles.miniLabel
            );
            EditorGUILayout.EndHorizontal();

            if (options.atlasTextureMode == AtlasTextureMode.Lod)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("LOD级别:", GUILayout.Width(80));
                options.lodLevel = (LodLevel)EditorGUILayout.EnumPopup(options.lodLevel, GUILayout.Width(100));
                EditorGUILayout.LabelField($"(使用 _lod{(int)options.lodLevel} 纹理)", EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Mesh类型:", GUILayout.Width(80));
            options.meshType = (MeshType)EditorGUILayout.EnumPopup(options.meshType, GUILayout.Width(100));
            EditorGUILayout.LabelField(
                options.meshType == MeshType.Original ? "(使用原始Mesh)" : "(使用 _70 后缀Mesh)",
                EditorStyles.miniLabel
            );
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("创建Prefab:", GUILayout.Width(80));
            options.createPrefab = EditorGUILayout.Toggle(options.createPrefab, GUILayout.Width(20));
            EditorGUILayout.LabelField("(在目标目录创建优化后的Prefab)", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("覆盖已存在:", GUILayout.Width(80));
            options.overwriteExisting = EditorGUILayout.Toggle(options.overwriteExisting, GUILayout.Width(20));
            EditorGUILayout.LabelField("(覆盖目标目录中已存在的文件)", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// 绘制操作按钮
        /// </summary>
        private void DrawActionButtons()
        {
            EditorGUILayout.BeginHorizontal();

            GUI.enabled = hasScanned && selectedObjects.Count > 0;

            if (GUILayout.Button($"迁移选中 ({selectedObjects.Count})", GUILayout.Height(30)))
            {
                MigrateSelected();
            }

            GUI.enabled = hasScanned && HasAnyCanMigrate();

            if (GUILayout.Button($"迁移全部有效物件", GUILayout.Height(30)))
            {
                MigrateAllValid();
            }

            GUI.enabled = true;

            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// 绘制状态信息
        /// </summary>
        private void DrawStatusMessage()
        {
            if (!string.IsNullOrEmpty(statusMessage))
            {
                EditorGUILayout.HelpBox(statusMessage, statusType);
            }
        }

        /// <summary>
        /// 绘制物件列表
        /// </summary>
        private void DrawObjectList()
        {
            if (!hasScanned)
                return;

            EditorGUILayout.LabelField("扫描结果", EditorStyles.boldLabel);

            DrawStatistics();
            DrawTableHeader();

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            foreach (var info in scannedObjects)
            {
                DrawObjectRow(info);
            }

            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// 绘制统计信息
        /// </summary>
        private void DrawStatistics()
        {
            int total = scannedObjects.Count;
            int valid = CountCanMigrate(scannedObjects);
            int selected = selectedObjects.Count;

            GUIStyle statsStyle = new GUIStyle(EditorStyles.label) { richText = true };

            EditorGUILayout.BeginHorizontal();
            string statsText = $"总计: <b>{total}</b> | 可迁移: <color=green><b>{valid}</b></color> | 已选择: <color=cyan><b>{selected}</b></color>";
            EditorGUILayout.LabelField(statsText, statsStyle);

            EditorGUI.BeginChangeCheck();
            bool newSelectAll = EditorGUILayout.ToggleLeft("全选", selectAll, GUILayout.Width(60));
            if (EditorGUI.EndChangeCheck())
            {
                selectAll = newSelectAll;
                if (selectAll)
                {
                    foreach (var info in scannedObjects)
                    {
                        if (info.CanMigrate)
                        {
                            selectedObjects.Add(info.sourcePath);
                        }
                    }
                }
                else
                {
                    selectedObjects.Clear();
                }
            }

            EditorGUILayout.EndHorizontal();

            var categoryGroups = GetCategoryGroups(scannedObjects);

            EditorGUILayout.BeginHorizontal();
            var parts = new List<string>();
            foreach (var group in categoryGroups)
            {
                int catTotal = group.Value.Count;
                int catValid = CountCanMigrate(group.Value);
                int catSelected = CountSelected(group.Value);
                parts.Add($"<b>[{group.Key}]</b> {catTotal}/{catValid}/{catSelected}");
            }

            string catStatsText = "分类统计 (总计/可迁移/已选): " + string.Join("   ", parts);
            EditorGUILayout.LabelField(catStatsText, statsStyle);
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// 绘制表格标题
        /// </summary>
        private void DrawTableHeader()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            EditorGUILayout.LabelField("", GUILayout.Width(CHECKBOX_WIDTH));
            EditorGUILayout.LabelField("物件名称", GUILayout.Width(NAME_WIDTH));
            EditorGUILayout.LabelField("分类", GUILayout.Width(CATEGORY_WIDTH));
            EditorGUILayout.LabelField("LOD", GUILayout.Width(35));
            EditorGUILayout.LabelField("Mesh", GUILayout.Width(35));
            EditorGUILayout.LabelField("Prefab", GUILayout.Width(45));
            EditorGUILayout.LabelField("状态", GUILayout.Width(STATUS_WIDTH));
            EditorGUILayout.LabelField("说明", GUILayout.ExpandWidth(true));
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// 绘制物件行
        /// </summary>
        /// <param name="info"></param>
        private void DrawObjectRow(AvatarObjectInfo info)
        {
            Color originalColor = GUI.backgroundColor;

            if (!info.CanMigrate)
            {
                GUI.backgroundColor = new Color(1f, 0.7f, 0.7f);
            }
            else if (selectedObjects.Contains(info.sourcePath))
            {
                GUI.backgroundColor = new Color(0.7f, 0.9f, 1f);
            }
            else
            {
                GUI.backgroundColor = new Color(0.9f, 1f, 0.9f);
            }

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox, GUILayout.Height(ROW_HEIGHT));

            bool isSelected = selectedObjects.Contains(info.sourcePath);
            EditorGUI.BeginChangeCheck();
            bool newSelected = EditorGUILayout.Toggle(isSelected, GUILayout.Width(CHECKBOX_WIDTH));
            if (EditorGUI.EndChangeCheck())
            {
                if (newSelected)
                    selectedObjects.Add(info.sourcePath);
                else
                    selectedObjects.Remove(info.sourcePath);
            }

            GUI.enabled = true;

            if (GUILayout.Button(info.objectName, EditorStyles.linkLabel, GUILayout.Width(NAME_WIDTH)))
            {
                PingAsset(info.sourcePath);
            }

            EditorGUILayout.LabelField(info.category ?? "-", GUILayout.Width(CATEGORY_WIDTH));

            GUI.backgroundColor = info.hasLodFolder ? Color.green : Color.red;
            EditorGUILayout.LabelField(info.hasLodFolder ? "✓" : "✗", GUILayout.Width(35));

            GUI.backgroundColor = info.hasMeshFolder ? Color.green : Color.red;
            EditorGUILayout.LabelField(info.hasMeshFolder ? "✓" : "✗", GUILayout.Width(35));

            GUI.backgroundColor = info.hasPrefab ? Color.green : Color.yellow;
            EditorGUILayout.LabelField(info.hasPrefab ? "✓" : "✗", GUILayout.Width(45));

            GUI.backgroundColor = originalColor;

            GUIStyle statusStyle = new GUIStyle(EditorStyles.label);
            if (info.CanMigrate)
            {
                statusStyle.normal.textColor = Color.green;
                EditorGUILayout.LabelField("可迁移", statusStyle, GUILayout.Width(STATUS_WIDTH));
            }
            else
            {
                statusStyle.normal.textColor = Color.red;
                EditorGUILayout.LabelField("不可迁移", statusStyle, GUILayout.Width(STATUS_WIDTH));
            }

            EditorGUILayout.LabelField(info.invalidReason ?? "-", EditorStyles.miniLabel, GUILayout.ExpandWidth(true));

            EditorGUILayout.EndHorizontal();
            GUI.backgroundColor = originalColor;
        }

        /// <summary>
        /// 从选择获取文件夹
        /// </summary>
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
                sourceFolderAsset = Selection.activeObject as DefaultAsset;
                sourceFolderPath = path;
                hasScanned = false;
                scannedObjects.Clear();
                selectedObjects.Clear();
                ScanObjects();
            }
            else
            {
                SetStatus("请选择一个文件夹", MessageType.Warning);
            }
        }

        /// <summary>
        /// 扫描物件
        /// </summary>
        private void ScanObjects()
        {
            if (string.IsNullOrEmpty(sourceFolderPath))
            {
                SetStatus("请先选择源文件夹", MessageType.Warning);
                return;
            }

            scannedObjects = MiniGameMigratorScanner.ScanFolder(sourceFolderPath);
            hasScanned = true;
            selectedObjects.Clear();

            int totalCount = scannedObjects.Count;
            int validCount = CountCanMigrate(scannedObjects);

            var categoryGroups = GetCategoryGroups(scannedObjects);

            string message = $"扫描完成: 找到 {totalCount} 个物件，其中 {validCount} 个可迁移";
            foreach (var group in categoryGroups)
            {
                int catTotal = group.Value.Count;
                int catValid = CountCanMigrate(group.Value);
                message += $"\n  [{group.Key}] 总计: {catTotal}，可迁移: {catValid}";
            }

            SetStatus(message, validCount > 0 ? MessageType.Info : MessageType.Warning);
        }

        /// <summary>
        /// 选择要迁移的物件进行迁移
        /// </summary>
        private void MigrateSelected()
        {
            if (selectedObjects.Count == 0)
            {
                SetStatus("请先选择要迁移的物件", MessageType.Warning);
                return;
            }

            List<AvatarObjectInfo> toMigrate = GetSelectedCanMigrate(scannedObjects);

            if (toMigrate.Count == 0)
            {
                SetStatus("所选物件中没有可迁移的", MessageType.Warning);
                return;
            }

            BatchMigrationResult result = MiniGameMigratorProcessor.MigrateBatch(toMigrate, options);
            SetStatus(result.message, result.success ? MessageType.Info : MessageType.Warning);

            ShowMigrationCompletionDialog(result);
        }

        /// <summary>
        /// 迁移所有可迁移的物件
        /// </summary>
        private void MigrateAllValid()
        {
            List<AvatarObjectInfo> toMigrate = GetAllCanMigrate(scannedObjects);

            if (toMigrate.Count == 0)
            {
                SetStatus("没有可迁移的物件", MessageType.Warning);
                return;
            }

            BatchMigrationResult result = MiniGameMigratorProcessor.MigrateBatch(toMigrate, options);
            SetStatus(result.message, result.success ? MessageType.Info : MessageType.Warning);

            ShowMigrationCompletionDialog(result);
        }

        /// <summary>
        /// 跳转到指定资产
        /// </summary>
        /// <param name="assetPath"></param>
        private void PingAsset(string assetPath)
        {
            var asset = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
            if (asset != null)
            {
                EditorGUIUtility.PingObject(asset);
                Selection.activeObject = asset;
            }
        }

        /// <summary>
        /// 设置状态信息
        /// </summary>
        /// <param name="message"></param>
        /// <param name="type"></param>
        private void SetStatus(string message, MessageType type)
        {
            statusMessage = message;
            statusType = type;
            Repaint();
        }

        /// <summary>
        /// 迁移完成后的提示框
        /// </summary>
        /// <param name="result"></param>
        private void ShowMigrationCompletionDialog(BatchMigrationResult result)
        {
            string title = result.success ? "迁移完成" : "迁移完成(部分失败)";
            string message = $"迁移操作已完成！\n\n" +
                             $"总计: {result.totalCount} 个物件\n" +
                             $"成功: {result.successCount} 个\n" +
                             $"失败: {result.failedCount} 个\n" +
                             $"跳过: {result.skippedCount} 个\n\n" +
                             $"LOD级别: lod{(int)options.lodLevel}\n" +
                             $"Mesh类型: {(options.meshType == MeshType.Original ? "原始" : "LOD")}\n" +
                             $"创建Prefab: {(options.createPrefab ? "是" : "否")}";

            if (result.failedCount > 0)
            {
                message += "\n\n查看Console窗口了解失败详情。";
            }

            EditorUtility.DisplayDialog(title, message, "确定");
        }

        #region Helper Methods (替代Linq)

        /// <summary>
        /// 是否有可迁移的物件
        /// </summary>
        /// <returns></returns>
        private bool HasAnyCanMigrate()
        {
            foreach (var obj in scannedObjects)
            {
                if (obj.CanMigrate)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 计算可迁移的物件数量
        /// </summary>
        /// <param name="list"></param>
        /// <returns></returns>
        private int CountCanMigrate(List<AvatarObjectInfo> list)
        {
            int count = 0;
            foreach (var obj in list)
            {
                if (obj.CanMigrate)
                    count++;
            }

            return count;
        }

        /// <summary>
        /// 计算选中的物件数量
        /// </summary>
        /// <param name="list"></param>
        /// <returns></returns>
        private int CountSelected(List<AvatarObjectInfo> list)
        {
            int count = 0;
            foreach (var obj in list)
            {
                if (selectedObjects.Contains(obj.sourcePath))
                    count++;
            }

            return count;
        }

        /// <summary>
        /// 获取分类组
        /// </summary>
        /// <param name="list"></param>
        /// <returns></returns>
        private SortedDictionary<string, List<AvatarObjectInfo>> GetCategoryGroups(List<AvatarObjectInfo> list)
        {
            var groups = new Dictionary<string, List<AvatarObjectInfo>>();
            foreach (var obj in list)
            {
                string key = obj.category ?? "未分类";
                if (!groups.ContainsKey(key))
                {
                    groups[key] = new List<AvatarObjectInfo>();
                }

                groups[key].Add(obj);
            }

            return new SortedDictionary<string, List<AvatarObjectInfo>>(groups);
        }

        /// <summary>
        /// 获取可迁移的物件
        /// </summary>
        /// <param name="list"></param>
        /// <returns></returns>
        private List<AvatarObjectInfo> GetSelectedCanMigrate(List<AvatarObjectInfo> list)
        {
            var result = new List<AvatarObjectInfo>();
            foreach (var obj in list)
            {
                if (selectedObjects.Contains(obj.sourcePath) && obj.CanMigrate)
                {
                    result.Add(obj);
                }
            }

            return result;
        }

        /// <summary>
        /// 获取所有可迁移的物件
        /// </summary>
        /// <param name="list"></param>
        /// <returns></returns>
        private List<AvatarObjectInfo> GetAllCanMigrate(List<AvatarObjectInfo> list)
        {
            var result = new List<AvatarObjectInfo>();
            foreach (var obj in list)
            {
                if (obj.CanMigrate)
                {
                    result.Add(obj);
                }
            }

            return result;
        }

        #endregion
    }
}
