# 小程序资源迁移工具（MiniGame Migrator）

## 概述

小程序资源迁移工具是 HiHi2 Atlas Tools 的重要组成部分，专门用于将主项目的 Avatar 资源迁移到小程序项目目录。工具支持 LOD 级别选择、图集模式切换、材质纹理替换等高级功能，能够智能识别 Avatar 物件结构，自动完成资源筛选、复制和优化。

## 功能特性

### 核心功能
- **智能物件扫描**：自动识别 Avatar 物件文件夹结构，检测 LOD 文件夹、Mesh 文件夹等必需资源
- **LOD 级别选择**：支持 Lod1/Lod2/Lod3 三级纹理优化，灵活控制资源质量与大小
- **Mesh 类型切换**：可选择使用原始 Mesh 或 LOD Mesh（带 `_70` 后缀）
- **图集模式切换**：支持原始图集和 LOD 图集两种纹理模式
- **材质纹理替换**：自动复制材质并替换其中的纹理引用
- **Prefab 自动创建**：可选生成优化后的 Prefab，自动替换 Mesh 和材质引用
- **批量迁移支持**：支持单个或批量物件迁移，提供详细的进度反馈
- **智能过滤**：自动跳过已打包进图集的散图，避免资源冗余

### 路径映射
- **源目录**：`Assets/Art/Avatar/BasicAvatar/[分类]/[物件]`
- **目标目录**：`Assets/Art_MiniGame/Avatar/BasicAvatar/[分类]/[物件]`

## 使用方法

### 方式一：快捷菜单（推荐）
1. 在 Project 窗口中选中包含 Avatar 物件的文件夹
2. 右键 → `Lod图及相关工具` → `小程序资源迁移` → `迁移选中文件夹`
3. 窗口自动打开并扫描选中文件夹下的所有物件

### 方式二：工具菜单
1. 顶部菜单栏 → `Tools` → `Lod图及相关工具` → `小程序资源迁移` → `迁移工具窗口`
2. 在窗口中手动选择源文件夹或拖拽文件夹
3. 点击「扫描」按钮开始扫描

### 操作步骤

#### 基础流程
1. **选择源文件夹**：
   - 通过右键菜单自动填充，或
   - 在窗口中拖拽/选择文件夹
2. **配置迁移选项**：
   - 选择图集模式（原始/LOD）
   - 选择 LOD 级别（Lod1/Lod2/Lod3）
   - 选择 Mesh 类型（原始/LOD）
   - 设置是否创建 Prefab
   - 设置是否覆盖已存在文件
3. **扫描物件**：点击「扫描」按钮，工具会自动识别所有 Avatar 物件
4. **选择要迁移的物件**：
   - 勾选列表中的物件（仅显示可迁移的物件）
   - 或使用「全选」按钮选择所有可迁移物件
5. **执行迁移**：
   - 点击「迁移选中」迁移已选择的物件
   - 或点击「迁移全部有效物件」迁移所有可迁移物件
6. **查看结果**：迁移完成后会显示统计对话框

#### 扫描结果说明
扫描结果列表显示以下信息：
- **物件名称**：点击可定位到源文件夹
- **分类**：物件所属类别（Head/Jacket/Face/Bone 等）
- **LOD**：是否包含 LOD 文件夹
- **Mesh**：是否包含 Mesh 文件夹
- **Prefab**：是否包含 Prefab 文件
- **状态**：可迁移/不可迁移
- **说明**：不可迁移的原因

## 界面说明

### 源文件夹选择区域
- **文件夹对象字段**：支持拖拽 DefaultAsset
- **从选择获取按钮**：使用 Project 窗口当前选中的文件夹
- **扫描按钮**：开始扫描源文件夹
- **路径显示**：显示当前选中的文件夹路径

### 迁移选项区域

#### 图集模式
- **Original（原始）**：使用原始图集纹理（无 LOD 后缀）
- **Lod（LOD）**：使用带 `_lodX` 后缀的图集纹理

#### LOD 级别（当图集模式为 Lod 时显示）
- **Lod1**：使用 `_lod1` 后缀纹理
- **Lod2**：使用 `_lod2` 后缀纹理
- **Lod3**：使用 `_lod3` 后缀纹理

#### Mesh 类型
- **Original**：使用原始 Mesh 文件
- **Lod**：使用带 `_70` 后缀的 LOD Mesh 文件

#### 其他选项
- **创建 Prefab**：在目标目录创建优化后的 Prefab
- **覆盖已存在**：覆盖目标目录中已存在的文件

### 物件列表区域
- **统计信息**：显示总计/可迁移/已选择的物件数量
- **分类统计**：按类别显示物件数量
- **全选复选框**：快速选择/取消选择所有可迁移物件
- **物件行**：
  - 绿色背景：可迁移
  - 红色背景：不可迁移
  - 蓝色背景：已选中

## 技术架构

### MiniGameMigratorAPI
提供对外接口的静态类，封装了扫描和迁移的核心功能。
```csharp
public static class MiniGameMigratorAPI { // 扫描文件夹，返回所有 Avatar 物件信息 public static List<AvatarObjectInfo> ScanFolder(string folderPath)
// 迁移单个物件
public static MigrationResult MigrateObject(AvatarObjectInfo objectInfo, MigrationOptions options)

// 批量迁移物件
public static BatchMigrationResult MigrateBatch(List<AvatarObjectInfo> objectInfos, MigrationOptions options)

// 迁移整个文件夹
public static BatchMigrationResult MigrateFolder(string folderPath, MigrationOptions options)

// 获取目标路径
public static string GetTargetPath(AvatarObjectInfo objectInfo)
}
```

### MiniGameMigratorScanner
负责扫描和发现 Avatar 物件，分析文件夹结构，收集资源信息。

**核心功能**：
- 递归扫描文件夹，识别物件文件夹
- 检测 LOD 文件夹、Mesh 文件夹、Prefab 等必需资源
- 收集材质、纹理、Mesh 等资源路径
- 验证物件是否满足迁移条件

**关键常量**：

```csharp
public const string SOURCE_BASIC_AVATAR_PATH = "Assets/Art/Avatar/BasicAvatar";
public const string TARGET_BASIC_AVATAR_PATH = "Assets/Art_MiniGame/Avatar/BasicAvatar";
public const string LOD_FOLDER_NAME = "LOD";
public const string MESH_FOLDER_NAME = "Mesh";
public const string LOD_MESH_SUFFIX = "_70";
```

### MiniGameMigratorProcessor
执行实际的迁移处理逻辑，包括纹理复制、材质处理、Mesh 复制和 Prefab 创建。

**处理流程**：
1. **准备目标文件夹**：创建或清空目标目录
2. **复制纹理**：根据 LOD 级别和图集模式筛选并复制纹理
3. **复制材质**：复制材质并替换其中的纹理引用
4. **复制 Mesh**：根据 Mesh 类型选择并复制 Mesh
5. **创建 Prefab**（可选）：创建优化后的 Prefab

## 配置选项说明

### LOD 级别（LodLevel）
| 级别 | 后缀 | 说明 |
|------|------|------|
| **Lod1** | `_lod1` | 最高质量，纹理尺寸最大 |
| **Lod2** | `_lod2` | 中等质量，平衡性能与画质 |
| **Lod3** | `_lod3` | 最低质量，最小资源占用 |

### Mesh 类型（MeshType）
| 类型 | 说明 | 适用场景 |
|------|------|----------|
| **Original** | 使用原始 Mesh 文件 | 需要完整细节的场景 |
| **Lod** | 使用带 `_70` 后缀的 LOD Mesh | 性能优先的场景 |

### 图集纹理模式（AtlasTextureMode）
| 模式 | 说明 | 纹理筛选规则 |
|------|------|--------------|
| **Original** | 使用原始图集纹理 | 复制所有非 LOD 纹理 |
| **Lod** | 使用 LOD 图集纹理 | 仅复制匹配当前 LOD 级别的纹理 |

### 其他选项
| 选项 | 说明 | 默认值 |
|------|------|--------|
| **createPrefab** | 是否在目标目录创建优化后的 Prefab | true |
| **overwriteExisting** | 是否覆盖目标目录中已存在的文件 | false |

## 数据结构定义

### AvatarObjectInfo
存储 Avatar 物件的完整信息。

```csharp
public class AvatarObjectInfo { public string objectName; // 物件名称 public string sourcePath; // 源文件夹路径 public string category; // 分类（Head/Jacket/Face/Bone 等）
public bool hasLodFolder;           // 是否有 LOD 文件夹
public bool hasMeshFolder;          // 是否有 Mesh 文件夹
public bool hasPrefab;              // 是否有 Prefab
public bool hasOriginalMesh;        // 是否有原始 Mesh
public bool hasLodMesh;             // 是否有 LOD Mesh

public string prefabPath;           // Prefab 路径
public string originalMeshPath;     // 原始 Mesh 路径
public string lodMeshPath;          // LOD Mesh 路径

public string lodAtlasMaterialPath; // LOD Atlas 材质路径
public string lodAtlasTexturePath;  // LOD Atlas 纹理路径

public List<string> atlasMaterials; // Atlas 材质列表
public List<string> atlasTextures;  // Atlas 纹理列表
public List<string> lodTextures;    // LOD 纹理列表

public bool isValid;                // 是否有效
public string invalidReason;        // 无效原因

public bool CanMigrate => isValid && hasLodFolder && hasMeshFolder && (hasOriginalMesh || hasLodMesh);
}
```

### MigrationResult
单个物件的迁移结果。

```csharp
public class MigrationResult {
    public bool success; // 是否成功
    public string message; // 结果消息
    public string objectName; // 物件名称
    public int copiedMaterialCount; // 复制的材质数量
    public int copiedTextureCount; // 复制的纹理数量
    public int replacedTextureCount; // 替换的纹理数量
    public bool prefabCreated; // 是否创建了
    Prefab public string outputPrefabPath; // 输出的 Prefab 路径
    public string outputMeshPath; // 输出的 Mesh 路径
}
```

### BatchMigrationResult
批量迁移的结果汇总。

```csharp
csharp public class BatchMigrationResult {
    public bool success; // 整体是否成功
    public string message; // 结果消息
    public int totalCount; // 总计物件数
    public int successCount; // 成功数
    public int failedCount; // 失败数
    public int skippedCount; // 跳过数
    public List<MigrationResult> results; // 详细结果列表
}
```

### MigrationOptions
迁移配置选项。

```csharp
public class MigrationOptions {
    public LodLevel lodLevel = LodLevel.Lod1; // LOD 级别
    public MeshType meshType = MeshType.Original; // Mesh 类型
    public AtlasTextureMode atlasTextureMode = AtlasTextureMode.Lod; // 图集模式
    public bool createPrefab = true; // 是否创建
    Prefab public bool overwriteExisting = false; // 是否覆盖已存在
}
```

## 工作流程

### 扫描流程
1. **递归遍历**：从源文件夹开始递归遍历所有子文件夹
2. **识别物件文件夹**：检测文件夹是否包含 LOD 或 Mesh 等结构性子文件夹
3. **收集资源信息**：
   - 扫描 LOD/AtlasMaterial 文件夹收集材质
   - 扫描 LOD/AtlasTexture 文件夹收集纹理
   - 扫描 Mesh 文件夹收集 Mesh
   - 检测根目录下的 Prefab
4. **验证有效性**：检查是否满足迁移条件（有 LOD 文件夹、有 Mesh 文件夹、有可用 Mesh）

### 迁移流程
1. **准备目标文件夹**：
   - 根据源路径计算目标路径
   - 创建目标文件夹（如果不存在）
   - 如开启覆盖选项，清空已存在的目标文件夹
2. **复制纹理**：
   - 根据图集模式和 LOD 级别筛选纹理
   - 跳过已打包进图集的散图
   - 复制符合条件的纹理到目标目录
   - 记录纹理名称到目标路径的映射
3. **复制材质**：
   - 复制源材质到目标目录
   - 替换材质中的纹理引用为新的目标纹理
   - 设置正确的纹理导入属性
4. **复制 Mesh**：
   - 根据 Mesh 类型选择源 Mesh
   - 复制 Mesh 到目标目录，文件名添加 `_mini` 后缀
5. **创建 Prefab**（可选）：
   - 实例化源 Prefab
   - 替换 Mesh 引用为新的目标 Mesh
   - 替换材质引用为新的目标材质
   - 保存新的 Prefab 到目标目录

## 目录结构约定

### 源目录结构
Assets/Art/Avatar/BasicAvatar/ 
├── [Category]/ # 分类文件夹（Head/Jacket/Face/Bone 等） 
│ ├── [ObjectName]/ # 物件文件夹 
│ │ ├── LOD/ # LOD 文件夹（必需） 
│ │ │ ├── AtlasMaterial/ # Atlas 材质文件夹 
│ │ │ ├── AtlasTexture/ # Atlas 纹理文件夹 
│ │ │ └── Texture/ # LOD 纹理文件夹 
│ │ ├── Mesh/ # Mesh 文件夹（必需） 
│ │ │ ├── [Name].fbx # 原始 Mesh 
│ │ │ └── [Name]_70.fbx # LOD Mesh（可选） 
│ │ └── [Name].prefab # Prefab（可选）

### 目标目录结构

Assets/Art_MiniGame/Avatar/BasicAvatar/ 
├── [Category]/ # 分类文件夹 
│ ├── [ObjectName]/ # 物件文件夹 
│ │ ├── Material/ # 迁移后的材质 
│ │ ├── Texture/ # 迁移后的纹理 
│ │ ├── Mesh/ # 迁移后的 Mesh（带 _mini 后缀） 
│ │ └── [Name]_mini.prefab # 优化后的 Prefab（可选）

## 常见问题解答

### Q1: 为什么某些物件显示「不可迁移」？
**A:** 物件必须满足以下条件才能迁移：
- 包含 LOD 文件夹
- 包含 Mesh 文件夹
- Mesh 文件夹中至少有一个有效 Mesh（原始或 LOD）

### Q2: 迁移后纹理显示异常？
**A:** 
- 检查源纹理的导入设置是否正确
- 确认选择的 LOD 级别在源文件夹中存在对应的纹理
- 查看 Console 日志是否有错误信息

### Q3: 如何只迁移特定分类的物件？
**A:** 在 Project 窗口中选择特定分类的文件夹，然后右键启动迁移工具，扫描结果将只包含该分类下的物件。

### Q4: 可以重复迁移同一个物件吗？
**A:** 可以。开启「覆盖已存在」选项后，工具会清空目标文件夹并重新迁移。

### Q5: 为什么某些纹理没有被复制？
**A:** 工具会自动跳过已打包进图集的散图（通过 AtlasConfig 识别）。只有独立的纹理文件会被复制。

### Q6: Prefab 创建失败怎么办？
**A:** 
- 确认源物件包含 Prefab
- 检查 Mesh 和材质是否成功复制
- 查看 Console 日志了解详细错误

### Q7: 如何批量迁移多个分类？
**A:** 选择 BasicAvatar 或其父级文件夹作为源文件夹，工具会自动扫描所有子文件夹中的 Avatar 物件。

## 最佳实践建议

1. **迁移前检查**：
   - 确认源物件结构完整（LOD 文件夹、Mesh 文件夹）
   - 检查纹理命名是否符合规范（`_lod1`、`_lod2`、`_lod3`）

2. **LOD 级别选择**：
   - **Lod1**：适用于高质量要求的场景
   - **Lod2**：适用于平衡性能与画质的场景（推荐）
   - **Lod3**：适用于性能优先的场景

3. **Mesh 类型选择**：
   - 小程序场景建议使用 **Lod** 类型，减少顶点数

4. **分批迁移**：
   - 大量物件建议按分类分批迁移
   - 便于排查问题和验证结果

5. **版本控制**：
   - 迁移前提交原始资源
   - 便于必要时回滚

6. **命名规范**：
   - 保持源资源命名规范
   - 便于工具正确识别和匹配

7. **日志检查**：
   - 迁移完成后检查 Console 日志
   - 关注警告和错误信息

## 相关工具

- **图集生成器**：生成 LOD 图集和配置文件
- **材质图集替换器**：替换材质中的散图为图集
- **一键图集处理器**：一键完成图集生成和材质替换

---

**开发者**: lalanbv
**最后更新**: 2026-02