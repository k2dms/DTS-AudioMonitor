using System.IO;
using System.Text.Json;

namespace DtsAudioMonitor.Models;

public sealed class AppConfig
{
    public string HeadphonesNameMatch { get; set; } = "Headphones*";
    public string MonitorNameMatch { get; set; } = "XV272U*";
    public string HeadphonesFriendlyId { get; set; } = @"HyperX Cloud III\Device\Headphones\Render";
    public string MonitorFriendlyId { get; set; } = @"NVIDIA High Definition Audio\Device\XV272U F3\Render";
    public string HeadphonesRegistryGuid { get; set; } = "{e0cb2f31-49bb-444d-bf05-d086c762cc93}";
    public string SpatialFormat { get; set; } = "DTS Headphone:X";
    public int PollSeconds { get; set; } = 3;
    public int MonitorFixCooldownSeconds { get; set; } = 45;
    public int HeadphonesCheckSeconds { get; set; } = 300;
    /// <summary>Hide DTS Sound Unbound window while automating (off-screen / minimized).</summary>
    public bool DtsAppRunHidden { get; set; } = true;
    public string SpatialDisabledGuid { get; set; } = "{00000000-0000-0000-0000-000000000000}";
    public bool StartWithWindows { get; set; }

    public static AppConfig Load(string path)
    {
        if (!File.Exists(path))
            return new AppConfig();

        var json = File.ReadAllText(path);
        var cfg = JsonSerializer.Deserialize<AppConfig>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        });
        return cfg ?? new AppConfig();
    }

    public void Save(string path)
    {
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }
}
