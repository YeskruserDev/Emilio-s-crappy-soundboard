# Emilio's Soundboard — v1.4.4

A customizable Windows soundboard built with C#, Windows Forms, .NET 8, and NAudio.

Features include a space-themed animated interface, searchable sound cards, custom backgrounds, configurable startup media, pitch-based live voice effects, and a hidden Rainbow Road mode.

## Requirements

- Windows 10 or Windows 11
- .NET 8 SDK to build from source
- Internet connection on first build to restore the NAudio NuGet package

## Build and run

1. Download or clone this repository.
2. Open `EmiliosSoundboard.sln` in Visual Studio 2022, or run `BUILD-AND-RUN.bat`.
3. If building from Visual Studio, select the `EmiliosSoundboard` project as the startup project and build in Release configuration.

## Publish a self-contained Windows build

Run `PUBLISH-PORTABLE.bat` on Windows. The script creates a self-contained x64 publish folder and ZIP for distribution. Test the published app before sharing it.

## Media assets

To keep this public source package clear of third-party media that may have separate copyright restrictions, it does not bundle Mario artwork, Mario Kart music, or the Windows XP startup sound. The app works without these optional files. Users can select their own startup sound and media in Settings.

If you own or have permission to redistribute suitable assets, optional files can be placed beside the built executable using these names:

- `MarioKart.png` — optional sprite for Cool Mode
- `RainbowRoad.mp3` — optional Cool Mode music
- `WindowsXPStartup.mp3` — optional bundled-style startup sound

Only distribute media you have rights to use. Missing optional files are not included in the project build.

## User data

The app stores settings and imported sound library data under `%LOCALAPPDATA%\EmiliosSoundboard`, separate from the source folder.

## Version status

Initial public source release, version 1.4.4. The source has not been independently build-verified in this packaging environment. Please report build errors with the complete terminal output.
