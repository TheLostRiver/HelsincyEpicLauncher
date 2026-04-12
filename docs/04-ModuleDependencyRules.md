# 模块依赖规则

> 本文档定义模块之间的依赖方向、允许的通信方式、以及硬性禁止项。  
> 这是防止耦合蔓延的核心规约，人类和 AI 都必须严格遵守。

---

## 1. 模块清单

| 模块 | 命名空间前缀 | 职责概述 |
|------|-------------|---------|
| **Shell** | `Launcher.*.Shell` | 主窗口壳层、导航、标题栏、全局 UI 宿主 |
| **Auth** | `Launcher.*.Auth` | Epic OAuth 登录、Token 管理、会话维持 |
| **FabLibrary** | `Launcher.*.FabLibrary` | Fab 资产浏览/搜索/已拥有资产管理 |
| **Downloads** | `Launcher.*.Downloads` | 下载队列、分块下载、断点续传、进度、CDN 切换 |
| **Installations** | `Launcher.*.Installations` | 安装/校验/修复/卸载/版本管理 |
| **EngineVersions** | `Launcher.*.EngineVersions` | UE 引擎版本列表/下载/安装/本地扫描 |
| **Plugins** | `Launcher.*.Plugins` | UE 插件管理/项目集成 |
| **Settings** | `Launcher.*.Settings` | 应用配置/下载配置/外观/路径 |
| **Diagnostics** | `Launcher.*.Diagnostics` | 日志面板/网络诊断/磁盘状态/缓存管理 |
| **Updates** | `Launcher.*.Updates` | 启动器自身更新检查与安装 |

---

## 2. 允许的依赖方向

### 2.1 层间依赖

```
Presentation  →  Application  →  Domain  →  Shared
                      ↕
                  Contracts（接口定义）
                      ↑
               Infrastructure（实现接口）
               Background（调用 Application / 实现 Contracts）
```

| 源 | 可以依赖 | 说明 |
|----|---------|------|
| Presentation | Application, Domain(仅 Contracts/DTO/枚举), Shared | 通过 ViewModel 调用用例 |
| Application | Domain, Shared, 各模块 Contracts | 编排业务流程 |
| Domain | Shared | 纯业务规则，零外部依赖 |
| Infrastructure | Domain, Shared, 各模块 Contracts | 实现接口 |
| Background | Application, Domain, Shared, 各模块 Contracts | 后台任务执行 |

### 2.2 模块间依赖

模块之间 **只允许** 依赖对方的 **Contracts**：

| 源模块 | 可以依赖 | 说明 |
|--------|---------|------|
| Shell | 所有模块的 Contracts | Shell 是总壳，需要路由到各模块页面 |
| FabLibrary | Downloads.Contracts | 查询下载状态、发起下载命令 |
| FabLibrary | Installations.Contracts | 查询安装状态 |
| Downloads | *(无跨模块依赖)* | 下载是底层服务，不依赖上层模块 |
| Installations | Downloads.Contracts | 查询下载完成的资产 |
| EngineVersions | Downloads.Contracts, Installations.Contracts | 复用下载和安装能力 |
| Plugins | FabLibrary.Contracts, Installations.Contracts | 查询资产和安装状态 |
| Settings | *(无跨模块依赖)* | 被其他模块通过 Contracts 查询 |
| Diagnostics | *(无跨模块依赖)* | 读取日志/系统信息 |
| Updates | *(无跨模块依赖)* | 独立检查/执行更新 |

---

## 3. 硬性禁止项

### 禁止 P-01：跨模块引用内部实现

```
❌ FabLibrary → Downloads.Infrastructure.ChunkDownloadClient
❌ Settings → Downloads.Domain.DownloadStateMachine
❌ Installations → FabLibrary.Infrastructure.SqliteFabAssetRepository
```

**正确做法**：通过 `Downloads.Contracts.IDownloadReadService` 查询。

### 禁止 P-02：跨模块直接操作 ViewModel

```
❌ Downloads 内部代码直接更新 FabLibraryViewModel
❌ Installations 直接弹 Settings 页面通知
```

**正确做法**：通过事件或 Shell 级通知服务间接通信。

### 禁止 P-03：跨模块共享可变领域对象

```
❌ FabLibrary 和 Downloads 共用同一个 FabAsset 实例并互改字段
```

**正确做法**：各自持有独立的 DTO/Summary 副本。

### 禁止 P-04：模块绕过 Contracts 直连数据库

```
❌ FabLibrary 直接查 Downloads 表
❌ Downloads 直接改 Installations 的数据库记录
```

**正确做法**：通过对方 Contracts 的 Repository 接口。

### 禁止 P-05：反向依赖

```
❌ Infrastructure → Presentation
❌ Domain → Infrastructure
❌ Application → Presentation
❌ Downloads → FabLibrary.Presentation
```

### 禁止 P-06：循环依赖

```
❌ ModuleA.Contracts → ModuleB.Contracts → ModuleA.Contracts
```

如果两个模块互相需要对方的能力，说明边界划分有问题，应该提取公共 Contracts 或引入中介。

---

## 4. 允许的四种跨模块通信方式

### 4.1 Query（只读查询）

返回摘要/投影，不暴露内部过程。

```csharp
// FabLibrary 查询某资产的下载状态
public interface IDownloadReadService
{
    Task<DownloadStatusSummary?> GetStatusAsync(string assetId, CancellationToken ct);
    Task<IReadOnlyList<DownloadStatusSummary>> GetActiveDownloadsAsync(CancellationToken ct);
}
```

### 4.2 Command（请求执行动作）

不暴露内部过程，返回结果。

```csharp
// FabLibrary 请求开始下载
public interface IDownloadCommandService
{
    Task<Result<DownloadTaskId>> StartAsync(StartDownloadRequest request, CancellationToken ct);
    Task<Result> PauseAsync(DownloadTaskId taskId, CancellationToken ct);
    Task<Result> ResumeAsync(DownloadTaskId taskId, CancellationToken ct);
    Task<Result> CancelAsync(DownloadTaskId taskId, CancellationToken ct);
}
```

### 4.3 Event（已发生事实通知）

事件是通知，不是调用替代品。发布者不关心谁订阅。

```csharp
// Downloads 模块发布
public sealed record DownloadCompletedEvent(DownloadTaskId TaskId, string AssetId);

// Installations 模块订阅后决定是否自动安装
// FabLibrary 模块订阅后刷新资产状态
```

**事件使用规则**：
- 事件名必须是过去时（`Completed`、`Failed`、`Queued`）
- 事件只表达"已发生"，不携带大量数据
- 不把事件系统当成万能总线
- 不在事件处理器中做长时间阻塞操作

### 4.4 Projection / Summary（稳定投影）

给 UI 或其他模块看的稳定数据视图。

```csharp
// 下载模块对外投影
public sealed class DownloadStatusSummary
{
    public string AssetId { get; init; } = default!;
    public DownloadUiState UiState { get; init; }  // 收敛后的 UI 状态
    public double Progress { get; init; }
    public long DownloadedBytes { get; init; }
    public long TotalBytes { get; init; }
    public bool CanPause { get; init; }
    public bool CanResume { get; init; }
    public bool CanCancel { get; init; }
    public string? ErrorMessage { get; init; }
}
```

---

## 5. UI 投影与领域状态分离

### 5.1 下载模块内部状态（细粒度）

```
Preparing
WaitingForManifest
WaitingForDisk
DownloadingChunks
RetryingChunk
VerifyingChunks
ApplyingPatch
Repairing
Finalizing
```

### 5.2 对外 UI 状态（收敛后，稳定）

```csharp
public enum DownloadUiState
{
    Queued,
    Downloading,
    Paused,
    Installing,
    Completed,
    Failed
}
```

**好处**：
- UI 稳定，不因内部状态机重构而变化
- 其他模块稳定，只消费收敛后的状态
- 内部可以随时增加新的细粒度状态而不影响外部

---

## 6. 典型规则示例

### 规则 D-01：FabLibrary 不得依赖 Downloads 内部

FabLibrary 模块只能依赖：
- `IDownloadReadService`
- `IDownloadCommandService`
- `DownloadStatusSummary`
- `DownloadUiState`
- `DownloadCompletedEvent`

这意味着：
- ✅ 可以重构下载器内部状态机
- ✅ 可以改 chunk 调度算法
- ✅ 可以改 retry 策略
- ✅ 可以改 checkpoint 结构
- ✅ 只要 Contracts 不变，FabLibrary **零代码改动**

### 规则 D-02：外部 DTO 必须只读

跨模块 DTO 设计为不可变（`init` 属性），禁止模块 A 把对象传给模块 B 后由 B 修改字段。

### 规则 D-03：禁止领域实体直接暴露给 UI

页面只能绑定：
- ViewModel
- Summary / DTO

禁止直接绑定 `DownloadTask`、`FabAsset` 等领域实体。

---

## 7. 依赖关系全景图

```
[Shell]
  ├─ uses → [Auth.Contracts]
  ├─ uses → [FabLibrary.Contracts]
  ├─ uses → [Downloads.Contracts]
  ├─ uses → [Installations.Contracts]
  ├─ uses → [EngineVersions.Contracts]
  ├─ uses → [Plugins.Contracts]
  ├─ uses → [Settings.Contracts]
  └─ uses → [Diagnostics.Contracts]

[FabLibrary]
  ├─ depends on → [FabLibrary.Contracts]
  ├─ may query → [Downloads.Contracts]
  └─ may query → [Installations.Contracts]

[Downloads]
  ├─ depends on → [Downloads.Contracts]
  └─ may emit → [DownloadCompletedEvent, DownloadFailedEvent]

[Installations]
  ├─ depends on → [Installations.Contracts]
  ├─ may query → [Downloads.Contracts]
  └─ may emit → [InstallationCompletedEvent, InstallationFailedEvent]

[EngineVersions]
  ├─ depends on → [EngineVersions.Contracts]
  ├─ may query → [Downloads.Contracts]
  └─ may query → [Installations.Contracts]

[Plugins]
  ├─ depends on → [Plugins.Contracts]
  ├─ may query → [FabLibrary.Contracts]
  └─ may query → [Installations.Contracts]

[Settings]
  └─ depends on → [Settings.Contracts]

[Diagnostics]
  └─ depends on → [Diagnostics.Contracts]

[Updates]
  └─ depends on → [Updates.Contracts]
```

**铁律**：
- 所有横向箭头都指向 `.Contracts`
- 不存在指向 `.Infrastructure`、`.Domain.Internal` 的横向箭头
- 不存在反向箭头（下游模块不依赖上游模块）

---

## 8. 反耦合检查清单

每次新增功能或修改代码时，逐条检查：

| 编号 | 检查项 | 健康标准 |
|------|--------|---------|
| C-01 | 这个改动是否要求改动两个以上无关模块？ | 否 |
| C-02 | 是否直接引用了别的模块内部实现？ | 否 |
| C-03 | 是否把领域实体直接暴露给 UI？ | 否 |
| C-04 | 是否新增了模糊命名的万能 Service？ | 否 |
| C-05 | 是否通过静态全局状态偷传数据？ | 否 |
| C-06 | 是否新增了跨模块共享可变对象？ | 否 |
| C-07 | 是否让 UI 直接调基础设施？ | 否 |
| C-08 | 是否某个 DTO 开始承载过多职责？ | 否 |
| C-09 | 是否把事件当成调用主干？ | 否 |
| C-10 | 这个模块能否单独拿出来理解和测试？ | 是 |

> **规则**：若 3 条以上答案不健康，必须停下来审视边界设计。
