<#
    verify-merge.ps1

    One command to verify the v2.0.1 merge on a machine with no .NET SDK:
      * installs a user-local .NET 8 SDK under %TEMP%\opencode\dotnet8 (no admin, no PATH changes)
      * restores and builds the solution
      * runs the unit tests
      * prints only a short summary plus the failing lines; full logs are written next to this script

    Usage:
      powershell -ExecutionPolicy Bypass -File tools\verify-merge.ps1
      powershell -ExecutionPolicy Bypass -File tools\verify-merge.ps1 -NoTests
#>
param(
    [switch]$NoTests,
    [switch]$ForceInstall
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$work = Join-Path $env:TEMP 'opencode\dotnet8'
$dotnet = Join-Path $work 'sdk\dotnet.exe'
$logDir = Join-Path $work 'logs'
$solution = Join-Path $repo 'EmiliosSoundboard-v2.0.0-GitHub-Source-Fixed\EmiliosSoundboard-v2.0.0\EmiliosSoundboard.sln'

function Write-Step($text) { Write-Host "==> $text" -ForegroundColor Cyan }

function Show-RelevantLog($path, $pattern = 'error|warning|failed', [int]$tail = 60)
{
    if (-not (Test-Path -LiteralPath $path)) { return }
    $lines = Get-Content -LiteralPath $path
    $interesting = $lines | Select-String -Pattern $pattern -CaseSensitive:$false
    if ($interesting) {
        $interesting | Select-Object -Last 40 | ForEach-Object { Write-Host ("    " + $_.Line) }
    } else {
        $lines | Select-Object -Last $tail | ForEach-Object { Write-Host ("    " + $_) }
    }
    Write-Host "    (full log: $path)"
}

New-Item -ItemType Directory -Path $work -Force | Out-Null
New-Item -ItemType Directory -Path $logDir -Force | Out-Null

if (-not (Test-Path -LiteralPath $solution)) {
    throw "Solution not found: $solution"
}

if ($ForceInstall -or -not (Test-Path -LiteralPath $dotnet)) {
    Write-Step 'Installing a user-local .NET 8 SDK (first run only)'
    $installer = Join-Path $work 'dotnet-install.ps1'
    if (-not (Test-Path -LiteralPath $installer)) {
        Invoke-WebRequest -Uri 'https://dot.net/v1/dotnet-install.ps1' -OutFile $installer -UseBasicParsing
    }
    $installLog = Join-Path $logDir 'install.log'
    & powershell -NoProfile -ExecutionPolicy Bypass -File $installer -Channel 8.0 -InstallDir (Join-Path $work 'sdk') -NoPath *> $installLog
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $dotnet)) {
        Show-RelevantLog $installLog 'error|failed|exception'
        throw 'The .NET 8 SDK could not be installed.'
    }
    Write-Host '    .NET 8 SDK installed.'
} else {
    Write-Step 'Using the existing user-local .NET 8 SDK'
}

$env:DOTNET_ROOT = Join-Path $work 'sdk'
$env:PATH = "$env:DOTNET_ROOT;$env:PATH"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'

Write-Host ("    " + (& $dotnet --version))

$restoreLog = Join-Path $logDir 'restore.log'
Write-Step 'Restoring packages'
# The machine-wide NuGet.Config uses package source mapping for another project, which blocks
# every package this solution needs. Use an isolated config for the verification build.
$nugetConfig = Join-Path $work 'NuGet.Config'
@'
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
  </packageSources>
</configuration>
'@ | Set-Content -LiteralPath $nugetConfig -Encoding UTF8
& $dotnet restore $solution --nologo --configfile $nugetConfig *> $restoreLog
if ($LASTEXITCODE -ne 0) {
    Show-RelevantLog $restoreLog 'error|failed|NU[0-9]+'
    exit 1
}

$buildLog = Join-Path $logDir 'build.log'
Write-Step 'Building the solution'
& $dotnet build $solution -c Release --no-restore --nologo *> $buildLog
$buildExit = $LASTEXITCODE
$warnings = @(Get-Content -LiteralPath $buildLog | Select-String -Pattern ': warning ' -CaseSensitive:$false)
if ($buildExit -ne 0) {
    Show-RelevantLog $buildLog 'error|failed'
    exit 1
}
Write-Host ("    Build succeeded ({0} warning line(s))." -f $warnings.Count)
if ($warnings.Count -gt 0) {
    $warnings | Select-Object -First 20 | ForEach-Object { Write-Host ("    " + $_.Line) }
}

if (-not $NoTests) {
    $testLog = Join-Path $logDir 'test.log'
    Write-Step 'Running unit tests'
    & $dotnet test $solution -c Release --no-build --nologo *> $testLog
    $testExit = $LASTEXITCODE
    $summary = Get-Content -LiteralPath $testLog | Select-String -Pattern 'Passed!|Failed!|error|Total tests' -CaseSensitive:$false
    if ($testExit -ne 0) {
        Show-RelevantLog $testLog 'error|failed'
        exit 1
    }
    $summary | ForEach-Object { Write-Host ("    " + $_.Line) }
}

Write-Host ''
Write-Host 'Verification passed.' -ForegroundColor Green
Write-Host ("Logs: " + $logDir)
