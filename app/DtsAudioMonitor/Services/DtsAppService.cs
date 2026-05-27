using System.Diagnostics;
using System.Windows.Automation;

namespace DtsAudioMonitor.Services;

public sealed class DtsAppService
{
    private const string AppUri = "shell:AppsFolder\\DTSInc.DTSSoundUnbound_t5j2fzbtdg37r!App";
    private const string WindowName = "DTS Sound Unbound";
    private const string ProcessName = "DTSSoundUnbound";

    private readonly bool _runHidden;

    public DtsAppService(bool runHidden = true) => _runHidden = runHidden;

    public async Task<bool> TryActivateHeadphoneXAsync(CancellationToken ct)
    {
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                if (await ActivateOnceAsync(ct))
                    return true;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                if (attempt == 3)
                    throw;
            }

            await Task.Delay(1200, ct);
        }

        return false;
    }

    public async Task CloseAndWaitAsync(CancellationToken ct)
    {
        TryCloseWindowGracefully();
        KillDtsProcesses();

        for (var i = 0; i < 50; i++)
        {
            ct.ThrowIfCancellationRequested();
            if (Process.GetProcessesByName(ProcessName).Length == 0)
            {
                await Task.Delay(800, ct);
                return;
            }
            await Task.Delay(200, ct);
        }

        KillDtsProcesses();
        await Task.Delay(600, ct);
    }

    private async Task<bool> ActivateOnceAsync(CancellationToken ct)
    {
        await CloseAndWaitAsync(ct);

        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = AppUri,
            UseShellExecute = true
        });

        AutomationElement? window = null;
        for (var i = 0; i < 50; i++)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Delay(350, ct);
            window = AutomationElement.RootElement.FindFirst(
                TreeScope.Children,
                new PropertyCondition(AutomationElement.NameProperty, WindowName));
            if (window is not null)
                break;
        }

        if (window is null)
            throw new InvalidOperationException("DTS Sound Unbound window not found");

        if (_runHidden)
            DtsWindowHelper.HideFromView(window);

        await Task.Delay(_runHidden ? 400 : 600, ct);

        if (!await TrySelectHeadphoneXAsync(window, ct))
            throw new InvalidOperationException("DTS Headphone:X control not found or not clickable");

        await Task.Delay(500, ct);

        var notLicensed = window.FindAll(
            TreeScope.Descendants,
            new PropertyCondition(AutomationElement.NameProperty, "Not licensed"));

        if (notLicensed.Count > 0)
        {
            TryClick(FindById(window, "m_tryButton"));
            await Task.Delay(2800, ct);
        }

        await CloseAndWaitAsync(ct);
        return true;
    }

    private static void TryCloseWindowGracefully()
    {
        try
        {
            var window = AutomationElement.RootElement.FindFirst(
                TreeScope.Children,
                new PropertyCondition(AutomationElement.NameProperty, WindowName));
            if (window is null) return;

            if (window.TryGetCurrentPattern(WindowPattern.Pattern, out var p) && p is WindowPattern wp)
            {
                wp.Close();
                return;
            }

            var closeBtn = window.FindFirst(
                TreeScope.Descendants,
                new AndCondition(
                    new PropertyCondition(AutomationElement.NameProperty, "Close"),
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button)));

            TryClick(closeBtn);
        }
        catch
        {
            // Fall back to process kill
        }
    }

    private static void KillDtsProcesses()
    {
        foreach (var p in Process.GetProcessesByName(ProcessName))
        {
            try { p.Kill(true); } catch { /* ignore */ }
        }
    }

    private static async Task<bool> TrySelectHeadphoneXAsync(AutomationElement window, CancellationToken ct)
    {
        for (var i = 0; i < 25; i++)
        {
            ct.ThrowIfCancellationRequested();

            var hpx = FindById(window, "HPXRadioButton");
            if (hpx is not null && TryClick(hpx))
                return true;

            var byName = window.FindFirst(
                TreeScope.Descendants,
                new OrCondition(
                    new PropertyCondition(AutomationElement.NameProperty, "DTS Headphone:X"),
                    new PropertyCondition(AutomationElement.NameProperty, "DTS Headphone :X")));

            if (byName is not null && TryClick(byName))
                return true;

            await Task.Delay(250, ct);
        }

        return false;
    }

    private static AutomationElement? FindById(AutomationElement root, string id) =>
        root.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, id));

    private static bool TryClick(AutomationElement? element)
    {
        if (element is null) return false;

        try
        {
            if (element.TryGetCurrentPattern(InvokePattern.Pattern, out var p) && p is InvokePattern invoke)
            {
                invoke.Invoke();
                return true;
            }
        }
        catch { /* next */ }

        try
        {
            if (element.TryGetCurrentPattern(SelectionItemPattern.Pattern, out var p) && p is SelectionItemPattern sel)
            {
                sel.Select();
                return true;
            }
        }
        catch { /* next */ }

        try
        {
            if (element.TryGetCurrentPattern(TogglePattern.Pattern, out var p) && p is TogglePattern toggle)
            {
                if (toggle.Current.ToggleState != ToggleState.On)
                    toggle.Toggle();
                return true;
            }
        }
        catch { /* next */ }

        try
        {
            if (element.TryGetCurrentPattern(ExpandCollapsePattern.Pattern, out var p) && p is ExpandCollapsePattern exp)
            {
                if (exp.Current.ExpandCollapseState == ExpandCollapseState.Collapsed)
                    exp.Expand();
                return true;
            }
        }
        catch { /* ignore */ }

        return false;
    }
}
