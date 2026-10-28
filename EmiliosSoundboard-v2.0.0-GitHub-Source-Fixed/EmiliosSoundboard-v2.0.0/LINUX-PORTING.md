# Linux porting status

## Current status

**Linux is not supported in this BETA build yet.** The portability changes in this branch are groundwork only, not a claim that the application can be compiled or launched natively on Linux.

The app currently targets `net8.0-windows` and uses Windows Forms for most of its UI, WPF media components for startup video, and NAudio's `WaveInEvent` / `WaveOutEvent` path for live voice processing. The custom drawing, dialogs, native window-title styling and other Windows-specific UI behaviours also need a proper cross-platform replacement.

## Portability groundwork included

- `PlatformServices.GetAppDataDirectory()` uses `XDG_DATA_HOME` on Linux and falls back to `~/.local/share`, while preserving `%LOCALAPPDATA%` behaviour on Windows.
- `PlatformServices.OpenFolder()` selects `explorer.exe`, `open`, or `xdg-open` according to the OS.
- Sound-library XML and files remain behind `DataStore`, which is a useful boundary for preserving library data when the UI is ported.

These helpers can be used after the project has a compatible Linux target. They do not remove Windows-only dependencies from the current executable.

## Recommended migration sequence

1. **Separate UI from application services.** Keep sound-library logic, settings, sound metadata and import rules outside the UI layer.
2. **Port the UI.** Replace WinForms/WPF forms and `System.Drawing`-specific controls with a cross-platform UI framework such as Avalonia. Recreate the sound cards, background animation, settings, colour wheel, startup media and Voice FX controls.
3. **Replace the audio backend.** Use an audio capture/output backend that supports Linux. Validate PipeWire and PulseAudio device discovery, microphone permissions, buffering, latency, sample format conversion and pitch shifting. Virtual cable routing differs between Linux distributions and must be documented rather than assumed.
4. **Audit Windows-only behaviour.** Replace the DWM title-bar call, Windows installer scripts, shortcut creation and Windows-specific publishing paths with per-platform implementations.
5. **Validate integrations.** Test Discord Rich Presence, audio device switching, startup media and persisted settings on supported Linux distributions.
6. **Add automated and real-device testing.** Build a Linux target on Linux, test with and without audio devices, and publish a Linux package only after a clean build and runtime validation.

## Do not use the Windows scripts on Linux

`BUILD-AND-RUN.bat`, `PUBLISH-PORTABLE.bat` and `INSTALL-AND-CREATE-DESKTOP-SHORTCUT.bat` are Windows scripts. A Linux build script should be added only after the project has a Linux-compatible UI and audio backend.
