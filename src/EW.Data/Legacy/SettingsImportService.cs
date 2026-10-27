using System.Text.Json;
using EW.Domain.Settings;

namespace EW.Data.Legacy;

public sealed class SettingsImportService(SqliteDatabase database)
{
    private readonly SqliteDatabase _database = database ?? throw new ArgumentNullException(nameof(database));

    public SettingsImportResult Import(LegacyIniFile file)
    {
        ArgumentNullException.ThrowIfNull(file);

        var sourceFile = Path.GetFileName(file.SourcePath);
        var imported = new List<(string Key, Setting Setting)>();
        var seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var legacySetting in file.Settings)
        {
            var key = $"{sourceFile}/{legacySetting.Section}/{legacySetting.Key}";
            if (seen.TryGetValue(key, out var index))
            {
                imported[index] = (key, Map(file, legacySetting, key));
            }
            else
            {
                seen[key] = imported.Count;
                imported.Add((key, Map(file, legacySetting, key)));
            }
        }

        var created = 0;
        var updated = 0;
        var keys = new List<string>(imported.Count);

        using var connection = _database.OpenConnection();
        using var transaction = connection.BeginTransaction();

        foreach (var (key, setting) in imported)
        {
            if (SettingRepository.Upsert(connection, transaction, setting))
            {
                created++;
            }
            else
            {
                updated++;
            }

            keys.Add(key);
        }

        transaction.Commit();

        return new SettingsImportResult(created, updated, keys, file.Warnings);
    }

    private static Setting Map(LegacyIniFile file, LegacyIniSetting legacySetting, string key)
    {
        var metadata = new
        {
            importVersion = 1,
            source = new
            {
                file = Path.GetFileName(file.SourcePath),
                path = file.SourcePath,
                sha256 = file.SourceSha256,
            },
            section = legacySetting.Section,
            key = legacySetting.Key,
            line = legacySetting.LineNumber,
            raw = legacySetting.RawLine,
        };

        return new Setting(
            key,
            legacySetting.Value,
            "string",
            JsonSerializer.Serialize(metadata));
    }
}

public sealed record SettingsImportResult(
    int Created,
    int Updated,
    IReadOnlyList<string> Keys,
    IReadOnlyList<string> Warnings);
