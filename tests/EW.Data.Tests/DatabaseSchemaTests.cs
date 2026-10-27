using System.Globalization;
using Microsoft.Data.Sqlite;

namespace EW.Data.Tests;

public sealed class DatabaseSchemaTests : IDisposable
{
    private readonly string _root;
    private readonly SqliteDatabase _database;

    public DatabaseSchemaTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "EWCloneTests", Guid.NewGuid().ToString("N"));
        _database = new SqliteDatabase(Path.Combine(_root, "clone.db"));
        _database.Initialize();
    }

    [Fact]
    public void InitializeCreatesCoreTables()
    {
        var tables = GetTableNames();

        Assert.Contains("Profile", tables);
        Assert.Contains("Song", tables);
        Assert.Contains("SongSlide", tables);
        Assert.Contains("MediaAsset", tables);
        Assert.Contains("Presentation", tables);
        Assert.Contains("Slide", tables);
        Assert.Contains("Element", tables);
        Assert.Contains("Resource", tables);
        Assert.Contains("Theme", tables);
        Assert.Contains("Schedule", tables);
        Assert.Contains("ScheduleItem", tables);
        Assert.Contains("BibleTranslation", tables);
        Assert.Contains("BibleVerse", tables);
        Assert.Contains("OutputProfile", tables);
        Assert.Contains("ServiceInterval", tables);
        Assert.Contains("Alert", tables);
        Assert.Contains("TimerToken", tables);
        Assert.Contains("Setting", tables);
        Assert.Contains("LegacyImport", tables);
        Assert.Contains("SongFts", tables);
    }

    [Fact]
    public void InitializeIsIdempotent()
    {
        _database.Initialize();

        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version;";

        var version = Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
        Assert.Equal(4, version);
    }

    [Fact]
    public void ForeignKeysAreEnforced()
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO SongSlide (Id, SongId, Position, ContentRtf, PlainText)
            VALUES ('slide-1', 'missing-song', 0, '', '');
            """;

        var exception = Assert.Throws<SqliteException>(() => command.ExecuteNonQuery());
        Assert.Contains("FOREIGN KEY", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SongFtsIndexesAndSearchesLyrics()
    {
        using var connection = _database.OpenConnection();
        using var insert = connection.CreateCommand();
        insert.CommandText = """
            INSERT INTO SongFts (SongId, Title, Author, Tags, Lyrics)
            VALUES ('song-1', 'Test Song', 'Test Author', 'grace', 'Amazing grace how sweet the sound');
            """;
        insert.ExecuteNonQuery();

        using var search = connection.CreateCommand();
        search.CommandText = "SELECT SongId FROM SongFts WHERE SongFts MATCH 'grace';";

        Assert.Equal("song-1", search.ExecuteScalar());
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private List<string> GetTableNames()
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT name
            FROM sqlite_master
            WHERE type = 'table'
              AND name NOT LIKE 'sqlite_%'
            ORDER BY name;
            """;

        using var reader = command.ExecuteReader();
        var names = new List<string>();
        while (reader.Read())
        {
            names.Add(reader.GetString(0));
        }

        return names;
    }
}
