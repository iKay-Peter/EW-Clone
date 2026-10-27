using Microsoft.Data.Sqlite;

namespace EW.Data.Tests;

internal sealed class LegacyServiceIntervalFixture : IDisposable
{
    public LegacyServiceIntervalFixture()
    {
        SQLitePCL.Batteries_V2.Init();
        Root = Path.Combine(Path.GetTempPath(), "EWCloneTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
        DatabasePath = Path.Combine(Root, "ServiceIntervals.db");

        using var connection = OpenDatabase(DatabasePath);
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE revision (
                rowid INTEGER PRIMARY KEY NOT NULL UNIQUE,
                revision_num INTEGER,
                tablename TEXT NOT NULL,
                key int8
            );

            CREATE TABLE interval (
                rowid INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL UNIQUE,
                start_time datetime,
                end_time datetime,
                start_time_fixed boolean,
                end_time_fixed boolean,
                future_field TEXT
            );

            INSERT INTO revision (revision_num, tablename, key)
            VALUES (1, 'interval', 0);

            INSERT INTO interval (
                start_time, end_time, start_time_fixed, end_time_fixed, future_field)
            VALUES (
                '2026-10-27 09:00:00', '2026-10-27 10:30:00', 1, 0, 'preserve-interval');

            INSERT INTO interval (
                start_time, end_time, start_time_fixed, end_time_fixed, future_field)
            VALUES (
                '2026-10-27 11:00:00', '2026-10-27 12:00:00', 0, 0, 'preserve-open');
            """;
        command.ExecuteNonQuery();
    }

    public string Root { get; }

    public string DatabasePath { get; }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }

    private static SqliteConnection OpenDatabase(string path)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Pooling = false,
        }.ToString();

        var connection = new SqliteConnection(connectionString);
        connection.Open();
        LegacyCollation.EnsureRegistered(connection);
        return connection;
    }
}
