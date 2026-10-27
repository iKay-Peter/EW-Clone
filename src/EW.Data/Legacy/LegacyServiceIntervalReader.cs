using System.Globalization;

namespace EW.Data.Legacy;

public static class LegacyServiceIntervalReader
{
    public static LegacyServiceIntervalBatch Read(string databasePath)
    {
        var sourcePath = Path.GetFullPath(databasePath);
        var rows = LegacyDatabaseReader.ReadTable(sourcePath, "interval");
        var intervals = new List<LegacyServiceInterval>(rows.Count);

        foreach (var row in rows.OrderBy(row => row.RowId))
        {
            intervals.Add(new LegacyServiceInterval(
                string.Create(CultureInfo.InvariantCulture, $"rowid:{row.RowId}"),
                row.RowId,
                LegacyDatabaseReader.GetString(row.Values, "start_time"),
                LegacyDatabaseReader.GetString(row.Values, "end_time"),
                LegacyDatabaseReader.GetBoolean(row.Values, "start_time_fixed"),
                LegacyDatabaseReader.GetBoolean(row.Values, "end_time_fixed"),
                row.RawJson));
        }

        return new LegacyServiceIntervalBatch(
            sourcePath,
            LegacyDatabaseReader.FileSha256(sourcePath),
            intervals);
    }

    public static string FileSha256(string path) => LegacyDatabaseReader.FileSha256(path);
}

public sealed record LegacyServiceIntervalBatch(
    string SourcePath,
    string SourceSha256,
    IReadOnlyList<LegacyServiceInterval> Intervals);

public sealed record LegacyServiceInterval(
    string LegacyKey,
    long RowId,
    string? StartValue,
    string? EndValue,
    bool StartFixed,
    bool EndFixed,
    string RawJson);
