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

## v2.0.1 BETA merge into the BETTER fork (2026-10-28)

- Merged the v2.0.1-BETA feature set: Comic Book Mode, custom accent colour wheel, fixed window resolutions, larger spinning title, refreshed search UI, restructured Voice FX with TEST MIC, fine pitch tuning, AUTO ROUTE and GoXLR endpoint labels, VB-CABLE guidance, space backdrops on dialogs, and mirrored local playback for routed clips.
- Kept the fork's output routing by device name, logging/diagnostics and split project structure.
- Voice FX has its own chat output under `Settings > VOICE FX OUTPUT (CHAT)`. Soundboard routing stays independent.
- Soundboard clips routed to a non-default endpoint are also played on the Windows default output so they remain audible locally; if the selected route fails, playback falls back to local output.
- New source files: `ColorWheelDialog.cs`, `SpaceBackdropPanel.cs`, `PlatformServices.cs`.
- Verified in a clean build: 0 warnings, 0 errors; 79 tests passing.
## v2.0.0 BETA — Settings, Voice FX, UI and portability groundwork (2026-10-27)

- Added fixed main-window resolutions in Settings: 1280×720, 1024×576 and 960×540, plus an unlocked/resizable option. The smaller presets are intended to reduce the amount of UI area being rendered on slower PCs.
- Added a persistent version footer to Settings: **Version 2.0.0 BETA**.
- Added a custom HSV colour wheel and brightness control for accent colours. Custom accent values are stored as hexadecimal colours.
- Applied accent colours more consistently to the search field frame, sorting controls, search panel border, progress bar, cursor glow and space haze.
- Fixed the cursor-glow bitmap cache so it is recreated after the accent colour changes.
- Increased the main title size, changed it to plain white, and added a one-second spin easter egg when the title is clicked.
- Moved **STOP SOUND** directly above **+ ADD SOUND** and added a small scale/glow response to hover and click.
- Added more responsive hover and press animations to the shared dialog buttons and Voice FX buttons.
- Reworked Voice FX setup copy and capture configuration. Added **TEST MIC**, a live input-level meter, and a silent test mode that checks microphone signal without playing it back. Live processing still needs verification with real microphones and output devices; do not treat it as fully validated until tested on Windows.
- Added a small platform-services layer for app-data paths and opening the sound library folder. Linux uses `XDG_DATA_HOME` (or `~/.local/share`) for data and `xdg-open` for folders where those helpers are used.
- Added `LINUX-PORTING.md` documenting the remaining blockers. **Linux is not supported by this build yet**: the UI still targets Windows Forms/WPF and the live voice path uses Windows-oriented audio APIs.
- Updated README feature, BETA status and Linux roadmap sections. Added an `.editorconfig` for consistent source formatting.
- This environment does not have the .NET SDK installed, so the project could not be compiled or run here. Please build and test this source ZIP on Windows, especially window locking, colour changes and Voice FX.


## Build Fix — v2.0.0 BETA

- Fixed the Voice FX button hover variable name collision that prevented compilation in `MainForm.Paint.cs`.
- Removed unused local variables in the card layout method.
- Renamed the custom form DPI scale field to avoid hiding the inherited `Control.Scale` member.
- Moved high-DPI configuration from the manifest to the Windows Forms project property (`PerMonitorV2`).
- These changes address the reported compile error and warnings; a fresh build on Windows is still needed to verify the full installer.


## v2.0.0 BETA — Voice FX audio-format fix and GoXLR endpoint support (2026-10-27)

- Fixed the reported `Must be already floating point` Voice FX startup error: the capture stream is PCM 16-bit, so the pipeline now uses NAudio's `Pcm16BitToSampleProvider` instead of `WaveToSampleProvider`, which expects IEEE-float input.
- Replaced legacy WaveIn/WaveOut device listing with Windows Core Audio (WASAPI) endpoint enumeration and playback/capture. This exposes active Windows recording and playback endpoints, including GoXLR / TC-Helicon virtual devices when the Windows audio driver publishes them.
- Added a **REFRESH** button to Voice FX so connected GoXLR or other endpoints can be discovered without closing the window. Matching device names are labelled `[GoXLR]` and are sorted to the top of the lists.
- Configured microphone capture to use a predictable 44.1 kHz, 16-bit mono PCM stream and duplicate processed output to stereo before sending it to the selected playback endpoint.
- Replaced the disabled GoXLR placeholder in Settings with a functional setup guide explaining device selection and safe routing.
- Scope note: this is Windows audio-endpoint integration, not direct hardware control. GoXLR faders, lighting, hardware effects and profile management are not controlled by Emilio's Soundboard.
- The change has not been compiled or run on a Windows machine in this environment. Please rebuild and test with a real microphone and GoXLR device; shared-mode endpoint support may still depend on the specific driver and routing configuration.


## v2.0.0 BETA — Virtual Audio Routing Update

- Added a soundboard output selector in Settings to route sound clips to a chosen Windows render endpoint, including supported virtual audio cable and GoXLR endpoints.
- Updated sound playback to use endpoint-targeted WASAPI output.
- Made Voice FX a modeless window so sound clips can be triggered while live voice processing is running.
- Renamed the live-processing control to a clear Voice Changer ON/OFF action.
- Voice FX remembers its selected output endpoint and shares that route with the soundboard output setting.
- Clarified Voice FX start/stop control labels and added more direct routing guidance for Discord and WhatsApp.
- Added virtual-cable route labels to recognized endpoints.
- Added README setup steps for routing soundboard audio and processed voice to chat apps.

**BETA limitation:** This app routes to Windows audio endpoints; it does not create or install a virtual microphone by itself. A compatible virtual cable or correctly configured GoXLR routing is required, and the end-to-end setup must be tested on the target PC.


## v2.0.0 BETA — Voice FX / Settings Space UI and chat routing polish (2026-10-27)

- Enabled Windows dark title-bar styling for Voice FX and explicitly set a deep-navy caption with white title text and an accent border.
- Added animated space backdrops to Voice FX, Settings, and other themed dialogs, including soft nebula clouds, drifting/twinkling stars, and subtle orbital detail.
- Replaced the standard Voice FX action buttons with rounded animated glow buttons and refreshed the device selector styling.
- Improved the refresh action: it now rescans Windows audio endpoints and resumes an active voice stream or silent mic test when device enumeration succeeds.
- Added **AUTO ROUTE** to select a detected VB-CABLE, Voicemeeter or GoXLR playback endpoint. The selected endpoint is shared by the soundboard and Voice FX, so sound clips and processed microphone audio use the same route. If a virtual cable is selected, local monitoring may need separate mixer/Windows configuration.
- Changing the microphone or chat output while Voice FX is active now restarts the processing stream on the newly selected endpoint.
- Updated the live status to distinguish a recognised chat route from a normal local speaker output, and gives an explicit warning when no virtual/mixer route is detected.
- Redesigned the Sound Library search field with a rounded dark frame, dynamic accent border, clearer heading and animated rounded sorting chips. The chips follow the selected accent colour.
- Added animated space backgrounds to the Settings scroll surface and used the same visual system for themed dialogs.
- Documented chat-routing setup. A virtual audio driver such as VB-CABLE or a configured GoXLR / Voicemeeter route is still required; the app cannot create a virtual microphone driver by itself.
- This source was not compiled or run in this environment because the .NET SDK is unavailable here. Build and test on Windows, including routing into Discord / WhatsApp with the selected virtual recording endpoint.


## v2.0.0 BETA — Comic Book Mode, Rainbow Road polish and audio monitoring

- Updated the developer profile link throughout the source to [YeskruserDev](https://github.com/YeskruserDev).
- Fixed accent-colour changes not refreshing the Windows title-bar border; re-applies the current accent to open windows and refreshes Voice FX accent controls. Windows border colours depend on Windows/DWM support.
- Made the **COOL MODE • RAINBOW ROAD** indicator animate through rainbow colours while Cool Mode is enabled.
- Added a **Comic Book Mode** toggle in Settings. It adds halftone space dots, comic speed lines, heavy black outlines, pop-art button colours, and bolder Comic Sans typography across the main UI, Settings, and Voice FX.
- Improved soundboard playback when routing to a non-default Windows endpoint: clips play through the selected route and are also mirrored to the Windows default output, so they can be heard locally while being sent to a virtual cable or mixer. If the selected route fails, the soundboard attempts local playback and reports the fallback.
- Kept the Voice FX output route separate from local soundboard monitoring; processed voice is sent to its selected output and may need a mixer/sidetone configuration to be heard locally.
- Added matching comic styling to the search controls, Voice FX pitch slider, microphone meter, Settings background, and animated controls. Halftone drawing reuses brushes to reduce per-frame graphics allocations.
- Updated README and itch.io description to document the modes and audio-monitoring behaviour.

**BETA validation note:** This source package was not compiled or run in this environment because the .NET SDK/compiler is unavailable. Please run the Windows installer/build script and test sound playback, local monitoring plus virtual-cable routing, colour changes, Rainbow Road, and Comic Book Mode before publishing.


### BETA follow-up — Audible local monitoring and comic polish

- Adjusted soundboard playback to use the normal Windows default output path when that output is selected.
- When routing clips to a separate virtual cable or mixer endpoint, a local monitor copy is also played so the user can hear the effects.
- If Windows cannot compare selected and default endpoint IDs, the app now favours local monitoring rather than risking silent routing.
- Added animated cursor-orbit sparkles and extra shimmer on the Add Sound button.
- Added a client-area accent frame as a fallback on Windows versions that do not apply DWM border colours; accent changes now request a non-client frame redraw.
- Extended Comic Book Mode styling to the Voice FX button, Add Sound, Stop Sound, and playback progress track.
- Updated repository links to https://github.com/YeskruserDev.

These source changes still need a successful Windows build and audio-device runtime test.


## v2.0.0 BETA — Clean window edges

- Removed the decorative accent-colour outline around the main soundboard, Voice FX, Settings, and themed dialog backgrounds.
- Removed the comic-mode frame drawn around the entire app window while keeping comic styling on individual controls.
- Stopped forcing the Windows/DWM window-border colour; the space backgrounds now run cleanly to the edges.


## v2.0.0 BETA — Voice FX controls and monochrome Comic Mode

- Locked the Voice FX and Settings windows to fixed dimensions while retaining scrolling within the Settings content.
- Expanded pitch adjustment to -24 to +24 semitones and added independent -100 to +100 cent fine tuning. Presets now save fine tuning; older presets default to 0 cents.
- Updated Voice FX controls with a more mixer-inspired layout and a live fine-tune readout.
- Reworked Comic Book Mode into a monochrome ink-and-halftone look across the main screen and themed utility windows.
- Added a button linking to the official VB-Audio VB-CABLE download page and clarified setup requirements. The driver is not bundled because it is a Windows audio driver that must be installed separately.
- Source-only update; build and real microphone routing still need verification on Windows.


- Extended strict monochrome treatment to animated settings labels, toast notifications, the desktop pet, sound-card action controls, and pitch sliders.
- Fixed the Comic Book Mode empty-state halftone clip so its dots remain inside the card without clearing the main sound-grid clip.


## v2.0.0 BETA — Build error fixes

- Corrected `ProcessStartInfo` object initializer syntax in Settings and Voice FX so `UseShellExecute` is assigned to the process-start info object.
- Changed the desktop-pet grayscale drawing call to use the `Rectangle` overload accepted by the `ImageAttributes` overload of `Graphics.DrawImage`.
- Renamed the comic empty-state planet centre variables to avoid the C# local-variable name collision (`cx` / `cy`).
- These fixes address the five compiler errors reported by the previous Windows build log. The project still needs to be rebuilt locally to confirm the full build succeeds.
## Voice FX UI and performance polish

- Removed decorative outlines from Voice FX section panels while preserving the dark grouped-panel backgrounds.
- Stopped continuously repainting the entire Voice FX window and Settings starfield; both space backdrops are now static while audio processing runs, reducing avoidable rendering load and text flicker.
- Standardized Voice FX controls on Segoe UI for cleaner, more stable small-text rendering in all themes. Comic Book Mode retains its monochrome surfaces and controls.
- Reduced shared hover/press animation update frequency from 60 Hz to roughly 33 Hz; the animation timer still stops when the effect settles.
- Limited theme refreshes to the controls that need repainting instead of forcing recursive invalidation of the entire Voice FX tree.



## v2.0.0 BETA — Voice FX audio-device lock handling

- Changed Voice FX output to shared-mode WASAPI with timer-driven buffering to improve compatibility with some Windows audio drivers.
- Added short retries when Windows reports `AUDCLNT_E_DEVICE_IN_USE` during output initialization.
- Replaced the generic error for `0x8889000A` with targeted guidance to disable exclusive-mode control on the microphone and playback endpoints.
- Expanded the dumb-dumb setup guide with recovery steps for this Windows audio error.
- Requires Windows rebuild and real-device testing; the environment cannot compile the application.
