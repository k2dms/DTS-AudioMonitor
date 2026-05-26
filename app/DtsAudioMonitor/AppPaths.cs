using System.IO;

namespace DtsAudioMonitor;

internal static class AppPaths
{
    public static string BaseDir => AppContext.BaseDirectory;

    public static string ConfigPath => Path.Combine(BaseDir, "config.json");

    public static string SoundVolumeViewExe =>
        Path.Combine(BaseDir, "SoundVolumeView", "SoundVolumeView.exe");

    public static string LogPath => Path.Combine(BaseDir, "app.log");

    public static string StartupShortcut =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Startup),
            "DTS Audio Monitor.lnk");
}
