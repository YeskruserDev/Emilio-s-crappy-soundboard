using System;
using System.IO;
using System.Xml.Serialization;
using Xunit;

namespace EmiliosSoundboard.Tests
{
    public class ModelsTests
    {
        [Fact]
        public void SoundItem_GetsAUniqueIdAndDefaults()
        {
            var a = new SoundItem();
            var b = new SoundItem();

            Assert.Equal(32, a.Id.Length);
            Assert.NotEqual(a.Id, b.Id);
            Assert.Equal("Untitled", a.Name);
            Assert.Equal("", a.AudioFile);
            Assert.Equal("", a.ImageFile);
            Assert.False(a.AudioMissing);
        }

        [Fact]
        public void AppSettings_DefaultsMatchTheShippedExperience()
        {
            var s = new AppSettings();

            Assert.True(s.AnimatedBackground);
            Assert.True(s.CursorGlow);
            Assert.True(s.StarReaction);
            Assert.True(s.HoverAnimations);
            Assert.False(s.CoolMode);
            Assert.False(s.DesktopPetEnabled);
            Assert.Equal(100, s.DesktopPetSize);
            Assert.Equal(60, s.Intensity);
            Assert.Equal("Blue", s.AccentColor);
            Assert.Equal(70, s.SoundVolume);
            Assert.Equal(55, s.MusicVolume);
        }

        [Fact]
        public void AppSettings_ResetToDefaults_RestoresEveryField()
        {
            var s = new AppSettings
            {
                AnimatedBackground = false,
                CursorGlow = false,
                StarReaction = false,
                HoverAnimations = false,
                CoolMode = true,
                DesktopPetEnabled = true,
                DesktopPetImagePath = "pet.png",
                DesktopPetSize = 180,
                Intensity = 5,
                AccentColor = "Violet",
                StartupVideoPath = "v.mp4",
                StartupPhotoPath = "p.png",
                StartupSoundPath = "s.mp3",
                BackgroundImagePath = "bg.png",
                Volume = 1,
                SoundVolume = 2,
                MusicVolume = 3,
            };

            s.ResetToDefaults();

            Assert.Equal(new AppSettings().SoundVolume, s.SoundVolume);
            Assert.Equal(new AppSettings().MusicVolume, s.MusicVolume);
            Assert.Equal(100, s.DesktopPetSize);
            Assert.Equal(60, s.Intensity);
            Assert.Equal("Blue", s.AccentColor);
            Assert.False(s.CoolMode);
            Assert.False(s.DesktopPetEnabled);
            Assert.Equal("", s.DesktopPetImagePath);
            Assert.Equal("", s.StartupVideoPath);
            Assert.Equal("", s.BackgroundImagePath);
        }

        [Fact]
        public void LibraryData_StartsEmptyAtVersionOne()
        {
            var data = new LibraryData();

            Assert.Equal(1, data.Version);
            Assert.NotNull(data.Sounds);
            Assert.Empty(data.Sounds);
            Assert.NotNull(data.Settings);
        }

        [Fact]
        public void LibraryXml_KeepsTheOnDiskContractStable()
        {
            var data = new LibraryData();
            data.Sounds.Add(new SoundItem
            {
                Name = "Beep",
                AudioFile = "abc.wav",
                ImageFile = "bg.png",
                AudioMissing = true,
            });

            var serializer = new XmlSerializer(typeof(LibraryData));
            string xml;
            using (var writer = new StringWriter())
            {
                serializer.Serialize(writer, data);
                xml = writer.ToString();
            }

            // users already have library.xml files; renaming these elements breaks their libraries
            Assert.Contains("<LibraryData", xml);   // root element name is the type name
            Assert.Contains("<Sounds>", xml);
            Assert.Contains("<Sound>", xml);
            Assert.Contains("<Name>Beep</Name>", xml);
            Assert.Contains("<AudioFile>abc.wav</AudioFile>", xml);
            Assert.Contains("<ImageFile>bg.png</ImageFile>", xml);
            Assert.DoesNotContain("AudioMissing", xml);   // [XmlIgnore] runtime-only flag
        }

        [Fact]
        public void ImportResult_DefaultIsEmpty()
        {
            var result = new ImportResult();

            Assert.Null(result.Item);
            Assert.Null(result.Warning);
        }
    }
}
