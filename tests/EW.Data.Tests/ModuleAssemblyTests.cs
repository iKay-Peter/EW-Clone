using System.Reflection;

namespace EW.Data.Tests;

public sealed class ModuleAssemblyTests
{
    [Fact]
    public void DataAssemblyLoads()
    {
        var assembly = Assembly.Load("EW.Data");

        Assert.Equal("EW.Data", assembly.GetName().Name);
    }
}
