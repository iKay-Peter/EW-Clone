using System.IO.Compression;
using EW.Data.Legacy;

namespace EW.Data.Tests;

public sealed class LegacyArchiveReaderTests : IDisposable
{
    private readonly string _root;
    private readonly LegacyPresentationFixture _fixture = new();

    public LegacyArchiveReaderTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "EWCloneTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public void ReadPresentationArchiveExtractsMainDatabaseAndPreservesSourceIdentity()
    {
        var archivePath = Path.Combine(_root, "schedule.ewsx");
        CreateArchive(archivePath, includeMainDatabase: true);

        var batch = LegacyArchiveReader.ReadPresentationArchive(archivePath);

        Assert.Equal(Path.GetFullPath(archivePath), batch.ArchivePath);
        Assert.Equal(batch.ArchiveSha256, batch.PresentationGraph.SourceSha256);
        Assert.Equal(Path.GetFullPath(archivePath), batch.PresentationGraph.SourcePath);
        Assert.Equal(2, batch.Entries.Count);
        Assert.Single(batch.PresentationGraph.Presentations);
        Assert.Equal(64, batch.ArchiveSha256.Length);
    }

    [Fact]
    public void ReadPresentationArchiveRejectsArchiveWithoutMainDatabase()
    {
        var archivePath = Path.Combine(_root, "invalid.ewsx");
        CreateArchive(archivePath, includeMainDatabase: false);

        Assert.Throws<InvalidDataException>(
            () => LegacyArchiveReader.ReadPresentationArchive(archivePath));
    }

    public void Dispose()
    {
        _fixture.Dispose();
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private void CreateArchive(string path, bool includeMainDatabase)
    {
        using var stream = File.Create(path);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create);

        if (includeMainDatabase)
        {
            var entry = archive.CreateEntry("main.db", CompressionLevel.Fastest);
            using var entryStream = entry.Open();
            using var source = File.OpenRead(_fixture.DatabasePath);
            source.CopyTo(entryStream);
        }

        var media = archive.CreateEntry("media/readme.txt", CompressionLevel.Fastest);
        using var writer = new StreamWriter(media.Open());
        writer.Write("synthetic media");
    }
}
