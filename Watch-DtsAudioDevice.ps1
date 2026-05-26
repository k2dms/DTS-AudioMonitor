#Requires -Version 5.1
# Legacy entry point — use DtsAudioMonitor.Service.ps1 + Install-DtsService.ps1
Write-Warning 'Watch-DtsAudioDevice.ps1 is deprecated. Use Install-DtsService.ps1 instead.'
& (Join-Path $PSScriptRoot 'DtsAudioMonitor.Service.ps1') @PSBoundParameters
