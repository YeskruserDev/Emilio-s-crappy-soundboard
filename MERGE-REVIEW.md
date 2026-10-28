# v2.0.1 merge review

Generated: 2026-10-28 20:40

Full diff: `git diff`; new files: `git status`

## Changed files

| File | +/- | Areas touched |
|---|---:|---|
| `EmiliosSoundboard-v2.0.0-GitHub-Source-Fixed/EmiliosSoundboard-v2.0.0/EmiliosSoundboard.Core/DataStore.cs` | +2 / -0 | -112,0 +113,2 namespace EmiliosSoundboard |
| `EmiliosSoundboard-v2.0.0-GitHub-Source-Fixed/EmiliosSoundboard-v2.0.0/EmiliosSoundboard.Core/Models.cs` | +16 / -0 | -36,0 +37,4 namespace EmiliosSoundboard<br>-46,0 +51,3 namespace EmiliosSoundboard<br>-63,0 +71,6 namespace EmiliosSoundboard<br>-73,0 +87 namespace EmiliosSoundboard<br>-78,0 +93 namespace EmiliosSoundboard<br>-87,0 +103 namespace EmiliosSoundboard |
| `EmiliosSoundboard-v2.0.0-GitHub-Source-Fixed/EmiliosSoundboard-v2.0.0/EmiliosSoundboard/AudioOutput.cs` | +44 / -0 | -2,0 +3 using System.Collections.Generic;<br>-91,0 +93,43 namespace EmiliosSoundboard |
| `EmiliosSoundboard-v2.0.0-GitHub-Source-Fixed/EmiliosSoundboard-v2.0.0/EmiliosSoundboard/Card.cs` | +1 / -1 | -9 +9 namespace EmiliosSoundboard |
| `EmiliosSoundboard-v2.0.0-GitHub-Source-Fixed/EmiliosSoundboard-v2.0.0/EmiliosSoundboard/EmiliosSoundboard.csproj` | +4 / -1 | -20 +20,4 |
| `EmiliosSoundboard-v2.0.0-GitHub-Source-Fixed/EmiliosSoundboard-v2.0.0/EmiliosSoundboard/MainForm.Animation.cs` | +9 / -0 | -45,0 +46,8 namespace EmiliosSoundboard<br>-163,0 +172 namespace EmiliosSoundboard |
| `EmiliosSoundboard-v2.0.0-GitHub-Source-Fixed/EmiliosSoundboard-v2.0.0/EmiliosSoundboard/MainForm.Backdrop.cs` | +9 / -0 | -98,0 +99,9 namespace EmiliosSoundboard |
| `EmiliosSoundboard-v2.0.0-GitHub-Source-Fixed/EmiliosSoundboard-v2.0.0/EmiliosSoundboard/MainForm.Input.cs` | +21 / -3 | -46,2 +46,12 namespace EmiliosSoundboard<br>-63,0 +74 namespace EmiliosSoundboard<br>-203 +214 namespace EmiliosSoundboard<br>-207,0 +219 namespace EmiliosSoundboard<br>-212,0 +225,6 namespace EmiliosSoundboard |
| `EmiliosSoundboard-v2.0.0-GitHub-Source-Fixed/EmiliosSoundboard-v2.0.0/EmiliosSoundboard/MainForm.Library.cs` | +29 / -27 | -29 +29 namespace EmiliosSoundboard<br>-36,2 +36,3 namespace EmiliosSoundboard<br>-40 +41,3 namespace EmiliosSoundboard<br>-46,2 +49,2 namespace EmiliosSoundboard<br>-52 +55 namespace EmiliosSoundboard<br>-55,14 +58,12 namespace EmiliosSoundboard<br>-79 +80 namespace EmiliosSoundboard<br>-85 +86 namespace EmiliosSoundboard |
| `EmiliosSoundboard-v2.0.0-GitHub-Source-Fixed/EmiliosSoundboard-v2.0.0/EmiliosSoundboard/MainForm.Paint.cs` | +293 / -88 | -16,0 +17 namespace EmiliosSoundboard<br>-72 +73 namespace EmiliosSoundboard<br>-116,0 +118,6 namespace EmiliosSoundboard<br>-119 +126,2 namespace EmiliosSoundboard<br>-121,3 +129,7 namespace EmiliosSoundboard<br>-148,3 +160,3 namespace EmiliosSoundboard<br>-154 +166 namespace EmiliosSoundboard<br>-181 +193,14 namespace EmiliosSoundboard |
| `EmiliosSoundboard-v2.0.0-GitHub-Source-Fixed/EmiliosSoundboard-v2.0.0/EmiliosSoundboard/MainForm.Playback.cs` | +81 / -4 | -35,0 +36,4 namespace EmiliosSoundboard<br>-36,0 +41,11 namespace EmiliosSoundboard<br>-45,4 +60,21 namespace EmiliosSoundboard<br>-62,0 +95 namespace EmiliosSoundboard<br>-75,0 +109,44 namespace EmiliosSoundboard |
| `EmiliosSoundboard-v2.0.0-GitHub-Source-Fixed/EmiliosSoundboard-v2.0.0/EmiliosSoundboard/MainForm.cs` | +229 / -29 | -26,0 +27,3 namespace EmiliosSoundboard<br>-28,0 +32 namespace EmiliosSoundboard<br>-62,0 +67 namespace EmiliosSoundboard<br>-78,0 +84 namespace EmiliosSoundboard<br>-102,0 +109 namespace EmiliosSoundboard<br>-123,0 +131,3 namespace EmiliosSoundboard<br>-137,0 +148 namespace EmiliosSoundboard<br>-141 +152 namespace EmiliosSoundboard |
| `EmiliosSoundboard-v2.0.0-GitHub-Source-Fixed/EmiliosSoundboard-v2.0.0/EmiliosSoundboard/Program.cs` | +19 / -7 | -22,0 +23,3 namespace EmiliosSoundboard<br>-28,0 +32 namespace EmiliosSoundboard<br>-33 +37 namespace EmiliosSoundboard<br>-58,0 +63 namespace EmiliosSoundboard<br>-61 +66,2 namespace EmiliosSoundboard<br>-66 +72,5 namespace EmiliosSoundboard<br>-79,0 +90,5 namespace EmiliosSoundboard<br>-85,4 +100 namespace EmiliosSoundboard |
| `EmiliosSoundboard-v2.0.0-GitHub-Source-Fixed/EmiliosSoundboard-v2.0.0/EmiliosSoundboard/SettingsDialog.cs` | +228 / -22 | -1,0 +2 using System;<br>-4,0 +6 using System.Drawing.Drawing2D;<br>-5,0 +8 using System.Windows.Forms;<br>-15 +18,2 namespace EmiliosSoundboard<br>-20 +24 namespace EmiliosSoundboard<br>-24 +28 namespace EmiliosSoundboard<br>-29,0 +34 namespace EmiliosSoundboard<br>-40 +45 namespace EmiliosSoundboard |
| `EmiliosSoundboard-v2.0.0-GitHub-Source-Fixed/EmiliosSoundboard-v2.0.0/EmiliosSoundboard/SpaceField.cs` | +29 / -5 | -46,0 +47 namespace EmiliosSoundboard<br>-284,4 +285,4 namespace EmiliosSoundboard<br>-305,0 +307,11 namespace EmiliosSoundboard<br>-348,0 +361,8 namespace EmiliosSoundboard<br>-351 +371,3 namespace EmiliosSoundboard<br>-383,0 +406,2 namespace EmiliosSoundboard |
| `EmiliosSoundboard-v2.0.0-GitHub-Source-Fixed/EmiliosSoundboard-v2.0.0/EmiliosSoundboard/Theme.cs` | +115 / -2 | -1,0 +2 using System;<br>-3,0 +5 using System.Drawing.Drawing2D;<br>-11,0 +14,17 namespace EmiliosSoundboard<br>-31,0 +51,5 namespace EmiliosSoundboard<br>-34,0 +59,72 namespace EmiliosSoundboard<br>-128,0 +225,15 namespace EmiliosSoundboard<br>-182,2 +293,4 namespace EmiliosSoundboard |
| `EmiliosSoundboard-v2.0.0-GitHub-Source-Fixed/EmiliosSoundboard-v2.0.0/EmiliosSoundboard/UiKit.cs` | +145 / -29 | -19 +19 namespace EmiliosSoundboard<br>-20,0 +21 namespace EmiliosSoundboard<br>-42,0 +44 namespace EmiliosSoundboard<br>-44 +46,2 namespace EmiliosSoundboard<br>-46,0 +50 namespace EmiliosSoundboard<br>-55,2 +59,2 namespace EmiliosSoundboard<br>-79,0 +84,5 namespace EmiliosSoundboard<br>-83 +92 namespace EmiliosSoundboard |
| `EmiliosSoundboard-v2.0.0-GitHub-Source-Fixed/EmiliosSoundboard-v2.0.0/EmiliosSoundboard/VoiceFxForm.cs` | +665 / -91 | -5,0 +6 using System.Linq;<br>-8,0 +10 using NAudio.Wave;<br>-17,5 +19,16 namespace EmiliosSoundboard<br>-25 +38,3 namespace EmiliosSoundboard<br>-27 +42 namespace EmiliosSoundboard<br>-28,0 +44,2 namespace EmiliosSoundboard<br>-30,2 +47,37 namespace EmiliosSoundboard<br>-32,0 +85,101 namespace EmiliosSoundboard |
| `EmiliosSoundboard-v2.0.0-GitHub-Source-Fixed/EmiliosSoundboard-v2.0.0/README.md` | +14 / -6 | -1 +1<br>-7 +7,4 A customizable Windows soundboard built with C#, Windows Forms, .NET 8, and NAud<br>-9 +12 A customizable Windows soundboard built with C#, Windows Forms, .NET 8, and NAud<br>-11 +14 A customizable Windows soundboard built with C#, Windows Forms, .NET 8, and NAud<br>-12,0 +16 A customizable Windows soundboard built with C#, Windows Forms, .NET 8, and NAud<br>-15,0 +20 A customizable Windows soundboard built with C#, Windows Forms, .NET 8, and NAud<br>-35 +40 To build and run directly from source instead, use `BUILD-AND-RUN.bat` or open `<br>-72 +77,4 CI (`.github/workflows/build.yml`) runs the tests on Ubuntu and does a warning-f |
| `EmiliosSoundboard-v2.0.0-GitHub-Source-Fixed/EmiliosSoundboard-v2.0.0/RELEASE-NOTES.md` | +146 / -0 | -95,0 +96,146 This is the single, cumulative patch-notes file. New changes should be appended |
| `EmiliosSoundboard-v2.0.0-GitHub-Source-Fixed/EmiliosSoundboard-v2.0.0/START-HERE.txt` | +6 / -2 | -1 +1<br>-12 +12 This is a source package, not a prebuilt EXE. The installer publishes a self-con<br>-15,0 +16,4 If the build fails, copy the full error output before closing the command window |
| `README.md` | +11 / -7 | -5,2 +5,2<br>-11 +11 A customizable Windows soundboard with a space-inspired UI, personal sound libra<br>-32,0 +33 A customizable Windows soundboard with a space-inspired UI, personal sound libra<br>-34 +35,3 A customizable Windows soundboard with a space-inspired UI, personal sound libra<br>-37 +40,2 A customizable Windows soundboard with a space-inspired UI, personal sound libra<br>-41 +45 A customizable Windows soundboard with a space-inspired UI, personal sound libra<br>-162 +166 Choose and add a license file before accepting outside contributions or distribu |

## New files

- `EmiliosSoundboard-v2.0.0-GitHub-Source-Fixed/EmiliosSoundboard-v2.0.0/.editorconfig`
- `EmiliosSoundboard-v2.0.0-GitHub-Source-Fixed/EmiliosSoundboard-v2.0.0/EmiliosSoundboard/ColorWheelDialog.cs`
- `EmiliosSoundboard-v2.0.0-GitHub-Source-Fixed/EmiliosSoundboard-v2.0.0/EmiliosSoundboard/PlatformServices.cs`
- `EmiliosSoundboard-v2.0.0-GitHub-Source-Fixed/EmiliosSoundboard-v2.0.0/EmiliosSoundboard/SpaceBackdropPanel.cs`
- `EmiliosSoundboard-v2.0.0-GitHub-Source-Fixed/EmiliosSoundboard-v2.0.0/ITCH-IO-DESCRIPTION.md`
- `EmiliosSoundboard-v2.0.0-GitHub-Source-Fixed/EmiliosSoundboard-v2.0.0/LINUX-PORTING.md`
- `EmiliosSoundboard-v2.0.0-GitHub-Source-Fixed/EmiliosSoundboard-v2.0.0/VOICE-CHANGER-DUMB-DUMB-GUIDE.md`
- `tools/merge-report.ps1`
- `tools/runtime-selftest.ps1`
- `tools/verify-merge.ps1`

## Verification

- `tools\verify-merge.ps1`: installs a user-local .NET 8 SDK if needed, restores, builds and tests.
- `tools\runtime-selftest.ps1`: launches the app with `--selftest`, waits for it to close, and prints only the self-test lines.
- Result: build succeeded with 0 warnings; 79 tests passed; runtime self-test 10/10 steps passed.
- Runtime self-test covers: startup + first paint, 4 playback devices, silent playback probe, custom accent, Comic Book Mode on/off, fixed/unlocked resolution, Settings render, Voice FX render, and graceful close.
- Still not covered automatically: user-imported sounds/cards, real microphone processing, and a real Discord/OBS virtual-cable route.
