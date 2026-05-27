#Requires -Version 5.1
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path

$programs = Join-Path ([Environment]::GetFolderPath('Programs')) 'DTS Audio Monitor'
$lnk = Join-Path $programs 'DTS Audio Monitor.lnk'
$startup = Join-Path ([Environment]::GetFolderPath('Startup')) 'DTS Audio Monitor.lnk'

foreach ($path in @($lnk, $programs, $startup)) {
    if (Test-Path $path) {
        Remove-Item -LiteralPath $path -Recurse -Force -ErrorAction SilentlyContinue
        Write-Host "Removed: $path"
    }
}

Remove-Item 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\k2dms.DtsAudioMonitor' -Recurse -Force -ErrorAction SilentlyContinue
Write-Host 'Removed Apps & Features entry.'

Get-Process -Name DtsAudioMonitor -ErrorAction SilentlyContinue | Stop-Process -Force
Write-Host 'Done. You can delete the folder manually if needed:'
Write-Host "  $root"
