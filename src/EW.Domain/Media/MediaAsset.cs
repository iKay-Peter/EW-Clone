namespace EW.Domain.Media;

public sealed record MediaAsset(
    MediaAssetId Id,
    string? LegacyId,
    string Path,
    string? Hash,
    int MediaType,
    int? Width,
    int? Height,
    long? DurationMs,
    long? TrimStartMs,
    long? TrimEndMs,
    string? PosterPath,
    int? AlphaMode,
    int Status,
    string? ErrorCode,
    string? ErrorMessage,
    string? LegacyMetadata)
{
    public override string ToString() => Path;
}
