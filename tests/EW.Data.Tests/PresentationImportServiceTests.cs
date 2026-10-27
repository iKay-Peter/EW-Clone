using EW.Data.Legacy;
using EW.Domain.Presentations;
using Microsoft.Data.Sqlite;

namespace EW.Data.Tests;

public sealed class PresentationImportServiceTests : IDisposable
{
    private readonly string _root;
    private readonly SqliteDatabase _database;
    private readonly PresentationRepository _presentations;
    private readonly ResourceRepository _resources;
    private readonly PresentationImportService _service;
    private readonly LegacyPresentationFixture _fixture = new();

    public PresentationImportServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "EWCloneTests", Guid.NewGuid().ToString("N"));
        _database = new SqliteDatabase(Path.Combine(_root, "clone.db"));
        _database.Initialize();
        _presentations = new PresentationRepository(_database);
        _resources = new ResourceRepository(_database);
        _service = new PresentationImportService(_database);
    }

    [Fact]
    public void ImportIsReadOnlyIdempotentAndPreservesGraph()
    {
        var hashBefore = LegacySongReader.FileSha256(_fixture.DatabasePath);
        var batch = LegacyPresentationReader.Read(_fixture.DatabasePath);

        var first = _service.Import(batch);

        Assert.Equal(1, first.PresentationsCreated);
        Assert.Equal(1, first.ResourcesCreated);
        var presentationId = Assert.Single(first.PresentationIds);
        var presentation = _presentations.Get(presentationId);

        Assert.NotNull(presentation);
        Assert.Equal("Synthetic Presentation", presentation.Title);
        Assert.Equal(16d / 9d, presentation.AspectRatio);
        Assert.Equal(2, presentation.Slides.Count);
        Assert.Contains("preserve-presentation", presentation.LegacyMetadata, StringComparison.Ordinal);

        var element = Assert.Single(presentation.Slides[0].Elements);
        Assert.NotNull(element.ForegroundResourceId);
        Assert.Equal(element.ForegroundResourceId, element.ResourceId);
        Assert.Contains("preserve-element", element.LegacyMetadata, StringComparison.Ordinal);

        var resource = _resources.Get(element.ForegroundResourceId!.Value);
        Assert.NotNull(resource);
        Assert.Equal("Hello resource", resource.TextPlain);
        Assert.Contains("preserve-resource", resource.LegacyMetadata, StringComparison.Ordinal);
        Assert.Equal(hashBefore, LegacySongReader.FileSha256(_fixture.DatabasePath));
        Assert.Equal(1, CountPresentationSearchMatches("Synthetic"));

        var second = _service.Import(batch);

        Assert.Equal(0, second.PresentationsCreated);
        Assert.Equal(1, second.PresentationsUpdated);
        Assert.Equal(0, second.ResourcesCreated);
        Assert.Equal(1, second.ResourcesUpdated);
        Assert.Equal(presentationId, Assert.Single(second.PresentationIds));
        Assert.Single(_presentations.List());
        Assert.Equal(1, CountRows("Resource"));
        Assert.Equal(2, CountRows("Slide"));
        Assert.Equal(2, CountRows("Element"));
        Assert.Equal(1, CountRows("PresentationFts"));
        Assert.Equal(1, CountPresentationSearchMatches("Synthetic"));
    }

    [Fact]
    public void FailedImportRollsBackAllWrites()
    {
        using (var connection = _database.OpenConnection())
        {
            using var trigger = connection.CreateCommand();
            trigger.CommandText = """
                CREATE TRIGGER FailPresentationImport
                BEFORE INSERT ON Presentation
                WHEN NEW.Title = 'Fail'
                BEGIN
                    SELECT RAISE(ABORT, 'blocked by test');
                END;
                """;
            trigger.ExecuteNonQuery();
        }

        var batch = new LegacyPresentationBatch(
            _fixture.DatabasePath,
            "source-hash",
            [
                CreatePresentation("good", "Good"),
                CreatePresentation("fail", "Fail"),
            ],
            []);

        Assert.Throws<SqliteException>(() => _service.Import(batch));
        Assert.Empty(_presentations.List());
        Assert.Equal(0, CountRows("Slide"));
        Assert.Equal(0, CountRows("Element"));
        Assert.Equal(0, CountRows("Resource"));
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

    private long CountPresentationSearchMatches(string term)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM PresentationFts WHERE PresentationFts MATCH $term;";
        command.Parameters.AddWithValue("$term", term);
        return Convert.ToInt64(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }

    private long CountRows(string tableName)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {tableName};";
        return Convert.ToInt64(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static LegacyPresentation CreatePresentation(string key, string title) =>
        new(
            key,
            RowId: 1,
            title,
            Author: null,
            AspectRatio: null,
            RawJson: "{}",
            Slides: []);
}
