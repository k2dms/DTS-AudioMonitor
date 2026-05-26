$taskName = 'DTS-AudioMonitor'
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$log = Join-Path $scriptDir 'service.log'
$lock = Join-Path $env:TEMP 'DtsAudioMonitor.Service.lock'

$task = Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue
if ($task) {
    $info = Get-ScheduledTaskInfo -TaskName $taskName
    Write-Host "Task: $taskName"
    Write-Host "  State: $($task.State)"
    Write-Host "  Last run: $($info.LastRunTime)"
    Write-Host "  Last result: $($info.LastTaskResult)"
} else {
    Write-Host "Task $taskName is not installed."
}

if (Test-Path $lock) {
    $pidText = Get-Content $lock -Raw
    $running = Get-Process -Id $pidText -ErrorAction SilentlyContinue
    Write-Host "Worker PID $pidText : $(if ($running) { 'running' } else { 'stale lock' })"
} else {
    Write-Host 'Worker lock: not present'
}

if (Test-Path $log) {
    Write-Host '--- last log lines ---'
    Get-Content $log -Tail 8
}
