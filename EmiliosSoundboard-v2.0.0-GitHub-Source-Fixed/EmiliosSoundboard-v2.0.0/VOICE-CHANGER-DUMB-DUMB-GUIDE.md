# Voice Changer — super simple setup guide

## The easiest setup: VB-CABLE

**Important:** Voice FX can change the sound, but Discord must listen to the *processed output*. If Discord listens to your normal microphone, you will sound normal even while Voice FX is running.

1. In Emilio's Soundboard, click **GET VB-CABLE** or visit https://vb-audio.com/Cable/.
2. Download and install VB-CABLE. Follow its installation instructions; restart Windows if the installer asks you to.
3. Open Emilio's Soundboard, then open **VOICE FX**.
4. For **MICROPHONE INPUT**, select your real microphone (for example, your USB microphone).
5. For **CHAT OUTPUT**, select **CABLE Input (VB-Audio Virtual Cable)**. If it isn't listed, click **REFRESH**.
6. Click **VOICE CHANGER OFF — TURN ON** so it changes to **VOICE CHANGER ON — TURN OFF**.
7. Move **PITCH SHIFT / COARSE** away from `0`. For an obvious test, set it to **+12 semitones**. Speak and check the output using your virtual cable route.
8. Open Discord → **User Settings** → **Voice & Video**.
9. Set **Input Device** to **CABLE Output (VB-Audio Virtual Cable)**. Do **not** select your real microphone or the normal GoXLR Chat Mic for this test.
10. Join a call and use Discord's mic test. Your voice should sound higher. Try `-12` for a lower voice.

## If Discord still hears your normal voice

- Check that **VOICE CHANGER ON** is displayed in Emilio's Soundboard.
- Make sure the pitch value is not `0 semitones` and `0 cents`.
- In Discord, verify **Input Device = CABLE Output**. This is the most common mistake.
- Speak and check the Voice FX input meter. If it does not move, the wrong microphone is selected or Windows microphone permission is blocked.
- In Windows Sound settings, make sure **CABLE Input** is an available playback device and **CABLE Output** is an available recording device.
- If using GoXLR, its mixer can still send your physical microphone directly to Chat Mic, bypassing the processed sound. For a simple test, use VB-CABLE first. For GoXLR, route the app's output into the chat mix and ensure the chat app is not receiving the raw microphone directly.
- Do not set both the real microphone and the cable as Discord's input. Discord should use only the cable for this test.

## What the buttons mean

- **TEST MIC** only checks whether input is arriving; it is intentionally silent and does not send processed audio to Discord.
- **VOICE CHANGER ON** starts the live processed audio stream.
- **DEEP / HIGH / ALIEN** apply stronger pitch changes. The sliders allow fine adjustment.
- Use headphones to reduce feedback.

## Can VB-CABLE be bundled inside Emilio's Soundboard?

No—not as an ordinary app file. VB-CABLE installs a Windows audio driver and must be installed separately with its installer and permissions. Emilio's Soundboard can link to it and guide you through setup, but should not silently bundle or install a driver.

## If you get error `0x8889000A`

This is a Windows audio error meaning an audio endpoint is already in use. The app now retries a short-lived output-device lock and shows more specific instructions if Windows still refuses the device.

1. Close Discord and other apps that might be using the microphone or audio device.
2. Press **Windows + R**, type `mmsys.cpl`, and press Enter.
3. Open the **Recording** tab, double-click your real microphone, then open **Advanced**.
4. Untick both options under **Exclusive Mode**, then click **Apply**.
5. Do the same for the playback device selected in Voice FX. If using VB-CABLE, do this for **CABLE Input** too.
6. Reopen Emilio's Soundboard and try **VOICE CHANGER ON** again.
7. If it still fails, restart Windows and test again.

If Windows still reports the device is in use after these steps, choose another microphone or output device temporarily. The app cannot force Windows to take an endpoint that another driver or application has locked.
