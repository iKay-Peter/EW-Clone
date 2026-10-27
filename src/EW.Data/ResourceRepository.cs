using EW.Domain.Media;
using EW.Domain.Resources;
using Microsoft.Data.Sqlite;

namespace EW.Data;

public sealed class ResourceRepository(SqliteDatabase database)
{
    private readonly SqliteDatabase _database = database ?? throw new ArgumentNullException(nameof(database));

    public Resource? Get(ResourceId id)
    {
        using var connection = _database.OpenConnection();
        return Get(connection, transaction: null, id);
    }

    internal static ResourceId? FindIdByLegacyId(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        string legacyId)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT Id FROM Resource WHERE LegacyId = $legacyId LIMIT 1;";
        command.Parameters.AddWithValue("$legacyId", legacyId);

        var value = command.ExecuteScalar();
        return value is string id ? new ResourceId(Guid.Parse(id)) : null;
    }

    internal static (ResourceId Id, bool Created) Upsert(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Resource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);

        var existingId = resource.LegacyId is null
            ? null
            : FindIdByLegacyId(connection, transaction, resource.LegacyId);

        var id = existingId ?? resource.Id;
        var created = existingId is null;

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = created
            ? """
                INSERT INTO Resource (
                    Id, LegacyId, ResourceType, TextRtf, TextPlain, MediaAssetId,
                    WebUrl, ShapeData, LegacyMetadata)
                VALUES (
                    $id, $legacyId, $resourceType, $textRtf, $textPlain, $mediaAssetId,
                    $webUrl, $shapeData, $legacyMetadata);
                """
            : """
                UPDATE Resource
                SET ResourceType = $resourceType,
                    TextRtf = $textRtf,
                    TextPlain = $textPlain,
                    MediaAssetId = $mediaAssetId,
                    WebUrl = $webUrl,
                    ShapeData = $shapeData,
                    LegacyMetadata = $legacyMetadata
                WHERE Id = $id;
                """;
        AddParameters(command, id, resource);
        command.ExecuteNonQuery();

        return (id, created);
    }

    private static Resource? Get(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        ResourceId id)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT Id, LegacyId, ResourceType, TextRtf, TextPlain, WebUrl, ShapeData,
                   MediaAssetId, LegacyMetadata
            FROM Resource
            WHERE Id = $id;
            """;
        command.Parameters.AddWithValue("$id", id.ToString());

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new Resource(
            new ResourceId(Guid.Parse(reader.GetString(0))),
            reader.IsDBNull(1) ? null : reader.GetString(1),
            reader.GetInt32(2),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetString(5),
            reader.IsDBNull(6) ? null : reader.GetString(6),
            reader.IsDBNull(7) ? null : new MediaAssetId(Guid.Parse(reader.GetString(7))),
            reader.IsDBNull(8) ? null : reader.GetString(8));
    }

    private static void AddParameters(SqliteCommand command, ResourceId id, Resource resource)
    {
        command.Parameters.AddWithValue("$id", id.ToString());
        command.Parameters.AddWithValue("$legacyId", (object?)resource.LegacyId ?? DBNull.Value);
        command.Parameters.AddWithValue("$resourceType", resource.ResourceType);
        command.Parameters.AddWithValue("$textRtf", (object?)resource.TextRtf ?? DBNull.Value);
        command.Parameters.AddWithValue("$textPlain", (object?)resource.TextPlain ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "$mediaAssetId",
            resource.MediaAssetId is { } mediaAssetId
                ? mediaAssetId.ToString()
                : DBNull.Value);
        command.Parameters.AddWithValue("$webUrl", (object?)resource.WebUrl ?? DBNull.Value);
        command.Parameters.AddWithValue("$shapeData", (object?)resource.ShapeData ?? DBNull.Value);
        command.Parameters.AddWithValue("$legacyMetadata", (object?)resource.LegacyMetadata ?? DBNull.Value);
    }
}
