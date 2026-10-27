namespace EW.Domain.Presentations;

public readonly record struct SlideId(Guid Value)
{
    public static SlideId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}
