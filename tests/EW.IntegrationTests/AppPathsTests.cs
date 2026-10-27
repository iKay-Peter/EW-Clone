using System.IO;
using EW.Infrastructure;

namespace EW.IntegrationTests;

public sealed class AppPathsTests
{
    [Fact]
    public void EnsureUserDirectoriesCreatesUserRoots()
    {
        var root = CreateTempRoot();
        try
        {
            var paths = new AppPaths(
                Path.Combine(root, "user"),
                Path.Combine(root, "machine"),
                Path.Combine(root, "shared"),
                Path.Combine(root, "install"));

            paths.EnsureUserDirectories();

            Assert.True(Directory.Exists(paths.UserRoot));
            Assert.True(Directory.Exists(paths.UserLogs));
            Assert.True(Directory.Exists(paths.UserCache));
            Assert.Equal(Path.Combine(paths.UserRoot, "settings.json"), paths.UserSettingsFile);
            Assert.False(Directory.Exists(paths.MachineRoot));
            Assert.False(Directory.Exists(paths.SharedRoot));
        }
        finally
        {
            DeleteIfExists(root);
        }
    }

    [Fact]
    public void ConstructorNormalizesAllRoots()
    {
        var root = CreateTempRoot();
        try
        {
            var paths = new AppPaths(
                Path.Combine(root, "user", "..", "user"),
                Path.Combine(root, "machine"),
                Path.Combine(root, "shared"),
                Path.Combine(root, "install"));

            Assert.Equal(Path.GetFullPath(Path.Combine(root, "user")), paths.UserRoot);
            Assert.Equal(Path.GetFullPath(Path.Combine(root, "machine")), paths.MachineRoot);
            Assert.Equal(Path.GetFullPath(Path.Combine(root, "shared")), paths.SharedRoot);
            Assert.Equal(Path.GetFullPath(Path.Combine(root, "install")), paths.InstallRoot);
        }
        finally
        {
            DeleteIfExists(root);
        }
    }

    private static string CreateTempRoot()
        => Path.Combine(Path.GetTempPath(), "EWCloneTests", Guid.NewGuid().ToString("N"));

    private static void DeleteIfExists(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
