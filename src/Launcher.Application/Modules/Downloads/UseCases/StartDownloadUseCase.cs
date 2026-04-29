// Copyright (c) Helsincy. All rights reserved.

using Launcher.Application.Modules.Downloads.Contracts;
using Launcher.Domain.Downloads;
using Launcher.Shared;

namespace Launcher.Application.Modules.Downloads.UseCases;

public sealed class StartDownloadUseCase
{
    private const string InvalidRequestCode = "DOWNLOAD_START_INVALID_REQUEST";

    private readonly IDownloadOrchestrator _orchestrator;

    public StartDownloadUseCase(IDownloadOrchestrator orchestrator)
    {
        ArgumentNullException.ThrowIfNull(orchestrator);
        _orchestrator = orchestrator;
    }

    public async Task<Result<DownloadTaskId>> ExecuteAsync(StartDownloadRequest request, CancellationToken ct = default)
    {
        var validationResult = Validate(request);
        if (validationResult.IsFailure)
        {
            return Result.Fail<DownloadTaskId>(validationResult.Error!);
        }

        return await _orchestrator.EnqueueAsync(request, ct);
    }

    private static Result Validate(StartDownloadRequest? request)
    {
        if (request is null)
        {
            return Result.Fail(InvalidRequestCode, "下载请求不能为空");
        }

        if (string.IsNullOrWhiteSpace(request.AssetId))
        {
            return Result.Fail(InvalidRequestCode, "资产 ID 不能为空");
        }

        if (string.IsNullOrWhiteSpace(request.AssetName))
        {
            return Result.Fail(InvalidRequestCode, "资产名称不能为空");
        }

        if (string.IsNullOrWhiteSpace(request.DownloadUrl))
        {
            return Result.Fail(InvalidRequestCode, "下载地址不能为空");
        }

        if (string.IsNullOrWhiteSpace(request.DestinationPath))
        {
            return Result.Fail(InvalidRequestCode, "下载目标路径不能为空");
        }

        if (request.TotalBytes < 0)
        {
            return Result.Fail(InvalidRequestCode, "下载大小不能为负数");
        }

        return Result.Ok();
    }
}
