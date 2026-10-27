using System.IO.Compression;
using EW.Data.Legacy;
using Microsoft.Data.Sqlite;

namespace EW.Data.Tests;

public sealed class ThemeImportServiceTests : IDisposable
{
    private readonly string _root;
    private readonly SqliteDatabase _database;
    private readonly ThemeRepository _repository;
    private readonly ThemeImportService _service;
    private readonly LegacyPresentationFixture _fixture = new();

    public ThemeImportServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "EWCloneTests", Guid.NewGuid().ToString("N"));
        _database = new SqliteDatabase(Path.Combine(_root, "clone.db"));
        _database.Initialize();
        _repository = new ThemeRepository(_database);
        _service = new ThemeImportService(_database);
    }

    [Fact]
    public void ImportIsReadOnlyIdempotentAndPreservesThemeGraph()
    {
        var archivePath = CreateThemeArchive();
        var hashBefore = LegacyThemeReader.FileSha256(archivePath);

        var batch = LegacyThemeReader.Read(archivePath);

        Assert.Equal("Synthetic Presentation", batch.Theme.Name);
        Assert.Contains("preserve-presentation", batch.Theme.StyleJson, StringComparison.Ordinal);
        Assert.Contains("Slide One", batch.Theme.StyleJson, StringComparison.Ordinal);

        var first = _service.Import(batch);

        Assert.Equal(1, first.Created);
        Assert.Equal(0, first.Updated);
        var theme = _repository.Get(first.ThemeId);
        Assert.NotNull(theme);
        Assert.Equal("Synthetic Presentation", theme.Name);
        Assert.Equal(archivePath, theme.SourcePath);
        Assert.Contains("preserve-presentation", theme.StyleJson, StringComparison.Ordinal);
        Assert.Contains("Synthetic Theme 2.ewtx", theme.LegacyMetadata, StringComparison.Ordinal);
        Assert.Equal(hashBefore, LegacyThemeReader.FileSha256(archivePath));

        var second = _service.Import(batch);

        Assert.Equal(0, second.Created);
        Assert.Equal(1, second.Updated);
        Assert.Single(_repository.List());
    }

    [Fact]
    public void ImportUpdatesSearchIndex()
    {
        var result = _service.Import(LegacyThemeReader.Read(CreateThemeArchive()));

        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT ThemeId FROM ThemeFts WHERE ThemeFts MATCH 'Synthetic';";

        Assert.Equal(result.ThemeId.ToString(), command.ExecuteScalar());
    }

    [Fact]
    public void FailedImportRollsBackAllWrites()
    {
        using (var connection = _database.OpenConnection())
        {
            using var trigger = connection.CreateCommand();
            trigger.CommandText = """
                CREATE TRIGGER FailThemeImport
                BEFORE INSERT ON Theme
                BEGIN
                    SELECT RAISE(ABORT, 'blocked by test');
                END;
                """;
            trigger.ExecuteNonQuery();
        }

        var batch = LegacyThemeReader.Read(CreateThemeArchive());

        Assert.Throws<SqliteException>(() => _service.Import(batch));
        Assert.Empty(_repository.List());
        Assert.Equal(0, CountRows("ThemeFts"));
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

    private string CreateThemeArchive()
    {
        var path = Path.Combine(_root, "Synthetic Theme 2.ewtx");
        using var stream = File.Create(path);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create);
        var entry = archive.CreateEntry("main.db", CompressionLevel.Fastest);
        using var entryStream = entry.Open();
        using var source = File.OpenRead(_fixture.DatabasePath);
        source.CopyTo(entryStream);
        return path;
    }
}
