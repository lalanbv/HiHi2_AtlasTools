# 多引用纹理复制工具

## 功能概述

该工具用于扫描材质文件夹中的纹理引用，找出被多次引用的纹理，并将其复制到指定的目标文件夹。

## 使用场景

当项目中存在多个材质球引用同一张纹理时，可能需要将这些共享纹理统一管理到一个公共文件夹中。

## 功能特性

### 扫描功能
- 扫描指定材质文件夹中所有材质球的纹理引用
- 统计每张纹理被引用的次数
- 筛选出引用次数大于1的纹理

### 复制功能
- 将多引用纹理复制到目标文件夹
- 智能处理命名冲突：
  - **MD5相同**：跳过复制（避免重复）
  - **MD5不同**：使用 `名称_0`、`名称_1`... 格式重命名

## 使用方法

### 方式一：右键菜单
1. 在 Project 窗口选中材质文件夹
2. 右键 → `Lod图及相关工具` → `多引用纹理复制`

### 方式二：顶部菜单
`Tools` → `Lod图及相关工具` → `图集生成替换` → `多引用纹理复制`

## 操作流程

1. **设置材质文件夹**：拖拽或点击选择包含材质球的文件夹
2. **设置目标文件夹**：拖拽或点击选择存放复制纹理的目标文件夹
3. **扫描**：点击"扫描多引用纹理"按钮
4. **查看结果**：在列表中查看多引用纹理详情
5. **执行复制**：点击"执行复制"按钮

## API 接口

### MultiRefTextureCopyProcessor

#### ScanMultiRefTextures

```csharp
public static TextureCopyScanResult ScanMultiRefTextures(string materialFolderPath)
```

扫描材质文件夹，返回多引用纹理列表。

**参数**：
- `materialFolderPath`: 材质文件夹路径（相对于 Assets）

**返回值**：`TextureCopyScanResult` 扫描结果对象

#### CopyMultiRefTextures

```csharp
public static TextureCopyResult CopyMultiRefTextures(List<TextureCopyInfo> texturesToCopy, string targetFolderPath)
```

执行纹理复制操作。

**参数**：
- `texturesToCopy`: 需要复制的纹理列表
- `targetFolderPath`: 目标文件夹路径

**返回值**：`TextureCopyResult` 复制结果对象

#### ComputeFileMD5

```csharp
public static string ComputeFileMD5(string filePath)
```

计算文件的 MD5 值。

**参数**：
- `filePath`: 文件绝对路径

**返回值**：MD5 字符串（小写）

## 数据结构

### TextureCopyInfo
| 字段 | 类型 | 说明 |
|------|------|------|
| sourceTexture | Texture2D | 源纹理对象 |
| sourcePath | string | 源纹理路径 |
| sourceMD5 | string | 源文件MD5 |
| referenceCount | int | 引用次数 |
| targetPath | string | 目标路径 |
| status | TextureCopyStatus | 复制状态 |

### TextureCopyStatus
| 枚举值 | 说明 |
|--------|------|
| Pending | 待处理 |
| Copied | 已复制 |
| Skipped | 已跳过(MD5相同) |
| Renamed | 已重命名复制 |
| Failed | 失败 |