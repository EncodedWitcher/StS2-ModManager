using System.IO.Compression;
using System.Text.Json;

namespace StS2ModManager.Core;

/// <summary>
/// 把用户选择的文件夹或 .zip 压缩包解析成一个可安装的 MOD：
/// 自动定位真正的 MOD 根目录（兼容外层包裹文件夹、压缩包内直接平铺等结构），
/// 并根据 manifest 的 id / pck_name / name 推导出 MOD 文件夹名。
/// </summary>
public sealed class ModImportResolver
{
    private readonly ModMetadataReader _metadataReader;

    public ModImportResolver(ModMetadataReader? metadataReader = null)
    {
        _metadataReader = metadataReader ?? new ModMetadataReader();
    }

    public ResolvedModPackage Resolve(string sourcePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

        string searchRoot;
        string? tempDirectory = null;

        if (Directory.Exists(sourcePath))
        {
            searchRoot = sourcePath;
        }
        else if (File.Exists(sourcePath))
        {
            if (!IsZipArchive(sourcePath))
            {
                throw new NotSupportedException("仅支持文件夹或 .zip 压缩包。");
            }

            tempDirectory = CreateTempDirectory();
            try
            {
                ZipFile.ExtractToDirectory(sourcePath, tempDirectory);
            }
            catch
            {
                SafeDelete(tempDirectory);
                throw;
            }

            searchRoot = tempDirectory;
        }
        else
        {
            throw new DirectoryNotFoundException($"找不到来源：{sourcePath}");
        }

        try
        {
            var modRoot = LocateModRoot(searchRoot);
            var metadata = _metadataReader.Read(new DirectoryInfo(modRoot));
            var name = DeriveName(modRoot, sourcePath, tempDirectory);
            return new ResolvedModPackage(modRoot, name, metadata, tempDirectory);
        }
        catch
        {
            SafeDelete(tempDirectory);
            throw;
        }
    }

    private static string LocateModRoot(string searchRoot)
    {
        var root = new DirectoryInfo(searchRoot);

        // 自浅到深广度优先，找到第一个“看起来像 MOD 根目录”的目录。
        var queue = new Queue<DirectoryInfo>();
        queue.Enqueue(root);
        while (queue.Count > 0)
        {
            var directory = queue.Dequeue();
            if (LooksLikeModRoot(directory))
            {
                return directory.FullName;
            }

            foreach (var sub in directory.GetDirectories().OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase))
            {
                queue.Enqueue(sub);
            }
        }

        // 没有可识别的 manifest：逐层拆掉“只有一个子文件夹”的外层包裹。
        var current = root;
        while (current.GetFiles().Length == 0)
        {
            var subdirectories = current.GetDirectories();
            if (subdirectories.Length != 1)
            {
                break;
            }

            current = subdirectories[0];
        }

        return current.FullName;
    }

    private static bool LooksLikeModRoot(DirectoryInfo directory)
    {
        if (directory.GetFiles("*.pck", SearchOption.TopDirectoryOnly).Length > 0)
        {
            return true;
        }

        return directory
            .GetFiles("*.json", SearchOption.TopDirectoryOnly)
            .Any(file => ReadManifestIdentity(file) is not null);
    }

    private static string DeriveName(string modRoot, string sourcePath, string? tempDirectory)
    {
        var identity = ReadIdentityFromDirectory(modRoot);
        var candidate = identity;

        if (string.IsNullOrWhiteSpace(candidate))
        {
            var isExtractionRoot = tempDirectory is not null && PathsEqual(modRoot, tempDirectory);
            if (!isExtractionRoot)
            {
                candidate = new DirectoryInfo(modRoot).Name;
            }
        }

        if (string.IsNullOrWhiteSpace(candidate))
        {
            var trimmedSource = sourcePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            candidate = Path.GetFileNameWithoutExtension(trimmedSource);
        }

        candidate = Sanitize(candidate);
        return string.IsNullOrWhiteSpace(candidate) ? "NewMod" : candidate;
    }

    private static string? ReadIdentityFromDirectory(string directory)
    {
        var jsonFiles = new DirectoryInfo(directory)
            .GetFiles("*.json", SearchOption.TopDirectoryOnly)
            // 优先采用文件名与目录名一致的 manifest（最贴近游戏期望的命名）。
            .OrderByDescending(file => string.Equals(
                Path.GetFileNameWithoutExtension(file.Name),
                new DirectoryInfo(directory).Name,
                StringComparison.OrdinalIgnoreCase))
            .ThenBy(file => file.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var file in jsonFiles)
        {
            var identity = ReadManifestIdentity(file);
            if (identity is not null)
            {
                return identity;
            }
        }

        return null;
    }

    private static string? ReadManifestIdentity(FileInfo file)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(file.FullName));
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var root = document.RootElement;
            return FirstNonEmpty(
                GetString(root, "id"),
                GetString(root, "pck_name"),
                GetString(root, "name"));
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

    private static string Sanitize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(value.Where(c => Array.IndexOf(invalid, c) < 0).ToArray());
        return cleaned.Trim().Trim('.');
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static bool IsZipArchive(string path)
    {
        return string.Equals(Path.GetExtension(path), ".zip", StringComparison.OrdinalIgnoreCase);
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"StS2ModImport-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static bool PathsEqual(string left, string right)
    {
        return string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)),
            StringComparison.OrdinalIgnoreCase);
    }

    private static void SafeDelete(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return;
        }

        try
        {
            Directory.Delete(directory, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
