@echo off
set APP=%~dp0app\DtsAudioMonitor\bin\Release\net8.0-windows\DtsAudioMonitor.exe
if not exist "%APP%" (
    echo Build the app first:
    echo   cd app\DtsAudioMonitor
    echo   dotnet build -c Release
    pause
    exit /b 1
)
start "" "%APP%" --minimized
