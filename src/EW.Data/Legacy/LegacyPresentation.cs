namespace EW.Data.Legacy;

public sealed record LegacyPresentationBatch(
    string SourcePath,
    string SourceSha256,
    IReadOnlyList<LegacyPresentation> Presentations,
    IReadOnlyList<LegacyResource> Resources);

public sealed record LegacyPresentation(
    string LegacyKey,
    long RowId,
    string Title,
    string? Author,
    string? AspectRatio,
    string RawJson,
    IReadOnlyList<LegacySlide> Slides);

public sealed record LegacySlide(
    long RowId,
    int Position,
    string? Notes,
    string RawJson,
    IReadOnlyList<LegacyElement> Elements);

public sealed record LegacyElement(
    long RowId,
    int Position,
    int ElementType,
    int? ElementStyleType,
    double X,
    double Y,
    double Width,
    double Height,
    long? BackgroundResourceId,
    long? ForegroundResourceId,
    long? ShapeResourceId,
    bool InternalMute,
    bool FromMaster,
    string RawJson);

public sealed record LegacyResource(
    string LegacyKey,
    long RowId,
    int ResourceType,
    string? TextRtf,
    string? TextPlain,
    string? WebUrl,
    string? ShapeData,
    string? MediaHash,
    string? ArchiveFileName,
    string? OriginalFileName,
    long? FileSize,
    int? Width,
    int? Height,
    long? Duration,
    int? AlphaMode,
    string RawJson);
