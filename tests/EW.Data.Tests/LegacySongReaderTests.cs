using EW.Data.Legacy;

namespace EW.Data.Tests;

public sealed class LegacySongReaderTests : IDisposable
{
    private readonly LegacySongFixture _fixture = new();

    [Fact]
    public void ReadPreservesKnownAndUnknownLegacyValues()
    {
        var batch = LegacySongReader.Read(_fixture.SongsDatabasePath, _fixture.SongWordsDatabasePath);

        var song = Assert.Single(batch.Songs);
        Assert.Equal("song-uid-1", song.LegacyKey);
        Assert.Equal("Amazing Grace", song.Title);
        Assert.Equal("John Newton", song.Author);
        Assert.Contains("preserve-me", song.RawJson, StringComparison.Ordinal);
        Assert.Equal(64, batch.SongsSha256.Length);
        Assert.Equal(64, batch.SongWordsSha256.Length);

        var word = Assert.Single(song.Words);
        Assert.Contains("Amazing grace", word.Rtf, StringComparison.Ordinal);
        Assert.Equal("slide-uid-1", word.SlideUids);
    }

    [Fact]
    public void ReadUsesRowIdWhenSongUidIsMissing()
    {
        var connectionString = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
        {
            DataSource = _fixture.SongsDatabasePath,
            Pooling = false,
        }.ToString();

        using (var connection = new Microsoft.Data.Sqlite.SqliteConnection(connectionString))
        {
            connection.Open();
            LegacyCollation.EnsureRegistered(connection);
            using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO song (song_uid, title, future_field)
                VALUES (NULL, 'Without UID', 'unknown-value');
                """;
            command.ExecuteNonQuery();
        }

        var batch = LegacySongReader.Read(
            _fixture.SongsDatabasePath,
            _fixture.SongWordsDatabasePath);

        Assert.Contains(batch.Songs, song => song.LegacyKey == $"rowid:{song.RowId}" && song.Title == "Without UID");
    }

    public void Dispose() => _fixture.Dispose();
}
