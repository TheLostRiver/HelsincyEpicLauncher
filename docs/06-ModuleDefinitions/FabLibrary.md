# FabLibrary 模块

---

## 架构定义

### 职责

- Fab 资产市场浏览（搜索、分类筛选、排序）
- 已拥有资产列表管理
- 资产详情查看（描述、截图、技术细节、兼容引擎版本）
- 资产与引擎版本兼容性过滤
- 发起资产下载（委托给 Downloads 模块）
- 本地资产缓存/索引维护
- 缩略图懒加载与缓存

> Fab 详情富内容（介绍图、格式信息、详情元数据）的一期设计见 [16-FabDetailRichContentDesign.md](../review/16-FabDetailRichContentDesign.md)。
> 如需防上下文丢失的细粒度实施拆解，请看 [17-FabDetailImplementationSlices.md](../review/17-FabDetailImplementationSlices.md)。

### 不负责

- 实际下载执行（由 Downloads 模块处理）
- 文件安装/解压（由 Installations 模块处理）
- 金融交易/购买（不在项目范围内）
- 引擎版本管理（由 EngineVersions 模块处理）

### 依赖

| 依赖目标 | 用途 |
|---------|------|
| `Downloads.Contracts` | 查询下载状态、发起下载 |
| `Installations.Contracts` | 查询安装状态 |
| `Auth.Contracts` | 获取 Token 调用 Fab API |
| `Settings.Contracts` | 读取默认安装路径 |

### 谁可以依赖 FabLibrary

| 模块 | 用途 |
|------|------|
| Plugins | 查询已拥有的插件类资产 |
| Shell | 路由到 Fab 页面 |

---

## API 定义

> 详见 [05-CoreInterfaces.md](../05-CoreInterfaces.md) 第 10 节 `IFabCatalogReadService` / `IFabAssetCommandService`

### Fab 资产类型

```csharp
public enum FabAssetType
{
    Model3D,           // 3D 模型
    Material,          // 材质
    Blueprint,         // 蓝图
    Audio,             // 音效/音乐
    CodePlugin,        // 代码插件
    Environment,       // 环境/场景
    Animation,         // 动画
    VFX,               // 特效
    UI,                // UI 素材
    Other              // 其他
}
```

### 资产所有权状态

```csharp
public enum AssetOwnershipState
{
    NotOwned,          // 未拥有
    Owned,             // 已拥有，未下载
    Downloaded,        // 已下载，未安装
    Installed,         // 已安装
    UpdateAvailable    // 已安装，有新版本
}
```

---

## 关键流程

### 浏览 Fab 资产

```
1. 用户进入 Fab 资产库页面
2. FabLibraryViewModel 初始化：
   a. 加载分类列表（优先使用缓存）
   b. 加载已拥有资产列表
   c. 执行默认搜索（最新/推荐）
3. 用户输入搜索关键词 / 选择分类 / 选择引擎版本
4. FabLibraryViewModel 调用 IFabCatalogReadService.SearchAsync()
5. 结果映射为 FabAssetCardViewModel 列表
6. 虚拟化列表渲染卡片
7. 缩略图懒加载（滚动到可见区域才加载）
```

### 下载 Fab 资产

```
1. 用户在资产详情页点击"下载"
2. FabAssetDetailViewModel 调用 IFabAssetCommandService.DownloadAssetAsync()
3. FabAssetCommandService 内部：
   a. 通过 IAuthService 获取 access token
   b. 通过 Fab API 获取资产 manifest / 下载链接
   c. 调用 IDownloadCommandService.StartAsync() 创建下载任务
4. 返回 DownloadTaskId
5. 页面 UI 切换为"下载中"状态
6. 通过 IDownloadReadService 订阅进度更新
```

### 刷新已拥有资产

```
1. 启动时 Phase 3 或用户手动刷新
2. 调用 IFabCatalogReadService.GetOwnedAssetsAsync()
3. 对比本地缓存，更新差异
4. 保存到 IFabAssetRepository
5. 通知 UI 刷新列表
```

---

## UI 结构

```
FabLibraryPage
├─ SearchBar（搜索框 + 筛选器）
│  ├─ 关键词输入
│  ├─ 分类下拉
│  ├─ 引擎版本下拉
│  └─ 排序方式
├─ AssetGrid（虚拟化网格列表）
│  └─ FabAssetCard × N
│     ├─ 缩略图（懒加载）
│     ├─ 标题
│     ├─ 作者
│     ├─ 评分
│     ├─ 价格 / 已拥有标识
│     └─ 下载/安装状态图标
├─ Pagination（分页或无限滚动）
└─ DetailPanel（资产详情侧边栏或新页面）
   ├─ 大图轮播
   ├─ 描述
   ├─ 技术细节
   ├─ 兼容引擎版本列表
   ├─ 文件大小
   └─ 操作按钮（下载 / 安装 / 打开目录）
```

### 性能要求

- **虚拟化列表**：不一次性渲染所有卡片
- **缩略图懒加载**：只在可视区域内加载图片
- **缩略图缓存**：已加载的缩略图写入本地磁盘缓存
- **搜索防抖**：用户输入 300ms 无变化后再发请求
- **缓存优先**：分类列表、已拥有列表优先读缓存，后台静默刷新
