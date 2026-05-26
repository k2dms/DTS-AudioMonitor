$taskName = 'DTS-AudioMonitor'
Stop-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue
$lock = Join-Path $env:TEMP 'DtsAudioMonitor.Service.lock'
if (Test-Path $lock) {
    $workerPid = Get-Content $lock -Raw
    if ($workerPid -match '^\d+$') {
        Stop-Process -Id ([int]$workerPid) -Force -ErrorAction SilentlyContinue
    }
    Remove-Item $lock -Force -ErrorAction SilentlyContinue
}
Write-Host "Stopped $taskName worker"
