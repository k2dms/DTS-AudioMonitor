namespace DtsAudioMonitor.Services;

internal static class DevicePattern
{
  public static bool IsMatch(string deviceName, string pattern)
  {
    if (pattern.EndsWith('*'))
      return deviceName.StartsWith(pattern.TrimEnd('*'), StringComparison.OrdinalIgnoreCase);
    return deviceName.Equals(pattern, StringComparison.OrdinalIgnoreCase);
  }

  public static string GetKind(string deviceName)
  {
    if (deviceName.Contains("Headphone", StringComparison.OrdinalIgnoreCase)) return "headphones";
    if (deviceName.Contains("XV272U", StringComparison.OrdinalIgnoreCase)) return "monitor";
    return "other";
  }
}
