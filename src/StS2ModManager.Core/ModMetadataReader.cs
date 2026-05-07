using System.Text.Json;

namespace StS2ModManager.Core;

public sealed class ModMetadataReader
{
    public ModMetadata Read(DirectoryInfo modDirectory)
    {
        ArgumentNullException.ThrowIfNull(modDirectory);

        if (!modDirectory.Exists)
        {
            return ModMetadata.Empty;
        }

        var candidates = modDirectory
            .GetFiles("*.json", SearchOption.TopDirectoryOnly)
            .Select(file => ReadCandidate(file, modDirectory.Name))
            .Where(candidate => candidate is not null && candidate.Metadata.HasAnyValue)
            .Cast<Candidate>()
            .OrderByDescending(candidate => candidate.IsPreferred)
            .ToArray();

        return candidates.Length > 0 ? candidates[0].Metadata : ModMetadata.Empty;
    }

    private static Candidate? ReadCandidate(FileInfo file, string folderName)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(file.FullName));
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var root = document.RootElement;
            var metadata = new ModMetadata(
                GetString(root, "name"),
                GetString(root, "author"),
                GetString(root, "description"),
                GetString(root, "version"));

            var fileNameMatches = string.Equals(Path.GetFileNameWithoutExtension(file.Name), folderName, StringComparison.OrdinalIgnoreCase);
            var idMatches = string.Equals(GetString(root, "id"), folderName, StringComparison.OrdinalIgnoreCase);
            var pckMatches = string.Equals(GetString(root, "pck_name"), folderName, StringComparison.OrdinalIgnoreCase);

            return new Candidate(metadata, fileNameMatches || idMatches || pckMatches);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private sealed record Candidate(ModMetadata Metadata, bool IsPreferred);
}
