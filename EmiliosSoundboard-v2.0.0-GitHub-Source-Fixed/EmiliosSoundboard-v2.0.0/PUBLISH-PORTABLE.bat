@echo off
setlocal EnableExtensions
cd /d "%~dp0"
title Emilio's Soundboard v2 - Publish
where dotnet >nul 2>nul
if errorlevel 1 (
  echo .NET 8 SDK not found. Install from https://dotnet.microsoft.com/download/dotnet/8.0
  pause
  exit /b 1
)
if not exist "%~dp0release" mkdir "%~dp0release"
echo Publishing self-contained Windows x64 build...
dotnet publish "EmiliosSoundboard\EmiliosSoundboard.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o "%~dp0release\EmiliosSoundboard-v2-win-x64"
if errorlevel 1 goto failed
if not exist "%~dp0release\EmiliosSoundboard-v2-win-x64\EmiliosSoundboard.exe" goto failed
where powershell >nul 2>nul
if errorlevel 1 goto nozip
if exist "%~dp0release\EmiliosSoundboard-v2-win-x64.zip" del "%~dp0release\EmiliosSoundboard-v2-win-x64.zip"
powershell -NoProfile -ExecutionPolicy Bypass -Command "Compress-Archive -Path '%~dp0release\EmiliosSoundboard-v2-win-x64\*' -DestinationPath '%~dp0release\EmiliosSoundboard-v2-win-x64.zip' -Force"
if errorlevel 1 goto zipfailed
echo.
echo PUBLISH SUCCEEDED.
echo Folder: release\EmiliosSoundboard-v2-win-x64\
echo ZIP: release\EmiliosSoundboard-v2-win-x64.zip
echo Upload the ZIP to the GitHub v2.0.0 Release.
pause
exit /b 0
:nozip
echo Publish succeeded, but PowerShell was unavailable to create the ZIP.
echo Upload the contents of release\EmiliosSoundboard-v2-win-x64 manually.
pause
exit /b 0
:zipfailed
echo The EXE was published but ZIP creation failed. Zip the contents of release\EmiliosSoundboard-v2-win-x64 manually.
pause
exit /b 0
:failed
echo PUBLISH FAILED. Copy the full error output for troubleshooting.
pause
exit /b 1
