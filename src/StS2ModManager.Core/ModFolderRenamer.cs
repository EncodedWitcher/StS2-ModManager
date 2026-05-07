namespace StS2ModManager.Core;

public sealed class ModFolderRenamer
{
    public string Rename(string sourcePath, string currentFolderName, string newFolderName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentFolderName);

        var normalizedName = newFolderName.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            throw new ArgumentException("MOD 文件夹名不能为空。", nameof(newFolderName));
        }

        if (normalizedName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException("MOD 文件夹名包含非法字符。", nameof(newFolderName));
        }

        if (string.Equals(currentFolderName, normalizedName, StringComparison.OrdinalIgnoreCase))
        {
            return currentFolderName;
        }

        var parentDirectory = Path.GetDirectoryName(sourcePath)
            ?? throw new InvalidOperationException("无法确定 MOD 所在目录。");
        var targetPath = Path.Combine(parentDirectory, normalizedName);

        if (Directory.Exists(targetPath))
        {
            throw new IOException($"同名 MOD 文件夹已存在：{normalizedName}");
        }

        Directory.Move(sourcePath, targetPath);
        return normalizedName;
    }
}
