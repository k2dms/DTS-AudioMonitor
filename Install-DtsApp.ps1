#Requires -Version 5.1
# Build, register in Start menu / Apps list, optional autostart
param(
    [switch]$NoStartup,
    [switch]$NoStartMenu
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
. (Join-Path $root 'Install-DtsShell.ps1')

& (Join-Path $root 'Build-App.ps1')

$exe = Join-Path $root 'publish\DtsAudioMonitor.exe'
if (-not (Test-Path $exe)) { throw "Build failed: $exe" }

$version = '1.1.5'
try {
    $v = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($exe).FileVersion
    if ($v) { $version = ($v -split '\.')[0..2] -join '.' }
} catch { }

Copy-Item (Join-Path $root 'Uninstall-DtsApp.ps1') (Join-Path $root 'publish\Uninstall-DtsApp.ps1') -Force

Unregister-ScheduledTask -TaskName 'DTS-AudioMonitor' -Confirm:$false -ErrorAction SilentlyContinue
Unregister-ScheduledTask -TaskName 'DTS-AudioMonitor-Watcher' -Confirm:$false -ErrorAction SilentlyContinue

if (-not $NoStartMenu) {
    Install-DtsStartMenu -ExePath $exe -Version $version
}

if (-not $NoStartup) {
    $startup = [Environment]::GetFolderPath('Startup')
    $lnk = Join-Path $startup 'DTS Audio Monitor.lnk'
    $icon = Join-Path (Split-Path $exe) 'Assets\app.ico'
    if (-not (Test-Path $icon)) { $icon = $exe }
    New-DtsShellShortcut -ShortcutPath $lnk -TargetExe $exe -Arguments '--minimized' -IconPath $icon
    Write-Host "Autostart: $lnk"
}

Get-Process -Name DtsAudioMonitor -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 800
Write-Host 'Starting app...'
Start-Process $exe -ArgumentList '--minimized'
