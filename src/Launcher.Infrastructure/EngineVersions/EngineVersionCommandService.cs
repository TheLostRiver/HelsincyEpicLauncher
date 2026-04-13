// Copyright (c) Helsincy. All rights reserved.

using System.Diagnostics;
using Launcher.Application.Modules.Downloads.Contracts;
using Launcher.Application.Modules.EngineVersions.Contracts;
using Launcher.Application.Modules.Installations.Contracts;
using Launcher.Shared;
using Serilog;

namespace Launcher.Infrastructure.EngineVersions;

/// <summary>
/// 引擎版本操作服务实现。下载安装、卸载、启动编辑器。
/// </summary>
public sealed class EngineVersionCommandService : IEngineVersionCommandService
{
    private static readonly ILogger Logger = Log.ForContext<EngineVersionCommandService>();

    private readonly EngineVersionApiClient _apiClient;
    private readonly IDownloadCommandService _downloadCommandService;
    private readonly IInstallCommandService _installCommandService;
    private readonly IEngineVersionReadService _readService;

    public EngineVersionCommandService(
        EngineVersionApiClient apiClient,
        IDownloadCommandService downloadCommandService,
        IInstallCommandService installCommandService,
        IEngineVersionReadService readService)
    {
        _apiClient = apiClient;
        _downloadCommandService = downloadCommandService;
        _installCommandService = installCommandService;
        _readService = readService;
    }

    public async Task<Result> DownloadAndInstallAsync(string versionId, string installPath, CancellationToken ct)
    {
        Logger.Information("发起引擎下载 {VersionId} → {Path}", versionId, installPath);

        // 获取可用版本列表找到下载 URL
        var versionsResult = await _apiClient.GetAvailableVersionsAsync(ct);
        if (!versionsResult.IsSuccess)
            return Result.Fail(versionsResult.Error!);

        var versionDto = versionsResult.Value!.Versions
            .FirstOrDefault(v => v.VersionId == versionId);

        if (versionDto is null)
        {
            return Result.Fail(new Error
            {
                Code = "ENGINE_NOT_FOUND",
                UserMessage = $"未找到引擎版本 {versionId}",
                TechnicalMessage = $"VersionId {versionId} not in available versions list",
                CanRetry = false,
                Severity = ErrorSeverity.Warning,
            });
        }

        // 创建下载任务（复用 Downloads 模块）
        var request = new StartDownloadRequest
        {
            AssetId = versionId,
            AssetName = $"Unreal Engine {versionDto.DisplayName}",
            DownloadUrl = versionDto.DownloadUrl,
            DestinationPath = installPath,
            TotalBytes = versionDto.DownloadSize,
            Priority = 10, // 引擎下载高优先级
        };

        var startResult = await _downloadCommandService.StartAsync(request, ct);
        if (!startResult.IsSuccess)
        {
            Logger.Error("引擎下载任务创建失败 {VersionId}: {Error}", versionId, startResult.Error?.TechnicalMessage);
            return Result.Fail(startResult.Error!);
        }

        Logger.Information("引擎下载已创建 {VersionId}, TaskId={TaskId}", versionId, startResult.Value);
        return Result.Ok();
    }

    public async Task<Result> UninstallAsync(string versionId, CancellationToken ct)
    {
        Logger.Information("卸载引擎 {VersionId}", versionId);
        return await _installCommandService.UninstallAsync(versionId, ct);
    }

    public async Task<Result> LaunchEditorAsync(string versionId, CancellationToken ct)
    {
        Logger.Information("启动引擎编辑器 {VersionId}", versionId);

        // 查找已安装路径
        var installedResult = await _readService.GetInstalledVersionsAsync(ct);
        if (!installedResult.IsSuccess)
            return Result.Fail(installedResult.Error!);

        var installed = installedResult.Value!.FirstOrDefault(v => v.VersionId == versionId);
        if (installed is null)
        {
            return Result.Fail(new Error
            {
                Code = "ENGINE_NOT_INSTALLED",
                UserMessage = $"引擎 {versionId} 未安装",
                TechnicalMessage = $"VersionId {versionId} not found in installed list",
                CanRetry = false,
                Severity = ErrorSeverity.Warning,
            });
        }

        // 查找编辑器可执行文件
        var editorPath = Path.Combine(installed.InstallPath, "Engine", "Binaries", "Win64", "UnrealEditor.exe");
        if (!File.Exists(editorPath))
        {
            return Result.Fail(new Error
            {
                Code = "ENGINE_EDITOR_NOT_FOUND",
                UserMessage = "编辑器可执行文件不存在",
                TechnicalMessage = $"Path: {editorPath}",
                CanRetry = false,
                Severity = ErrorSeverity.Error,
            });
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = editorPath,
                UseShellExecute = true,
            });

            Logger.Information("编辑器已启动 {VersionId}: {Path}", versionId, editorPath);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "启动编辑器失败 {VersionId}", versionId);
            return Result.Fail(new Error
            {
                Code = "ENGINE_LAUNCH_ERROR",
                UserMessage = "启动编辑器失败",
                TechnicalMessage = ex.Message,
                CanRetry = true,
                Severity = ErrorSeverity.Error,
            });
        }
    }
}
