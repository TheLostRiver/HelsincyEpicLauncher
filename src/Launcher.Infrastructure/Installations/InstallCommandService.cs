// Copyright (c) Helsincy. All rights reserved.

using Launcher.Application.Modules.Installations.Contracts;
using Launcher.Domain.Installations;
using Launcher.Shared;
using Serilog;

namespace Launcher.Infrastructure.Installations;

/// <summary>
/// 安装命令服务实现。编排安装/卸载/修复流程。
/// </summary>
public sealed class InstallCommandService : IInstallCommandService
{
    private readonly IInstallationRepository _repository;
    private readonly InstallWorker _worker;
    private readonly ILogger _logger = Log.ForContext<InstallCommandService>();

    /// <summary>安装完成事件</summary>
    public event Action<InstallationCompletedEvent>? InstallationCompleted;

    /// <summary>安装失败事件</summary>
    public event Action<InstallationFailedEvent>? InstallationFailed;

    /// <summary>卸载完成事件</summary>
    public event Action<UninstallCompletedEvent>? UninstallCompleted;

    public InstallCommandService(IInstallationRepository repository, InstallWorker worker)
    {
        _repository = repository;
        _worker = worker;
    }

    public async Task<Result> InstallAsync(InstallRequest request, CancellationToken ct)
    {
        _logger.Information("开始安装流程 {AssetId} → {InstallPath}", request.AssetId, request.InstallPath);

        // 检查是否已安装
        var existing = await _repository.GetByAssetIdAsync(request.AssetId, ct);
        if (existing is not null && existing.State == InstallState.Installed)
        {
            return Result.Fail(new Error
            {
                Code = "INSTALL_ALREADY_EXISTS",
                UserMessage = "该资产已安装",
                TechnicalMessage = $"资产 {request.AssetId} 已存在安装记录",
                CanRetry = false,
                Severity = ErrorSeverity.Warning,
            });
        }

        // 创建安装记录
        var installation = existing ?? new Installation(
            request.AssetId,
            request.AssetName,
            request.Version,
            request.InstallPath,
            request.AssetType);

        var transitionResult = installation.TransitionTo(InstallState.Installing);
        if (!transitionResult.IsSuccess)
            return transitionResult;

        if (existing is null)
            await _repository.InsertAsync(installation, ct);
        else
            await _repository.UpdateAsync(installation, ct);

        // 执行安装
        var installResult = await _worker.ExecuteAsync(installation, request.SourcePath, null, ct);

        if (installResult.IsSuccess)
        {
            installation.TransitionTo(InstallState.Installed);
            await _repository.UpdateAsync(installation, ct);
            InstallationCompleted?.Invoke(new InstallationCompletedEvent(request.AssetId, request.InstallPath));
            _logger.Information("安装成功 {AssetId}", request.AssetId);
        }
        else
        {
            installation.TransitionTo(InstallState.Failed);
            installation.SetError(installResult.Error?.TechnicalMessage ?? "Unknown error");
            await _repository.UpdateAsync(installation, ct);
            InstallationFailed?.Invoke(new InstallationFailedEvent(request.AssetId, installResult.Error?.UserMessage ?? "安装失败"));
            _logger.Warning("安装失败 {AssetId}: {Error}", request.AssetId, installResult.Error?.TechnicalMessage);
        }

        return installResult;
    }

    public async Task<Result> UninstallAsync(string assetId, CancellationToken ct)
    {
        _logger.Information("开始卸载 {AssetId}", assetId);

        var installation = await _repository.GetByAssetIdAsync(assetId, ct);
        if (installation is null)
        {
            return Result.Fail(new Error
            {
                Code = "INSTALL_NOT_FOUND",
                UserMessage = "未找到该资产的安装记录",
                TechnicalMessage = $"AssetId {assetId} 不存在",
                CanRetry = false,
                Severity = ErrorSeverity.Warning,
            });
        }

        var transitionResult = installation.TransitionTo(InstallState.Uninstalling);
        if (!transitionResult.IsSuccess)
            return transitionResult;

        await _repository.UpdateAsync(installation, ct);

        try
        {
            // 删除安装目录
            if (Directory.Exists(installation.InstallPath))
            {
                Directory.Delete(installation.InstallPath, recursive: true);
                _logger.Debug("已删除安装目录 {Path}", installation.InstallPath);
            }

            // 删除 Manifest
            await _repository.DeleteManifestAsync(assetId, ct);

            // 删除数据库记录
            await _repository.DeleteAsync(installation.Id, ct);

            UninstallCompleted?.Invoke(new UninstallCompletedEvent(assetId));
            _logger.Information("卸载完成 {AssetId}", assetId);
            return Result.Ok();
        }
        catch (IOException ex)
        {
            installation.TransitionTo(InstallState.Failed);
            installation.SetError(ex.Message);
            await _repository.UpdateAsync(installation, ct);

            _logger.Error(ex, "卸载失败 {AssetId}", assetId);
            return Result.Fail(new Error
            {
                Code = "UNINSTALL_IO_ERROR",
                UserMessage = "卸载时发生错误，请关闭正在使用该文件夹的程序后重试",
                TechnicalMessage = ex.Message,
                CanRetry = true,
                Severity = ErrorSeverity.Error,
            });
        }
    }

    public async Task<Result> RepairAsync(string assetId, CancellationToken ct)
    {
        _logger.Information("开始修复 {AssetId}", assetId);

        var installation = await _repository.GetByAssetIdAsync(assetId, ct);
        if (installation is null)
        {
            return Result.Fail(new Error
            {
                Code = "INSTALL_NOT_FOUND",
                UserMessage = "未找到该资产的安装记录",
                TechnicalMessage = $"AssetId {assetId} 不存在",
                CanRetry = false,
                Severity = ErrorSeverity.Warning,
            });
        }

        var transitionResult = installation.TransitionTo(InstallState.Repairing);
        if (!transitionResult.IsSuccess)
            return transitionResult;

        await _repository.UpdateAsync(installation, ct);

        // TODO: Task 5.2 将实现完整的修复逻辑（校验 + 重新下载损坏文件）
        // 当前仅做为桩方法，等待 IntegrityVerifier 实现后集成

        installation.TransitionTo(InstallState.Installed);
        installation.ClearError();
        await _repository.UpdateAsync(installation, ct);

        _logger.Information("修复完成 {AssetId}（桩方法）", assetId);
        return Result.Ok();
    }
}
