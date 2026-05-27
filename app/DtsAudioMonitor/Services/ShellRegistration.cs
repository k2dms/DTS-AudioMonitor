using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace DtsAudioMonitor.Services;

/// <summary>
/// Registers Start menu shortcut and Apps &amp; Features entry so Windows lists the app.
/// </summary>
internal static class ShellRegistration
{
    private const string AppName = "DTS Audio Monitor";
    private const string AppUserModelId = "k2dms.DtsAudioMonitor";
    private const string UninstallKeyName = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\k2dms.DtsAudioMonitor";

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SetCurrentProcessExplicitAppUserModelID(string appId);

    public static void Apply()
    {
        try { SetCurrentProcessExplicitAppUserModelID(AppUserModelId); } catch { /* ignore */ }

        try
        {
            EnsureStartMenuShortcut();
            EnsureUninstallEntry();
        }
        catch
        {
            // Non-fatal: app still runs from tray / publish folder
        }
    }

    private static void EnsureStartMenuShortcut()
    {
        var exe = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exe) || !File.Exists(exe)) return;

        var programs = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
        var folder = Path.Combine(programs, AppName);
        Directory.CreateDirectory(folder);

        var lnk = Path.Combine(folder, $"{AppName}.lnk");
        if (File.Exists(lnk)) return;

        var icon = Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");
        if (!File.Exists(icon)) icon = exe;

        CreateShortcut(lnk, exe, "", Path.GetDirectoryName(exe)!, icon, AppName);
    }

    private static void EnsureUninstallEntry()
    {
        var exe = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exe)) return;

        var installDir = AppContext.BaseDirectory.TrimEnd('\\');
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";
        var icon = Path.Combine(installDir, "Assets", "app.ico");
        if (!File.Exists(icon)) icon = exe;

        var uninstallScript = Path.Combine(installDir, "Uninstall-DtsApp.ps1");
        var uninstall = File.Exists(uninstallScript)
            ? $"powershell.exe -NoProfile -ExecutionPolicy Bypass -File \"{uninstallScript}\""
            : $"powershell.exe -NoProfile -Command \"Remove-Item -LiteralPath '{exe}' -ErrorAction SilentlyContinue\"";

        using var key = Registry.CurrentUser.CreateSubKey(UninstallKeyName, true);
        if (key is null) return;

        key.SetValue("DisplayName", AppName, RegistryValueKind.String);
        key.SetValue("DisplayIcon", icon, RegistryValueKind.String);
        key.SetValue("DisplayVersion", version, RegistryValueKind.String);
        key.SetValue("Publisher", "k2dms", RegistryValueKind.String);
        key.SetValue("InstallLocation", installDir, RegistryValueKind.String);
        key.SetValue("InstallSource", installDir, RegistryValueKind.String);
        key.SetValue("UninstallString", uninstall, RegistryValueKind.String);
        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
    }

    private static void CreateShortcut(string shortcutPath, string targetExe, string arguments, string workingDir, string iconPath, string description)
    {
        var shell = Type.GetTypeFromProgID("WScript.Shell")
                    ?? throw new InvalidOperationException("WScript.Shell unavailable");
        dynamic wsh = Activator.CreateInstance(shell)!;
        dynamic shortcut = wsh.CreateShortcut(shortcutPath);
        shortcut.TargetPath = targetExe;
        shortcut.Arguments = arguments;
        shortcut.WorkingDirectory = workingDir;
        shortcut.Description = description;
        shortcut.IconLocation = $"{iconPath},0";
        shortcut.Save();
    }
}
