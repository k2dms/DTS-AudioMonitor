using DtsAudioMonitor.Models;

namespace DtsAudioMonitor.Services;

public sealed class MonitorEngine : IDisposable
{
  private static readonly TimeSpan SpatialWrongFixCooldown = TimeSpan.FromSeconds(20);

  private readonly AppConfig _config;
  private readonly AudioService _audio;
  private readonly SpatialService _spatial;
  private readonly DtsAppService _dts;
  private readonly Action<string> _log;
  private readonly Action<string, string> _status;
  private readonly SemaphoreSlim _fixGate = new(1, 1);

  private CancellationTokenSource? _cts;
  private Task? _loop;
  private volatile bool _busy;
  private string? _previousName;
  private DateTime _lastHeadphonesCheck = DateTime.MinValue;
  private DateTime _lastMonitorFix = DateTime.MinValue;
  private DateTime _lastWrongSpatialFix = DateTime.MinValue;
  private bool _startupFixScheduled;

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
    ScheduleStartupMonitorFixIfNeeded();
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
    if (!await _fixGate.WaitAsync(0, ct))
    {
      _log("Monitor fix: already running, skipped.");
      return;
    }

    try
    {
      await RunMonitorFixAsync(ct);
    }
    finally
    {
      _fixGate.Release();
    }
  }

  private void ScheduleStartupMonitorFixIfNeeded()
  {
    if (_startupFixScheduled) return;
    _startupFixScheduled = true;

    _ = Task.Run(async () =>
    {
      try
      {
        await Task.Delay(TimeSpan.FromSeconds(10));
        if (!AutoEnabled || _cts?.IsCancellationRequested == true) return;

        var current = _audio.GetDefaultPlayback();
        if (current is null || !DevicePattern.IsMatch(current.Name, _config.MonitorNameMatch)) return;

        var spatial = _spatial.Evaluate();
        if (!spatial.NeedsFix) return;

        _log(spatial.IsWrongFormat
          ? $"Startup: wrong spatial ({spatial.ActiveGuid}), auto fix..."
          : "Startup: monitor active, spatial off — auto fix...");
        await RunMonitorFixNowAsync();
      }
      catch (Exception ex) when (ex is not OperationCanceledException)
      {
        _log($"Startup fix: {ex.Message}");
      }
    });
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

    _status(current.Name, DevicePattern.GetKind(current.Name));
    var spatial = _spatial.Evaluate();

    if (DevicePattern.IsMatch(current.Name, _config.HeadphonesNameMatch))
    {
      if (ShouldCheckHeadphones(spatial))
        await TryRunHeadphonesSpatialFixAsync(ct, spatial);
    }
    else if (DevicePattern.IsMatch(current.Name, _config.MonitorNameMatch))
    {
      if (spatial.NeedsFix && CanFixWrongSpatial())
      {
        _log(spatial.IsWrongFormat
          ? $"Monitor: wrong spatial ({spatial.ActiveGuid}) → restoring DTS Headphone:X..."
          : "Monitor: headphones spatial off → restoring...");
        await TryRunHeadphonesSpatialFixAsync(ct, spatial);
      }
      else if (ShouldRunMonitorTransitionFix(current.Name))
      {
        _log($"Switch to monitor: '{_previousName}' -> '{current.Name}'");
        await RunMonitorFixNowAsync(ct);
        _lastMonitorFix = DateTime.UtcNow;
      }
    }

    _previousName = current.Name;
  }

  private bool ShouldCheckHeadphones(SpatialHealth spatial) =>
    spatial.NeedsFix
    || (DateTime.UtcNow - _lastHeadphonesCheck).TotalSeconds >= _config.HeadphonesCheckSeconds;

  private bool ShouldRunMonitorTransitionFix(string currentName) =>
    _previousName is not null
    && !DevicePattern.IsMatch(_previousName, _config.MonitorNameMatch)
    && (DateTime.UtcNow - _lastMonitorFix).TotalSeconds >= _config.MonitorFixCooldownSeconds;

  private bool CanFixWrongSpatial() =>
    (DateTime.UtcNow - _lastWrongSpatialFix) >= SpatialWrongFixCooldown;

  private async Task TryRunHeadphonesSpatialFixAsync(CancellationToken ct, SpatialHealth spatial)
  {
    if (!await _fixGate.WaitAsync(0, ct)) return;
    try
    {
      await RestoreHeadphonesSpatialAsync(ct, spatial, useDtsApp: spatial.IsWrongFormat || spatial.IsDisabled);
      _lastHeadphonesCheck = DateTime.UtcNow;
      _lastWrongSpatialFix = DateTime.UtcNow;
    }
    finally
    {
      _fixGate.Release();
    }
  }

  private async Task RestoreHeadphonesSpatialAsync(CancellationToken ct, SpatialHealth spatial, bool useDtsApp)
  {
    if (spatial.IsWrongFormat)
      _log($"Headphones: wrong spatial ({spatial.ActiveGuid}), switching to {_config.SpatialFormat}...");
    else
      _log($"Headphones: ensuring {_config.SpatialFormat}...");

    if (useDtsApp)
    {
      try
      {
        await _dts.TryActivateHeadphoneXAsync(ct);
      }
      catch (Exception ex)
      {
        _log($"DTS app warning: {ex.Message}");
        await _dts.CloseAndWaitAsync(ct);
      }
    }

    _spatial.EnsureSpatialOnHeadphones();
    _log("Headphones: DTS Headphone:X OK.");
  }

  private async Task RunMonitorFixAsync(CancellationToken ct)
  {
    _busy = true;
    try
    {
      var hp = _audio.FindByNamePattern(_config.HeadphonesNameMatch)
               ?? throw new InvalidOperationException("Headphones not found");
      var mon = _audio.FindByNamePattern(_config.MonitorNameMatch)
                ?? throw new InvalidOperationException("Monitor not found");

      var current = _audio.GetDefaultPlayback();
      if (current is null || !DevicePattern.IsMatch(current.Name, _config.HeadphonesNameMatch))
      {
        _log("Monitor fix: switch to headphones...");
        _audio.SetDefault(hp.Id);
        await WaitForDefaultDeviceAsync(hp.Name, TimeSpan.FromSeconds(8), ct);
      }

      await Task.Delay(1500, ct);

      var spatial = _spatial.Evaluate();
      await RestoreHeadphonesSpatialAsync(ct, spatial, useDtsApp: true);

      await Task.Delay(800, ct);

      _log("Monitor fix: switching back to monitor...");
      _audio.SetDefault(mon.Id);
      await WaitForDefaultDeviceAsync(mon.Name, TimeSpan.FromSeconds(8), ct);

      _lastHeadphonesCheck = DateTime.UtcNow;
      _lastMonitorFix = DateTime.UtcNow;
      _lastWrongSpatialFix = DateTime.UtcNow;
      _log("Monitor fix: done.");
      _status(mon.Name, "monitor");
    }
    finally
    {
      _busy = false;
    }
  }

  private async Task WaitForDefaultDeviceAsync(string nameContains, TimeSpan timeout, CancellationToken ct)
  {
    var deadline = DateTime.UtcNow + timeout;
    while (DateTime.UtcNow < deadline)
    {
      ct.ThrowIfCancellationRequested();
      var d = _audio.GetDefaultPlayback();
      if (d is not null && d.Name.Contains(nameContains, StringComparison.OrdinalIgnoreCase))
        return;
      await Task.Delay(250, ct);
    }
  }

  public void Dispose()
  {
    Stop();
    _fixGate.Dispose();
  }
}
