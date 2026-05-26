using System.Windows;
using System.Windows.Media;
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
        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(MainViewModel.DeviceKind) or nameof(MainViewModel.DeviceKindLabel))
                ApplyDeviceKindStyle();
        };
        ApplyDeviceKindStyle();
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

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
    }
}
