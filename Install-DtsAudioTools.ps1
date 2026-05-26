#Requires -Version 5.1
# Устанавливает AudioDeviceCmdlets и SoundVolumeView
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$modDir = Join-Path $env:USERPROFILE 'Documents\WindowsPowerShell\Modules\AudioDeviceCmdlets'
$dll = Join-Path $modDir 'AudioDeviceCmdlets.dll'
$svvDir = Join-Path $scriptDir 'SoundVolumeView'
$zip = Join-Path $env:TEMP 'soundvolumeview-x64.zip'

Write-Host 'AudioDeviceCmdlets...'
New-Item -ItemType Directory -Force -Path $modDir | Out-Null
if (-not (Test-Path $dll)) {
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest -Uri 'https://github.com/frgnca/AudioDeviceCmdlets/releases/download/v3.0/AudioDeviceCmdlets.dll' -OutFile $dll -UseBasicParsing
}

Write-Host 'SoundVolumeView...'
if (-not (Test-Path (Join-Path $svvDir 'SoundVolumeView.exe'))) {
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest -Uri 'https://www.nirsoft.net/utils/soundvolumeview-x64.zip' -OutFile $zip -UseBasicParsing
    New-Item -ItemType Directory -Force -Path $svvDir | Out-Null
    Expand-Archive -Path $zip -DestinationPath $svvDir -Force
}

Write-Host 'Done.'
