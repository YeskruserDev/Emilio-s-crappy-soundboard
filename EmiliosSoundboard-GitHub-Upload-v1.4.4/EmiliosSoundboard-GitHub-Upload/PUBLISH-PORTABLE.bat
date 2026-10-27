@echo off
setlocal
cd /d "%~dp0"
where dotnet >nul 2>nul
if errorlevel 1 (
  echo .NET SDK not found. Install .NET 8 SDK from https://dotnet.microsoft.com/download/dotnet/8.0
  pause
  exit /b 1
)
echo Publishing self-contained Windows x64 EXE...
dotnet publish "EmiliosSoundboard\EmiliosSoundboard.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o "%~dp0release\EmiliosSoundboard-win-x64"
if errorlevel 1 goto failed
if not exist "%~dp0release" mkdir "%~dp0release"
where powershell >nul 2>nul
if not errorlevel 1 (
  if exist "%~dp0release\EmiliosSoundboard-win-x64.zip" del "%~dp0release\EmiliosSoundboard-win-x64.zip"
  powershell -NoProfile -ExecutionPolicy Bypass -Command "Compress-Archive -Path '%~dp0release\EmiliosSoundboard-win-x64\*' -DestinationPath '%~dp0release\EmiliosSoundboard-win-x64.zip' -Force"
  if errorlevel 1 goto zipfailed
)
echo.
echo PUBLISH SUCCEEDED.
echo EXE: release\EmiliosSoundboard-win-x64\EmiliosSoundboard.exe
echo GITHUB ZIP: release\EmiliosSoundboard-win-x64.zip
echo Upload the ZIP to GitHub Releases. Keep the EXE for users who want the direct download.
pause
exit /b 0
:zipfailed
echo The EXE published, but the release ZIP could not be created. Zip the contents of release\EmiliosSoundboard-win-x64 manually.
pause
exit /b 0
:failed
echo.
echo PUBLISH FAILED. Copy the error text above and send it back for a targeted fix.
pause
exit /b 1
