using System.Globalization;

namespace EW.Data.Legacy;

public static class LegacySongReader
{
    public static LegacySongBatch Read(string songsDatabasePath, string songWordsDatabasePath)
    {
        var songsPath = Path.GetFullPath(songsDatabasePath);
        var wordsPath = Path.GetFullPath(songWordsDatabasePath);

        var songRows = LegacyDatabaseReader.ReadTable(songsPath, "song");
        var wordRows = LegacyDatabaseReader.ReadTable(wordsPath, "word");
        var wordsBySong = wordRows
            .Where(row => LegacyDatabaseReader.TryGetInt64(row.Values, "song_id", out _))
            .GroupBy(row =>
            {
                LegacyDatabaseReader.TryGetInt64(row.Values, "song_id", out var songId);
                return songId;
            })
            .ToDictionary(group => group.Key, group => group.OrderBy(row => row.RowId).ToList());

        var songs = new List<LegacySong>(songRows.Count);
        foreach (var row in songRows.OrderBy(row => row.RowId))
        {
            var uid = LegacyDatabaseReader.GetString(row.Values, "song_uid");
            var legacyKey = string.IsNullOrWhiteSpace(uid)
                ? string.Create(CultureInfo.InvariantCulture, $"rowid:{row.RowId}")
                : uid;

            var words = wordsBySong.TryGetValue(row.RowId, out var rows)
                ? rows.Select((word, position) => new LegacySongWord(
                    word.RowId,
                    position,
                    LegacyDatabaseReader.GetString(word.Values, "words"),
                    LegacyDatabaseReader.GetString(word.Values, "slide_uids"),
                    LegacyDatabaseReader.GetString(word.Values, "slide_layout_revisions"),
                    LegacyDatabaseReader.GetString(word.Values, "slide_revisions"),
                    word.RawJson)).ToArray()
                : [];

            songs.Add(new LegacySong(
                legacyKey,
                row.RowId,
                uid,
                LegacyDatabaseReader.GetString(row.Values, "title") ?? string.Empty,
                LegacyDatabaseReader.GetString(row.Values, "author"),
                LegacyDatabaseReader.GetString(row.Values, "copyright"),
                LegacyDatabaseReader.GetString(row.Values, "tags"),
                row.RawJson,
                words));
        }

        return new LegacySongBatch(
            songsPath,
            wordsPath,
            LegacyDatabaseReader.FileSha256(songsPath),
            LegacyDatabaseReader.FileSha256(wordsPath),
            songs);
    }

    public static string FileSha256(string path) => LegacyDatabaseReader.FileSha256(path);
}
