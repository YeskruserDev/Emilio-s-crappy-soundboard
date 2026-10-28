# Emilio's Soundboard — Release Notes

This is the single, cumulative patch-notes file. New changes should be appended here; do not create separate release-note files for future updates.

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
