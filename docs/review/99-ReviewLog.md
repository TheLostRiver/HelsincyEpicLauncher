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
| P1 | Downloads 接口提取 | 3 | R3-03🔴, R3-04, R3-05 | 🔲 |
| P2 | 跨模块依赖修复 | 3 | R2-01🔴, R2-03, R2-04, R2-05 | 🔲 |
| P3 | ThemeService 架构 | 1 | R1-05🔴 | 🔲 |
| P4 | 启动管线完善 | 3 | R1-06, R3-08, R3-10 | 🔲 |
| P5 | 安全加固 | 3 | R5-18, R5-19, R5-20 | 🔲 |
| P6 | CT 改造 | 3 | R4-23, R4-26, R5-11 | 🔲 |
| P7 | 代码质量 | 6 | R5-02,05,08,14,15,16,17 | 🔲 |
| P8 | Presentation 层 | 5 | R1-01, R2-06, R5-04,07,10 | 🔲 |
| P9 | 文档同步 | 7 | R2-02,07, R3-01,02,06,07,11,12, R5-06 | 🔲 |
| P10 | 测试覆盖 | 6 | R5-12🔴 | 🔲 |
| **总计** | | **40** | **39 项（含1重复）** | |
