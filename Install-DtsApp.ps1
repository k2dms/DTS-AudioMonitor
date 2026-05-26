#Requires -Version 5.1
# Build and add shortcut to Startup
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
& (Join-Path $root 'Build-App.ps1')

$exe = Join-Path $root 'publish\DtsAudioMonitor.exe'
if (-not (Test-Path $exe)) { throw "Build failed: $exe" }

# Stop legacy scheduled task
Unregister-ScheduledTask -TaskName 'DTS-AudioMonitor' -Confirm:$false -ErrorAction SilentlyContinue
Unregister-ScheduledTask -TaskName 'DTS-AudioMonitor-Watcher' -Confirm:$false -ErrorAction SilentlyContinue

$startup = [Environment]::GetFolderPath('Startup')
$lnk = Join-Path $startup 'DTS Audio Monitor.lnk'
$shell = New-Object -ComObject WScript.Shell
$sc = $shell.CreateShortcut($lnk)
$sc.TargetPath = $exe
$sc.Arguments = '--minimized'
$sc.WorkingDirectory = Split-Path $exe
$sc.Description = 'DTS Audio Monitor'
$sc.Save()

Write-Host "Installed startup shortcut: $lnk"
Write-Host "Starting app..."
Start-Process $exe -ArgumentList '--minimized'
