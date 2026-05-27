using System.Drawing;
using System.IO;
using System.Windows;
using Hardcodet.Wpf.TaskbarNotification;
using DtsAudioMonitor.Services;
using DtsAudioMonitor.ViewModels;

namespace DtsAudioMonitor;

public partial class App : System.Windows.Application
{
    private SingleInstance? _singleInstance;
    private TaskbarIcon? _tray;
    private MainWindow? _window;
    private MainViewModel? _vm;

    protected override void OnStartup(StartupEventArgs e)
    {
        if (!SingleInstance.TryStart(() => Shutdown(0), ActivateFromRunningInstance, out _singleInstance))
            return;

        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        ShellRegistration.Apply();

        _vm = new MainViewModel();
        _vm.FixCommand = new RelayCommand(_vm.FixMonitorAsync, () => _vm.CanFix);
        _vm.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(MainViewModel.CanFix) or nameof(MainViewModel.IsBusy))
                _vm.FixCommand.RaiseCanExecuteChanged();
        };

        _window = new MainWindow(_vm)
        {
            AnimateOnFirstShow = !e.Args.Contains("--minimized", StringComparer.OrdinalIgnoreCase)
        };

        var appIcon = LoadAppIcon();
        _tray = new TaskbarIcon
        {
            ToolTipText = "DTS Audio Monitor",
            ContextMenu = BuildTrayMenu(),
            Icon = appIcon
        };
        _tray.TrayMouseDoubleClick += (_, _) => ShowMain();

        if (e.Args.Contains("--minimized", StringComparer.OrdinalIgnoreCase))
            _window.Hide();
        else
            _window.Show();
    }

    private void ActivateFromRunningInstance()
    {
        Dispatcher.BeginInvoke(() =>
        {
            ShowMain();
            _tray?.ShowBalloonTip("DTS Audio Monitor", "Уже запущен — открыто существующее окно.", BalloonIcon.Info);
        });
    }

    private System.Windows.Controls.ContextMenu BuildTrayMenu()
    {
        var menu = new System.Windows.Controls.ContextMenu();

        var open = new System.Windows.Controls.MenuItem { Header = "Открыть" };
        open.Click += (_, _) => ShowMain();
        menu.Items.Add(open);

        var fix = new System.Windows.Controls.MenuItem { Header = "DTS для монитора" };
        fix.Click += async (_, _) => { if (_vm is not null) await _vm.FixMonitorAsync(); };
        menu.Items.Add(fix);

        menu.Items.Add(new System.Windows.Controls.Separator());

        var exit = new System.Windows.Controls.MenuItem { Header = "Выход" };
        exit.Click += (_, _) => ShutdownApp();
        menu.Items.Add(exit);

        return menu;
    }

    private static Icon LoadAppIcon()
    {
        var paths = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico"),
            Path.Combine(AppContext.BaseDirectory, "app.ico")
        };
        foreach (var p in paths)
        {
            if (File.Exists(p))
                return new Icon(p);
        }
        return Icon.ExtractAssociatedIcon(Environment.ProcessPath!) ?? SystemIcons.Application;
    }

    private void ShowMain()
    {
        if (_window is null) return;
        _window.PlayShowAnimation();
    }

    private void ShutdownApp()
    {
        _vm?.Dispose();
        _tray?.Dispose();
        _singleInstance?.Dispose();
        _singleInstance = null;
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _vm?.Dispose();
        _tray?.Dispose();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
