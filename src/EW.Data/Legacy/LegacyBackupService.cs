using System.Globalization;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;

namespace EW.Data.Legacy;

public static class LegacyBackupService
{
    private static readonly byte[] SqliteHeader = "SQLite format 3\0"u8.ToArray();

    static LegacyBackupService()
    {
        SQLitePCL.Batteries_V2.Init();
    }

    public static BackupManifest CreateBackup(
        IEnumerable<string> sourcePaths,
        string backupDirectory)
    {
        ArgumentNullException.ThrowIfNull(sourcePaths);
        ArgumentException.ThrowIfNullOrWhiteSpace(backupDirectory);

        var sources = sourcePaths
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        EnsureSufficientSpace(sources, backupDirectory);
        Directory.CreateDirectory(backupDirectory);

        var entries = new List<BackupEntry>(sources.Length);
        foreach (var source in sources)
        {
            entries.Add(BackupFile(source, backupDirectory));
        }

        return new BackupManifest(
            Path.GetFullPath(backupDirectory),
            DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            entries);
    }

    private static BackupEntry BackupFile(string sourcePath, string backupDirectory)
    {
        if (!File.Exists(sourcePath))
        {
            return new BackupEntry(
                sourcePath,
                string.Empty,
                string.Empty,
                string.Empty,
                0,
                "missing",
                "Source file not found.");
        }

        var destinationPath = CreateDestinationPath(sourcePath, backupDirectory);
        try
        {
            var sourceHash = FileSha256(sourcePath);
            File.Copy(sourcePath, destinationPath, overwrite: false);
            var backupHash = FileSha256(destinationPath);
            var size = new FileInfo(destinationPath).Length;
            var error = sourceHash == backupHash ? null : "Backup hash does not match source hash.";

            return new BackupEntry(
                sourcePath,
                destinationPath,
                sourceHash,
                backupHash,
                size,
                CheckIntegrity(destinationPath),
                error);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new BackupEntry(
                sourcePath,
                destinationPath,
                string.Empty,
                string.Empty,
                0,
                "not-checked",
                exception.Message);
        }
    }

    private static string CheckIntegrity(string path)
    {
        if (!HasSqliteHeader(path))
        {
            return "not-database";
        }

        try
        {
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = path,
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false,
            }.ToString();

            using var connection = new SqliteConnection(connectionString);
            connection.Open();
            LegacyCollation.EnsureRegistered(connection);

            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA integrity_check;";
            return Convert.ToString(command.ExecuteScalar(), CultureInfo.InvariantCulture) ?? "unknown";
        }
        catch (SqliteException exception)
        {
            return $"error: {exception.Message}";
        }
    }

    private static bool HasSqliteHeader(string path)
    {
        Span<byte> header = stackalloc byte[SqliteHeader.Length];
        using var stream = File.OpenRead(path);
        return stream.Read(header) == header.Length && header.SequenceEqual(SqliteHeader);
    }

    private static void EnsureSufficientSpace(
        IReadOnlyCollection<string> sources,
        string backupDirectory)
    {
        var totalBytes = sources
            .Where(File.Exists)
            .Sum(path => new FileInfo(path).Length);
        if (totalBytes == 0)
        {
            return;
        }

        var root = Path.GetPathRoot(Path.GetFullPath(backupDirectory));
        if (string.IsNullOrEmpty(root))
        {
            return;
        }

        var available = new DriveInfo(root).AvailableFreeSpace;
        var required = totalBytes + Math.Max(totalBytes / 10, 1024 * 1024);
        if (available < required)
        {
            throw new IOException(
                $"Insufficient free space for legacy backup. Required: {required} bytes; available: {available} bytes.");
        }
    }

    private static string CreateDestinationPath(string sourcePath, string backupDirectory)
    {
        var name = Path.GetFileName(sourcePath);
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(invalid, '_');
        }

        var stamp = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture);
        var destination = Path.Combine(backupDirectory, $"{stamp}-{name}");
        if (!File.Exists(destination))
        {
            return destination;
        }

        return Path.Combine(
            backupDirectory,
            $"{stamp}-{Guid.NewGuid():N}-{name}");
    }

    private static string FileSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }
}

public sealed record BackupManifest(
    string BackupDirectory,
    string CreatedUtc,
    IReadOnlyList<BackupEntry> Entries)
{
    public bool Succeeded => Entries.All(entry => entry.Error is null);
}

public sealed record BackupEntry(
    string SourcePath,
    string BackupPath,
    string SourceSha256,
    string BackupSha256,
    long SizeBytes,
    string Integrity,
    string? Error);
