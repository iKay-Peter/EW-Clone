using System.IO.Compression;
using EW.Data.Legacy;
using Microsoft.Data.Sqlite;

namespace EW.Data.Tests;

public sealed class ArchiveMediaImportServiceTests : IDisposable
{
    private readonly string _root;
    private readonly SqliteDatabase _database;
    private readonly MediaAssetRepository _repository;
    private readonly ArchiveMediaImportService _service;

    public ArchiveMediaImportServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "EWCloneTests", Guid.NewGuid().ToString("N"));
        _database = new SqliteDatabase(Path.Combine(_root, "clone.db"));
        _database.Initialize();
        _repository = new MediaAssetRepository(_database);
        _service = new ArchiveMediaImportService(_database);
    }

    [Fact]
    public void ImportRegistersExtractedMediaIdempotently()
    {
        var archivePath = CreateArchive("schedule.ewsx", ("media/clip.mp4", "video"));
        var hashBefore = LegacyArchiveMediaExtractor.FileSha256(archivePath);
        var manifest = LegacyArchiveMediaExtractor.Extract(
            archivePath,
            Path.Combine(_root, "extracted"));

        var first = _service.Import(manifest);

        Assert.Equal(1, first.Created);
        Assert.Equal(0, first.Updated);
        var extracted = Assert.Single(manifest.Entries);
        var asset = _repository.GetByLegacyId($"archive-media:{extracted.Sha256}");
        Assert.NotNull(asset);
        Assert.Equal(extracted.OutputPath, asset.Path);
        Assert.Equal(extracted.Sha256, asset.Hash);
        Assert.Equal(1, asset.Status);
        Assert.Contains("schedule.ewsx", asset.LegacyMetadata, StringComparison.Ordinal);
        Assert.Equal(hashBefore, LegacyArchiveMediaExtractor.FileSha256(archivePath));

        var second = _service.Import(manifest);

        Assert.Equal(0, second.Created);
        Assert.Equal(1, second.Updated);
        Assert.Single(_repository.List());
    }

    [Fact]
    public void FailedImportRollsBackAllWrites()
    {
        using (var connection = _database.OpenConnection())
        {
            using var trigger = connection.CreateCommand();
            trigger.CommandText = """
                CREATE TRIGGER FailArchiveMediaImport
                BEFORE INSERT ON MediaAsset
                BEGIN
                    SELECT RAISE(ABORT, 'blocked by test');
                END;
                """;
            trigger.ExecuteNonQuery();
        }

        var archivePath = CreateArchive("schedule.ewsx", ("media/clip.mp4", "video"));
        var manifest = LegacyArchiveMediaExtractor.Extract(
            archivePath,
            Path.Combine(_root, "extracted"));

        Assert.Throws<SqliteException>(() => _service.Import(manifest));
        Assert.Empty(_repository.List());
        Assert.Equal(0, CountRows("MediaFts"));
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private long CountRows(string tableName)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {tableName};";
        return Convert.ToInt64(
            command.ExecuteScalar(),
            System.Globalization.CultureInfo.InvariantCulture);
    }

    private string CreateArchive(string name, params (string Name, string Content)[] entries)
    {
        var path = Path.Combine(_root, name);
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
