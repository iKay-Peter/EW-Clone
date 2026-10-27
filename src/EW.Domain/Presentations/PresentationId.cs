namespace EW.Domain.Presentations;

public readonly record struct PresentationId(Guid Value)
{
    public static PresentationId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}
