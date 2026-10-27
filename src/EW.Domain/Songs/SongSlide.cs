namespace EW.Domain.Songs;

public sealed record SongSlide(
    SongSlideId Id,
    int Position,
    string? SectionLabel,
    string ContentRtf,
    string PlainText);
