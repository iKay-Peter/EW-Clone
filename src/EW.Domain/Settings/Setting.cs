namespace EW.Domain.Settings;

public sealed record Setting(
    string Key,
    string? Value,
    string? ValueType,
    string? LegacyMetadata)
{
    public override string ToString() => Key;
}
