using System.Globalization;
using EW.Domain.Themes;
using Microsoft.Data.Sqlite;

namespace EW.Data;

public sealed class ThemeRepository(SqliteDatabase database)
{
    private readonly SqliteDatabase _database = database ?? throw new ArgumentNullException(nameof(database));

    public Theme? Get(ThemeId id)
    {
        using var connection = _database.OpenConnection();
        return Get(connection, transaction: null, id);
    }

    public Theme? GetByLegacyId(string legacyId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(legacyId);

        using var connection = _database.OpenConnection();
        var id = FindIdByLegacyId(connection, transaction: null, legacyId);
        return id is null ? null : Get(connection, transaction: null, id.Value);
    }

    public IReadOnlyList<Theme> List()
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id FROM Theme ORDER BY Name COLLATE NOCASE;";

        var ids = new List<ThemeId>();
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                ids.Add(new ThemeId(Guid.Parse(reader.GetString(0))));
            }
        }

        return ids.Select(id => Get(connection, transaction: null, id)).OfType<Theme>().ToArray();
    }

    internal static ThemeId? FindIdByLegacyId(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        string legacyId)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT Id FROM Theme WHERE LegacyId = $legacyId LIMIT 1;";
        command.Parameters.AddWithValue("$legacyId", legacyId);

        var value = command.ExecuteScalar();
        return value is string id ? new ThemeId(Guid.Parse(id)) : null;
    }

    internal static (ThemeId Id, bool Created) Upsert(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Theme theme)
    {
        ArgumentNullException.ThrowIfNull(theme);

        var existingId = theme.LegacyId is null
            ? null
            : FindIdByLegacyId(connection, transaction, theme.LegacyId);

        var id = existingId ?? theme.Id;
        var created = existingId is null;
        var now = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);

        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = created
                ? """
                    INSERT INTO Theme (
                        Id, LegacyId, Name, SourcePath, StyleJson, LegacyMetadata,
                        CreatedUtc, ModifiedUtc)
                    VALUES (
                        $id, $legacyId, $name, $sourcePath, $styleJson, $legacyMetadata,
                        $createdUtc, $modifiedUtc);
                    """
                : """
                    UPDATE Theme
                    SET Name = $name,
                        SourcePath = $sourcePath,
                        StyleJson = $styleJson,
                        LegacyMetadata = $legacyMetadata,
                        ModifiedUtc = $modifiedUtc
                    WHERE Id = $id;
                    """;

            command.Parameters.AddWithValue("$id", id.ToString());
            command.Parameters.AddWithValue("$legacyId", (object?)theme.LegacyId ?? DBNull.Value);
            command.Parameters.AddWithValue("$name", theme.Name);
            command.Parameters.AddWithValue("$sourcePath", (object?)theme.SourcePath ?? DBNull.Value);
            command.Parameters.AddWithValue("$styleJson", (object?)theme.StyleJson ?? DBNull.Value);
            command.Parameters.AddWithValue(
                "$legacyMetadata",
                (object?)theme.LegacyMetadata ?? DBNull.Value);
            command.Parameters.AddWithValue("$createdUtc", now);
            command.Parameters.AddWithValue("$modifiedUtc", now);
            command.ExecuteNonQuery();
        }

        if (!created)
        {
            using var deleteSearch = connection.CreateCommand();
            deleteSearch.Transaction = transaction;
            deleteSearch.CommandText = "DELETE FROM ThemeFts WHERE ThemeId = $id;";
            deleteSearch.Parameters.AddWithValue("$id", id.ToString());
            deleteSearch.ExecuteNonQuery();
        }

        using (var search = connection.CreateCommand())
        {
            search.Transaction = transaction;
            search.CommandText = """
                INSERT INTO ThemeFts (ThemeId, Name, Tags)
                VALUES ($themeId, $name, NULL);
                """;
            search.Parameters.AddWithValue("$themeId", id.ToString());
            search.Parameters.AddWithValue("$name", theme.Name);
            search.ExecuteNonQuery();
        }

        return (id, created);
    }

    private static Theme? Get(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        ThemeId id)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT Id, LegacyId, Name, SourcePath, StyleJson, LegacyMetadata
            FROM Theme
            WHERE Id = $id;
            """;
        command.Parameters.AddWithValue("$id", id.ToString());

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new Theme(
            new ThemeId(Guid.Parse(reader.GetString(0))),
            reader.IsDBNull(1) ? null : reader.GetString(1),
            reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetString(5));
    }
}
