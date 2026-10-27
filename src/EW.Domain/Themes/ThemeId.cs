namespace EW.Domain.Themes;

public readonly record struct ThemeId(Guid Value)
{
    public static ThemeId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}
