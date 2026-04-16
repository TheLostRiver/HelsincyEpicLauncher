# Windows App Runtime 1.6 修复清单

> 本文档用于修复 MyEpicLauncher 在开发机上启动时弹出“需要 Windows App Runtime 1.6”的问题。  
> 这是**环境修复文档**，不涉及仓库代码修改。  
> 适用于当前项目这种 **非打包 WinUI 3 应用**（`WindowsPackageType=None`）的运行时安装/注册异常场景。

---

## 1. 结论摘要

当前仓库的应用配置明确依赖 **Windows App Runtime 1.6**：

- [src/Launcher.App/Launcher.App.csproj](../src/Launcher.App/Launcher.App.csproj)
  - `WindowsPackageType=None`
  - `RuntimeIdentifiers=win-x64`
  - `Microsoft.WindowsAppSDK=1.6.250205002`

这意味着应用启动时会通过 **Bootstrapper 自动初始化** 去匹配本机的 Windows App Runtime 1.6。

本次排查确认：

- 本机存在多个 `Microsoft.WindowsAppRuntime.1.6` framework 包
- 版本号高于应用要求的 `6000.401.2352.0`
- 但当前用户侧 **1.6 对应的 Main / DDLM / Singleton 配套包不完整或未正确注册**
- 同时本机又存在 **1.8 系列的 Main / DDLM / Singleton**

因此，问题不是“完全没装 1.6”，而是：

**1.6 运行时集合未完整落地，Bootstrapper 无法为该应用找到一套可用的 1.6 运行时组合。**

---

## 2. 修复原则

| 编号 | 原则 | 说明 |
|------|------|------|
| R-01 | **先修复安装，再碰代码** | 当前问题是环境依赖不完整，不是仓库代码逻辑错误 |
| R-02 | **优先使用官方安装器** | 官方安装器会一次性部署 Framework、Main、Singleton、DDLM，比手工装单包更稳 |
| R-03 | **先做修复安装，不先手拆包** | 先避免把本机的 Windows App Runtime 状态越拆越乱 |
| R-04 | **每一步都可验证** | 每一步都有明确的检查命令和成功标准 |
| R-05 | **只有修复安装失败时才进入清理路径** | 删除包是次选方案，风险高于 repair / force install |

---

## 3. 目标状态

在 **x64 Windows** 上，Windows App Runtime 1.6 的目标状态应为：

```text
Framework = x86 + x64
Main      = x64
Singleton = x64
DDLM      = x86 + x64
```

只要缺其中一类，非打包 WinUI 3 应用就可能在 Bootstrap 阶段弹窗失败。

---

## 4. 稳健修复步骤

### Step 0：关闭相关进程

先关闭：

- `Launcher.App.exe`
- Visual Studio / VS Code 中正在调试该应用的进程
- 其他正在运行的 WinUI 3 开发版应用（如有）

建议以 **管理员 PowerShell** 执行后续步骤。

---

### Step 1：记录当前状态（只读）

执行以下命令，保存当前运行时状态，便于失败时回溯：

```powershell
Get-AppxPackage -Name 'Microsoft.WindowsAppRuntime.1.6' |
  Select-Object Name, PackageFullName, Version, Architecture, IsFramework, Status |
  Format-Table -AutoSize

Get-AppxPackage | Where-Object {
  $_.Name -like 'MicrosoftCorporationII.WinAppRuntime.Main*' -or
  $_.Name -like 'MicrosoftCorporationII.WinAppRuntime.Singleton*' -or
  $_.Name -like 'Microsoft.WinAppRuntime.DDLM*'
} | Select-Object Name, PackageFullName, Version, Architecture, Status |
  Sort-Object Name, Version |
  Format-Table -AutoSize
```

**成功标准**：只是记录现状，不要求这一阶段已经正常。

---

### Step 2：优先执行官方 1.6 安装器的修复安装

官方 `winget` 源当前可用的稳定版本是：

- `Microsoft.WindowsAppRuntime.1.6`
- 版本 `1.6.9`
- 安装器类型 `exe`
- x64 安装器 URL：`https://aka.ms/windowsappsdk/1.6/1.6.250602001/windowsappruntimeinstall-x64.exe`

优先方案二选一，任选其一：

#### 方案 A：直接用 winget 强制修复

```powershell
winget install --id Microsoft.WindowsAppRuntime.1.6 --exact --version 1.6.9 --force --accept-package-agreements --accept-source-agreements
```

#### 方案 B：下载官方安装器并强制修复

```powershell
Invoke-WebRequest -Uri 'https://aka.ms/windowsappsdk/1.6/1.6.250602001/windowsappruntimeinstall-x64.exe' -OutFile "$env:TEMP\WindowsAppRuntimeInstall-1.6-x64.exe"
Start-Process -FilePath "$env:TEMP\WindowsAppRuntimeInstall-1.6-x64.exe" -ArgumentList '--force' -Wait -Verb RunAs
```

如果需要静默执行：

```powershell
Start-Process -FilePath "$env:TEMP\WindowsAppRuntimeInstall-1.6-x64.exe" -ArgumentList '--force --quiet' -Wait -Verb RunAs
```

**为什么先这样做**：官方文档明确建议对非打包应用优先运行安装器，让安装器一次性补齐所有 MSIX 依赖，而不是单独处理 framework 包。

**成功标准**：安装器返回成功，或 `winget` 输出安装/修复完成。

---

### Step 3：重启当前用户会话

优先顺序：

1. 注销并重新登录当前 Windows 用户
2. 如果不方便注销，则重启系统

**原因**：Windows App Runtime 的动态依赖和包注册缓存，有时不会在当前用户会话中即时完全刷新。

---

### Step 4：验证 1.6 运行时集合是否完整

重新登录后，执行：

```powershell
Get-AppxPackage -Name 'Microsoft.WindowsAppRuntime.1.6' |
  Select-Object Name, PackageFullName, Version, Architecture, IsFramework, Status |
  Sort-Object Architecture, Version |
  Format-Table -AutoSize

Get-AppxPackage | Where-Object {
  $_.Name -like 'MicrosoftCorporationII.WinAppRuntime.Main.1.6*' -or
  $_.Name -like 'MicrosoftCorporationII.WinAppRuntime.Singleton*' -or
  $_.Name -like 'Microsoft.WinAppRuntime.DDLM.6000*'
} | Select-Object Name, PackageFullName, Version, Architecture, Status |
  Sort-Object Name, Version |
  Format-Table -AutoSize
```

**通过标准**：

- 能看到 `Microsoft.WindowsAppRuntime.1.6` 的 `x64` framework 包
- 能看到 `MicrosoftCorporationII.WinAppRuntime.Main.1.6` 的 `x64` 包
- 能看到 Singleton 的 `x64` 包
- 能看到 `DDLM.6000*` 的 `x64` 和 `x86` 包

如果这一步通过，再启动应用。

---

### Step 5：验证应用启动

建议在仓库根目录执行：

```powershell
dotnet build .\HelsincyEpicLauncher.slnx
.\src\Launcher.App\bin\Debug\net9.0-windows10.0.19041.0\win-x64\Launcher.App.exe
```

**通过标准**：

- 不再弹“需要 Windows App Runtime 1.6”窗口
- 应用主窗口正常显示

---

## 5. 若 Step 2 失败，再进入清理路径

> 这一节是**次选方案**。只有在官方安装器 `--force` 仍无法补齐 1.6 组件时再做。  
> 目标是清掉当前用户侧损坏/残缺的 1.6 注册，再重新执行官方安装器。

### Step 5.1：再次确认当前用户缺的是哪类包

```powershell
Get-AppxPackage | Where-Object {
  $_.Name -like 'Microsoft.WindowsAppRuntime.1.6' -or
  $_.Name -like 'MicrosoftCorporationII.WinAppRuntime.Main.1.6*' -or
  $_.Name -like 'Microsoft.WinAppRuntime.DDLM.6000*' -or
  $_.Name -like 'MicrosoftCorporationII.WinAppRuntime.Singleton*'
} | Select-Object Name, PackageFullName, Version, Architecture, Status |
  Sort-Object Name, Version |
  Format-Table -AutoSize
```

如果依旧是 “1.6 framework 在，但 Main/DDLM 不在”，继续下一步。

### Step 5.2：卸载当前用户侧残缺的 1.6 包

```powershell
Get-AppxPackage -Name 'Microsoft.WindowsAppRuntime.1.6' |
  Remove-AppxPackage

Get-AppxPackage | Where-Object {
  $_.Name -like 'MicrosoftCorporationII.WinAppRuntime.Main.1.6*' -or
  $_.Name -like 'Microsoft.WinAppRuntime.DDLM.6000*'
} | Remove-AppxPackage
```

如果命令提示权限问题，使用 **管理员 PowerShell** 重试。

**注意**：

- 不建议先动 1.7 / 1.8
- 不建议先手删 Singleton，除非确认它本身也损坏

### Step 5.3：立即重新执行官方 1.6 安装器

```powershell
winget install --id Microsoft.WindowsAppRuntime.1.6 --exact --version 1.6.9 --force --accept-package-agreements --accept-source-agreements
```

然后重复 **Step 3** 和 **Step 4**。

---

## 6. 不建议的做法

以下做法都不稳，容易把环境搞得更乱：

- 只看到 framework 包就认定“1.6 已完整安装”
- 手工只装某一个 `Microsoft.WindowsAppRuntime.1.6` 单包
- 直接删除全部 1.7 / 1.8 运行时
- 在问题未确认前修改仓库里的 Windows App SDK 版本
- 在未验证 Main / DDLM / Singleton 的情况下，把问题归咎于 `Program.cs`

---

## 7. 验收清单

全部满足才算修复完成：

- [ ] `Microsoft.WindowsAppRuntime.1.6` framework 包完整存在
- [ ] `MicrosoftCorporationII.WinAppRuntime.Main.1.6` 已注册
- [ ] `Singleton` 已注册
- [ ] `DDLM.6000*` 的 `x64` / `x86` 已注册
- [ ] 应用启动时不再弹运行时缺失窗口
- [ ] [src/Launcher.App/Launcher.App.csproj](../src/Launcher.App/Launcher.App.csproj) 无需修改即可正常启动

---

## 8. 仍未修复时的下一步

如果按上面的顺序执行后仍失败，下一步继续排查：

1. 读取 `AppModel-Runtime/Admin` 事件日志，定位 Bootstrap 返回码
2. 读取 `AppXDeploymentServer/Operational`，确认 1.6 的 Main / DDLM 是否在安装阶段报错
3. 检查企业策略、Developer Mode、用户权限是否阻止了 MSIX 注册
4. 必要时改为显式调用 Bootstrap API，把错误码写入日志

---

## 9. 参考依据

- Microsoft 官方：Windows App SDK deployment guide for framework-dependent apps packaged with external location or unpackaged
- Microsoft 官方：Use the Windows App SDK runtime for apps packaged with external location or unpackaged
- Microsoft 官方：Deployment architecture and overview for framework-dependent apps
