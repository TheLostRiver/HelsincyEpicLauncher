// Copyright (c) Helsincy. All rights reserved.

using Launcher.Background.Hosting;

namespace Launcher.Tests.Unit;

public sealed class BackgroundTaskHostTests
{
    [Fact]
    public async Task StartAllAsync_ShouldStartWorkersInRegistrationOrder()
    {
        var calls = new List<string>();
        var first = new RecordingWorker("first", calls);
        var second = new RecordingWorker("second", calls);
        var host = new BackgroundTaskHost([first, second]);

        await host.StartAllAsync(CancellationToken.None);

        calls.Should().Equal("start:first", "start:second");
        first.State.Should().Be(WorkerStatus.Running);
        second.State.Should().Be(WorkerStatus.Running);
    }

    [Fact]
    public async Task StartAllAsync_ShouldContinueAfterWorkerFailureAndRecordFault()
    {
        var calls = new List<string>();
        var failing = new RecordingWorker("failing", calls, failOnStart: true);
        var second = new RecordingWorker("second", calls);
        var host = new BackgroundTaskHost([failing, second]);

        await host.StartAllAsync(CancellationToken.None);

        calls.Should().Equal("start:failing", "start:second");
        failing.State.Should().Be(WorkerStatus.Faulted);
        second.State.Should().Be(WorkerStatus.Running);
        host.Faults.Should().ContainKey("failing");
    }

    [Fact]
    public async Task StopAllAsync_ShouldStopWorkersInReverseRegistrationOrder()
    {
        var calls = new List<string>();
        var first = new RecordingWorker("first", calls);
        var second = new RecordingWorker("second", calls);
        var host = new BackgroundTaskHost([first, second]);

        await host.StartAllAsync(CancellationToken.None);
        calls.Clear();

        await host.StopAllAsync(CancellationToken.None);

        calls.Should().Equal("stop:second", "stop:first");
        first.State.Should().Be(WorkerStatus.Stopped);
        second.State.Should().Be(WorkerStatus.Stopped);
    }

    private sealed class RecordingWorker : IBackgroundWorker
    {
        private readonly List<string> _calls;
        private readonly bool _failOnStart;

        public RecordingWorker(string name, List<string> calls, bool failOnStart = false)
        {
            Name = name;
            _calls = calls;
            _failOnStart = failOnStart;
        }

        public string Name { get; }

        public WorkerStatus State { get; private set; } = WorkerStatus.Idle;

        public Task StartAsync(CancellationToken ct = default)
        {
            _calls.Add($"start:{Name}");
            if (_failOnStart)
            {
                State = WorkerStatus.Faulted;
                throw new InvalidOperationException($"Failed to start {Name}");
            }

            State = WorkerStatus.Running;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken ct = default)
        {
            _calls.Add($"stop:{Name}");
            State = WorkerStatus.Stopped;
            return Task.CompletedTask;
        }
    }
}
