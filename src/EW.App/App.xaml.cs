using System.IO;
using System.Windows;
using System.Windows.Threading;
using EW.App.ViewModels;
using EW.Data;
using EW.Data.Legacy;
using EW.Infrastructure;
using EW.Infrastructure.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EW.App;

public partial class App : Application
{
    private IHost? _host;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var paths = new AppPaths();
        paths.EnsureUserDirectories();

        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            Args = e.Args,
            ContentRootPath = AppContext.BaseDirectory,
        });
        builder.Configuration.AddJsonFile(paths.UserSettingsFile, optional: true, reloadOnChange: false);
        builder.Services.Configure<AppSettings>(builder.Configuration.GetSection("App"));
        builder.Services.AddSingleton<IAppPaths>(paths);
        builder.Services.AddSingleton(new SqliteDatabase(
            Path.Combine(paths.UserRoot, "ewclone.db")));
        builder.Services.AddSingleton<SongRepository>();
        builder.Services.AddSingleton<MediaAssetRepository>();
        builder.Services.AddSingleton<PresentationRepository>();
        builder.Services.AddSingleton<ThemeRepository>();
        builder.Services.AddSingleton<ServiceIntervalRepository>();
        builder.Services.AddSingleton<SettingRepository>();
        builder.Services.AddSingleton<SongImportService>();
        builder.Services.AddSingleton<MediaImportService>();
        builder.Services.AddSingleton<PresentationImportService>();
        builder.Services.AddSingleton<ThemeImportService>();
        builder.Services.AddSingleton<ServiceIntervalImportService>();
        builder.Services.AddSingleton<SettingsImportService>();
        builder.Services.AddSingleton<MainViewModel>();
        builder.Services.AddSingleton<MainWindow>();
        builder.Logging.AddProvider(new FileLoggerProvider(paths.UserLogs, "ewclone.log"));

        _host = builder.Build();
        _host.Start();

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

        var logger = _host.Services.GetRequiredService<ILogger<App>>();
        LogStartup(logger, typeof(App).Assembly.GetName().Version);

        _host.Services.GetRequiredService<SqliteDatabase>().Initialize();

        var window = _host.Services.GetRequiredService<MainWindow>();
        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        DispatcherUnhandledException -= OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;

        if (_host is not null)
        {
            _host.StopAsync(TimeSpan.FromSeconds(2)).GetAwaiter().GetResult();
            _host.Dispose();
            _host = null;
        }

        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LogException(e.Exception);
        MessageBox.Show(
            "EW Clone encountered an unexpected error and will close. See the log for details.",
            "EW Clone",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
        Shutdown(1);
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            LogException(exception);
        }
    }

    private void LogException(Exception exception)
    {
        var logger = _host?.Services.GetService<ILogger<App>>();
        if (logger is not null)
        {
            LogUnhandledException(logger, exception);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "EW Clone starting, version {Version}")]
    private static partial void LogStartup(ILogger logger, Version? version);

    [LoggerMessage(Level = LogLevel.Critical, Message = "Unhandled exception")]
    private static partial void LogUnhandledException(ILogger logger, Exception exception);
}
