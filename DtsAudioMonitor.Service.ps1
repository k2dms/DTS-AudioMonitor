#Requires -Version 5.1
<#
.SYNOPSIS
  Background worker for DTS Audio Monitor (run via scheduled task / service installer).

  - Headphones selected: only verify/enable spatial sound, never switch devices.
  - Switch TO monitor: full cycle (headphones -> DTS -> spatial -> monitor).
#>
[CmdletBinding()]
param(
    [switch]$Quiet
)

$ErrorActionPreference = 'Stop'
$script:Busy = $false

. (Join-Path (Split-Path -Parent $MyInvocation.MyCommand.Path) 'DtsAudioMonitor.Common.ps1')

$paths = Initialize-DtsDependencies
$config = Get-DtsConfig
$lockFile = Join-Path $env:TEMP 'DtsAudioMonitor.Service.lock'

function Test-ServiceLock {
    if (-not (Test-Path $lockFile)) { return $false }
    $owner = Get-Content $lockFile -Raw -ErrorAction SilentlyContinue
    $procId = 0
    if ($owner -match '^\d+$') { [void][int]::TryParse($owner, [ref]$procId) }
    return ($procId -gt 0 -and (Get-Process -Id $procId -ErrorAction SilentlyContinue))
}

if (Test-ServiceLock) {
    Write-DtsLog 'Service worker already running.' -Quiet:$Quiet
    exit 0
}

Set-Content -Path $lockFile -Value $PID -Encoding ASCII

try {
    Write-DtsLog 'Service worker started.' -Quiet:$Quiet
    $previousName = $null

    while ($true) {
        if ($script:Busy) {
            Start-Sleep -Seconds 1
            continue
        }

        $current = Get-DtsPlaybackDevice
        $name = $current.Name

        $spatial = Get-DtsSpatialHealth

        if ($name -like $config.HeadphonesNameMatch) {
            $needCheck = $spatial.State -ne 'CorrectDts'
            if (-not $needCheck) {
                $needCheck = -not (Test-DtsSpatialRecentlyOk -WithinSeconds $config.HeadphonesCheckSeconds)
            }

            if ($needCheck) {
                if ($spatial.State -eq 'WrongFormat') {
                    Write-DtsLog "Headphones: wrong spatial ($($spatial.ActiveGuid)), restoring DTS..." -Quiet:$Quiet
                } else {
                    Write-DtsLog "Headphones active ($name): checking spatial sound..." -Quiet:$Quiet
                }
                try {
                    $script:Busy = $true
                    $useApp = $spatial.State -ne 'CorrectDts'
                    Enable-DtsSpatialOnHeadphones -UseDtsApp:$useApp -Quiet:$Quiet
                } catch {
                    Write-DtsLog "Headphones spatial error: $($_.Exception.Message)" -Quiet:$Quiet
                    try {
                        Enable-DtsSpatialOnHeadphones -UseDtsApp -Quiet:$Quiet
                    } catch {
                        Write-DtsLog "Headphones spatial retry failed: $($_.Exception.Message)" -Quiet:$Quiet
                    }
                } finally {
                    $script:Busy = $false
                }
            }
        }
        elseif ($name -like $config.MonitorNameMatch) {
            if ($spatial.State -ne 'CorrectDts') {
                if ($spatial.State -eq 'WrongFormat') {
                    Write-DtsLog "Monitor: wrong spatial ($($spatial.ActiveGuid)), restoring DTS on headphones..." -Quiet:$Quiet
                } else {
                    Write-DtsLog 'Monitor: headphones spatial off, restoring...' -Quiet:$Quiet
                }
                try {
                    $script:Busy = $true
                    Enable-DtsSpatialOnHeadphones -UseDtsApp -Quiet:$Quiet
                } catch {
                    Write-DtsLog "Spatial restore error: $($_.Exception.Message)" -Quiet:$Quiet
                } finally {
                    $script:Busy = $false
                }
            }
            elseif ($previousName -and $previousName -notlike $config.MonitorNameMatch) {
                if (-not (Test-DtsMonitorFixCooldown -Seconds $config.MonitorFixCooldownSeconds)) {
                    Write-DtsLog "Switch to monitor: '$previousName' -> '$name'. Full DTS cycle..." -Quiet:$Quiet
                    try {
                        $script:Busy = $true
                        Enable-DtsMonitorPlaybackFix -Quiet:$Quiet
                    } catch {
                        Write-DtsLog "Monitor fix error: $($_.Exception.Message)" -Quiet:$Quiet
                    } finally {
                        $script:Busy = $false
                    }
                } else {
                    Write-DtsLog 'Monitor fix skipped (cooldown).' -Quiet:$Quiet
                }
            }
        }

        $previousName = $name
        Start-Sleep -Seconds $config.PollSeconds
    }
} finally {
    Remove-Item $lockFile -Force -ErrorAction SilentlyContinue
}
