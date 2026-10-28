# Emilio's Soundboard v2.0.1 BETA

A customizable Windows soundboard built with C#, Windows Forms, .NET 8, and NAudio.

## Features

- Animated space-themed interface and searchable sound cards with rounded sorting chips
- Comic Book Mode: monochrome ink outlines, halftone dots, pop-art controls and bolder typography
- Custom accent colour wheel, plus preset accents that recolour the search UI, glow, progress and labels
- Fixed window-resolution presets (1280x720, 1024x576, 960x540) for slower PCs, or unlocked/resizable
- Customizable startup image/video and startup sound
- Cool Mode with Rainbow Road media and an animated rainbow mode label
- Desktop pet with custom image, adjustable size, wandering, pauses, looking around, jumps, and spins
- Voice FX with coarse/fine pitch, TEST MIC level meter, presets, AUTO ROUTE and GoXLR / TC-Helicon endpoint labels
- Sound output device routing (global and per sound), so a virtual cable can feed Discord or OBS
- Local monitoring: clips routed to a non-default endpoint are also played on the Windows default output
- Rolling log file with a diagnostics panel in settings
- Discord Rich Presence integration
- Optional Mica / Acrylic window material on the settings window (Windows 11 22H2+)
- Windows dark title bars and animated space backdrops on themed dialogs
- User settings and imported sound library stored separately from the app under `%LOCALAPPDATA%\EmiliosSoundboard`

## Requirements

- Windows 10 or Windows 11, 64-bit
- .NET 8 SDK to build from source
- Internet connection on first build to restore NuGet packages

## Quick start

1. Download and extract this repository.
2. Install the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) if it is not installed.
3. Double-click `INSTALL-AND-CREATE-DESKTOP-SHORTCUT.bat` to publish a self-contained Windows x64 app and create a desktop shortcut.
4. Launch **Emilio's Soundboard** from the shortcut.

To build and run directly from source instead, use `BUILD-AND-RUN.bat` or open `EmiliosSoundboard.sln` in Visual Studio 2022.

## Create a GitHub release build

On Windows, run `PUBLISH-PORTABLE.bat`. It creates `release\EmiliosSoundboard-v2-win-x64\` and, when PowerShell is available, `release\EmiliosSoundboard-v2-win-x64.zip`. Test the app before uploading that ZIP to a GitHub Release tagged `v2.0.1`.

## Adding sounds

Use **+ ADD SOUND** inside the app to import your own audio. You can find sound effects on [Pixabay](https://pixabay.com/sound-effects/), [Mixkit](https://mixkit.co/free-sound-effects/), and [Freesound](https://freesound.org/). Review the license for each file and ensure you have rights to use or redistribute it. “Free to download” does not automatically mean a sound can be redistributed.

## Optional media and rights

The current source package includes `MarioKart.png` and `RainbowRoad.mp3`. Only redistribute these or any custom startup/pet media if you have the necessary rights. `WindowsXPStartup.mp3` is optional and is not bundled by default. Users can choose their own startup and pet media in Settings.

## Patch notes

All historical and future patch notes are maintained in the single [`RELEASE-NOTES.md`](RELEASE-NOTES.md) file. Add new entries there rather than creating separate patch-note files.

## Project layout

```
EmiliosSoundboard.Core/    library format + storage (plain .NET 8, no UI, no System.Drawing)
EmiliosSoundboard/         WinForms app: painting, audio, dialogs, image pipeline
EmiliosSoundboard.Tests/   xunit tests for the core
```

## Build and test

```bat
dotnet build EmiliosSoundboard.sln -c Release
dotnet test EmiliosSoundboard.Tests/EmiliosSoundboard.Tests.csproj -c Release
```

The solution builds on Linux and macOS as well. The app project sets `EnableWindowsTargeting`, so it cross-compiles anywhere, but the UI itself only runs on Windows. The core and its 79 tests are platform neutral and run on any OS.

CI (`.github/workflows/build.yml`) runs the tests on Ubuntu and does a warning-free build of the whole solution plus the tests on Windows.

## Build status

- Builds clean: 0 warnings, 0 errors with the .NET 8 SDK.
- Core tests: 79 passing.
- Runtime self-test: 10/10 steps passed (startup and first paint, playback-device discovery, silent playback, accent, Comic Book Mode, window resolutions, Settings render, Voice FX render, graceful close).
- Not yet automated: user-imported cards, live microphone processing and a real Discord/OBS virtual-cable route. Those still need a manual test with real audio devices.

Run the runtime smoke test with `EmiliosSoundboard.exe --verbose --selftest`; it closes itself when finished. The helper scripts in `tools/` automate the SDK install, build, test and this smoke test.
