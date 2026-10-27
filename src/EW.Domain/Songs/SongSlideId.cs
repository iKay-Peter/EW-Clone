namespace EW.Domain.Songs;

public readonly record struct SongSlideId(Guid Value)
{
    public static SongSlideId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}
