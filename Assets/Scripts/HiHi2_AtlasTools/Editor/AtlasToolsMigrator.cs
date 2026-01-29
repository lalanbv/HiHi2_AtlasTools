// using UnityEngine;
// using UnityEditor;
// using System.IO;
// using System.Collections.Generic;
//
// public class AtlasToolsMigrator : EditorWindow
// {
//     private const string OLD_ROOT = "Assets/Scripts/HiHi2_Atlas_Texture_Material";
//     private const string NEW_ROOT = "Assets/Scripts/HiHi2_AtlasTools";
//
//     [MenuItem("Tools/图集工具迁移/执行目录结构迁移")]
//     public static void ShowWindow()
//     {
//         GetWindow<AtlasToolsMigrator>("目录迁移工具");
//     }
//
//     private void OnGUI()
//     {
//         EditorGUILayout.HelpBox("此工具将重构 HiHi2_Atlas_Texture_Material 目录结构", MessageType.Info);
//         
//         EditorGUILayout.Space(10);
//         
//         if (GUILayout.Button("1. 预览迁移计划", GUILayout.Height(30)))
//         {
//             PreviewMigration();
//         }
//         
//         EditorGUILayout.Space(5);
//         
//         GUI.backgroundColor = new Color(0.8f, 1f, 0.8f);
//         if (GUILayout.Button("2. 执行迁移", GUILayout.Height(40)))
//         {
//             if (EditorUtility.DisplayDialog("确认迁移", 
//                 "此操作将:\n1. 创建新目录结构\n2. 移动所有文件\n3. 删除旧目录\n\n建议先备份！是否继续？", 
//                 "执行", "取消"))
//             {
//                 ExecuteMigration();
//             }
//         }
//         GUI.backgroundColor = Color.white;
//     }
//
//     private static void PreviewMigration()
//     {
//         var migrations = GetMigrationPlan();
//         Debug.Log("===== 迁移计划预览 =====");
//         foreach (var m in migrations)
//         {
//             Debug.Log($"[MOVE] {m.Key}\n    -> {m.Value}");
//         }
//         Debug.Log($"===== 共 {migrations.Count} 个文件 =====");
//     }
//
//     private static void ExecuteMigration()
//     {
//         try
//         {
//             EditorUtility.DisplayProgressBar("迁移中", "创建目录结构...", 0.1f);
//             CreateDirectoryStructure();
//
//             EditorUtility.DisplayProgressBar("迁移中", "移动文件...", 0.3f);
//             MoveFiles();
//
//             EditorUtility.DisplayProgressBar("迁移中", "清理旧目录...", 0.8f);
//             CleanupOldDirectories();
//
//             EditorUtility.DisplayProgressBar("迁移中", "刷新资源...", 0.9f);
//             AssetDatabase.Refresh();
//
//             EditorUtility.ClearProgressBar();
//             EditorUtility.DisplayDialog("完成", "目录迁移完成！\n\n请手动检查并更新代码中的命名空间引用。", "确定");
//         }
//         catch (System.Exception e)
//         {
//             EditorUtility.ClearProgressBar();
//             EditorUtility.DisplayDialog("错误", $"迁移失败: {e.Message}", "确定");
//             Debug.LogError(e);
//         }
//     }
//
//     private static void CreateDirectoryStructure()
//     {
//         string[] folders = new[]
//         {
//             NEW_ROOT,
//             $"{NEW_ROOT}/Documentation",
//             $"{NEW_ROOT}/Runtime",
//             $"{NEW_ROOT}/Runtime/Data",
//             $"{NEW_ROOT}/Runtime/Utilities",
//             $"{NEW_ROOT}/Editor",
//             $"{NEW_ROOT}/Editor/Core",
//             $"{NEW_ROOT}/Editor/Generator",
//             $"{NEW_ROOT}/Editor/Replacer",
//             $"{NEW_ROOT}/Editor/OneClick"
//         };
//
//         foreach (var folder in folders)
//         {
//             if (!AssetDatabase.IsValidFolder(folder))
//             {
//                 string parent = Path.GetDirectoryName(folder).Replace("\\", "/");
//                 string name = Path.GetFileName(folder);
//                 AssetDatabase.CreateFolder(parent, name);
//                 Debug.Log($"[CREATE] {folder}");
//             }
//         }
//     }
//
//     private static Dictionary<string, string> GetMigrationPlan()
//     {
//         string oldCreate = $"{OLD_ROOT}/HiHi2_Create_Avatar_Atlas";
//         string oldMaterial = $"{OLD_ROOT}/HiHi2_Materials_Change_Texture";
//         string oldOneClick = $"{OLD_ROOT}/HiHi2_OneClick_Atlas_Change_Texture";
//
//         return new Dictionary<string, string>
//         {
//             // ===== Runtime/Data =====
//             { $"{oldCreate}/AtlasConfig.cs", $"{NEW_ROOT}/Runtime/Data/AtlasConfig.cs" },
//             { $"{oldCreate}/TextureInfo.cs", $"{NEW_ROOT}/Runtime/Data/TextureInfo.cs" },
//
//             // ===== Runtime/Utilities =====
//             { $"{oldCreate}/AtlasConstants.cs", $"{NEW_ROOT}/Runtime/Utilities/AtlasConstants.cs" },
//             { $"{oldCreate}/AtlasLogger.cs", $"{NEW_ROOT}/Runtime/Utilities/AtlasLogger.cs" },
//
//             // ===== Editor/Core =====
//             { $"{oldCreate}/AtlasCommon.cs", $"{NEW_ROOT}/Editor/Core/AtlasCommon.cs" },
//             { $"{oldCreate}/AtlasPathUtility.cs", $"{NEW_ROOT}/Editor/Core/AtlasPathUtility.cs" },
//             { $"{oldCreate}/AtlasTextureUtility.cs", $"{NEW_ROOT}/Editor/Core/AtlasTextureUtility.cs" },
//             { $"{oldCreate}/MaxRectsAtlasPacker.cs", $"{NEW_ROOT}/Editor/Core/MaxRectsAtlasPacker.cs" },
//
//             // ===== Editor/Generator =====
//             { $"{oldCreate}/AtlasGeneratorSettings.cs", $"{NEW_ROOT}/Editor/Generator/AtlasGeneratorSettings.cs" },
//             { $"{oldCreate}/Editor/AtlasGeneratorWindow.cs", $"{NEW_ROOT}/Editor/Generator/AtlasGeneratorWindow.cs" },
//             { $"{oldCreate}/Editor/AtlasConfigEditor.cs", $"{NEW_ROOT}/Editor/Generator/AtlasConfigEditor.cs" },
//             { $"{oldCreate}/Editor/GenerateOptimizedAtlasEditor.cs", $"{NEW_ROOT}/Editor/Generator/GenerateOptimizedAtlasEditor.cs" },
//
//             // ===== Editor/Replacer =====
//             { $"{oldMaterial}/Editor/MaterialAtlasReplacer.cs", $"{NEW_ROOT}/Editor/Replacer/MaterialAtlasReplacer.cs" },
//             { $"{oldMaterial}/Editor/MaterialAtlasReplacerWindow.cs", $"{NEW_ROOT}/Editor/Replacer/MaterialAtlasReplacerWindow.cs" },
//             { $"{oldMaterial}/MaterialCopyUtility.cs", $"{NEW_ROOT}/Editor/Replacer/MaterialCopyUtility.cs" },
//
//             // ===== Editor/OneClick =====
//             { $"{oldMaterial}/Editor/OneClickAtlasProcessor.cs", $"{NEW_ROOT}/Editor/OneClick/OneClickAtlasProcessor.cs" },
//             { $"{oldOneClick}/Editor/OneClickAtlasReplacerWindow.cs", $"{NEW_ROOT}/Editor/OneClick/OneClickAtlasReplacerWindow.cs" },
//
//             // ===== Documentation =====
//             { $"{oldCreate}/HiHi2 图集生成模块 (Create_Avatar_Atlas).md", $"{NEW_ROOT}/Documentation/AtlasGenerator.md" },
//             { $"{oldMaterial}/HiHi2 材质图集替换模块 (Materials_Change_Texture).md", $"{NEW_ROOT}/Documentation/MaterialReplacer.md" },
//             { $"{oldOneClick}/HiHi2 一键图集生成与材质替换模块 (OneClick_Atlas_Change_Texture).md", $"{NEW_ROOT}/Documentation/OneClickProcessor.md" },
//         };
//     }
//
//     private static void MoveFiles()
//     {
//         var migrations = GetMigrationPlan();
//         int count = 0;
//         
//         foreach (var kvp in migrations)
//         {
//             string src = kvp.Key;
//             string dst = kvp.Value;
//
//             if (!File.Exists(src.Replace("Assets", Application.dataPath)))
//             {
//                 Debug.LogWarning($"[SKIP] 源文件不存在: {src}");
//                 continue;
//             }
//
//             string result = AssetDatabase.MoveAsset(src, dst);
//             if (string.IsNullOrEmpty(result))
//             {
//                 Debug.Log($"[MOVED] {Path.GetFileName(src)} -> {dst}");
//                 count++;
//             }
//             else
//             {
//                 Debug.LogError($"[FAILED] {src}: {result}");
//             }
//         }
//
//         Debug.Log($"成功移动 {count} 个文件");
//     }
//
//     private static void CleanupOldDirectories()
//     {
//         string[] oldDirs = new[]
//         {
//             $"{OLD_ROOT}/HiHi2_Create_Avatar_Atlas/Editor",
//             $"{OLD_ROOT}/HiHi2_Create_Avatar_Atlas",
//             $"{OLD_ROOT}/HiHi2_Materials_Change_Texture/Editor",
//             $"{OLD_ROOT}/HiHi2_Materials_Change_Texture",
//             $"{OLD_ROOT}/HiHi2_OneClick_Atlas_Change_Texture/Editor",
//             $"{OLD_ROOT}/HiHi2_OneClick_Atlas_Change_Texture",
//             OLD_ROOT
//         };
//
//         foreach (var dir in oldDirs)
//         {
//             if (AssetDatabase.IsValidFolder(dir))
//             {
//                 // 检查是否为空或只有meta文件
//                 string fullPath = dir.Replace("Assets", Application.dataPath);
//                 if (Directory.Exists(fullPath))
//                 {
//                     var files = Directory.GetFiles(fullPath, "*", SearchOption.AllDirectories);
//                     var nonMeta = System.Array.FindAll(files, f => !f.EndsWith(".meta"));
//                     
//                     if (nonMeta.Length == 0)
//                     {
//                         AssetDatabase.DeleteAsset(dir);
//                         Debug.Log($"[DELETE] 空目录: {dir}");
//                     }
//                 }
//             }
//         }
//     }
// }