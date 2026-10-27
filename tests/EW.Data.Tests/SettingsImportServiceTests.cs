using System.Text;
using EW.Data.Legacy;
using Microsoft.Data.Sqlite;

namespace EW.Data.Tests;

public sealed class SettingsImportServiceTests : IDisposable
{
    private readonly string _root;
    private readonly string _settingsPath;
    private readonly SqliteDatabase _database;
    private readonly SettingRepository _repository;
    private readonly SettingsImportService _service;

    public SettingsImportServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "EWCloneTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _settingsPath = Path.Combine(_root, "Sample.ini");
        File.WriteAllText(
            _settingsPath,
            """
            [Alpha]
            Key=one
            Empty=
            Key=two

            [Beta]
            Key=three
            """,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        _database = new SqliteDatabase(Path.Combine(_root, "clone.db"));
        _database.Initialize();
        _repository = new SettingRepository(_database);
        _service = new SettingsImportService(_database);
    }

    [Fact]
    public void ImportIsReadOnlyIdempotentAndPreservesLastDuplicateValue()
    {
        var hashBefore = LegacyIniFileReader.Read(_settingsPath).SourceSha256;
        var file = LegacyIniFileReader.Read(_settingsPath);

        var first = _service.Import(file);

        Assert.Equal(3, first.Created);
        Assert.Equal(0, first.Updated);
        Assert.Equal(3, first.Keys.Count);
        Assert.Contains(
            first.Warnings,
            warning => warning.Contains("Duplicate key", StringComparison.Ordinal));
        Assert.Equal("two", _repository.GetValue("Sample.ini/Alpha/Key"));
        Assert.Equal("", _repository.GetValue("Sample.ini/Alpha/Empty"));
        Assert.Equal("three", _repository.GetValue("Sample.ini/Beta/Key"));
        Assert.Contains(
            "\"section\":\"Alpha\"",
            _repository.Get("Sample.ini/Alpha/Key")!.LegacyMetadata!,
            StringComparison.Ordinal);
        Assert.Equal(
            hashBefore,
            LegacyIniFileReader.Read(_settingsPath).SourceSha256);

        var second = _service.Import(file);

        Assert.Equal(0, second.Created);
        Assert.Equal(3, second.Updated);
        Assert.Equal(3, _repository.List().Count);
    }

    [Fact]
    public void FailedImportRollsBackAllWrites()
    {
        using (var connection = _database.OpenConnection())
        {
            using var trigger = connection.CreateCommand();
            trigger.CommandText = """
                CREATE TRIGGER FailSettingsImport
                BEFORE INSERT ON Setting
                BEGIN
                    SELECT RAISE(ABORT, 'blocked by test');
                END;
                """;
            trigger.ExecuteNonQuery();
        }

        var file = LegacyIniFileReader.Read(_settingsPath);

        Assert.Throws<SqliteException>(() => _service.Import(file));
        Assert.Empty(_repository.List());
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
