#Requires -Version 5.1
# Start menu + Apps & Features registration for DTS Audio Monitor

function New-DtsShellShortcut {
    param(
        [Parameter(Mandatory)][string]$ShortcutPath,
        [Parameter(Mandatory)][string]$TargetExe,
        [string]$Arguments = '',
        [string]$WorkingDirectory = '',
        [string]$IconPath = '',
        [string]$Description = 'DTS Audio Monitor'
    )
    if (-not $WorkingDirectory) { $WorkingDirectory = Split-Path $TargetExe -Parent }
    if (-not $IconPath) { $IconPath = $TargetExe }

    $dir = Split-Path $ShortcutPath -Parent
    if ($dir -and -not (Test-Path $dir)) {
        New-Item -ItemType Directory -Force -Path $dir | Out-Null
    }

    $shell = New-Object -ComObject WScript.Shell
    $sc = $shell.CreateShortcut($ShortcutPath)
    $sc.TargetPath = $TargetExe
    $sc.Arguments = $Arguments
    $sc.WorkingDirectory = $WorkingDirectory
    $sc.Description = $Description
    $sc.IconLocation = "$IconPath,0"
    $sc.Save()
}

function Register-DtsUninstallEntry {
    param(
        [Parameter(Mandatory)][string]$InstallDir,
        [Parameter(Mandatory)][string]$ExePath,
        [string]$Version = '1.0.0'
    )
    $icon = Join-Path $InstallDir 'Assets\app.ico'
    if (-not (Test-Path $icon)) { $icon = $ExePath }

    $uninstallScript = Join-Path $InstallDir 'Uninstall-DtsApp.ps1'
    if (Test-Path $uninstallScript) {
        $uninstall = "powershell.exe -NoProfile -ExecutionPolicy Bypass -File `"$uninstallScript`""
    } else {
        $uninstall = "powershell.exe -NoProfile -Command `"Write-Host 'Remove folder manually: $InstallDir'`""
    }

    $keyPath = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\k2dms.DtsAudioMonitor'
    New-Item -Path $keyPath -Force | Out-Null
    Set-ItemProperty -Path $keyPath -Name DisplayName -Value 'DTS Audio Monitor'
    Set-ItemProperty -Path $keyPath -Name DisplayIcon -Value $icon
    Set-ItemProperty -Path $keyPath -Name DisplayVersion -Value $Version
    Set-ItemProperty -Path $keyPath -Name Publisher -Value 'k2dms'
    Set-ItemProperty -Path $keyPath -Name InstallLocation -Value $InstallDir
    Set-ItemProperty -Path $keyPath -Name InstallSource -Value $InstallDir
    Set-ItemProperty -Path $keyPath -Name UninstallString -Value $uninstall
    Set-ItemProperty -Path $keyPath -Name NoModify -Value 1 -Type DWord
    Set-ItemProperty -Path $keyPath -Name NoRepair -Value 1 -Type DWord
}

function Install-DtsStartMenu {
    param(
        [Parameter(Mandatory)][string]$ExePath,
        [string]$Version = '1.0.0'
    )
    $installDir = Split-Path $ExePath -Parent
    $icon = Join-Path $installDir 'Assets\app.ico'
    if (-not (Test-Path $icon)) { $icon = $ExePath }

    $programs = [Environment]::GetFolderPath('Programs')
    $folder = Join-Path $programs 'DTS Audio Monitor'
    $lnk = Join-Path $folder 'DTS Audio Monitor.lnk'
    New-DtsShellShortcut -ShortcutPath $lnk -TargetExe $ExePath -WorkingDirectory $installDir -IconPath $icon

    Register-DtsUninstallEntry -InstallDir $installDir -ExePath $ExePath -Version $Version
    Write-Host "Start menu: $lnk"
    Write-Host 'Apps list: Settings -> Apps -> Installed apps -> DTS Audio Monitor'
}
