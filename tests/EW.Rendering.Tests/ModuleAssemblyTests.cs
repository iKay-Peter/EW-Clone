using System.Reflection;

namespace EW.Rendering.Tests;

public sealed class ModuleAssemblyTests
{
    [Fact]
    public void RenderingAssemblyLoads()
    {
        var assembly = Assembly.Load("EW.Rendering");

        Assert.Equal("EW.Rendering", assembly.GetName().Name);
    }
}
