using System.Reflection;

namespace EW.Import.Tests;

public sealed class ModuleAssemblyTests
{
    [Fact]
    public void ImportDependenciesLoad()
    {
        var data = Assembly.Load("EW.Data");
        var application = Assembly.Load("EW.Application");

        Assert.Equal("EW.Data", data.GetName().Name);
        Assert.Equal("EW.Application", application.GetName().Name);
    }
}
