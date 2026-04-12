# Plugins 模块

---

## 架构定义

### 职责

- 已安装 UE 插件/资产的管理
- 插件与 UE 项目的集成（添加/移除插件到项目）
- 插件兼容性检查（引擎版本匹配）
- 插件启用/禁用

### 不负责

- 插件下载（由 Downloads 模块处理）
- 插件浏览/购买（由 FabLibrary 处理）
- 引擎版本管理（由 EngineVersions 处理）

### 依赖

| 依赖目标 | 用途 |
|---------|------|
| `FabLibrary.Contracts` | 查询已拥有的插件类资产信息 |
| `Installations.Contracts` | 查询插件安装状态 |
| `EngineVersions.Contracts` | 引擎版本兼容性检查 |
| `Settings.Contracts` | UE 项目路径配置 |

### 谁可以依赖 Plugins

无。Plugins 是末端消费模块。

---

## API 定义

```csharp
namespace Launcher.Application.Modules.Plugins.Contracts;

public interface IPluginReadService
{
    /// <summary>获取已安装的插件列表</summary>
    Task<Result<IReadOnlyList<PluginSummary>>> GetInstalledPluginsAsync(CancellationToken ct);

    /// <summary>检查插件与引擎版本的兼容性</summary>
    Task<Result<CompatibilityReport>> CheckCompatibilityAsync(
        string pluginId, string engineVersionId, CancellationToken ct);
}

public interface IPluginCommandService
{
    /// <summary>将插件添加到指定 UE 项目</summary>
    Task<Result> AddToProjectAsync(string pluginId, string projectPath, CancellationToken ct);

    /// <summary>从 UE 项目中移除插件</summary>
    Task<Result> RemoveFromProjectAsync(string pluginId, string projectPath, CancellationToken ct);
}

public sealed class PluginSummary
{
    public string PluginId { get; init; } = default!;
    public string Name { get; init; } = default!;
    public string Version { get; init; } = default!;
    public string Author { get; init; } = default!;
    public string InstallPath { get; init; } = default!;
    public IReadOnlyList<string> SupportedEngineVersions { get; init; } = [];
}

public sealed class CompatibilityReport
{
    public bool IsCompatible { get; init; }
    public string? IncompatibilityReason { get; init; }
}
```
