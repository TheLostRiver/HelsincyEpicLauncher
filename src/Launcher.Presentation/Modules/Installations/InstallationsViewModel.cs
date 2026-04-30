// Copyright (c) Helsincy. All rights reserved.

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Launcher.Application.Modules.Installations.Contracts;
using Launcher.Presentation.Shell;
using Serilog;

namespace Launcher.Presentation.Modules.Installations;

/// <summary>
/// 已安装资产管理页面 ViewModel。
/// </summary>
public partial class InstallationsViewModel : ObservableObject
{
    private static readonly ILogger Logger = Log.ForContext<InstallationsViewModel>();

    private readonly IInstallReadService _readService;
    private readonly IInstallCommandService _commandService;
    private readonly IIntegrityVerifier _verifier;
    private readonly IInstallationRepository _repository;
    private readonly IDialogService _dialogService;

    public ObservableCollection<InstallItemViewModel> Installations { get; } = [];

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _hasInstallations;
    [ObservableProperty] private int _installCount;
    [ObservableProperty] private string _totalSizeText = string.Empty;
    [ObservableProperty] private bool _hasError;
    [ObservableProperty] private string _errorMessage = string.Empty;

    public InstallationsViewModel(
        IInstallReadService readService,
        IInstallCommandService commandService,
        IIntegrityVerifier verifier,
        IInstallationRepository repository,
        IDialogService dialogService)
    {
        _readService = readService;
        _commandService = commandService;
        _verifier = verifier;
        _repository = repository;
        _dialogService = dialogService;

        Logger.Debug("InstallationsViewModel 已创建");
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        HasError = false;
        IsLoading = true;
        try
        {
            var items = await _readService.GetInstalledAsync(CancellationToken.None);
            Installations.Clear();
            foreach (var summary in items)
                Installations.Add(InstallItemViewModel.FromSummary(summary));

            UpdateAggregates();
            Logger.Information("已安装列表已加载：{Count} 个资产", Installations.Count);
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = "加载已安装列表失败";
            Logger.Error(ex, "加载已安装列表失败");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task VerifyAsync(string assetId)
    {
        var item = FindItem(assetId);
        if (item is null) return;

        item.IsVerifying = true;
        item.StatusText = "正在校验...";

        try
        {
            var manifest = await _repository.GetManifestAsync(assetId, CancellationToken.None);
            if (manifest is null)
            {
                item.StatusText = "清单不存在，无法校验";
                Logger.Warning("Manifest 不存在 {AssetId}", assetId);
                return;
            }

            var progress = new Progress<VerificationProgress>(p =>
            {
                item.StatusText = $"校验中 {p.CheckedFiles}/{p.TotalFiles}";
            });

            var result = await _verifier.VerifyInstallationAsync(item.InstallPath, manifest, progress, CancellationToken.None);
            if (!result.IsSuccess)
            {
                item.StatusText = "校验失败";
                Logger.Error("校验失败 {AssetId}: {Error}", assetId, result.Error?.TechnicalMessage);
                return;
            }

            var report = result.Value!;
            if (report.IsValid)
            {
                item.StatusText = "校验通过";
                item.NeedsRepair = false;
                Logger.Information("校验通过 {AssetId}, {Files} 个文件", assetId, report.TotalFilesChecked);
            }
            else
            {
                var damaged = report.MissingFiles.Count + report.CorruptedFiles.Count;
                item.StatusText = $"发现 {damaged} 个损坏文件";
                item.NeedsRepair = true;
                Logger.Warning("校验发现问题 {AssetId}: 缺失={Missing} 损坏={Corrupted}",
                    assetId, report.MissingFiles.Count, report.CorruptedFiles.Count);
            }
        }
        finally
        {
            item.IsVerifying = false;
        }
    }

    [RelayCommand]
    private async Task RepairAsync(string assetId)
    {
        var item = FindItem(assetId);
        if (item is null) return;

        var confirmed = await _dialogService.ShowConfirmAsync("修复安装", $"确定要修复 {item.AssetName} 吗？", "修复", "取消");
        if (!confirmed) return;

        item.StatusText = "正在修复...";
        var result = await _commandService.RepairAsync(assetId, CancellationToken.None);
        if (result.IsSuccess)
        {
            item.StatusText = "修复完成";
            item.NeedsRepair = false;
            item.Status = InstallStatusKind.Installed;
            Logger.Information("修复完成 {AssetId}", assetId);
        }
        else
        {
            item.StatusText = "修复失败";
            Logger.Error("修复失败 {AssetId}: {Error}", assetId, result.Error?.TechnicalMessage);
        }
    }

    [RelayCommand]
    private async Task UninstallAsync(string assetId)
    {
        var item = FindItem(assetId);
        if (item is null) return;

        var confirmed = await _dialogService.ShowConfirmAsync("卸载确认", $"确定要卸载 {item.AssetName} 吗？\n安装目录将被删除。", "卸载", "取消");
        if (!confirmed) return;

        item.StatusText = "正在卸载...";
        var result = await _commandService.UninstallAsync(assetId, CancellationToken.None);
        if (result.IsSuccess)
        {
            Installations.Remove(item);
            UpdateAggregates();
            Logger.Information("卸载完成 {AssetId}", assetId);
        }
        else
        {
            item.StatusText = "卸载失败";
            Logger.Error("卸载失败 {AssetId}: {Error}", assetId, result.Error?.TechnicalMessage);
        }
    }

    private void UpdateAggregates()
    {
        InstallCount = Installations.Count;
        HasInstallations = Installations.Count > 0;
        TotalSizeText = FormatTotalSize(Installations.Sum(i => i.SizeOnDisk));
    }

    private InstallItemViewModel? FindItem(string assetId)
        => Installations.FirstOrDefault(i => i.AssetId == assetId);

    internal static string FormatTotalSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
    }
}

/// <summary>
/// 已安装资产列表项 ViewModel
/// </summary>
public partial class InstallItemViewModel : ObservableObject
{
    public string AssetId { get; init; } = string.Empty;
    public string AssetName { get; init; } = string.Empty;
    public string InstallPath { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public long SizeOnDisk { get; init; }
    public DateTime InstalledAt { get; init; }

    [ObservableProperty] private InstallStatusKind _status;
    [ObservableProperty] private bool _needsRepair;
    [ObservableProperty] private string _statusText = string.Empty;
    [ObservableProperty] private bool _isVerifying;

    public string SizeText => InstallationsViewModel.FormatTotalSize(SizeOnDisk);
    public string InstalledAtText => InstalledAt.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture);

    public static InstallItemViewModel FromSummary(InstallStatusSummary summary)
        => new()
        {
            AssetId = summary.AssetId,
            AssetName = summary.AssetName,
            InstallPath = summary.InstallPath,
            Version = summary.Version,
            SizeOnDisk = summary.SizeOnDisk,
            InstalledAt = summary.InstalledAt,
            Status = summary.Status,
            NeedsRepair = summary.NeedsRepair,
            StatusText = summary.NeedsRepair ? "需要修复" : summary.Status.ToString(),
        };
}
