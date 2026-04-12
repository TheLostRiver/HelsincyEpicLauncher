# Updates 模块

---

## 架构定义

### 职责

- 启动器自身版本检查
- 新版本下载
- 更新安装（退出后替换）
- 版本变更日志展示

### 不负责

- Fab 资产更新（由 FabLibrary + Downloads 处理）
- 引擎更新（由 EngineVersions 处理）

### 依赖

| 依赖目标 | 用途 |
|---------|------|
| `Settings.Contracts` | 读取更新检查频率等配置 |
| `Launcher.Shared` | Result 模型 |

### 谁可以依赖 Updates

| 模块 | 用途 |
|------|------|
| Shell | 显示"有更新可用"提示 |

---

## API 定义

```csharp
namespace Launcher.Application.Modules.Updates.Contracts;

public interface IAppUpdateService
{
    /// <summary>检查是否有新版本</summary>
    Task<Result<UpdateInfo?>> CheckForUpdateAsync(CancellationToken ct);

    /// <summary>下载并准备更新</summary>
    Task<Result> DownloadUpdateAsync(UpdateInfo update, IProgress<double>? progress, CancellationToken ct);

    /// <summary>应用更新（需要重启应用）</summary>
    Task<Result> ApplyUpdateAsync(CancellationToken ct);

    /// <summary>跳过此版本</summary>
    Task SkipVersionAsync(string version, CancellationToken ct);
}

public sealed class UpdateInfo
{
    public string Version { get; init; } = default!;
    public string DownloadUrl { get; init; } = default!;
    public long DownloadSize { get; init; }
    public string ReleaseNotes { get; init; } = default!;
    public DateTime ReleaseDate { get; init; }
    public bool IsMandatory { get; init; }
}
```

### 对外事件

```csharp
public sealed record UpdateAvailableEvent(string Version, bool IsMandatory);
```

---

## 关键流程

### 自动检查更新

```
1. App 启动 Phase 3（延迟初始化）
2. AppUpdateWorker 定时检查新版本（默认每 24 小时）
3. 发现新版本 → 发布 UpdateAvailableEvent
4. Shell 收到事件 → 在 UI 上显示更新提示
5. 用户选择"立即更新"或"稍后"或"跳过此版本"
```

### 执行更新

```
1. 用户点击"立即更新"
2. IAppUpdateService.DownloadUpdateAsync() 下载更新包
3. 下载完成 → IAppUpdateService.ApplyUpdateAsync()
4. 复制更新包到临时目录
5. 启动更新器进程
6. 退出当前应用
7. 更新器替换文件
8. 重新启动应用
```
