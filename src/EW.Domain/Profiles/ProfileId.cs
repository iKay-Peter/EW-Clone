namespace EW.Domain.Profiles;

public readonly record struct ProfileId(Guid Value)
{
    public static ProfileId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}
