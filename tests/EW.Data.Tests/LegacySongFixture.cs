using Microsoft.Data.Sqlite;

namespace EW.Data.Tests;

internal sealed class LegacySongFixture : IDisposable
{
    public LegacySongFixture()
    {
        SQLitePCL.Batteries_V2.Init();
        Root = Path.Combine(Path.GetTempPath(), "EWCloneTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
        SongsDatabasePath = Path.Combine(Root, "Songs.db");
        SongWordsDatabasePath = Path.Combine(Root, "SongWords.db");

        CreateSongsDatabase();
        CreateSongWordsDatabase();
    }

    public string Root { get; }

    public string SongsDatabasePath { get; }

    public string SongWordsDatabasePath { get; }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }

    private void CreateSongsDatabase()
    {
        using var connection = OpenDatabase(SongsDatabasePath);
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE song (
                rowid INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL UNIQUE,
                song_uid TEXT UNIQUE,
                song_rev_uid TEXT,
                title TEXT NOT NULL COLLATE UTF8_U_CI,
                author TEXT COLLATE UTF8_U_CI,
                copyright TEXT COLLATE UTF8_U_CI,
                administrator TEXT COLLATE UTF8_U_CI,
                description TEXT,
                tags TEXT COLLATE UTF8_U_CI,
                reference_number TEXT COLLATE UTF8_U_CI,
                provider_id INTEGER DEFAULT -1,
                vendor_id INTEGER,
                presentation_id INTEGER,
                layout_revision INTEGER DEFAULT 1,
                revision INTEGER DEFAULT 1,
                future_field TEXT
            );

            CREATE INDEX IX_song_title ON song (title COLLATE UTF8_U_CI);

            INSERT INTO song (
                song_uid, song_rev_uid, title, author, copyright, administrator,
                description, tags, reference_number, provider_id, vendor_id,
                presentation_id, layout_revision, revision, future_field)
            VALUES (
                'song-uid-1', 'song-rev-1', 'Amazing Grace', 'John Newton',
                'Public Domain', 'Public Domain', 'A synthetic fixture',
                'grace, hymn', 'REF-1', -1, 0, 1, 1, 1, 'preserve-me');
            """;
        command.ExecuteNonQuery();
    }

    private void CreateSongWordsDatabase()
    {
        using var connection = OpenDatabase(SongWordsDatabasePath);
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE word (
                rowid INTEGER PRIMARY KEY NOT NULL UNIQUE,
                song_id INTEGER,
                words RTF,
                slide_uids TEXT,
                slide_layout_revisions int64a,
                slide_revisions int64a
            );

            INSERT INTO word (
                song_id, words, slide_uids, slide_layout_revisions, slide_revisions)
            VALUES (
                1,
                '{\rtf1\ansi\deff0{\fonttbl{\f0 Arial;}}Amazing grace\par how sweet the sound}',
                'slide-uid-1', '1', '1');
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
