using Microsoft.Win32;

namespace DtsAudioMonitor.Services;

internal static class SpatialRegistryReader
{
  private static readonly string[] SpatialValueNames =
  {
    @"{6597f250-c913-4f95-8072-9c59a52b6552},3",
    @"{6597f250-c913-4f95-8072-9c59a52b6552},2",
    @"{f8d2c69d-0989-4cc6-b197-a9e152f3b5d3},3",
    @"{f19f064d-082c-4e27-bc73-6882a1bb8e4c},2"
  };

  /// <summary>GUIDs that are not DTS Headphone:X (other spatial engines).</summary>
  private static readonly HashSet<string> KnownOtherSpatialGuids = new(StringComparer.OrdinalIgnoreCase)
  {
    "{00000000-0000-0000-0000-000000000000}",
    "{b53d940c-b846-4831-9f76-d102b9b725a0}", // Windows Sonic For Headphones
    "{b53b4c27-1c42-4148-8533-507b42b6a0f7}", // Windows Sonic (alt)
    "{2718ab58-91b0-4aaf-b264-508f8a8fd87c}", // Dolby Atmos for Headphones
    "{DFF21CE2-F70F-11D0-B917-00A0C9223196}" // default stereo — not a spatial renderer
  };

  public static string? ReadActiveSpatialGuid(string headphonesRegistryGuid)
  {
    var guid = headphonesRegistryGuid.Trim('{', '}').ToLowerInvariant();
    var basePath = $@"SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Render\{{{guid}}}";

    foreach (var subKey in new[] { "Properties", "FxProperties" })
    {
      using var key = Registry.LocalMachine.OpenSubKey($@"{basePath}\{subKey}");
      if (key is null) continue;

      foreach (var valueName in SpatialValueNames)
      {
        var val = key.GetValue(valueName) as string;
        if (IsGuid(val))
          return NormalizeGuid(val!);
      }
    }

    return null;
  }

  public static bool IsKnownOtherSpatial(string guid) =>
    KnownOtherSpatialGuids.Contains(NormalizeGuid(guid));

  private static bool IsGuid(string? value) =>
    !string.IsNullOrWhiteSpace(value)
    && value.StartsWith('{')
    && value.EndsWith('}')
    && value.Length == 38;

  private static string NormalizeGuid(string guid) => guid.Trim().ToUpperInvariant();
}
