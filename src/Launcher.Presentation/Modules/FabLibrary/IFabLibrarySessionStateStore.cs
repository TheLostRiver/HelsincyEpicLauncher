// Copyright (c) Helsincy. All rights reserved.

namespace Launcher.Presentation.Modules.FabLibrary;

/// <summary>
/// Fab 列表页会话快照存储。仅在 Presentation 层内部使用。
/// </summary>
internal interface IFabLibrarySessionStateStore
{
    void Save(FabLibrarySessionSnapshot snapshot);

    bool TryGet(out FabLibrarySessionSnapshot? snapshot);

    void Clear();

    void Trim();
}