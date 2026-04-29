# Installations Application Boundary

> AI 模型：GPT-5 Codex
> 创建日期：2026-04-30
> 依据：`docs/06-ModuleDefinitions/Installations.md`、当前 Installations 代码结构

---

## 1. 目标边界

Installations 模块的 Application 层应承载安装、卸载、校验、修复的用例编排和跨模块稳定契约。文件解压、复制、目录删除、哈希计算、HTTP 修复下载、SQLite 持久化等技术细节应留在 Infrastructure。

当前实现的主要问题是 `Infrastructure.Installations.InstallCommandService` 同时承担了用例编排和部分文件系统实现。后续迁移应先把主流程拆到 Application，再逐步引入内部端口。

---

## 2. 对外公共 Contracts

以下接口是跨模块和 UI 可见入口：

| Contract | 当前职责 | 边界要求 |
|----------|----------|----------|
| `IInstallCommandService` | Install/Uninstall/Repair 命令入口 | 应由 Application 实现；只接受请求 DTO 或稳定标识，返回 `Result` |
| `IInstallReadService` | 查询安装状态 | 应返回稳定 Summary，不返回可变领域实体 |

以下模型当前作为公共模型：

| Model | 当前用途 | 边界要求 |
|-------|----------|----------|
| `InstallRequest` | 安装请求 | 可以作为公共请求 DTO |
| `InstallStatusSummary` | 安装状态摘要 | 应保持为 UI/跨模块投影 |
| `VerificationReport` | 校验结果 | 可作为校验用例输出，避免暴露文件系统细节 |
| `VerificationProgress` | 校验进度 | 仅用于节流后的 UI 进度 |
| `RepairFileResult` | 修复结果 | 可作为内部端口结果，公共暴露时应谨慎 |
| Installation/Repair/Uninstall events | 领域事实通知 | 只描述已发生事实，不替代命令或查询 |

当前 `InstallStatusSummary.State` 使用 Domain 的 `InstallState`，属于已知短期兼容债务。长期目标是让 UI 和跨模块调用方不直接引用 `Launcher.Domain.Installations`。

---

## 3. 内部端口与技术实现

以下接口当前位于 `Contracts` 目录，但多数应视为模块内部端口：

| Port | 当前实现 | 应留位置 | 说明 |
|------|----------|----------|------|
| `IInstallationRepository` | `Infrastructure.Installations.InstallationRepository` | Infrastructure 实现 | SQLite 持久化端口，可返回 `Installation`、`InstallManifest` |
| `IIntegrityVerifier` | `Infrastructure.Installations.IntegrityVerifier` | Infrastructure 实现 | 文件系统遍历和哈希比对，Application 只依赖接口 |
| `IHashingService` | `Infrastructure.Installations.HashingService` | Infrastructure 实现 | 文件哈希计算，纯技术能力 |
| `IRepairDownloadUrlProvider` | `Infrastructure.Installations.RepairDownloadUrlProvider` | Infrastructure 实现 | 修复用下载地址解析，依赖 Fab/Downloads 信息 |

建议目录演进：

```text
src/Launcher.Application/Modules/Installations/
  Contracts/   # IInstallCommandService、IInstallReadService、请求、Summary、Event
  Ports/       # Repository、Verifier、Hashing、Repair URL、File operations
  UseCases/    # Install/Uninstall/Repair/Verify 用例
```

---

## 4. 当前编排与文件系统职责划分

### 4.1 应迁移到 Application 的用例编排

`InstallCommandService.InstallAsync` 中应迁移的部分：

- 查询是否已有安装记录。
- 创建或复用 `Installation` 领域实体。
- 执行安装状态转换。
- 调用安装执行端口。
- 根据安装结果更新状态和发布完成/失败事件。

`InstallCommandService.UninstallAsync` 中应迁移的部分：

- 查询安装记录。
- 执行卸载状态转换。
- 调用文件删除端口。
- 删除 manifest 和 repository 记录。
- 发布卸载完成事件。

`InstallCommandService.RepairAsync` 中应迁移的主流程：

- 查询安装记录。
- 进入 `Repairing` 状态。
- 读取 manifest。
- 调用完整性校验端口。
- 根据 missing/corrupted 文件决定是否需要修复。
- 获取修复下载信息。
- 调用修复文件下载端口。
- 二次校验。
- 根据结果转回 `Installed`、`NeedsRepair` 或 `Failed`。
- 发布 `RepairCompletedEvent`。

### 4.2 应继续留在 Infrastructure 的实现

`InstallWorker` 应继续留在 Infrastructure：

- 检查源文件是否存在。
- 检查磁盘空间。
- 创建目标目录。
- ZIP 解压和单文件复制。
- Zip Slip 防护。
- 逐文件哈希计算。
- 生成并保存 manifest 的技术细节。

`RepairFileDownloader` 应继续留在 Infrastructure：

- HTTP 下载修复文件。
- 写入临时文件。
- 替换损坏文件。
- 下载后哈希验证。

`IntegrityVerifier` 和 `HashingService` 应继续留在 Infrastructure：

- 遍历本地文件。
- 计算 SHA-1/SHA-256。
- 对比 manifest。
- 汇报校验进度。

`InstallationRepository` 应继续留在 Infrastructure：

- SQLite 读写。
- manifest 持久化。
- row/domain 映射。

---

## 5. 当前主要架构债务

| 债务 | 影响 | 后续方向 |
|------|------|----------|
| `InstallCommandService` 位于 Infrastructure | 应用编排落在技术层 | 迁移到 Application |
| `UninstallAsync` 直接调用 `Directory.Delete` | 命令服务混入文件系统实现 | 引入安装文件操作端口 |
| `RepairAsync` 主流程过长 | 修复策略、状态流转、下载和校验耦合 | 拆成 `RepairInstallationUseCase` |
| `InstallWorker` 同时执行安装和保存 manifest | 技术执行与持久化边界不够细 | 短期保留，后续按需要拆端口 |
| `InstallStatusSummary` 暴露 `InstallState` | UI 依赖 Domain | 后续 Contract-owned 状态 |
| Presentation 当前依赖 `IIntegrityVerifier` 和 `IInstallationRepository` | UI 可见内部端口 | 后续通过应用查询/命令收口 |

---

## 6. 迁移顺序建议

1. 先新增 `InstallInstallationUseCase` 壳，封装 `InstallAsync` 参数校验和委托。
2. 新增 `RepairInstallationUseCase` 壳，先承接 `RepairAsync` 主流程，不改文件下载实现。
3. 将 `IInstallCommandService` 实现迁到 Application。
4. `InstallWorker`、`RepairFileDownloader`、`IntegrityVerifier`、`HashingService`、`InstallationRepository` 继续留在 Infrastructure，并通过内部端口注入。
5. 后续再把 Presentation 对 Repository/Verifier 的直接依赖收口到应用查询或命令。

---

## 7. 验收约束

- 新的 Installations 公共 Contract 不能返回 `Installation` 领域实体。
- 新的 UI 查询不应要求 Presentation 直接引用 `Launcher.Domain.Installations`。
- `RepairAsync` 主流程迁移时不得改动 `RepairFileDownloader` 的 HTTP/文件替换细节。
- `InstallWorker` 在迁移预备阶段不得移动；只允许通过端口包装或委托。
