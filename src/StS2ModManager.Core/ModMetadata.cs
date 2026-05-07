namespace StS2ModManager.Core;

public sealed record ModMetadata(
    string? Name,
    string? Author,
    string? Description,
    string? Version)
{
    public static ModMetadata Empty { get; } = new(null, null, null, null);

    public bool HasAnyValue =>
        !string.IsNullOrWhiteSpace(Name)
        || !string.IsNullOrWhiteSpace(Author)
        || !string.IsNullOrWhiteSpace(Description)
        || !string.IsNullOrWhiteSpace(Version);
}
