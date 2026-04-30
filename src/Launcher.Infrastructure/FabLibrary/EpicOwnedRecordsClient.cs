// Copyright (c) Helsincy. All rights reserved.

using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using Launcher.Application.Modules.Auth.Contracts;
using Launcher.Shared;
using Serilog;

namespace Launcher.Infrastructure.FabLibrary;

/// <summary>
/// Loads Epic library owned Fab records, including paging, cursor continuation, and short-lived cache state.
/// </summary>
internal sealed class EpicOwnedRecordsClient : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);
    private const int CurlConnectTimeoutSeconds = 10;
    private const int CurlMaxTimeSeconds = 900;
    private const int PreviewBufferSize = 1024 * 1024;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IAuthService _authService;
    private readonly ILogger _logger = Log.ForContext<EpicOwnedRecordsClient>();
    private readonly SemaphoreSlim _recordsLock = new(1, 1);

    private IReadOnlyList<OwnedRecord>? _cachedRecords;
    private DateTime _cachedRecordsAt;
    private string? _cachedNextCursor;
    private bool _cachedRecordsComplete;

    public EpicOwnedRecordsClient(IHttpClientFactory httpClientFactory, IAuthService authService)
    {
        _httpClientFactory = httpClientFactory;
        _authService = authService;
    }

    public async Task<Result<IReadOnlyList<OwnedRecord>>> GetOwnedRecordsAsync(CancellationToken ct)
    {
        var snapshotResult = await EnsureOwnedRecordsAsync(OwnedRecordRequirement.Complete, ct);
        if (!snapshotResult.IsSuccess)
        {
            return Result.Fail<IReadOnlyList<OwnedRecord>>(snapshotResult.Error!);
        }

        return Result.Ok<IReadOnlyList<OwnedRecord>>(snapshotResult.Value!.Records);
    }

    public async Task<Result<OwnedRecordWindow>> GetOwnedRecordWindowAsync(int requiredUniqueCount, CancellationToken ct)
    {
        var snapshotResult = await EnsureOwnedRecordsAsync(new OwnedRecordRequirement(RequiredUniqueCount: requiredUniqueCount), ct);
        if (!snapshotResult.IsSuccess)
        {
            return Result.Fail<OwnedRecordWindow>(snapshotResult.Error!);
        }

        return Result.Ok(new OwnedRecordWindow(snapshotResult.Value!.Records, !snapshotResult.Value.IsComplete));
    }

    public async Task<Result<OwnedRecordSnapshot>> EnsureOwnedRecordsAsync(OwnedRecordRequirement requirement, CancellationToken ct)
    {
        if (TryGetOwnedRecordSnapshot(requirement, out var cachedSnapshot))
        {
            return Result.Ok(cachedSnapshot!);
        }

        await _recordsLock.WaitAsync(ct);
        try
        {
            if (TryGetOwnedRecordSnapshot(requirement, out cachedSnapshot))
            {
                return Result.Ok(cachedSnapshot!);
            }

            var tokenResult = await _authService.GetAccessTokenAsync(ct);
            if (!tokenResult.IsSuccess)
            {
                return Result.Fail<OwnedRecordSnapshot>(tokenResult.Error!);
            }

            var records = HasFreshRecordsCache() && _cachedRecords is not null
                ? _cachedRecords.ToList()
                : [];
            var isComplete = HasFreshRecordsCache() && _cachedRecordsComplete;
            var nextCursor = HasFreshRecordsCache() ? _cachedNextCursor : null;
            var didExpandRecords = false;

            if (!HasFreshRecordsCache())
            {
                _cachedNextCursor = null;
                _cachedRecordsComplete = false;
                nextCursor = null;
                isComplete = false;
            }

            if (!CanSatisfyRequirement(records, requirement, isComplete) && !CanContinueFromCursor(nextCursor))
            {
                var previewResult = await LoadOwnedRecordWindowFromStreamAsync(tokenResult.Value!, records, requirement, ct);
                if (!previewResult.IsSuccess)
                {
                    return Result.Fail<OwnedRecordSnapshot>(previewResult.Error!);
                }

                records = previewResult.Value!.Records.ToList();
                nextCursor = previewResult.Value.NextCursor;
                isComplete = previewResult.Value.IsComplete;
                UpdateRecordsCache(records, nextCursor, isComplete);
                didExpandRecords = true;
            }

            while (!CanSatisfyRequirement(records, requirement, isComplete) && CanContinueFromCursor(nextCursor))
            {
                var pageResult = await GetOwnedRecordPageAsync(tokenResult.Value!, nextCursor, ct);
                if (!pageResult.IsSuccess)
                {
                    return Result.Fail<OwnedRecordSnapshot>(pageResult.Error!);
                }

                records = MergeOwnedRecords(records, pageResult.Value!.Records);
                nextCursor = pageResult.Value.ResponseMetadata?.NextCursor;
                isComplete = string.IsNullOrWhiteSpace(nextCursor);
                UpdateRecordsCache(records, nextCursor, isComplete);
                didExpandRecords = true;
            }

            var snapshot = new OwnedRecordSnapshot(_cachedRecords ?? records, _cachedRecordsComplete);
            if (didExpandRecords)
            {
                LogOwnedRecordLoadState(snapshot);
            }

            return Result.Ok(snapshot);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.Error(ex, "读取 Epic library 回退数据异常");
            return Result.Fail<OwnedRecordSnapshot>(new Error
            {
                Code = "FAB_OWNED_LIBRARY_EXCEPTION",
                UserMessage = "读取已拥有 Fab 资产时发生错误",
                TechnicalMessage = ex.Message,
                CanRetry = true,
                Severity = ErrorSeverity.Error,
            });
        }
        finally
        {
            _recordsLock.Release();
        }
    }

    private async Task<Result<OwnedRecordLoadState>> LoadOwnedRecordWindowFromStreamAsync(
        string accessToken,
        IReadOnlyList<OwnedRecord> seedRecords,
        OwnedRecordRequirement requirement,
        CancellationToken ct)
    {
        var httpClient = _httpClientFactory.CreateClient("EpicLibraryApi");
        using var request = new HttpRequestMessage(HttpMethod.Get, "/library/api/public/items?includeMetadata=true");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.Warning("读取 Epic library 预览失败 | StatusCode={Code}", response.StatusCode);
            return Result.Fail<OwnedRecordLoadState>(BuildLibraryReadError($"HTTP {(int)response.StatusCode}: {body}"));
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var buffer = new byte[PreviewBufferSize];
        var state = new JsonReaderState();
        var bytesBuffered = 0;
        var inRecordsArray = false;
        var recordsById = BuildOwnedRecordMap(seedRecords);
        string? nextCursor = null;

        while (true)
        {
            if (bytesBuffered == buffer.Length)
            {
                Array.Resize(ref buffer, buffer.Length * 2);
            }

            var bytesRead = await stream.ReadAsync(buffer.AsMemory(bytesBuffered), ct);
            var totalBytes = bytesBuffered + bytesRead;
            var isFinalBlock = bytesRead == 0;
            var reader = new Utf8JsonReader(buffer.AsSpan(0, totalBytes), isFinalBlock, state);
            var consumedBytes = 0;
            var nextState = state;

            while (true)
            {
                var checkpointState = reader.CurrentState;
                var checkpointConsumed = (int)reader.BytesConsumed;
                if (!reader.Read())
                {
                    consumedBytes = (int)reader.BytesConsumed;
                    nextState = reader.CurrentState;
                    break;
                }

                if (!inRecordsArray)
                {
                    if (reader.TokenType == JsonTokenType.PropertyName && reader.ValueTextEquals("records"u8))
                    {
                        if (!reader.Read())
                        {
                            consumedBytes = checkpointConsumed;
                            nextState = checkpointState;
                            break;
                        }

                        inRecordsArray = reader.TokenType == JsonTokenType.StartArray;
                        consumedBytes = (int)reader.BytesConsumed;
                        nextState = reader.CurrentState;
                        continue;
                    }

                    if (reader.TokenType == JsonTokenType.PropertyName && reader.ValueTextEquals("responseMetadata"u8))
                    {
                        if (!reader.Read())
                        {
                            consumedBytes = checkpointConsumed;
                            nextState = checkpointState;
                            break;
                        }

                        var metadataReader = reader;
                        if (!JsonDocument.TryParseValue(ref metadataReader, out var metadataDocument))
                        {
                            consumedBytes = checkpointConsumed;
                            nextState = checkpointState;
                            break;
                        }

                        reader = metadataReader;
                        using (metadataDocument)
                        {
                            nextCursor = TryGetOptionalStringProperty(metadataDocument.RootElement, "nextCursor");
                        }

                        consumedBytes = (int)reader.BytesConsumed;
                        nextState = reader.CurrentState;
                    }

                    consumedBytes = (int)reader.BytesConsumed;
                    nextState = reader.CurrentState;

                    continue;
                }

                if (reader.TokenType == JsonTokenType.EndArray)
                {
                    inRecordsArray = false;
                    continue;
                }

                if (reader.TokenType != JsonTokenType.StartObject)
                {
                    continue;
                }

                var objectReader = reader;
                if (!JsonDocument.TryParseValue(ref objectReader, out var document))
                {
                    consumedBytes = checkpointConsumed;
                    nextState = checkpointState;
                    break;
                }

                reader = objectReader;
                using (document)
                {
                    if (!TryMapOwnedRecord(document.RootElement, out var record))
                    {
                        consumedBytes = (int)reader.BytesConsumed;
                        nextState = reader.CurrentState;
                        continue;
                    }

                    UpsertOwnedRecord(recordsById, record);

                    if (CanStopPreview(recordsById, requirement))
                    {
                        return Result.Ok(new OwnedRecordLoadState(
                            BuildOrderedOwnedRecords(recordsById.Values),
                            IsComplete: false,
                            NextCursor: null));
                    }
                }

                consumedBytes = (int)reader.BytesConsumed;
                nextState = reader.CurrentState;
            }

            bytesBuffered = totalBytes - consumedBytes;
            if (bytesBuffered > 0)
            {
                Buffer.BlockCopy(buffer, consumedBytes, buffer, 0, bytesBuffered);
            }

            state = nextState;

            if (isFinalBlock)
            {
                break;
            }
        }

        return Result.Ok(new OwnedRecordLoadState(
            BuildOrderedOwnedRecords(recordsById.Values),
            IsComplete: string.IsNullOrWhiteSpace(nextCursor),
            NextCursor: nextCursor));
    }

    private async Task<Result<LibraryResponse>> GetOwnedRecordPageAsync(string accessToken, string? cursor, CancellationToken ct)
    {
        var url = string.IsNullOrWhiteSpace(cursor)
            ? "https://library-service.live.use1a.on.epicgames.com/library/api/public/items?includeMetadata=true"
            : $"https://library-service.live.use1a.on.epicgames.com/library/api/public/items?includeMetadata=true&cursor={Uri.EscapeDataString(cursor)}";

        var bodyResult = await ExecuteCurlGetAsync(url, accessToken, ct);
        if (!bodyResult.IsSuccess)
        {
            return Result.Fail<LibraryResponse>(bodyResult.Error!);
        }

        try
        {
            return Result.Ok(JsonSerializer.Deserialize<LibraryResponse>(bodyResult.Value!, JsonOptions) ?? new LibraryResponse());
        }
        catch (JsonException ex)
        {
            _logger.Warning(ex, "解析 Epic library 响应失败");
            return Result.Fail<LibraryResponse>(new Error
            {
                Code = "FAB_OWNED_LIBRARY_FAILED",
                UserMessage = "读取已拥有 Fab 资产失败",
                TechnicalMessage = ex.Message,
                CanRetry = true,
                Severity = ErrorSeverity.Warning,
            });
        }
    }

    private async Task<Result<string>> ExecuteCurlGetAsync(string url, string accessToken, CancellationToken ct)
    {
        var tempFile = Path.GetTempFileName();
        using var process = new Process();

        try
        {
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "curl.exe",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            process.StartInfo.ArgumentList.Add("--silent");
            process.StartInfo.ArgumentList.Add("--show-error");
            process.StartInfo.ArgumentList.Add("--http1.1");
            process.StartInfo.ArgumentList.Add("--connect-timeout");
            process.StartInfo.ArgumentList.Add(CurlConnectTimeoutSeconds.ToString(CultureInfo.InvariantCulture));
            process.StartInfo.ArgumentList.Add("--max-time");
            process.StartInfo.ArgumentList.Add(CurlMaxTimeSeconds.ToString(CultureInfo.InvariantCulture));
            process.StartInfo.ArgumentList.Add("--output");
            process.StartInfo.ArgumentList.Add(tempFile);
            process.StartInfo.ArgumentList.Add("--write-out");
            process.StartInfo.ArgumentList.Add("%{http_code}");
            process.StartInfo.ArgumentList.Add("--header");
            process.StartInfo.ArgumentList.Add("Accept: application/json");
            process.StartInfo.ArgumentList.Add("--header");
            process.StartInfo.ArgumentList.Add("Accept-Encoding: identity");
            process.StartInfo.ArgumentList.Add("--header");
            process.StartInfo.ArgumentList.Add($"Authorization: Bearer {accessToken}");
            process.StartInfo.ArgumentList.Add(url);

            if (!process.Start())
            {
                return Result.Fail<string>(BuildLibraryReadError("无法启动 curl.exe 读取 Epic library"));
            }

            var standardOutputTask = process.StandardOutput.ReadToEndAsync(ct);
            var standardErrorTask = process.StandardError.ReadToEndAsync(ct);

            try
            {
                await process.WaitForExitAsync(ct);
            }
            catch (OperationCanceledException)
            {
                TryKillProcess(process);
                throw;
            }

            var statusText = (await standardOutputTask).Trim();
            var errorText = (await standardErrorTask).Trim();
            var body = File.Exists(tempFile)
                ? await File.ReadAllTextAsync(tempFile, ct)
                : string.Empty;

            if (process.ExitCode != 0)
            {
                return Result.Fail<string>(BuildLibraryReadError($"curl exited with code {process.ExitCode}: {FirstNonEmpty(errorText, body)}"));
            }

            if (!int.TryParse(statusText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var statusCode))
            {
                return Result.Fail<string>(BuildLibraryReadError(string.IsNullOrWhiteSpace(errorText) ? "curl 未返回有效状态码" : errorText));
            }

            if (statusCode < 200 || statusCode >= 300)
            {
                return Result.Fail<string>(BuildLibraryReadError($"HTTP {statusCode}: {FirstNonEmpty(body, errorText)}"));
            }

            return Result.Ok(body);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.Warning(ex, "使用 curl 读取 Epic library 失败");
            return Result.Fail<string>(BuildLibraryReadError(ex.Message));
        }
        finally
        {
            TryDeleteFile(tempFile);
        }
    }

    private void UpdateRecordsCache(IReadOnlyList<OwnedRecord> records, string? nextCursor, bool isComplete)
    {
        _cachedRecords = records;
        _cachedRecordsAt = DateTime.UtcNow;
        _cachedNextCursor = nextCursor;
        _cachedRecordsComplete = isComplete;
    }

    private bool HasFreshRecordsCache()
    {
        return _cachedRecords is not null && DateTime.UtcNow - _cachedRecordsAt < CacheDuration;
    }

    private bool TryGetOwnedRecordSnapshot(OwnedRecordRequirement requirement, out OwnedRecordSnapshot? snapshot)
    {
        if (!HasFreshRecordsCache() || _cachedRecords is null || !CanSatisfyRequirement(_cachedRecords, requirement, _cachedRecordsComplete))
        {
            snapshot = null;
            return false;
        }

        snapshot = new OwnedRecordSnapshot(_cachedRecords, _cachedRecordsComplete);
        return true;
    }

    private static bool CanSatisfyRequirement(IReadOnlyList<OwnedRecord> records, OwnedRecordRequirement requirement, bool isComplete)
    {
        if (requirement.RequireComplete)
        {
            return isComplete;
        }

        if (requirement.RequiredUniqueCount is int requiredUniqueCount && records.Count < requiredUniqueCount && !isComplete)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(requirement.RequiredAssetId)
            && !ContainsRecord(records, requirement.RequiredAssetId)
            && !isComplete)
        {
            return false;
        }

        return true;
    }

    private static bool CanContinueFromCursor(string? cursor)
    {
        return !string.IsNullOrWhiteSpace(cursor);
    }

    private static bool CanStopPreview(Dictionary<string, OwnedRecord> recordsById, OwnedRecordRequirement requirement)
    {
        if (requirement.RequireComplete)
        {
            return false;
        }

        if (requirement.RequiredUniqueCount is int requiredUniqueCount && recordsById.Count >= requiredUniqueCount)
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(requirement.RequiredAssetId)
            && recordsById.ContainsKey(requirement.RequiredAssetId);
    }

    private static bool ContainsRecord(IEnumerable<OwnedRecord> records, string assetId)
    {
        return records.Any(r => string.Equals(r.CatalogItemId, assetId, StringComparison.OrdinalIgnoreCase));
    }

    private void LogOwnedRecordLoadState(OwnedRecordSnapshot snapshot)
    {
        if (snapshot.IsComplete)
        {
            _logger.Information("Epic library 回退已加载 {Count} 个 Fab 已拥有资产", snapshot.Records.Count);
            return;
        }

        _logger.Information("Epic library 预览已加载 {Count} 个 Fab 已拥有资产 | HasMore=true", snapshot.Records.Count);
    }

    private static Dictionary<string, OwnedRecord> BuildOwnedRecordMap(IEnumerable<OwnedRecord> records)
    {
        var recordsById = new Dictionary<string, OwnedRecord>(StringComparer.OrdinalIgnoreCase);
        foreach (var record in records)
        {
            UpsertOwnedRecord(recordsById, record);
        }

        return recordsById;
    }

    private static void UpsertOwnedRecord(Dictionary<string, OwnedRecord> recordsById, OwnedRecord record)
    {
        if (!recordsById.TryGetValue(record.CatalogItemId, out var existing)
            || record.AcquisitionDate > existing.AcquisitionDate)
        {
            recordsById[record.CatalogItemId] = record;
        }
    }

    private static List<OwnedRecord> BuildOrderedOwnedRecords(IEnumerable<OwnedRecord> records)
    {
        return records
            .OrderByDescending(r => r.AcquisitionDate)
            .ToList();
    }

    private static string? TryGetOptionalStringProperty(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var value = property.GetString();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static List<OwnedRecord> MergeOwnedRecords(IEnumerable<OwnedRecord> existingRecords, IEnumerable<LibraryRecord> incomingRecords)
    {
        return existingRecords
            .Concat(incomingRecords
                .Where(r => string.Equals(r.SandboxName, "fab-listing-live", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(r.RecordType, "APPLICATION", StringComparison.OrdinalIgnoreCase))
                .Select(r => new OwnedRecord(
                    r.Namespace ?? string.Empty,
                    r.CatalogItemId ?? string.Empty,
                    r.AppName ?? string.Empty,
                    r.ProductId ?? string.Empty,
                    r.AcquisitionDate)))
            .Where(r => !string.IsNullOrWhiteSpace(r.Namespace) && !string.IsNullOrWhiteSpace(r.CatalogItemId))
            .GroupBy(r => r.CatalogItemId, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(x => x.AcquisitionDate).First())
            .OrderByDescending(r => r.AcquisitionDate)
            .ToList();
    }

    private static bool TryMapOwnedRecord(JsonElement element, out OwnedRecord record)
    {
        record = default!;

        if (!TryGetStringProperty(element, "sandboxName", out var sandboxName)
            || !string.Equals(sandboxName, "fab-listing-live", StringComparison.OrdinalIgnoreCase)
            || !TryGetStringProperty(element, "recordType", out var recordType)
            || !string.Equals(recordType, "APPLICATION", StringComparison.OrdinalIgnoreCase)
            || !TryGetStringProperty(element, "namespace", out var recordNamespace)
            || !TryGetStringProperty(element, "catalogItemId", out var catalogItemId))
        {
            return false;
        }

        var appName = TryGetStringProperty(element, "appName", out var value)
            ? value
            : string.Empty;
        var productId = TryGetStringProperty(element, "productId", out var parsedProductId)
            ? parsedProductId
            : string.Empty;
        var acquisitionDate = TryGetDateTimeOffsetProperty(element, "acquisitionDate", out var parsed)
            ? parsed
            : DateTimeOffset.MinValue;

        record = new OwnedRecord(recordNamespace, catalogItemId, appName, productId, acquisitionDate);
        return true;
    }

    private static bool TryGetStringProperty(JsonElement element, string propertyName, out string value)
    {
        value = string.Empty;
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(value);
    }

    private static bool TryGetDateTimeOffsetProperty(JsonElement element, string propertyName, out DateTimeOffset value)
    {
        value = default;
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        return DateTimeOffset.TryParse(
            property.GetString(),
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out value);
    }

    private static Error BuildLibraryReadError(string technicalMessage)
    {
        return new Error
        {
            Code = "FAB_OWNED_LIBRARY_FAILED",
            UserMessage = "读取已拥有 Fab 资产失败",
            TechnicalMessage = technicalMessage,
            CanRetry = true,
            Severity = ErrorSeverity.Warning,
        };
    }

    private static string FirstNonEmpty(string first, string second)
    {
        if (!string.IsNullOrWhiteSpace(first))
        {
            return first;
        }

        return string.IsNullOrWhiteSpace(second) ? "empty response body" : second;
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }

    private static void TryKillProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(true);
            }
        }
        catch
        {
        }
    }

    public void Dispose()
    {
        _recordsLock.Dispose();
        GC.SuppressFinalize(this);
    }

    private sealed record OwnedRecordLoadState(IReadOnlyList<OwnedRecord> Records, bool IsComplete, string? NextCursor);

    private sealed class LibraryResponse
    {
        public List<LibraryRecord> Records { get; init; } = [];

        public LibraryResponseMetadata? ResponseMetadata { get; init; }
    }

    private sealed class LibraryResponseMetadata
    {
        public string? NextCursor { get; init; }
    }

    private sealed class LibraryRecord
    {
        public string? Namespace { get; init; }

        public string? CatalogItemId { get; init; }

        public string? AppName { get; init; }

        public string? ProductId { get; init; }

        public string? SandboxName { get; init; }

        public string? RecordType { get; init; }

        public DateTimeOffset AcquisitionDate { get; init; }
    }
}

internal sealed record OwnedRecord(string Namespace, string CatalogItemId, string AppName, string ProductId, DateTimeOffset AcquisitionDate);

internal sealed record OwnedRecordWindow(IReadOnlyList<OwnedRecord> Records, bool HasMore);

internal sealed record OwnedRecordSnapshot(IReadOnlyList<OwnedRecord> Records, bool IsComplete);

internal sealed record OwnedRecordRequirement(int? RequiredUniqueCount = null, string? RequiredAssetId = null, bool RequireComplete = false)
{
    public static OwnedRecordRequirement Complete { get; } = new(RequireComplete: true);
}
