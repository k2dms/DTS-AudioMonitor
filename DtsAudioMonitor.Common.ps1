# Shared helpers for DTS Audio Monitor
#Requires -Version 5.1

function Get-DtsScriptRoot {
    if ($PSScriptRoot) { return $PSScriptRoot }
    return Split-Path -Parent $MyInvocation.MyCommand.Path
}

function Get-DtsConfig {
    $root = Get-DtsScriptRoot
    $path = Join-Path $root 'config.json'
    $defaults = [ordered]@{
        HeadphonesNameMatch       = 'Headphones*'
        MonitorNameMatch          = 'XV272U*'
        HeadphonesFriendlyId      = 'HyperX Cloud III\Device\Headphones\Render'
        MonitorFriendlyId         = 'NVIDIA High Definition Audio\Device\XV272U F3\Render'
        HeadphonesRegistryGuid    = '{e0cb2f31-49bb-444d-bf05-d086c762cc93}'
        SpatialFormat             = 'DTS Headphone:X'
        PollSeconds               = 3
        MonitorFixCooldownSeconds = 45
        HeadphonesCheckSeconds    = 300
        DtsAppRunHidden           = $true
        SpatialDisabledGuid       = '{00000000-0000-0000-0000-000000000000}'
    }
    if (Test-Path $path) {
        $file = Get-Content $path -Raw | ConvertFrom-Json
        foreach ($key in @($defaults.Keys)) {
            if ($null -ne $file.$key) { $defaults[$key] = $file.$key }
        }
    }
    return [pscustomobject]$defaults
}

function Get-DtsPaths {
    $root = Get-DtsScriptRoot
    [pscustomobject]@{
        Root      = $root
        Svv       = Join-Path $root 'SoundVolumeView\SoundVolumeView.exe'
        ModuleDll = Join-Path $env:USERPROFILE 'Documents\WindowsPowerShell\Modules\AudioDeviceCmdlets\AudioDeviceCmdlets.dll'
        LogFile   = Join-Path $root 'service.log'
        StateFile = Join-Path $root 'service-state.json'
    }
}

function Initialize-DtsDependencies {
    $paths = Get-DtsPaths
    if (-not (Test-Path $paths.ModuleDll)) {
        throw "AudioDeviceCmdlets missing. Run Install-DtsAudioTools.ps1"
    }
    if (-not (Test-Path $paths.Svv)) {
        throw "SoundVolumeView missing. Run Install-DtsAudioTools.ps1"
    }
    Import-Module $paths.ModuleDll -Force
    return $paths
}

function Write-DtsLog {
    param([string]$Message, [switch]$Quiet)
    $paths = Get-DtsPaths
    $line = "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') $Message"
    Add-Content -Path $paths.LogFile -Value $line -Encoding UTF8 -ErrorAction SilentlyContinue
    if (-not $Quiet) { Write-Host $line }
}

function Get-DtsState {
    $paths = Get-DtsPaths
    if (-not (Test-Path $paths.StateFile)) {
        return [pscustomobject]@{
            LastHeadphonesSpatialOk = $null
            LastMonitorFix          = $null
        }
    }
    return (Get-Content $paths.StateFile -Raw | ConvertFrom-Json)
}

function Set-DtsState {
    param($State)
    $paths = Get-DtsPaths
    $State | ConvertTo-Json | Set-Content -Path $paths.StateFile -Encoding UTF8
}

function Find-DtsByAutomationId {
    param([Windows.Automation.AutomationElement]$Root, [string]$Id)
    $cond = New-Object Windows.Automation.PropertyCondition(
        [Windows.Automation.AutomationElement]::AutomationIdProperty, $Id)
    return $Root.FindFirst([Windows.Automation.TreeScope]::Descendants, $cond)
}

function Invoke-DtsUiClick {
    param([Windows.Automation.AutomationElement]$Element)
    if (-not $Element) { return $false }

    $patterns = @(
        [Windows.Automation.InvokePattern]::Pattern,
        [Windows.Automation.SelectionItemPattern]::Pattern,
        [Windows.Automation.TogglePattern]::Pattern
    )
    foreach ($patternId in $patterns) {
        try {
            $pattern = $Element.GetCurrentPattern($patternId)
            if ($pattern -is [Windows.Automation.InvokePattern]) {
                $pattern.Invoke()
                return $true
            }
            if ($pattern -is [Windows.Automation.SelectionItemPattern]) {
                $pattern.Select()
                return $true
            }
            if ($pattern -is [Windows.Automation.TogglePattern]) {
                if ($pattern.Current.ToggleState -ne [Windows.Automation.ToggleState]::On) {
                    $pattern.Toggle()
                }
                return $true
            }
        } catch { }
    }
    return $false
}

function Hide-DtsAppWindow {
    param($Window)
    if (-not $Window) { return }
    try {
        Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class DtsWin32 {
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr ins, int x, int y, int cx, int cy, uint flags);
}
'@ -ErrorAction SilentlyContinue | Out-Null
        $hwnd = [IntPtr]$Window.Current.NativeWindowHandle
        if ($hwnd -ne [IntPtr]::Zero) {
            [void][DtsWin32]::SetWindowPos($hwnd, [IntPtr]::Zero, -32000, -32000, 0, 0, 0x0011)
            [void][DtsWin32]::ShowWindow($hwnd, 0)
            return
        }
    } catch { }
    try {
        $wp = $Window.GetCurrentPattern([Windows.Automation.WindowPattern]::Pattern)
        $wp.SetWindowVisualState([Windows.Automation.WindowVisualState]::Minimized)
    } catch { }
}

function Start-DtsSoundUnboundApp {
    param([switch]$Quiet)
    $config = Get-DtsConfig
    Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes | Out-Null

    Get-Process -Name 'DTSSoundUnbound*' -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 800
    Start-Process explorer.exe 'shell:AppsFolder\DTSInc.DTSSoundUnbound_t5j2fzbtdg37r!App'

    $window = $null
    for ($i = 0; $i -lt 40; $i++) {
        Start-Sleep -Milliseconds 400
        $root = [Windows.Automation.AutomationElement]::RootElement
        $cond = New-Object Windows.Automation.PropertyCondition(
            [Windows.Automation.AutomationElement]::NameProperty, 'DTS Sound Unbound')
        $window = $root.FindFirst([Windows.Automation.TreeScope]::Children, $cond)
        if ($window) { break }
    }
    if (-not $window) { throw 'DTS Sound Unbound window not found' }

    if ($config.DtsAppRunHidden) {
        Hide-DtsAppWindow $window
        Start-Sleep -Milliseconds 400
    }

    $clicked = $false
    for ($try = 0; $try -lt 25; $try++) {
        $hpx = Find-DtsByAutomationId $window 'HPXRadioButton'
        if ($hpx -and (Invoke-DtsUiClick $hpx)) {
            $clicked = $true
            break
        }
        Start-Sleep -Milliseconds 250
    }
    if (-not $clicked) { throw 'DTS Headphone:X control not found or not clickable' }
    Start-Sleep -Milliseconds 700

    $notLicensed = $window.FindAll([Windows.Automation.TreeScope]::Descendants,
        (New-Object Windows.Automation.PropertyCondition(
            [Windows.Automation.AutomationElement]::NameProperty, 'Not licensed')))
    if ($notLicensed.Count -gt 0) {
        $tryBtn = Find-DtsByAutomationId $window 'm_tryButton'
        if ($tryBtn) {
            Write-DtsLog 'DTS app: activating trial/license...' -Quiet:$Quiet
            Invoke-DtsUiClick $tryBtn | Out-Null
            Start-Sleep -Seconds 3
        }
    }

    Wait-DtsAppClosed
}

function Wait-DtsAppClosed {
    Get-Process -Name 'DTSSoundUnbound*' -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    for ($i = 0; $i -lt 50; $i++) {
        if (-not (Get-Process -Name 'DTSSoundUnbound*' -ErrorAction SilentlyContinue)) {
            Start-Sleep -Milliseconds 800
            return
        }
        Start-Sleep -Milliseconds 200
    }
}

function Set-DtsSpatialSound {
    param(
        [string]$DeviceFriendlyId,
        [string]$Format
    )
    $paths = Get-DtsPaths
    $p = Start-Process -FilePath $paths.Svv -ArgumentList @('/SetSpatial', $DeviceFriendlyId, $Format) -Wait -PassThru -NoNewWindow
    if ($p.ExitCode -ne 0) {
        throw "SetSpatial failed (exit $($p.ExitCode)) for $DeviceFriendlyId"
    }
}

function Test-DtsSpatialOnRegistry {
    param(
        [string]$RegistryGuid,
        [string]$DisabledGuid = '{00000000-0000-0000-0000-000000000000}'
    )
    $guid = $RegistryGuid.Trim('{}').ToLower()
    $path = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Render\{$guid}\Properties"
    if (-not (Test-Path $path)) { return $false }

    $props = Get-ItemProperty $path
    $candidates = @(
        '{6597f250-c913-4f95-8072-9c59a52b6552},3',
        '{6597f250-c913-4f95-8072-9c59a52b6552},2',
        '{f19f064d-082c-4e27-bc73-6882a1bb8e4c},2',
        '{1da5d803-d492-4edd-8c23-e0c0ffee7f0e},8'
    )
    foreach ($name in $candidates) {
        if (-not $props.PSObject.Properties[$name]) { continue }
        $val = [string]$props.$name
        if ($val -and $val -ne $DisabledGuid -and $val -match '^\{[0-9A-Fa-f-]{36}\}$') {
            return $true
        }
    }
    return $false
}

function Test-DtsSpatialRecentlyOk {
    param([int]$WithinSeconds = 300)
    $state = Get-DtsState
    if (-not $state.LastHeadphonesSpatialOk) { return $false }
    $last = [datetime]$state.LastHeadphonesSpatialOk
    return ((Get-Date) - $last).TotalSeconds -lt $WithinSeconds
}

function Get-DtsPlaybackDevice {
    return Get-AudioDevice -Playback
}

function Get-DtsDeviceByMatch {
    param([string]$NameMatch)
    Get-AudioDevice -List | Where-Object {
        $_.Type -eq 'Playback' -and $_.Name -like $NameMatch
    } | Select-Object -First 1
}

function Enable-DtsSpatialOnHeadphones {
    param(
        [switch]$UseDtsApp,
        [switch]$Quiet
    )
    $config = Get-DtsConfig
    Write-DtsLog "Headphones: ensure $($config.SpatialFormat)..." -Quiet:$Quiet

    if ($UseDtsApp) {
        Start-DtsSoundUnboundApp -Quiet:$Quiet
    }

    Set-DtsSpatialSound -DeviceFriendlyId $config.HeadphonesFriendlyId -Format $config.SpatialFormat
    Start-Sleep -Milliseconds 500

    $ok = Test-DtsSpatialOnRegistry -RegistryGuid $config.HeadphonesRegistryGuid -DisabledGuid $config.SpatialDisabledGuid
    $state = Get-DtsState
    $state.LastHeadphonesSpatialOk = (Get-Date).ToString('o')
    Set-DtsState $state

    if (-not $ok) {
        Write-DtsLog 'Headphones: spatial set sent (registry probe inconclusive).' -Quiet:$Quiet
    } else {
        Write-DtsLog 'Headphones: spatial sound OK.' -Quiet:$Quiet
    }
}

function Enable-DtsMonitorPlaybackFix {
    param([switch]$Quiet)
    $config = Get-DtsConfig
    $paths = Get-DtsPaths

    $hp = Get-DtsDeviceByMatch -NameMatch $config.HeadphonesNameMatch
    $mon = Get-DtsDeviceByMatch -NameMatch $config.MonitorNameMatch
    if (-not $hp) { throw 'Headphones device not found' }
    if (-not $mon) { throw 'Monitor device not found' }

    Write-DtsLog "Monitor fix: switch to Headphones ($($hp.Index))..." -Quiet:$Quiet
    Set-AudioDevice -Index $hp.Index | Out-Null
    Start-Sleep -Seconds 3

    Write-DtsLog 'Monitor fix: DTS Sound Unbound...' -Quiet:$Quiet
    Start-DtsSoundUnboundApp -Quiet:$Quiet

    Write-DtsLog "Monitor fix: enable $($config.SpatialFormat)..." -Quiet:$Quiet
    Set-DtsSpatialSound -DeviceFriendlyId $config.HeadphonesFriendlyId -Format $config.SpatialFormat
    Start-Sleep -Seconds 1

    Wait-DtsAppClosed
    Write-DtsLog "Monitor fix: DTS closed — switch back to monitor ($($mon.Index))..." -Quiet:$Quiet
    Set-AudioDevice -Index $mon.Index | Out-Null

    $state = Get-DtsState
    $state.LastHeadphonesSpatialOk = (Get-Date).ToString('o')
    $state.LastMonitorFix = (Get-Date).ToString('o')
    Set-DtsState $state

    Write-DtsLog 'Monitor fix: done.' -Quiet:$Quiet
}

function Test-DtsMonitorFixCooldown {
    param([int]$Seconds)
    $state = Get-DtsState
    if (-not $state.LastMonitorFix) { return $false }
    $last = [datetime]$state.LastMonitorFix
    return ((Get-Date) - $last).TotalSeconds -lt $Seconds
}
