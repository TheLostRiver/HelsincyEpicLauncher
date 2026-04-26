// Copyright (c) Helsincy. All rights reserved.

namespace Launcher.Presentation.Modules.FabLibrary;

/// <summary>
/// Fab 列表页会话快照的进程内单槽位存储。
/// </summary>
internal sealed class InMemoryFabLibrarySessionStateStore : IFabLibrarySessionStateStore
{
    private readonly object _syncRoot = new();
    private FabLibrarySessionSnapshot? _snapshot;

    public void Save(FabLibrarySessionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        lock (_syncRoot)
        {
            _snapshot = snapshot;
        }
    }

    public bool TryGet(out FabLibrarySessionSnapshot? snapshot)
    {
        lock (_syncRoot)
        {
            snapshot = _snapshot;
            return snapshot is not null;
        }
    }

    public void Clear()
    {
        lock (_syncRoot)
        {
            _snapshot = null;
        }
    }

    public void Trim()
    {
        // 当前实现是单槽位进程内快照，暂时没有可裁剪内容。
    }
}