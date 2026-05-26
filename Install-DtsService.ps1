#Requires -Version 5.1
<#
.SYNOPSIS
  Installs DTS Audio Monitor as a persistent background task (user session).

  Windows cannot run DTS UI in Session 0; this task starts at logon and restarts
  on failure, behaving like a service for the logged-in user.
#>
#Requires -RunAsAdministrator

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$worker = Join-Path $scriptDir 'DtsAudioMonitor.Service.ps1'
$taskName = 'DTS-AudioMonitor'
$legacyTask = 'DTS-AudioMonitor-Watcher'

$installTools = Join-Path $scriptDir 'Install-DtsAudioTools.ps1'
if (Test-Path $installTools) {
    & $installTools
}

$argument = "-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File `"$worker`" -Quiet"
$action = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument $argument

$trigger = New-ScheduledTaskTrigger -AtLogOn -User $env:USERNAME

$settings = New-ScheduledTaskSettingsSet `
    -AllowStartIfOnBatteries `
    -DontStopIfGoingOnBatteries `
    -StartWhenAvailable `
    -RestartCount 999 `
    -RestartInterval (New-TimeSpan -Minutes 1) `
    -ExecutionTimeLimit ([TimeSpan]::Zero)

$principal = New-ScheduledTaskPrincipal -UserId $env:USERNAME -LogonType Interactive -RunLevel Limited

foreach ($old in @($legacyTask, $taskName)) {
    Unregister-ScheduledTask -TaskName $old -Confirm:$false -ErrorAction SilentlyContinue
}

Register-ScheduledTask `
    -TaskName $taskName `
    -Action $action `
    -Trigger $trigger `
    -Settings $settings `
    -Principal $principal `
    -Description 'DTS Audio Monitor: spatial on headphones, auto-fix when switching to XV272U F3' | Out-Null

Start-ScheduledTask -TaskName $taskName

Write-Host "Installed and started task: $taskName"
Write-Host "Log: $scriptDir\service.log"
Write-Host "Stop:  Stop-DtsService.ps1"
Write-Host "Status: Get-DtsServiceStatus.ps1"
