using DtsAudioMonitor.Models;

namespace DtsAudioMonitor.Services;

public sealed class MonitorEngine : IDisposable
{
    private readonly AppConfig _config;
    private readonly AudioService _audio;
    private readonly SpatialService _spatial;
    private readonly DtsAppService _dts;
    private readonly Action<string> _log;
    private readonly Action<string, string> _status;

    private CancellationTokenSource? _cts;
    private Task? _loop;
    private volatile bool _busy;
    private string? _previousName;
    private DateTime _lastHeadphonesCheck = DateTime.MinValue;
    private DateTime _lastMonitorFix = DateTime.MinValue;

    public bool AutoEnabled { get; set; } = true;

    public MonitorEngine(
        AppConfig config,
        AudioService audio,
        SpatialService spatial,
        DtsAppService dts,
        Action<string> log,
        Action<string, string> status)
    {
        _config = config;
        _audio = audio;
        _spatial = spatial;
        _dts = dts;
        _log = log;
        _status = status;
    }

    public void Start()
    {
        if (_loop is not null) return;
        _cts = new CancellationTokenSource();
        _loop = Task.Run(() => RunAsync(_cts.Token));
        _log("Monitoring started.");
    }

    public void Stop()
    {
        _cts?.Cancel();
        try { _loop?.Wait(TimeSpan.FromSeconds(3)); } catch { /* ignore */ }
        _cts?.Dispose();
        _cts = null;
        _loop = null;
        _log("Monitoring stopped.");
    }

    public async Task RunMonitorFixNowAsync(CancellationToken ct = default)
    {
        if (_busy) return;
        _busy = true;
        try
        {
            await RunMonitorFixAsync(ct);
        }
        finally
        {
            _busy = false;
        }
    }

    private async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (AutoEnabled && !_busy)
                    await TickAsync(ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _log($"Error: {ex.Message}");
            }

            await Task.Delay(TimeSpan.FromSeconds(_config.PollSeconds), ct);
        }
    }

    private async Task TickAsync(CancellationToken ct)
    {
        var current = _audio.GetDefaultPlayback();
        if (current is null) return;

        _status(current.Name, GetDeviceKind(current.Name));

        if (IsMatch(current.Name, _config.HeadphonesNameMatch))
        {
            var need = (DateTime.UtcNow - _lastHeadphonesCheck).TotalSeconds >= _config.HeadphonesCheckSeconds
                       || !_spatial.IsSpatialLikelyEnabled(_config);
            if (need)
            {
                _busy = true;
                try
                {
                    _log($"Headphones: checking {_config.SpatialFormat}...");
                    await EnsureHeadphonesSpatialAsync(ct);
                    _lastHeadphonesCheck = DateTime.UtcNow;
                }
                finally { _busy = false; }
            }
        }
        else if (IsMatch(current.Name, _config.MonitorNameMatch))
        {
            if (_previousName is not null
                && !IsMatch(_previousName, _config.MonitorNameMatch)
                && (DateTime.UtcNow - _lastMonitorFix).TotalSeconds >= _config.MonitorFixCooldownSeconds)
            {
                _busy = true;
                try
                {
                    _log($"Switch to monitor: '{_previousName}' -> '{current.Name}'");
                    await RunMonitorFixAsync(ct);
                    _lastMonitorFix = DateTime.UtcNow;
                }
                finally { _busy = false; }
            }
        }

        _previousName = current.Name;
    }

    private async Task EnsureHeadphonesSpatialAsync(CancellationToken ct, bool forceDtsApp = false)
    {
        var useApp = forceDtsApp || !_spatial.IsSpatialLikelyEnabled(_config);
        if (useApp)
            await _dts.ActivateHeadphoneXAsync(ct);

        _spatial.SetSpatial(_config.HeadphonesFriendlyId, _config.SpatialFormat);
        _log("Headphones: spatial sound OK.");
    }

    private async Task RunMonitorFixAsync(CancellationToken ct)
    {
        var hp = _audio.FindByNamePattern(_config.HeadphonesNameMatch)
                 ?? throw new InvalidOperationException("Headphones not found");
        var mon = _audio.FindByNamePattern(_config.MonitorNameMatch)
                  ?? throw new InvalidOperationException("Monitor not found");

        _log("Monitor fix: switch to headphones...");
        _audio.SetDefault(hp.Id);
        await Task.Delay(2000, ct);

        _log("Monitor fix: DTS Sound Unbound...");
        await _dts.ActivateHeadphoneXAsync(ct);

        _log($"Monitor fix: enable {_config.SpatialFormat}...");
        _spatial.SetSpatial(_config.HeadphonesFriendlyId, _config.SpatialFormat);
        await Task.Delay(800, ct);

        _log("Monitor fix: switch back to monitor...");
        _audio.SetDefault(mon.Id);

        _lastHeadphonesCheck = DateTime.UtcNow;
        _log("Monitor fix: done.");
        _status(mon.Name, "monitor");
    }

    private static bool IsMatch(string name, string pattern)
    {
        if (pattern.EndsWith('*'))
            return name.StartsWith(pattern.TrimEnd('*'), StringComparison.OrdinalIgnoreCase);
        return name.Equals(pattern, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetDeviceKind(string name)
    {
        if (name.Contains("Headphone", StringComparison.OrdinalIgnoreCase)) return "headphones";
        if (name.Contains("XV272U", StringComparison.OrdinalIgnoreCase)) return "monitor";
        return "other";
    }

    public void Dispose() => Stop();
}
