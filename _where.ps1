$paths = @(
    'C:\Users\dms\Scripts\DTS-AudioMonitor\publish\DtsAudioMonitor.exe',
    'C:\Users\dms\Scripts\DTS-AudioMonitor\release\DtsAudioMonitor\DtsAudioMonitor.exe',
    "$env:APPDATA\Microsoft\Windows\Start Menu\Programs\Startup\DTS Audio Monitor.lnk"
)
foreach ($p in $paths) {
    if (Test-Path $p) { Write-Host "OK: $p" } else { Write-Host "NO: $p" }
}
Get-Process DtsAudioMonitor -ErrorAction SilentlyContinue | Format-Table Id, Path -AutoSize
