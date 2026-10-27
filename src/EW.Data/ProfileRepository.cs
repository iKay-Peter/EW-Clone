using System.Globalization;
using EW.Data.Migrations;
using EW.Domain.Profiles;
using Microsoft.Data.Sqlite;

namespace EW.Data;

public sealed class ProfileRepository(SqliteDatabase database)
{
    private readonly SqliteDatabase _database = database ?? throw new ArgumentNullException(nameof(database));

    public void Add(Profile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Profile (Id, Name, CreatedUtc, SchemaVersion)
            VALUES ($id, $name, $createdUtc, $schemaVersion);
            """;
        command.Parameters.AddWithValue("$id", profile.Id.ToString());
        command.Parameters.AddWithValue("$name", profile.Name);
        command.Parameters.AddWithValue(
            "$createdUtc",
            DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$schemaVersion", DatabaseMigrator.CurrentVersion);
        command.ExecuteNonQuery();
    }

    public Profile? Get(ProfileId id)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Name FROM Profile WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", id.ToString());

        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadProfile(reader) : null;
    }

    public IReadOnlyList<Profile> List()
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Name FROM Profile ORDER BY Name COLLATE NOCASE;";

        using var reader = command.ExecuteReader();
        var profiles = new List<Profile>();
        while (reader.Read())
        {
            profiles.Add(ReadProfile(reader));
        }

        return profiles;
    }

    private static Profile ReadProfile(SqliteDataReader reader)
    {
        var id = Guid.Parse(reader.GetString(0));
        return new Profile(new ProfileId(id), reader.GetString(1));
    }
}
