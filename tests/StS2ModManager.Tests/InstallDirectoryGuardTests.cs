using StS2ModManager.Core;

namespace StS2ModManager.Tests;

public sealed class InstallDirectoryGuardTests
{
    [Fact]
    public void IsInstallDirectoryClean_AllowsExecutableInstructionAndTemplateConfigOnly()
    {
        using var temp = new TempDirectory();
        var executablePath = Path.Combine(temp.Path, "StS2ModManager.exe");
        var configPath = Path.Combine(temp.Path, "config", "config.json");
        Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
        File.WriteAllText(executablePath, string.Empty);
        File.WriteAllText(configPath, "{}");
        File.WriteAllText(Path.Combine(temp.Path, "使用说明.txt"), string.Empty);

        var result = InstallDirectoryGuard.IsInstallDirectoryClean(executablePath, configPath);

        Assert.True(result);
    }

    [Fact]
    public void IsInstallDirectoryClean_ReturnsFalse_WhenUnexpectedRootFileExists()
    {
        using var temp = new TempDirectory();
        var executablePath = Path.Combine(temp.Path, "StS2ModManager.exe");
        var configPath = Path.Combine(temp.Path, "config", "config.json");
        Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
        File.WriteAllText(executablePath, string.Empty);
        File.WriteAllText(configPath, "{}");
        File.WriteAllText(Path.Combine(temp.Path, "README.md"), string.Empty);

        var result = InstallDirectoryGuard.IsInstallDirectoryClean(executablePath, configPath);

        Assert.False(result);
    }

    [Fact]
    public void IsInstallDirectoryClean_ReturnsFalse_WhenExtraDirectoryExists()
    {
        using var temp = new TempDirectory();
        var executablePath = Path.Combine(temp.Path, "StS2ModManager.exe");
        var configPath = Path.Combine(temp.Path, "config", "config.json");
        Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
        Directory.CreateDirectory(Path.Combine(temp.Path, "extra"));
        File.WriteAllText(executablePath, string.Empty);
        File.WriteAllText(configPath, "{}");
        File.WriteAllText(Path.Combine(temp.Path, "使用说明.txt"), string.Empty);

        var result = InstallDirectoryGuard.IsInstallDirectoryClean(executablePath, configPath);

        Assert.False(result);
    }
}
