# AtlasToolsAPI 使用文档
## 目录
+ [快速入门](#快速入门)
+ [API 概览](#api-概览)
+ [图集生成 API](#图集生成-api)
+ [材质替换 API](#材质替换-api)
+ [完整处理 API](#完整处理-api)
+ [LOD MaxSize 同步 API](#lod-maxsize-同步-api)
+ [数据结构参考](#数据结构参考)
+ [注意事项与最佳实践](#注意事项与最佳实践)

---

## 快速入门
### 引入命名空间
```csharp
using HiHi2.AtlasTools.Editor;
```

### 最常用场景
**场景一：一键完成图集生成 + 材质替换**

```csharp
var result = AtlasToolsAPI.ProcessAll( sourceTexturePath: "Assets/MyProject/Texture", sourceMaterialPath: "Assets/MyProject/Materials", outputAtlasPath: "Assets/MyProject/LOD/Texture", outputMaterialPath: "Assets/MyProject/LOD/Materials" );
if (result.success) {
    Debug.Log($"处理成功：生成了 {result.atlasResult.atlasCount} 张图集");
} else {
    Debug.LogError($"处理失败：{result.message}");
}
```

**场景二：一键同步 LOD 纹理的 MaxSize（推荐）**

```csharp
var result = AtlasToolsAPI.ScanAndSyncMaxSize(lodTexturePath: "Assets/MyProject/LOD/Texture", sourceTexturePath: "Assets/MyProject/Texture");
if (result.success) {
    Debug.Log($"同步完成：{result.syncResult.syncedCount} 个纹理已更新");
}
```

---

## API 概览
| 分类 | 方法 | 说明 | 推荐度 |
| --- | --- | --- | --- |
| **图集生成** | `GenerateAtlas` | 从源纹理文件夹生成图集和配置文件 | ⭐⭐⭐ |
| **材质替换** | `ReplaceMaterialTextures` | 复制材质并替换为图集纹理 | ⭐⭐⭐ |
| **完整处理** | `ProcessAll` | 一键执行图集生成 + 材质替换 | ⭐⭐⭐⭐⭐ |
| **LOD MaxSize** | `ScanAndSyncMaxSize` | 一键扫描 + 同步 LOD MaxSize | ⭐⭐⭐⭐⭐ |
| **LOD MaxSize** | `ScanProjectMaxSize` | 扫描项目 MaxSize（自动模式） | ⭐⭐⭐ |
| **LOD MaxSize** | `ScanMaxSizeCustom` | 扫描 MaxSize（自定义模式） | ⭐⭐⭐ |
| **LOD MaxSize** | `SyncAllMaxSize` | 同步所有需要修改的 MaxSize | ⭐⭐⭐ |
| **LOD MaxSize** | `ProcessMaxSizeSync` | 扫描 + 同步（自动模式） | ⭐⭐⭐⭐ |
| **LOD MaxSize** | `ProcessMaxSizeSyncCustom` | 扫描 + 同步（自定义模式） | ⭐⭐⭐⭐ |


---

## 图集生成 API
### GenerateAtlas
从源纹理文件夹生成图集和配置文件。

**方法签名**

```csharp
public static AtlasGenerationResult GenerateAtlas( string sourceTexturePath, string outputAtlasPath, AtlasGeneratorSettings settings = null )
```

**参数说明**

| 参数 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| `sourceTexturePath` | string | ✅ | 源纹理文件夹路径，必须以 `Assets/` 开头 |
| `outputAtlasPath` | string | ✅ | 图集输出路径，必须以 `Assets/` 开头 |
| `settings` | AtlasGeneratorSettings | ❌ | 图集生成设置，为 `null` 时使用默认设置 |


**返回值**

返回 `AtlasGenerationResult` 对象，详见 [AtlasGenerationResult](#atlasgenerationresult)。

**示例代码**

```csharp
// 使用默认设置生成图集
var result = AtlasToolsAPI.GenerateAtlas( "Assets/MyProject/Texture", "Assets/MyProject/LOD/Texture" );
if (result.success) {
    Debug.Log($"生成了 {result.atlasCount} 张图集，包含 {result.textureCount} 张纹理");
    Debug.Log($"输出路径：{result.outputPath}");
    // 查看生成的文件列表
    foreach (var file in result.atlasFiles)
    {
        Debug.Log($"  - {file}");
    }
} else {
    Debug.LogError($"图集生成失败：{result.message}");
}

// 使用自定义设置
var settings = ScriptableObject.CreateInstance<AtlasGeneratorSettings>(); settings.padding = 4; settings.maxAtlasSize = 2048; settings.maxWastagePercent = 20f;
var result = AtlasToolsAPI.GenerateAtlas( "Assets/MyProject/Texture", "Assets/MyProject/LOD/Texture", settings );
```

---

## 材质替换 API
### ReplaceMaterialTextures
复制源材质到输出目录，并将材质中的纹理替换为图集纹理。

**方法签名**

```csharp
public static MaterialReplacementResult ReplaceMaterialTextures( string sourceMaterialPath, string outputMaterialPath, string atlasConfigPath )
```

**参数说明**

| 参数 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| `sourceMaterialPath` | string | ✅ | 源材质文件夹路径，必须以 `Assets/` 开头 |
| `outputMaterialPath` | string | ✅ | 材质输出路径，必须以 `Assets/` 开头 |
| `atlasConfigPath` | string | ✅ | 图集配置文件夹路径（包含 `.asset` 配置文件） |


**返回值**

返回 `MaterialReplacementResult` 对象，详见 [MaterialReplacementResult](#materialreplacementresult)。

**前置条件**

> ⚠️ 调用此方法前，必须先通过 `GenerateAtlas` 生成图集配置文件。
>

**示例代码**

```csharp
// 先确保已有图集配置 var matResult = AtlasToolsAPI.ReplaceMaterialTextures( sourceMaterialPath: "Assets/MyProject/Materials", outputMaterialPath: "Assets/MyProject/LOD/Materials", atlasConfigPath: "Assets/MyProject/LOD/Texture" // 图集配置所在路径 );
if (matResult.success) {
    Debug.Log($"处理了 {matResult.materialCount} 个材质");
    Debug.Log($"替换了 {matResult.replacedTextureCount} 张纹理");
    // 查看处理的材质
    foreach (var matName in matResult.processedMaterials)
    {
        Debug.Log($"  - {matName}");
    }
} else {
    Debug.LogError($"材质替换失败：{matResult.message}");
}
```

---

## 完整处理 API
### ProcessAll
一键执行完整的图集生成和材质替换流程，推荐在需要同时处理两个步骤时使用。

**方法签名**

```csharp
public static ProcessAllResult ProcessAll( string sourceTexturePath, string sourceMaterialPath, string outputAtlasPath, string outputMaterialPath, AtlasGeneratorSettings settings = null )
```

**参数说明**

| 参数 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| `sourceTexturePath` | string | ✅ | 源纹理文件夹路径 |
| `sourceMaterialPath` | string | ✅ | 源材质文件夹路径 |
| `outputAtlasPath` | string | ✅ | 图集输出路径 |
| `outputMaterialPath` | string | ✅ | 材质输出路径 |
| `settings` | AtlasGeneratorSettings | ❌ | 图集生成设置 |


**返回值**

返回 `ProcessAllResult` 对象，详见 [ProcessAllResult](#processallresult)。

**示例代码**

```csharp
var result = AtlasToolsAPI.ProcessAll( sourceTexturePath: "Assets/MyProject/Texture", sourceMaterialPath: "Assets/MyProject/Materials", outputAtlasPath: "Assets/MyProject/LOD/Texture", outputMaterialPath: "Assets/MyProject/LOD/Materials" );
if (result.success) {
    Debug.Log("=== 处理完成 ===");
    Debug.Log($"图集：生成 {result.atlasResult.atlasCount} 张，包含 {result.atlasResult.textureCount} 张纹理");
    Debug.Log($"材质：处理 {result.materialResult.materialCount} 个，替换 {result.materialResult.replacedTextureCount} 张纹理");
} else {
    Debug.LogError($"处理失败：{result.message}");
    // 可以进一步判断是哪个步骤失败
    if (result.atlasResult != null && !result.atlasResult.success)
    {
        Debug.LogError($"  图集生成失败：{result.atlasResult.message}");
    }
    if (result.materialResult != null && !result.materialResult.success)
    {
        Debug.LogError($"  材质替换失败：{result.materialResult.message}");
    }
}
```

---

## LOD MaxSize 同步 API
### ScanAndSyncMaxSize（推荐）
一键扫描并同步 LOD 纹理的 MaxSize，直接传入两个路径即可完成全部操作。

**方法签名**

```csharp
csharp public static MaxSizeProcessResult ScanAndSyncMaxSize( string lodTexturePath, string sourceTexturePath )
```

**参数说明**

| 参数 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| `lodTexturePath` | string | ✅ | LOD 纹理文件夹路径，如 `Assets/MyProject/LOD/Texture` |
| `sourceTexturePath` | string | ✅ | 源纹理文件夹路径，如 `Assets/MyProject/Texture` |


**返回值**

返回 `MaxSizeProcessResult` 对象，详见 [MaxSizeProcessResult](#maxsizeprocessresult)。

**示例代码**

```csharp
var result = AtlasToolsAPI.ScanAndSyncMaxSize( lodTexturePath: "Assets/MyProject/LOD/Texture", sourceTexturePath: "Assets/MyProject/Texture" );
if (result.success) { Debug.Log($"扫描纹理数：{result.scanResult.totalCount}"); Debug.Log($"需要同步数：{result.scanResult.needsSyncCount}"); Debug.Log($"实际同步数：{result.syncResult.syncedCount}"); }
```

---

### ScanProjectMaxSize
扫描项目纹理的 MaxSize 匹配信息（自动模式），自动识别标准目录结构。

**方法签名**

```csharp
public static MaxSizeScanResult ScanProjectMaxSize(string projectPath)
```

**参数说明**

| 参数 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| `projectPath` | string | ✅ | 项目根文件夹路径，必须包含 `LOD/Texture` 和 `Texture` 子目录 |


**目录结构要求**  
[projectPath]/   
├── LOD/   
│ └── Texture/ ← LOD 纹理目录   
│ ├── cloth_body.png   
│ └── cloth_arm.png   
└── Texture/ ← 源 Lod0 纹理目录   
├── cloth_body_lod0.png   
└── cloth_arm_lod0.png

## **示例代码**
```csharp
var scanResult = AtlasToolsAPI.ScanProjectMaxSize("Assets/MyProject");
if (scanResult.success) {
    Debug.Log($"扫描到 {scanResult.totalCount} 个纹理");
    Debug.Log($"已匹配 {scanResult.matchedCount} 个");
    Debug.Log($"需要同步 {scanResult.needsSyncCount} 个");
    // 遍历查看详细信息
    foreach (var info in scanResult.matchInfoList)
    {
        if (info.needsSync)
        {
            Debug.Log($"{info.lodTextureName}: {info.lodTextureCurrentMaxSize} → {info.targetMaxSize}");
        }
    }
}
```

### ScanMaxSizeCustom
扫描纹理的 MaxSize 匹配信息（自定义模式），可指定任意两个纹理文件夹。

## **方法签名**
```csharp
public static MaxSizeScanResult ScanMaxSizeCustom( string lodTexturePath, string sourceTexturePath )
```

##   
**示例代码**  

```csharp
var scanResult = AtlasToolsAPI.ScanMaxSizeCustom( lodTexturePath: "Assets/CustomLOD/Textures", sourceTexturePath: "Assets/CustomSource/Textures" );
```

### SyncAllMaxSize
同步所有需要修改的纹理 MaxSize，需配合扫描方法使用。

**方法签名**

```csharp
public static MaxSizeSyncResult SyncAllMaxSize( List<TextureMaxSizeMatchInfo> matchInfoList )
```

**参数说明**

| 参数 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| `matchInfoList` | List<TextureMaxSizeMatchInfo> | ✅ | 从扫描方法获取的匹配信息列表 |


## **示例代码**  

```csharp
// 分步执行：先扫描，再同步
var scanResult = AtlasToolsAPI.ScanProjectMaxSize("Assets/MyProject");
if (scanResult.success && scanResult.needsSyncCount > 0) { 
    // 可以在这里添加用户确认逻辑
    var syncResult = AtlasToolsAPI.SyncAllMaxSize(scanResult.matchInfoList);
    Debug.Log($"同步完成：{syncResult.syncedCount} 个纹理已更新");
}
```

### ProcessMaxSizeSync / ProcessMaxSizeSyncCustom
一键执行 LOD MaxSize 扫描和同步。

**方法签名**

```csharp
// 自动模式 - 使用标准目录结构 public static MaxSizeProcessResult ProcessMaxSizeSync(string projectPath)
// 自定义模式 - 指定任意目录 public static MaxSizeProcessResult ProcessMaxSizeSyncCustom( string lodTexturePath, string sourceTexturePath )
```

**示例代码**

```csharp
// 自动模式 var result = AtlasToolsAPI.ProcessMaxSizeSync("Assets/MyProject");
// 自定义模式 var result = AtlasToolsAPI.ProcessMaxSizeSyncCustom( "Assets/CustomLOD/Textures", "Assets/CustomSource/Textures" );
```

---

## 数据结构参考
### AtlasGenerationResult
图集生成结果。

| 属性 | 类型 | 说明 |
| --- | --- | --- |
| `success` | bool | 操作是否成功 |
| `message` | string | 结果描述信息 |
| `atlasCount` | int | 生成的图集数量 |
| `textureCount` | int | 包含的纹理总数 |
| `outputPath` | string | 输出目录路径 |
| `atlasFiles` | List<string> | 生成的文件列表（.png 和 .asset） |


---

### MaterialReplacementResult
材质替换结果。

| 属性 | 类型 | 说明 |
| --- | --- | --- |
| `success` | bool | 操作是否成功 |
| `message` | string | 结果描述信息 |
| `materialCount` | int | 处理的材质数量 |
| `replacedTextureCount` | int | 替换的纹理数量 |
| `outputPath` | string | 输出目录路径 |
| `processedMaterials` | List<string> | 处理的材质名称列表 |


---

### ProcessAllResult
完整处理结果。

| 属性 | 类型 | 说明 |
| --- | --- | --- |
| `success` | bool | 操作是否成功 |
| `message` | string | 结果描述信息 |
| `atlasResult` | AtlasGenerationResult | 图集生成结果 |
| `materialResult` | MaterialReplacementResult | 材质替换结果 |


---

### MaxSizeScanResult
MaxSize 扫描结果。

| 属性 | 类型 | 说明 |
| --- | --- | --- |
| `success` | bool | 操作是否成功 |
| `message` | string | 结果描述信息 |
| `totalCount` | int | 扫描到的纹理总数 |
| `matchedCount` | int | 成功匹配源纹理的数量 |
| `needsSyncCount` | int | 需要同步 MaxSize 的数量 |
| `matchInfoList` | List<TextureMaxSizeMatchInfo> | 纹理匹配详情列表 |


---

### MaxSizeSyncResult
MaxSize 同步结果。

| 属性 | 类型 | 说明 |
| --- | --- | --- |
| `success` | bool | 操作是否成功 |
| `message` | string | 结果描述信息 |
| `syncedCount` | int | 实际同步的纹理数量 |


---

### MaxSizeProcessResult
MaxSize 完整处理结果。

| 属性 | 类型 | 说明 |
| --- | --- | --- |
| `success` | bool | 操作是否成功 |
| `message` | string | 结果描述信息 |
| `scanResult` | MaxSizeScanResult | 扫描结果 |
| `syncResult` | MaxSizeSyncResult | 同步结果 |


---

### TextureMaxSizeMatchInfo
纹理匹配详情。

| 属性 | 类型 | 说明 |
| --- | --- | --- |
| `lodTexturePath` | string | LOD 纹理完整路径 |
| `sourceTexturePath` | string | 匹配到的源纹理路径 |
| `lodTextureName` | string | LOD 纹理文件名 |
| `sourceTextureName` | string | 源纹理文件名 |
| `lodTextureCurrentMaxSize` | int | LOD 纹理当前 MaxSize |
| `sourceLod0MaxSize` | int | 源纹理的 MaxSize |
| `targetMaxSize` | int | 目标 MaxSize（将同步到此值） |
| `isMatched` | bool | 是否成功匹配到源纹理 |
| `needsSync` | bool | 是否需要同步（当前值与目标值不同） |
| `errorMessage` | string | 错误信息（未匹配时的原因） |


---

## 注意事项与最佳实践
### 路径格式要求
> ⚠️ **所有路径参数必须以 **`Assets/`** 开头**
>

```csharp
// ✅ 正确 AtlasToolsAPI.GenerateAtlas("Assets/MyProject/Texture", "Assets/MyProject/LOD/Texture");
// ❌ 错误 - 使用了绝对路径 AtlasToolsAPI.GenerateAtlas("D:/Project/Assets/MyProject/Texture", "...");
// ❌ 错误 - 没有以 Assets/ 开头 AtlasToolsAPI.GenerateAtlas("MyProject/Texture", "...");
```

### 纹理匹配规则
LOD MaxSize 同步功能使用以下规则匹配纹理：

| LOD 纹理名称 | 匹配的源纹理名称 | 说明 |
| --- | --- | --- |
| `cloth_body.png` | `cloth_body_lod0.png` | 自动添加 `_lod0` 后缀匹配 |
| `cloth_body.png` | `cloth_body.png` | 同名匹配 |
| `Cloth_Body.png` | `cloth_body_lod0.png` | 忽略大小写 |


### 错误处理模板
建议使用以下模板处理 API 调用结果：

```csharp
public void ProcessWithErrorHandling() {
    var result = AtlasToolsAPI.ProcessAll( "Assets/MyProject/Texture", "Assets/MyProject/Materials", "Assets/MyProject/LOD/Texture", "Assets/MyProject/LOD/Materials" );
    if (!result.success)
    {
        // 记录错误日志
        Debug.LogError($"[AtlasTools] 处理失败：{result.message}");

        // 可选：显示对话框
        EditorUtility.DisplayDialog("处理失败", result.message, "确定");
        return;
    }

    // 成功处理
    Debug.Log($"[AtlasTools] 处理成功：{result.message}");
}
```

### 性能建议
1. **批量处理**：尽量使用 `ProcessAll` 或 `ScanAndSyncMaxSize` 一键方法，避免多次调用单独方法
2. **大量纹理**：处理大量纹理时，建议在非 UI 线程中执行或使用进度条反馈
3. **输出目录**：`GenerateAtlas` 会清空并重建输出目录，请确保输出路径正确

---

## 版本历史
| 版本 | 更新内容 |
| --- | --- |
| 1.0 | 初始版本：图集生成、材质替换功能 |
| 1.1 | 新增 LOD MaxSize 扫描与同步功能 |
| 1.2 | 新增 `ScanAndSyncMaxSize` 一键方法（推荐） |


