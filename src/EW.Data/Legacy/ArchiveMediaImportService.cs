using System.Text.Json;
using EW.Domain.Media;

namespace EW.Data.Legacy;

public sealed class ArchiveMediaImportService(SqliteDatabase database)
{
    private readonly SqliteDatabase _database = database ?? throw new ArgumentNullException(nameof(database));

    public ArchiveMediaImportResult Import(LegacyArchiveMediaManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        var created = 0;
        var updated = 0;
        var assetIds = new List<MediaAssetId>(manifest.Entries.Count);

        using var connection = _database.OpenConnection();
        using var transaction = connection.BeginTransaction();

        foreach (var entry in manifest.Entries)
        {
            var asset = Map(manifest, entry);
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
        return new ArchiveMediaImportResult(created, updated, assetIds);
    }

    private static MediaAsset Map(
        LegacyArchiveMediaManifest manifest,
        ExtractedArchiveMedia entry)
    {
        var metadata = new
        {
            importVersion = 1,
            source = new
            {
                archive = manifest.ArchivePath,
                archiveSha256 = manifest.ArchiveSha256,
            },
            entry = new
            {
                entry.ArchiveEntry,
                entry.OutputPath,
                entry.SizeBytes,
                entry.Sha256,
            },
        };

        return new MediaAsset(
            MediaAssetId.New(),
            $"archive-media:{entry.Sha256}",
            entry.OutputPath,
            entry.Sha256,
            MediaType: 0,
            Width: null,
            Height: null,
            DurationMs: null,
            TrimStartMs: null,
            TrimEndMs: null,
            PosterPath: null,
            AlphaMode: null,
            Status: 1,
            ErrorCode: null,
            ErrorMessage: null,
            JsonSerializer.Serialize(metadata));
    }
}

public sealed record ArchiveMediaImportResult(
    int Created,
    int Updated,
    IReadOnlyList<MediaAssetId> MediaAssetIds);
