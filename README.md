# Emilio-s-crappy-soundboard
Emilio’s Soundboard is a customizable Windows soundboard featuring a space-themed UI, custom sound cards, sound search and sorting, live voice effects with pitch control, and custom backgrounds. 
Emilio's Soundboard — v1.4.4
A customizable Windows soundboard built with C#, Windows Forms, .NET 8, and NAudio.

Features include an animated space-themed interface, searchable sound cards, custom backgrounds, configurable startup media, pitch-based live voice effects, and a hidden Rainbow Road mode.
Where to get sounds
You can add your own audio files to the soundboard using + ADD SOUND. Here are some places to find downloadable sound effects:
- Pixabay Sound Effects — a large library of sound effects. Review the Pixabay Content License before using or redistributing a sound.
- Mixkit Sound Effects — free effects in categories such as games, transitions, ambience, and notifications. Read the Sound Effects Free License and note that it does not allow redistributing the sound files on their own as a standalone library.
- Freesound — community-uploaded recordings and effects. Each sound can have a different Creative Commons license, so check the individual sound page for attribution and commercial-use conditions. See the Freesound license FAQ.
Adding sounds
1. Download a sound file from a source you trust.
2. Check that sound's license and keep any attribution details if required.
3. Open Emilio's Soundboard and click + ADD SOUND.
4. Select the downloaded audio file and follow the app's prompts to add it to your library.
Important: “Free to download” does not automatically mean a sound can be redistributed or used for every purpose. Avoid uploading copyrighted movie, game, music, or meme audio unless you have permission or a license that allows your intended use. If you publish a sound pack, include the original creators and license information where required.
Requirements
- Windows 10 or Windows 11
- .NET 8 SDK to build from source
- Internet connection on first build to restore the NAudio NuGet package
Build and run
1. Download or clone this repository.
2. Open EmiliosSoundboard.sln in Visual Studio 2022, or run BUILD-AND-RUN.bat.
3. If building from Visual Studio, select the EmiliosSoundboard project as the startup project and build in Release configuration.
Publish a self-contained Windows build
Run PUBLISH-PORTABLE.bat on Windows. The script creates a self-contained x64 publish folder and ZIP for distribution. Test the published app before sharing it.
Optional media assets
To avoid bundling third-party media that may have separate copyright restrictions, this public source package does not include Mario artwork, Mario Kart music, or the Windows XP startup sound. The app can be used without these optional files. Users can choose their own startup sound and media in Settings.

If you own or have permission to redistribute suitable assets, optional files can be placed beside the built executable using these names:
- MarioKart.png — optional sprite for Cool Mode
- RainbowRoad.mp3 — optional Cool Mode music
- WindowsXPStartup.mp3 — optional startup sound
Only distribute media you have rights to use. Missing optional files are not included in the project build.
User data
The app stores settings and imported sound library data under %LOCALAPPDATA%\EmiliosSoundboard, separate from the source folder.
Version status
Initial public source release, version 1.4.4. The source has not been independently build-verified in this packaging environment. Please report build errors with the complete terminal output.
