using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Threading;
using DtsAudioMonitor.Models;
using DtsAudioMonitor.Services;

namespace DtsAudioMonitor.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly Dispatcher _dispatcher;
    private readonly AppConfig _config;
    private readonly MonitorEngine _engine;
    private readonly AudioService _audio = new();

    private string _deviceName = "—";
    private string _deviceKind = "other";
    private bool _autoEnabled = true;
    private bool _startWithWindows;
    private bool _isBusy;
    private string _statusText = "Готов";

    public string VersionLabel { get; } =
        "v" + (Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.1.0");

    public MainViewModel()
    {
        _dispatcher = Application.Current.Dispatcher;
        _config = AppConfig.Load(AppPaths.ConfigPath);

        var spatial = new SpatialService(AppPaths.SoundVolumeViewExe);
        var dts = new DtsAppService();
        _engine = new MonitorEngine(
            _config,
            _audio,
            spatial,
            dts,
            AppendLog,
            UpdateDevice);

        _engine.AutoEnabled = true;
        _autoEnabled = true;
        _startWithWindows = File.Exists(AppPaths.StartupShortcut);

        RefreshDevice();
        _engine.Start();
        AppendLog("DTS Audio Monitor started.");
    }

    public ObservableCollection<string> LogLines { get; } = new();

    public string DeviceName
    {
        get => _deviceName;
        private set { _deviceName = value; OnPropertyChanged(); }
    }

    public string DeviceKind
    {
        get => _deviceKind;
        private set { _deviceKind = value; OnPropertyChanged(); OnPropertyChanged(nameof(DeviceKindLabel)); }
    }

    public string DeviceKindLabel => DeviceKind switch
    {
        "headphones" => "Наушники — только spatial",
        "monitor" => "Монитор — авто-цикл при переключении",
        _ => "Другое устройство"
    };

    public bool AutoEnabled
    {
        get => _autoEnabled;
        set
        {
            if (_autoEnabled == value) return;
            _autoEnabled = value;
            _engine.AutoEnabled = value;
            OnPropertyChanged();
            StatusText = value ? "Активен" : "Пауза";
            AppendLog(value ? "Auto monitoring enabled." : "Auto monitoring paused.");
        }
    }

    public bool StartWithWindows
    {
        get => _startWithWindows;
        set
        {
            if (_startWithWindows == value) return;
            _startWithWindows = value;
            _config.StartWithWindows = value;
            _config.Save(AppPaths.ConfigPath);
            SetStartup(value);
            OnPropertyChanged();
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set { _isBusy = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanFix)); }
    }

    public bool CanFix => !IsBusy;

    public string StatusText
    {
        get => _statusText;
        private set { _statusText = value; OnPropertyChanged(); }
    }

    public RelayCommand FixCommand { get; set; } = null!;

    public async Task FixMonitorAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        FixCommand.RaiseCanExecuteChanged();
        StatusText = "Работа...";
        try
        {
            await _engine.RunMonitorFixNowAsync();
            RefreshDevice();
            StatusText = "Готов";
        }
        catch (Exception ex)
        {
            AppendLog($"Fix failed: {ex.Message}");
            StatusText = "Ошибка";
        }
        finally
        {
            IsBusy = false;
            FixCommand.RaiseCanExecuteChanged();
        }
    }

    public void RefreshDevice()
    {
        var d = _audio.GetDefaultPlayback();
        if (d is not null)
            UpdateDevice(d.Name, d.Name.Contains("Headphone", StringComparison.OrdinalIgnoreCase) ? "headphones"
                : d.Name.Contains("XV272U", StringComparison.OrdinalIgnoreCase) ? "monitor" : "other");
    }

    private void UpdateDevice(string name, string kind)
    {
        RunOnUi(() =>
        {
            DeviceName = name;
            DeviceKind = kind;
        });
    }

    private void AppendLog(string line)
    {
        var text = $"{DateTime.Now:HH:mm:ss}  {line}";
        try { File.AppendAllText(AppPaths.LogPath, text + Environment.NewLine); } catch { /* ignore */ }

        RunOnUi(() =>
        {
            LogLines.Insert(0, text);
            while (LogLines.Count > 12)
                LogLines.RemoveAt(LogLines.Count - 1);
        });
    }

    private void RunOnUi(Action action)
    {
        if (_dispatcher.CheckAccess()) action();
        else _dispatcher.Invoke(action);
    }

    private static void SetStartup(bool enable)
    {
        try
        {
            if (enable)
            {
                var exe = Environment.ProcessPath ?? throw new InvalidOperationException("Process path unknown");
                CreateShortcut(AppPaths.StartupShortcut, exe);
            }
            else if (File.Exists(AppPaths.StartupShortcut))
            {
                File.Delete(AppPaths.StartupShortcut);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Startup: {ex.Message}", "DTS Audio Monitor", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private static void CreateShortcut(string shortcutPath, string targetExe)
    {
        var shell = Type.GetTypeFromProgID("WScript.Shell")
                    ?? throw new InvalidOperationException("WScript.Shell unavailable");
        dynamic wsh = Activator.CreateInstance(shell)!;
        dynamic shortcut = wsh.CreateShortcut(shortcutPath);
        shortcut.TargetPath = targetExe;
        shortcut.WorkingDirectory = Path.GetDirectoryName(targetExe)!;
        shortcut.Description = "DTS Audio Monitor";
        shortcut.Save();
    }

    public void Dispose() => _engine.Dispose();

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
