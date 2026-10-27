using System.Globalization;
using Microsoft.Data.Sqlite;

namespace EW.Data.Migrations;

internal static class DatabaseMigrator
{
    private static readonly Migration[] Migrations =
    [
        new Migration(1, "Initial clone schema", SchemaV1.Sql),
        new Migration(2, "Legacy identity indexes", SchemaV2.Sql),
        new Migration(3, "Presentation resource and element fields", SchemaV3.Sql),
        new Migration(4, "Service interval legacy identity", SchemaV4.Sql),
    ];

    internal static int CurrentVersion { get; } = Migrations[^1].Version;

    public static void Migrate(SqliteConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        using var transaction = connection.BeginTransaction();

        var currentVersion = GetUserVersion(connection, transaction);
        foreach (var migration in Migrations.Where(migration => migration.Version > currentVersion))
        {
            Execute(connection, transaction, migration.Sql);
            SetUserVersion(connection, transaction, migration.Version);
        }

        transaction.Commit();
    }

    private static int GetUserVersion(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "PRAGMA user_version;";
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private static void SetUserVersion(
        SqliteConnection connection,
        SqliteTransaction transaction,
        int version)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"PRAGMA user_version = {version.ToString(CultureInfo.InvariantCulture)};";
        command.ExecuteNonQuery();
    }

    private static void Execute(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string sql)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private sealed record Migration(int Version, string Name, string Sql);
}
