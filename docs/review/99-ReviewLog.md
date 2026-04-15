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
