# Installations 模块

---

## 架构定义

### 职责

- 已下载资产的安装（解压到目标目录）
- 文件完整性校验（哈希比对）
- 损坏文件修复（重新下载损坏的文件）
- 资产卸载（清理安装目录）
- 安装版本管理
- 本地已安装资产扫描与索引
- Patch 应用（增量更新）

### 不负责

- 文件下载（由 Downloads 模块处理）
- Fab API 调用（由 FabLibrary 处理）
- UI 渲染

### 依赖

| 依赖目标 | 用途 |
|---------|------|
| `Downloads.Contracts` | 查询下载完成状态，修复时重新下载 |
| `Settings.Contracts` | 读取默认安装路径 |
| `Launcher.Shared` | Result 模型 |

### 谁可以依赖 Installations

| 模块 | 用途 |
|------|------|
| FabLibrary | 查询资产是否已安装 |
| EngineVersions | 复用安装/校验能力 |
| Plugins | 查询插件安装状态 |

---

## API 定义

> 详见 [05-CoreInterfaces.md](../05-CoreInterfaces.md) 第 8~9 节 `IInstallCommandService` / `IInstallReadService` / `IIntegrityVerifier`

### 补充：安装 Manifest

```csharp
/// <summary>
/// 安装清单。记录一个资产的全部文件信息，用于校验和修复。
/// </summary>
public sealed class InstallManifest
{
    public string AssetId { get; init; } = default!;
    public string Version { get; init; } = default!;
    public IReadOnlyList<ManifestFileEntry> Files { get; init; } = [];
    public long TotalSize { get; init; }
}

public sealed class ManifestFileEntry
{
    public string RelativePath { get; init; } = default!;
    public long Size { get; init; }
    public string Hash { get; init; } = default!;       // SHA-1
}
```

### 对外事件

```csharp
public sealed record InstallationCompletedEvent(string AssetId, string InstallPath);
public sealed record InstallationFailedEvent(string AssetId, string ErrorMessage);
public sealed record UninstallCompletedEvent(string AssetId);
public sealed record RepairCompletedEvent(string AssetId, int RepairedFileCount);
```

---

## 关键流程

### 安装资产

```
1. DownloadCompletedEvent 触发 或 用户手动点击"安装"
2. IInstallCommandService.InstallAsync(request)
3. InstallHandler：
   a. 验证源文件存在
   b. 验证目标路径（磁盘空间、权限）
   c. 创建 InstallJob 领域实体
   d. 交由 InstallWorker（后台）执行
4. InstallWorker：
   a. 解压/复制文件到安装目录
   b. 逐文件写入
   c. 校验已安装文件哈希
   d. 保存 Manifest 到本地
   e. 写 InstallationRepository 记录
   f. 发布 InstallationCompletedEvent
```

### 校验安装

```
1. 用户点击"验证文件"
2. IIntegrityVerifier.VerifyInstallationAsync(installPath, manifest, progress)
3. VerificationWorker（后台）：
   a. 读取本地 Manifest
   b. 遍历 Manifest 中的每个文件
   c. 计算 SHA-1 与 Manifest 比对
   d. 收集 missing + corrupted 列表
   e. 返回 VerificationReport
4. 如果有损坏文件 → UI 提示"是否修复？"
```

### 修复安装

```
1. 用户确认修复 或 IInstallCommandService.RepairAsync(assetId)
2. RepairHandler：
   a. 先执行完整性校验，获取损坏文件列表
   b. 对每个损坏/缺失文件：
      - 通过 Downloads 模块重新下载对应文件
      - 替换损坏文件
   c. 再次校验
   d. 发布 RepairCompletedEvent
```

### 卸载

```
1. 用户点击"卸载"
2. IInstallCommandService.UninstallAsync(assetId)
3. 确认对话框
4. 删除安装目录
5. 删除 InstallationRepository 记录
6. 删除本地 Manifest
7. 发布 UninstallCompletedEvent
```

---

## 安装状态机

```csharp
public enum InstallState
{
    NotInstalled,
    Installing,
    Installed,
    Verifying,
    NeedsRepair,
    Repairing,
    Uninstalling,
    Failed
}
```

状态转换：

```
NotInstalled → Installing → Installed
Installed → Verifying → Installed（校验通过）
Installed → Verifying → NeedsRepair（校验失败）
NeedsRepair → Repairing → Installed
Installed → Uninstalling → NotInstalled
Installing → Failed → NotInstalled（重试） 
```
