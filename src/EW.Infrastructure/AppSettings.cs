namespace EW.Infrastructure;

public sealed class AppSettings
{
    public string ProfileName { get; set; } = "Default";

    public string SharedRoot { get; set; } = string.Empty;
}
