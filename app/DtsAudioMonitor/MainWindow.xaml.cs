using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using DtsAudioMonitor.ViewModels;

namespace DtsAudioMonitor;

public partial class MainWindow : Window
{
    private static readonly Duration ShowDuration = TimeSpan.FromMilliseconds(280);
    private static readonly Duration HideDuration = TimeSpan.FromMilliseconds(220);

    private bool _isHiding;

    public MainViewModel ViewModel { get; }

    public MainWindow(MainViewModel vm)
    {
        ViewModel = vm;
        DataContext = vm;
        InitializeComponent();
        Loaded += OnLoaded;
        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(MainViewModel.DeviceKind) or nameof(MainViewModel.DeviceKindLabel))
                ApplyDeviceKindStyle();
        };
        ApplyDeviceKindStyle();
    }

    public bool AnimateOnFirstShow { get; set; } = true;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ApplyWindowChrome();
        if (AnimateOnFirstShow && IsVisible)
            PlayShowAnimation();
    }

    public void PlayShowAnimation()
    {
        _isHiding = false;
        Show();
        WindowState = WindowState.Normal;
        Activate();

        RootChrome.BeginAnimation(OpacityProperty, null);
        RootScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        RootScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);

        RootChrome.Opacity = 0;
        RootScale.ScaleX = 0.9;
        RootScale.ScaleY = 0.9;

        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        RootChrome.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, ShowDuration) { EasingFunction = ease });
        RootScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.9, 1, ShowDuration) { EasingFunction = ease });
        RootScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.9, 1, ShowDuration) { EasingFunction = ease });
    }

    public void PlayHideAnimation(Action onComplete)
    {
        if (_isHiding || !IsVisible)
        {
            onComplete();
            return;
        }

        _isHiding = true;
        var ease = new CubicEase { EasingMode = EasingMode.EaseIn };

        var opacityAnim = new DoubleAnimation(RootChrome.Opacity, 0, HideDuration) { EasingFunction = ease };
        Storyboard.SetTarget(opacityAnim, RootChrome);
        Storyboard.SetTargetProperty(opacityAnim, new PropertyPath(OpacityProperty));

        var scaleXAnim = new DoubleAnimation(RootScale.ScaleX, 0.94, HideDuration) { EasingFunction = ease };
        Storyboard.SetTarget(scaleXAnim, RootScale);
        Storyboard.SetTargetProperty(scaleXAnim, new PropertyPath(ScaleTransform.ScaleXProperty));

        var scaleYAnim = new DoubleAnimation(RootScale.ScaleY, 0.94, HideDuration) { EasingFunction = ease };
        Storyboard.SetTarget(scaleYAnim, RootScale);
        Storyboard.SetTargetProperty(scaleYAnim, new PropertyPath(ScaleTransform.ScaleYProperty));

        var storyboard = new Storyboard();
        storyboard.Children.Add(opacityAnim);
        storyboard.Children.Add(scaleXAnim);
        storyboard.Children.Add(scaleYAnim);
        storyboard.Completed += (_, _) =>
        {
            RootChrome.BeginAnimation(OpacityProperty, null);
            RootScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            RootScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            RootChrome.Opacity = 1;
            RootScale.ScaleX = 1;
            RootScale.ScaleY = 1;
            _isHiding = false;
            onComplete();
        };
        storyboard.Begin();
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
        catch { /* ignore */ }
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void HideToTray_Click(object sender, RoutedEventArgs e) =>
        PlayHideAnimation(() => Hide());

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = true;
        PlayHideAnimation(() => Hide());
    }
}
