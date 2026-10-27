namespace EW.Infrastructure;

public interface IAppPaths
{
    string InstallRoot { get; }

    string MachineRoot { get; }

    string SharedRoot { get; }

    string UserRoot { get; }

    string UserLogs { get; }

    string UserCache { get; }

    string UserSettingsFile { get; }

    void EnsureUserDirectories();
}
