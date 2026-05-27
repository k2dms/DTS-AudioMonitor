namespace DtsAudioMonitor.Models;

public enum SpatialState
{
  /// <summary>Spatial sound off or not configured on headphones endpoint.</summary>
  Disabled,
  /// <summary>DTS Headphone:X (or saved golden GUID) is active.</summary>
  CorrectDts,
  /// <summary>Another spatial engine is active (e.g. Windows Sonic, Dolby).</summary>
  WrongFormat
}

public readonly record struct SpatialHealth(SpatialState State, string? ActiveGuid)
{
  public bool NeedsFix => State != SpatialState.CorrectDts;
  public bool IsWrongFormat => State == SpatialState.WrongFormat;
  public bool IsDisabled => State == SpatialState.Disabled;
}
