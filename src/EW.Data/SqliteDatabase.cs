using EW.Data.Migrations;
using Microsoft.Data.Sqlite;

namespace EW.Data;

public sealed class SqliteDatabase
{
    private readonly string _connectionString;

    static SqliteDatabase()
    {
        SQLitePCL.Batteries_V2.Init();
    }

    public SqliteDatabase(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        DatabasePath = Path.GetFullPath(databasePath);

        var directory = Path.GetDirectoryName(DatabasePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            ForeignKeys = true,
        }.ToString();
    }

    public string DatabasePath { get; }

    public SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        LegacyCollation.EnsureRegistered(connection);

        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_keys = ON; PRAGMA busy_timeout = 5000;";
        command.ExecuteNonQuery();

        return connection;
    }

    public void Initialize()
    {
        using var connection = OpenConnection();
        DatabaseMigrator.Migrate(connection);
    }
}
