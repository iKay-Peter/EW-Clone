namespace EW.Domain.Presentations;

public readonly record struct ElementId(Guid Value)
{
    public static ElementId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}
