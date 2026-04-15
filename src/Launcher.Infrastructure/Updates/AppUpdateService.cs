// Copyright (c) Helsincy. All rights reserved.

using Launcher.Application.Modules.Updates.Contracts;
using Launcher.Shared;
using Launcher.Shared.Configuration;
using Serilog;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Launcher.Infrastructure.Updates;

/// <summary>
/// 启动器自身版本更新服务。
/// 通过自定义 API（GitHub Releases API 格式）检查最新版本，
/// 下载更新包到临时目录，通过外部更新器进程完成替换。
/// AppUpdateWorker 检测到新版本后调用 NotifyUpdateAvailable() 触发 UpdateAvailable 事件。
/// Shell 层只订阅 IAppUpdateService.UpdateAvailable，不与后台层直接耦合。
/// </summary>
public sealed class AppUpdateService : IAppUpdateService, IInternalUpdateNotifier
{
    /// <inheritdoc />
    public event Action<UpdateAvailableEvent>? UpdateAvailable;

    /// <inheritdoc />
    internal void NotifyUpdateAvailable(UpdateAvailableEvent evt)
        => UpdateAvailable?.Invoke(evt);

    void IInternalUpdateNotifier.NotifyUpdateAvailable(UpdateAvailableEvent evt)
        => NotifyUpdateAvailable(evt);

    // 更新检查 API（GitHub Releases JSON API 格式）
    private const string UpdateApiUrl =
        "https://api.github.com/repos/TheLostRiver/HelsincyEpicLauncher/releases/latest";

    // 已跳过版本列表的本地持久化路径
    private readonly string _skippedVersionsFile;

    // 下载中的更新包临时路径（ApplyUpdateAsync 中读取）
    private string? _downloadedPackagePath;

    private readonly HttpClient _httpClient;
    private readonly IAppConfigProvider _configProvider;
    private readonly ILogger _logger = Log.ForContext<AppUpdateService>();

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public AppUpdateService(IHttpClientFactory httpClientFactory, IAppConfigProvider configProvider)
    {
        _httpClient = httpClientFactory.CreateClient("UpdateApi");
        _configProvider = configProvider;
        _skippedVersionsFile = Path.Combine(configProvider.DataPath, "skipped_versions.json");
    }

    /// <inheritdoc />
    public async Task<Result<UpdateInfo?>> CheckForUpdateAsync(CancellationToken ct)
    {
        _logger.Information("开始检查更新 | 当前版本={CurrentVersion}", _configProvider.AppVersion);
        try
        {
            var dto = await _httpClient.GetFromJsonAsync<GitHubReleaseDto>(
                UpdateApiUrl, _jsonOptions, ct);

            if (dto is null)
            {
                _logger.Warning("更新 API 返回空响应");
                return Result.Ok<UpdateInfo?>(null);
            }

            // 比较版本号（去掉前缀 'v'）
            var latestVersion = dto.TagName?.TrimStart('v') ?? string.Empty;
            var currentVersion = _configProvider.AppVersion;

            if (!IsNewerVersion(latestVersion, currentVersion))
            {
                _logger.Information("已是最新版本 | 版本={Version}", currentVersion);
                return Result.Ok<UpdateInfo?>(null);
            }

            // 检查是否被用户跳过
            if (await IsVersionSkippedAsync(latestVersion))
            {
                _logger.Information("版本已被用户跳过 | 版本={Version}", latestVersion);
                return Result.Ok<UpdateInfo?>(null);
            }

            // 查找 Windows 更新包资产
            var asset = dto.Assets?.FirstOrDefault(a =>
                a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) &&
                a.Name.Contains("win", StringComparison.OrdinalIgnoreCase));

            if (asset is null)
            {
                _logger.Warning("发布版本中未找到 Windows 更新包 | 版本={Version}", latestVersion);
                return Result.Ok<UpdateInfo?>(null);
            }

            var info = new UpdateInfo
            {
                Version = latestVersion,
                DownloadUrl = asset.BrowserDownloadUrl,
                DownloadSize = asset.Size,
                ReleaseNotes = dto.Body ?? string.Empty,
                ReleaseDate = dto.PublishedAt,
                IsMandatory = dto.Body?.Contains("[MANDATORY]", StringComparison.OrdinalIgnoreCase) == true,
            };

            _logger.Information("发现新版本 | 版本={NewVersion} | 强制={IsMandatory}",
                latestVersion, info.IsMandatory);

            return Result.Ok<UpdateInfo?>(info);
        }
        catch (HttpRequestException ex)
        {
            _logger.Warning(ex, "检查更新时网络请求失败");
            return Result.Ok<UpdateInfo?>(null); // 网络故障时静默失败，不影响主功能
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "检查更新时发生未预期错误");
            return Result.Fail<UpdateInfo?>(new Shared.Error
            {
                Code = "UPDATE_CHECK_FAILED",
                UserMessage = "检查更新失败，请稍后重试。",
                TechnicalMessage = ex.Message,
                CanRetry = true,
                Severity = Shared.ErrorSeverity.Warning,
            });
        }
    }

    /// <inheritdoc />
    public async Task<Result> DownloadUpdateAsync(
        UpdateInfo update,
        IProgress<double>? progress,
        CancellationToken ct)
    {
        _logger.Information("开始下载更新包 | 版本={Version} | 大小={Size} bytes",
            update.Version, update.DownloadSize);

        var tempDir = Path.Combine(Path.GetTempPath(), Launcher.Shared.AppConstants.AppName, "updates");
        Directory.CreateDirectory(tempDir);

        var fileName = $"HelsincyEpicLauncher_{update.Version}.zip";
        var destPath = Path.Combine(tempDir, fileName);

        try
        {
            using var response = await _httpClient.GetAsync(
                update.DownloadUrl,
                HttpCompletionOption.ResponseHeadersRead, ct);

            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? update.DownloadSize;
            await using var contentStream = await response.Content.ReadAsStreamAsync(ct);
            await using var fileStream = new FileStream(destPath, FileMode.Create, FileAccess.Write,
                FileShare.None, bufferSize: 81920, useAsync: true);

            var buffer = new byte[81920];
            long bytesRead = 0;
            int read;

            while ((read = await contentStream.ReadAsync(buffer, ct)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, read), ct);
                bytesRead += read;

                if (totalBytes > 0)
                    progress?.Report((double)bytesRead / totalBytes);
            }

            _downloadedPackagePath = destPath;
            _logger.Information("更新包下载完成 | 路径={Path}", destPath);
            return Result.Ok();
        }
        catch (OperationCanceledException)
        {
            _logger.Information("更新包下载已取消");
            return Result.Fail(new Shared.Error
            {
                Code = "UPDATE_DOWNLOAD_CANCELLED",
                UserMessage = "更新下载已取消。",
                TechnicalMessage = "OperationCanceled",
                CanRetry = true,
                Severity = Shared.ErrorSeverity.Warning,
            });
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "下载更新包失败 | 版本={Version}", update.Version);
            return Result.Fail(new Shared.Error
            {
                Code = "UPDATE_DOWNLOAD_FAILED",
                UserMessage = "更新包下载失败，请检查网络连接后重试。",
                TechnicalMessage = ex.Message,
                CanRetry = true,
                Severity = Shared.ErrorSeverity.Error,
            });
        }
    }

    /// <inheritdoc />
    public async Task<Result> ApplyUpdateAsync(CancellationToken ct)
    {
        if (_downloadedPackagePath is null || !File.Exists(_downloadedPackagePath))
        {
            return Result.Fail(new Shared.Error
            {
                Code = "UPDATE_PACKAGE_MISSING",
                UserMessage = "更新包不存在，请重新下载。",
                TechnicalMessage = $"Package not found at: {_downloadedPackagePath}",
                CanRetry = true,
                Severity = Shared.ErrorSeverity.Error,
            });
        }

        try
        {
            // 生成更新脚本：等待当前进程退出后解压替换文件
            var appDir = AppContext.BaseDirectory;
            var updaterScript = GenerateUpdaterScript(_downloadedPackagePath, appDir);
            var scriptPath = Path.Combine(Path.GetTempPath(), "helsincy_updater.ps1");

            await File.WriteAllTextAsync(scriptPath, updaterScript, ct);

            _logger.Information("启动更新脚本 | 脚本={Script} | 目标目录={AppDir}",
                scriptPath, appDir);

            // 启动 PowerShell 执行更新脚本（等待当前进程退出后运行）
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-ExecutionPolicy Bypass -WindowStyle Hidden -File \"{scriptPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            System.Diagnostics.Process.Start(startInfo);

            // 延迟后退出，让更新脚本有时间启动
            await Task.Delay(500, ct);

            _logger.Information("应用即将退出以完成更新");
            // 使用 Environment.Exit 退出进程，避免 Infrastructure 层引用 WinUI API
            Environment.Exit(0);

            return Result.Ok();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "启动更新器失败");
            return Result.Fail(new Shared.Error
            {
                Code = "UPDATE_APPLY_FAILED",
                UserMessage = "启动更新器失败，请手动重新安装。",
                TechnicalMessage = ex.Message,
                CanRetry = false,
                Severity = Shared.ErrorSeverity.Error,
            });
        }
    }

    /// <inheritdoc />
    public async Task SkipVersionAsync(string version, CancellationToken ct)
    {
        var skipped = await LoadSkippedVersionsAsync();
        if (!skipped.Contains(version))
        {
            skipped.Add(version);
            var json = JsonSerializer.Serialize(skipped);
            await File.WriteAllTextAsync(_skippedVersionsFile, json, ct);
            _logger.Information("已跳过版本 | 版本={Version}", version);
        }
    }

    // ── 私有辅助方法 ────────────────────────────────────────────

    private static bool IsNewerVersion(string latest, string current)
    {
        if (!Version.TryParse(latest, out var latestVer)) return false;
        if (!Version.TryParse(current, out var currentVer)) return false;
        return latestVer > currentVer;
    }

    private async Task<bool> IsVersionSkippedAsync(string version)
    {
        var skipped = await LoadSkippedVersionsAsync();
        return skipped.Contains(version);
    }

    private async Task<List<string>> LoadSkippedVersionsAsync()
    {
        if (!File.Exists(_skippedVersionsFile))
            return [];

        try
        {
            var json = await File.ReadAllTextAsync(_skippedVersionsFile);
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static string GenerateUpdaterScript(string packagePath, string appDir)
    {
        // 用 String.Format 构建 PowerShell 脚本，避免 raw string literal 与 PS {} 语法冲突
        var pid = Environment.ProcessId;
        return string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            """
            $currentPid = {0}
            $timeout = 30
            $elapsed = 0

            # 等待当前进程退出（最多 30 秒）
            while ($elapsed -lt $timeout) {{
                $running = Get-Process -Id $currentPid -ErrorAction SilentlyContinue
                if (-not $running) {{ break }}
                Start-Sleep -Seconds 1
                $elapsed++
            }}

            # 解压更新包到应用目录（覆盖现有文件）
            try {{
                Expand-Archive -Path '{1}' -DestinationPath '{2}' -Force
                Write-EventLog -LogName Application -Source "HelsincyEpicLauncher" -EventId 1 -Message "更新安装成功" -ErrorAction SilentlyContinue
            }} catch {{
                Write-EventLog -LogName Application -Source "HelsincyEpicLauncher" -EventId 2 -Message "更新安装失败" -ErrorAction SilentlyContinue
            }}

            # 清理临时包
            Remove-Item -Path '{1}' -Force -ErrorAction SilentlyContinue

            # 重新启动应用
            $appExe = Join-Path '{2}' 'Launcher.App.exe'
            if (Test-Path $appExe) {{
                Start-Process $appExe
            }}
            """, pid, packagePath, appDir);
    }

    // ── 内部 GitHub API DTO ─────────────────────────────────────

    private sealed class GitHubReleaseDto
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; init; }

        [JsonPropertyName("body")]
        public string? Body { get; init; }

        [JsonPropertyName("published_at")]
        public DateTime PublishedAt { get; init; }

        [JsonPropertyName("assets")]
        public List<AssetDto>? Assets { get; init; }
    }

    private sealed class AssetDto
    {
        [JsonPropertyName("name")]
        public string Name { get; init; } = string.Empty;

        [JsonPropertyName("browser_download_url")]
        public string BrowserDownloadUrl { get; init; } = string.Empty;

        [JsonPropertyName("size")]
        public long Size { get; init; }
    }
}
