using EW.Domain.Settings;
using Microsoft.Data.Sqlite;

namespace EW.Data.Tests;

public sealed class SettingRepositoryTests : IDisposable
{
    private readonly string _root;
    private readonly SqliteDatabase _database;
    private readonly SettingRepository _repository;

    public SettingRepositoryTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "EWCloneTests", Guid.NewGuid().ToString("N"));
        _database = new SqliteDatabase(Path.Combine(_root, "clone.db"));
        _database.Initialize();
        _repository = new SettingRepository(_database);
    }

    [Fact]
    public void SetThenGetReturnsSetting()
    {
        _repository.Set(new Setting("Last_Bible", "28", "int", """{"legacy":true}"""));

        var loaded = _repository.Get("Last_Bible");

        Assert.NotNull(loaded);
        Assert.Equal("Last_Bible", loaded.Key);
        Assert.Equal("28", loaded.Value);
        Assert.Equal("int", loaded.ValueType);
        Assert.Equal("""{"legacy":true}""", loaded.LegacyMetadata);
        Assert.Equal("28", _repository.GetValue("Last_Bible"));
    }

    [Fact]
    public void SetReplacesExistingValue()
    {
        _repository.Set(new Setting("InterfaceMode", "1", "int", null));
        _repository.Set(new Setting("InterfaceMode", "2", "int", null));

        var loaded = _repository.Get("InterfaceMode");

        Assert.NotNull(loaded);
        Assert.Equal("2", loaded.Value);
        Assert.Single(_repository.List());
    }

    [Fact]
    public void SetAllowsNullValueAndMetadata()
    {
        _repository.Set(new Setting("Empty", null, null, null));

        var loaded = _repository.Get("Empty");

        Assert.NotNull(loaded);
        Assert.Null(loaded.Value);
        Assert.Null(loaded.ValueType);
        Assert.Null(loaded.LegacyMetadata);
    }

    [Fact]
    public void ListReturnsSettingsByKeyCaseInsensitively()
    {
        _repository.Set(new Setting("zulu", "1", "string", null));
        _repository.Set(new Setting("Alpha", "2", "string", null));
        _repository.Set(new Setting("charlie", "3", "string", null));

        var settings = _repository.List();

        Assert.Equal(["Alpha", "charlie", "zulu"], settings.Select(setting => setting.Key));
    }

    [Fact]
    public void RemoveDeletesSettingAndReportsWhetherItExisted()
    {
        _repository.Set(new Setting("RemoveMe", "value", "string", null));

        Assert.True(_repository.Remove("RemoveMe"));
        Assert.Null(_repository.Get("RemoveMe"));
        Assert.False(_repository.Remove("RemoveMe"));
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
