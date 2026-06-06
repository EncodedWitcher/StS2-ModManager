namespace StS2ModManager.Core;

/// <summary>
/// 解析后的待安装 MOD。<see cref="RootPath"/> 指向真正包含 MOD 文件的目录，
/// 如果来源是压缩包，<see cref="Dispose"/> 会清理解压出来的临时目录。
/// </summary>
public sealed class ResolvedModPackage : IDisposable
{
    private readonly string? _tempDirectory;

    internal ResolvedModPackage(string rootPath, string suggestedName, ModMetadata metadata, string? tempDirectory)
    {
        RootPath = rootPath;
        SuggestedName = suggestedName;
        Metadata = metadata;
        _tempDirectory = tempDirectory;
    }

    public string RootPath { get; }

    public string SuggestedName { get; }

    public ModMetadata Metadata { get; }

    public void Dispose()
    {
        if (string.IsNullOrWhiteSpace(_tempDirectory) || !Directory.Exists(_tempDirectory))
        {
            return;
        }

        try
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
