using System.IO.Compression;
using EW.Data.Legacy;
using Microsoft.Data.Sqlite;

namespace EW.Data.Tests;

public sealed class PresentationMediaMappingTests : IDisposable
{
    private readonly string _root;
    private readonly SqliteDatabase _database;
    private readonly MediaAssetRepository _media;
    private readonly PresentationRepository _presentations;
    private readonly ResourceRepository _resources;

    public PresentationMediaMappingTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "EWCloneTests", Guid.NewGuid().ToString("N"));
        _database = new SqliteDatabase(Path.Combine(_root, "clone.db"));
        _database.Initialize();
        _media = new MediaAssetRepository(_database);
        _presentations = new PresentationRepository(_database);
        _resources = new ResourceRepository(_database);
    }

    [Fact]
    public void PresentationResourcesLinkToRegisteredArchiveMediaByHash()
    {
        var archivePath = CreateArchive(("media/photo.png", "image-bytes"));
        var manifest = LegacyArchiveMediaExtractor.Extract(
            archivePath,
            Path.Combine(_root, "extracted"));
        var extracted = Assert.Single(manifest.Entries);
        var mediaResult = new ArchiveMediaImportService(_database).Import(manifest);
        var mediaAssetId = Assert.Single(mediaResult.MediaAssetIds);

        using var fixture = new LegacyPresentationMediaFixture(extracted.Sha256.ToLowerInvariant());
        var hashBefore = LegacyPresentationReader.FileSha256(fixture.DatabasePath);
        var batch = LegacyPresentationReader.Read(fixture.DatabasePath);

        var legacyResource = Assert.Single(batch.Resources);
        Assert.Equal(extracted.Sha256, legacyResource.MediaHash, ignoreCase: true);
        Assert.Equal("photo.png", legacyResource.ArchiveFileName);
        Assert.Equal("<images>photo.png", legacyResource.OriginalFileName);
        Assert.Equal(800, legacyResource.Width);
        Assert.Equal(600, legacyResource.Height);
        Assert.Equal(11, legacyResource.FileSize);

        var import = new PresentationImportService(_database).Import(batch);

        var presentation = _presentations.Get(Assert.Single(import.PresentationIds));
        Assert.NotNull(presentation);
        var element = Assert.Single(presentation.Slides[0].Elements);
        var resource = _resources.Get(element.ForegroundResourceId!.Value);
        Assert.NotNull(resource);
        Assert.Equal(mediaAssetId, resource.MediaAssetId);
        Assert.Equal(extracted.OutputPath, _media.Get(mediaAssetId)!.Path);
        Assert.Equal(hashBefore, LegacyPresentationReader.FileSha256(fixture.DatabasePath));
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private string CreateArchive(params (string Name, string Content)[] entries)
    {
        var path = Path.Combine(_root, "media.ewsx");
        using var stream = File.Create(path);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create);
        foreach (var (entryName, content) in entries)
        {
            var entry = archive.CreateEntry(entryName, CompressionLevel.Fastest);
            using var entryStream = entry.Open();
            using var writer = new StreamWriter(entryStream);
            writer.Write(content);
        }

        return path;
    }
}
