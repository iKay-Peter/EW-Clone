using EW.Domain.Settings;
using Microsoft.Data.Sqlite;

namespace EW.Data;

public sealed class SettingRepository(SqliteDatabase database)
{
    private readonly SqliteDatabase _database = database ?? throw new ArgumentNullException(nameof(database));

    public Setting? Get(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Key, Value, ValueType, LegacyMetadata
            FROM Setting
            WHERE Key = $key;
            """;
        command.Parameters.AddWithValue("$key", key);

        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadSetting(reader) : null;
    }

    public string? GetValue(string key) => Get(key)?.Value;

    public IReadOnlyList<Setting> List()
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Key, Value, ValueType, LegacyMetadata
            FROM Setting
            ORDER BY Key COLLATE NOCASE, Key;
            """;

        using var reader = command.ExecuteReader();
        var settings = new List<Setting>();
        while (reader.Read())
        {
            settings.Add(ReadSetting(reader));
        }

        return settings;
    }

    public void Set(Setting setting)
    {
        ArgumentNullException.ThrowIfNull(setting);
        ArgumentException.ThrowIfNullOrWhiteSpace(setting.Key);

        using var connection = _database.OpenConnection();
        using var transaction = connection.BeginTransaction();
        Upsert(connection, transaction, setting);
        transaction.Commit();
    }

    internal static bool Upsert(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Setting setting)
    {
        using var exists = connection.CreateCommand();
        exists.Transaction = transaction;
        exists.CommandText = "SELECT 1 FROM Setting WHERE Key = $key;";
        exists.Parameters.AddWithValue("$key", setting.Key);
        var created = exists.ExecuteScalar() is null;

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO Setting (Key, Value, ValueType, LegacyMetadata)
            VALUES ($key, $value, $valueType, $legacyMetadata)
            ON CONFLICT (Key) DO UPDATE SET
                Value = excluded.Value,
                ValueType = excluded.ValueType,
                LegacyMetadata = excluded.LegacyMetadata;
            """;
        command.Parameters.AddWithValue("$key", setting.Key);
        command.Parameters.AddWithValue("$value", (object?)setting.Value ?? DBNull.Value);
        command.Parameters.AddWithValue("$valueType", (object?)setting.ValueType ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "$legacyMetadata",
            (object?)setting.LegacyMetadata ?? DBNull.Value);
        command.ExecuteNonQuery();

        return created;
    }

    public bool Remove(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Setting WHERE Key = $key;";
        command.Parameters.AddWithValue("$key", key);
        return command.ExecuteNonQuery() > 0;
    }

    private static Setting ReadSetting(SqliteDataReader reader) => new(
        reader.GetString(0),
        reader.IsDBNull(1) ? null : reader.GetString(1),
        reader.IsDBNull(2) ? null : reader.GetString(2),
        reader.IsDBNull(3) ? null : reader.GetString(3));
}
