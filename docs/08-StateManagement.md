# 状态管理

> 状态管理是大型桌面客户端最容易失控的部分。本文档定义状态的分类、存储位置、更新策略和隔离规则。

---

## 1. 状态四分法

项目中所有状态严格分为四类，**禁止混存**：

| 分类 | 生命周期 | 存储位置 | 更新方式 |
|------|---------|---------|---------|
| 持久化状态 | 跨进程/重启 | SQLite / JSON 文件 | Repository 写入 |
| 运行时业务状态 | 仅进程内 | 内存 Store | Service 更新 |
| 页面状态 | 仅页面存活期 | ViewModel 字段 | 属性赋值 |
| 全局 UI 状态 | 仅进程内 | Shell 级 Store | Shell 服务更新 |

---

## 2. 持久化状态

### 2.1 什么属于持久化状态

- 已安装资产列表及路径
- 下载任务（含断点 checkpoint）
- Fab 已拥有资产缓存
- 安装 Manifest 缓存
- 用户登录会话（Token）
- 用户配置（下载路径、并发数、主题等）
- 最近浏览/搜索记录

### 2.2 存储选择

| 数据类型 | 存储 | 原因 |
|---------|------|------|
| 结构化、需查询的数据 | SQLite | 支持索引、事务、复杂查询 |
| 用户配置 | JSON 文件 | 人可读可编辑，配置系统原生支持 |
| 大型 Manifest | JSON + SQLite 索引 | JSON 存完整内容，SQLite 存检索索引 |
| 登录凭证 | Windows Credential Locker | 操作系统级安全存储 |

### 2.3 访问规则

```
UI / ViewModel  ──❌──▶  SQLite / FileSystem
UI / ViewModel  ──✅──▶  Repository 接口 ──▶  Infrastructure 实现
```

持久化状态只能通过 Repository 接口访问，Repository 只暴露业务语义方法：

```csharp
// ✅ 好：业务语义
Task<IReadOnlyList<FabAssetSummary>> GetOwnedAssetsAsync(CancellationToken ct);

// ❌ 坏：泄露实现细节
Task<List<Dictionary<string, object>>> ExecuteSqlAsync(string sql);
```

---

## 3. 运行时业务状态

### 3.1 什么属于运行时业务状态

- 当前下载进度（每个任务的实时字节数、速度、预估时间）
- 当前活跃安装任务
- 网络可用性状态
- 磁盘监控状态（剩余空间）
- CDN 节点健康度

### 3.2 存储方式

使用专门的内存 Store，不和持久化混在一起：

```csharp
// 下载运行时状态
public interface IDownloadRuntimeStore
{
    IReadOnlyCollection<DownloadRuntimeSnapshot> Current { get; }
    void Upsert(DownloadRuntimeSnapshot snapshot);
    void Remove(DownloadTaskId taskId);
    event EventHandler<DownloadRuntimeSnapshot>? SnapshotChanged;
}

// 全局运行时状态
public interface IAppRuntimeState
{
    bool IsNetworkAvailable { get; }
    event EventHandler<bool>? NetworkAvailabilityChanged;
}
```

### 3.3 更新策略

- 下载进度：每 500ms 聚合一次，写入 Store，触发事件
- 网络状态：监听系统 `NetworkChange` 事件
- 磁盘空间：每 60 秒检查一次

### 3.4 与持久化状态的关系

```
运行时状态是持久化状态的"活跃影子"。

举例：
- SQLite 存：DownloadTask（状态、元数据、checkpoint）
- Runtime Store 存：DownloadRuntimeSnapshot（实时进度、速度）

两者独立更新周期：
- Runtime Store：500ms 更新一次
- SQLite checkpoint：30s 或 chunk 完成时更新
- SQLite 任务状态：状态转换时更新
```

---

## 4. 页面状态

### 4.1 什么属于页面状态

- 当前搜索框文本
- 当前选中的筛选条件
- 当前排序方式
- 选中的列表项
- 面板展开/折叠状态
- 当前 Tab 选中项
- 分页游标
- 输入框草稿内容

### 4.2 存储方式

直接存在 ViewModel 私有字段中：

```csharp
public partial class FabLibraryViewModel : ObservableObject
{
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private string? _selectedCategory;
    [ObservableProperty] private FabSortOrder _sortOrder = FabSortOrder.Relevance;
    [ObservableProperty] private FabAssetCardViewModel? _selectedAsset;
    [ObservableProperty] private bool _isLoading;
}
```

### 4.3 规则

- 页面离开时，页面状态自然销毁（除非特意缓存）
- 页面状态不写数据库
- 页面状态不通过事件总线广播
- 如果某个"页面状态"需要跨页面共享 → 它不是页面状态，而是全局 UI 状态

---

## 5. 全局 UI 状态

### 5.1 什么属于全局 UI 状态

- 当前主题（Light / Dark / System）
- 全局 Loading 遮罩
- Toast 队列
- 当前活跃下载数（显示在导航栏角标）
- 是否显示 Mini 下载面板
- 全局搜索栏是否展开
- 当前登录用户显示名

### 5.2 存储方式

通过 Shell 级 ViewModel 直接管理（当前实现未提取独立 ShellState 类，
全局 UI 状态以 `[ObservableProperty]` 形式存放在 `ShellViewModel` 中）：

```csharp
// 概念模型 — 实际分散在 ShellViewModel 的属性中
public sealed class ShellState : ObservableObject
{
    [ObservableProperty] private string _theme = "System";
    [ObservableProperty] private bool _isGlobalLoading;
    [ObservableProperty] private int _activeDownloadCount;
    [ObservableProperty] private bool _isMiniDownloadPanelOpen;
    [ObservableProperty] private bool _isAuthenticated;
    [ObservableProperty] private string _userName = "";
}
```

### 5.3 更新方式

- ShellViewModel 订阅各模块的状态变化事件，汇总更新 ShellState
- 不允许业务模块直接操作 ShellState
- 业务模块通过发布事件（如 `DownloadCompletedEvent`）间接影响

---

## 6. 四类状态的隔离规则

### 规则 S-01：持久化状态不直接暴露给 UI

```
❌ Page.xaml 直接绑定 SqliteEntity
✅ Page.xaml 绑定 ViewModel 属性，ViewModel 从 ReadService 获取 DTO
```

### 规则 S-02：运行时状态不替代持久化

```
❌ 只把下载进度存内存，崩溃后丢失
✅ 内存存实时快照，SQLite 定期存 checkpoint
```

### 规则 S-03：页面状态不外泄

```
❌ FabLibraryViewModel 的搜索条件通过事件广播给 Downloads 模块
✅ 搜索条件是 FabLibraryViewModel 私有，调用 ReadService 时作为参数传入
```

### 规则 S-04：全局 UI 状态不承载业务逻辑

```
❌ ShellState.ActiveDownloadCount 被用来判断"是否可以退出应用"
✅ 退出前调用 IDownloadReadService.GetActiveDownloadsAsync() 判断
```

### 规则 S-05：不同类型状态不混存到一个对象

```
❌ 一个巨大的 AppState 类同时包含主题、下载进度、搜索条件、数据库连接状态
✅ ShellState 只管 UI 全局状态，DownloadRuntimeStore 只管下载运行时
```

---

## 7. 状态更新频率控制

| 状态类型 | 更新频率 | 节流策略 |
|---------|---------|---------|
| 下载进度 | 500ms 聚合 | DownloadWorker 内部 Debouncer |
| 磁盘空间 | 60s 一次 | 定时器 |
| 网络状态 | 事件驱动 | 系统事件去重 |
| 页面筛选 | 300ms 防抖 | 搜索框 Debouncer |
| Toast | 即时 | 队列限制最大条数 |
| 活跃下载数 | 事件驱动 | 状态转换时更新 |

---

## 8. 状态恢复策略

### 8.1 启动时恢复

| 状态 | 恢复方式 |
|------|---------|
| 登录会话 | TokenStore → TryRestoreSession |
| 下载任务 | SQLite → IDownloadOrchestrator.RecoverAsync |
| 用户配置 | JSON → Configuration 系统自动加载 |
| 已安装列表 | SQLite → 按需加载 |
| 页面状态 | 不恢复（从默认值开始） |
| 全局 UI 状态 | 主题从配置恢复，其余不恢复 |

### 8.2 崩溃恢复

```
1. 下载任务：checkpoint 保证最多丢失 30 秒进度
2. 安装任务：中断的安装标记为 NeedsRepair
3. 配置：JSON 只有写入完成才替换原文件（原子写入）
4. 数据库：SQLite WAL 模式保证事务完整性
```
