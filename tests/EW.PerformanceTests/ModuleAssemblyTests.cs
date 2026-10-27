using System.Reflection;

namespace EW.PerformanceTests;

public sealed class ModuleAssemblyTests
{
    [Fact]
    public void ApplicationAssemblyLoads()
    {
        var assembly = Assembly.Load("EW.Application");

        Assert.Equal("EW.Application", assembly.GetName().Name);
    }
}
