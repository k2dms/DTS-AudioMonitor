#Requires -Version 5.1
<#
.SYNOPSIS
  Следит за переключением на XV272U F3 и автоматически включает DTS Headphone:X.
#>
param(
    [int]$PollSeconds = 3,
    [int]$CooldownSeconds = 45,
    [string]$MonitorNameMatch = 'XV272U',
    [switch]$Quiet
)

$ErrorActionPreference = 'Stop'
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$fixScript = Join-Path $scriptDir 'Enable-DtsForXV272U.ps1'
$lockFile = Join-Path $env:TEMP 'DtsAudioMonitor.lock'
$cooldownFile = Join-Path $env:TEMP 'DtsAudioMonitor.cooldown'
$logFile = Join-Path $scriptDir 'watch.log'

$moduleDll = Join-Path $env:USERPROFILE 'Documents\WindowsPowerShell\Modules\AudioDeviceCmdlets\AudioDeviceCmdlets.dll'
if (-not (Test-Path $moduleDll)) {
    throw "Run Install-DtsAudioTools.ps1 first: $scriptDir"
}
Import-Module $moduleDll

function Write-Log([string]$Message) {
    $line = "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') $Message"
    Add-Content -Path $logFile -Value $line -Encoding UTF8
    if (-not $Quiet) { Write-Host $line }
}

function Test-Cooldown {
    if (-not (Test-Path $cooldownFile)) { return $false }
    $last = [datetime](Get-Content $cooldownFile -Raw)
    return ((Get-Date) - $last).TotalSeconds -lt $CooldownSeconds
}

function Set-Cooldown {
    Set-Content -Path $cooldownFile -Value (Get-Date).ToString('o') -Encoding UTF8
}

if (Test-Path $lockFile) {
    $owner = Get-Content $lockFile -Raw -ErrorAction SilentlyContinue
    $procId = 0
    if ($owner -match '^\d+$') { [void][int]::TryParse($owner, [ref]$procId) }
    if ($procId -gt 0 -and (Get-Process -Id $procId -ErrorAction SilentlyContinue)) {
        Write-Log ('Watcher already running (PID ' + $procId + ')')
        exit 0
    }
}

Set-Content -Path $lockFile -Value $PID -Encoding ASCII
try {
    Write-Log "Watcher started. Monitor match: *$MonitorNameMatch*, poll ${PollSeconds}s"
    $previous = $null

    while ($true) {
        $current = Get-AudioDevice -Playback
        $name = $current.Name

        if ($name -like "*$MonitorNameMatch*" -and $previous -and $previous -notlike "*$MonitorNameMatch*") {
            if (-not (Test-Cooldown)) {
                Write-Log "Switch detected: '$previous' -> '$name'. Running DTS fix..."
                try {
                    & $fixScript -Quiet:$Quiet
                    Set-Cooldown
                    Write-Log 'DTS fix completed.'
                } catch {
                    Write-Log "Error: $($_.Exception.Message)"
                }
            } else {
                Write-Log 'Skipped (cooldown).'
            }
        }

        $previous = $name
        Start-Sleep -Seconds $PollSeconds
    }
} finally {
    Remove-Item $lockFile -Force -ErrorAction SilentlyContinue
}
