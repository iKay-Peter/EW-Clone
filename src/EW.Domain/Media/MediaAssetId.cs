namespace EW.Domain.Media;

public readonly record struct MediaAssetId(Guid Value)
{
    public static MediaAssetId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}
