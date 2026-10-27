using EW.Domain.Media;

namespace EW.Domain.Resources;

public sealed record Resource(
    ResourceId Id,
    string? LegacyId,
    int ResourceType,
    string? TextRtf,
    string? TextPlain,
    string? WebUrl,
    string? ShapeData,
    MediaAssetId? MediaAssetId,
    string? LegacyMetadata);
