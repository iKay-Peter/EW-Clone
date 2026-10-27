using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;

namespace EW.Data.Legacy;

public static class LegacyArchiveMediaExtractor
{
    private const long MaximumEntryBytes = 2L * 1024 * 1024 * 1024;
    private const long MaximumTotalBytes = 10L * 1024 * 1024 * 1024;

    public static LegacyArchiveMediaManifest Extract(
        string archivePath,
        string destinationDirectory)
    {
        var fullArchivePath = Path.GetFullPath(archivePath);
        if (!File.Exists(fullArchivePath))
        {
            throw new FileNotFoundException("Legacy archive not found.", fullArchivePath);
        }

        var fullDestination = Path.GetFullPath(destinationDirectory);
        var archiveHash = LegacyDatabaseReader.FileSha256(fullArchivePath);

        using var stream = File.OpenRead(fullArchivePath);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

        var plan = BuildPlan(archive, fullDestination);
        EnsureSufficientSpace(fullDestination, plan.Sum(item => item.Entry.Length));
        Directory.CreateDirectory(fullDestination);

        var entries = new List<ExtractedArchiveMedia>(plan.Count);
        foreach (var item in plan)
        {
            var directory = Path.GetDirectoryName(item.OutputPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using (var input = item.Entry.Open())
            using (var output = new FileStream(
                item.OutputPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None))
            {
                input.CopyTo(output);
            }

            entries.Add(new ExtractedArchiveMedia(
                item.Entry.FullName,
                item.OutputPath,
                item.Entry.Length,
                FileSha256(item.OutputPath)));
        }

        return new LegacyArchiveMediaManifest(
            fullArchivePath,
            archiveHash,
            fullDestination,
            entries);
    }

    private static List<MediaPlan> BuildPlan(ZipArchive archive, string destinationDirectory)
    {
        var plan = new List<MediaPlan>();
        var outputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var totalBytes = 0L;

        foreach (var entry in archive.Entries)
        {
            var normalized = entry.FullName.Replace('\\', '/');
            if (normalized.EndsWith('/'))
            {
                continue;
            }

            if (!normalized.StartsWith("media/", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var relative = normalized["media/".Length..];
            ValidateRelativePath(relative);

            var outputPath = Path.GetFullPath(
                Path.Combine(
                    destinationDirectory,
                    relative.Replace('/', Path.DirectorySeparatorChar)));

            var destinationPrefix = destinationDirectory.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!outputPath.StartsWith(destinationPrefix, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"Archive entry escapes the destination: {entry.FullName}");
            }

            if (!outputs.Add(outputPath))
            {
                throw new InvalidDataException($"Archive contains a duplicate media path: {entry.FullName}");
            }

            if (entry.Length > MaximumEntryBytes)
            {
                throw new InvalidDataException(
                    $"Archive media entry exceeds the {MaximumEntryBytes} byte safety limit: {entry.FullName}");
            }

            totalBytes += entry.Length;
            if (totalBytes > MaximumTotalBytes)
            {
                throw new InvalidDataException(
                    $"Archive media exceeds the {MaximumTotalBytes} byte safety limit.");
            }

            plan.Add(new MediaPlan(entry, outputPath));
        }

        return plan;
    }

    private static void ValidateRelativePath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            throw new InvalidDataException("Archive media entry has an empty path.");
        }

        foreach (var segment in relativePath.Split('/'))
        {
            if (segment is "" or "." or "..")
            {
                throw new InvalidDataException(
                    $"Archive media entry contains an unsafe path segment: {relativePath}");
            }

            if (segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                throw new InvalidDataException(
                    $"Archive media entry contains invalid path characters: {relativePath}");
            }
        }
    }

    private static void EnsureSufficientSpace(string destinationDirectory, long totalBytes)
    {
        var root = Path.GetPathRoot(destinationDirectory);
        if (string.IsNullOrEmpty(root))
        {
            return;
        }

        var available = new DriveInfo(root).AvailableFreeSpace;
        var required = totalBytes + Math.Max(totalBytes / 10, 1024 * 1024);
        if (available < required)
        {
            throw new IOException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Insufficient free space for archive media. Required: {required} bytes; available: {available} bytes."));
        }
    }

    public static string FileSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private sealed record MediaPlan(ZipArchiveEntry Entry, string OutputPath);
}

public sealed record LegacyArchiveMediaManifest(
    string ArchivePath,
    string ArchiveSha256,
    string DestinationDirectory,
    IReadOnlyList<ExtractedArchiveMedia> Entries);

public sealed record ExtractedArchiveMedia(
    string ArchiveEntry,
    string OutputPath,
    long SizeBytes,
    string Sha256);
