using Microsoft.Data.Sqlite;

namespace EW.Data.Tests;

public sealed class LegacyCollationTests : IDisposable
{
    private readonly string _root;
    private readonly string _databasePath;

    public LegacyCollationTests()
    {
        SQLitePCL.Batteries_V2.Init();
        _root = Path.Combine(Path.GetTempPath(), "EWCloneTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _databasePath = Path.Combine(_root, "legacy.db");
    }

    [Fact]
    public void RegisteredCollationAllowsLegacyIndexedLookup()
    {
        using (var connection = new SqliteConnection($"Data Source={_databasePath}"))
        {
            connection.Open();
            LegacyCollation.EnsureRegistered(connection);

            using var command = connection.CreateCommand();
            command.CommandText = $"""
                CREATE TABLE LegacyItem (Name TEXT NOT NULL);
                CREATE INDEX IX_LegacyItem_Name ON LegacyItem (Name COLLATE {LegacyCollation.Name});
                INSERT INTO LegacyItem (Name) VALUES ('Alpha');
                """;
            command.ExecuteNonQuery();
        }

        using (var connection = new SqliteConnection(
            new SqliteConnectionStringBuilder
            {
                DataSource = _databasePath,
                Mode = SqliteOpenMode.ReadOnly,
            }.ToString()))
        {
            connection.Open();
            LegacyCollation.EnsureRegistered(connection);

            using var command = connection.CreateCommand();
            command.CommandText = $"""
                SELECT Name
                FROM LegacyItem
                WHERE Name = 'alpha' COLLATE {LegacyCollation.Name};
                """;

            Assert.Equal("Alpha", command.ExecuteScalar());
        }
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
