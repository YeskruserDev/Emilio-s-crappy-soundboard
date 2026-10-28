<#
    runtime-selftest.ps1

    Launches the built app with --selftest, waits for it to close itself, then reads the new
    log section and prints only the self-test lines and any errors.

    Prerequisites:
      powershell -File tools\verify-merge.ps1   (builds the app first)

    Usage:
      powershell -ExecutionPolicy Bypass -File tools\runtime-selftest.ps1
      powershell -ExecutionPolicy Bypass -File tools\runtime-selftest.ps1 -TimeoutSeconds 60
#>
param(
    [int]$TimeoutSeconds = 45
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $repo 'EmiliosSoundboard-v2.0.0-GitHub-Source-Fixed\EmiliosSoundboard-v2.0.0\EmiliosSoundboard\bin\Release\net8.0-windows\EmiliosSoundboard.exe'
$logDir = Join-Path $env:LOCALAPPDATA 'EmiliosSoundboard\logs'

if (-not (Test-Path -LiteralPath $exe)) {
    throw "Built app not found. Run: powershell -File tools\verify-merge.ps1"
}

Write-Host '==> Launching the app self-test' -ForegroundColor Cyan
$process = Start-Process -FilePath $exe -ArgumentList '--verbose', '--selftest' -PassThru
if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
    try { $process.Kill() } catch { }
    throw "The app did not finish its self-test within $TimeoutSeconds seconds."
}

$exitCode = $process.ExitCode
$latest = Get-ChildItem -LiteralPath $logDir -Filter 'app-*.log' -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1
if ($null -eq $latest) { throw 'No log file was written.' }

$lines = Get-Content -LiteralPath $latest.FullName
$start = -1
for ($i = $lines.Count - 1; $i -ge 0; $i--) {
    if ($lines[$i] -match '\[self-test\]') { $start = $i; break }
}
if ($start -lt 0) { throw "No self-test section was found in $($latest.FullName)" }

$section = $lines[$start..($lines.Count - 1)]
$interesting = $section | Select-String -Pattern '\[selftest\]|\[app\s*\] stopped|FATAL|ERROR|WARN' -CaseSensitive:$false
$interesting | ForEach-Object { Write-Host ('    ' + $_.Line) }

$failed = ($section | Select-String -Pattern '\[selftest\] FAIL' -CaseSensitive:$false).Count
$finished = $section | Select-String -Pattern '\[selftest\] finished:' | Select-Object -Last 1

Write-Host ''
if ($exitCode -ne 0 -or $failed -gt 0 -or $null -eq $finished) {
    Write-Host ('Self-test failed (exit {0}, {1} failed step(s)). Log: {2}' -f $exitCode, $failed, $latest.FullName) -ForegroundColor Red
    exit 1
}

Write-Host ('Self-test passed (exit {0}). Log: {1}' -f $exitCode, $latest.FullName) -ForegroundColor Green
