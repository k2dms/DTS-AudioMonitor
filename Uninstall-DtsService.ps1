#Requires -RunAsAdministrator
$taskName = 'DTS-AudioMonitor'
Unregister-ScheduledTask -TaskName $taskName -Confirm:$false -ErrorAction SilentlyContinue
Unregister-ScheduledTask -TaskName 'DTS-AudioMonitor-Watcher' -Confirm:$false -ErrorAction SilentlyContinue
Remove-Item (Join-Path $env:TEMP 'DtsAudioMonitor.Service.lock') -Force -ErrorAction SilentlyContinue
Write-Host "Removed scheduled task(s)."
