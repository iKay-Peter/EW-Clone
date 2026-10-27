using EW.Domain.Profiles;
using Microsoft.Data.Sqlite;

namespace EW.Data.Tests;

public sealed class ProfileRepositoryTests : IDisposable
{
    private readonly string _root;
    private readonly SqliteDatabase _database;
    private readonly ProfileRepository _repository;

    public ProfileRepositoryTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "EWCloneTests", Guid.NewGuid().ToString("N"));
        _database = new SqliteDatabase(Path.Combine(_root, "clone.db"));
        _database.Initialize();
        _repository = new ProfileRepository(_database);
    }

    [Fact]
    public void AddThenGetReturnsProfile()
    {
        var profile = Profile.Create("Test Profile");

        _repository.Add(profile);
        var loaded = _repository.Get(profile.Id);

        Assert.NotNull(loaded);
        Assert.Equal(profile.Id, loaded.Id);
        Assert.Equal("Test Profile", loaded.Name);
    }

    [Fact]
    public void ListReturnsProfilesByName()
    {
        _repository.Add(Profile.Create("Zulu"));
        _repository.Add(Profile.Create("Alpha"));

        var profiles = _repository.List();

        Assert.Equal(["Alpha", "Zulu"], profiles.Select(profile => profile.Name));
    }

    [Fact]
    public void AddDuplicateIdThrows()
    {
        var profile = Profile.Create("Duplicate");
        _repository.Add(profile);

        Assert.Throws<SqliteException>(() => _repository.Add(profile));
    }

    [Fact]
    public void AddStoresCurrentSchemaVersion()
    {
        var profile = Profile.Create("Versioned");
        _repository.Add(profile);

        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT SchemaVersion FROM Profile WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", profile.Id.ToString());

        Assert.Equal(
            4L,
            Convert.ToInt64(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture));
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
