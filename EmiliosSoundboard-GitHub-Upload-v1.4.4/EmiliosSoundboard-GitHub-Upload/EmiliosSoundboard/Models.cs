using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace EmiliosSoundboard
{
    /// <summary>One sound button. Only file NAMES are stored; the folders are known to DataStore.</summary>
    public class SoundItem
    {
        public string Id = Guid.NewGuid().ToString("N");
        public string Name = "Untitled";

        /// <summary>File name (no folder) of the copied audio inside the Sounds folder.</summary>
        public string AudioFile = "";

        /// <summary>File name (no folder) of the custom background inside the Backgrounds folder, or empty for default.</summary>
        public string ImageFile = "";

        /// <summary>Not saved. True when the audio file could not be found on disk at startup.</summary>
        [XmlIgnore]
        public bool AudioMissing;
    }

    public class AppSettings
    {
        public bool AnimatedBackground = true;
        public bool CursorGlow = true;
        public bool StarReaction = true;
        public bool HoverAnimations = true;
        public bool CoolMode = false;

        /// <summary>0 - 100.</summary>
        public int Intensity = 60;

        public string AccentColor = "Blue";
        public string StartupVideoPath = "";
        public string StartupPhotoPath = "";
        public string StartupSoundPath = "";
        public string BackgroundImagePath = "";
        public int Volume = 70; // Legacy shared volume for older settings files.
        public int SoundVolume = 70;
        public int MusicVolume = 55;

        public void ResetToDefaults()
        {
            AnimatedBackground = true;
            CursorGlow = true;
            StarReaction = true;
            HoverAnimations = true;
            CoolMode = false;
            Intensity = 60;
            AccentColor = "Blue";
            StartupVideoPath = "";
            StartupPhotoPath = "";
            StartupSoundPath = "";
            BackgroundImagePath = "";
            Volume = 70;
            SoundVolume = 70;
            MusicVolume = 55;
        }
    }

    public class LibraryData
    {
        public int Version = 1;
        public AppSettings Settings = new AppSettings();

        [XmlArray("Sounds")]
        [XmlArrayItem("Sound")]
        public List<SoundItem> Sounds = new List<SoundItem>();
    }

    public sealed class ImportResult
    {
        public SoundItem Item;
        public string Warning;
    }
}
