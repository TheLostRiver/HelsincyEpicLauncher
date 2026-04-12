# EngineVersions 模块

---

## 架构定义

### 职责

- Unreal Engine 版本列表浏览（远程 + 本地缓存）
- 已安装引擎版本扫描
- 引擎版本下载（委托给 Downloads 模块）
- 引擎版本安装（委托给 Installations 模块）
- 引擎启动入口
- 版本兼容性信息

### 不负责

- 实际下载执行
- 实际文件安装
- Fab 资产管理

### 依赖

| 依赖目标 | 用途 |
|---------|------|
| `Downloads.Contracts` | 下载引擎版本 |
| `Installations.Contracts` | 安装/卸载引擎版本 |
| `Auth.Contracts` | 获取 Token 访问引擎版本列表 |
| `Settings.Contracts` | 引擎安装路径 |

### 谁可以依赖 EngineVersions

| 模块 | 用途 |
|------|------|
| FabLibrary | 按引擎版本过滤 Fab 资产 |
| Plugins | 按引擎版本过滤兼容插件 |

---

## API 定义

```csharp
namespace Launcher.Application.Modules.EngineVersions.Contracts;

public interface IEngineVersionReadService
{
    /// <summary>获取可用引擎版本列表</summary>
    Task<Result<IReadOnlyList<EngineVersionSummary>>> GetAvailableVersionsAsync(CancellationToken ct);

    /// <summary>获取本地已安装引擎版本</summary>
    Task<Result<IReadOnlyList<InstalledEngineSummary>>> GetInstalledVersionsAsync(CancellationToken ct);
}

public interface IEngineVersionCommandService
{
    /// <summary>下载并安装指定引擎版本</summary>
    Task<Result> DownloadAndInstallAsync(string versionId, string installPath, CancellationToken ct);

    /// <summary>卸载引擎版本</summary>
    Task<Result> UninstallAsync(string versionId, CancellationToken ct);

    /// <summary>启动引擎编辑器</summary>
    Task<Result> LaunchEditorAsync(string versionId, CancellationToken ct);
}

public sealed class EngineVersionSummary
{
    public string VersionId { get; init; } = default!;
    public string DisplayName { get; init; } = default!;   // 例："5.4.1"
    public long DownloadSize { get; init; }
    public DateTime ReleaseDate { get; init; }
    public bool IsInstalled { get; init; }
    public string? InstalledPath { get; init; }
}

public sealed class InstalledEngineSummary
{
    public string VersionId { get; init; } = default!;
    public string DisplayName { get; init; } = default!;
    public string InstallPath { get; init; } = default!;
    public long SizeOnDisk { get; init; }
    public DateTime InstalledAt { get; init; }
}
```

---

## 关键流程

### 浏览引擎版本

```
1. 用户进入引擎版本页面
2. EngineVersionsViewModel 初始化：
   a. 加载远程可用版本列表（缓存优先）
   b. 扫描本地已安装版本
   c. 合并显示
3. 卡片显示：版本号、发布日期、大小、安装状态
```

### 下载安装引擎

```
1. 用户点击某版本的"下载"按钮
2. IEngineVersionCommandService.DownloadAndInstallAsync()
3. 内部通过 Downloads.Contracts 创建下载任务
4. 下载完成后自动通过 Installations.Contracts 安装
5. 安装完成后更新本地引擎版本记录
```
