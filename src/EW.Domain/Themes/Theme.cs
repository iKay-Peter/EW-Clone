namespace EW.Domain.Themes;

public sealed record Theme(
    ThemeId Id,
    string? LegacyId,
    string Name,
    string? SourcePath,
    string? StyleJson,
    string? LegacyMetadata)
{
    public override string ToString() => Name;
}
