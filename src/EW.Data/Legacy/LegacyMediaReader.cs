using System.Globalization;

namespace EW.Data.Legacy;

public static class LegacyMediaReader
{
    public static LegacyMediaBatch Read(string mediaDatabasePath)
    {
        var sourcePath = Path.GetFullPath(mediaDatabasePath);
        var rows = LegacyDatabaseReader.ReadTable(sourcePath, "media");
        var assets = new List<LegacyMediaAsset>(rows.Count);

        foreach (var row in rows.OrderBy(row => row.RowId))
        {
            var uid = LegacyDatabaseReader.GetString(row.Values, "media_uid");
            var legacyKey = string.IsNullOrWhiteSpace(uid)
                ? string.Create(CultureInfo.InvariantCulture, $"rowid:{row.RowId}")
                : uid;

            var path = LegacyDatabaseReader.GetString(row.Values, "path");
            var filename = LegacyDatabaseReader.GetString(row.Values, "filename");

            assets.Add(new LegacyMediaAsset(
                legacyKey,
                row.RowId,
                uid,
                LegacyDatabaseReader.GetString(row.Values, "file_hash"),
                ResolvePath(path, filename),
                LegacyDatabaseReader.GetInt64(row.Values, "media_type") is { } mediaType
                    ? checked((int)mediaType)
                    : 0,
                LegacyDatabaseReader.GetInt64(row.Values, "width") is { } width
                    ? checked((int)width)
                    : null,
                LegacyDatabaseReader.GetInt64(row.Values, "height") is { } height
                    ? checked((int)height)
                    : null,
                LegacyDatabaseReader.GetInt64(row.Values, "duration"),
                LegacyDatabaseReader.GetInt64(row.Values, "start_pos"),
                LegacyDatabaseReader.GetInt64(row.Values, "end_pos"),
                LegacyDatabaseReader.GetInt64(row.Values, "alpha_channel_mode") is { } alphaMode
                    ? checked((int)alphaMode)
                    : null,
                LegacyDatabaseReader.GetBoolean(row.Values, "ready"),
                LegacyDatabaseReader.GetInt64(row.Values, "error_no"),
                LegacyDatabaseReader.GetString(row.Values, "error_msg"),
                row.RawJson));
        }

        return new LegacyMediaBatch(
            sourcePath,
            LegacyDatabaseReader.FileSha256(sourcePath),
            assets);
    }

    public static string FileSha256(string path) => LegacyDatabaseReader.FileSha256(path);

    private static string ResolvePath(string? path, string? filename)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return filename ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(filename))
        {
            return path;
        }

        return Path.GetFileName(path).Equals(filename, StringComparison.OrdinalIgnoreCase)
            ? path
            : Path.Combine(path, filename);
    }
}

public sealed record LegacyMediaBatch(
    string SourcePath,
    string SourceSha256,
    IReadOnlyList<LegacyMediaAsset> Assets);

public sealed record LegacyMediaAsset(
    string LegacyKey,
    long RowId,
    string? Uid,
    string? Hash,
    string Path,
    int MediaType,
    int? Width,
    int? Height,
    long? Duration,
    long? StartPosition,
    long? EndPosition,
    int? AlphaMode,
    bool Ready,
    long? ErrorNumber,
    string? ErrorMessage,
    string RawJson);
