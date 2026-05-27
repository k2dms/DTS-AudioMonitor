using DtsAudioMonitor.Models;

namespace DtsAudioMonitor.Services;

public sealed class MonitorEngine : IDisposable
{
  private static readonly TimeSpan SpatialAutoFixCooldown = TimeSpan.FromSeconds(60);

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
  private DateTime _lastSpatialAutoFix = DateTime.MinValue;
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
    ScheduleStartupSpatialCheckIfNeeded();
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

  /// <summary>Manual button: full cycle with optional DTS app if configured.</summary>
  public async Task RunMonitorFixNowAsync(CancellationToken ct = default)
  {
    if (!await _fixGate.WaitAsync(0, ct))
    {
      _log("Monitor fix: already running, skipped.");
      return;
    }

    try
    {
      await RunMonitorFixAsync(ct, allowDtsApp: _config.UseDtsAppOnManualFix);
    }
    finally
    {
      _fixGate.Release();
    }
  }

  private void ScheduleStartupSpatialCheckIfNeeded()
  {
    if (_startupFixScheduled) return;
    _startupFixScheduled = true;

    _ = Task.Run(async () =>
    {
      try
      {
        await Task.Delay(TimeSpan.FromSeconds(12));
        if (!AutoEnabled || _cts?.IsCancellationRequested == true) return;

        var spatial = _spatial.Evaluate();
        if (!spatial.NeedsFix) return;

        _log("Startup: spatial check (registry only)...");
        await TryRunSilentSpatialRestoreAsync(CancellationToken.None, spatial);
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
        await TryRunSilentSpatialRestoreAsync(ct, spatial);
    }
    else if (DevicePattern.IsMatch(current.Name, _config.MonitorNameMatch))
    {
      if (spatial.NeedsFix && CanRunAutoSpatialFix())
      {
        _log(spatial.IsWrongFormat
          ? $"Monitor: wrong spatial ({spatial.ActiveGuid}) → fixing via Windows (no DTS app)..."
          : "Monitor: spatial off on headphones → fixing via Windows...");
        await TryRunSilentSpatialRestoreAsync(ct, spatial);
      }
      else if (ShouldRunMonitorTransitionFix())
      {
        _log($"Switch to monitor: '{_previousName}' -> '{current.Name}'");
        await RunMonitorTransitionFixAsync(ct);
      }
    }

    _previousName = current.Name;
  }

  private bool ShouldCheckHeadphones(SpatialHealth spatial) =>
    spatial.NeedsFix && CanRunAutoSpatialFix()
    || (DateTime.UtcNow - _lastHeadphonesCheck).TotalSeconds >= _config.HeadphonesCheckSeconds
       && spatial.NeedsFix;

  private bool ShouldRunMonitorTransitionFix() =>
    _previousName is not null
    && !DevicePattern.IsMatch(_previousName, _config.MonitorNameMatch)
    && (DateTime.UtcNow - _lastMonitorFix).TotalSeconds >= _config.MonitorFixCooldownSeconds;

  private bool CanRunAutoSpatialFix() =>
    DateTime.UtcNow - _lastSpatialAutoFix >= SpatialAutoFixCooldown;

  /// <summary>Registry check + SoundVolumeView only — never opens DTS Sound Unbound.</summary>
  private async Task TryRunSilentSpatialRestoreAsync(CancellationToken ct, SpatialHealth spatial)
  {
    if (!await _fixGate.WaitAsync(0, ct)) return;
    try
    {
      if (spatial.IsWrongFormat)
        _log($"Spatial: was {spatial.ActiveGuid}, setting {_config.SpatialFormat} (SVV)...");
      else
        _log($"Spatial: enabling {_config.SpatialFormat} (SVV)...");

      var ok = _spatial.TryRestoreViaSoundVolumeView();
      if (ok)
      {
        _log("Spatial: OK (Windows / SoundVolumeView).");
      }
      else
      {
        _log("Spatial: SVV could not apply — use «Применить DTS для монитора» if needed.");
      }

      _lastHeadphonesCheck = DateTime.UtcNow;
      _lastSpatialAutoFix = DateTime.UtcNow;
      await Task.CompletedTask;
    }
    finally
    {
      _fixGate.Release();
    }
  }

  private async Task RunMonitorTransitionFixAsync(CancellationToken ct)
  {
    if (!await _fixGate.WaitAsync(0, ct)) return;
    try
    {
      _busy = true;
      var spatial = _spatial.Evaluate();
      var ok = _spatial.TryRestoreViaSoundVolumeView();
      if (!ok)
      {
        _log("Transition: SVV fix failed, trying device switch...");
        await RunMonitorFixAsync(ct, allowDtsApp: false);
      }
      else
      {
        _log("Transition: spatial OK on headphones (no device switch).");
      }

      _lastMonitorFix = DateTime.UtcNow;
      _lastHeadphonesCheck = DateTime.UtcNow;
      _lastSpatialAutoFix = DateTime.UtcNow;
    }
    finally
    {
      _busy = false;
      _fixGate.Release();
    }
  }

  private async Task RunMonitorFixAsync(CancellationToken ct, bool allowDtsApp)
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
        await Task.Delay(1500, ct);
      }

      var spatial = _spatial.Evaluate();
      var ok = _spatial.TryRestoreViaSoundVolumeView();

      if (!ok && allowDtsApp)
      {
        _log(_config.DtsAppRunHidden
          ? "Monitor fix: SVV failed, trying DTS Sound Unbound (hidden)..."
          : "Monitor fix: SVV failed, trying DTS Sound Unbound...");
        try
        {
          await _dts.TryActivateHeadphoneXAsync(ct);
        }
        catch (Exception ex)
        {
          _log($"DTS app warning: {ex.Message}");
          await _dts.CloseAndWaitAsync(ct);
        }

        ok = _spatial.TryRestoreViaSoundVolumeView();
      }

      if (!ok)
        _log("Monitor fix: could not confirm spatial sound.");

      await Task.Delay(500, ct);

      _log("Monitor fix: switching back to monitor...");
      _audio.SetDefault(mon.Id);
      await WaitForDefaultDeviceAsync(mon.Name, TimeSpan.FromSeconds(8), ct);

      _lastHeadphonesCheck = DateTime.UtcNow;
      _lastMonitorFix = DateTime.UtcNow;
      _lastSpatialAutoFix = DateTime.UtcNow;
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
