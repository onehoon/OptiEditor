using OptiEditor.Core.Models;

namespace OptiEditor.Core.Ini;

public sealed class IniEditorSession { public required OptiInstallation Installation { get; init; } public required IniDocument Document { get; init; } public required IniFileSnapshot Snapshot { get; init; } }
public sealed class IniEditorSessionService(IIniFileService fileService)
{
    public async Task<IniEditorSession> OpenSessionAsync(OptiInstallation installation, CancellationToken cancellationToken = default)
    { var loaded = await fileService.LoadAsync(installation.IniPath, cancellationToken); return new() { Installation = installation, Document = loaded.Document, Snapshot = loaded.Snapshot }; }
}
