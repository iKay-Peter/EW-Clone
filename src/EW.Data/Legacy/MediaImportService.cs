using System.Globalization;
using System.Text.Json;
using EW.Domain.Media;

namespace EW.Data.Legacy;

public sealed class MediaImportService(SqliteDatabase database)
{
    private readonly SqliteDatabase _database = database ?? throw new ArgumentNullException(nameof(database));

    public MediaImportResult Import(LegacyMediaBatch batch)
    {
        ArgumentNullException.ThrowIfNull(batch);

        var created = 0;
        var updated = 0;
        var assetIds = new List<MediaAssetId>(batch.Assets.Count);

        using var connection = _database.OpenConnection();
        using var transaction = connection.BeginTransaction();

        foreach (var legacyAsset in batch.Assets)
        {
            var asset = Map(batch, legacyAsset);
            var (id, wasCreated) = MediaAssetRepository.Upsert(connection, transaction, asset);
            assetIds.Add(id);

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
        return new MediaImportResult(created, updated, assetIds);
    }

    private static MediaAsset Map(LegacyMediaBatch batch, LegacyMediaAsset legacyAsset)
    {
        return new MediaAsset(
            MediaAssetId.New(),
            legacyAsset.LegacyKey,
            legacyAsset.Path,
            legacyAsset.Hash,
            legacyAsset.MediaType,
            legacyAsset.Width,
            legacyAsset.Height,
            DurationMs: null,
            TrimStartMs: null,
            TrimEndMs: null,
            PosterPath: null,
            legacyAsset.AlphaMode,
            legacyAsset.Ready ? 1 : 0,
            ErrorCode: legacyAsset.ErrorNumber?.ToString(CultureInfo.InvariantCulture),
            legacyAsset.ErrorMessage,
            BuildMetadata(batch, legacyAsset));
    }

    private static string BuildMetadata(LegacyMediaBatch batch, LegacyMediaAsset legacyAsset)
    {
        var metadata = new
        {
            importVersion = 1,
            source = new
            {
                database = batch.SourcePath,
                sha256 = batch.SourceSha256,
            },
            media = JsonSerializer.Deserialize<JsonElement>(legacyAsset.RawJson),
        };

        return JsonSerializer.Serialize(metadata);
    }
}

public sealed record MediaImportResult(
    int Created,
    int Updated,
    IReadOnlyList<MediaAssetId> MediaAssetIds);
