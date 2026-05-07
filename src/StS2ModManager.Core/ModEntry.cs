namespace StS2ModManager.Core;

public sealed record ModEntry(
    string Name,
    bool IsEnabled,
    bool HasConflict,
    string SourcePath,
    ModMetadata Metadata);
