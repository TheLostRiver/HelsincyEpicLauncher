# 技术栈选型

> 本文档列出所有技术选型及其理由。

---

## 1. 技术栈总览

| 层面 | 选型 | 版本 | 用途 |
|------|------|------|------|
| **运行时** | .NET 8+ (LTS) | 最新 LTS | 应用运行时 |
| **语言** | C# 12+ | 随 .NET 版本 | 主要开发语言 |
| **UI 框架** | WinUI 3 (Windows App SDK) | 最新稳定版 | 原生 Windows UI |
| **MVVM 库** | CommunityToolkit.Mvvm | 最新稳定版 | ViewModel 基类、Command、消息 |
| **依赖注入** | Microsoft.Extensions.DependencyInjection | 随 .NET | IoC 容器 |
| **配置** | Microsoft.Extensions.Configuration | 随 .NET | 分层配置加载 |
| **本地数据库** | SQLite (Microsoft.Data.Sqlite) | 最新 | 结构化数据持久化 |
| **ORM** | Dapper 或 raw ADO.NET | 最新 | 轻量数据访问 |
| **HTTP** | HttpClient + HttpClientFactory | 随 .NET | API 调用 |
| **重试/熔断** | Polly | 最新 | 网络韧性 |
| **日志** | Serilog | 最新 | 结构化日志 |
| **JSON** | System.Text.Json | 随 .NET | JSON 序列化 |
| **压缩** | System.IO.Compression | 随 .NET | 解压安装包 |
| **哈希** | System.Security.Cryptography | 随 .NET | SHA-1 / SHA-256 |
| **单元测试** | xUnit | 最新 | 测试框架 |
| **Mock** | NSubstitute | 最新 | 接口 Mock |
| **断言** | FluentAssertions | 最新 | 可读断言 |

---

## 2. 选型理由

### 2.1 WinUI 3

**为什么选它**：

- 微软当前推荐的 Windows 原生 UI 框架
- 属于 Windows App SDK 核心部分
- 天然贴近 Windows 10/11 Fluent Design
- 它是"做新的 Windows-only 应用"的最佳起点（微软官方立场）
- 支持自定义标题栏、Mica/Acrylic 背景、深色模式

**为什么不是 WPF**：

- WPF 更成熟但不是新项目首推
- WinUI 3 的 Fluent Theme 更现代
- 从零开始没有历史包袱

**为什么不是 Flutter / Avalonia**：

- 只做 Windows，跨平台优势不需要
- 原生感和系统集成度不如 WinUI 3

### 2.2 CommunityToolkit.Mvvm

- 微软官方 MVVM 库（WinUI 教程默认推荐）
- 源码生成器减少样板代码（`[ObservableProperty]`, `[RelayCommand]`）
- 轻量，不绑定特定 UI 框架
- 提供消息机制（模块内低耦合通知）

### 2.3 SQLite

- 嵌入式，无需安装额外服务
- 适合客户端本地数据存储
- 支持事务、索引、WAL 模式
- 文件级数据库，易于备份和迁移

**为什么不用重 ORM（Entity Framework）**：

- 启动器客户端的 SQL 需求简单
- EF Core 引入太多抽象和运行时开销
- Dapper 或原生 ADO.NET 更轻量、更可控

### 2.4 Polly

- .NET 生态最成熟的韧性库
- 支持重试、熔断、超时、限流、回退
- 完美配合 HttpClientFactory
- 下载器的 CDN 切换和重试策略直接基于 Polly 策略
- .NET 8+ 内置 `Microsoft.Extensions.Http.Resilience` 基于 Polly

### 2.5 Serilog

- 结构化日志（JSON 格式，方便诊断面板读取）
- 多 Sink 支持：文件、Console、诊断面板实时流
- 日志轮转：按大小/日期自动清理旧日志
- 高性能异步写入

### 2.6 Dapper（而非 EF Core）

- 轻量：几乎零抽象开销
- 快速：接近原生 ADO.NET 性能
- 透明：SQL 直接可见，不走黑盒
- 适合客户端场景：不需要复杂迁移框架

---

## 3. NuGet 包清单

```xml
<!-- Launcher.App -->
<PackageReference Include="Microsoft.WindowsAppSDK" />
<PackageReference Include="Microsoft.Windows.SDK.BuildTools" />
<PackageReference Include="Microsoft.Extensions.DependencyInjection" />
<PackageReference Include="Microsoft.Extensions.Configuration" />
<PackageReference Include="Microsoft.Extensions.Configuration.Json" />
<PackageReference Include="Serilog" />
<PackageReference Include="Serilog.Sinks.File" />

<!-- Launcher.Presentation -->
<PackageReference Include="CommunityToolkit.Mvvm" />
<PackageReference Include="CommunityToolkit.WinUI.Controls" />

<!-- Launcher.Infrastructure -->
<PackageReference Include="Microsoft.Data.Sqlite" />
<PackageReference Include="Dapper" />
<PackageReference Include="Polly" />
<PackageReference Include="Microsoft.Extensions.Http" />
<PackageReference Include="System.Text.Json" />

<!-- Launcher.Tests.Unit -->
<PackageReference Include="xunit" />
<PackageReference Include="xunit.runner.visualstudio" />
<PackageReference Include="NSubstitute" />
<PackageReference Include="FluentAssertions" />

<!-- Launcher.Tests.Integration -->
<PackageReference Include="xunit" />
<PackageReference Include="Microsoft.Data.Sqlite" />
<PackageReference Include="FluentAssertions" />
```

---

## 4. 开发工具

| 工具 | 用途 |
|------|------|
| Visual Studio 2022+ | 主 IDE（WinUI 3 XAML 设计器） |
| VS Code + C# DevKit | 轻量编辑 |
| DB Browser for SQLite | 数据库调试 |
| Fiddler / Charles | 网络抓包 |
| WinUI 3 Gallery | 控件参考 |
| Git | 版本控制 |

---

## 5. 最低系统要求

| 项目 | 要求 |
|------|------|
| 操作系统 | Windows 10 1809 (Build 17763)+ |
| 架构 | x64 |
| 运行时 | 自包含部署（不要求用户安装 .NET） |
| 磁盘 | 安装本体约 50-100 MB |
| 内存 | 建议 4GB+ |
| 网络 | 需要网络进行 Fab 资产浏览和下载 |

---

## 6. 打包与部署

### 6.1 部署方式

推荐 **Unpackaged** 部署（非 MSIX）：

- 不需要 Windows Store
- 用户直接运行安装包
- 更接近传统桌面软件的分发方式
- 自动更新由 Updates 模块自行实现

### 6.2 自包含部署

```xml
<PropertyGroup>
    <SelfContained>true</SelfContained>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <PublishSingleFile>false</PublishSingleFile>
    <PublishTrimmed>false</PublishTrimmed>
</PropertyGroup>
```

- 自包含：不依赖用户安装 .NET Runtime
- 不建议 SingleFile：WinUI 3 和 Windows App SDK 有 DLL 依赖
- 不建议 Trimmed：WinUI 3 反射较多，trim 容易出问题
