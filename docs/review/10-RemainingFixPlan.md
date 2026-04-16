# 剩余审查项实施计划

> 本文档是 5 轮代码审查后 **39 个未修复项** 的完整实施指南。  
> 任务按 **模块隔离原则** 拆分为 10 个 Phase、40 个原子任务。  
> 每个 Task 可在单次 AI 会话中独立完成，严格遵循 [12-AICollaboration.md](../12-AICollaboration.md) 五条铁律。

---

## 0. 总览

### 0.1 修复统计

| 状态 | 数量 | 占比 |
|------|------|------|
| ✅ 已修复（Batch 1-6） | 31 | 43.7% |
| 🔲 待修复（本计划） | 39 | 54.9% |
| ⏭️ 重复项（R3-09=R1-06） | 1 | 1.4% |
| **总计** | **71** | **100%** |

### 0.2 待修复严重度分布

| 严重度 | 数量 | Phase 分布 |
|--------|------|------------|
| 🔴 严重 | 4 | P1(R3-03), P2(R2-01), P3(R1-05), P10(R5-12) |
| 🟡 中等 | 20 | P1-P9 分散 |
| 🟢 轻微 | 15 | P6, P8, P9 为主 |

### 0.3 Phase 总表

| Phase | 名称 | Task 数 | 严重度 | 前置依赖 |
|-------|------|---------|--------|----------|
| **P1** | Downloads 接口提取 | 3 | 🔴🟡🟡 | 无（阻塞 P2/P6/P10） |
| **P2** | 跨模块依赖修复 | 3 | 🔴🟡🟡 | P1 |
| **P3** | ThemeService 架构修复 | 1 | 🔴 | 无 |
| **P4** | 启动管线完善 | 3 | 🟡🟡🟡 | 无 |
| **P5** | 安全加固 | 3 | 🟡🟡🟡 | 无 |
| **P6** | CancellationToken 改造 | 3 | 🟡🟢🟢 | P1 |
| **P7** | 代码质量提升 | 6 | 🟡🟡🟡🟡🟢🟢 | 无 |
| **P8** | Presentation 层改进 | 5 | 🟢×5 | 无 |
| **P9** | 文档同步 | 7 | 🟡🟢×6 | P1-P8 全部完成 |
| **P10** | 测试覆盖 | 6 | 🔴 | P1 |
| **总计** | | **40** | | |

### 0.4 依赖图

```
Phase 1 (Downloads 接口) ──┬──→ Phase 2 (跨模块依赖)
                           ├──→ Phase 6 (CT 改造)
                           └──→ Phase 10 (测试覆盖)

Phase 3 (ThemeService)  ─────→ 独立
Phase 4 (启动管线)      ─────→ 独立（→ P9.4 文档）
Phase 5 (安全加固)      ─────→ 独立
Phase 7 (代码质量)      ─────→ 独立
Phase 8 (Presentation)  ─────→ 独立

Phase 9 (文档同步) ← 等待 Phase 1-8 全部完成
```

### 0.5 约束（来自 12-AICollaboration.md）

| 编号 | 铁律 | 执行要求 |
|------|------|----------|
| AI-01 | 单模块原则 | 每 Task 仅操作一个模块，跨模块需声明契约变更 |
| AI-02 | 先读文档再写代码 | 编码前必读：模块 README、Contracts/、相关接口和测试 |
| AI-03 | 不跨模块加依赖 | 仅通过 Contracts 接口交互 |
| AI-04 | 同步更新文档 | 公共接口变更时同步更新 API 文档 |
| AI-05 | 声明影响面 | 跨模块契约变更在提交说明中列出受影响模块 |
| X-07 | 禁止自行 Task.Run | 后台任务通过 BackgroundTaskHost |
| X-09 | 不省略 CT | 所有异步方法包含 CancellationToken |
| X-10 | 不吞 Error | 处理 Result 的 Error 分支 |

### 0.6 验证标准

每个 Task 完成后必须满足：

```
1. dotnet build          → 零错误零警告
2. dotnet test           → 176/176 通过（或更多，如果新增测试）
3. 无遗留 TODO/HACK      → grep 确认
4. git commit + push     → 每 Phase 一次提交
5. 更新 99-ReviewLog.md  → 记录修复内容
```

---

## Phase 1：Downloads 接口提取

> **模块**：Downloads  
> **性质**：架构重构（契约变更）  
> **影响面**：所有引用 DownloadOrchestrator / DownloadScheduler 的模块  
> **前置依赖**：无  
> **阻塞**：P2、P6、P10

### Task 1.1 — 提取 IDownloadOrchestrator 接口 [R3-03 🔴]

**目标**：将具体类 `DownloadOrchestrator` 背后提取公共接口，遵循 DIP

**输入文件**（编码前必读）：
- `docs/05-CoreInterfaces.md` — 查看文档定义的接口签名
- `docs/07-DownloadSubsystem.md` — 下载编排器职责定义
- `src/Launcher.Infrastructure/Downloads/DownloadOrchestrator.cs` — 当前实现
- `src/Launcher.Application/Modules/Downloads/Contracts/` — 已有契约目录

**操作步骤**：
1. 在 `src/Launcher.Application/Modules/Downloads/Contracts/` 创建 `IDownloadOrchestrator.cs`
2. 提取所有 `public` 方法签名（`EnqueueAsync`, `PauseAsync`, `ResumeAsync`, `CancelAsync`, `RecoverAsync` 等）
3. 所有异步方法确保包含 `CancellationToken ct` 参数（AI-05 / X-09）
4. 修改 `DownloadOrchestrator.cs`：`internal sealed class DownloadOrchestrator : IDownloadOrchestrator`
5. 更新 `src/Launcher.Infrastructure/DependencyInjection.cs`：`services.AddSingleton<IDownloadOrchestrator, DownloadOrchestrator>()`
6. 全局搜索 `DownloadOrchestrator` 的直接引用，改为注入 `IDownloadOrchestrator`
7. 更新 `docs/05-CoreInterfaces.md` 添加接口文档（AI-04）

**受影响模块**（AI-05）：
- `Launcher.App`（App.xaml.cs 中 DI 注册）
- `Launcher.Background`（DownloadWorker / AutoInstallWorker 可能引用）
- `Launcher.Presentation`（DownloadsViewModel）
- `Launcher.Infrastructure`（内部引用）

**验证**：
- `dotnet build` 零错误
- `dotnet test` 全部通过
- grep 确认无 `DownloadOrchestrator` 的具体类直接注入（仅通过接口）

---

### Task 1.2 — 提取 IDownloadScheduler + IChunkDownloader 接口 [R3-04 🟡]

**目标**：将内部实现类提取接口，提升可测试性

**输入文件**：
- `src/Launcher.Infrastructure/Downloads/DownloadScheduler.cs`
- `src/Launcher.Infrastructure/Downloads/ChunkDownloadClient.cs`
- `src/Launcher.Application/Modules/Downloads/Contracts/` — 目标目录

**操作步骤**：
1. 在 Contracts 目录分别创建 `IDownloadScheduler.cs` 和 `IChunkDownloader.cs`
2. 提取公共方法签名（仅编排器/调度器之间交互的方法）
3. 两个实现类标记 `internal sealed`，实现对应接口
4. 更新 DI 注册
5. 更新 `docs/05-CoreInterfaces.md`

**注意**：这两个接口可能仅在 Downloads 模块内部使用，可考虑标记为 `internal` 接口。根据实际代码判断。

---

### Task 1.3 — IDownloadCheckpointRepository 分离决策 [R3-05 🟡]

**目标**：对齐文档与代码的 Checkpoint 接口设计

**推荐方案**：**方案 A — 保持合并，更新文档**

**理由**：
- `IDownloadTaskRepository` 已包含 Checkpoint CRUD 方法且运行正常
- 拆分为两个接口会增加 DI 注册复杂度，收益低
- 文档定义是早期设计，实现更符合实际需求

**操作步骤**：
1. 更新 `docs/07-DownloadSubsystem.md` §持久化层，说明 Checkpoint 操作合并到 `IDownloadTaskRepository`
2. 更新 `docs/05-CoreInterfaces.md` 反映合并设计
3. 在接口文件中添加注释分组：`// ── Checkpoint 操作 ──`

---

## Phase 2：跨模块依赖修复

> **性质**：契约变更  
> **前置依赖**：Phase 1（接口就位后才能正确解耦）

### Task 2.1 — RepairDownloadUrlProvider 消除对 FabApiClient 的直接引用 [R2-01 🔴]

**目标**：消除 Installations → FabLibrary 的具体类耦合（P-01 违规）

**输入文件**：
- `src/Launcher.Infrastructure/Installations/RepairDownloadUrlProvider.cs`
- `src/Launcher.Infrastructure/FabLibrary/FabApiClient.cs`
- `src/Launcher.Application/Modules/FabLibrary/Contracts/` — 已有契约
- `docs/04-ModuleDependencyRules.md` — 依赖规则

**操作步骤**：
1. 在 `src/Launcher.Application/Modules/FabLibrary/Contracts/` 创建 `IFabDownloadInfoProvider.cs`
   ```csharp
   public interface IFabDownloadInfoProvider
   {
       Task<Result<string>> GetDownloadUrlAsync(string assetId, CancellationToken ct);
   }
   ```
2. 在 `src/Launcher.Infrastructure/FabLibrary/` 创建实现（可在 `FabApiClient.cs` 中添加接口实现，或独立文件）
3. 修改 `RepairDownloadUrlProvider`：注入 `IFabDownloadInfoProvider` 替代 `FabApiClient`
4. 更新 DI 注册
5. 更新 `docs/04-ModuleDependencyRules.md`：Installations 允许依赖 FabLibrary.Contracts

**受影响模块**：Installations、FabLibrary

---

### Task 2.2 — Auth 依赖统一处理 [R2-03 + R2-04 🟡]

**目标**：消除 FabApiClient / EngineVersionApiClient 对 `IAuthService` 的显式依赖

**推荐方案**：**DelegatingHandler 统一注入 Token**

**输入文件**：
- `src/Launcher.Infrastructure/FabLibrary/FabApiClient.cs` — L7 using Auth
- `src/Launcher.Infrastructure/EngineVersions/EngineVersionApiClient.cs` — L7 using Auth
- `src/Launcher.Infrastructure/Auth/` — Token 获取逻辑
- `src/Launcher.Infrastructure/DependencyInjection.cs` — HttpClient 注册

**操作步骤**：
1. 在 `src/Launcher.Infrastructure/Auth/` 创建 `AuthenticatedHttpHandler.cs`
   ```csharp
   internal sealed class AuthenticatedHttpHandler : DelegatingHandler
   {
       private readonly IAuthService _authService;
       protected override async Task<HttpResponseMessage> SendAsync(
           HttpRequestMessage request, CancellationToken ct)
       {
           var token = await _authService.GetAccessTokenAsync(ct);
           if (token is not null)
               request.Headers.Authorization = new("Bearer", token);
           return await base.SendAsync(request, ct);
       }
   }
   ```
2. 在 DI 中为需要认证的 HttpClient 链式注入此 Handler
3. 从 `FabApiClient` 和 `EngineVersionApiClient` 中移除 `IAuthService` 注入和手动 Token 设置
4. 更新 `docs/04-ModuleDependencyRules.md`

**受影响模块**：Auth、FabLibrary、EngineVersions

---

### Task 2.3 — IFabAssetCommandService 返回类型修复 [R2-05 🟡]

**目标**：消除 FabLibrary.Contracts 对 Downloads.Domain 类型的泄漏

**输入文件**：
- `src/Launcher.Application/Modules/FabLibrary/Contracts/IFabAssetCommandService.cs`
- `src/Launcher.Domain/Downloads/` — DownloadTaskId 定义

**操作步骤**：
1. 检查 `IFabAssetCommandService.DownloadAssetAsync()` 的返回值
2. 将 `Result<DownloadTaskId>` 改为 `Result<Guid>`
3. 在实现端做 `.Map(id => id.Value)` 转换
4. 更新所有调用方

**注意**：改动较小但属跨模块契约变更，需声明影响面

---

## Phase 3：ThemeService 架构修复

> **模块**：Shell → Infrastructure  
> **性质**：架构重构  
> **前置依赖**：无，可与 P2 并行

### Task 3.1 — ThemeService 文件 I/O 下沉 [R1-05 🔴]

**目标**：Presentation 层禁止文件 I/O，将持久化逻辑下沉到 Infrastructure

**输入文件**：
- `src/Launcher.Presentation/Shell/ThemeService.cs` — L68-105，File.Exists/ReadAllText/WriteAllText
- `docs/02-ArchitecturePrinciples.md` — 层级职责定义

**操作步骤**：
1. 在 `src/Launcher.Infrastructure/Settings/` 创建 `IThemePersistenceService.cs`
   ```csharp
   public interface IThemePersistenceService
   {
       Task<string?> LoadThemeAsync(CancellationToken ct);
       Task SaveThemeAsync(string themeName, CancellationToken ct);
   }
   ```
2. 在同目录创建 `FileThemePersistenceService.cs` 实现（迁移原 File.* 逻辑）
3. 修改 `ThemeService.cs`：注入 `IThemePersistenceService`，删除所有 File.*/Directory.* 调用
4. 更新 DI 注册
5. 确认 `ThemeService.cs` 不再有 `using System.IO`

**验证**：grep `ThemeService.cs` 确认无 `File.`、`Directory.`、`Path.` 调用

---

## Phase 4：启动管线完善

> **模块**：App  
> **前置依赖**：无，可独立执行

### Task 4.1 — Phase 2 恢复步骤独立化 [R1-06 / R3-09 🟡]

**目标**：启动管线显式区分 Phase 2（恢复阶段）

**输入文件**：
- `src/Launcher.App/App.xaml.cs` — 当前启动流程
- `docs/10-StartupPipeline.md` — Phase 0-3 定义

**操作步骤**：
1. 在 `App.xaml.cs` 添加 `InitializePhase2Async(CancellationToken ct)` 方法
2. 内容：
   ```csharp
   // Phase 2：会话恢复 + 数据恢复
   await _authService.TryRestoreSessionAsync(ct);
   await _downloadOrchestrator.RecoverAsync(ct);
   // 资产索引如果单独加载，也放这里
   ```
3. 在 Phase 1 完成后、Phase 3 启动前调用
4. 添加日志：`Logger.Information("Phase 2 完成：会话恢复 + 下载恢复")`

---

### Task 4.2 — Phase 3 延迟初始化补全 [R3-10 🟡]

**目标**：根据实际已实现的服务，补充 Phase 3 延迟初始化项

**输入文件**：
- `src/Launcher.App/App.xaml.cs` — `StartBackgroundServicesAsync()`
- `docs/10-StartupPipeline.md` — Phase 3 定义的 6 项

**操作步骤**：
1. 审查已实现的服务，确定哪些 Phase 3 项实际可用
2. 已知可用：`AppUpdateWorker.Start()`
3. 对于文档中定义但尚未实现的服务（如缩略图预热、诊断收集），在代码中添加 TODO 注释标记
4. 更新 `docs/10-StartupPipeline.md` 对齐实际状态

---

### Task 4.3 — Splash / 骨架屏 [R3-08 🟡]

**目标**：启动时显示加载状态，避免空白窗口

**输入文件**：
- `src/Launcher.App/App.xaml.cs` — Phase 0 窗口显示
- `src/Launcher.App/MainWindow.xaml` — 主窗口定义

**操作步骤**：
1. 在 `MainWindow.xaml` 的 Shell 区域添加 ContentPlaceholder / Loading 指示器
2. Phase 0 显示 MainWindow 时展示"加载中"状态
3. Phase 1 完成后切换到真实内容
4. **注意**：WinUI 3 不支持传统 Splash Screen，需在 MainWindow 内部实现

**复杂度评估**：Medium — 需要协调 UI 和启动流程

---

## Phase 5：安全加固

> **模块**：Infrastructure  
> **前置依赖**：无，可独立执行

### Task 5.1 — HTTPS 强制校验 [R5-19 🟡]

**目标**：防止 API 端点被意外配置为 HTTP，导致 Token 明文传输

**输入文件**：
- `src/Launcher.Infrastructure/DependencyInjection.cs` — L93, L113, L120：HttpClient 注册
- `src/Launcher.App/appsettings.json` — API 端点配置

**操作步骤**：
1. 在每个 `ConfigureHttpClient` 委托中添加：
   ```csharp
   if (client.BaseAddress?.Scheme != "https")
       throw new InvalidOperationException(
           $"API 端点必须使用 HTTPS: {client.BaseAddress}");
   ```
2. 或创建统一的 `HttpClientSchemeValidator` 扩展方法
3. 如果存在开发环境需要 HTTP 的场景，通过 `IHostEnvironment.IsDevelopment()` 条件跳过

---

### Task 5.2 — LogSanitizer 全面应用 [R5-20 🟡]

**目标**：在所有涉及敏感信息（Token、URL）的日志点使用脱敏

**输入文件**：
- `src/Launcher.Shared/Logging/LogSanitizer.cs` — 已有 MaskToken/SanitizeUrl 方法
- `src/Launcher.Infrastructure/Auth/AuthService.cs` — Token 相关日志
- `src/Launcher.Infrastructure/Auth/EpicOAuthHandler.cs` — OAuth 相关日志

**操作步骤**：
1. 在 `AuthService.cs` 中搜索所有 `Logger.` 调用，Token 相关用 `LogSanitizer.MaskToken()`
2. 在 `EpicOAuthHandler.cs` 中搜索 URL 日志，用 `LogSanitizer.SanitizeUrl()`
3. 检查其他可能记录敏感信息的文件（grep `AccessToken|RefreshToken|ClientSecret`）

---

### Task 5.3 — TechnicalMessage 脱敏 [R5-18 🟡]

**目标**：Error 对象的 TechnicalMessage 不泄露系统细节

**输入文件**：
- `src/Launcher.Infrastructure/Downloads/DownloadErrors.cs` — Error 工厂
- 全项目 grep `TechnicalMessage` 或 `ex.Message`

**操作步骤**：
1. 在 `LogSanitizer` 中添加 `SanitizeExceptionMessage(Exception ex)` 方法
2. 过滤：文件路径 → 替换为 `[path]`、SQL 语句 → `[query]`、连接字符串 → `[conn]`
3. 审计所有 `TechnicalMessage = ex.Message` 赋值点，改为经过 Sanitizer
4. 确保 Error.UserMessage 始终使用预定义文案，不包含异常详情

---

## Phase 6：CancellationToken 改造

> **前置依赖**：Phase 1（新接口签名包含 CT）

### Task 6.1 — AutoInstallWorker 安装操作可取消 [R4-23 🟡]

**目标**：应用关闭时能优雅取消正在进行的安装

**输入文件**：
- `src/Launcher.Background/Installations/AutoInstallWorker.cs`
- `tests/Launcher.UnitTests/AutoInstallWorkerTests.cs` — 确认不破坏现有测试

**操作步骤**：
1. 添加 `private CancellationTokenSource _cts = new();`
2. 修改 `Start()` 方法保存 CTS 引用
3. 在所有安装调用中使用 `_cts.Token` 替代 `CancellationToken.None`
4. 添加 `Stop()` / `Dispose()` 方法：`_cts.Cancel(); _cts.Dispose();`
5. App 关闭时调用 `Stop()`

---

### Task 6.2 — DownloadsViewModel 生命周期 CT [R4-26 🟢]

**目标**：用户离开下载页面时取消进行中的操作

**输入文件**：
- `src/Launcher.Presentation/Modules/Downloads/DownloadsViewModel.cs`

**操作步骤**：
1. 添加 `private readonly CancellationTokenSource _disposalCts = new();`
2. 所有命令中的 `CancellationToken.None` 改为 `_disposalCts.Token`
3. 在 `Dispose()` 或 `OnNavigatedFrom()` 中调用 `_disposalCts.Cancel()`

---

### Task 6.3 — ConfigureAwait(false) 批量添加 [R5-11 🟢]

**目标**：非 UI 层 await 添加 `.ConfigureAwait(false)` 防御性编程

**涉及项目**：
- `Launcher.Infrastructure` — 全部 async 方法
- `Launcher.Background` — 全部 async 方法
- `Launcher.Application` — 全部 async 方法
- `Launcher.Shared` — 全部 async 方法

**排除**：
- `Launcher.Presentation` — 需要 SyncContext 回 UI 线程
- `Launcher.App` — 混合场景，逐一判断

**操作步骤**：
1. 逐文件审计 `await` 调用
2. 非 UI 代码统一添加 `.ConfigureAwait(false)`
3. 注意：`Task.Run` 内部的 await 不需要（已在线程池）
4. 批量操作，每完成一个项目后 `dotnet build` 确认

**复杂度**：Medium（文件多但改动机械）

---

## Phase 7：代码质量提升

> **模块**：Infrastructure 为主  
> **前置依赖**：无，各 Task 间独立

### Task 7.1 — Polly Pipeline 工厂 [R5-02 🟡]

**输入文件**：
- `src/Launcher.Infrastructure/FabLibrary/FabApiClient.cs` — L36-55
- `src/Launcher.Infrastructure/EngineVersions/EngineVersionApiClient.cs` — L33-49

**操作步骤**：
1. 在 `src/Launcher.Infrastructure/Network/` 创建 `HttpResiliencePipelineFactory.cs`
   ```csharp
   internal static class HttpResiliencePipelineFactory
   {
       public static ResiliencePipeline<HttpResponseMessage> CreateDefault() => ...
   }
   ```
2. 两个 API 客户端改为调用工厂方法
3. 删除重复的 Polly 配置代码

---

### Task 7.2 — 魔法数字提取为常量 [R5-14 🟡]

**输入文件**：
- `src/Launcher.Shared/AppConstants.cs` — 已有常量文件
- 全项目 grep 魔法数字

**需提取的常量**：

| 当前值 | 常量名 | 归属 |
|--------|--------|------|
| `1.2` | `DiskSpaceBufferFactor` | DownloadConstants |
| `5` (秒) | `SpeedSampleWindowSeconds` | DownloadConstants |
| `20` | `DefaultPageSize` | AppConstants |
| `300` (ms) | `SearchDebounceMs` | UiConstants |
| `500` (ms) | `ProgressThrottleMs` | DownloadConstants |
| `24` (h) | `UpdateCheckIntervalHours` | UpdateConstants |

**操作步骤**：
1. 在 `AppConstants.cs` 中添加嵌套静态类分组定义
2. 逐文件替换硬编码值为常量引用
3. 每替换一处后确认编译通过

---

### Task 7.3 — API URL 配置化 [R5-15 🟢]

**输入文件**：
- `src/Launcher.Infrastructure/DependencyInjection.cs` — L93, L113, L120
- `src/Launcher.Infrastructure/Auth/EpicOAuthHandler.cs` — OAuth 端点
- `src/Launcher.Infrastructure/Updates/AppUpdateService.cs` — L34
- `src/Launcher.App/appsettings.json`

**操作步骤**：
1. 在 `appsettings.json` 添加 `ApiEndpoints` 节：
   ```json
   "ApiEndpoints": {
     "FabApi": "https://www.fab.com/api",
     "UnrealEngine": "https://www.unrealengine.com/api",
     "GitHub": "https://api.github.com"
   }
   ```
2. 在 DI 注册中从 `IConfiguration["ApiEndpoints:*"]` 读取 BaseAddress
3. 删除代码中的硬编码 URL

---

### Task 7.4 — SettingsService 改进 [R5-08 + R5-17 🟡🟢]

**输入文件**：
- `src/Launcher.Infrastructure/Settings/SettingsService.cs` — L78-94, L218-222

**操作步骤**：
1. **R5-08**：JSON 深拷贝改为 record 或手动浅拷贝
   - 如果 Settings 类可改为 `record`，天然不可变，无需深拷贝
   - 否则用 `MemberwiseClone()` 替代 JSON 序列化
2. **R5-17**：4 个同构 `UpdateXxxConfigAsync` 改为泛型：
   ```csharp
   private async Task UpdateSectionAsync<T>(
       string section, Func<Settings, T> getter, Action<Settings, T> setter, T value)
   ```

---

### Task 7.5 — RecoverAsync 可读性改进 [R5-16 🟢]

**输入文件**：
- `src/Launcher.Infrastructure/Downloads/DownloadOrchestrator.cs` — RecoverAsync 方法

**操作步骤**：
1. 提取 `RecoverSingleTaskAsync(DownloadTask task, CancellationToken ct)` 私有方法
2. 在 `RecoverAsync` 中循环调用
3. 添加中文注释说明 Failed → Queued 的恢复意图

---

### Task 7.6 — Logger 命名规范统一 [R5-05 🟡]

**目标**：全项目统一 Logger 声明风格

**统一规范**：
```csharp
private static readonly ILogger Logger = Log.ForContext<ClassName>();
```

**操作步骤**：
1. grep 全项目 `ILogger|Log\.ForContext|Log\.Logger`
2. 统计现有 3 种风格的分布
3. 批量替换为统一风格
4. 确认无编译错误

---

## Phase 8：Presentation 层改进

> **模块**：Presentation  
> **前置依赖**：无，各 Task 间独立

### Task 8.1 — FabLibraryPage 排序映射移入 ViewModel [R1-01 🟢]

**输入文件**：
- `src/Launcher.Presentation/Modules/FabLibrary/FabLibraryPage.xaml.cs` — L56-66
- `src/Launcher.Presentation/Modules/FabLibrary/FabLibraryViewModel.cs`

**操作步骤**：
1. 在 ViewModel 中添加 `SelectedSortIndex` 属性（int）和 private `MapSortOrder()` 方法
2. Code-Behind 的 `SortComboBox_SelectionChanged` 改为仅设置 `ViewModel.SelectedSortIndex`
3. ViewModel 内部做 index → `FabSortOrder` 映射

---

### Task 8.2 — ViewModel 进度节流 [R2-06 🟢]

**输入文件**：
- `src/Launcher.Presentation/Modules/Downloads/DownloadsViewModel.cs`
- `src/Launcher.Presentation/Shell/ShellViewModel.cs`

**操作步骤**：
1. 在 `SnapshotChanged` 回调中添加 `Stopwatch` 防御性节流
2. 最多每 500ms 更新一次 UI 绑定属性
3. 源头已有 500ms 节流，这是双重保险

---

### Task 8.3 — ObservableCollection 批量操作 [R5-10 🟢]

**输入文件**：
- 所有 ViewModel 中使用 `ObservableCollection` 的 `Add()` 调用

**操作步骤**：
1. 搜索 `foreach.*\.Add(` 模式
2. 改为先构建 `List<T>` 再一次性赋值 `Items = new ObservableCollection<T>(list)`
3. 或使用 `Clear()` + 赋值新集合

---

### Task 8.4 — ViewModel Loading Guard 基类方法 [R5-04 🟢]

**操作步骤**：
1. 在 ViewModel 基类中添加：
   ```csharp
   protected async Task WithLoadingGuardAsync(Func<Task> operation)
   {
       if (IsLoading) return;
       IsLoading = true;
       try { await operation(); }
       finally { IsLoading = false; }
   }
   ```
2. 各 ViewModel 的 `LoadAsync` 改为调用此方法

---

### Task 8.5 — ViewModel 命名 + CT 约定统一 [R5-07 🟢]

**操作步骤**：
1. 统一命名约定：`LoadAsync(ct)`, `RefreshAsync(ct)`, `SearchAsync(query, ct)`
2. 所有公共异步方法添加 `CancellationToken ct = default` 参数
3. 支持 Refresh 的页面统一提供 `RefreshAsync()`

---

## Phase 9：文档同步

> **前置依赖**：Phase 1-8 全部完成后执行  
> **原则**：确保文档反映最终实现状态

### Task 9.1 — 核心接口文档更新 [R3-01 + R3-02 + R3-06]

**目标文件**：`docs/05-CoreInterfaces.md`

**更新内容**：
1. **R3-01**：§AuthService 添加 `event Action<SessionExpiredEvent>? SessionExpired;`
2. **R3-02**：§DownloadCommandService 添加 `PauseAllAsync`, `ResumeAllAsync` 签名
3. **R3-06**：§IDownloadRuntimeStore 更新至与代码一致的签名（`DownloadProgressSnapshot`、`Action<T>`委托、新增事件和方法）

---

### Task 9.2 — 模块依赖表补充 [R2-02]

**目标文件**：`docs/04-ModuleDependencyRules.md`

**更新内容**：
- Plugins 允许列表添加 `EngineVersions.Contracts`
- 如果 Task 2.2 采用方案 A（AuthenticatedHttpHandler），确认 Auth 依赖已从各模块移除

---

### Task 9.3 — 下载状态 + ShellState 文档对齐 [R3-07 + R3-12]

**目标文件**：
- `docs/07-DownloadSubsystem.md` — 移除或标注 `Installing` 值（R3-07）
- `docs/08-StateManagement.md` — 更新 ShellState 描述，反映直接在 ViewModel 管理的实际设计（R3-12）

---

### Task 9.4 — 启动流程文档对齐 [R3-08 + R3-10]

**目标文件**：`docs/10-StartupPipeline.md`

**更新内容**：
- Phase 0：更新为实际实现的加载方式
- Phase 2：补充独立恢复步骤（对应 Task 4.1）
- Phase 3：标注已实现和未实现的延迟初始化项

---

### Task 9.5 — Serilog enricher 配置补全 [R3-11]

**操作步骤**：
1. 在 `App.xaml.cs` 的 Serilog 配置链中添加 `.Enrich.WithMachineName()` 和 `.Enrich.WithProcessId()`
2. 确认 NuGet 包 `Serilog.Enrichers.Environment` 已引用
3. 更新 `docs/15-LoggingStrategy.md` 如需要

---

### Task 9.6 — ViewModelLocator 架构妥协文档化 [R2-07]

**操作步骤**：
1. 在 `ViewModelLocator.cs` 文件顶部添加注释说明此架构妥协的原因
2. 在 `docs/12-AICollaboration.md` 或相关文档中增加一条架构决策记录

---

### Task 9.7 — 日志消息语言统一 [R5-06]

**操作步骤**：
1. grep 全项目英文日志消息
2. 统一为中文（团队约定语言）
3. 保留结构化日志的属性名为英文（如 `{TaskId}`, `{ElapsedMs}`）

---

## Phase 10：测试覆盖

> **前置依赖**：Phase 1（接口就位后才能 Mock）  
> **目标**：核心子系统测试覆盖 ≥50%  
> **审查项**：R5-12 🔴

### Task 10.1 — DownloadOrchestrator 测试

**测试文件**：`tests/Launcher.UnitTests/DownloadOrchestratorTests.cs`（新建）

**最少用例**：
1. EnqueueAsync — 成功入队，返回 TaskId
2. EnqueueAsync — 磁盘空间不足，返回 Error
3. PauseAsync — 下载中 → 暂停
4. ResumeAsync — 暂停 → 恢复
5. CancelAsync — 任意状态 → 取消
6. RecoverAsync — Downloading 状态 → 恢复
7. RecoverAsync — Failed 状态 → 重新入队
8. EnqueueAsync — 重复任务 → 返回 Error

**Mock 依赖**：`IDownloadTaskRepository`, `IDownloadScheduler`, `IDownloadRuntimeStore`, `IFileSystemService`

---

### Task 10.2 — DownloadScheduler 测试

**测试文件**：`tests/Launcher.UnitTests/DownloadSchedulerTests.cs`（新建）

**最少用例**：
1. Schedule — 并发上限内，立即开始
2. Schedule — 超过并发上限，排队等待
3. Schedule — 异常不丢失（R4-01 回归测试）
4. Cancel — CT 取消时正确清理

---

### Task 10.3 — ChunkDownloadClient 测试

**测试文件**：`tests/Launcher.UnitTests/ChunkDownloadClientTests.cs`（新建）

**最少用例**：
1. Download — 成功分块下载合并
2. Download — 网络错误重试成功
3. Download — 用户取消不重试（R4-02 回归测试）
4. Download — 超过重试次数返回 Error

---

### Task 10.4 — AuthService 测试

**测试文件**：`tests/Launcher.UnitTests/AuthServiceTests.cs`（新建）

**最少用例**：
1. Login — 成功登录，Token 存储
2. Logout — 清除 Token，触发事件
3. Token 刷新 — 过期自动刷新
4. Token 刷新 — 并发请求不竞态（R4-09 回归测试）
5. RestoreSession — 有效 Token 恢复
6. RestoreSession — 过期 Token 刷新

---

### Task 10.5 — SettingsService 测试

**测试文件**：`tests/Launcher.UnitTests/SettingsServiceTests.cs`（新建）

**最少用例**：
1. Load — 默认配置
2. Update — 保存后读取一致
3. Update — 并发写入安全

---

### Task 10.6 — ViewModel 层 Happy Path 测试

**测试文件**：`tests/Launcher.UnitTests/ViewModelTests/`（新建目录）

**覆盖范围**：
1. `ShellViewModelTests` — 初始化、导航、更新状态
2. `DownloadsViewModelTests` — 加载列表、暂停、恢复命令
3. `FabLibraryViewModelTests` — 加载、搜索、排序

---

## 附录 A：审查项 → Task 映射表

| 审查 ID | 严重度 | Task | Phase | 状态 |
|---------|--------|------|-------|------|
| R1-01 | 🟢 | 8.1 | P8 | 🔲 |
| R1-05 | 🔴 | 3.1 | P3 | 🔲 |
| R1-06 | 🟡 | 4.1 | P4 | 🔲 |
| R2-01 | 🔴 | 2.1 | P2 | 🔲 |
| R2-02 | 🟡 | 9.2 | P9 | 🔲 |
| R2-03 | 🟡 | 2.2 | P2 | 🔲 |
| R2-04 | 🟡 | 2.2 | P2 | 🔲 |
| R2-05 | 🟡 | 2.3 | P2 | 🔲 |
| R2-06 | 🟢 | 8.2 | P8 | 🔲 |
| R2-07 | 🟢 | 9.6 | P9 | 🔲 |
| R3-01 | 🟡 | 9.1 | P9 | 🔲 |
| R3-02 | 🟡 | 9.1 | P9 | 🔲 |
| R3-03 | 🔴 | 1.1 | P1 | 🔲 |
| R3-04 | 🟡 | 1.2 | P1 | 🔲 |
| R3-05 | 🟡 | 1.3 | P1 | 🔲 |
| R3-06 | 🟡 | 9.1 | P9 | 🔲 |
| R3-07 | 🟢 | 9.3 | P9 | 🔲 |
| R3-08 | 🟡 | 4.3 | P4 | 🔲 |
| R3-09 | 🟡 | (=R1-06) | P4 | 🔲 |
| R3-10 | 🟡 | 4.2 | P4 | 🔲 |
| R3-11 | 🟢 | 9.5 | P9 | 🔲 |
| R3-12 | 🟢 | 9.3 | P9 | 🔲 |
| R4-23 | 🟡 | 6.1 | P6 | 🔲 |
| R4-26 | 🟢 | 6.2 | P6 | 🔲 |
| R5-02 | 🟡 | 7.1 | P7 | 🔲 |
| R5-04 | 🟢 | 8.4 | P8 | 🔲 |
| R5-05 | 🟡 | 7.6 | P7 | 🔲 |
| R5-06 | 🟢 | 9.7 | P9 | 🔲 |
| R5-07 | 🟢 | 8.5 | P8 | 🔲 |
| R5-08 | 🟡 | 7.4 | P7 | 🔲 |
| R5-10 | 🟢 | 8.3 | P8 | 🔲 |
| R5-11 | 🟢 | 6.3 | P6 | 🔲 |
| R5-12 | 🔴 | 10.1-10.6 | P10 | 🔲 |
| R5-14 | 🟡 | 7.2 | P7 | 🔲 |
| R5-15 | 🟢 | 7.3 | P7 | 🔲 |
| R5-16 | 🟢 | 7.5 | P7 | 🔲 |
| R5-17 | 🟢 | 7.4 | P7 | 🔲 |
| R5-18 | 🟡 | 5.3 | P5 | 🔲 |
| R5-19 | 🟡 | 5.1 | P5 | 🔲 |
| R5-20 | 🟡 | 5.2 | P5 | 🔲 |

---

## 附录 B：推荐执行顺序

```
第 1 轮（阻塞项）：P1 → P2
第 2 轮（并行）：  P3 | P4 | P5 | P7
第 3 轮（并行）：  P6 | P8
第 4 轮（收尾）：  P9
第 5 轮（质量）：  P10
```

每轮完成后：`git commit + push` → 更新 `99-ReviewLog.md` → 更新 `SESSION_HANDOFF.md`

---

## 附录 C：决策记录

| 编号 | 决策 | 理由 |
|------|------|------|
| D-01 | R3-05 保持 Checkpoint 合并到 TaskRepository | 拆分 ROI 低，现有设计运行正常 |
| D-02 | R2-03/04 用 AuthenticatedHttpHandler 统一 | 消除多模块对 Auth 的显式依赖 |
| D-03 | R3-12 更新文档反映 ViewModel 内嵌状态设计 | 独立 ShellState 类增加复杂度无收益 |
| D-04 | R3-09 = R1-06 重复项，仅执行一次 | 同一问题 |
| D-05 | R4-14 / R4-19 已在 Batch 2/4 修复，不重复 | 之前批次已处理 |
