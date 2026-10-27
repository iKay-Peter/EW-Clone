using System.IO.Compression;
using EW.Data.Legacy;

namespace EW.Data.Tests;

public sealed class LegacyArchiveMediaExtractorTests : IDisposable
{
    private readonly string _root;

    public LegacyArchiveMediaExtractorTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "EWCloneTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public void ExtractCopiesMediaEntriesAndSkipsNonMediaEntries()
    {
        var archivePath = Path.Combine(_root, "schedule.ewsx");
        CreateArchive(
            archivePath,
            ("media/", "ignored directory"),
            ("media/clip.mp4", "video"),
            ("media/nested/image.png", "image"),
            ("docs/readme.txt", "ignored"));

        var destination = Path.Combine(_root, "extracted");
        var manifest = LegacyArchiveMediaExtractor.Extract(archivePath, destination);

        Assert.Equal(Path.GetFullPath(archivePath), manifest.ArchivePath);
        Assert.Equal(Path.GetFullPath(destination), manifest.DestinationDirectory);
        Assert.Equal(2, manifest.Entries.Count);
        Assert.All(manifest.Entries, entry =>
        {
            Assert.True(File.Exists(entry.OutputPath));
            Assert.Equal(64, entry.Sha256.Length);
        });

        Assert.Equal(
            "video",
            File.ReadAllText(Path.Combine(destination, "clip.mp4")));
        Assert.Equal(
            "image",
            File.ReadAllText(Path.Combine(destination, "nested", "image.png")));
        Assert.False(File.Exists(Path.Combine(destination, "docs", "readme.txt")));
    }

    [Fact]
    public void ExtractRejectsArchiveEntriesThatEscapeTheDestination()
    {
        var archivePath = Path.Combine(_root, "malicious.ewsx");
        CreateArchive(archivePath, ("media/../evil.txt", "evil"));

        var destination = Path.Combine(_root, "extracted");

        Assert.Throws<InvalidDataException>(
            () => LegacyArchiveMediaExtractor.Extract(archivePath, destination));
        Assert.False(Directory.Exists(destination));
        Assert.False(File.Exists(Path.Combine(_root, "evil.txt")));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static void CreateArchive(string path, params (string Name, string Content)[] entries)
    {
        using var stream = File.Create(path);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create);
        foreach (var (name, content) in entries)
        {
            var entry = archive.CreateEntry(name, CompressionLevel.Fastest);
            using var entryStream = entry.Open();
            using var writer = new StreamWriter(entryStream);
            writer.Write(content);
        }
    }
}
