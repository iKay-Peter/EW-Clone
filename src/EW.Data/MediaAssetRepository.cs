using System.Globalization;
using EW.Domain.Media;
using Microsoft.Data.Sqlite;

namespace EW.Data;

public sealed class MediaAssetRepository(SqliteDatabase database)
{
    private readonly SqliteDatabase _database = database ?? throw new ArgumentNullException(nameof(database));

    public MediaAsset? Get(MediaAssetId id)
    {
        using var connection = _database.OpenConnection();
        return Get(connection, transaction: null, id);
    }

    public MediaAsset? GetByLegacyId(string legacyId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(legacyId);

        using var connection = _database.OpenConnection();
        var id = FindIdByLegacyId(connection, transaction: null, legacyId);
        return id is null ? null : Get(connection, transaction: null, id.Value);
    }

    public IReadOnlyList<MediaAsset> List()
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id FROM MediaAsset ORDER BY Path COLLATE NOCASE;";

        var ids = new List<MediaAssetId>();
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                ids.Add(new MediaAssetId(Guid.Parse(reader.GetString(0))));
            }
        }

        return ids.Select(id => Get(connection, transaction: null, id)).OfType<MediaAsset>().ToArray();
    }

    internal static MediaAssetId? FindIdByLegacyId(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        string legacyId)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT Id FROM MediaAsset WHERE LegacyId = $legacyId LIMIT 1;";
        command.Parameters.AddWithValue("$legacyId", legacyId);

        var value = command.ExecuteScalar();
        return value is string id ? new MediaAssetId(Guid.Parse(id)) : null;
    }

    internal static MediaAssetId? FindIdByHash(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        string hash)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT Id FROM MediaAsset WHERE Hash = $hash COLLATE NOCASE LIMIT 1;";
        command.Parameters.AddWithValue("$hash", hash);

        var value = command.ExecuteScalar();
        return value is string id ? new MediaAssetId(Guid.Parse(id)) : null;
    }

    internal static (MediaAssetId Id, bool Created) Upsert(
        SqliteConnection connection,
        SqliteTransaction transaction,
        MediaAsset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);

        var existingId = asset.LegacyId is null
            ? null
            : FindIdByLegacyId(connection, transaction, asset.LegacyId);

        var id = existingId ?? asset.Id;
        var created = existingId is null;
        var now = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);

        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = created
                ? """
                    INSERT INTO MediaAsset (
                        Id, LegacyId, Path, Hash, MediaType, Width, Height, DurationMs,
                        TrimStartMs, TrimEndMs, PosterPath, AlphaMode, Status, ErrorCode,
                        ErrorMessage, LegacyMetadata, CreatedUtc, ModifiedUtc)
                    VALUES (
                        $id, $legacyId, $path, $hash, $mediaType, $width, $height, $durationMs,
                        $trimStartMs, $trimEndMs, $posterPath, $alphaMode, $status, $errorCode,
                        $errorMessage, $legacyMetadata, $createdUtc, $modifiedUtc);
                    """
                : """
                    UPDATE MediaAsset
                    SET Path = $path,
                        Hash = $hash,
                        MediaType = $mediaType,
                        Width = $width,
                        Height = $height,
                        DurationMs = $durationMs,
                        TrimStartMs = $trimStartMs,
                        TrimEndMs = $trimEndMs,
                        PosterPath = $posterPath,
                        AlphaMode = $alphaMode,
                        Status = $status,
                        ErrorCode = $errorCode,
                        ErrorMessage = $errorMessage,
                        LegacyMetadata = $legacyMetadata,
                        ModifiedUtc = $modifiedUtc
                    WHERE Id = $id;
                    """;

            AddParameters(command, id, asset, now);
            command.ExecuteNonQuery();
        }

        if (!created)
        {
            using var deleteSearch = connection.CreateCommand();
            deleteSearch.Transaction = transaction;
            deleteSearch.CommandText = "DELETE FROM MediaFts WHERE MediaAssetId = $id;";
            deleteSearch.Parameters.AddWithValue("$id", id.ToString());
            deleteSearch.ExecuteNonQuery();
        }

        using (var search = connection.CreateCommand())
        {
            search.Transaction = transaction;
            search.CommandText = """
                INSERT INTO MediaFts (MediaAssetId, Title, Tags)
                VALUES ($mediaAssetId, $title, NULL);
                """;
            search.Parameters.AddWithValue("$mediaAssetId", id.ToString());
            search.Parameters.AddWithValue("$title", GetTitle(asset.Path));
            search.ExecuteNonQuery();
        }

        return (id, created);
    }

    private static MediaAsset? Get(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        MediaAssetId id)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT Id, LegacyId, Path, Hash, MediaType, Width, Height, DurationMs,
                   TrimStartMs, TrimEndMs, PosterPath, AlphaMode, Status, ErrorCode,
                   ErrorMessage, LegacyMetadata
            FROM MediaAsset
            WHERE Id = $id;
            """;
        command.Parameters.AddWithValue("$id", id.ToString());

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new MediaAsset(
            new MediaAssetId(Guid.Parse(reader.GetString(0))),
            reader.IsDBNull(1) ? null : reader.GetString(1),
            reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.GetInt32(4),
            reader.IsDBNull(5) ? null : reader.GetInt32(5),
            reader.IsDBNull(6) ? null : reader.GetInt32(6),
            reader.IsDBNull(7) ? null : reader.GetInt64(7),
            reader.IsDBNull(8) ? null : reader.GetInt64(8),
            reader.IsDBNull(9) ? null : reader.GetInt64(9),
            reader.IsDBNull(10) ? null : reader.GetString(10),
            reader.IsDBNull(11) ? null : reader.GetInt32(11),
            reader.GetInt32(12),
            reader.IsDBNull(13) ? null : reader.GetString(13),
            reader.IsDBNull(14) ? null : reader.GetString(14),
            reader.IsDBNull(15) ? null : reader.GetString(15));
    }

    private static void AddParameters(
        SqliteCommand command,
        MediaAssetId id,
        MediaAsset asset,
        string now)
    {
        command.Parameters.AddWithValue("$id", id.ToString());
        command.Parameters.AddWithValue("$legacyId", (object?)asset.LegacyId ?? DBNull.Value);
        command.Parameters.AddWithValue("$path", asset.Path);
        command.Parameters.AddWithValue("$hash", (object?)asset.Hash ?? DBNull.Value);
        command.Parameters.AddWithValue("$mediaType", asset.MediaType);
        command.Parameters.AddWithValue("$width", (object?)asset.Width ?? DBNull.Value);
        command.Parameters.AddWithValue("$height", (object?)asset.Height ?? DBNull.Value);
        command.Parameters.AddWithValue("$durationMs", (object?)asset.DurationMs ?? DBNull.Value);
        command.Parameters.AddWithValue("$trimStartMs", (object?)asset.TrimStartMs ?? DBNull.Value);
        command.Parameters.AddWithValue("$trimEndMs", (object?)asset.TrimEndMs ?? DBNull.Value);
        command.Parameters.AddWithValue("$posterPath", (object?)asset.PosterPath ?? DBNull.Value);
        command.Parameters.AddWithValue("$alphaMode", (object?)asset.AlphaMode ?? DBNull.Value);
        command.Parameters.AddWithValue("$status", asset.Status);
        command.Parameters.AddWithValue("$errorCode", (object?)asset.ErrorCode ?? DBNull.Value);
        command.Parameters.AddWithValue("$errorMessage", (object?)asset.ErrorMessage ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "$legacyMetadata",
            (object?)asset.LegacyMetadata ?? DBNull.Value);
        command.Parameters.AddWithValue("$createdUtc", now);
        command.Parameters.AddWithValue("$modifiedUtc", now);
    }

    private static string GetTitle(string path)
    {
        var title = Path.GetFileName(path);
        return string.IsNullOrWhiteSpace(title) ? path : title;
    }
}
