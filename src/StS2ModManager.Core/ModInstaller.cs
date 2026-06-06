namespace StS2ModManager.Core;

/// <summary>
/// 负责把解析好的 MOD 安装/更新到 mods、mods_disabled 目录，以及彻底卸载某个 MOD。
/// </summary>
public sealed class ModInstaller
{
    private const string StagingSuffix = ".__updating__";
    private const string BackupSuffix = ".__old__";

    /// <summary>
    /// 安装一个新的 MOD。如果 mods 或 mods_disabled 中已存在同名 MOD，则抛出异常。
    /// </summary>
    public string Install(ModPaths paths, ResolvedModPackage package, bool enabled)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(package);

        Directory.CreateDirectory(paths.EnabledDirectory);
        Directory.CreateDirectory(paths.DisabledDirectory);

        var name = package.SuggestedName;
        var enabledTarget = Path.Combine(paths.EnabledDirectory, name);
        var disabledTarget = Path.Combine(paths.DisabledDirectory, name);

        if (Directory.Exists(enabledTarget) || Directory.Exists(disabledTarget))
        {
            throw new IOException($"已存在同名 MOD：{name}。如需替换，请使用“更新 mod”。");
        }

        CopyDirectory(package.RootPath, enabled ? enabledTarget : disabledTarget);
        return name;
    }

    /// <summary>
    /// 用解析好的内容替换某个已存在的 MOD，保持其原有文件夹名与启用/禁用位置。
    /// 采用“暂存 → 备份旧目录 → 就位 → 清理”的流程，尽量保证失败时不破坏原有 MOD。
    /// </summary>
    public void Update(string existingModPath, ResolvedModPackage package)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(existingModPath);
        ArgumentNullException.ThrowIfNull(package);

        if (!Directory.Exists(existingModPath))
        {
            throw new DirectoryNotFoundException($"找不到要更新的 MOD：{existingModPath}");
        }

        var parent = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(existingModPath))
            ?? throw new InvalidOperationException("无法确定 MOD 所在目录。");
        var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(existingModPath));
        var staging = Path.Combine(parent, name + StagingSuffix);
        var backup = Path.Combine(parent, name + BackupSuffix);

        SafeDeleteDirectory(staging);
        SafeDeleteDirectory(backup);

        CopyDirectory(package.RootPath, staging);

        try
        {
            Directory.Move(existingModPath, backup);
            try
            {
                Directory.Move(staging, existingModPath);
            }
            catch
            {
                // 就位失败：把备份的旧目录还原回去。
                if (!Directory.Exists(existingModPath) && Directory.Exists(backup))
                {
                    Directory.Move(backup, existingModPath);
                }

                throw;
            }
        }
        catch
        {
            SafeDeleteDirectory(staging);
            throw;
        }

        SafeDeleteDirectory(backup);
    }

    /// <summary>
    /// 从磁盘删除某个 MOD 文件夹。
    /// </summary>
    public void Uninstall(string modPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modPath);

        if (Directory.Exists(modPath))
        {
            Directory.Delete(modPath, recursive: true);
        }
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        var sourceDirectory = new DirectoryInfo(source);

        foreach (var file in sourceDirectory.GetFiles())
        {
            file.CopyTo(Path.Combine(destination, file.Name), overwrite: true);
        }

        foreach (var subdirectory in sourceDirectory.GetDirectories())
        {
            CopyDirectory(subdirectory.FullName, Path.Combine(destination, subdirectory.Name));
        }
    }

    private static void SafeDeleteDirectory(string directory)
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
