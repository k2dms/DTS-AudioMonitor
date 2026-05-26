using System.Drawing;
using System.Windows;
using Hardcodet.Wpf.TaskbarNotification;
using DtsAudioMonitor.ViewModels;

namespace DtsAudioMonitor;

public partial class App : System.Windows.Application
{
    private TaskbarIcon? _tray;
    private MainWindow? _window;
    private MainViewModel? _vm;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _vm = new MainViewModel();
        _vm.FixCommand = new RelayCommand(_vm.FixMonitorAsync, () => _vm.CanFix);
        _vm.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(MainViewModel.CanFix) or nameof(MainViewModel.IsBusy))
                _vm.FixCommand.RaiseCanExecuteChanged();
        };

        _window = new MainWindow(_vm);

        _tray = new TaskbarIcon
        {
            ToolTipText = "DTS Audio Monitor",
            ContextMenu = BuildTrayMenu(),
            Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!) ?? SystemIcons.Application
        };
        _tray.TrayMouseDoubleClick += (_, _) => ShowMain();

        if (e.Args.Contains("--minimized", StringComparer.OrdinalIgnoreCase))
            _window.Hide();
        else
            _window.Show();
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

    private void ShowMain()
    {
        if (_window is null) return;
        _window.Show();
        _window.WindowState = WindowState.Normal;
        _window.Activate();
    }

    private void ShutdownApp()
    {
        _vm?.Dispose();
        _tray?.Dispose();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _vm?.Dispose();
        _tray?.Dispose();
        base.OnExit(e);
    }
}
