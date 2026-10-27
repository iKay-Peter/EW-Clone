using System.IO;
using System.Xml.Linq;

namespace EW.IntegrationTests;

public sealed class ArchitectureTests
{
    public static TheoryData<string, string[]> ExpectedReferences => new()
    {
        { "EW.Domain", [] },
        { "EW.Application", ["EW.Domain"] },
        { "EW.Data", ["EW.Domain"] },
        { "EW.Infrastructure", [] },
        { "EW.Rendering", ["EW.Domain"] },
        { "EW.Media", ["EW.Domain"] },
        { "EW.Output", ["EW.Domain", "EW.Rendering"] },
        { "EW.Platform", ["EW.Domain"] },
        {
            "EW.App",
            [
                "EW.Application",
                "EW.Data",
                "EW.Infrastructure",
                "EW.Media",
                "EW.Output",
                "EW.Platform",
                "EW.Rendering",
            ]
        },
    };

    [Theory]
    [MemberData(nameof(ExpectedReferences))]
    public void ProjectReferencesMatchArchitecture(string projectName, string[] expected)
    {
        var projectPath = Path.Combine(GetRepositoryRoot(), "src", projectName, $"{projectName}.csproj");
        Assert.True(File.Exists(projectPath), $"Missing project: {projectPath}");

        var document = XDocument.Load(projectPath);
        var actual = document
            .Descendants()
            .Where(element => element.Name.LocalName == "ProjectReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(value => value is not null)
            .Select(value => Path.GetFileNameWithoutExtension(value!.Replace('\\', Path.DirectorySeparatorChar)))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expected.Order(StringComparer.Ordinal).ToArray(), actual);
    }

    private static string GetRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "EasyWorshipClone.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root was not found.");
    }
}
