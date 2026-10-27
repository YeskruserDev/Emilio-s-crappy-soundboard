@echo off
setlocal EnableExtensions
cd /d "%~dp0"
title Emilio's Soundboard v2 - Install

echo ==========================================
echo   EMILIO'S SOUNDBOARD v2 - INSTALL
echo ==========================================
echo.
where dotnet >nul 2>nul
if errorlevel 1 (
  echo ERROR: .NET 8 SDK was not found.
  echo Install it from: https://dotnet.microsoft.com/download/dotnet/8.0
  echo Choose the Windows x64 SDK, then run this installer again.
  pause
  exit /b 1
)

echo Publishing Emilio's Soundboard for Windows x64...
dotnet publish "EmiliosSoundboard\EmiliosSoundboard.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o "%~dp0App\EmiliosSoundboard"
if errorlevel 1 goto failed

if not exist "%~dp0App\EmiliosSoundboard\EmiliosSoundboard.exe" (
  echo ERROR: Publish finished but the executable was not found.
  goto failed
)

set "APP_EXE=%~dp0App\EmiliosSoundboard\EmiliosSoundboard.exe"
set "APP_DIR=%~dp0App\EmiliosSoundboard"
set "SHORTCUT_NAME=Emilio's Soundboard.lnk"
where powershell >nul 2>nul
if errorlevel 1 (
  echo App built successfully, but PowerShell was not found to create a shortcut.
  echo Launch this file manually: "%APP_EXE%"
  pause
  exit /b 0
)

echo Creating desktop shortcut...
powershell -NoProfile -ExecutionPolicy Bypass -Command "$ErrorActionPreference='Stop'; $desktop=[Environment]::GetFolderPath('DesktopDirectory'); if (-not (Test-Path $desktop)) { $desktop=Join-Path $env:USERPROFILE 'OneDrive\Desktop'; if (-not (Test-Path $desktop)) { New-Item -ItemType Directory -Path $desktop -Force | Out-Null } }; $shell=New-Object -ComObject WScript.Shell; $shortcutPath=Join-Path $desktop $env:SHORTCUT_NAME; $s=$shell.CreateShortcut($shortcutPath); $s.TargetPath=$env:APP_EXE; $s.WorkingDirectory=$env:APP_DIR; $s.IconLocation=($env:APP_EXE + ',0'); $s.Description='Launch Emilio''s Soundboard'; $s.Save(); Write-Output ('Shortcut created: ' + $shortcutPath)"
if errorlevel 1 (
  echo App built, but Windows could not create the shortcut automatically.
  echo Open App\EmiliosSoundboard and double-click EmiliosSoundboard.exe.
  pause
  exit /b 0
)

echo.
echo SUCCESS! Emilio's Soundboard v2 is ready.
echo Desktop shortcut: Emilio's Soundboard
echo App folder: App\EmiliosSoundboard\
echo You can now launch it from the desktop whenever you want.
pause
exit /b 0

:failed
echo.
echo INSTALL FAILED. Copy the complete error text above and send it for troubleshooting.
pause
exit /b 1
