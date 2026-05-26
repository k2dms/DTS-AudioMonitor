$taskName = 'DTS-AudioMonitor'
Start-ScheduledTask -TaskName $taskName -ErrorAction Stop
Write-Host "Started $taskName"
