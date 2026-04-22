# 审查日志

> 实时记录每轮审查的执行状态、发现数量和关键摘要。

---

## 日志条目

| 时间 | 轮次 | 状态 | 发现数 | 摘要 |
|------|------|------|--------|------|
| 2026-04-16 | 准备 | ✅ 完成 | - | 审查计划编写完成，文件夹建立 |
| 2026-04-16 | 第1遍 | ✅ 完成 | 6 | 架构与分层合规性审查：1🔴 4🟡 1🟢 |
| 2026-04-16 | 第2遍 | ✅ 完成 | 7 | 模块耦合与依赖审查：1🔴 4🟡 2🟢 |
| 2026-04-16 | 第3遍 | ✅ 完成 | 12 | 文档契约vs实际实现：1🔴 8🟡 3🟢 |
| 2026-04-16 | 第4遍 | ✅ 完成 | 26 | Bug与边界条件：3🔴 19🟡 4🟢 |
| 2026-04-16 | 第5遍 | ✅ 完成 | 20 | 可改进项与最终总结：1🔴 11🟡 8🟢 |
| **总计** | **5轮** | **✅ 全部完成** | **71** | **7🔴 + 46🟡 + 18🟢 · 综合评分 6.7/10** |

---

## 详细日志

### 2026-04-16 — 审查准备

- 创建 `docs/review/` 目录
- 编写审查计划 `00-ReviewPlan.md`
- 创建审查日志 `99-ReviewLog.md`
- 阅读全部设计文档（01~15 + 06-ModuleDefinitions 全部 10 个模块）
- 基线知识就绪，准备开始第 1 遍审查

### 2026-04-16 — 第1遍审查

- 审查范围：层级引用(csproj)、Code-Behind合规、ViewModel合规、Shell服务、DI注册、启动管线、Background隔离、Domain纯净
- 检查了全部 9 个 csproj 文件的 ProjectReference
- 逐文件审查所有 .xaml.cs (10个) 和 ViewModel (9个)  
- 关键发现：ThemeService.cs 在 Presentation 层直接执行文件 I/O（🔴）
- 关键发现：ShellViewModel 使用 Visibility 类型耦合 WinUI（🟡）
- 关键发现：2 个 ViewModel 硬编码安装路径（🟡）
- 输出文档：`01-Review-ArchitectureCompliance.md`

### 2026-04-16 — 第2遍审查

- 审查范围：P-01~P-06 禁止项、模块依赖表合规、反模式 AP-01~AP-06
- 逐文件检查 Infrastructure 层和 Background 层的 using 语句
- 对照 04-ModuleDependencyRules.md 依赖表逐模块校验
- 关键发现：RepairDownloadUrlProvider 直接引用 FabApiClient（🔴 P-01 违规）
- 关键发现：3 个模块依赖超出总依赖表（Auth.Contracts 未列入、Plugins→EngineVersions）
- 关键发现：IFabAssetCommandService 返回值泄漏 Downloads 域类型
- 反模式检查全部通过（ViewModel 最大 342 行、Service 最大 8 方法）
- 输出文档：`02-Review-ModuleCoupling.md`

### 2026-04-16 — 第3遍审查

- 审查范围：核心接口签名(13个接口)、下载状态机、错误处理、启动流程、日志策略、状态管理
- 关键发现：IDownloadOrchestrator 接口不存在，仅有具体类（🔴 违反 DIP）
- 关键发现：IDownloadRuntimeStore 签名与文档大幅偏离（方法名/事件类型/DTO名均不同）
- 关键发现：Phase 0-3 启动流程多处不完整（无骨架屏、Phase 2 未独立、Phase 3 仅 1/6 完成）
- 确认：状态机、错误处理模型完全匹配；日志策略合规；结构化模板无违规
- 输出文档：`03-Review-ContractCompliance.md`

### 2026-04-16 — 第4遍审查

- 审查范围：空引用、资源泄漏、线程安全、异常吞噬、CancellationToken传递、async/await陷阱、DB操作、路径安全、并发竞态
- 逐文件审查 Infrastructure/Downloads(7文件)、Infrastructure/Auth(4文件)、Infrastructure/Installations(4文件)、Background Workers(3文件)、Persistence/Sqlite(3文件)、Shell层
- 关键发现：DownloadScheduler fire-and-forget 调度异常静默丢失（🔴）
- 关键发现：ShellViewModel.OnSessionExpired 在非UI线程修改 ObservableProperty 导致崩溃（🔴）
- 关键发现：App.xaml.cs GetAwaiter().GetResult() UI线程死锁风险（🔴）
- 关键发现：AuthService Token刷新 TOCTOU 竞态条件（🟡）
- 关键发现：ChunkDownloadClient Polly 误重试用户取消，暂停响应延迟31秒（🟡）
- 关键发现：InstallationRepository assetId 路径遍历风险（🟡）
- 累计发现（4轮）：6🔴 + 35🟡 + 10🟢 = 51 项
- 输出文档：`04-Review-BugsAndEdgeCases.md`

### 2026-04-16 — 第5遍审查

- 审查范围：代码重复(DRY)、命名一致性、性能隐患、测试覆盖度、硬编码值、可读性、安全性(OWASP)
- 关键发现：核心子系统（DownloadOrchestrator/Scheduler/AuthService/ViewModel）无单元测试（🔴）
- 关键发现：Error 对象创建冗余（DL_NOT_FOUND × 4 等同一文件重复）（🟡）
- 关键发现：Polly Pipeline + JsonSerializerOptions 配置跨文件重复（🟡）
- 关键发现：应用名 "HelsincyEpicLauncher" 8 处散布硬编码（🟡）
- 关键发现：SettingsService 使用 JSON 序列化做深拷贝（性能）（🟡）
- 关键发现：LogSanitizer 已实现但未在 Auth 日志中使用（🟡）
- 测试覆盖度估算：Domain ~70%, Infrastructure ~25%, Background ~33%, Presentation ~5%, App 0%
- **五轮审查全部完成**
- 累计发现：7🔴 + 46🟡 + 18🟢 = 71 项
- 综合代码质量评分：**6.7 / 10**
- Top 10 优先修复项已排列
- 输出文档：`05-Review-ImprovementsAndSummary.md`

---

## 修复记录

### 2026-04-16 — Batch 1（关键 Bug 修复）

**Commit**: `dd33a85` | **文件**: 6 | **测试**: 176/176 通过

| ID | 修复内容 |
|----|----------|
| R4-08 | ShellViewModel.OnSessionExpired 加 DispatcherQueue 包裹（线程崩溃） |
| R4-01 | DownloadScheduler.TryScheduleNextAsync 顶层 try/catch（异常丢失） |
| R4-13 | App.xaml.cs 数据库初始化用 Task.Run 包裹（死锁风险） |
| R4-02 | ChunkDownloadClient Polly 排除用户取消的 TaskCanceledException |
| R4-03 | ChunkDownloadClient 删除未使用的 HttpRequestMessage |
| R4-15 | ShellViewModel 实现 IDisposable，lambda 改命名方法，退订所有事件 |
| R4-09 | AuthService Token 刷新用 SemaphoreSlim 防竞态 + double-check |
| R4-22 | TokenRefreshBackgroundService Timer 回调防并发重叠 |

### 2026-04-16 — Batch 2（中等 Bug 修复）

**Commit**: `d86bc48` | **文件**: 4 | **测试**: 176/176 通过

| ID | 修复内容 |
|----|----------|
| R4-04 | DownloadOrchestrator.RecoverAsync 恢复逻辑重写 |
| R4-05 | DownloadOrchestrator Path.GetPathRoot null 引用防护 |
| R4-14 | App.xaml.cs StartPipeListener CancellationToken + 失败退避 |
| R4-16 | InstallationRepository.GetManifestPath 路径遍历防护 (OWASP A01) |
| R4-21 | DownloadTaskRepository.DeleteCheckpointAsync 包裹事务 |

### 2026-04-16 — Batch 3（中等 Bug 修复）

**Commit**: `791c3b9` | **文件**: 7 | **测试**: 176/176 通过

| ID | 修复内容 |
|----|----------|
| R4-06 | DownloadScheduler CTS 不再链接调用方 CT |
| R4-11 | FileTokenStore DateTime.Kind 反序列化修正（确保 UTC） |
| R4-17 | HashingService/IntegrityVerifier SemaphoreSlim 添加 using |
| R4-18 | InstallationRepository 同步 IO 改为 File.WriteAllTextAsync/ReadAllTextAsync |
| R4-24 | TrayIconManager.Dispose 释放 ContextMenuStrip |
| R4-25 | FabAssetCardViewModel 裸 catch 添加异常日志 |

### 2026-04-16 — Batch 4（性能 + 安全）

**Commit**: `9b6abbc` | **文件**: 5 | **测试**: 176/176 通过

| ID | 修复内容 |
|----|----------|
| R4-07 | SpeedCalculator Queue.Last() O(n) → O(1) 缓存最新样本 |
| R4-10 | EpicOAuthHandler 客户端凭据移至 appsettings.json |
| R4-12 | EpicOAuthHandler.WaitForCallbackAsync CT 统一用 cts.Token |
| R4-19 | RepositoryBase SQL 表名正则验证防注入 |
| R4-20 | SqliteConnectionFactory PRAGMA WAL 只执行一次 |

### 2026-04-16 — Batch 5（架构 + 质量）

**Commit**: `9289eda` | **文件**: 13 | **测试**: 176/176 通过

| ID | 修复内容 |
|----|----------|
| R1-02 | ShellViewModel.CanSkipUpdate 从 Visibility 改为 bool |
| R1-03 | EngineVersionsViewModel 引擎路径改用 IAppConfigProvider |
| R1-04 | FabAssetDetailViewModel 资产路径改用 IAppConfigProvider |
| R5-13 | 提取 AppConstants.AppName 替换 8 处硬编码 |
| R5-09 | DownloadOrchestrator 查询优化，新增仓储方法直接 SQL 过滤 |

### 2026-04-16 — Batch 6（代码质量）

**Commit**: `5c77777` | **文件**: 6 | **测试**: 176/176 通过

| ID | 修复内容 |
|----|----------|
| R5-01 | 提取 DownloadErrors 静态错误工厂（消除 10 处重复） |
| R5-03 | 提取 JsonDefaults 到 Shared 层（替换 3 处重复定义） |

### 2026-04-16 — 启动修复（Windows App Runtime + WinUI 启动链）

**分支**: `fix/windows-app-runtime-startup` | **修复提交**: `11ce2b9` | **合并提交**: `d8a1b15` | **测试**: 211/211 通过

| 类别 | 详细内容 |
|------|----------|
| 问题定位 | 原始症状是启动弹出“需要 Windows App Runtime 1.6”；排查后确认本机 1.6 Framework 包存在，但 1.6 对应的 Main / DDLM / Singleton 配套不完整，Bootstrapper 无法匹配完整 1.6 运行时集合 |
| 修复策略 | 仓库依赖从 `Microsoft.WindowsAppSDK 1.6.250205002` 升级到 `1.8.260317003`，与开发机上已完整安装的 1.8 运行时对齐 |
| 启动骨架修复 | `Program.cs` 补 `XamlCheckProcessRequirements()`；`App.xaml.cs` 补标准 `App()` 构造并调用 `InitializeComponent()`；`app.manifest` 补 `maxversiontested` |
| 根因修复 | `AppConfigProvider` 之前把 `appsettings.json` 里的空字符串路径当成有效路径，导致 `Paths:Logs = ""` 在启动期触发 `Directory.CreateDirectory("")` 异常，表面现象被包装成 WinUI/XAML 启动崩溃 |
| 文档同步 | `docs/16-WindowsAppRuntimeRepair.md` 更新为当前 1.8 目标版本与正式修复方案 |
| 验证结果 | `dotnet build HelsincyEpicLauncher.slnx` 通过；`runTests` 211/211 通过；应用窗口成功拉起，进程存活，主窗口标题为 `HelsincyEpicLauncher`，日志目录已创建 |

### 2026-04-16 — Settings 路径交互与响应式布局修复

**状态**: 工作区已实现 | **验证**: 211/211 通过

| 类别 | 详细内容 |
|------|----------|
| 用户体验 | 设置页的默认下载路径、默认安装路径、缓存路径新增“选择文件夹”按钮，用户不再只能手工输入 |
| 架构边界 | 文件夹选择器能力限制在 Presentation 层；Settings.Contracts 仍只传递路径字符串，未新增跨层 UI 耦合 |
| 组合根适配 | 由 App 组合根提供主窗口句柄适配给 Presentation 使用，避免 Presentation 直接依赖 App 内部窗口实现 |
| 保存链路 | 选择文件夹后仅更新 SettingsViewModel 页面状态，最终保存仍通过 `ISettingsCommandService.UpdatePathConfigAsync()` 进入既有配置持久化链路 |
| 异常处理 | 若系统文件夹选择器打开失败，则在 Settings 页面通过现有对话框服务反馈错误，并保留手动输入作为降级路径 |
| 布局问题根因 | SettingsPage 使用可横向滚动的 `ScrollViewer` 承载表单，当路径行变宽后，宽窗口场景会让内容获得错误的水平测量/滚动上下文，导致整块表单右移并发生裁切 |
| 布局修复策略 | 禁用横向滚动与横向滚动条，并把 Settings 主表单的内容宿主宽度显式钉到 `ScrollViewer` 当前视口宽度；同时保留 `MaxWidth=800` 作为可读宽度上限，避免在桌面宽屏下拉成过宽表单 |
| 设计原则 | 桌面设置表单的正确做法是响应式重排与宽度约束，不是把整页 UI 做位图式等比例缩放 |
| 验证结果 | `dotnet build HelsincyEpicLauncher.slnx` 通过；`runTests` 211/211 通过 |

### 2026-04-17 — Auth 登录重定向 URI 不匹配分析

**状态**: 已分析，暂未修复 | **影响**: 首次网页登录不可用

| 类别 | 详细内容 |
|------|----------|
| 用户现象 | 点击“登录 Epic Games”后会跳转到 Epic 网页，但浏览器直接显示“抱歉，您使用的重定向 URL 不可用于该客户端”错误页 |
| 运行时证据 | 本地日志 `app-20260417.log` 记录到 `OAuth 回调监听已启动 | RedirectUri=http://localhost:6780/callback`，3 分钟后 `ShellViewModel` 记录“未收到授权回调，登录已取消” |
| 代码证据 | `EpicOAuthHandler` 当前固定采用 `http://localhost:{6780-6784}/callback` 作为回调地址，并在授权 URL 与 token 交换请求中都传入该 `redirect_uri` |
| 根因判断 | 问题发生在 Epic 授权页阶段，说明当前 `ClientId=34a02cf8f4414e29b15921876da36f9a` 对应的客户端配置不接受当前 loopback 回调地址，或未将 `http://localhost:6780/callback` 这类 URI 加入允许列表，因此授权请求在回调前即被拒绝 |
| 诊断缺口 | 浏览器端已经明确给出“重定向 URL 不可用于该客户端”，但应用侧最终只会在超时后记录“未收到授权回调，登录已取消”，导致真实根因被误折叠为用户取消/超时 |
| 协议健壮性 | 当前授权 URL 仅包含 `client_id`、`response_type=code`、`redirect_uri`，未看到 `state`/PKCE 等桌面 OAuth 常见防护参数；即便后续修正 redirect URI，也建议一并评估登录链路的协议完整性 |
| 回调处理缺口 | `WaitForCallbackAsync()` 只读取查询参数中的 `code`，未处理 `error` / `error_description`，也未校验请求路径是否匹配 `/callback`，导致授权失败原因无法准确透传到应用层 |
| 文档漂移 | Auth 模块文档写明安全存储为 Windows Credential Locker，但当前实现仍是 `FileTokenStore + DPAPI 文件存储`，需要后续确认是文档超前还是实现滞后 |
| 配置缺口 | 当前仓库中的 `EpicOAuth` 配置只有 `ClientId` / `ClientSecret`，没有任何可配置的 `RedirectUri` 或回调白名单来源，说明代码目前只能“硬编码猜测”回调地址 |
| 回归判断 | 通过 git 历史检查，loopback 回调方案从 Auth 模块初始实现起就存在，并非本轮 Settings/布局改动引入的新回归 |
| 影响范围 | 首次网页登录链路不可用；若本地已存在有效 refresh token，则启动时会话恢复路径仍可能正常工作 |
| 组合结论 | 仅从当前仓库和本地日志可以确认“现有 `http://localhost:6780/callback` 不可用”，但无法可靠推导出该 `ClientId` 的正确允许 URI；正确组合必须回到 Epic 客户端注册信息或历史可用配置来源确认 |
| 后续修复方向 | 后续修复应优先核对 Epic 客户端注册的允许回调 URI，再决定是将回调地址改为配置驱动的固定白名单 URI，还是更换/调整与 loopback 回调兼容的客户端配置；修复前不应继续依赖“任意 localhost 端口都可用”的假设 |

### 2026-04-17 — Auth 登录链路预修复（配置化 + 协议校验）

**状态**: 已完成预修复，等待外部有效 RedirectUri | **验证**: 216/216 通过

| 类别 | 详细内容 |
|------|----------|
| 修复目标 | 在无法立即获得 Epic 客户端注册真值的前提下，先清理 Auth 登录链路中确定存在的实现问题，为后续切入正确 `redirect_uri` 做准备 |
| 回调策略 | `EpicOAuthHandler` 不再轮询 `6780-6784` 猜测端口，改为读取固定配置的 `EpicOAuth:RedirectUri`；默认值仍为 `http://localhost:6780/callback` |
| 配置能力 | `appsettings.json` 新增 `EpicOAuth:RedirectUri` 配置项，后续拿到正确 URI 后只需修改配置即可切换，不必再改 Handler 代码 |
| 协议健壮性 | 授权 URL 新增 `state` 参数；回调处理补充 `state` 校验、路径校验，以及 `error` / `error_description` 解析 |
| 诊断改进 | 登录链路超时、错误回调、路径错误、配置错误、监听端口占用等情况均会返回更准确的结构化错误码，而不是统一折叠为“登录取消” |
| 测试补充 | 新增 `EpicOAuthProtocolTests`，覆盖授权 URL 组装、成功回调、Provider 错误、`state` 不匹配、回调路径错误等场景 |
| 当前限制 | 由于外部 Epic 客户端注册信息仍未知，当前预修复不能单凭仓库内代码保证网页登录立即恢复；后续仍需填入已验证可用的 `redirect_uri` |
| 验证结果 | `dotnet build HelsincyEpicLauncher.slnx` 通过；`runTests` 216/216 通过 |

### 2026-04-17 — Auth 正式修复方案拆分与验证矩阵

**状态**: 已完成计划拆分，待执行 | **实施文档**: [11-AuthLoginRepairPlan.md](11-AuthLoginRepairPlan.md)

| 类别 | 详细内容 |
|------|----------|
| 方案结论 | 正式修复继续以“系统浏览器 + 固定、已验证的 `redirect_uri`”为主链路，不把 SID、第三方辅助站点或外部 CLI 依赖纳入主方案 |
| 任务拆分 | 新增 `docs/review/11-AuthLoginRepairPlan.md`，按上下文限制拆成 4 个 Phase、10 个小任务；默认尽量只改 Auth，只有手动 code 回退入口属于显式契约升级 |
| 架构约束 | 方案明确受 02/04/12/14 与 Auth 模块文档约束：禁止 UI 写业务、禁止新增万能 Auth 服务、禁止跨模块直接依赖 Auth 内部实现 |
| 决策闸门 | 修复前必须先确认 Epic 客户端真实允许的 `redirect_uri`；若结果不是 loopback HTTP + 显式端口，则先在 Auth 内部处理回调策略兼容性，不把协议细节扩散到 Shell/Settings |
| 主链路修复 | 若拿到可用 loopback URI，优先只切换配置并补足 Auth 自动化验证，再做人工登录、重启恢复、登出清理验收 |
| 回退策略 | 只有当主链路仍受外部环境影响时，才增加“手动导入 authorization code”作为第二入口；该入口仍由 `IAuthService` 负责会话建立 |
| 验证矩阵 | 验证矩阵覆盖配置错误、监听拒绝、Provider 错误、`state` / 路径校验、首次登录成功、重启恢复、登出清理，以及条件性的手动 code 回退成功/失败 |
| 文档同步 | 后续如升级 `IAuthService`，必须同步更新 `docs/05-CoreInterfaces.md`、`docs/06-ModuleDefinitions/Auth.md` 和 `docs/review/99-ReviewLog.md` |

### 2026-04-17 — P0.1 外部 redirect 真值确认 + P0.2 分支锁定

**状态**: 已完成分析，代码尚未开始 | **结论**: 当前分支不再继续 loopback 配置切换

| 类别 | 详细内容 |
|------|----------|
| 仓库证据 | 代码库中只能确认当前 `http://localhost:6780/callback` 不可用；git 历史未发现任何第二个已验证可用的 loopback `redirect_uri` |
| 历史来源 | `EpicOAuth:RedirectUri` 是 2026-04-16 预修复阶段才写入 `appsettings.json` 的默认配置；更早的 Auth 实现依赖代码里 `6780-6784` 端口轮询猜测 |
| 外部参考 | `legendary` 对同一组客户端凭据 `34a02cf8f4414e29b15921876da36f9a` / `daafbccc737745039dffe53d94fc76cf` 的交互式登录入口，使用 `https://www.epicgames.com/id/login?redirectUrl=https://www.epicgames.com/id/api/redirect?clientId=34a02cf8f4414e29b15921876da36f9a&responseType=code` |
| 跳转验证 | `https://legendary.gl/epiclogin` 实际也会跳转到相同的 Epic HTTPS 重定向链路，而不是 localhost 回调 |
| 根因收束 | 说明当前这组客户端凭据的成熟外部用法并不是“浏览器 -> localhost 回调”，所以继续替换 localhost 端口或路径没有证据基础 |
| P0.2 决策 | 由于当前确认到的有效链路不是 loopback HTTP + 显式端口，正式修复路线切换为：先在 Auth 模块内部设计非 loopback 浏览器结果接收策略，再做代码实现 |
| 后续动作 | 下一步任务不再是修改 `EpicOAuth:RedirectUri`，而是设计最小范围的 Auth 内部策略改造，并优先评估“手动 authorization code 导入”是否可作为主恢复路径或第一回退路径 |

### 2026-04-17 — 非 loopback 登录最小策略设计

**状态**: 已完成设计，待实现 | **定位**: P0 后续设计收束

| 类别 | 详细内容 |
|------|----------|
| 策略结论 | 选择“两步式 authorization code 导入”作为当前 clientId 的最小恢复路线，不引入 WebView2、App 自定义协议注册或第三方 CLI 依赖 |
| 交互形态 | 保留 Shell 现有登录按钮；点击后由 Auth 打开 Epic 登录页，再由 Presentation 通过输入对话框收集 authorization code 或完整 JSON 响应 |
| 边界控制 | UI 只负责显示提示和收集文本输入；authorization code 提取、token 交换、账户信息加载、会话保存全部留在 Auth 模块内部 |
| 契约方向 | 建议把当前单步 `LoginAsync()` 升级为两步式窄接口，避免让 UI 感知 OAuth 协议细节，也避免继续维持一个已经与当前 clientId 不匹配的单步 loopback 契约 |
| 实现落点 | 主要改动集中在 `IAuthService`、`AuthService`、`EpicOAuthHandler`、`IDialogService`、`DialogService`、`ShellViewModel`，预计不需要改 `ShellPage.xaml` 结构 |
| 协议注意 | 当前仓库交互式入口是 `id/authorize + redirect_uri`，而外部成熟链路更接近 `id/login?redirectUrl=.../id/api/redirect?...`；两者不应在实现时继续混用为同一种 code exchange 逻辑 |
| 测试方向 | 后续实现应新增：authorization code 输入解析测试、两步式登录编排测试、输入取消测试、错误 code 失败测试 |

### 2026-04-17 — Auth 宿主第二实例自动回调转发运行态修复

**状态**: 已完成并通过运行态验收 | **测试**: 226/226 通过 | **运行态结论**: 主实例已可自动消费第二实例转发的回调 URL 候选负载

| 类别 | 详细内容 |
|------|----------|
| 初始现象 | 手动运行第二实例并传入假的回调 URL 时，主日志只出现“收到第二实例的激活请求”，没有进入自动回调消费链路 |
| 第一层根因 | 原宿主实现把第二实例参数解析放在 `App.OnLaunched(...)` 阶段，当前 WinUI/宿主组合下该阶段并不可靠地携带第二实例启动参数 |
| 第二层根因 | 运行态排查中还确认了一个容易误判的问题：`dotnet test` 不会构建 `Launcher.App.csproj`，因此如果只跑测试不显式构建 App，可执行文件会继续停留在旧版本，导致运行态结论失真 |
| 正式修复 | 新增 `SingleInstanceCoordinator`，把单实例判定与第二实例命名管道转发前移到 `Program.Main`；第二实例参数解析统一改为 `Main(args)` → 原始命令行 → `Environment.GetCommandLineArgs()` 三层兜底 |
| 宿主职责 | `App.xaml.cs` 现在只负责主实例的命名管道监听、回调候选负载入队和现有 Auth 完成登录链路调度，不再承担第二实例的单实例判定 |
| 运行态验证 | 在显式执行 `dotnet build src/Launcher.App/Launcher.App.csproj` 后，使用第二实例传入假的 `https://localhost/auth/callback?code=...`，主日志已出现“收到第二实例转发的认证回调候选负载，激活主窗口并尝试自动完成登录” |
| 终态证据 | 同一轮日志继续出现“提交 authorization code/回调链接，完成登录流程”→“开始使用 authorization code 完成登录”→“Token 交换失败（fake code not found）”，说明宿主自动回调链路已真正打通，失败只来自故意使用的伪造授权码 |
| 操作约束 | 后续凡是改动 `src/Launcher.App/*` 且要做真实运行态验收时，必须额外执行 `dotnet build src/Launcher.App/Launcher.App.csproj`，不能只依赖 `dotnet test` |

### 2026-04-17 — 两步式 authorization code 登录实现

**状态**: 已完成实现 | **验证**: `dotnet build` 通过，`runTests` 220/220 通过

| 类别 | 详细内容 |
|------|----------|
| 契约升级 | `IAuthService` 从单步 `LoginAsync()` 升级为两步式 `StartAuthorizationCodeLoginAsync()` + `CompleteAuthorizationCodeLoginAsync()` |
| Auth 实现 | `AuthService` 现在分离“打开 Epic 登录页”和“提交 authorization code 完成登录”两段流程；会话保存、账户信息加载、登出、恢复链路保持在 Auth 内部 |
| 协议处理 | `EpicOAuthProtocol` 新增 Epic 登录 URL 组装和用户输入 authorization code 提取能力；允许用户粘贴纯 code 或完整 JSON 响应 |
| 交互入口 | `EpicOAuthHandler` 新增非 loopback 浏览器登录入口，并为 authorization code 导入流单独执行 token 交换；该流不再强行绑定 loopback `redirect_uri` |
| Shell 交互 | Shell 保留原有登录按钮，不新增常驻输入框；点击后由对话框服务收集 authorization code 文本，ViewModel 仅负责触发和显示错误，不解析协议字段 |
| 对话框能力 | `IDialogService` / `DialogService` 新增窄文本输入能力，避免回落到未实现的泛型自定义弹窗 |
| 文档同步 | 已同步更新 `docs/05-CoreInterfaces.md` 和 `docs/06-ModuleDefinitions/Auth.md`，避免接口文档与实现再次漂移 |
| 测试补充 | `EpicOAuthProtocolTests` 新增 Epic 登录 URL 组装、plain code 提取、JSON code 提取、JSON 缺失字段失败等测试 |
| 架构检查 | 本次改动仍遵守边界：Presentation 不直接依赖 Auth 内部实现，Auth 协议细节没有泄漏到 Shell / Settings / App，未新增万能服务 |

### 2026-04-17 — authorization code 输入与错误处理加固

**状态**: 已完成实现 | **目标**: 缩短人工登录链路的排错时间并提高输入兼容性

| 类别 | 详细内容 |
|------|----------|
| 输入兼容 | `EpicOAuthProtocol` 现在除 plain code / 完整 JSON 外，还支持从 `redirectUrl` 字段或直接粘贴的完整回调 URL 中提取 `code` 参数 |
| 用户提示 | Shell 登录对话框改为优先提示用户粘贴 `authorizationCode`，并明确说明授权码一次性且可能很快失效 |
| 失败映射 | `EpicOAuthHandler` 对 token 交换阶段的 provider 返回体做最小解析；遇到 `authorization_code_not_found` / `invalid_grant` 时，不再统一提示“登录授权失败”，而是明确提示用户重新获取新的授权码 |
| 诊断日志 | token 刷新、token 交换、账户信息获取失败日志现在会携带脱敏后的响应体，便于继续定位 Epic 服务端拒绝原因 |
| 测试补充 | 新增 `redirectUrl` / 完整 URL 提取授权码测试，防止未来回退到只支持单一输入形态 |

### 2026-04-17 — Auth 手动 JSON 输入止血

**状态**: 已完成实现 | **目标**: 把完整响应内容从默认登录路径移出，降低敏感字段暴露面

| 类别 | 详细内容 |
|------|----------|
| 风险收束 | 当前版本不再把“粘贴完整 JSON 响应”作为默认登录交互；主按钮仅负责发起浏览器登录 |
| Shell 交互 | `ShellViewModel` 新增显式“继续登录（导入授权码）”次级动作，用户只有在需要人工继续时才打开文本输入框 |
| 输入边界 | `EpicOAuthProtocol` 不再接受完整 JSON 负载；人工继续登录只接受纯 `authorizationCode` 或完整回调 URL |
| 失败提示 | 若用户仍粘贴整段 JSON，返回明确错误消息，提示只粘贴授权码或回调链接 |
| 架构边界 | 这次止血没有把 OAuth 协议细节继续扩散到页面结构之外；Shell 仍只做发起/收集，Auth 仍负责解析与 token 交换 |
| 后续方向 | 当前只是把高风险默认交互降级；真正的终点仍是自动回调正式方案，把人工导入彻底降为兜底 |

### 2026-04-17 — Auth 自动回调预研与 loopback 验证清单

**状态**: 已完成预研，待外部确认 | **结论**: 当前最现实的自动回调路径仍然是拿到 Epic 真实可用的 loopback `redirect_uri`

| 类别 | 详细内容 |
|------|----------|
| 宿主现状 | `Launcher.App` 当前为未打包宿主：`WindowsPackageType=None` + `app.manifest`，仓库内无 `Package.appxmanifest`，也没有协议激活入口 |
| 代码现状 | Auth 现成自动回调实现只有 loopback：`HttpListener` + 固定 `RedirectUri` + `state` 校验 + 回调 path 校验 |
| 排除项 | 源码层未实现 WebAuthenticationBroker、协议激活或 WebView2 登录容器；构建产物中的 WebView2 相关文件不足以说明仓库已经具备可用的嵌入登录实现 |
| 推荐顺序 | 先确认 Epic 是否给当前 clientId 提供精确可用的 loopback `redirect_uri`；若给出可用值，优先直接恢复 loopback 自动回调；若彻底不支持，再评估宿主级非 loopback 回调方案 |
| 风险判断 | 在未拿到精确外部真值前，直接投入自定义协议或 WebView2 改造都会扩大实现面，并可能建立在错误前提上 |
| 交付物 | 已在 `docs/review/11-AuthLoginRepairPlan.md` 的 P0.1 下补充一份可直接对外确认的 loopback 验证清单，覆盖 `redirect_uri` 精确值、host/port/path、authorize 入口形式、token exchange 参数矩阵和 code 返回字段 |

### 2026-04-17 — Auth 宿主自动回调骨架接入

**状态**: 已完成实现 | **验证**: `dotnet test` 226/226 通过

| 类别 | 详细内容 |
|------|----------|
| 宿主改动 | `Launcher.App` 现可从首实例启动参数和第二实例命名管道消息中识别“完整回调 URL 候选负载”，并自动转交 `IAuthService.CompleteAuthorizationCodeLoginAsync()` |
| 单实例协同 | 原先第二实例只会发送 `ACTIVATE`；现在若启动参数看起来像回调 URL，则会以命名管道消息转发给已运行实例，再由主实例自动尝试完成登录 |
| 架构边界 | 应用宿主只负责接收和转发原始回调负载；authorization code 提取、token 交换、会话建立仍完全留在 Auth 模块内部 |
| 价值定位 | 这一步没有假设最终一定是 loopback 还是自定义协议；它只是提前把“应用内部自动消费外部回调”的骨架打通，避免后续拿到真实 redirect 方案后还要再返工宿主链路 |
| 剩余缺口 | 当前宿主骨架已经能消费外部回调 URL，但系统级来源仍未最终打通；还需要外部确认真实可用的 loopback `redirect_uri`，或者在 loopback 被否决后继续实现非 loopback 来源接入 |

### 2026-04-17 — Auth loopback 外部确认模板

**状态**: 已完成整理 | **输出**: [12-AuthRedirectInquiryTemplate.md](12-AuthRedirectInquiryTemplate.md)

| 类别 | 详细内容 |
|------|----------|
| 输出形式 | 新增一份可直接发送给 Epic/外部维护方的英文确认模板，不再只停留在内部检查项 |
| 模板内容 | 覆盖发送前需准备的证据、外发正文、补充追问项，以及收到回复后的判定规则 |
| 目标 | 逼出精确完整的 `redirect_uri`、authorize 入口形式和 token exchange 参数矩阵，避免再次建立在模糊答复上推进实现 |

### 2026-04-17 — 手动授权码入口进一步降级

**状态**: 已完成实现 | **目标**: 让手动导入明确回到“高级兜底动作”而不是普通次级按钮

| 类别 | 详细内容 |
|------|----------|
| UI 暴露面 | `ShellPage` 不再把手动继续登录显示成完整按钮，而是改为低显著度的“高级：手动继续登录”链接 |
| 误用控制 | 用户点击高级链接后，先经过一次确认，再进入授权码/回调链接输入框 |
| 交互语义 | 登录主按钮仍只负责正常浏览器登录；手动导入进一步收敛为“自动回调未生效时的兜底路径” |

### 2026-04-17 — Fab/引擎版本网页端接口误接入根因确认

**状态**: 根因已确认，已完成错误识别修补 | **影响**: Fab 在线目录暂不可用；引擎版本旧网页端入口同样不可靠

| 类别 | 详细内容 |
|------|----------|
| 运行时证据 | Fab 搜索 403 的响应体不是业务 JSON，而是 `www.fab.com` 返回的 Cloudflare `Just a moment...` 挑战页；说明请求根本没有到达真实目录后端 |
| 结论收束 | 当前 `https://www.fab.com/api/v1/assets/*` 是网页站点受保护入口，不是适合桌面客户端直连的稳定服务 API；登录成功并不等于可以直接访问该入口 |
| 扩展发现 | `https://www.unrealengine.com/api/engine/versions` 也表现为同类站点口问题；而同一 access token 调用 `launcher-public-service-prod06.ol.epicgames.com` 的官方 launcher 服务端点可以返回 200，证明 token 本身有效 |
| 立即修补 | `FabApiClient` / `EngineVersionApiClient` 新增网站挑战页识别逻辑；命中 Cloudflare challenge 时不再把问题误报为普通 403，而是明确提示“网页登录成功，但当前客户端仍在访问网页端受保护入口” |
| 后续方向 | Fab 在线目录需要切换到 Epic 可供客户端使用的后端服务链路；在找到正式目录接口前，当前版本只能做清晰降级，不能再把 `www.fab.com/api` 当作可持续方案 |

### 2026-04-17 — Fab owned 回退收敛到统一流式详情/分页链路

**状态**: 已完成代码收敛，分页运行时已验证 | **验证**: `dotnet build` 通过，`dotnet test` 226/226 通过

| 类别 | 详细内容 |
|------|----------|
| 架构约束 | 先重新核对 `02-ArchitecturePrinciples.md`、`04-ModuleDependencyRules.md`、`14-AntiPatterns.md` 与 `06-ModuleDefinitions/FabLibrary.md`，确保改动继续局限在 FabLibrary 既有边界内，不把业务逻辑塞回页面 Code-Behind，也不新增万能回退服务 |
| 核心重构 | `EpicOwnedFabCatalogClient` 由“首屏预览 + 详情/全量独立路径”改为 requirement-driven owned 记录提供器；搜索分页、详情读取、owned 全量枚举统一通过 `EnsureOwnedRecordsAsync()` 扩展同一份缓存 |
| 分页语义 | 搜索后续页不再强制等待 owned 全量枚举完成，而是按 `Page * PageSize` 逐步扩展预览窗口；当响应仍有后续数据时，用近似 `TotalCount` 保持 `PagedResult.HasNextPage` 继续成立 |
| 详情语义 | 详情回退不再走独立的“全量 owned 列表先拉完”路径，而是按 `assetId` 需求在流式预览中命中目标资产后立即进入 catalog 富化 |
| Presentation 配合 | `FabAssetDetailViewModel` 补充 `AUTH_NOT_AUTHENTICATED` 软处理，避免会话恢复前把详情页误报为加载失败 |
| 自动化验证 | 新增单测覆盖两条关键回归：一条验证 `FabCatalogReadService` 在网站 challenge 时会把详情请求切到 owned fallback；一条验证 `EpicOwnedFabCatalogClient` 会随着页码提升逐步扩大预览窗口，而不是退回旧的独立路径 |
| 运行时验证 | 最新应用日志已验证 Fab 回退分页在 page 1~5 上按需扩展：20 → 40 → 60 → 144；`Fab 页面初始化完成：20 个资产，1 个分类` 与后续 `Fab Epic 回退搜索完成` 日志均正常出现 |
| 真实链路补充验证 | 由于当前环境中的 `Launcher.App` 未暴露可用 UIA 顶级窗口，无法做自动化点击；改为复用当前本机会话直接调用应用自身 `IFabCatalogReadService`，实测成功完成 owned 搜索→详情读取，命中资产 `3e9c264b685f43edabb1bcb000a2330d`（`Modular Scifi Season 2 Starter Bundle`），返回了标签和兼容引擎版本 |
| 剩余缺口 | 详情后端链路已通过真实会话验证，但详情页的最终视觉/UI 呈现仍需在可交互桌面环境中手动点开确认 |

### 2026-04-17 — 基于 Legendary 参考实现的 Auth 下一阶段设计定稿

**状态**: 已完成文档设计，未开始实现 | **输出**: [13-LegendaryAuthReferenceDesign.md](13-LegendaryAuthReferenceDesign.md)

| 类别 | 详细内容 |
|------|----------|
| 设计依据 | 本轮重新对齐了 `02-ArchitecturePrinciples.md`、`04-ModuleDependencyRules.md`、`12-AICollaboration.md`、`14-AntiPatterns.md` 以及 Auth / Shell 模块文档，再把 Legendary 的实际登录实现映射到本仓库边界 |
| 外部事实 | Legendary 的成熟登录链路证明：当前 clientId 的交互式主线更接近 Epic 托管登录页 + Epic HTTPS 重定向端点，而不是 localhost loopback 回调 |
| 方案结论 | 当前仓库不应继续把 loopback 当成唯一设计中心；下一阶段应保留已上线的 authorization code 兜底链路，并先在 Auth 内部补上“登录结果归一化 + grant 执行策略”抽象 |
| 边界控制 | 设计明确要求 Shell 只负责触发和收集输入、App 只负责转发候选回调负载、Auth 负责归一处理和 token exchange；不新增万能 Auth 管理器，不把协议细节泄漏到页面或宿主 |
| 契约策略 | 近期不建议立刻重命名 `IAuthService`；仅在真正进入 `exchange_code` 或 EGL refresh token 导入实现时，才升级为窄 DTO 契约 |
| 日志要求 | 文档补充了分层日志边界、推荐事件名和脱敏规则，要求后续实现必须记录“来源 / 输入类型 / grant / 结果”，同时禁止记录原始 code、完整回调 URL 和 token |
| 下一步 | 若继续实现，优先执行 Phase L1：仅在 Auth 内部引入 completion kind / grant executor 与结构化日志归一；WebView2 exchange code 和 EGL 导入都应作为后续独立立项选择 |

### 2026-04-17 — Auth Phase L1：内部 completion 抽象与结构化日志归一

**状态**: 已完成实现 | **验证**: `dotnet test HelsincyEpicLauncher.slnx --no-restore` 230/230 通过（另有 1 条既有 `CA1816` 警告）

| 类别 | 详细内容 |
|------|----------|
| 内部模型 | `Launcher.Infrastructure.Auth` 新增 `EpicLoginResultKind` / `EpicLoginResult`，把手工 authorization code、完整回调 URL、loopback callback 统一归一成 Auth 内部结果对象 |
| grant 执行器 | 新增 `IEpicLoginGrantExecutor` 与 `AuthorizationCodeGrantExecutor`；当前先落 `authorization_code` 一条执行器，但结构上已经为后续 `exchange_code` 和外部 refresh token 导入预留位置 |
| 主链路收敛 | `EpicOAuthHandler` 不再直接在方法里手写授权码换 token，而是先记录归一结果，再匹配 grant 执行器；loopback callback 与手工兜底路径现在共享同一条 token exchange 执行逻辑 |
| 契约控制 | `IAuthService` 未发生公共签名变化；Shell / App 无需适配，符合“先在 Auth 内部收口、暂不扩大跨模块契约”的设计目标 |
| 日志归一 | Auth 层新增结构化日志：登录结果归一日志记录 `Kind` / `Source` / `IncludeTokenType` / `HasRedirectUri`；grant 执行器记录 token exchange 的 started / failed / succeeded，并保持 provider 错误体脱敏 |
| 测试补充 | `EpicOAuthProtocolTests` 新增归一化结果类型断言；新增 `AuthorizationCodeGrantExecutorTests`，验证普通 authorization code 输入会携带 `token_type=eg1`，而 loopback callback 保留 `redirect_uri` 且不附带 `token_type=eg1` |
| 测试辅助修补 | 为 `MockHttpMessageHandler` 增加请求体快照，避免新单测因 `FormUrlEncodedContent` 在发送后释放而误判失败 |
| 下一步 | 当前 Phase L1 已完成，下一轮应由用户在 `WebView2 exchange code` 预研与 `EGL refresh token` 导入预研之间做选择；若只是整理当前状态，也可以先提交这一轮改动 |

### 2026-04-17 — EGL refresh token 导入预研

**状态**: 已完成设计预研，未开始实现 | **输出**: [14-EGLRefreshTokenImportResearch.md](14-EGLRefreshTokenImportResearch.md)

| 类别 | 详细内容 |
|------|----------|
| 外部事实 | Legendary 的 `auth_import()` 不是复用 bearer token，而是从 `%LOCALAPPDATA%\EpicGamesLauncher\Saved\Config\Windows\GameUserSettings.ini` 的 `[RememberMe]` / `Data` 读取 refresh token 来源，再调用标准 `grant_type=refresh_token` + `token_type=eg1` 建会话 |
| 兼容性判断 | 本仓库当前 Auth Phase L1 已有 `EpicLoginResultKind.ExternalRefreshToken` 这一扩展点，因此从结构上完全可以把 EGL 导入作为新的高级登录来源接入，而不必再改回大而杂的 Handler |
| 产品风险 | Legendary 文档明确提示导入会把 EGL 登出，因此该路径不能作为默认登录按钮，只能是明确带风险告知的高级入口 |
| 合规风险 | Legendary 的 EGL 解密实现位于 GPL 代码库中；若后续要支持加密 RememberMe 数据，必须用 .NET 标准密码学库写独立实现，不能直接复制其 `egl_crypt.py` 或常量表 |
| 本机证据 | 本轮对当前机器做了只读存在性检查；`%LOCALAPPDATA%\EpicGamesLauncher\Saved\Config\Windows\GameUserSettings.ini` 当前为 `MISSING`，因此只能完成静态预研，不能做真实导入验收 |
| 架构边界 | 预研结论要求：读取 EGL RememberMe 的逻辑留在 `Launcher.Infrastructure.Auth`，Shell 只负责确认提示和触发；不应把路径配置暴露到 Settings UI，也不应让 App 宿主参与协议处理 |
| 下一步 | 真要开始实现时，建议先做合规与样本准备，再落 `EpicLauncherRememberMeReader` 与 `ExternalRefreshTokenGrantExecutor`；如果暂时不想处理解密/许可证风险，则下一步更适合优先推进 WebView2 exchange code |

### 2026-04-20 — WebView2 exchange code 预研

**状态**: 已完成设计预研，未开始实现 | **输出**: [15-WebView2ExchangeCodeResearch.md](15-WebView2ExchangeCodeResearch.md)

| 类别 | 详细内容 |
|------|----------|
| 外部事实 | Legendary 的 WebView 登录是 optional capability，不是主前提；无 WebView 或显式 `--disable-webview` 时，会回退到系统浏览器并让用户手工复制 `authorizationCode` |
| 协议结论 | Legendary 的嵌入式登录不是等待 loopback 回调，而是通过页面桥接直接拿 `exchange_code`，随后调用标准 `grant_type=exchange_code` + `token_type=eg1` 完成登录 |
| 本仓库兼容性 | 当前仓库已经有可工作的系统浏览器 + authorization code 兜底链路，因此 WebView2 的价值是“压缩人工步骤”，而不是“补齐底层登录能力” |
| 基建现状 | 仓库里当前没有 `Microsoft.Web.WebView2` 依赖、`WebView2` 控件封装或现成的嵌入登录容器，因此若立项，必然是新的跨模块契约变更任务 |
| 架构边界 | 推荐把 WebView2 容器留在 Presentation，只负责收集 `exchange_code`；grant 选择、token exchange、会话保存继续留在 Auth，且应按既有设计把 completion 输入升级为窄 DTO |
| 主要风险 | Epic 页面桥接点可能漂移，嵌入浏览器中的验证码/cookie 行为可能不同；Legendary 还会在 Windows 上先走 logout URL 处理会话，说明该路径存在运行态副作用风险 |
| 下一步 | 若要继续推进，不应直接大改主线，而应先做 WinUI 3 WebView2 最小 POC，验证在合适 User-Agent 下能否稳定拿到 `exchange_code`；若 POC 不稳，应立即止损并继续沿用现有系统浏览器路径 |

### 2026-04-20 — WebView2 exchange code 默认登录实现

**状态**: 已完成实现，待真实 Epic 运行态验收 | **验证**: `dotnet test HelsincyEpicLauncher.slnx --no-restore` 232/232 通过；`dotnet build src/Launcher.App/Launcher.App.csproj --no-restore` 通过

| 类别 | 详细内容 |
|------|----------|
| 契约升级 | `IAuthService` 新增 `StartExchangeCodeLoginAsync()` 与 `CompleteLoginAsync(AuthLoginCompletionInput)`；原有 `StartAuthorizationCodeLoginAsync()` / `CompleteAuthorizationCodeLoginAsync()` 保留为系统浏览器兜底链路 |
| Auth 结构 | 在 `Launcher.Infrastructure.Auth` 内新增 `ExchangeCodeGrantExecutor`，通过标准 `grant_type=exchange_code` + `token_type=eg1` 完成 token exchange；`EpicOAuthHandler` 现可返回嵌入式登录上下文并执行类型化 completion 输入 |
| Shell / Presentation | `DialogService` 现在可承载嵌入式 Epic 登录对话框，在 WebView2 中注入最小桥接并接收 `exchange_code`；Shell 默认登录先走嵌入式自动链路，失败时再自动回退到系统浏览器 |
| 安全边界 | UI 侧只承载浏览器和原始 `exchange_code` 回传，不做 token exchange、不缓存 token；Auth 仍负责 grant 选择、会话保存和账户信息加载 |
| 回退策略 | 若 WebView2 初始化失败、页面加载失败或 `exchange_code` 完成登录失败，Shell 会自动切换到现有系统浏览器链路，并只在必要时保留高级“手动继续登录”入口 |
| 测试补充 | 新增 `ExchangeCodeGrantExecutorTests`，并扩展 `EpicOAuthProtocolTests` 覆盖嵌入式登录 URL；整仓库回归测试现为 232/232 通过 |
| 剩余风险 | 当前只完成了编译和测试级验证，尚未在真实 Epic 登录页上手动验收 WinUI 3 WebView2 是否稳定捕获 `exchange_code`，因此运行态真实性仍需单独确认 |

### 2026-04-21 — WebView2 默认登录主线加固

**状态**: 已完成实现级加固，待真实 Epic 运行态验收 | **验证**: `dotnet test HelsincyEpicLauncher.slnx --no-restore` 242/242 通过；`dotnet build src/Launcher.App/Launcher.App.csproj --no-restore` 通过

| 类别 | 详细内容 |
|------|----------|
| 信任边界 | `DialogService` 现在只接受来自受信任 Epic HTTPS 页面上下文的 WebView2 桥接消息，并拒绝打开非 Epic 域名的嵌入式登录外链，避免默认登录主线对任意文档来源过度信任 |
| 会话隔离 | 嵌入式登录现在使用独立 WebView2 用户数据目录，并在初始化后尽力清理浏览数据，避免默认登录主线复用残留 cookie/profile 带来不稳定的账号切换和会话污染 |
| 线程安全 | `CancellationToken` 驱动的登录取消现在通过 UI `DispatcherQueue` 回到对话框线程再执行关闭逻辑，避免后续真正接入超时或应用退出时发生跨线程关闭对话框 |
| 契约防御 | `AuthService.CompleteLoginAsync(...)` 现在对空 completion 输入先返回结构化错误，而不是在记录日志前直接解引用 `input.Kind` |
| 测试补充 | 新增 `EpicLoginWebViewBridgeTests` 覆盖 Epic 域名信任与桥接消息解析；整仓库测试总数已从 232 提升到 242，全部通过 |
| 剩余风险 | 当前仍然缺少真实 Epic 登录页运行态证据，尚未证明 Epic 页面桥接点在 WinUI 3 WebView2 中长期稳定，因此主风险已从“本地实现边界过宽”收敛为“外部页面行为是否稳定” |

### 2026-04-22 — WebView2 运行态根因与浏览器 JSON 兜底修正

**状态**: 已完成代码修正，待重新做真实运行态验收 | **运行态结论**: 当前卡点不是单一的“登录页一直加载”，而是 `WebView2Loader.dll` 输出缺失与浏览器 JSON 兜底不兼容两个问题叠加

| 类别 | 详细内容 |
|------|----------|
| 运行态证据 | 实际点击登录后，主日志先记录 `Auth login started | Mode=embedded_webview_exchange_code` 与“显示 Epic 嵌入式登录对话框”，随后 `DialogService` 在 `CoreWebView2Environment.CreateWithOptionsAsync(...)` 处抛出 `System.IO.FileNotFoundException: 找不到指定的模块`，紧接着 `ShellViewModel` 记录“嵌入式登录窗口失败”并切换到系统浏览器兜底 |
| 第一层根因 | 默认启动目录 `src/Launcher.App/bin/Debug/net9.0-windows10.0.19041.0` 根层没有 `WebView2Loader.dll`；该文件只存在于 `runtimes/win-x64/native` 和 `win-x64` 子目录中，因此直接启动默认输出时 WebView2 无法初始化 |
| 构建修正 | `Launcher.App.csproj` 现显式引用 `Microsoft.Web.WebView2`，并在 Build / Publish 后把 `runtimes/win-x64/native/WebView2Loader.dll` 复制到真实应用基目录 `TargetDir` / `PublishDir`，不再错误使用相对 `OutDir` |
| 第二层根因 | 浏览器兜底链路打开的仍是 `id/login?redirectUrl=.../id/api/redirect?...`，该链路在当前 clientId 下会把用户带到 Epic 返回的 JSON 响应页；应用此前又刻意禁止整段 JSON 输入，导致用户即使完成网页登录，回到高级继续入口后也会因为输入整段 JSON 而被拒绝 |
| 协议修正 | `EpicOAuthProtocol` 现恢复最小 JSON 兼容：若高级继续入口收到 JSON 响应，只提取 `authorizationCode`、`code` 或 `redirectUrl` 中可用的授权码，不记录原始 JSON，也不把其他字段暴露到日志 |
| 交互修正 | `ShellViewModel` 的高级继续登录提示已同步为“优先粘贴 `authorizationCode` / 回调 URL；若浏览器只显示 JSON 响应，也可临时整段粘贴，由应用只提取必需字段” |
| 测试补充 | `EpicOAuthProtocolTests` 已把 JSON 场景从“必然失败”改为“提取 `authorizationCode` / `redirectUrl` 成功”，同时保留缺失关键字段时的失败断言 |
| 当前剩余风险 | 本轮尚未完成修正后的第二次真实运行态验收；仍需确认默认输出目录下 WebView2 初始化是否恢复，以及若仍进入浏览器兜底时，高级继续入口是否已能用 JSON 响应闭环登录 |

### 2026-04-22 — 嵌入式登录页面裁切修正

**状态**: 已完成代码修正并完成真实运行态闭环验收 | **运行态结论**: 仅放宽 `ContentDialog` 宽度不足以稳定承载 Epic 的人机验证页面；根因级修复是把嵌入式登录容器改为独立可调整大小的窗口，避免 Arkose/验证码继续被宿主裁切

| 类别 | 详细内容 |
|------|----------|
| 运行态证据 | 修复 loader 缺失后，实际运行已能弹出嵌入式 Epic 登录页；但即使放宽 `ContentDialog` 宽度，用户在继续到人机验证页面时仍反馈“页面还是被裁剪了，导致无法通过验证”，说明问题不只是默认模板宽度，而是 `ContentDialog` 作为宿主本身不适合这一类登录流程 |
| 根因 | `ContentDialog` 仍然受限于主窗口内部可用区域与自身模板布局，用户也无法像普通窗口一样自由拉伸或最大化；对 Epic 后续的人机验证页来说，这种宿主形式不稳定，仍会出现裁切 |
| UI 修正 | `DialogService` 现不再使用 `ContentDialog` 承载 Epic 登录，而是创建独立的 WinUI 3 登录窗口，按显示器工作区计算初始大小，允许用户直接调整或最大化窗口，再在其中承载 WebView2 与取消按钮 |
| 验证结果 | `dotnet build src/Launcher.App/Launcher.App.csproj --no-restore` 重新通过；用户在新一轮运行态复验中确认“已完整可见并可操作”，随后继续登录并确认“登录成功并已进入已登录状态”；日志同时记录了 `exchange_code_webview`、`Auth token exchange succeeded` 与 `嵌入式登录成功` |
| 当前剩余风险 | 当前默认主线已完成真实闭环；剩余风险主要转回外部 Epic 页面桥接点未来是否漂移，而不是本地宿主尺寸问题 |

---

## 修复统计

| 批次 | 修复数 | 类型 |
|------|--------|------|
| Batch 1 | 8 | 关键 Bug（线程安全/死锁/竞态/泄漏） |
| Batch 2 | 5 | 中等 Bug（逻辑/安全/事务） |
| Batch 3 | 6 | 中等 Bug（资源泄漏/IO/日志） |
| Batch 4 | 5 | 性能 + 安全 |
| Batch 5 | 5 | 架构改进 |
| Batch 6 | 2 | 代码质量 DRY |
| **总计** | **31** | **71 项中已修复 31 项 (43.7%)** |

### 未修复项 → 实施计划

**详细实施文档**：[10-RemainingFixPlan.md](10-RemainingFixPlan.md)

| Phase | 名称 | Task 数 | 审查 ID | 状态 |
|-------|------|---------|---------|------|
| P1 | Downloads 接口提取 | 3 | R3-03🔴, R3-04, R3-05 | ✅ fcc6145 |
| P2 | 跨模块依赖修复 | 3 | R2-01🔴, R2-03, R2-04, R2-05 | ✅ fe5f469 |
| P3 | ThemeService 架构 | 1 | R1-05🔴 | ✅ 900e10d |
| P4 | 启动管线完善 | 3 | R1-06, R3-08, R3-10 | ✅ bab76a2 |
| P5 | 安全加固 | 3 | R5-18, R5-19, R5-20 | ✅ 8994045 |
| P6 | CT 改造 | 3 | R4-23, R4-26, R5-11 | ✅ 6472813 (R5-11 deferred) |
| P7 | 代码质量 | 6 | R5-02,05,08,14,15,16,17 | ✅ 41a4979 (R5-05/08/15/17 deferred) |
| P8 | Presentation 层 | 5 | R1-01, R2-06, R5-04,07,10 | ✅ a07c62f (R5-04/07/10 deferred) |
| P9 | 文档同步 | 7 | R2-02,07, R3-01,02,06,07,11,12, R5-06 | ✅ ab7b4b4 |
| P10 | 测试覆盖 | 6 | R5-12🔴 | ✅ db308eb |
| **总计** | | **40** | **39 项（含1重复）** | **P1-P10 完成** |
