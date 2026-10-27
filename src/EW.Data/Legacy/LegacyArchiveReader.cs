using System.IO.Compression;

namespace EW.Data.Legacy;

public static class LegacyArchiveReader
{
    private const long MaximumMainDatabaseBytes = 1L * 1024 * 1024 * 1024;

    public static LegacyArchiveBatch ReadPresentationArchive(string archivePath)
    {
        var fullPath = Path.GetFullPath(archivePath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("Legacy archive not found.", fullPath);
        }

        var archiveHash = LegacyDatabaseReader.FileSha256(fullPath);
        var tempRoot = Path.Combine(Path.GetTempPath(), "EWClone", "LegacyArchives", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        var tempDatabasePath = Path.Combine(tempRoot, "main.db");

        try
        {
            using var stream = File.OpenRead(fullPath);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

            var entries = archive.Entries
                .Select(entry => new LegacyArchiveEntry(entry.FullName, entry.Length, entry.CompressedLength))
                .ToArray();

            var mainEntry = archive.Entries.FirstOrDefault(entry =>
                entry.FullName.Replace('\\', '/').Equals("main.db", StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidDataException("Archive does not contain main.db.");

            if (mainEntry.Length > MaximumMainDatabaseBytes)
            {
                throw new InvalidDataException(
                    $"Archive main.db exceeds the {MaximumMainDatabaseBytes} byte safety limit.");
            }

            using (var input = mainEntry.Open())
            using (var output = new FileStream(
                tempDatabasePath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None))
            {
                input.CopyTo(output);
            }

            var graph = LegacyPresentationReader.Read(tempDatabasePath);
            return new LegacyArchiveBatch(
                fullPath,
                archiveHash,
                entries,
                graph with
                {
                    SourcePath = fullPath,
                    SourceSha256 = archiveHash,
                });
        }
        finally
        {
            if (File.Exists(tempDatabasePath))
            {
                File.Delete(tempDatabasePath);
            }

            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }
}

public sealed record LegacyArchiveBatch(
    string ArchivePath,
    string ArchiveSha256,
    IReadOnlyList<LegacyArchiveEntry> Entries,
    LegacyPresentationBatch PresentationGraph);

public sealed record LegacyArchiveEntry(string FullName, long Length, long CompressedLength);
