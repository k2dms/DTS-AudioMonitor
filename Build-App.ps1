#Requires -Version 5.1
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$proj = Join-Path $root 'app\DtsAudioMonitor\DtsAudioMonitor.csproj'

$svvSrc = Join-Path $root 'SoundVolumeView\SoundVolumeView.exe'
if (-not (Test-Path $svvSrc)) {
    Write-Host 'Downloading SoundVolumeView...'
    & (Join-Path $root 'Install-DtsAudioTools.ps1')
}

$env:Path = [System.Environment]::GetEnvironmentVariable('Path', 'Machine') + ';' + [System.Environment]::GetEnvironmentVariable('Path', 'User')
dotnet publish $proj -c Release -r win-x64 --self-contained false -o (Join-Path $root 'publish')

Copy-Item (Join-Path $root 'config.json') (Join-Path $root 'publish\config.json') -Force
if (Test-Path $svvSrc) {
    $dest = Join-Path $root 'publish\SoundVolumeView'
    New-Item -ItemType Directory -Force -Path $dest | Out-Null
    Copy-Item $svvSrc (Join-Path $dest 'SoundVolumeView.exe') -Force
}

Write-Host "Built: $root\publish\DtsAudioMonitor.exe"
