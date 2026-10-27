namespace EW.Domain.Scheduling;

public sealed record ServiceInterval(
    ServiceIntervalId Id,
    string? LegacyId,
    string? Name,
    string? StartValue,
    string? EndValue,
    bool IsFixed,
    string? LegacyMetadata)
{
    public override string ToString() => Name ?? StartValue ?? "Service interval";
}
