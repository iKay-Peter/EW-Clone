using EW.Domain.Resources;

namespace EW.Domain.Presentations;

public sealed record Element(
    ElementId Id,
    int Position,
    int ElementType,
    int? ElementStyleType,
    double X,
    double Y,
    double Width,
    double Height,
    string? StyleJson,
    ResourceId? ResourceId,
    ResourceId? BackgroundResourceId,
    ResourceId? ForegroundResourceId,
    ResourceId? ShapeResourceId,
    bool InternalMute,
    bool FromMaster,
    string? LegacyMetadata);
