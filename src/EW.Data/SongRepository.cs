using System.Globalization;
using EW.Domain.Songs;
using Microsoft.Data.Sqlite;

namespace EW.Data;

public sealed class SongRepository(SqliteDatabase database)
{
    private readonly SqliteDatabase _database = database ?? throw new ArgumentNullException(nameof(database));

    public Song? Get(SongId id)
    {
        using var connection = _database.OpenConnection();
        return Get(connection, transaction: null, id);
    }

    public Song? GetByLegacyId(string legacyId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(legacyId);

        using var connection = _database.OpenConnection();
        var id = FindIdByLegacyId(connection, transaction: null, legacyId);
        return id is null ? null : Get(connection, transaction: null, id.Value);
    }

    public IReadOnlyList<Song> List()
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id FROM Song ORDER BY Title COLLATE NOCASE;";

        var ids = new List<SongId>();
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                ids.Add(new SongId(Guid.Parse(reader.GetString(0))));
            }
        }

        return ids.Select(id => Get(connection, transaction: null, id)).OfType<Song>().ToArray();
    }

    internal static SongId? FindIdByLegacyId(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        string legacyId)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT Id FROM Song WHERE LegacyId = $legacyId LIMIT 1;";
        command.Parameters.AddWithValue("$legacyId", legacyId);

        var value = command.ExecuteScalar();
        return value is string id ? new SongId(Guid.Parse(id)) : null;
    }

    internal static (SongId Id, bool Created) Upsert(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Song song)
    {
        ArgumentNullException.ThrowIfNull(song);

        var existingId = song.LegacyId is null
            ? null
            : FindIdByLegacyId(connection, transaction, song.LegacyId);

        var id = existingId ?? song.Id;
        var created = existingId is null;
        var now = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        var lyrics = string.Join('\n', song.Slides.Select(slide => slide.PlainText));
        var searchText = JoinSearchText(song.Title, song.Author, song.Tags, lyrics);

        if (created)
        {
            using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = """
                INSERT INTO Song (
                    Id, LegacyId, Title, Author, Copyright, Tags, SearchText,
                    LegacyMetadata, CreatedUtc, ModifiedUtc)
                VALUES (
                    $id, $legacyId, $title, $author, $copyright, $tags, $searchText,
                    $legacyMetadata, $createdUtc, $modifiedUtc);
                """;
            AddSongParameters(insert, id, song, searchText, now);
            insert.ExecuteNonQuery();
        }
        else
        {
            using var update = connection.CreateCommand();
            update.Transaction = transaction;
            update.CommandText = """
                UPDATE Song
                SET Title = $title,
                    Author = $author,
                    Copyright = $copyright,
                    Tags = $tags,
                    SearchText = $searchText,
                    LegacyMetadata = $legacyMetadata,
                    ModifiedUtc = $modifiedUtc
                WHERE Id = $id;
                """;
            AddSongParameters(update, id, song, searchText, now);
            update.ExecuteNonQuery();

            using var deleteSlides = connection.CreateCommand();
            deleteSlides.Transaction = transaction;
            deleteSlides.CommandText = "DELETE FROM SongSlide WHERE SongId = $id;";
            deleteSlides.Parameters.AddWithValue("$id", id.ToString());
            deleteSlides.ExecuteNonQuery();

            using var deleteSearch = connection.CreateCommand();
            deleteSearch.Transaction = transaction;
            deleteSearch.CommandText = "DELETE FROM SongFts WHERE SongId = $id;";
            deleteSearch.Parameters.AddWithValue("$id", id.ToString());
            deleteSearch.ExecuteNonQuery();
        }

        InsertSlides(connection, transaction, id, song.Slides);

        using var search = connection.CreateCommand();
        search.Transaction = transaction;
        search.CommandText = """
            INSERT INTO SongFts (SongId, Title, Author, Tags, Lyrics)
            VALUES ($songId, $title, $author, $tags, $lyrics);
            """;
        search.Parameters.AddWithValue("$songId", id.ToString());
        search.Parameters.AddWithValue("$title", song.Title);
        search.Parameters.AddWithValue("$author", (object?)song.Author ?? DBNull.Value);
        search.Parameters.AddWithValue("$tags", (object?)song.Tags ?? DBNull.Value);
        search.Parameters.AddWithValue("$lyrics", lyrics);
        search.ExecuteNonQuery();

        return (id, created);
    }

    private static Song? Get(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        SongId id)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT Id, LegacyId, Title, Author, Copyright, Tags, LegacyMetadata
            FROM Song
            WHERE Id = $id;
            """;
        command.Parameters.AddWithValue("$id", id.ToString());

        SongId songId;
        string? legacyId;
        string title;
        string? author;
        string? copyright;
        string? tags;
        string? legacyMetadata;

        using (var reader = command.ExecuteReader())
        {
            if (!reader.Read())
            {
                return null;
            }

            songId = new SongId(Guid.Parse(reader.GetString(0)));
            legacyId = reader.IsDBNull(1) ? null : reader.GetString(1);
            title = reader.GetString(2);
            author = reader.IsDBNull(3) ? null : reader.GetString(3);
            copyright = reader.IsDBNull(4) ? null : reader.GetString(4);
            tags = reader.IsDBNull(5) ? null : reader.GetString(5);
            legacyMetadata = reader.IsDBNull(6) ? null : reader.GetString(6);
        }

        return new Song(
            songId,
            legacyId,
            title,
            author,
            copyright,
            tags,
            ReadSlides(connection, transaction, songId),
            legacyMetadata);
    }

    private static List<SongSlide> ReadSlides(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        SongId songId)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT Id, Position, SectionLabel, ContentRtf, PlainText
            FROM SongSlide
            WHERE SongId = $songId
            ORDER BY Position;
            """;
        command.Parameters.AddWithValue("$songId", songId.ToString());

        using var reader = command.ExecuteReader();
        var slides = new List<SongSlide>();
        while (reader.Read())
        {
            slides.Add(new SongSlide(
                new SongSlideId(Guid.Parse(reader.GetString(0))),
                reader.GetInt32(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4)));
        }

        return slides;
    }

    private static void InsertSlides(
        SqliteConnection connection,
        SqliteTransaction transaction,
        SongId songId,
        IReadOnlyList<SongSlide> slides)
    {
        foreach (var slide in slides)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO SongSlide (Id, SongId, Position, SectionLabel, ContentRtf, PlainText)
                VALUES ($id, $songId, $position, $sectionLabel, $contentRtf, $plainText);
                """;
            command.Parameters.AddWithValue("$id", slide.Id.ToString());
            command.Parameters.AddWithValue("$songId", songId.ToString());
            command.Parameters.AddWithValue("$position", slide.Position);
            command.Parameters.AddWithValue("$sectionLabel", (object?)slide.SectionLabel ?? DBNull.Value);
            command.Parameters.AddWithValue("$contentRtf", slide.ContentRtf);
            command.Parameters.AddWithValue("$plainText", slide.PlainText);
            command.ExecuteNonQuery();
        }
    }

    private static void AddSongParameters(
        SqliteCommand command,
        SongId id,
        Song song,
        string searchText,
        string now)
    {
        command.Parameters.AddWithValue("$id", id.ToString());
        command.Parameters.AddWithValue("$legacyId", (object?)song.LegacyId ?? DBNull.Value);
        command.Parameters.AddWithValue("$title", song.Title);
        command.Parameters.AddWithValue("$author", (object?)song.Author ?? DBNull.Value);
        command.Parameters.AddWithValue("$copyright", (object?)song.Copyright ?? DBNull.Value);
        command.Parameters.AddWithValue("$tags", (object?)song.Tags ?? DBNull.Value);
        command.Parameters.AddWithValue("$searchText", searchText);
        command.Parameters.AddWithValue("$legacyMetadata", (object?)song.LegacyMetadata ?? DBNull.Value);
        command.Parameters.AddWithValue("$createdUtc", now);
        command.Parameters.AddWithValue("$modifiedUtc", now);
    }

    private static string JoinSearchText(params string?[] values) =>
        string.Join(
            '\n',
            values.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!));
}
