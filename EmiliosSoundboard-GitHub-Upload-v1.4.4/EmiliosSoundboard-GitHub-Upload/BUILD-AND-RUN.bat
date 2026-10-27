@echo off
setlocal
cd /d "%~dp0"
echo ==========================================
echo   EMILIO'S SOUNDBOARD - BUILD AND RUN
echo ==========================================
echo.
where dotnet >nul 2>nul
if errorlevel 1 (
  echo ERROR: .NET SDK was not found.
  echo Install the .NET 8 SDK from https://dotnet.microsoft.com/download/dotnet/8.0
  echo Then reopen this file.
  pause
  exit /b 1
)
echo Restoring packages and building the app...
dotnet restore "EmiliosSoundboard.sln"
if errorlevel 1 goto failed
dotnet build "EmiliosSoundboard.sln" -c Release --no-restore
if errorlevel 1 goto failed
echo.
echo Build succeeded. Launching...
start "" "%~dp0EmiliosSoundboard\bin\Release\net8.0-windows\EmiliosSoundboard.exe"
exit /b 0
:failed
echo.
echo BUILD FAILED. Copy the error text above and send it back for a targeted fix.
pause
exit /b 1
