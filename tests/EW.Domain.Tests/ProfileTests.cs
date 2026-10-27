using EW.Domain.Profiles;

namespace EW.Domain.Tests;

public sealed class ProfileTests
{
    [Fact]
    public void CreateTrimsName()
    {
        var profile = Profile.Create("  Test Profile  ");

        Assert.Equal("Test Profile", profile.Name);
        Assert.NotEqual(Guid.Empty, profile.Id.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateRejectsBlankName(string name)
    {
        Assert.Throws<ArgumentException>(() => Profile.Create(name));
    }

    [Fact]
    public void NewIdFormatsAsGuid()
    {
        var id = ProfileId.New();

        Assert.Matches("^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$", id.ToString());
    }
}
