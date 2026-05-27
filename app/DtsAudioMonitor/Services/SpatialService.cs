using System.Diagnostics;
using System.IO;
using DtsAudioMonitor.Models;
using Microsoft.Win32;

namespace DtsAudioMonitor.Services;

public sealed class SpatialService
{
    private readonly string _svvPath;
    private readonly AppConfig _config;

    public SpatialService(string svvPath, AppConfig config)
    {
        _svvPath = svvPath;
        _config = config;
    }

    public void EnsureSpatialOnHeadphones()
    {
        if (!File.Exists(_svvPath))
            throw new FileNotFoundException("SoundVolumeView not found", _svvPath);

        var deviceId = ResolveHeadphonesFriendlyId();
        var formats = GetSpatialFormatCandidates();

        Exception? last = null;
        foreach (var format in formats)
        {
            try
            {
                RunSetSpatial(deviceId, format);
                if (IsSpatialLikelyEnabled(_config))
                    return;
            }
            catch (Exception ex)
            {
                last = ex;
            }
        }

        throw last ?? new InvalidOperationException("Failed to enable spatial sound (unsupported format)");
    }

    private string ResolveHeadphonesFriendlyId()
    {
        var configured = _config.HeadphonesFriendlyId.Trim();
        if (!string.IsNullOrEmpty(configured))
            return configured;

        var hp = new AudioService().FindByNamePattern(_config.HeadphonesNameMatch);
        if (hp is null)
            throw new InvalidOperationException("Headphones device not found");

        return hp.Name + @"\Device\Headphones\Render";
    }

    private IReadOnlyList<string> GetSpatialFormatCandidates()
    {
        var list = new List<string>();
        if (!string.IsNullOrWhiteSpace(_config.SpatialFormat))
            list.Add(_config.SpatialFormat.Trim());

        list.Add("DTS Headphone:X");
        list.Add("DTS Headphone");
        list.Add("Headphone:X");

        return list.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private void RunSetSpatial(string deviceFriendlyId, string format)
    {
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
            throw new InvalidOperationException($"SetSpatial failed (code {p.ExitCode}) for format '{format}'");
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
