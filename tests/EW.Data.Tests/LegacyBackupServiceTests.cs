using EW.Data.Legacy;

namespace EW.Data.Tests;

public sealed class LegacyBackupServiceTests : IDisposable
{
    private readonly string _root;
    private readonly LegacySongFixture _fixture = new();

    public LegacyBackupServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "EWCloneTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public void CreateBackupCopiesAndVerifiesSqliteDatabasesWithoutChangingSources()
    {
        var songsHashBefore = LegacySongReader.FileSha256(_fixture.SongsDatabasePath);
        var wordsHashBefore = LegacySongReader.FileSha256(_fixture.SongWordsDatabasePath);
        var textPath = Path.Combine(_root, "notes.txt");
        File.WriteAllText(textPath, "synthetic");

        var backupDirectory = Path.Combine(_root, "backup");
        var manifest = LegacyBackupService.CreateBackup(
            [_fixture.SongsDatabasePath, _fixture.SongWordsDatabasePath, textPath],
            backupDirectory);

        Assert.True(manifest.Succeeded);
        Assert.Equal(3, manifest.Entries.Count);
        Assert.All(manifest.Entries, entry =>
        {
            Assert.True(File.Exists(entry.BackupPath));
            Assert.Equal(entry.SourceSha256, entry.BackupSha256);
        });

        Assert.Equal("ok", manifest.Entries[0].Integrity);
        Assert.Equal("ok", manifest.Entries[1].Integrity);
        Assert.Equal("not-database", manifest.Entries[2].Integrity);
        Assert.Equal(songsHashBefore, LegacySongReader.FileSha256(_fixture.SongsDatabasePath));
        Assert.Equal(wordsHashBefore, LegacySongReader.FileSha256(_fixture.SongWordsDatabasePath));
    }

    [Fact]
    public void CreateBackupReportsMissingSourcesWithoutCopying()
    {
        var missing = Path.Combine(_root, "missing.db");
        var manifest = LegacyBackupService.CreateBackup(
            [missing],
            Path.Combine(_root, "backup"));

        Assert.False(manifest.Succeeded);
        var entry = Assert.Single(manifest.Entries);
        Assert.Equal(missing, entry.SourcePath);
        Assert.Equal(string.Empty, entry.BackupPath);
        Assert.Equal("missing", entry.Integrity);
        Assert.NotNull(entry.Error);
    }

    public void Dispose()
    {
        _fixture.Dispose();
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
