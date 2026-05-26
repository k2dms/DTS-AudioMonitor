#Requires -Version 5.1
<#
.SYNOPSIS
  Включает DTS Headphone:X на наушниках и возвращает вывод на монитор XV272U F3.

.DESCRIPTION
  1) Переключает воспроизведение на Headphones
  2) Запускает DTS Sound Unbound (активация лицензии/пробной версии при необходимости)
  3) Включает пространственный звук DTS Headphone:X
  4) Переключает воспроизведение обратно на XV272U F3
#>
[CmdletBinding()]
param(
    [int]$HeadphonesIndex = 0,
    [int]$MonitorIndex = 0,
    [string]$SpatialFormat = 'DTS Headphone:X',
    [switch]$SkipDtsApp,
    [switch]$Quiet
)

$ErrorActionPreference = 'Stop'
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$svv = Join-Path $scriptDir 'SoundVolumeView\SoundVolumeView.exe'
$moduleDll = Join-Path $env:USERPROFILE 'Documents\WindowsPowerShell\Modules\AudioDeviceCmdlets\AudioDeviceCmdlets.dll'

if (-not (Test-Path $moduleDll)) {
    throw "AudioDeviceCmdlets not found. Run: $scriptDir\Install-DtsAudioTools.ps1"
}
if (-not (Test-Path $svv)) {
    throw "SoundVolumeView not found. Run: $scriptDir\Install-DtsAudioTools.ps1"
}

Import-Module $moduleDll

function Write-Log([string]$Message) {
    if (-not $Quiet) { Write-Host $Message }
}

function Find-ById([Windows.Automation.AutomationElement]$Root, [string]$Id) {
    $cond = New-Object Windows.Automation.PropertyCondition(
        [Windows.Automation.AutomationElement]::AutomationIdProperty, $Id)
    return $Root.FindFirst([Windows.Automation.TreeScope]::Descendants, $cond)
}

function Invoke-UiClick([Windows.Automation.AutomationElement]$Element) {
    if (-not $Element) { return $false }
    try {
        $invoke = $Element.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern)
        $invoke.Invoke()
        return $true
    } catch {
        return $false
    }
}

function Start-DtsSoundUnbound {
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

    Invoke-UiClick (Find-ById $window 'HPXRadioButton') | Out-Null
    Start-Sleep -Milliseconds 700

    $notLicensed = $window.FindAll([Windows.Automation.TreeScope]::Descendants,
        (New-Object Windows.Automation.PropertyCondition(
            [Windows.Automation.AutomationElement]::NameProperty, 'Not licensed')))
    if ($notLicensed.Count -gt 0) {
        $tryBtn = Find-ById $window 'm_tryButton'
        if ($tryBtn) {
            Write-Log 'DTS: activating trial Headphone:X...'
            Invoke-UiClick $tryBtn | Out-Null
            Start-Sleep -Seconds 3
        }
    }

    Get-Process -Name 'DTSSoundUnbound*' -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
}

function Set-SpatialSound([string]$DeviceFriendlyId, [string]$Format) {
    $p = Start-Process -FilePath $svv -ArgumentList @('/SetSpatial', $DeviceFriendlyId, $Format) -Wait -PassThru -NoNewWindow
    if ($p.ExitCode -ne 0) {
        throw "SoundVolumeView /SetSpatial failed with exit code $($p.ExitCode)"
    }
}

$devices = Get-AudioDevice -List | Where-Object { $_.Type -eq 'Playback' }
if ($HeadphonesIndex -le 0) {
    $hp = $devices | Where-Object { $_.Name -like 'Headphones*' } | Select-Object -First 1
    if (-not $hp) { throw 'Headphones device not found' }
    $HeadphonesIndex = $hp.Index
}
if ($MonitorIndex -le 0) {
    $mon = $devices | Where-Object { $_.Name -like 'XV272U*' } | Select-Object -First 1
    if (-not $mon) { throw 'XV272U F3 device not found' }
    $MonitorIndex = $mon.Index
}

$hpFriendly = 'HyperX Cloud III\Device\Headphones\Render'
$monFriendly = 'NVIDIA High Definition Audio\Device\XV272U F3\Render'

Write-Log "Step 1/4: switch to Headphones (index $HeadphonesIndex)..."
Set-AudioDevice -Index $HeadphonesIndex | Out-Null
Start-Sleep -Seconds 2

if (-not $SkipDtsApp) {
    Write-Log 'Step 2/4: launch DTS Sound Unbound...'
    Start-DtsSoundUnbound
} else {
    Write-Log 'Step 2/4: skip DTS Sound Unbound (-SkipDtsApp)'
}

Write-Log "Step 3/4: enable $SpatialFormat..."
Set-SpatialSound $hpFriendly $SpatialFormat
Start-Sleep -Seconds 1

Write-Log "Step 4/4: switch to XV272U F3 (index $MonitorIndex)..."
Set-AudioDevice -Index $MonitorIndex | Out-Null

Write-Log 'Gotovo: DTS Headphone:X vklyuchen, vyvod na monitor XV272U F3.'
