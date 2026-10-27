using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace EW.Data.Legacy;

internal static class LegacyDatabaseReader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
    };

    public static string FileSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    public static List<LegacyTableRow> ReadTable(string databasePath, string tableName)
    {
        if (!File.Exists(databasePath))
        {
            throw new FileNotFoundException("Legacy database not found.", databasePath);
        }

        using var connection = OpenReadOnly(databasePath);
        if (!TableExists(connection, tableName))
        {
            return [];
        }

        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT * FROM \"{EscapeIdentifier(tableName)}\" ORDER BY rowid;";

        using var reader = command.ExecuteReader();
        var rows = new List<LegacyTableRow>();
        while (reader.Read())
        {
            var values = new Dictionary<string, object?>(StringComparer.Ordinal);
            for (var index = 0; index < reader.FieldCount; index++)
            {
                var name = reader.GetName(index);
                values[name] = NormalizeValue(reader.GetValue(index));
            }

            if (!TryGetInt64(values, "rowid", out var rowId))
            {
                rowId = rows.Count;
            }

            rows.Add(new LegacyTableRow(rowId, values, JsonSerializer.Serialize(values, JsonOptions)));
        }

        return rows;
    }

    public static string? GetString(IReadOnlyDictionary<string, object?> values, string key)
    {
        if (!values.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }

        return Convert.ToString(value, CultureInfo.InvariantCulture);
    }

    public static bool TryGetInt64(
        IReadOnlyDictionary<string, object?> values,
        string key,
        out long result)
    {
        result = 0;
        if (!values.TryGetValue(key, out var value) || value is null)
        {
            return false;
        }

        if (value is long longValue)
        {
            result = longValue;
            return true;
        }

        return long.TryParse(
            Convert.ToString(value, CultureInfo.InvariantCulture),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out result);
    }

    public static long? GetInt64(IReadOnlyDictionary<string, object?> values, string key) =>
        TryGetInt64(values, key, out var value) ? value : null;

    public static double? GetDouble(IReadOnlyDictionary<string, object?> values, string key)
    {
        if (!values.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }

        if (value is double doubleValue)
        {
            return doubleValue;
        }

        return double.TryParse(
            Convert.ToString(value, CultureInfo.InvariantCulture),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var result)
            ? result
            : null;
    }

    public static bool GetBoolean(IReadOnlyDictionary<string, object?> values, string key)
    {
        if (!values.TryGetValue(key, out var value) || value is null)
        {
            return false;
        }

        if (value is long longValue)
        {
            return longValue != 0;
        }

        if (value is bool booleanValue)
        {
            return booleanValue;
        }

        return bool.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), out var parsed) && parsed;
    }

    private static SqliteConnection OpenReadOnly(string databasePath)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadOnly,
            Cache = SqliteCacheMode.Shared,
            Pooling = false,
        }.ToString();

        var connection = new SqliteConnection(connectionString);
        connection.Open();
        LegacyCollation.EnsureRegistered(connection);

        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA query_only = ON;";
        command.ExecuteNonQuery();

        return connection;
    }

    private static bool TableExists(SqliteConnection connection, string tableName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
            FROM sqlite_master
            WHERE type = 'table'
              AND name = $name;
            """;
        command.Parameters.AddWithValue("$name", tableName);
        return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture) > 0;
    }

    private static object? NormalizeValue(object value) => value switch
    {
        DBNull => null,
        byte[] bytes => Convert.ToBase64String(bytes),
        _ => value,
    };

    private static string EscapeIdentifier(string identifier) =>
        identifier.Replace("\"", "\"\"", StringComparison.Ordinal);
}

internal sealed record LegacyTableRow(
    long RowId,
    IReadOnlyDictionary<string, object?> Values,
    string RawJson);
