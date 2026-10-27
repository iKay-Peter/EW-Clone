using System.Text.Json;
using EW.Domain.Songs;

namespace EW.Data.Legacy;

public sealed class SongImportService(SqliteDatabase database)
{
    private readonly SqliteDatabase _database = database ?? throw new ArgumentNullException(nameof(database));

    public SongImportResult Import(LegacySongBatch batch)
    {
        ArgumentNullException.ThrowIfNull(batch);

        var created = 0;
        var updated = 0;
        var songIds = new List<SongId>(batch.Songs.Count);

        using var connection = _database.OpenConnection();
        using var transaction = connection.BeginTransaction();

        foreach (var legacySong in batch.Songs)
        {
            var song = Map(batch, legacySong);
            var (id, wasCreated) = SongRepository.Upsert(connection, transaction, song);
            songIds.Add(id);

            if (wasCreated)
            {
                created++;
            }
            else
            {
                updated++;
            }
        }

        transaction.Commit();
        return new SongImportResult(created, updated, songIds);
    }

    private static Song Map(LegacySongBatch batch, LegacySong legacySong)
    {
        var slides = legacySong.Words
            .SelectMany(word => RtfSlides.Split(word.Rtf))
            .Select((slide, position) => new SongSlide(
                SongSlideId.New(),
                position,
                SectionLabel: null,
                slide.ContentRtf,
                slide.PlainText))
            .ToArray();

        return new Song(
            SongId.New(),
            legacySong.LegacyKey,
            legacySong.Title,
            legacySong.Author,
            legacySong.Copyright,
            legacySong.Tags,
            slides,
            BuildMetadata(batch, legacySong));
    }

    private static string BuildMetadata(LegacySongBatch batch, LegacySong legacySong)
    {
        var metadata = new
        {
            importVersion = 1,
            source = new
            {
                songsDatabase = batch.SongsDatabasePath,
                songWordsDatabase = batch.SongWordsDatabasePath,
                songsSha256 = batch.SongsSha256,
                songWordsSha256 = batch.SongWordsSha256,
            },
            song = JsonSerializer.Deserialize<JsonElement>(legacySong.RawJson),
            words = legacySong.Words
                .Select(word => JsonSerializer.Deserialize<JsonElement>(word.RawJson))
                .ToArray(),
        };

        return JsonSerializer.Serialize(metadata);
    }
}

public sealed record SongImportResult(
    int Created,
    int Updated,
    IReadOnlyList<SongId> SongIds);
