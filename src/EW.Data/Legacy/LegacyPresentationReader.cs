using System.Globalization;
using System.Text;

namespace EW.Data.Legacy;

public static class LegacyPresentationReader
{
    public static LegacyPresentationBatch Read(string databasePath)
    {
        var sourcePath = Path.GetFullPath(databasePath);

        var presentationRows = LegacyDatabaseReader.ReadTable(sourcePath, "presentation");
        var slideRows = LegacyDatabaseReader.ReadTable(sourcePath, "slide");
        var elementRows = LegacyDatabaseReader.ReadTable(sourcePath, "element");
        var resourceRows = LegacyDatabaseReader.ReadTable(sourcePath, "resource");
        var fileRows = LegacyDatabaseReader.ReadTable(sourcePath, "file")
            .ToDictionary(row => row.RowId);

        var resourceSubtypes = ReadResourceSubtypes(sourcePath);
        var elementsBySlide = elementRows
            .Where(row => LegacyDatabaseReader.TryGetInt64(row.Values, "slide_id", out _))
            .GroupBy(row =>
            {
                LegacyDatabaseReader.TryGetInt64(row.Values, "slide_id", out var slideId);
                return slideId;
            })
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(row => OrderIndex(row)).ThenBy(row => row.RowId).ToList());

        var slidesByPresentation = slideRows
            .Where(row => LegacyDatabaseReader.TryGetInt64(row.Values, "presentation_id", out _))
            .GroupBy(row =>
            {
                LegacyDatabaseReader.TryGetInt64(row.Values, "presentation_id", out var presentationId);
                return presentationId;
            })
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(row => OrderIndex(row)).ThenBy(row => row.RowId).ToList());

        var resources = resourceRows
            .OrderBy(row => row.RowId)
            .Select(row => MapResource(row, resourceSubtypes, fileRows))
            .ToArray();

        var presentations = new List<LegacyPresentation>(presentationRows.Count);
        foreach (var row in presentationRows.OrderBy(row => row.RowId))
        {
            var slides = slidesByPresentation.TryGetValue(row.RowId, out var slideGroup)
                ? slideGroup.Select((slide, position) => MapSlide(slide, position, elementsBySlide)).ToArray()
                : [];

            var uid = LegacyDatabaseReader.GetString(row.Values, "presentation_uid");
            var globalUid = LegacyDatabaseReader.GetString(row.Values, "presentation_global_uid");
            var legacyKey = !string.IsNullOrWhiteSpace(uid)
                ? uid
                : !string.IsNullOrWhiteSpace(globalUid)
                    ? globalUid
                    : string.Create(CultureInfo.InvariantCulture, $"rowid:{row.RowId}");

            presentations.Add(new LegacyPresentation(
                legacyKey,
                row.RowId,
                LegacyDatabaseReader.GetString(row.Values, "title") ?? string.Empty,
                LegacyDatabaseReader.GetString(row.Values, "author"),
                LegacyDatabaseReader.GetString(row.Values, "aspect_ratio"),
                row.RawJson,
                slides));
        }

        return new LegacyPresentationBatch(
            sourcePath,
            LegacyDatabaseReader.FileSha256(sourcePath),
            presentations,
            resources);
    }

    public static string FileSha256(string path) => LegacyDatabaseReader.FileSha256(path);

    private static LegacySlide MapSlide(
        LegacyTableRow row,
        int position,
        Dictionary<long, List<LegacyTableRow>> elementsBySlide)
    {
        var elements = elementsBySlide.TryGetValue(row.RowId, out var elementGroup)
            ? elementGroup.Select((element, elementPosition) => MapElement(element, elementPosition)).ToArray()
            : [];

        return new LegacySlide(
            row.RowId,
            position,
            Notes: null,
            row.RawJson,
            elements);
    }

    private static LegacyElement MapElement(LegacyTableRow row, int position) =>
        new(
            row.RowId,
            position,
            (int)(LegacyDatabaseReader.GetInt64(row.Values, "element_type") ?? 0),
            (int?)LegacyDatabaseReader.GetInt64(row.Values, "element_style_type"),
            LegacyDatabaseReader.GetDouble(row.Values, "x") ?? 0,
            LegacyDatabaseReader.GetDouble(row.Values, "y") ?? 0,
            LegacyDatabaseReader.GetDouble(row.Values, "width") ?? 0,
            LegacyDatabaseReader.GetDouble(row.Values, "height") ?? 0,
            LegacyDatabaseReader.GetInt64(row.Values, "background_resource_id"),
            LegacyDatabaseReader.GetInt64(row.Values, "foreground_resource_id"),
            LegacyDatabaseReader.GetInt64(row.Values, "shape_resource_id"),
            LegacyDatabaseReader.GetBoolean(row.Values, "internal_mute"),
            LegacyDatabaseReader.GetBoolean(row.Values, "from_master"),
            row.RawJson);

    private static LegacyResource MapResource(
        LegacyTableRow row,
        Dictionary<long, IReadOnlyDictionary<string, LegacyTableRow>> subtypedRows,
        IReadOnlyDictionary<long, LegacyTableRow> fileRows)
    {
        var subtypes = subtypedRows.TryGetValue(row.RowId, out var value)
            ? value
            : new Dictionary<string, LegacyTableRow>(StringComparer.Ordinal);

        subtypes.TryGetValue("resource_text", out var text);
        subtypes.TryGetValue("resource_web", out var web);
        subtypes.TryGetValue("resource_shape", out var shape);

        var media = FirstMediaSubtype(subtypes);
        var fileRow = FindMediaFile(media, fileRows);
        var textRtf = text is null ? null : LegacyDatabaseReader.GetString(text.Values, "rtf");
        var uid = LegacyDatabaseReader.GetString(row.Values, "resource_uid");
        var legacyKey = string.IsNullOrWhiteSpace(uid)
            ? string.Create(CultureInfo.InvariantCulture, $"rowid:{row.RowId}")
            : uid;

        return new LegacyResource(
            legacyKey,
            row.RowId,
            (int)(LegacyDatabaseReader.GetInt64(row.Values, "resource_type") ?? 0),
            textRtf,
            textRtf is null ? null : RtfPlainText.ToPlainText(textRtf),
            web is null ? null : LegacyDatabaseReader.GetString(web.Values, "url"),
            shape is null ? null : LegacyDatabaseReader.GetString(shape.Values, "shape_data"),
            fileRow is null ? null : LegacyDatabaseReader.GetString(fileRow.Values, "file_hash"),
            fileRow is null ? null : ResolveArchiveFileName(fileRow),
            media is null ? null : LegacyDatabaseReader.GetString(media.Values, "original_filename"),
            media is null ? null : LegacyDatabaseReader.GetInt64(media.Values, "filesize"),
            GetInt32(media, "width"),
            GetInt32(media, "height"),
            GetInt64(media, "duration"),
            GetInt32(media, "alpha_channel_mode"),
            CombineRawJson(row.RawJson, subtypes, fileRow));
    }

    private static LegacyTableRow? FirstMediaSubtype(
        IReadOnlyDictionary<string, LegacyTableRow> subtypes)
    {
        foreach (var tableName in MediaSubtypeTables)
        {
            if (subtypes.TryGetValue(tableName, out var row))
            {
                return row;
            }
        }

        return null;
    }

    private static LegacyTableRow? FindMediaFile(
        LegacyTableRow? media,
        IReadOnlyDictionary<long, LegacyTableRow> fileRows)
    {
        if (media is null ||
            !LegacyDatabaseReader.TryGetInt64(media.Values, "file_id", out var fileId))
        {
            return null;
        }

        return fileRows.TryGetValue(fileId, out var row) ? row : null;
    }

    private static string? ResolveArchiveFileName(LegacyTableRow fileRow)
    {
        var filename = LegacyDatabaseReader.GetString(fileRow.Values, "filename");
        return string.IsNullOrWhiteSpace(filename)
            ? LegacyDatabaseReader.GetString(fileRow.Values, "shared_filename")
            : filename;
    }

    private static int? GetInt32(LegacyTableRow? row, string key)
    {
        var value = GetInt64(row, key);
        return value is null ? null : checked((int)value.Value);
    }

    private static long? GetInt64(LegacyTableRow? row, string key) =>
        row is null ? null : LegacyDatabaseReader.GetInt64(row.Values, key);

    private static Dictionary<long, IReadOnlyDictionary<string, LegacyTableRow>> ReadResourceSubtypes(
        string databasePath)
    {
        var result = new Dictionary<long, Dictionary<string, LegacyTableRow>>();
        foreach (var tableName in ResourceSubtypeTables)
        {
            foreach (var row in LegacyDatabaseReader.ReadTable(databasePath, tableName))
            {
                if (!LegacyDatabaseReader.TryGetInt64(row.Values, "resource_id", out var resourceId))
                {
                    continue;
                }

                if (!result.TryGetValue(resourceId, out var tables))
                {
                    tables = new Dictionary<string, LegacyTableRow>(StringComparer.Ordinal);
                    result[resourceId] = tables;
                }

                tables[tableName] = row;
            }
        }

        return result.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyDictionary<string, LegacyTableRow>)pair.Value.ToDictionary(
                inner => inner.Key,
                inner => inner.Value,
                StringComparer.Ordinal));
    }

    private static string CombineRawJson(
        string resourceRawJson,
        IReadOnlyDictionary<string, LegacyTableRow> subtypes,
        LegacyTableRow? fileRow)
    {
        var builder = new StringBuilder(resourceRawJson.Length + 256);
        builder.Append("{\"resource\":").Append(resourceRawJson);
        foreach (var (tableName, row) in subtypes.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            builder.Append(",\"")
                .Append(tableName)
                .Append("\":")
                .Append(row.RawJson);
        }

        if (fileRow is not null)
        {
            builder.Append(",\"file\":").Append(fileRow.RawJson);
        }

        return builder.Append('}').ToString();
    }

    private static long OrderIndex(LegacyTableRow row) =>
        LegacyDatabaseReader.GetInt64(row.Values, "order_index") ?? long.MaxValue;

    private static readonly string[] ResourceSubtypeTables =
    [
        "resource_audio",
        "resource_color",
        "resource_feed",
        "resource_gradient",
        "resource_image",
        "resource_powerpoint",
        "resource_shape",
        "resource_text",
        "resource_video",
        "resource_web",
    ];

    private static readonly string[] MediaSubtypeTables =
    [
        "resource_image",
        "resource_video",
        "resource_audio",
        "resource_powerpoint",
    ];
}
