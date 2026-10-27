namespace EW.Infrastructure;

public sealed class AppPaths : IAppPaths
{
    public AppPaths()
        : this(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EWClone"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "EWClone"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments), "EWClone"),
            AppContext.BaseDirectory)
    {
    }

    public AppPaths(string userRoot, string machineRoot, string sharedRoot, string installRoot)
    {
        UserRoot = Normalize(userRoot);
        MachineRoot = Normalize(machineRoot);
        SharedRoot = Normalize(sharedRoot);
        InstallRoot = Normalize(installRoot);
        UserLogs = Path.Combine(UserRoot, "Logs");
        UserCache = Path.Combine(UserRoot, "Cache");
        UserSettingsFile = Path.Combine(UserRoot, "settings.json");
    }

    public string InstallRoot { get; }

    public string MachineRoot { get; }

    public string SharedRoot { get; }

    public string UserRoot { get; }

    public string UserLogs { get; }

    public string UserCache { get; }

    public string UserSettingsFile { get; }

    public void EnsureUserDirectories()
    {
        Directory.CreateDirectory(UserRoot);
        Directory.CreateDirectory(UserLogs);
        Directory.CreateDirectory(UserCache);
    }

    private static string Normalize(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Path.GetFullPath(path);
    }
}
