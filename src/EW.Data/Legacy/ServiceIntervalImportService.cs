using System.Text.Json;
using EW.Domain.Scheduling;

namespace EW.Data.Legacy;

public sealed class ServiceIntervalImportService(SqliteDatabase database)
{
    private readonly SqliteDatabase _database = database ?? throw new ArgumentNullException(nameof(database));

    public ServiceIntervalImportResult Import(LegacyServiceIntervalBatch batch)
    {
        ArgumentNullException.ThrowIfNull(batch);

        var created = 0;
        var updated = 0;
        var intervalIds = new List<ServiceIntervalId>(batch.Intervals.Count);

        using var connection = _database.OpenConnection();
        using var transaction = connection.BeginTransaction();

        foreach (var legacyInterval in batch.Intervals)
        {
            var interval = Map(batch, legacyInterval);
            var (id, wasCreated) = ServiceIntervalRepository.Upsert(connection, transaction, interval);
            intervalIds.Add(id);

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
        return new ServiceIntervalImportResult(created, updated, intervalIds);
    }

    private static ServiceInterval Map(
        LegacyServiceIntervalBatch batch,
        LegacyServiceInterval legacyInterval)
    {
        var metadata = new
        {
            importVersion = 1,
            source = new
            {
                database = batch.SourcePath,
                sha256 = batch.SourceSha256,
            },
            interval = JsonSerializer.Deserialize<JsonElement>(legacyInterval.RawJson),
        };

        return new ServiceInterval(
            ServiceIntervalId.New(),
            legacyInterval.LegacyKey,
            Name: null,
            legacyInterval.StartValue,
            legacyInterval.EndValue,
            legacyInterval.StartFixed || legacyInterval.EndFixed,
            JsonSerializer.Serialize(metadata));
    }
}

public sealed record ServiceIntervalImportResult(
    int Created,
    int Updated,
    IReadOnlyList<ServiceIntervalId> ServiceIntervalIds);
