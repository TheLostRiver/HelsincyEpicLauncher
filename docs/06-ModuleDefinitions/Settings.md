# Settings 模块

---

## 架构定义

### 职责

- 应用配置管理（读取/写入/验证）
- 下载配置（并发数、chunk 大小、自动安装）
- 外观配置（主题、语言）
- 路径配置（默认安装路径、缓存路径、日志路径）
- 网络配置（代理、超时、CDN 回退）
- 配置分层加载：`appsettings.json` → `appsettings.Local.json` → `user.settings.json` → 数据库动态配置

### 不负责

- 执行下载/安装操作
- UI 渲染
- 配置文件的物理存储实现（由 Infrastructure 处理）

### 依赖

| 依赖目标 | 用途 |
|---------|------|
| `Launcher.Shared` | Result 模型 |

### 谁可以依赖 Settings

| 模块 | 用途 |
|------|------|
| Downloads | 读取下载相关配置 |
| FabLibrary | 读取默认安装路径 |
| Installations | 读取安装路径 |
| EngineVersions | 读取引擎安装路径 |
| Diagnostics | 读取日志路径 |
| Shell | 读取主题和语言配置 |

---

## API 定义

> 详见 [05-CoreInterfaces.md](../05-CoreInterfaces.md) 第 16 节 `IAppConfigProvider`

### 补充：配置写入

```csharp
namespace Launcher.Application.Modules.Settings.Contracts;

/// <summary>
/// 配置写入服务。
/// </summary>
public interface ISettingsCommandService
{
    Task<Result> UpdateDownloadConfigAsync(DownloadConfig config, CancellationToken ct);
    Task<Result> UpdateAppearanceConfigAsync(AppearanceConfig config, CancellationToken ct);
    Task<Result> UpdatePathConfigAsync(PathConfig config, CancellationToken ct);
    Task<Result> UpdateNetworkConfigAsync(NetworkConfig config, CancellationToken ct);
    Task<Result> ResetToDefaultsAsync(CancellationToken ct);
}
```

### 配置变更事件

```csharp
public sealed record ConfigChangedEvent(string Section, object NewConfig);
// Section: "Download" / "Appearance" / "Paths" / "Network"
```

---

## 关键流程

### 配置加载（启动时）

```
1. App 启动 Phase 1
2. 按优先级加载配置：
   a. 内嵌默认值
   b. appsettings.json
   c. appsettings.Local.json（如存在，覆盖）
   d. user.settings.json（用户级覆盖）
3. 绑定到强类型 Config 对象
4. 注册到 DI 容器
```

### 配置修改

```
1. 用户在设置页面修改配置
2. SettingsViewModel 调用 ISettingsCommandService.UpdateXxxAsync()
3. 验证新配置合法性
4. 写入 user.settings.json
5. 更新内存中的 Config 对象
6. 发布 ConfigChangedEvent
7. 相关模块收到事件后自行调整行为
```
