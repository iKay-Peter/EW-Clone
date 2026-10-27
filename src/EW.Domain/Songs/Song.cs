namespace EW.Domain.Songs;

public sealed record Song(
    SongId Id,
    string? LegacyId,
    string Title,
    string? Author,
    string? Copyright,
    string? Tags,
    IReadOnlyList<SongSlide> Slides,
    string? LegacyMetadata)
{
    public override string ToString() => Title;
}
