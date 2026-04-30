# Auth 模块

---

## 架构定义

### 职责

- Epic Games OAuth 2.0 登录流程
- Access Token / Refresh Token 管理
- Token 自动刷新（过期前主动刷新）
- 会话缓存与恢复（启动时自动恢复）
- 登出和会话清理
- 安全存储（Windows Credential Locker）

### 不负责

- UI 登录页面布局（由 Presentation 处理）
- 用户偏好设置（由 Settings 模块处理）
- 网络请求重试策略（由 Infrastructure 的 HTTP 层统一处理）

### 依赖

| 依赖目标 | 用途 |
|---------|------|
| `ITokenStore`（Infrastructure） | 安全存储 Token |
| `EpicAuthClient`（Infrastructure） | 调用 Epic OAuth API |
| `Launcher.Shared` | Result 模型 |

### OAuth 回调约束

- 当前默认登录路径已升级为嵌入式 WebView2 + `exchange_code` 自动完成；若嵌入式登录不可用或用户主动取消，则回退到系统浏览器链路
- 系统浏览器兜底路径仍优先要求 `authorizationCode` 或完整回调 URL；但如果 Epic 浏览器链路最终只显示 JSON 响应，Auth 会在高级手动继续入口中仅提取 `authorizationCode` 或 `redirectUrl`，避免用户必须手工拆分敏感字段
- App 宿主现在已能把启动参数或第二实例转发过来的“完整回调 URL 候选负载”自动交回 Auth；因此一旦后续拿到可用的 loopback 或协议回调来源，应用内部已具备自动完成登录的消费骨架
- 当前 clientId 的成熟外部交互链路不是本地 loopback localhost 回调，因此登录契约不再要求 UI 直接等待本地 HTTP 回调
- 若后续需要支持其他类型的回调接收方式，必须封装在 Auth 模块内部，不能把协议细节泄漏到 Shell / Settings / App；Presentation 最多只承载浏览器容器和原始结果回传
- 回调或授权结果处理必须校验输入有效性，并在 provider 返回 `error` / `error_description` 或 token 交换 `invalid_grant` 时把失败原因准确透传回应用日志

### OAuth 配置安全语义

- `EpicOAuth:ClientId` 和 `EpicOAuth:ClientSecret` 当前用于 Epic 公开桌面客户端 OAuth 流程；若该 `ClientSecret` 来源是公开桌面客户端凭据，它不能被当作用户私人 secret 或服务器端机密。
- 不要把私人、用户专属、企业内部或临时调试凭据提交到仓库；需要本机覆盖时使用 `appsettings.Local.json` 或环境变量。
- App 启动配置加载顺序为 `appsettings.json` → 可选 `appsettings.Local.json`，后者只用于本机覆盖，并已被 `.gitignore` 忽略。
- OAuth 字段支持以下环境变量覆盖，环境变量优先级高于配置文件：`HELSINCY_EPIC_OAUTH_CLIENT_ID`、`HELSINCY_EPIC_OAUTH_CLIENT_SECRET`、`HELSINCY_EPIC_OAUTH_REDIRECT_URI`、`HELSINCY_EPIC_OAUTH_EMBEDDED_LOGIN_USER_AGENT`。
- 日志和错误信息不得输出 access token、refresh token、authorization code、exchange code 或包含敏感 query 的完整 URL。

### 谁可以依赖 Auth

| 模块 | 用途 |
|------|------|
| Shell | 显示登录状态、用户头像 |
| FabLibrary | 获取 Access Token 调用 Fab API |
| Downloads | 获取 Access Token 下载认证资源 |
| EngineVersions | 获取 Access Token 访问引擎列表 |

---

## API 定义

> 详见 [05-CoreInterfaces.md](../05-CoreInterfaces.md) 第 4 节 `IAuthService`

### 补充：Token 刷新策略

```csharp
/// <summary>
/// Token 存储接口。由 Infrastructure 层实现。
/// </summary>
public interface ITokenStore
{
    Task SaveTokensAsync(TokenPair tokens, CancellationToken ct);
    Task<TokenPair?> LoadTokensAsync(CancellationToken ct);
    Task ClearAsync(CancellationToken ct);
}

public sealed class TokenPair
{
    public string AccessToken { get; init; } = default!;
    public string RefreshToken { get; init; } = default!;
    public DateTime ExpiresAt { get; init; }
}
```

---

## 关键流程

### 首次登录

```
1. 用户点击"登录"按钮
2. ShellViewModel → IAuthService.StartExchangeCodeLoginAsync()
3. DialogService 在 Presentation 中承载 WebView2 登录容器，并通过页面桥接捕获 `exchange_code`
4. ShellViewModel → IAuthService.CompleteLoginAsync({ Kind = ExchangeCode, Payload = code })
5. AuthService 通过 `grant_type=exchange_code` 换取 access_token + refresh_token
6. TokenStore 安全存储 token pair
7. 返回 AuthUserInfo 给 Shell
8. Shell 更新登录状态 UI
9. 若嵌入式登录不可用、失败或用户主动取消，再回退到系统浏览器链路；若浏览器没有自动回到应用，仍可通过显式“继续登录”入口提交 `authorizationCode`、完整回调 URL，或必要时直接粘贴 Epic 返回的 JSON 响应，由 Auth 只提取必需字段完成登录
```

### 启动时会话恢复

```
1. App 启动 Phase 2
2. 调用 IAuthService.TryRestoreSessionAsync()
3. 从 TokenStore 加载缓存的 token pair
4. 检查 access_token 是否过期
   a. 未过期 → 直接使用
   b. 已过期 → 用 refresh_token 刷新
   c. refresh_token 也过期 → 返回失败，需要重新登录
5. 恢复成功 → 更新 Shell 登录状态
6. 恢复失败 → Shell 显示"需要登录"状态
```

### Token 自动刷新

```
1. 任何模块调用 IAuthService.GetAccessTokenAsync()
2. AuthService 检查当前 access_token：
   a. 有效期 > 5 分钟 → 直接返回
   b. 有效期 < 5 分钟 → 主动刷新
   c. 已过期 → 刷新
3. 刷新成功 → 更新 TokenStore，返回新 token
4. 刷新失败 → 发布 SessionExpiredEvent
5. Shell 收到事件 → 提示用户重新登录
```

### 登出

```
1. 用户点击"登出"
2. IAuthService.LogoutAsync()
3. 调用 Epic API 撤销 token（如支持）
4. 清除 TokenStore
5. 清除内存中的会话信息
6. 发布 SessionExpiredEvent
7. Shell 导航到登录界面
```
