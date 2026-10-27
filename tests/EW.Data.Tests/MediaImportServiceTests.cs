using EW.Data.Legacy;
using Microsoft.Data.Sqlite;

namespace EW.Data.Tests;

public sealed class MediaImportServiceTests : IDisposable
{
    private readonly string _root;
    private readonly SqliteDatabase _database;
    private readonly MediaAssetRepository _repository;
    private readonly MediaImportService _service;
    private readonly LegacyMediaFixture _fixture = new();

    public MediaImportServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "EWCloneTests", Guid.NewGuid().ToString("N"));
        _database = new SqliteDatabase(Path.Combine(_root, "clone.db"));
        _database.Initialize();
        _repository = new MediaAssetRepository(_database);
        _service = new MediaImportService(_database);
    }

    [Fact]
    public void ImportIsReadOnlyIdempotentAndPreservesUnknownValues()
    {
        var sourceHashBefore = LegacyMediaReader.FileSha256(_fixture.DatabasePath);
        var batch = LegacyMediaReader.Read(_fixture.DatabasePath);

        Assert.Equal(2, batch.Assets.Count);
        var video = batch.Assets[0];
        Assert.Equal("media-uid-1", video.LegacyKey);
        Assert.Equal("clip.mp4", Path.GetFileName(video.Path));
        Assert.Equal(1, video.MediaType);
        Assert.Equal(1920, video.Width);
        Assert.Equal(1080, video.Height);
        Assert.Equal(5000, video.Duration);
        Assert.Equal(2, video.AlphaMode);
        Assert.True(video.Ready);
        Assert.Contains("preserve-media", video.RawJson, StringComparison.Ordinal);

        var first = _service.Import(batch);

        Assert.Equal(2, first.Created);
        Assert.Equal(0, first.Updated);
        var imported = _repository.GetByLegacyId("media-uid-1");
        Assert.NotNull(imported);
        Assert.Equal(Path.Combine(@"C:\Media", "clip.mp4"), imported.Path);
        Assert.Equal("ABC123", imported.Hash);
        Assert.Null(imported.DurationMs);
        Assert.Equal(2, imported.AlphaMode);
        Assert.Equal(1, imported.Status);
        Assert.Contains("preserve-media", imported.LegacyMetadata, StringComparison.Ordinal);

        var missing = _repository.GetByLegacyId("media-uid-2");
        Assert.NotNull(missing);
        Assert.Equal("missing.mp4", missing.Path);
        Assert.Equal(0, missing.Status);
        Assert.Equal("7", missing.ErrorCode);
        Assert.Equal("File not found", missing.ErrorMessage);
        Assert.Equal(sourceHashBefore, LegacyMediaReader.FileSha256(_fixture.DatabasePath));

        var second = _service.Import(batch);

        Assert.Equal(0, second.Created);
        Assert.Equal(2, second.Updated);
        Assert.Equal(2, _repository.List().Count);
    }

    [Fact]
    public void ImportUpdatesSearchIndex()
    {
        var result = _service.Import(LegacyMediaReader.Read(_fixture.DatabasePath));

        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT MediaAssetId FROM MediaFts WHERE MediaFts MATCH 'clip';";

        Assert.Equal(result.MediaAssetIds[0].ToString(), command.ExecuteScalar());
    }

    [Fact]
    public void FailedImportRollsBackAllWrites()
    {
        using (var connection = _database.OpenConnection())
        {
            using var trigger = connection.CreateCommand();
            trigger.CommandText = """
                CREATE TRIGGER FailMediaImport
                BEFORE INSERT ON MediaAsset
                WHEN NEW.Path LIKE '%fail%'
                BEGIN
                    SELECT RAISE(ABORT, 'blocked by test');
                END;
                """;
            trigger.ExecuteNonQuery();
        }

        var batch = new LegacyMediaBatch(
            _fixture.DatabasePath,
            "media-hash",
            [
                CreateLegacyAsset("good", "good.mp4"),
                CreateLegacyAsset("fail", "fail.mp4"),
            ]);

        Assert.Throws<SqliteException>(() => _service.Import(batch));
        Assert.Empty(_repository.List());
        Assert.Equal(0, CountRows("MediaFts"));
    }

    public void Dispose()
    {
        _fixture.Dispose();
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

    private static LegacyMediaAsset CreateLegacyAsset(string key, string filename) =>
        new(
            key,
            RowId: 1,
            Uid: key,
            Hash: null,
            Path: filename,
            MediaType: 1,
            Width: null,
            Height: null,
            Duration: null,
            StartPosition: null,
            EndPosition: null,
            AlphaMode: null,
            Ready: true,
            ErrorNumber: null,
            ErrorMessage: null,
            RawJson: "{}");
}
