namespace EW.Domain.Scheduling;

public readonly record struct ServiceIntervalId(Guid Value)
{
    public static ServiceIntervalId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}
