using System.Diagnostics;
using System.IO;
using DtsAudioMonitor.Models;
using Microsoft.Win32;

namespace DtsAudioMonitor.Services;

public sealed class SpatialService
{
    private readonly string _svvPath;

    public SpatialService(string svvPath) => _svvPath = svvPath;

    public void SetSpatial(string deviceFriendlyId, string format)
    {
        if (!File.Exists(_svvPath))
            throw new FileNotFoundException("SoundVolumeView not found", _svvPath);

        var psi = new ProcessStartInfo
        {
            FileName = _svvPath,
            Arguments = $"/SetSpatial \"{deviceFriendlyId}\" \"{format}\"",
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var p = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start SoundVolumeView");
        p.WaitForExit();
        if (p.ExitCode != 0)
            throw new InvalidOperationException($"SetSpatial exit code {p.ExitCode}");
    }

    public bool IsSpatialLikelyEnabled(AppConfig config)
    {
        var guid = config.HeadphonesRegistryGuid.Trim('{', '}').ToLowerInvariant();
        var path = $@"SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Render\{{{guid}}}\Properties";
        using var key = Registry.LocalMachine.OpenSubKey(path);
        if (key is null) return false;

        var names = new[]
        {
            @"{6597f250-c913-4f95-8072-9c59a52b6552},3",
            @"{6597f250-c913-4f95-8072-9c59a52b6552},2",
            @"{f19f064d-082c-4e27-bc73-6882a1bb8e4c},2",
            @"{1da5d803-d492-4edd-8c23-e0c0ffee7f0e},8"
        };

        foreach (var name in names)
        {
            var val = key.GetValue(name) as string;
            if (!string.IsNullOrWhiteSpace(val)
                && val != config.SpatialDisabledGuid
                && val.StartsWith('{') && val.EndsWith('}'))
                return true;
        }
        return false;
    }
}
