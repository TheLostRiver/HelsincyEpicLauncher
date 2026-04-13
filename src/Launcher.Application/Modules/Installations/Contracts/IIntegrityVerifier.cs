// Copyright (c) Helsincy. All rights reserved.

using Launcher.Domain.Installations;
using Launcher.Shared;

namespace Launcher.Application.Modules.Installations.Contracts;

/// <summary>
/// 文件完整性校验服务。
/// </summary>
public interface IIntegrityVerifier
{
    /// <summary>校验单个文件哈希</summary>
    Task<Result<bool>> VerifyFileAsync(string filePath, string expectedHash, CancellationToken ct);

    /// <summary>校验整个安装目录</summary>
    Task<Result<VerificationReport>> VerifyInstallationAsync(
        string installPath,
        InstallManifest manifest,
        IProgress<VerificationProgress>? progress,
        CancellationToken ct);
}
