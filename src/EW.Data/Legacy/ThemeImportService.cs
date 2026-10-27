using System.Text.Json;
using EW.Domain.Themes;

namespace EW.Data.Legacy;

public sealed class ThemeImportService(SqliteDatabase database)
{
    private readonly SqliteDatabase _database = database ?? throw new ArgumentNullException(nameof(database));

    public ThemeImportResult Import(LegacyThemeBatch batch)
    {
        ArgumentNullException.ThrowIfNull(batch);

        var theme = Map(batch);
        using var connection = _database.OpenConnection();
        using var transaction = connection.BeginTransaction();

        var (id, created) = ThemeRepository.Upsert(connection, transaction, theme);
        transaction.Commit();

        return new ThemeImportResult(created ? 1 : 0, created ? 0 : 1, id);
    }

    private static Theme Map(LegacyThemeBatch batch)
    {
        var metadata = new
        {
            importVersion = 1,
            source = new
            {
                archive = batch.ArchivePath,
                sha256 = batch.ArchiveSha256,
                entries = batch.Entries.Select(entry => new
                {
                    entry.FullName,
                    entry.Length,
                    entry.CompressedLength,
                }),
            },
        };

        return new Theme(
            ThemeId.New(),
            batch.Theme.LegacyKey,
            batch.Theme.Name,
            batch.ArchivePath,
            batch.Theme.StyleJson,
            JsonSerializer.Serialize(metadata));
    }
}

public sealed record ThemeImportResult(
    int Created,
    int Updated,
    ThemeId ThemeId);
