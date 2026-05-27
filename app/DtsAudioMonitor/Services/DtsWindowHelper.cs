using System.Runtime.InteropServices;
using System.Windows.Automation;

namespace DtsAudioMonitor.Services;

internal static class DtsWindowHelper
{
    private const int SwHide = 0;
    private const int SwMinimize = 6;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    /// <summary>Hides DTS window off-screen so automation can run without flashing on screen.</summary>
    public static void HideFromView(AutomationElement window)
    {
        var hwnd = IntPtr.Zero;
        try
        {
            if (window.Current.NativeWindowHandle != 0)
                hwnd = (IntPtr)window.Current.NativeWindowHandle;
        }
        catch
        {
            // ignore
        }

        if (hwnd != IntPtr.Zero)
        {
            SetWindowPos(hwnd, IntPtr.Zero, -32000, -32000, 0, 0, SwpNoSize | SwpNoZOrder | SwpNoActivate);
            ShowWindow(hwnd, SwHide);
            return;
        }

        try
        {
            if (window.GetCurrentPattern(WindowPattern.Pattern) is WindowPattern wp)
                wp.SetWindowVisualState(WindowVisualState.Minimized);
        }
        catch
        {
            // ignore
        }
    }
}
