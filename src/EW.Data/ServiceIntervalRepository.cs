using System.Globalization;
using EW.Domain.Scheduling;
using Microsoft.Data.Sqlite;

namespace EW.Data;

public sealed class ServiceIntervalRepository(SqliteDatabase database)
{
    private readonly SqliteDatabase _database = database ?? throw new ArgumentNullException(nameof(database));

    public ServiceInterval? Get(ServiceIntervalId id)
    {
        using var connection = _database.OpenConnection();
        return Get(connection, transaction: null, id);
    }

    public ServiceInterval? GetByLegacyId(string legacyId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(legacyId);

        using var connection = _database.OpenConnection();
        var id = FindIdByLegacyId(connection, transaction: null, legacyId);
        return id is null ? null : Get(connection, transaction: null, id.Value);
    }

    public IReadOnlyList<ServiceInterval> List()
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id
            FROM ServiceInterval
            ORDER BY StartUtc, EndUtc;
            """;

        var ids = new List<ServiceIntervalId>();
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                ids.Add(new ServiceIntervalId(Guid.Parse(reader.GetString(0))));
            }
        }

        return ids.Select(id => Get(connection, transaction: null, id))
            .OfType<ServiceInterval>()
            .ToArray();
    }

    internal static ServiceIntervalId? FindIdByLegacyId(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        string legacyId)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT Id FROM ServiceInterval WHERE LegacyId = $legacyId LIMIT 1;";
        command.Parameters.AddWithValue("$legacyId", legacyId);

        var value = command.ExecuteScalar();
        return value is string id ? new ServiceIntervalId(Guid.Parse(id)) : null;
    }

    internal static (ServiceIntervalId Id, bool Created) Upsert(
        SqliteConnection connection,
        SqliteTransaction transaction,
        ServiceInterval interval)
    {
        ArgumentNullException.ThrowIfNull(interval);

        var existingId = interval.LegacyId is null
            ? null
            : FindIdByLegacyId(connection, transaction, interval.LegacyId);

        var id = existingId ?? interval.Id;
        var created = existingId is null;

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = created
            ? """
                INSERT INTO ServiceInterval (
                    Id, LegacyId, Name, StartUtc, EndUtc, IsFixed, LegacyMetadata)
                VALUES (
                    $id, $legacyId, $name, $startUtc, $endUtc, $isFixed, $legacyMetadata);
                """
            : """
                UPDATE ServiceInterval
                SET Name = $name,
                    StartUtc = $startUtc,
                    EndUtc = $endUtc,
                    IsFixed = $isFixed,
                    LegacyMetadata = $legacyMetadata
                WHERE Id = $id;
                """;

        command.Parameters.AddWithValue("$id", id.ToString());
        command.Parameters.AddWithValue("$legacyId", (object?)interval.LegacyId ?? DBNull.Value);
        command.Parameters.AddWithValue("$name", (object?)interval.Name ?? DBNull.Value);
        command.Parameters.AddWithValue("$startUtc", (object?)interval.StartValue ?? DBNull.Value);
        command.Parameters.AddWithValue("$endUtc", (object?)interval.EndValue ?? DBNull.Value);
        command.Parameters.AddWithValue("$isFixed", interval.IsFixed ? 1 : 0);
        command.Parameters.AddWithValue(
            "$legacyMetadata",
            (object?)interval.LegacyMetadata ?? DBNull.Value);
        command.ExecuteNonQuery();

        return (id, created);
    }

    private static ServiceInterval? Get(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        ServiceIntervalId id)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT Id, LegacyId, Name, StartUtc, EndUtc, IsFixed, LegacyMetadata
            FROM ServiceInterval
            WHERE Id = $id;
            """;
        command.Parameters.AddWithValue("$id", id.ToString());

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new ServiceInterval(
            new ServiceIntervalId(Guid.Parse(reader.GetString(0))),
            reader.IsDBNull(1) ? null : reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.GetInt64(5) != 0,
            reader.IsDBNull(6) ? null : reader.GetString(6));
    }
}
