#Requires -Version 5.1
<#
.SYNOPSIS
  Manual run: full DTS cycle for XV272U F3 (same as service monitor fix).
#>
[CmdletBinding()]
param(
    [switch]$Quiet
)

$ErrorActionPreference = 'Stop'
. (Join-Path (Split-Path -Parent $MyInvocation.MyCommand.Path) 'DtsAudioMonitor.Common.ps1')
$null = Initialize-DtsDependencies
Enable-DtsMonitorPlaybackFix -Quiet:$Quiet
