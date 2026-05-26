using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DtsAudioMonitor.ViewModels;

namespace DtsAudioMonitor;

public partial class MainWindow : Window
{
    public MainViewModel ViewModel { get; }

    public MainWindow(MainViewModel vm)
    {
        ViewModel = vm;
        DataContext = vm;
        InitializeComponent();
        Loaded += (_, _) => ApplyWindowChrome();
        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(MainViewModel.DeviceKind) or nameof(MainViewModel.DeviceKindLabel))
                ApplyDeviceKindStyle();
        };
        ApplyDeviceKindStyle();
    }

    private void ApplyWindowChrome()
    {
        var ico = Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");
        if (File.Exists(ico))
            Icon = BitmapFrame.Create(new Uri(ico, UriKind.Absolute));

        var logo = Path.Combine(AppContext.BaseDirectory, "Assets", "logo.png");
        if (!TryLoadLogo(logo))
            TryLoadLogo(Path.Combine(AppContext.BaseDirectory, "logo.png"));
    }

    private bool TryLoadLogo(string path)
    {
        if (!File.Exists(path)) return false;
        try
        {
            var img = new BitmapImage();
            img.BeginInit();
            img.UriSource = new Uri(path, UriKind.Absolute);
            img.CacheOption = BitmapCacheOption.OnLoad;
            img.EndInit();
            HeroLogoImage.Source = img;
            HeroLogoImage.Visibility = Visibility.Visible;
            HeroLogoFallback.Visibility = Visibility.Collapsed;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void ApplyDeviceKindStyle()
    {
        var kind = ViewModel.DeviceKind;
        if (kind == "headphones")
        {
            DeviceIconText.Text = "\uE7F6";
            DeviceKindLabel.Foreground = (Brush)FindResource("HeadphonesBrush");
        }
        else if (kind == "monitor")
        {
            DeviceIconText.Text = "\uE7F4";
            DeviceKindLabel.Foreground = (Brush)FindResource("MonitorBrush");
        }
        else
        {
            DeviceIconText.Text = "\uE74B";
            DeviceKindLabel.Foreground = (Brush)FindResource("MutedBrush");
        }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2) return;
        try { DragMove(); }
        catch { /* ignore drag race */ }
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void HideToTray_Click(object sender, RoutedEventArgs e) => Hide();

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
    }
}
