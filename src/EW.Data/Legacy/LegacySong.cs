namespace EW.Data.Legacy;

public sealed record LegacySong(
    string LegacyKey,
    long RowId,
    string? Uid,
    string Title,
    string? Author,
    string? Copyright,
    string? Tags,
    string RawJson,
    IReadOnlyList<LegacySongWord> Words);

public sealed record LegacySongWord(
    long RowId,
    int Position,
    string? Rtf,
    string? SlideUids,
    string? SlideLayoutRevisions,
    string? SlideRevisions,
    string RawJson);

public sealed record LegacySongBatch(
    string SongsDatabasePath,
    string SongWordsDatabasePath,
    string SongsSha256,
    string SongWordsSha256,
    IReadOnlyList<LegacySong> Songs);
