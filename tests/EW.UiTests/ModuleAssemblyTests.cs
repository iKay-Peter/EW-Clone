namespace EW.UiTests;

public sealed class ModuleAssemblyTests
{
    [Fact]
    public void ApplicationAssemblyLoads()
    {
        var assembly = typeof(EW.App.App).Assembly;

        Assert.Equal("EWClone", assembly.GetName().Name);
    }
}
