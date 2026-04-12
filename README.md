# HelsincyEpicLauncher

Epic Games 启动器替代客户端，基于 WinUI 3 + .NET 8 构建。

## 项目概述

本项目旨在构建一个功能完整的 Epic Games 启动器替代品，专注于以下核心功能：

- **Fab 资产库** — 浏览、搜索、下载 Unreal Engine 资产
- **下载管理** — 分块下载、断点续传、崩溃恢复
- **安装管理** — 安装、校验、修复、卸载
- **引擎版本管理** — UE 引擎下载、安装、启动
- **插件管理** — 已安装插件管理
- **认证** — Epic Games OAuth 2.0 登录

> 本项目 **不包含** 游戏商店和游戏库存功能。

## 技术栈

| 类别 | 技术 |
|------|------|
| UI 框架 | WinUI 3 (Windows App SDK) |
| 运行时 | .NET 8+ / C# 12+ |
| MVVM | CommunityToolkit.Mvvm |
| DI | Microsoft.Extensions.DependencyInjection |
| 日志 | Serilog |
| 数据库 | SQLite + Dapper |
| 网络韧性 | Polly |
| 测试 | xUnit + NSubstitute + FluentAssertions |

## 项目结构

```
HelsincyEpicLauncher.sln
├─ src/
│  ├─ Launcher.App              → WinUI 3 入口 + DI 注册
│  ├─ Launcher.Presentation     → 页面、ViewModel、控件
│  ├─ Launcher.Application      → 用例处理器、DTO
│  ├─ Launcher.Domain           → 领域实体、状态机
│  ├─ Launcher.Infrastructure   → 数据库、HTTP、文件系统
│  ├─ Launcher.Background       → 后台任务宿主
│  └─ Launcher.Shared           → Result、Error、基类
└─ tests/
   ├─ Launcher.Tests.Unit
   └─ Launcher.Tests.Integration
```

## 开发文档

详细的架构设计文档位于 `docs/` 目录，参见 [docs/01-ProjectOverview.md](docs/01-ProjectOverview.md)。

## 系统要求

- Windows 10 1809+ / Windows 11
- .NET 8 Runtime
- 4GB+ RAM
- 1GB+ 磁盘空间（不含下载内容）

## 构建

```bash
dotnet build
dotnet test
```

## 许可证

私有项目，仅供学习和个人使用。
