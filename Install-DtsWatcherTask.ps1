#Requires -RunAsAdministrator
# Создаёт задачу планировщика: watcher при входе в Windows
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$watchScript = Join-Path $scriptDir 'Watch-DtsAudioDevice.ps1'
$taskName = 'DTS-AudioMonitor-Watcher'

$action = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument @(
    '-NoProfile', '-WindowStyle', 'Hidden', '-ExecutionPolicy', 'Bypass',
    '-File', "`"$watchScript`"", '-Quiet'
)
$trigger = New-ScheduledTaskTrigger -AtLogOn -User $env:USERNAME
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -StartWhenAvailable

Unregister-ScheduledTask -TaskName $taskName -Confirm:$false -ErrorAction SilentlyContinue
Register-ScheduledTask -TaskName $taskName -Action $action -Trigger $trigger -Settings $settings -Description 'Auto DTS when switching to XV272U F3' | Out-Null

Write-Host "Task '$taskName' created. Starting..."
Start-ScheduledTask -TaskName $taskName
Write-Host 'Watcher started in background.'
