#Requires -Version 5.1
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$proj = Join-Path $root 'app\DtsAudioMonitor\DtsAudioMonitor.csproj'
$outDir = Join-Path $root 'release\DtsAudioMonitor'
$version = '1.1.5'
$zipPath = Join-Path $root "release\DtsAudioMonitor-v$version-win-x64.zip"

# Dependencies
$svvDir = Join-Path $root 'SoundVolumeView'
$svvExe = Join-Path $svvDir 'SoundVolumeView.exe'
if (-not (Test-Path $svvExe)) {
    Write-Host 'Downloading SoundVolumeView...'
    & (Join-Path $root 'Install-DtsAudioTools.ps1')
}

$env:Path = [System.Environment]::GetEnvironmentVariable('Path', 'Machine') + ';' + [System.Environment]::GetEnvironmentVariable('Path', 'User')
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw '.NET SDK not found. Install .NET 8 SDK first.'
}

Write-Host 'Publishing self-contained app...'
if (Test-Path $outDir) { Remove-Item $outDir -Recurse -Force }
dotnet publish $proj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -o $outDir

Copy-Item (Join-Path $root 'config.json') (Join-Path $outDir 'config.json') -Force
$destSvv = Join-Path $outDir 'SoundVolumeView'
New-Item -ItemType Directory -Force -Path $destSvv | Out-Null
Copy-Item $svvExe (Join-Path $destSvv 'SoundVolumeView.exe') -Force
Copy-Item (Join-Path $svvDir 'readme.txt') (Join-Path $destSvv 'readme.txt') -Force -ErrorAction SilentlyContinue

@'
@echo off
title DTS Audio Monitor
cd /d "%~dp0"
start "" "DtsAudioMonitor.exe" --minimized
'@ | Set-Content (Join-Path $outDir 'Start DTS Audio Monitor.bat') -Encoding ASCII

@'
@echo off
set STARTUP=%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup
set LNK=%STARTUP%\DTS Audio Monitor.lnk
powershell -NoProfile -Command "$s=(New-Object -ComObject WScript.Shell).CreateShortcut('%LNK%'); $s.TargetPath='%~dp0DtsAudioMonitor.exe'; $s.Arguments='--minimized'; $s.WorkingDirectory='%~dp0'; $s.Description='DTS Audio Monitor'; $s.Save()"
echo Autostart enabled: %LNK%
pause
'@ | Set-Content (Join-Path $outDir 'Install autostart.bat') -Encoding ASCII

Copy-Item (Join-Path $root 'Install-DtsShell.ps1') (Join-Path $outDir 'Install-DtsShell.ps1') -Force
Copy-Item (Join-Path $root 'Uninstall-DtsApp.ps1') (Join-Path $outDir 'Uninstall-DtsApp.ps1') -Force

@'
@echo off
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -Command "& { . '%~dp0Install-DtsShell.ps1'; Install-DtsStartMenu -ExePath '%~dp0DtsAudioMonitor.exe' -Version '1.1.5' }"
echo.
echo Done. Open Start menu and search: DTS Audio Monitor
pause
'@ | Set-Content (Join-Path $outDir 'Install Start Menu.bat') -Encoding ASCII

@'
# DTS Audio Monitor v1.1.5

1. Распакуйте архив в любую папку (например C:\Apps\DtsAudioMonitor)
2. Запустите **Install Start Menu.bat** (ярлык в Пуске и запись в «Приложения»)
3. Запустите **DtsAudioMonitor.exe** или **Start DTS Audio Monitor.bat**
4. Для автозагрузки: **Install autostart.bat**

Иконка в трее (рядом с часами). Двойной щелчок — открыть окно.

Требования: Windows 10/11 x64, DTS Sound Unbound из Microsoft Store.
'@ | Set-Content (Join-Path $outDir 'README.txt') -Encoding UTF8

Get-ChildItem $outDir -Filter '*.pdb' -Recurse | Remove-Item -Force -ErrorAction SilentlyContinue

Write-Host 'Creating zip...'
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path (Join-Path $outDir '*') -DestinationPath $zipPath -Force

Write-Host "Release folder: $outDir"
Write-Host "Release zip:    $zipPath"
$size = [math]::Round((Get-Item $zipPath).Length / 1MB, 1)
Write-Host "Zip size:       $size MB"
