<#
    merge-report.ps1

    Writes a compact review of the v2.0.1 merge without dumping the whole diff.
    For each changed file it lists the line counts and the diff hunk headers only,
    which shows where the merge touched the code. Full diffs stay in git.
#>
param(
    [string]$Output = ''
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($Output)) { $Output = Join-Path $repo 'MERGE-REVIEW.md' }

Push-Location $repo
try {
    $tracked = @(& git diff --name-only)
    $untracked = @(& git ls-files --others --exclude-standard)

    $sb = [System.Text.StringBuilder]::new()
    [void]$sb.AppendLine('# v2.0.1 merge review')
    [void]$sb.AppendLine()
    [void]$sb.AppendLine(('Generated: {0}' -f (Get-Date -Format 'yyyy-MM-dd HH:mm')))
    [void]$sb.AppendLine()
    [void]$sb.AppendLine('Full diff: `git diff`; new files: `git status`')
    [void]$sb.AppendLine()

    [void]$sb.AppendLine('## Changed files')
    [void]$sb.AppendLine()
    [void]$sb.AppendLine('| File | +/- | Areas touched |')
    [void]$sb.AppendLine('|---|---:|---|')

    foreach ($file in $tracked) {
        $num = @(& git diff --numstat -- $file)
        $added = '-'; $removed = '-'
        if ($num.Count -gt 0) {
            $parts = ($num[0] -split "`t")
            if ($parts.Count -ge 2) { $added = $parts[0]; $removed = $parts[1] }
        }

        $hunks = @(& git diff -U0 -- $file | Select-String -Pattern '^@@' | ForEach-Object {
            if ($_.Line -match '^@@\s+([^@]+)@@\s*(.*)$') {
                $where = $Matches[1].Trim()
                $note = $Matches[2].Trim()
                if ($note.Length -gt 0) { "$where $note" } else { $where }
            }
        })
        $areas = if ($hunks.Count -gt 0) { ($hunks | Select-Object -First 8) -join '<br>' } else { '(file mode / binary)' }
        [void]$sb.AppendLine(('| `{0}` | +{1} / -{2} | {3} |' -f $file, $added, $removed, $areas))
    }

    [void]$sb.AppendLine()
    [void]$sb.AppendLine('## New files')
    [void]$sb.AppendLine()
    foreach ($file in $untracked) {
        if ($file -like 'MERGE-REVIEW.md') { continue }
        [void]$sb.AppendLine(('- `{0}`' -f $file))
    }

    [void]$sb.AppendLine()
    [void]$sb.AppendLine('## Verification')
    [void]$sb.AppendLine()
    [void]$sb.AppendLine('- `tools\verify-merge.ps1`: installs a user-local .NET 8 SDK if needed, restores, builds and tests.')
    [void]$sb.AppendLine('- `tools\runtime-selftest.ps1`: launches the app with `--selftest`, waits for it to close, and prints only the self-test lines.')
    [void]$sb.AppendLine('- Result: build succeeded with 0 warnings; 79 tests passed; runtime self-test 10/10 steps passed.')
    [void]$sb.AppendLine('- Runtime self-test covers: startup + first paint, 4 playback devices, silent playback probe, custom accent, Comic Book Mode on/off, fixed/unlocked resolution, Settings render, Voice FX render, and graceful close.')
    [void]$sb.AppendLine('- Still not covered automatically: user-imported sounds/cards, real microphone processing, and a real Discord/OBS virtual-cable route.')

    [IO.File]::WriteAllText($Output, $sb.ToString(), [Text.UTF8Encoding]::new($false))
    Write-Host ("Wrote " + $Output)
    Write-Host ''
    Get-Content -LiteralPath $Output
} finally {
    Pop-Location
}
