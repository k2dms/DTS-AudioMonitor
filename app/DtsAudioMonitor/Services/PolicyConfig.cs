using System.Runtime.InteropServices;

namespace DtsAudioMonitor.Services;

internal enum ERole
{
    Console = 0,
    Multimedia = 1,
    Communications = 2
}

[ComImport]
[Guid("870af99c-171d-4f9e-af0d-e63df40c2bc9")]
internal class PolicyConfigClient;

[ComImport]
[Guid("F8679F50-850A-41CF-9C72-430F290290C8")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IPolicyConfig
{
    void Reserved1();
    void Reserved2();
    void Reserved3();
    void Reserved4();
    void Reserved5();
    void Reserved6();
    void Reserved7();
    void Reserved8();
    void Reserved9();
    void Reserved10();
    [PreserveSig]
    int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string deviceId, ERole role);
}

internal static class PolicyConfig
{
    public static void SetDefaultDevice(string deviceId)
    {
        var policy = (IPolicyConfig)new PolicyConfigClient();
        policy.SetDefaultEndpoint(deviceId, ERole.Console);
        policy.SetDefaultEndpoint(deviceId, ERole.Multimedia);
        policy.SetDefaultEndpoint(deviceId, ERole.Communications);
    }
}
