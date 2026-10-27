using EW.Data.Legacy;
using Microsoft.Data.Sqlite;

namespace EW.Data.Tests;

public sealed class SongImportServiceTests : IDisposable
{
    private readonly string _root;
    private readonly SqliteDatabase _database;
    private readonly SongRepository _repository;
    private readonly SongImportService _service;
    private readonly LegacySongFixture _fixture = new();

    public SongImportServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "EWCloneTests", Guid.NewGuid().ToString("N"));
        _database = new SqliteDatabase(Path.Combine(_root, "clone.db"));
        _database.Initialize();
        _repository = new SongRepository(_database);
        _service = new SongImportService(_database);
    }

    [Fact]
    public void ImportIsReadOnlyIdempotentAndPreservesUnknownValues()
    {
        var songsHashBefore = LegacySongReader.FileSha256(_fixture.SongsDatabasePath);
        var wordsHashBefore = LegacySongReader.FileSha256(_fixture.SongWordsDatabasePath);

        var batch = LegacySongReader.Read(
            _fixture.SongsDatabasePath,
            _fixture.SongWordsDatabasePath);
        var first = _service.Import(batch);

        Assert.Equal(1, first.Created);
        Assert.Equal(0, first.Updated);
        var songId = Assert.Single(first.SongIds);
        var song = _repository.Get(songId);

        Assert.NotNull(song);
        Assert.Equal("Amazing Grace", song.Title);
        Assert.Equal("John Newton", song.Author);
        Assert.Single(song.Slides);
        Assert.Equal("Amazing grace\nhow sweet the sound", song.Slides[0].PlainText);
        Assert.Contains("preserve-me", song.LegacyMetadata, StringComparison.Ordinal);
        Assert.Equal(songsHashBefore, LegacySongReader.FileSha256(_fixture.SongsDatabasePath));
        Assert.Equal(wordsHashBefore, LegacySongReader.FileSha256(_fixture.SongWordsDatabasePath));

        var second = _service.Import(batch);

        Assert.Equal(0, second.Created);
        Assert.Equal(1, second.Updated);
        Assert.Equal(songId, Assert.Single(second.SongIds));
        var reloaded = _repository.Get(songId);
        Assert.NotNull(reloaded);
        Assert.Single(reloaded.Slides);
        Assert.Single(_repository.List());
    }

    [Fact]
    public void ImportSplitsLegacySlideMarkers()
    {
        const string rtf = @"{\rtf1\ansi{\fonttbl{\f0 Arial;}}{\pard First slide\par}{\pard\sdslidemarker\qc\par}{\pard Second slide\par}{\pard\sdslidemarker\qc\par}{\pard Third slide\par}}";
        var batch = new LegacySongBatch(
            _fixture.SongsDatabasePath,
            _fixture.SongWordsDatabasePath,
            "songs-hash",
            "words-hash",
            [
                new LegacySong(
                    "marked-song",
                    RowId: 1,
                    Uid: "marked-song",
                    "Marked Song",
                    Author: null,
                    Copyright: null,
                    Tags: null,
                    RawJson: "{}",
                    Words:
                    [
                        new LegacySongWord(
                            RowId: 1,
                            Position: 0,
                            rtf,
                            "slide-1,slide-2,slide-3",
                            SlideLayoutRevisions: null,
                            SlideRevisions: null,
                            RawJson: "{}"),
                    ]),
            ]);

        var result = _service.Import(batch);

        var song = _repository.Get(Assert.Single(result.SongIds));
        Assert.NotNull(song);
        Assert.Collection(
            song.Slides,
            slide => Assert.Equal("First slide", slide.PlainText),
            slide => Assert.Equal("Second slide", slide.PlainText),
            slide => Assert.Equal("Third slide", slide.PlainText));
    }

    [Fact]
    public void ImportSplitsLegacyBlankParagraphSlides()
    {
        const string rtf = @"{\rtf1\ansi{\fonttbl{\f0 Arial;}}\pard Chorus 1\par Come to my soul.\par \li0\par Verse 1\par Give me a heart.\par \li0\par}";
        var batch = new LegacySongBatch(
            _fixture.SongsDatabasePath,
            _fixture.SongWordsDatabasePath,
            "songs-hash",
            "words-hash",
            [
                new LegacySong(
                    "blank-slide-song",
                    RowId: 1,
                    Uid: "blank-slide-song",
                    "Blank Slide Song",
                    Author: null,
                    Copyright: null,
                    Tags: null,
                    RawJson: "{}",
                    Words:
                    [
                        new LegacySongWord(
                            RowId: 1,
                            Position: 0,
                            rtf,
                            "slide-1,slide-2",
                            SlideLayoutRevisions: null,
                            SlideRevisions: null,
                            RawJson: "{}"),
                    ]),
            ]);

        var result = _service.Import(batch);

        var song = _repository.Get(Assert.Single(result.SongIds));
        Assert.NotNull(song);
        Assert.Collection(
            song.Slides,
            slide => Assert.Equal("Chorus 1\nCome to my soul.", slide.PlainText),
            slide => Assert.Equal("Verse 1\nGive me a heart.", slide.PlainText));
    }

    [Fact]
    public void ImportUpdatesSearchIndex()
    {
        var batch = LegacySongReader.Read(
            _fixture.SongsDatabasePath,
            _fixture.SongWordsDatabasePath);
        var result = _service.Import(batch);

        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT SongId FROM SongFts WHERE SongFts MATCH 'grace';";

        Assert.Equal(result.SongIds[0].ToString(), command.ExecuteScalar());
    }

    [Fact]
    public void FailedImportRollsBackAllWrites()
    {
        using (var connection = _database.OpenConnection())
        {
            using var trigger = connection.CreateCommand();
            trigger.CommandText = """
                CREATE TRIGGER FailSongImport
                BEFORE INSERT ON Song
                WHEN NEW.Title = 'Fail'
                BEGIN
                    SELECT RAISE(ABORT, 'blocked by test');
                END;
                """;
            trigger.ExecuteNonQuery();
        }

        var batch = new LegacySongBatch(
            _fixture.SongsDatabasePath,
            _fixture.SongWordsDatabasePath,
            "songs-hash",
            "words-hash",
            [
                CreateLegacySong("good", "Good"),
                CreateLegacySong("fail", "Fail"),
            ]);

        Assert.Throws<SqliteException>(() => _service.Import(batch));
        Assert.Empty(_repository.List());
        Assert.Equal(0, CountRows("SongSlide"));
        Assert.Equal(0, CountRows("SongFts"));
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
        return Convert.ToInt64(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static LegacySong CreateLegacySong(string key, string title) =>
        new(
            key,
            RowId: 1,
            Uid: key,
            title,
            Author: null,
            Copyright: null,
            Tags: null,
            RawJson: "{}",
            Words: []);
}
