# Emilio's Soundboard v2.0.1 BETA

**A customizable Windows soundboard with a space-inspired interface, live voice effects, and audio routing for chat apps.**

Emilio's Soundboard lets you build your own sound library and play clips quickly from a clean, animated space-themed interface. Add your own sounds, search and sort your collection, and use the stop control whenever you need to cut a clip off.

## Features

- **Custom sound library:** Import your own audio clips and organize them into searchable sound cards.
- **Chat app routing:** Send soundboard clips to a chosen Windows audio endpoint, including compatible virtual audio cables and mixer endpoints.
- **Live Voice FX:** Apply real-time pitch effects to microphone audio, try presets, adjust pitch, and save your own presets.
- **Voice changer ON/OFF:** Keep Voice FX open while using the soundboard and toggle live processing on or off.
- **GoXLR endpoint support:** Use Windows-exposed GoXLR / TC-Helicon audio endpoints with an appropriate mixer routing setup.
- **Cool Mode:** Switch to a colourful racing-inspired cosmic theme with music.
- **Comic Book Mode:** Monochrome ink outlines, halftone dots and pop-art controls.
- **Customisation:** Adjust accent colours with a colour wheel, background animation, window resolution, sound levels, and more.
- **Local monitoring:** Sounds routed to a virtual cable are also played on your default output so you can hear them.
- **Desktop pet:** Add a custom image that moves and occasionally jumps or spins inside the app window.
- **Discord Rich Presence:** Display app activity in Discord when supported and configured.

## Setup for Discord / WhatsApp

To let people in a voice call hear your clips or processed voice, configure a compatible virtual audio cable (such as VB-CABLE) or your mixer first. In Settings, select the virtual cable's playback endpoint (often called `CABLE Input`) for soundboard clips. In Discord or WhatsApp, select its paired recording endpoint (often called `CABLE Output`) as your microphone. In Voice FX, choose your physical microphone, send processed audio to the same virtual playback endpoint, and switch the voice changer on. GoXLR users should select a playback channel that their mixer routes to the chat microphone mix.

**The app does not install a virtual microphone by itself.** External virtual-audio drivers or mixer routing may be required, depending on your setup. Use headphones and test the route before joining a call.

## Platform and status

- **Platform:** Windows 10 / Windows 11
- **Framework:** .NET 8
- **Release:** v2.0.1 BETA
- **Linux:** Not supported yet; a port would require replacing the current Windows Forms / WPF UI and Windows-specific audio pieces.

This is a BETA release. Some features may behave differently across audio devices, and Voice FX / virtual routing still needs real-device testing. Feedback and bug reports are welcome.
