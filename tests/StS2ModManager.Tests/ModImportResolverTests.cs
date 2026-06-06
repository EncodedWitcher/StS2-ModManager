using System.IO.Compression;
using StS2ModManager.Core;

namespace StS2ModManager.Tests;

public sealed class ModImportResolverTests
{
    [Fact]
    public void Resolve_FolderWithManifestAtTop_UsesManifestIdAsName()
    {
        using var temp = new TempDirectory();
        var modDirectory = Path.Combine(temp.Path, "rawFiles");
        Directory.CreateDirectory(modDirectory);
        WriteManifest(Path.Combine(modDirectory, "anything.json"), id: "CoolMod", pckName: "CoolMod");
        File.WriteAllText(Path.Combine(modDirectory, "CoolMod.pck"), "data");

        using var package = new ModImportResolver().Resolve(modDirectory);

        Assert.Equal("CoolMod", package.SuggestedName);
        Assert.True(File.Exists(Path.Combine(package.RootPath, "CoolMod.pck")));
    }

    [Fact]
    public void Resolve_FolderWithWrapper_UnwrapsToModRoot()
    {
        using var temp = new TempDirectory();
        var modDirectory = Path.Combine(temp.Path, "Download", "MyMod");
        Directory.CreateDirectory(modDirectory);
        WriteManifest(Path.Combine(modDirectory, "MyMod.json"), id: "MyMod");

        using var package = new ModImportResolver().Resolve(Path.Combine(temp.Path, "Download"));

        Assert.Equal("MyMod", package.SuggestedName);
        Assert.Equal(modDirectory, package.RootPath);
    }

    [Fact]
    public void Resolve_FolderWithPckOnly_DetectsRootAndFallsBackToFolderName()
    {
        using var temp = new TempDirectory();
        var modDirectory = Path.Combine(temp.Path, "skinPack");
        Directory.CreateDirectory(modDirectory);
        File.WriteAllText(Path.Combine(modDirectory, "content.pck"), "data");

        using var package = new ModImportResolver().Resolve(modDirectory);

        Assert.Equal("skinPack", package.SuggestedName);
    }

    [Fact]
    public void Resolve_UsesPckNameWhenIdMissing()
    {
        using var temp = new TempDirectory();
        var modDirectory = Path.Combine(temp.Path, "weirdFolder");
        Directory.CreateDirectory(modDirectory);
        WriteManifest(Path.Combine(modDirectory, "manifest.json"), pckName: "RealName");

        using var package = new ModImportResolver().Resolve(modDirectory);

        Assert.Equal("RealName", package.SuggestedName);
    }

    [Fact]
    public void Resolve_Zip_ExtractsResolvesAndCleansUpOnDispose()
    {
        using var temp = new TempDirectory();
        var staging = Path.Combine(temp.Path, "staging");
        var modDirectory = Path.Combine(staging, "ZippedMod");
        Directory.CreateDirectory(modDirectory);
        WriteManifest(Path.Combine(modDirectory, "ZippedMod.json"), id: "ZippedMod");
        File.WriteAllText(Path.Combine(modDirectory, "ZippedMod.pck"), "data");

        var zipPath = Path.Combine(temp.Path, "ZippedMod.zip");
        ZipFile.CreateFromDirectory(staging, zipPath);

        string rootPath;
        using (var package = new ModImportResolver().Resolve(zipPath))
        {
            rootPath = package.RootPath;
            Assert.Equal("ZippedMod", package.SuggestedName);
            Assert.True(File.Exists(Path.Combine(rootPath, "ZippedMod.pck")));
        }

        Assert.False(Directory.Exists(rootPath));
    }

    [Fact]
    public void Resolve_NonZipFile_Throws()
    {
        using var temp = new TempDirectory();
        var filePath = Path.Combine(temp.Path, "readme.txt");
        File.WriteAllText(filePath, "not a mod");

        Assert.Throws<NotSupportedException>(() => new ModImportResolver().Resolve(filePath));
    }

    private static void WriteManifest(string path, string? id = null, string? pckName = null)
    {
        var fields = new List<string>();
        if (id is not null)
        {
            fields.Add($"\"id\": \"{id}\"");
        }

        if (pckName is not null)
        {
            fields.Add($"\"pck_name\": \"{pckName}\"");
        }

        File.WriteAllText(path, "{" + string.Join(",", fields) + "}");
    }
}
