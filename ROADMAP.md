# Roadmap

What's missing, what's broken, and what would make this the soundboard people actually keep open.

**Status first:** the app builds clean and the core has 50 passing tests, but it has **never been run on Windows** by anyone on this project. Every visual claim in the repo is compile-verified only. That single fact outranks everything below, because a roadmap you can't test against is a wish list.

**Sizes:** `S` = under a day, `M` = a few days, `L` = a week or more, `XL` = multi-week.
**Verify** = how you'll know it's actually done, not just written.

**Done since this file was written:** P0-2 (output device routing, global + per sound) and P4-3 (in-app diagnostics: open the log folder, copy recent lines). Both are implemented and unit-tested where the logic is platform-neutral, and both still need a run on real Windows hardware. The rows below are left in place so the reasoning stays readable.

---

## If you only do five things

| # | Do this | Why it's first |
|---|---|---|
| 1 | Run it on Windows and note what breaks | Nothing below can be trusted until someone has seen the window |
| 2 | Sound output device routing | Without it the app plays out of your speakers and is unusable in Discord, OBS or any voice chat |
| 3 | Global hotkeys + always-on-top + tray icon | A soundboard you have to alt-tab to is a demo, not a tool |
| 4 | Ship a signed installer and a release zip | A friend can't install this today without reading `.bat` scripts and installing the .NET 8 SDK first |
| 5 | Frame-time overlay (F12) | Turns "it's laggy" into a number so the next performance pass is measured, not guessed |

---

## P0 - blockers (nothing else matters until these are true)

| ID | Item | Why | Size |
|---|---|---|---|
| P0-1 | **Actually run it on Windows.** Win11 VM or the friend's PC, click through every screen, record what breaks | 3,000 lines of UI painting and audio code with zero runtime passes | `S` |
| P0-2 | **Playback device selection.** `AudioPlayer` calls `new WaveOutEvent()` with no `DeviceNumber`, so every sound goes to the Windows default device. Add a device picker (default + per-sound override) using `MMDeviceEnumerator` / `WasapiOut` | This is *the* soundboard feature. You route sounds to a virtual cable (VB-Cable, VoiceMeeter) so Discord hears them while your mic still sounds like you. Without it the app is a speaker toy | `M` |
| P0-3 | **Global hotkeys.** `RegisterHotKey` per card, assignable in the card menu, working while another window has focus | Alt-tabbing mid-game defeats the purpose. Every real soundboard has this | `M` |
| P0-4 | **Always-on-top + compact mode + tray icon + single instance.** `TopMost` toggle, a small always-visible strip of buttons, `NotifyIcon` to minimise to tray, `Mutex` to prevent a second copy | Two instances both write `library.xml`; `File.Replace` means last one wins and the other's changes vanish. That's silent data loss | `M` |
| P0-5 | **Installer + release.** MSIX or Inno Setup, plus the existing portable zip. Document code signing (or at least why SmartScreen warns) | The repo is source-only today: `.bat` scripts and a `.NET 8 SDK` requirement | `M` |
| P0-6 | **Add a `LICENSE`.** The README asks contributors to add one before accepting contributions; there still isn't one | Legally the project is "all rights reserved" right now, which contradicts a public repo inviting PRs | `S` |
| P0-7 | **Audio device robustness.** Handle unplug/replug, device in use, sample-rate mismatch, and surface the error instead of silently stopping | `WasapiOut`/`WaveOutEvent` failures currently end up as a toast at best | `M` |

---

## P1 - soundboard core (what makes it a real soundboard)

| ID | Item | Why | Size |
|---|---|---|---|
| P1-1 | **Per-sound volume** (gain in dB, not just a global slider) | Memes are mastered at wildly different levels; one loud one ruins the bit | `S` |
| P1-2 | **Normalise on import** (peak/RMS to a target) | Fixes the above automatically at the source | `M` |
| P1-3 | **Playback mode per sound:** interrupt / overlap / loop / hold-to-play | "Stop current sound" is global today; you want a screaming sound to overlap and a music bed to loop | `M` |
| P1-4 | **Trim start/end + fade in/out** | Most downloaded clips have dead air at both ends. This is the #1 daily annoyance of imported sound files | `M` |
| P1-5 | **Speed and pitch per sound** (reuse `SmbPitchShiftingSampleProvider` from Voice FX) | Turns 3 sounds into 12. Cheap given the provider is already in the project | `S` |
| P1-6 | **Drag and drop import.** No `AllowDrop` anywhere in the project | Dragging 20 files onto the window is the normal way people add sounds | `S` |
| P1-7 | **Duplicate detection on import** (file hash) + "clean up unused files" in settings | Sound folders rot into 400MB of near-duplicates | `S` |
| P1-8 | **Keyboard control of the board.** Only scrolling keys are handled today: Space = play selected, Delete = delete, Ctrl+F = search, arrows = move selection | Currently the app cannot be driven without a mouse | `M` |
| P1-9 | **Microphone passthrough / mixing** - play sounds *into* the output device your voice goes to, so no virtual cable is needed | The genuinely hard one: needs WASAPI loopback or a bundled virtual device. Flag it as `M`→`L` and expect driver issues | `L` |

---

## P2 - library and everyday UX

| ID | Item | Why | Size |
|---|---|---|---|
| P2-1 | **Folders / collections / tags**, with the left sidebar becoming a real tree | Sort modes are ALL / NAME / LENGTH / TYPE. Twenty sounds is fine, two hundred is chaos | `M` |
| P2-2 | **Favourites and recently played** | The five sounds you actually spam should be one click away, not a search | `S` |
| P2-3 | **Manual ordering** (drag to reorder, persisted as `Order` in `SoundItem`) | A board you arrange yourself is muscle memory; re-sorting by name destroys it | `M` |
| P2-4 | **Multi-select + bulk actions** (delete, move to folder, export) | Deleting 30 sounds one confirmation dialog at a time is painful | `M` |
| P2-5 | **Undo** for delete and rename (a simple stack, or "recently deleted" holding files for 7 days) | Delete currently nukes the file immediately | `S` |
| P2-6 | **Waveform preview** in the details dialog, with the trim handles on it | Pairs with P1-4; seeing the clip is how you trim it | `M` |
| P2-7 | **Search upgrade:** fuzzy match, search by tag/folder, filter chips | Only name + extension substring today | `M` |
| P2-8 | **Settings: import/export profile** (JSON with all settings, no audio) | You said you want exportable profiles; this is the smallest version of it | `S` |
| P2-9 | **Portable mode** (data folder next to the exe, chosen at first run) + library backup/restore | `%LOCALAPPDATA%` only; a USB-stick board is a common request | `S` |
| P2-10 | **Accessibility pass:** tab order, visible focus rings, automation names, high-contrast check, colour-blind-safe accents | Everything is custom-painted, so screen readers currently see nothing | `M` |
| P2-11 | **Localisation** (EN/DE at minimum) via resource strings | Lots of hardcoded UI strings; do it before the string count doubles | `L` |

---

## P3 - what makes it unlike every other soundboard

| ID | Item | Why | Size |
|---|---|---|---|
| P3-1 | **Sound packs (`.espack`)**: export a whole board - audio, images, names, hotkeys, order - as one file, and import someone else's | This is how the app spreads. "Download my pack" is a share button nobody else has | `M` |
| P3-2 | **HTTP trigger + Stream Deck / MIDI.** Local server (`http://localhost:port/play/{id}`) plus a browser source for OBS, and a Stream Deck plugin that calls it | Turns the board into infrastructure: OBS overlays, physical buttons, phone control. Biggest differentiator on this list | `L` |
| P3-3 | **Audio visualiser**: FFT (`SampleAggregator`) per playing sound → card waveform + the starfield pulsing with amplitude | The space theme already exists; making it *react to sound* is the unique bit | `M` |
| P3-4 | **Desktop pet that reacts**: bobs to the beat, covers its ears on a loud sound, sleeps when idle for a while, can be dragged | The pet is already the app's mascot; right now it ignores the audio entirely | `M` |
| P3-5 | **Pet outside the window** (taskbar / desktop overlay) | Currently clamped inside the form ("The pet stays inside the app window" per the README) | `M` |
| P3-6 | **Shareable themes** (`.estheme`): the `Theme.cs` tokens are already centralised, so export them + accent + background | Cheap, and it feeds P3-1 | `S` |
| P3-7 | **Cool Mode expansion**: more tracks, a track picker, road variants | Currently one bundled mp3 and one sprite | `S` |
| P3-8 | **Voice FX routing**: it selects a capture and output device, but doesn't publish itself as a mic for other apps | Without a virtual device it can't be a "voice changer" for Discord, only a monitor | `L` |

---

## P4 - engineering, shipping and proof

| ID | Item | Why | Size |
|---|---|---|---|
| P4-1 | **Frame-time / FPS overlay** (F12), plus a `--perf-log` flag writing frame times to a file | The fullscreen lag was diagnosed by reading code, not measuring. With an overlay the next report is "42ms per frame at 1440p" instead of "it's laggy" | `S` |
| P4-2 | **Golden-image tests for the hand-painted surface.** Render `PaintBackdrop` / `DrawCard` into a bitmap in a `windows-latest` job and compare against a stored PNG hash | The UI has no tests at all. Painting is the entire app, and it's the part that regresses silently | `M` |
| P4-3 | **Crash reporting in-app**: the `error.log` exists - add a "view log" button in settings and a copy-to-clipboard for bug reports | Right now the log is a path inside a message box | `S` |
| P4-4 | **Update check** against the GitHub releases API with a dismissible banner | Friends running a 3-month-old build is the normal state of small apps | `M` |
| P4-5 | **Thread-safety review.** `ImportNew` runs inside `Task.Run` and shares `DataStore` with the UI thread (directory creation, file copies), while `Save()` can fire from the UI thread at the same time | Importing a batch while playing, scrolling and closing the window is the realistic overlap | `M` |
| P4-6 | **`CONTRIBUTING.md` + issue/PR templates + a screenshot/GIF in the README** | The README has no screenshot of the app. That's the single highest-value line of documentation missing | `S` |
| P4-7 | **Benchmark pass** that fills in the empty table in the README (startup, idle CPU, memory, click-to-audio latency) with real numbers and the test method | The README currently ships an honest table of "Not measured" - fill it in | `S` |

---

## Known rough edges (found by reading the code, not reported by users)

These are bugs or risks, not features. Fix before the feature list grows.

- **Two instances = data loss.** No `Mutex`; both write `library.xml` with `File.Replace`, so one copy's edits disappear.
- **No `LICENSE`**, while the README asks people to contribute.
- **`AppSettings.Volume` is dead weight**: a legacy shared volume kept in sync with `SoundVolume` only on the sound slider. New code should not read it.
- **The desktop pet ignores audio** and cannot leave the window, which the README presents as a feature.
- **`VoiceFxForm` has its own device pickers** while the main player has none - the inconsistency is the hint that P0-2 was never finished.
- **No `AllowDrop`**: the "drop an image on a card" gesture in the README is the card menu, not drag-and-drop.
- **Search matches name + extension only**, so a renamed sound is unfindable by its old name.

---

## Suggested order

1. **Session 1 - truth:** run on Windows (P0-1), write down every broken screen, add the frame-time overlay (P4-1).
2. **Session 2 - routing:** playback device selection + persistence (P0-2). This is the one that changes what the app *is*.
3. **Session 3 - ergonomics:** tray + always-on-top + single instance (P0-4), global hotkeys (P0-3).
4. **Session 4 - sound quality:** per-sound volume + normalise + trim (P1-1, P1-2, P1-4).
5. **Session 5 - library:** folders + favourites + manual order (P2-1, P2-2, P2-3).
6. **Session 6 - shipping:** installer, licence, README screenshot (P0-5, P0-6, P4-6).

Everything in P3 is a reward for finishing P0 and P1. Don't start there - a shareable sound pack is worthless if the app can't output to Discord.

---

## Not doing (and why)

- **Cloud sync / accounts.** Adds a backend, privacy policy and auth surface to a local toy. Export/import files (P2-8, P3-1) get 90% of the value for 5% of the work.
- **Auto-transcription / AI sound generation.** Cool demo, not a soundboard feature, and it costs money per use.
- **A mobile or web version.** The whole identity is a hand-painted WinForms window with a desktop pet. Porting it (see `dotnet-desktop-development` notes) means throwing away the paint code.
- **Plugin SDK.** No third-party demand yet; the HTTP trigger in P3-2 covers the same use cases with a URL instead of an API.
