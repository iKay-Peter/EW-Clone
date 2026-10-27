namespace EW.Domain.Presentations;

public sealed record Presentation(
    PresentationId Id,
    string? LegacyId,
    string Title,
    string? Author,
    double? AspectRatio,
    string? LegacyMetadata,
    IReadOnlyList<Slide> Slides)
{
    public override string ToString() => Title;
}
