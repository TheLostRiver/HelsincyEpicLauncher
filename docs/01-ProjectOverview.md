# HelsincyEpicLauncher — 项目总览

> 版本：v1.0  
> 最后更新：2026-04-12  
> 状态：架构设计阶段

---

## 1. 项目定位

HelsincyEpicLauncher 是一款 **Windows 10/11 原生桌面客户端**，用于替代 Epic Games 官方启动器。

目标是解决官方启动器长期存在的问题：
- 冷启动慢
- UI 面板打不开 / 卡死
- 下载状态刷新时界面冻结
- 标签切换卡顿
- 滚动掉帧

---

## 2. 功能范围

### 2.1 包含的功能

| 功能域 | 说明 |
|--------|------|
| **Fab 资产库** | 浏览、搜索、筛选、下载、管理 Fab 市场资产（3D 模型、材质、蓝图、音效、代码插件等） |
| **下载管理** | 分块下载、断点续传、暂停/恢复、下载队列、优先级调整、完整性校验 |
| **安装管理** | 资产安装、文件校验、损坏修复、版本管理 |
| **账户认证** | Epic Games OAuth 2.0 登录、Token 管理、会话维持 |
| **引擎版本管理** | Unreal Engine 版本浏览、下载、安装、切换 |
| **插件管理** | UE 插件/资产在本地项目中的集成管理 |
| **设置中心** | 下载路径、并发数、代理设置、主题、语言等 |
| **诊断中心** | 日志查看、网络诊断、磁盘状态、缓存管理 |
| **自动更新** | 启动器自身的版本检查与更新 |

### 2.2 明确不包含的功能

| 排除项 | 原因 |
|--------|------|
| **游戏商店** | 不做游戏购买/浏览页面 |
| **游戏库存** | 不管理已购游戏列表和游戏启动（注意：Fab 资产库存 ≠ 游戏库存） |
| **社交功能** | 不做好友列表、聊天、成就 |
| **云存档** | 不做游戏存档云同步 |

---

## 3. 目标平台

- **唯一目标**：Windows 10 / Windows 11（x64）
- **不考虑跨平台**
- **最低系统要求**：Windows 10 1809+，.NET 8+（或当时最新 LTS）

---

## 4. 核心质量目标

| 指标 | 目标 |
|------|------|
| 冷启动到可交互 | < 2 秒 |
| 页面切换 | < 100ms 感知延迟 |
| 滚动帧率 | 60fps 无掉帧 |
| 下载进度刷新 | 不阻塞 UI 线程 |
| 100+ 资产卡片 | 虚拟化列表，无全量渲染 |
| 崩溃恢复 | 下载任务可从断点恢复 |

---

## 5. 技术栈概要

| 层面 | 选型 |
|------|------|
| UI 框架 | WinUI 3（Windows App SDK） |
| MVVM 库 | CommunityToolkit.Mvvm |
| 依赖注入 | Microsoft.Extensions.DependencyInjection |
| 配置系统 | Microsoft.Extensions.Configuration |
| 本地存储 | SQLite + JSON |
| HTTP | HttpClient + Polly（重试/熔断） |
| 日志 | Serilog（结构化日志） |
| 语言 | C# 12+ / .NET 8+ |

> 详见 [11-TechStack.md](11-TechStack.md)

---

## 6. 架构风格

采用 **分层架构 + 模块化纵向切片** 的混合模式：

```
┌─────────────────────────────────────────┐
│              Launcher.App               │  ← WinUI 3 宿主
├─────────────────────────────────────────┤
│           Presentation Layer            │  ← Pages / ViewModels / Navigation
├─────────────────────────────────────────┤
│           Application Layer             │  ← UseCases / Commands / Queries
├─────────────────────────────────────────┤
│             Domain Layer                │  ← Entities / StateMachines / Rules
├─────────────────────────────────────────┤
│          Infrastructure Layer           │  ← HTTP / SQLite / FileSystem / Auth
├─────────────────────────────────────────┤
│           Background Layer              │  ← Workers / Schedulers / Queues
└─────────────────────────────────────────┘
```

每个业务域（Downloads、FabLibrary、Installations 等）在层内形成独立模块，模块之间仅通过 Contracts 通信。

> 详见 [02-ArchitecturePrinciples.md](02-ArchitecturePrinciples.md) 和 [03-SolutionStructure.md](03-SolutionStructure.md)

---

## 7. 模块清单

| 模块 | 职责 |
|------|------|
| **Shell** | 主窗口壳层、导航、标题栏、全局状态栏、Toast/Dialog 宿主 |
| **Auth** | Epic Games OAuth 登录、Token 刷新、会话管理 |
| **FabLibrary** | Fab 资产浏览、搜索、筛选、已拥有资产管理、版本兼容性 |
| **Downloads** | 下载队列、分块下载、断点续传、进度聚合、CDN 切换 |
| **Installations** | 安装、校验、修复、卸载、版本管理 |
| **EngineVersions** | UE 引擎版本列表、下载、安装、本地扫描 |
| **Plugins** | UE 插件管理、项目集成 |
| **Settings** | 应用配置、下载配置、外观配置、路径配置 |
| **Diagnostics** | 日志面板、网络诊断、磁盘状态、缓存管理 |
| **Updates** | 启动器自身更新检查与安装 |

> 详见 [06-ModuleDefinitions](06-ModuleDefinitions/) 目录下各模块文档

---

## 8. 文档索引

| 编号 | 文档 | 内容 |
|------|------|------|
| 01 | 项目总览（本文档） | 项目定位、范围、目标 |
| 02 | [架构原则](02-ArchitecturePrinciples.md) | 6 大架构原则 + 铁律 |
| 03 | [解决方案结构](03-SolutionStructure.md) | .sln 拆分、项目职责、完整目录树 |
| 04 | [模块依赖规则](04-ModuleDependencyRules.md) | 允许/禁止的依赖、通信方式、依赖图 |
| 05 | [核心接口设计](05-CoreInterfaces.md) | 跨模块契约、关键接口定义 |
| 06 | [模块定义](06-ModuleDefinitions/) | 每个模块的 ARCH/API/FLOW 文档 |
| 07 | [下载子系统](07-DownloadSubsystem.md) | 下载管线、调度器、状态机、时序图 |
| 08 | [状态管理](08-StateManagement.md) | 四类状态分离策略 |
| 09 | [错误处理](09-ErrorHandling.md) | 统一 Result/Error 模型、错误分级 |
| 10 | [启动流程](10-StartupPipeline.md) | 分阶段启动策略 |
| 11 | [技术栈选型](11-TechStack.md) | 技术选型依据 |
| 12 | [AI 协作规则](12-AICollaboration.md) | AI 辅助编码的约束与工作流 |
| 13 | [开发阶段规划](13-DevelopmentPhases.md) | Phase 0 ~ Phase 5 路线图 |
| 14 | [反模式清单](14-AntiPatterns.md) | 必须避免的架构灾难 |
