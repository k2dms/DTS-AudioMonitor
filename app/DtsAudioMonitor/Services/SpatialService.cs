using System.Diagnostics;
using System.IO;
using DtsAudioMonitor.Models;

namespace DtsAudioMonitor.Services;

public sealed class SpatialService
{
  private static readonly string[] DefaultDtsSpatialGuids =
  {
    "{821C6636-896A-4C74-9B31-35474D8937DB}",
    "{0C8F181A-19D0-4A6A-864C-05275560CD9C}",
    "{BCACBE40-827A-466B-8129-EBAF9BF6F21C}",
    "{C3DA67EA-9195-4F81-B463-C0210CBE8FFB}"
  };

  private readonly string _svvPath;
  private readonly AppConfig _config;
  private readonly string _statePath;
  private SpatialStateStore _store;
  private DateTime _assumeOkUntil = DateTime.MinValue;

  public SpatialService(string svvPath, AppConfig config, string? statePath = null)
  {
    _svvPath = svvPath;
    _config = config;
    _statePath = statePath ?? Path.Combine(AppContext.BaseDirectory, "spatial-state.json");
    _store = SpatialStateStore.Load(_statePath);
  }

  /// <summary>Registry-only check — never launches DTS Sound Unbound.</summary>
  public SpatialHealth Evaluate()
  {
    if (DateTime.UtcNow < _assumeOkUntil)
      return new SpatialHealth(SpatialState.CorrectDts, _store.GoldenSpatialGuid);

    var active = SpatialRegistryReader.ReadActiveSpatialGuid(_config.HeadphonesRegistryGuid);
    if (active is null)
      return new SpatialHealth(SpatialState.Disabled, null);

    if (SpatialRegistryReader.IsKnownOtherSpatial(active))
      return new SpatialHealth(SpatialState.WrongFormat, active);

    // Spatial on and not Sonic/Dolby — treat as OK (DTS or unknown GUID)
    return new SpatialHealth(SpatialState.CorrectDts, active);
  }

  /// <summary>Sets spatial via SoundVolumeView only (silent). Returns true if registry looks OK or SVV succeeded.</summary>
  public bool TryRestoreViaSoundVolumeView()
  {
    if (!File.Exists(_svvPath))
      return false;

    var deviceId = ResolveHeadphonesFriendlyId();

    foreach (var format in GetSpatialFormatCandidates())
    {
      try
      {
        RunSetSpatial(deviceId, format);
        Thread.Sleep(450);
        RememberGoldenGuid();

        var health = Evaluate();
        if (!health.NeedsFix)
          return true;

        // Registry may lag — trust SVV for a short window
        _assumeOkUntil = DateTime.UtcNow.AddSeconds(90);
        return true;
      }
      catch
      {
        // try next format name
      }
    }

    return false;
  }

  public void RememberGoldenGuid()
  {
    var active = SpatialRegistryReader.ReadActiveSpatialGuid(_config.HeadphonesRegistryGuid);
    if (active is null) return;
    _store.GoldenSpatialGuid = active;
    _store.Save(_statePath);
  }

  private bool IsAcceptedDtsGuid(string active)
  {
    if (!string.IsNullOrEmpty(_store.GoldenSpatialGuid)
        && string.Equals(_store.GoldenSpatialGuid, active, StringComparison.OrdinalIgnoreCase))
      return true;

    if (_config.SpatialFormatGuids is { Count: > 0 })
    {
      return _config.SpatialFormatGuids.Any(g =>
        string.Equals(g.Trim(), active, StringComparison.OrdinalIgnoreCase));
    }

    return DefaultDtsSpatialGuids.Any(g =>
      string.Equals(g, active, StringComparison.OrdinalIgnoreCase));
  }

  private string ResolveHeadphonesFriendlyId()
  {
    var configured = _config.HeadphonesFriendlyId.Trim();
    if (!string.IsNullOrEmpty(configured))
      return configured;

    var hp = new AudioService().FindByNamePattern(_config.HeadphonesNameMatch)
               ?? throw new InvalidOperationException("Headphones device not found");
    return hp.Name + @"\Device\Headphones\Render";
  }

  private IReadOnlyList<string> GetSpatialFormatCandidates()
  {
    var list = new List<string>();
    if (!string.IsNullOrWhiteSpace(_config.SpatialFormat))
      list.Add(_config.SpatialFormat.Trim());

    list.AddRange(new[] { "DTS Headphone:X", "DTS Headphone", "Headphone:X" });
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
}
