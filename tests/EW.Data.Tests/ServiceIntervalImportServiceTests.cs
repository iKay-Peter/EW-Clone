using EW.Data.Legacy;
using Microsoft.Data.Sqlite;

namespace EW.Data.Tests;

public sealed class ServiceIntervalImportServiceTests : IDisposable
{
    private readonly string _root;
    private readonly SqliteDatabase _database;
    private readonly ServiceIntervalRepository _repository;
    private readonly ServiceIntervalImportService _service;
    private readonly LegacyServiceIntervalFixture _fixture = new();

    public ServiceIntervalImportServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "EWCloneTests", Guid.NewGuid().ToString("N"));
        _database = new SqliteDatabase(Path.Combine(_root, "clone.db"));
        _database.Initialize();
        _repository = new ServiceIntervalRepository(_database);
        _service = new ServiceIntervalImportService(_database);
    }

    [Fact]
    public void ImportIsReadOnlyIdempotentAndPreservesUnknownValues()
    {
        var hashBefore = LegacyServiceIntervalReader.FileSha256(_fixture.DatabasePath);
        var batch = LegacyServiceIntervalReader.Read(_fixture.DatabasePath);

        Assert.Equal(2, batch.Intervals.Count);
        Assert.True(batch.Intervals[0].StartFixed);
        Assert.False(batch.Intervals[0].EndFixed);

        var first = _service.Import(batch);

        Assert.Equal(2, first.Created);
        Assert.Equal(0, first.Updated);
        var interval = _repository.GetByLegacyId("rowid:1");
        Assert.NotNull(interval);
        Assert.Equal("2026-10-27 09:00:00", interval.StartValue);
        Assert.Equal("2026-10-27 10:30:00", interval.EndValue);
        Assert.True(interval.IsFixed);
        Assert.Contains("preserve-interval", interval.LegacyMetadata, StringComparison.Ordinal);
        Assert.False(_repository.GetByLegacyId("rowid:2")!.IsFixed);
        Assert.Equal(hashBefore, LegacyServiceIntervalReader.FileSha256(_fixture.DatabasePath));

        var second = _service.Import(batch);

        Assert.Equal(0, second.Created);
        Assert.Equal(2, second.Updated);
        Assert.Equal(2, _repository.List().Count);
    }

    [Fact]
    public void FailedImportRollsBackAllWrites()
    {
        using (var connection = _database.OpenConnection())
        {
            using var trigger = connection.CreateCommand();
            trigger.CommandText = """
                CREATE TRIGGER FailServiceIntervalImport
                BEFORE INSERT ON ServiceInterval
                BEGIN
                    SELECT RAISE(ABORT, 'blocked by test');
                END;
                """;
            trigger.ExecuteNonQuery();
        }

        var batch = LegacyServiceIntervalReader.Read(_fixture.DatabasePath);

        Assert.Throws<SqliteException>(() => _service.Import(batch));
        Assert.Empty(_repository.List());
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
}
