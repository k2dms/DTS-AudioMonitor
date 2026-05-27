using System.IO;
using System.Text.Json;

namespace DtsAudioMonitor.Models;

/// <summary>Persists the spatial GUID observed after a successful DTS fix on this PC.</summary>
public sealed class SpatialStateStore
{
  private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

  public string? GoldenSpatialGuid { get; set; }

  public static SpatialStateStore Load(string path)
  {
    if (!File.Exists(path)) return new SpatialStateStore();
    try
    {
      var json = File.ReadAllText(path);
      return JsonSerializer.Deserialize<SpatialStateStore>(json, JsonOptions) ?? new SpatialStateStore();
    }
    catch
    {
      return new SpatialStateStore();
    }
  }

  public void Save(string path)
  {
    var dir = Path.GetDirectoryName(path);
    if (!string.IsNullOrEmpty(dir))
      Directory.CreateDirectory(dir);
    File.WriteAllText(path, JsonSerializer.Serialize(this, JsonOptions));
  }
}
