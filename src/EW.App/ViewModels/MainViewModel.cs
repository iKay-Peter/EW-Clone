using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EW.Data;
using EW.Data.Legacy;
using EW.Domain.Media;
using EW.Domain.Presentations;
using EW.Domain.Scheduling;
using EW.Domain.Settings;
using EW.Domain.Songs;
using EW.Domain.Themes;
using EW.Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Win32;

namespace EW.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly SongRepository _songRepository;
    private readonly MediaAssetRepository _mediaRepository;
    private readonly PresentationRepository _presentationRepository;
    private readonly ThemeRepository _themeRepository;
    private readonly ServiceIntervalRepository _serviceIntervalRepository;
    private readonly SettingRepository _settingRepository;
    private readonly SongImportService _songImportService;
    private readonly MediaImportService _mediaImportService;
    private readonly PresentationImportService _presentationImportService;
    private readonly ThemeImportService _themeImportService;
    private readonly ServiceIntervalImportService _serviceIntervalImportService;
    private readonly SettingsImportService _settingsImportService;
    private readonly ILogger<MainViewModel> _logger;

    private string _activeTitle = "Preview";
    private string _activeText = "Select or import an item.";
    private Song? _liveSong;
    private int _liveSlideIndex;

    public MainViewModel(
        IAppPaths paths,
        SqliteDatabase database,
        IOptions<AppSettings> options,
        SongRepository songRepository,
        MediaAssetRepository mediaRepository,
        PresentationRepository presentationRepository,
        ThemeRepository themeRepository,
        ServiceIntervalRepository serviceIntervalRepository,
        SettingRepository settingRepository,
        SongImportService songImportService,
        MediaImportService mediaImportService,
        PresentationImportService presentationImportService,
        ThemeImportService themeImportService,
        ServiceIntervalImportService serviceIntervalImportService,
        SettingsImportService settingsImportService,
        ILogger<MainViewModel> logger)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(songRepository);
        ArgumentNullException.ThrowIfNull(mediaRepository);
        ArgumentNullException.ThrowIfNull(presentationRepository);
        ArgumentNullException.ThrowIfNull(themeRepository);
        ArgumentNullException.ThrowIfNull(serviceIntervalRepository);
        ArgumentNullException.ThrowIfNull(settingRepository);
        ArgumentNullException.ThrowIfNull(songImportService);
        ArgumentNullException.ThrowIfNull(mediaImportService);
        ArgumentNullException.ThrowIfNull(presentationImportService);
        ArgumentNullException.ThrowIfNull(themeImportService);
        ArgumentNullException.ThrowIfNull(serviceIntervalImportService);
        ArgumentNullException.ThrowIfNull(settingsImportService);
        ArgumentNullException.ThrowIfNull(logger);

        _songRepository = songRepository;
        _mediaRepository = mediaRepository;
        _presentationRepository = presentationRepository;
        _themeRepository = themeRepository;
        _serviceIntervalRepository = serviceIntervalRepository;
        _settingRepository = settingRepository;
        _songImportService = songImportService;
        _mediaImportService = mediaImportService;
        _presentationImportService = presentationImportService;
        _themeImportService = themeImportService;
        _serviceIntervalImportService = serviceIntervalImportService;
        _settingsImportService = settingsImportService;
        _logger = logger;

        ProfileName = string.IsNullOrWhiteSpace(options.Value.ProfileName)
            ? "Default"
            : options.Value.ProfileName.Trim();
        DataRoot = paths.UserRoot;
        DatabasePath = database.DatabasePath;
        Version = $"v{typeof(MainViewModel).Assembly.GetName().Version?.ToString(3) ?? "0.0.0"}";

        RefreshLibrary();
        LogMainViewModelCreated(_logger, ProfileName);
    }

    public string WindowTitle => $"EW Clone - {ProfileName}";

    public string ProfileName { get; }

    public string DataRoot { get; }

    public string DatabasePath { get; }

    public string Version { get; }

    public ObservableCollection<Song> Songs { get; } = [];

    public ObservableCollection<MediaAsset> Media { get; } = [];

    public ObservableCollection<Presentation> Presentations { get; } = [];

    public ObservableCollection<Theme> Themes { get; } = [];

    public ObservableCollection<ServiceInterval> ServiceIntervals { get; } = [];

    public ObservableCollection<Setting> Settings { get; } = [];

    [ObservableProperty]
    private string _status = "Ready";

    [ObservableProperty]
    private string _outputStatus = "No output";

    [ObservableProperty]
    private string _librarySummary = "Library is empty";

    [ObservableProperty]
    private string _previewTitle = "Preview";

    [ObservableProperty]
    private string _previewText = "Select or import an item.";

    [ObservableProperty]
    private string _liveTitle = "Live Output";

    [ObservableProperty]
    private string _liveText = "No output";

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private Song? _selectedSong;

    [ObservableProperty]
    private MediaAsset? _selectedMedia;

    [ObservableProperty]
    private Presentation? _selectedPresentation;

    [ObservableProperty]
    private Theme? _selectedTheme;

    [ObservableProperty]
    private ServiceInterval? _selectedServiceInterval;

    partial void OnSelectedSongChanged(Song? value)
    {
        if (value is null)
        {
            return;
        }

        ShowPreview(value.Title, BuildSongPreview(value));
    }

    partial void OnSelectedMediaChanged(MediaAsset? value)
    {
        if (value is null)
        {
            return;
        }

        var details = new List<string>
        {
            value.Path,
            $"{value.Width ?? 0} x {value.Height ?? 0}",
            $"Type {value.MediaType} | Status {value.Status}",
        };
        if (!string.IsNullOrWhiteSpace(value.ErrorMessage))
        {
            details.Add(value.ErrorMessage);
        }

        ShowPreview(Path.GetFileName(value.Path), string.Join(Environment.NewLine, details));
    }

    partial void OnSelectedPresentationChanged(Presentation? value)
    {
        if (value is null)
        {
            return;
        }

        var text = $"{value.Slides.Count} slide(s)";
        if (!string.IsNullOrWhiteSpace(value.Author))
        {
            text = $"{text}{Environment.NewLine}{value.Author}";
        }

        ShowPreview(value.Title, text);
    }

    partial void OnSelectedThemeChanged(Theme? value)
    {
        if (value is null)
        {
            return;
        }

        ShowPreview(value.Name, value.SourcePath ?? "No source path recorded.");
    }

    partial void OnSelectedServiceIntervalChanged(ServiceInterval? value)
    {
        if (value is null)
        {
            return;
        }

        ShowPreview(
            value.Name ?? "Service interval",
            $"{value.StartValue ?? "?"}  -  {value.EndValue ?? "?"}");
    }

    [RelayCommand]
    private async Task ImportSongsAsync()
    {
        var dialog = CreateDialog("Select EasyWorship Songs.db", "Songs database (Songs.db)|Songs.db|SQLite database (*.db)|*.db|All files (*.*)|*.*");
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var wordsPath = Path.Combine(Path.GetDirectoryName(dialog.FileName)!, "SongWords.db");
        if (!File.Exists(wordsPath))
        {
            ShowError("SongWords.db was not found next to the selected Songs.db.");
            return;
        }

        await RunImportAsync(
            "songs",
            () =>
            {
                var result = _songImportService.Import(LegacySongReader.Read(dialog.FileName, wordsPath));
                return $"Songs imported: {result.Created} new, {result.Updated} updated.";
            });
    }

    [RelayCommand]
    private async Task ImportMediaAsync()
    {
        var dialog = CreateDialog("Select EasyWorship Media.db", "Media database (Media.db)|Media.db|SQLite database (*.db)|*.db|All files (*.*)|*.*");
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        await RunImportAsync(
            "media",
            () =>
            {
                var result = _mediaImportService.Import(LegacyMediaReader.Read(dialog.FileName));
                return $"Media imported: {result.Created} new, {result.Updated} updated.";
            });
    }

    [RelayCommand]
    private async Task ImportPresentationsAsync()
    {
        var dialog = CreateDialog("Select PresentationLayouts.db", "Presentation database (PresentationLayouts.db)|PresentationLayouts.db|SQLite database (*.db)|*.db|All files (*.*)|*.*");
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        await RunImportAsync(
            "presentations",
            () =>
            {
                var result = _presentationImportService.Import(
                    LegacyPresentationReader.Read(dialog.FileName));
                return $"Presentations imported: {result.PresentationsCreated} new, {result.PresentationsUpdated} updated. Resources: {result.ResourcesCreated + result.ResourcesUpdated}.";
            });
    }

    [RelayCommand]
    private async Task ImportThemeAsync()
    {
        var dialog = CreateDialog("Select EasyWorship theme", "EasyWorship theme (*.ewtx)|*.ewtx|All files (*.*)|*.*");
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        await RunImportAsync(
            "theme",
            () =>
            {
                var result = _themeImportService.Import(LegacyThemeReader.Read(dialog.FileName));
                return $"Theme imported: {result.Created} new, {result.Updated} updated.";
            });
    }

    [RelayCommand]
    private async Task ImportServiceIntervalsAsync()
    {
        var dialog = CreateDialog("Select ServiceIntervals.db", "Service interval database (ServiceIntervals.db)|ServiceIntervals.db|SQLite database (*.db)|*.db|All files (*.*)|*.*");
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        await RunImportAsync(
            "service intervals",
            () =>
            {
                var result = _serviceIntervalImportService.Import(
                    LegacyServiceIntervalReader.Read(dialog.FileName));
                return $"Service intervals imported: {result.Created} new, {result.Updated} updated.";
            });
    }

    [RelayCommand]
    private async Task ImportSettingsAsync()
    {
        var dialog = CreateDialog(
            "Select EasyWorship settings files",
            "EasyWorship settings (*.ini)|*.ini|All files (*.*)|*.*",
            multiselect: true);
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var paths = dialog.FileNames;
        await RunImportAsync(
            "settings",
            () =>
            {
                var files = paths
                    .Select(LegacyIniFileReader.Read)
                    .ToArray();
                var result = _settingsImportService.Import(files);
                return $"Settings imported from {files.Length} file(s): {result.Created} new, {result.Updated} updated, {result.Warnings.Count} warning(s).";
            });
    }

    [RelayCommand]
    private void GoLive()
    {
        if (SelectedSong is not null)
        {
            _liveSong = SelectedSong;
            _liveSlideIndex = 0;
            UpdateLiveSongSlide();
            return;
        }

        _liveSong = null;
        LiveTitle = _activeTitle;
        LiveText = _activeText;
        OutputStatus = $"Live: {_activeTitle}";
        Status = "Sent to live output";
    }

    [RelayCommand]
    private void NextSlide()
    {
        if (_liveSong is null || _liveSong.Slides.Count == 0)
        {
            Status = "No live song is active";
            return;
        }

        _liveSlideIndex = Math.Min(_liveSlideIndex + 1, _liveSong.Slides.Count - 1);
        UpdateLiveSongSlide();
    }

    [RelayCommand]
    private void PreviousSlide()
    {
        if (_liveSong is null || _liveSong.Slides.Count == 0)
        {
            Status = "No live song is active";
            return;
        }

        _liveSlideIndex = Math.Max(_liveSlideIndex - 1, 0);
        UpdateLiveSongSlide();
    }

    [RelayCommand]
    private void BlackOutput()
    {
        LiveTitle = "Black";
        LiveText = string.Empty;
        OutputStatus = "Black";
        Status = "Live output is black";
    }

    [RelayCommand]
    private void ClearOutput()
    {
        _liveSong = null;
        LiveTitle = "Live Output";
        LiveText = "No output";
        OutputStatus = "No output";
        Status = "Live output cleared";
    }

    [RelayCommand]
    private void ShowAlerts()
    {
        Status = "Alerts are not implemented yet";
    }

    [RelayCommand]
    private void ShowLogo()
    {
        Status = "Logo output is not implemented yet";
    }

    [RelayCommand]
    private void OpenDataFolder()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = DataRoot,
            UseShellExecute = true,
        });
    }

    [RelayCommand]
    private static void Exit() => Application.Current.Shutdown();

    private async Task RunImportAsync(string label, Func<string> import)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        Status = $"Importing {label}...";

        try
        {
            var message = await Task.Run(import);
            RefreshLibrary();
            Status = message;
        }
        catch (Exception exception)
        {
            LogImportFailed(_logger, label, exception);
            Status = $"{label} import failed";
            ShowError($"{label} import failed:{Environment.NewLine}{exception.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void RefreshLibrary()
    {
        Replace(Songs, _songRepository.List());
        Replace(Media, _mediaRepository.List());
        Replace(Presentations, _presentationRepository.List());
        Replace(Themes, _themeRepository.List());
        Replace(ServiceIntervals, _serviceIntervalRepository.List());
        Replace(Settings, _settingRepository.List());

        LibrarySummary =
            $"Songs {Songs.Count} | Media {Media.Count} | Presentations {Presentations.Count} | " +
            $"Themes {Themes.Count} | Service intervals {ServiceIntervals.Count} | Settings {Settings.Count}";
    }

    private void ShowPreview(string title, string text)
    {
        _activeTitle = title;
        _activeText = text;
        PreviewTitle = title;
        PreviewText = text;
    }

    private void UpdateLiveSongSlide()
    {
        if (_liveSong is null || _liveSong.Slides.Count == 0)
        {
            return;
        }

        var slide = _liveSong.Slides[_liveSlideIndex];
        LiveTitle = $"{_liveSong.Title} ({_liveSlideIndex + 1}/{_liveSong.Slides.Count})";
        LiveText = slide.PlainText;
        OutputStatus = $"Live: {_liveSong.Title}";
        Status = $"Live slide {_liveSlideIndex + 1} of {_liveSong.Slides.Count}";
    }

    private static string BuildSongPreview(Song song)
    {
        if (song.Slides.Count == 0)
        {
            return "No lyrics imported for this song.";
        }

        var slides = song.Slides.Take(6).Select(slide => slide.PlainText);
        var text = string.Join($"{Environment.NewLine}{Environment.NewLine}", slides);
        return song.Slides.Count > 6
            ? $"{text}{Environment.NewLine}{Environment.NewLine}..."
            : text;
    }

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> source)
    {
        target.Clear();
        foreach (var item in source)
        {
            target.Add(item);
        }
    }

    private static OpenFileDialog CreateDialog(
        string title,
        string filter,
        bool multiselect = false) =>
        new()
        {
            Title = title,
            Filter = filter,
            CheckFileExists = true,
            Multiselect = multiselect,
        };

    private static void ShowError(string message) =>
        MessageBox.Show(message, "EW Clone", MessageBoxButton.OK, MessageBoxImage.Warning);

    [LoggerMessage(Level = LogLevel.Information, Message = "Main view model created for profile {ProfileName}")]
    private static partial void LogMainViewModelCreated(ILogger logger, string profileName);

    [LoggerMessage(Level = LogLevel.Error, Message = "Import failed for {ImportLabel}")]
    private static partial void LogImportFailed(ILogger logger, string importLabel, Exception exception);
}
