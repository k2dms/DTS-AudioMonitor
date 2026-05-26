using NAudio.CoreAudioApi;

namespace DtsAudioMonitor.Services;

public sealed record PlaybackDevice(string Id, string Name);

public sealed class AudioService
{
    private readonly MMDeviceEnumerator _enumerator = new();

    public PlaybackDevice? GetDefaultPlayback()
    {
        try
        {
            using var device = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            return new PlaybackDevice(device.ID, device.FriendlyName);
        }
        catch
        {
            return null;
        }
    }

    public IReadOnlyList<PlaybackDevice> ListPlayback()
    {
        var list = new List<PlaybackDevice>();
        foreach (var device in _enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
        {
            list.Add(new PlaybackDevice(device.ID, device.FriendlyName));
        }
        return list;
    }

    public PlaybackDevice? FindByNamePattern(string pattern)
    {
        var glob = pattern.TrimEnd('*');
        return ListPlayback().FirstOrDefault(d =>
            pattern.EndsWith('*')
                ? d.Name.StartsWith(glob, StringComparison.OrdinalIgnoreCase)
                : d.Name.Equals(pattern, StringComparison.OrdinalIgnoreCase));
    }

    public void SetDefault(string deviceId)
    {
        PolicyConfig.SetDefaultDevice(deviceId);
    }
}
