namespace EW.Domain.Profiles;

public sealed record Profile(ProfileId Id, string Name)
{
    public static Profile Create(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new Profile(ProfileId.New(), name.Trim());
    }
}
