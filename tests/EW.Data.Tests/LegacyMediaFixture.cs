using Microsoft.Data.Sqlite;

namespace EW.Data.Tests;

internal sealed class LegacyMediaFixture : IDisposable
{
    public LegacyMediaFixture()
    {
        SQLitePCL.Batteries_V2.Init();
        Root = Path.Combine(Path.GetTempPath(), "EWCloneTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
        DatabasePath = Path.Combine(Root, "Media.db");

        CreateDatabase();
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

    private void CreateDatabase()
    {
        using var connection = OpenDatabase(DatabasePath);
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE media (
                rowid INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL UNIQUE,
                media_uid TEXT UNIQUE,
                media_type INTEGER DEFAULT -1,
                filename TEXT COLLATE UTF8_U_CI,
                path TEXT COLLATE UTF8_U_CI,
                file_hash TEXT,
                width INTEGER,
                height INTEGER,
                duration INTEGER,
                start_pos INTEGER,
                end_pos INTEGER,
                alpha_channel_mode INTEGER,
                ready INTEGER DEFAULT 0,
                error_no INTEGER DEFAULT 0,
                error_msg TEXT,
                future_field TEXT
            );

            INSERT INTO media (
                media_uid, media_type, filename, path, file_hash, width, height,
                duration, start_pos, end_pos, alpha_channel_mode, ready, error_no,
                error_msg, future_field)
            VALUES (
                'media-uid-1', 1, 'clip.mp4', 'C:\Media', 'ABC123', 1920, 1080,
                5000, 0, 5000, 2, 1, 0, NULL, 'preserve-media');

            INSERT INTO media (
                media_uid, media_type, filename, path, file_hash, width, height,
                duration, start_pos, end_pos, alpha_channel_mode, ready, error_no,
                error_msg, future_field)
            VALUES (
                'media-uid-2', 1, 'missing.mp4', NULL, NULL, NULL, NULL,
                NULL, NULL, NULL, NULL, 0, 7, 'File not found', 'preserve-error');
            """;
        command.ExecuteNonQuery();
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
