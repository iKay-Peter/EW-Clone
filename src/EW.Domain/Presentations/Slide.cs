namespace EW.Domain.Presentations;

public sealed record Slide(
    SlideId Id,
    int Position,
    string? Notes,
    string? LegacyMetadata,
    IReadOnlyList<Element> Elements);
