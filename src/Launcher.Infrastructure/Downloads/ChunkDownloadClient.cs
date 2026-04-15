// Copyright (c) Helsincy. All rights reserved.

using System.Net.Http.Headers;
using System.Security.Cryptography;
using Launcher.Shared;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Serilog;

namespace Launcher.Infrastructure.Downloads;

/// <summary>
/// 分块下载客户端。HTTP Range 请求 + Polly 重试/断路器。
/// </summary>
public sealed class ChunkDownloadClient
{
    private const int BufferSize = 81920; // 80KB 读写缓冲
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ResiliencePipeline<HttpResponseMessage> _resiliencePipeline;
    private readonly ILogger _logger = Log.ForContext<ChunkDownloadClient>();

    public ChunkDownloadClient(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
        _resiliencePipeline = BuildResiliencePipeline();
    }

    /// <summary>
    /// 下载单个分块（HTTP Range 请求）
    /// </summary>
    public async Task<Result<ChunkDownloadResult>> DownloadChunkAsync(
        ChunkDownloadRequest request,
        IProgress<long>? progress,
        CancellationToken ct)
    {
        try
        {
            var directory = Path.GetDirectoryName(request.DestinationPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            // 检查断点续传：如果临时文件已存在，从已下载位置继续
            long existingBytes = 0;
            if (File.Exists(request.DestinationPath))
            {
                existingBytes = new FileInfo(request.DestinationPath).Length;
            }

            var actualRangeStart = request.RangeStart + existingBytes;
            if (actualRangeStart > request.RangeEnd)
            {
                // 已经下载完了，直接校验
                return await VerifyAndReturn(request, existingBytes);
            }

            using var httpClient = _httpClientFactory.CreateClient("ChunkDownload");

            var response = await _resiliencePipeline.ExecuteAsync(
                async token =>
                {
                    var req = new HttpRequestMessage(HttpMethod.Get, request.Url);
                    req.Headers.Range = new RangeHeaderValue(actualRangeStart, request.RangeEnd);
                    return await httpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, token);
                },
                ct);

            response.EnsureSuccessStatusCode();

            using var contentStream = await response.Content.ReadAsStreamAsync(ct);
            using var fileStream = new FileStream(
                request.DestinationPath,
                existingBytes > 0 ? FileMode.Append : FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                BufferSize,
                useAsync: true);

            var buffer = new byte[BufferSize];
            long totalBytesRead = existingBytes;
            int bytesRead;

            while ((bytesRead = await contentStream.ReadAsync(buffer, ct)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
                totalBytesRead += bytesRead;
                progress?.Report(totalBytesRead - request.RangeStart);
            }

            await fileStream.FlushAsync(ct);

            return await VerifyAndReturn(request, totalBytesRead - request.RangeStart);
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("分块下载已取消: {Path}", request.DestinationPath);
            return Result.Fail<ChunkDownloadResult>(new Error
            {
                Code = "DL_CANCELLED",
                UserMessage = "下载已取消",
                TechnicalMessage = $"Chunk download cancelled: {request.DestinationPath}",
                Severity = ErrorSeverity.Warning
            });
        }
        catch (BrokenCircuitException ex)
        {
            _logger.Warning(ex, "断路器已打开, 分块下载暂停: {Url}", request.Url);
            return Result.Fail<ChunkDownloadResult>(new Error
            {
                Code = "DL_CIRCUIT_OPEN",
                UserMessage = "服务端暂时不可用，请稍后重试",
                TechnicalMessage = $"Circuit breaker open for {request.Url}: {ex.Message}",
                CanRetry = true,
                Severity = ErrorSeverity.Warning
            });
        }
        catch (HttpRequestException ex)
        {
            _logger.Error(ex, "分块下载 HTTP 错误: {Url}, Range {Start}-{End}",
                request.Url, request.RangeStart, request.RangeEnd);
            return Result.Fail<ChunkDownloadResult>(new Error
            {
                Code = "DL_HTTP_ERROR",
                UserMessage = "下载失败，网络错误",
                TechnicalMessage = $"HTTP error: {ex.Message}",
                CanRetry = true,
                Severity = ErrorSeverity.Error
            });
        }
        catch (IOException ex)
        {
            _logger.Error(ex, "分块下载 IO 错误: {Path}", request.DestinationPath);
            return Result.Fail<ChunkDownloadResult>(new Error
            {
                Code = "DL_IO_ERROR",
                UserMessage = "磁盘写入失败",
                TechnicalMessage = $"IO error: {ex.Message}",
                CanRetry = false,
                Severity = ErrorSeverity.Error
            });
        }
    }

    private static async Task<Result<ChunkDownloadResult>> VerifyAndReturn(
        ChunkDownloadRequest request, long bytesDownloaded)
    {
        string actualHash = string.Empty;
        bool hashMatch = true;

        if (!string.IsNullOrEmpty(request.ExpectedHash) && File.Exists(request.DestinationPath))
        {
            actualHash = await ComputeHashAsync(request.DestinationPath);
            hashMatch = string.Equals(actualHash, request.ExpectedHash, StringComparison.OrdinalIgnoreCase);
        }

        return Result.Ok(new ChunkDownloadResult
        {
            BytesDownloaded = bytesDownloaded,
            ActualHash = actualHash,
            HashMatch = hashMatch,
        });
    }

    private static async Task<string> ComputeHashAsync(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        var hashBytes = await SHA256.HashDataAsync(stream);
        return Convert.ToHexString(hashBytes);
    }

    /// <summary>
    /// 构建 Polly 韧性管道：重试（指数退避 + 抖动）+ 断路器
    /// </summary>
    private static ResiliencePipeline<HttpResponseMessage> BuildResiliencePipeline()
    {
        return new ResiliencePipelineBuilder<HttpResponseMessage>()
            // 重试策略：5 次，指数退避 1s→2s→4s→8s→16s，±20% 抖动
            .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
            {
                MaxRetryAttempts = 5,
                DelayGenerator = args =>
                {
                    var baseDelay = TimeSpan.FromSeconds(Math.Pow(2, args.AttemptNumber)); // 1, 2, 4, 8, 16
                    var jitter = baseDelay * (Random.Shared.NextDouble() * 0.4 - 0.2);   // ±20%
                    return ValueTask.FromResult<TimeSpan?>(baseDelay + jitter);
                },
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .Handle<HttpRequestException>()
                    .Handle<TaskCanceledException>(ex => ex.CancellationToken == default || !ex.CancellationToken.IsCancellationRequested)
                    .HandleResult(r => r.StatusCode == System.Net.HttpStatusCode.TooManyRequests
                                    || r.StatusCode >= System.Net.HttpStatusCode.InternalServerError),
            })
            // 断路器：连续 5 次失败后打开 30 秒
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions<HttpResponseMessage>
            {
                FailureRatio = 0.8,
                MinimumThroughput = 5,
                SamplingDuration = TimeSpan.FromSeconds(30),
                BreakDuration = TimeSpan.FromSeconds(30),
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .Handle<HttpRequestException>()
                    .HandleResult(r => r.StatusCode >= System.Net.HttpStatusCode.InternalServerError),
            })
            .Build();
    }
}
