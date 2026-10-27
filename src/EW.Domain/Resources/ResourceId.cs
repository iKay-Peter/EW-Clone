namespace EW.Domain.Resources;

public readonly record struct ResourceId(Guid Value)
{
    public static ResourceId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}
