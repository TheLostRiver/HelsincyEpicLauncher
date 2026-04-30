// Copyright (c) Helsincy. All rights reserved.

using Serilog;

namespace Launcher.Background.Hosting;

/// <summary>
/// 顺序启动、逆序停止后台 Worker，并隔离单个 Worker 故障。
/// </summary>
public sealed class BackgroundTaskHost : IBackgroundTaskHost
{
    private readonly ILogger _logger = Log.ForContext<BackgroundTaskHost>();
    private readonly Dictionary<string, Exception> _faults = [];

    public BackgroundTaskHost(IEnumerable<IBackgroundWorker> workers)
    {
        Workers = workers.ToArray();
    }

    public IReadOnlyList<IBackgroundWorker> Workers { get; }

    public IReadOnlyDictionary<string, Exception> Faults => _faults;

    public async Task StartAllAsync(CancellationToken ct = default)
    {
        foreach (var worker in Workers)
        {
            try
            {
                ct.ThrowIfCancellationRequested();
                await worker.StartAsync(ct).ConfigureAwait(false);
                _logger.Information("后台 Worker 已启动 | Name={WorkerName}", worker.Name);
            }
            catch (Exception ex)
            {
                RecordFault(worker, ex, "启动");
            }
        }
    }

    public async Task StopAllAsync(CancellationToken ct = default)
    {
        foreach (var worker in Workers.Reverse())
        {
            try
            {
                ct.ThrowIfCancellationRequested();
                await worker.StopAsync(ct).ConfigureAwait(false);
                _logger.Information("后台 Worker 已停止 | Name={WorkerName}", worker.Name);
            }
            catch (Exception ex)
            {
                RecordFault(worker, ex, "停止");
            }
        }
    }

    private void RecordFault(IBackgroundWorker worker, Exception ex, string operation)
    {
        _faults[worker.Name] = ex;
        _logger.Warning(ex, "后台 Worker {Operation}失败 | Name={WorkerName}", operation, worker.Name);
    }
}
