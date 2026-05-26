using System.Diagnostics;
using System.Windows.Automation;

namespace DtsAudioMonitor.Services;

public sealed class DtsAppService
{
    private const string AppUri = "shell:AppsFolder\\DTSInc.DTSSoundUnbound_t5j2fzbtdg37r!App";

    public async Task ActivateHeadphoneXAsync(CancellationToken ct)
    {
        foreach (var p in Process.GetProcessesByName("DTSSoundUnbound"))
            p.Kill(true);

        await Task.Delay(500, ct);

        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = AppUri,
            UseShellExecute = true
        });

        AutomationElement? window = null;
        for (var i = 0; i < 40 && window is null; i++)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Delay(400, ct);
            window = AutomationElement.RootElement.FindFirst(
                TreeScope.Children,
                new PropertyCondition(AutomationElement.NameProperty, "DTS Sound Unbound"));
        }

        if (window is null)
            throw new InvalidOperationException("DTS Sound Unbound window not found");

        ClickById(window, "HPXRadioButton");
        await Task.Delay(500, ct);

        var notLicensed = window.FindAll(
            TreeScope.Descendants,
            new PropertyCondition(AutomationElement.NameProperty, "Not licensed"));

        if (notLicensed.Count > 0)
        {
            ClickById(window, "m_tryButton");
            await Task.Delay(2500, ct);
        }

        foreach (var p in Process.GetProcessesByName("DTSSoundUnbound"))
        {
            try { p.Kill(true); } catch { /* ignore */ }
        }
    }

    private static void ClickById(AutomationElement root, string id)
    {
        var el = root.FindFirst(
            TreeScope.Descendants,
            new PropertyCondition(AutomationElement.AutomationIdProperty, id));
        if (el?.GetCurrentPattern(InvokePattern.Pattern) is InvokePattern invoke)
            invoke.Invoke();
    }
}
