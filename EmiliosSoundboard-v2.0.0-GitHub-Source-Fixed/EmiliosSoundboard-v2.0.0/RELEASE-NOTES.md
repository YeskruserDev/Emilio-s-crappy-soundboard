# Emilio's Soundboard — Release Notes

This is the single, cumulative patch-notes file. New changes should be appended here; do not create separate release-note files for future updates.

## Output device routing and a real logging system (2026-10-28)
- **Sound output device selection.** Sounds used to go to the Windows default speaker with no way to change it, which meant the board could not feed Discord or OBS. The settings window now has a `SOUND OUTPUT DEVICE` picker (`System default` plus every playback device, with a refresh button), and each sound can override it from its edit dialog.
- Devices are stored by **name**, not index: the index Windows reports changes when hardware is plugged in, and a stale index silently plays into the wrong device.
- If a saved device disappears (headset unplugged, driver renamed) the app falls back to the system default, says so on the settings screen, and logs a warning instead of failing. If a device refuses to open, playback retries once on the default.
- Cool Mode music and the startup sound follow the board device as well.
- **Logging.** New `AppLogger`: daily files in `%LOCALAPPDATA%\EmiliosSoundboard\logs\app-YYYY-MM-DD.log`, size-based rollover, 7-day retention, level filtering, a 400-entry in-memory ring, and an exception formatter that keeps the type, message, stack and inner exceptions.
- Logging never throws. A failed write is recorded in `LastWriteError` and the app continues.
- Wired to `Application.ThreadException`, `AppDomain.UnhandledException` and `TaskScheduler.UnobservedTaskException`, so crashes and unobserved background failures land in the log instead of vanishing. The old single `error.log` writer is gone.
- Failure paths now log instead of swallowing: library load/save, imports, playback, Discord presence. Startup logs the version, OS, library count and chosen device.
- Settings has a `DIAGNOSTICS` section: a `Detailed logging` switch, a one-line log summary, `OPEN LOG FOLDER` and `COPY RECENT LOG` (last 200 lines to the clipboard). `--verbose` on the command line does the same at startup.
- Tests: 50 -> 79. New coverage for log line format, level filtering, newline collapsing, exception formatting, ring capacity, rollover, retention purge, a blocked log folder, thread safety, the static facade without a logger, and device-name resolution (exact, case-insensitive, truncated, ambiguous, missing).
- Unverified on Windows: the device list itself, actual playback routing to a virtual cable, and the new settings layout. All of it is compile-verified plus unit tests on the pure parts.

## Fullscreen performance pass and window material (2026-10-28)
- The backdrop no longer re-renders its haze every frame. The drifting haze was four oversized, bilinear-filtered, alpha-blended blits, roughly 22 megapixels per frame at 1080p. It is now rendered into a cached bitmap about eight times a second and blitted 1:1 in between. While a window is being resized, painting goes straight to the screen instead of allocating a fullscreen bitmap per frame. If the cache cannot be allocated, painting falls back to the old path.
- The visible card list is built once per frame instead of through three LINQ queries plus a sort for every tick and every paint. Offscreen cards were already skipped when drawing.
- The LENGTH sort no longer opens an audio file per comparison. Durations are cached per file.
- Star tint is cached per accent colour. `ControlPaint.Light` used to run for every star, every frame.
- Removed per-frame GDI+ allocations: the Rainbow Road loop was creating one `SolidBrush` per road segment (about 99 per frame), and the header, footer, Voice FX button, cool-mode label and title auto-fit were each building and disposing fonts every frame.
- Added a `WINDOW EFFECT` setting in the settings window: `Mica`, `Acrylic` or `Off`. It applies the Windows 11 DWM system backdrop plus rounded corners to the settings window. The material is drawn by the compositor, so it shows on the window frame and title bar; the painted content area stays opaque because WinForms GDI+ cannot composite over it. On Windows 10 and older Windows 11 builds the call is ignored and the dialog looks exactly as before.
- The stored value is `WindowEffect` in `library.xml`. Older files without it load with `Mica`.
- Still unverified: nobody has run this on Windows yet. The frame-rate improvement and the Mica appearance are not measured; both need a real window on Windows 11 22H2 or newer.

## Structure cleanup, warning-free build, first test suite (2026-10-28)
- Split the 1361-line `MainForm.cs` into partial files by job: `MainForm.cs` (state, startup, lifecycle), `MainForm.Library.cs` (cards, search, sorting, import, edit, delete), `MainForm.Animation.cs` (the 60 Hz loop, space field, desktop pet), `MainForm.Input.cs` (hit testing, mouse and keyboard), `MainForm.Playback.cs` (playback, progress, toasts, settings). No drawing or event code was rewritten; member bodies moved verbatim.
- Added `EmiliosSoundboard.Core`, a platform-neutral .NET 8 project holding the library format and storage. Image decoding stays in the app behind the new `IImageImporter` seam, so the storage layer no longer depends on System.Drawing.
- `DataStore` can now be pointed at an explicit folder (`new DataStore(root)`), which is what makes the storage tests possible.
- Fixed all 6 compiler warnings: renamed `ThemedForm.Scale` to `ScaleFactor` (it hid `Control.Scale`), used the existing `margin` constant in `LayoutCards` instead of a duplicated literal, removed the unused `left` local, removed the unused `wet` and `save` fields in Voice FX, moved DPI awareness from `app.manifest` into `ApplicationHighDpiMode` (SystemAware, same behaviour as before).
- Deleted dead code with no callers: `MainForm.LiveCount`, `MainForm.ContentWidth`, `Theme.MakeLightFont`, `Theme.Lerp`, `Theme.FormatTime`, `ImageUtil.MakeCover`.
- Added `EmiliosSoundboard.Tests` (xunit, 32 tests) covering the library round-trip, corrupt-file recovery, sanitising, unique names, import, delete safety and byte formatting. The tests run on Linux and Windows.
- Added a GitHub Actions workflow: tests on Ubuntu, warning-free build of the whole solution plus tests on Windows.
- The WinForms UI still has not been run on Windows. Layout, Voice FX with real devices and Discord presence remain runtime-unverified.

## Latest update — Search UI refresh
- Removed the liquid-glass appearance from the search panel.
- Replaced it with a clean, opaque midnight-navy panel, restrained cyan top accent, and subtle shadow.
- Updated the search heading, text field, and sorting buttons to a consistent Segoe UI / dark-navy / cyan-accent style.
- Made the active sorting mode clearer and reduced harsh outlines and glow effects.
- Search and sorting behaviour remains unchanged.

## Desktop pet
- Added Settings controls to enable/disable the pet, choose or clear a custom image, and adjust size up to 300%.
- Added slow wandering, occasional pauses, look-around tilts, varied jumps, mid-air spins, walking bob, landing squash, and a shadow that responds to jump height.
- The pet is rendered inside the app window, not as a separate Windows desktop overlay.

## Main UI and sound playback
- Added a STOP SOUND control and removed the Ready / Playing / Done Playing footer status messages.
- Kept the progress track along the bottom and aligned the Add Sound control with the footer layout.
- Refined the header layout to reserve space for Voice FX and Settings controls.

## Cool Mode
- Includes the Rainbow Road visual and Cool Mode audio asset.
- Includes the Mario kart image asset and repeated movement animation.
- Cool Mode media files are configured as project content for copying to build output.

## Voice FX
- Added explicit audio sample-format conversion/checks before pitch processing and improved diagnostics for startup failures.
- The live microphone pipeline still requires runtime testing on Windows with the user's audio devices; do not assume the error is fully resolved until tested.

## Discord Rich Presence
- Configured the supplied Discord Application ID: `1564710814374436884`.
- Discord desktop must be running for the activity to appear. If it does not appear, restart Discord and the soundboard and check Discord activity settings.
- No Discord user token or password is used.

## Build and verification
- This is a source package for Windows and .NET 8. Build using `BUILD-AND-RUN.bat` or open `EmiliosSoundboard.sln` in Visual Studio.
- The project has not been compiled or run in this environment; test the build and runtime behaviour on Windows and report any compiler output or error dialogs.


## v1.4.7 — Startup media sizing and fade (2026-10-27)
- Startup photo and video now appear in a centered popup sized to the soundboard window (within the screen work area), instead of taking over the entire screen.
- Added a short fade-in and fade-out transition for startup media.
- Startup photo display lasts 4.5 seconds; the startup audio is stopped when the image closes so it does not continue playing after the image disappears.
- Startup video closes with a fade after playback ends or fails, and the startup audio is stopped at the same time.
- Kept all patch history in this single `RELEASE-NOTES.md` file.


## v2.0.0 — Packaging and installer update (2026-10-27)
- Fixed desktop shortcut creation to use the actual Windows Desktop known-folder path, with a OneDrive Desktop fallback.
- Avoided hard-coding the Desktop path inside PowerShell; passed the executable and working directory through environment variables.
- Installer now publishes a self-contained Windows x64 app before creating the shortcut and verifies that the EXE exists.
- Updated build/run and portable publish scripts with clearer checks, consistent v2 output names, and actionable errors.
- Updated README and quick-start instructions for the v2.0.0 GitHub source package and release workflow.
- Future patch notes remain consolidated in this file.
- Build and runtime still need verification on Windows; this packaging environment cannot compile the WinForms app.


## v2.0.0 build-fix update

- Fixed ambiguous `Brushes` reference in `StartupVideoSplash.cs` by explicitly using `System.Windows.Media.Brushes.Black`.
- Updated `DiscordRichPresence` package reference to `1.2.1.24`, the version resolved by NuGet, to avoid the NU1603 version approximation warning.
