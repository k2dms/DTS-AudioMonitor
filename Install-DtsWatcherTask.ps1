#Requires -RunAsAdministrator
Write-Warning 'Install-DtsWatcherTask.ps1 is deprecated. Running Install-DtsService.ps1...'
& (Join-Path $PSScriptRoot 'Install-DtsService.ps1')
