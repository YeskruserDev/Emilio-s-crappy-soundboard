using System;
using System.IO;
using System.Linq;
using Xunit;

namespace EmiliosSoundboard.Tests
{
    public class DataStoreTests
    {
        [Fact]
        public void Constructor_CreatesManagedFolders()
        {
            using (var tmp = new TempLibrary())
            {
                var store = tmp.NewStore();

                Assert.True(Directory.Exists(store.SoundsDir));
                Assert.True(Directory.Exists(store.ImagesDir));
                Assert.True(Directory.Exists(store.ConfigDir));
                Assert.Equal(Path.Combine(tmp.Root, "Config", "library.xml"), store.ConfigPath);
            }
        }

        [Fact]
        public void Constructor_RejectsEmptyRoot()
        {
            Assert.Throws<ArgumentException>(() => new DataStore("  "));
        }

        [Fact]
        public void Load_WithoutSavedFile_StartsEmptyAndClean()
        {
            using (var tmp = new TempLibrary())
            {
                var store = tmp.NewStore();
                store.Load();

                Assert.Null(store.LoadWarning);
                Assert.NotNull(store.Data);
                Assert.NotNull(store.Data.Settings);
                Assert.Empty(store.Data.Sounds);
            }
        }

        [Fact]
        public void SaveThenLoad_RoundTripsSoundsAndSettings()
        {
            using (var tmp = new TempLibrary())
            {
                var store = tmp.NewStore();
                store.Load();
                string audio = tmp.WriteAudio("beep.wav");

                var imported = store.ImportNew(audio, "Beep", null);
                store.Data.Sounds.Add(imported.Item);
                store.Data.Settings.Intensity = 42;
                store.Data.Settings.CoolMode = true;
                store.Data.Settings.AccentColor = "Violet";

                Assert.True(store.Save());

                var reloaded = tmp.NewStore();
                reloaded.Load();

                var item = Assert.Single(reloaded.Data.Sounds);
                Assert.Equal("Beep", item.Name);
                Assert.Equal(imported.Item.AudioFile, item.AudioFile);
                Assert.False(item.AudioMissing);
                Assert.Equal(42, reloaded.Data.Settings.Intensity);
                Assert.True(reloaded.Data.Settings.CoolMode);
                Assert.Equal("Violet", reloaded.Data.Settings.AccentColor);
            }
        }

        [Fact]
        public void Save_IsAtomicAndLeavesNoTempFile()
        {
            using (var tmp = new TempLibrary())
            {
                var store = tmp.NewStore();
                store.Load();
                store.Save();
                store.Save();

                Assert.True(File.Exists(store.ConfigPath));
                Assert.False(File.Exists(store.ConfigPath + ".tmp"));
                Assert.Empty(store.Data.Sounds);
            }
        }

        [Fact]
        public void Load_WithCorruptFile_KeepsBackupAndWarns()
        {
            using (var tmp = new TempLibrary())
            {
                var store = tmp.NewStore();
                File.WriteAllText(store.ConfigPath, "<LibraryData><Sounds><Sound>");

                store.Load();

                Assert.NotNull(store.LoadWarning);
                Assert.Contains("empty list", store.LoadWarning);
                Assert.Empty(store.Data.Sounds);

                var backups = Directory.GetFiles(store.ConfigDir, "library.corrupt-*.xml");
                Assert.Single(backups);
                Assert.Contains("<Sounds>", File.ReadAllText(backups[0]));
            }
        }

        [Fact]
        public void Load_SanitizesDroppedAndBentEntries()
        {
            using (var tmp = new TempLibrary())
            {
                var store = tmp.NewStore();
                string audio = tmp.WriteAudio("keep.wav");
                var imported = store.ImportNew(audio, "Keep", null);
                store.Data.Sounds.Add(imported.Item);

                // a second entry that points at a file which does not exist
                store.Data.Sounds.Add(new SoundItem { Name = "  Gone  ", AudioFile = "" });
                store.Data.Sounds.Add(new SoundItem
                {
                    Name = "   ",
                    AudioFile = "missing.wav",
                    ImageFile = "missing.png"
                });
                store.Data.Sounds.Add(new SoundItem { AudioFile = "b.wav", Id = imported.Item.Id });

                store.Save();

                var reloaded = tmp.NewStore();
                reloaded.Load();

                // the entry with no audio file at all is dropped, everything else survives
                Assert.Equal(3, reloaded.Data.Sounds.Count);
                Assert.DoesNotContain(reloaded.Data.Sounds, s => s.AudioFile.Length == 0);

                var missing = reloaded.Data.Sounds.Single(s => s.AudioFile == "missing.wav");
                Assert.True(missing.AudioMissing);
                Assert.Equal("Untitled", missing.Name);
                Assert.Equal("", missing.ImageFile);

                // the duplicate id was regenerated, ids stay unique
                Assert.Equal(3, reloaded.Data.Sounds.Select(s => s.Id).Distinct().Count());
            }
        }

        [Fact]
        public void Load_ForgivesOutOfRangeIntensity()
        {
            using (var tmp = new TempLibrary())
            {
                var store = tmp.NewStore();
                store.Load();
                store.Data.Settings.Intensity = 500;
                store.Save();

                var reloaded = tmp.NewStore();
                reloaded.Load();

                Assert.Equal(100, reloaded.Data.Settings.Intensity);
            }
        }

        [Fact]
        public void UniqueName_AppendsSuffixesAndIgnoresCase()
        {
            using (var tmp = new TempLibrary())
            {
                var store = tmp.NewStore();
                store.Load();
                store.Data.Sounds.Add(new SoundItem { Name = "Beep", AudioFile = "a.wav" });
                store.Data.Sounds.Add(new SoundItem { Name = "beep (2)", AudioFile = "b.wav" });

                // the requested casing is kept, only the suffix is added
                Assert.Equal("beep (3)", store.UniqueName("beep", null));
                Assert.Equal("Bloop", store.UniqueName("Bloop", null));
            }
        }

        [Fact]
        public void UniqueName_SkipsTheItemBeingEdited()
        {
            using (var tmp = new TempLibrary())
            {
                var store = tmp.NewStore();
                store.Load();
                var item = new SoundItem { Name = "Beep", AudioFile = "a.wav" };
                store.Data.Sounds.Add(item);

                Assert.Equal("Beep", store.UniqueName("Beep", item));
            }
        }

        [Fact]
        public void UniqueName_TruncatesVeryLongNames()
        {
            using (var tmp = new TempLibrary())
            {
                var store = tmp.NewStore();
                store.Load();

                string request = new string('x', 200);
                string result = store.UniqueName(request, null);

                Assert.Equal(60, result.Length);
            }
        }

        [Fact]
        public void ImportNew_CopiesAudioIntoTheLibrary()
        {
            using (var tmp = new TempLibrary())
            {
                var store = tmp.NewStore();
                store.Load();
                string source = tmp.WriteAudio("source.WAV", "payload");

                var result = store.ImportNew(source, "Source", null);

                Assert.NotNull(result.Item);
                Assert.Null(result.Warning);
                Assert.EndsWith(".wav", result.Item.AudioFile);
                Assert.True(File.Exists(Path.Combine(store.SoundsDir, result.Item.AudioFile)));
                Assert.Equal("payload", File.ReadAllText(Path.Combine(store.SoundsDir, result.Item.AudioFile)));
                Assert.Equal("", result.Item.ImageFile);
            }
        }

        [Fact]
        public void ImportNew_KeepsTheSoundWhenTheImageIsUnusable()
        {
            using (var tmp = new TempLibrary())
            {
                var store = tmp.NewStore();
                store.Load();
                string audio = tmp.WriteAudio("source.wav");
                string missingImage = Path.Combine(tmp.Root, "not-there.png");

                var result = store.ImportNew(audio, "Source", missingImage);

                Assert.NotNull(result.Item);
                Assert.NotNull(result.Warning);
                Assert.Equal("", result.Item.ImageFile);
                Assert.True(File.Exists(Path.Combine(store.SoundsDir, result.Item.AudioFile)));
            }
        }

        [Fact]
        public void ImportImage_WithoutImporter_CopiesTheFileThrough()
        {
            using (var tmp = new TempLibrary())
            {
                var store = tmp.NewStore();
                string source = tmp.WriteImage("art.PNG", "pixels");

                string stored = store.ImportImage(source);

                Assert.StartsWith("bg_", stored);
                Assert.EndsWith(".png", stored);
                Assert.True(File.Exists(Path.Combine(store.ImagesDir, stored)));
                Assert.Equal("pixels", File.ReadAllText(Path.Combine(store.ImagesDir, stored)));
            }
        }

        [Fact]
        public void ImportImage_WithImporter_DelegatesToTheFrontEnd()
        {
            using (var tmp = new TempLibrary())
            {
                var store = tmp.NewStore();
                var importer = new RecordingImporter();
                store.ImageImporter = importer;
                string source = tmp.WriteImage("art.jpg");

                string stored = store.ImportImage(source);

                Assert.Equal("bg_from_importer.png", stored);
                Assert.Same(source, importer.LastSource);
                Assert.Equal(store.ImagesDir, importer.LastTargetDir);
                Assert.Empty(Directory.GetFiles(store.ImagesDir));
            }
        }

        [Fact]
        public void RemoveSound_DeletesOnlyItsOwnFiles()
        {
            using (var tmp = new TempLibrary())
            {
                var store = tmp.NewStore();
                store.Load();
                string audio = tmp.WriteAudio("a.wav");
                var imported = store.ImportNew(audio, "A", null);
                store.Data.Sounds.Add(imported.Item);
                string audioPath = Path.Combine(store.SoundsDir, imported.Item.AudioFile);

                Assert.True(store.RemoveSound(imported.Item));
                Assert.False(File.Exists(audioPath));
                Assert.Empty(store.Data.Sounds);
            }
        }

        [Fact]
        public void RemoveSound_KeepsFilesStillUsedByAnotherSound()
        {
            using (var tmp = new TempLibrary())
            {
                var store = tmp.NewStore();
                store.Load();
                string audio = tmp.WriteAudio("shared.wav");
                var first = store.ImportNew(audio, "First", null);
                store.Data.Sounds.Add(first.Item);

                var second = new SoundItem { Name = "Second", AudioFile = first.Item.AudioFile };
                store.Data.Sounds.Add(second);

                store.RemoveSound(first.Item);

                Assert.True(File.Exists(Path.Combine(store.SoundsDir, second.AudioFile)));
            }
        }

        [Fact]
        public void RemoveSound_RefusesPathsOutsideTheManagedFolder()
        {
            using (var tmp = new TempLibrary())
            {
                var store = tmp.NewStore();
                store.Load();
                string outside = Path.Combine(tmp.Root, "escape.txt");
                File.WriteAllText(outside, "keep me");

                var item = new SoundItem { Name = "Sneaky", AudioFile = "a.wav", ImageFile = "../escape.txt" };
                store.Data.Sounds.Add(item);

                store.RemoveSound(item);

                Assert.True(File.Exists(outside));
            }
        }

        [Fact]
        public void GetStorageBytes_SumsSoundsAndImages()
        {
            using (var tmp = new TempLibrary())
            {
                var store = tmp.NewStore();
                store.Load();
                File.WriteAllText(Path.Combine(store.SoundsDir, "a.bin"), "12345");
                File.WriteAllText(Path.Combine(store.ImagesDir, "b.bin"), "123");

                Assert.Equal(8, store.GetStorageBytes());
            }
        }

        [Theory]
        [InlineData(0, "0 B")]
        [InlineData(1023, "1023 B")]
        [InlineData(1024, "1 KB")]
        [InlineData(1536, "1.5 KB")]
        [InlineData(1048576, "1 MB")]
        [InlineData(1610612736, "1.5 GB")]
        public void FormatBytes_FormatsEveryMagnitude(long bytes, string expected)
        {
            Assert.Equal(expected, DataStore.FormatBytes(bytes));
        }

        [Fact]
        public void NewImageName_IsUniqueAndKeepsTheExtension()
        {
            string first = DataStore.NewImageName(".PNG");
            string second = DataStore.NewImageName("png");

            Assert.NotEqual(first, second);
            Assert.StartsWith("bg_", first);
            Assert.EndsWith(".png", first);
            Assert.EndsWith(".png", second);
            Assert.Equal(39, first.Length); // "bg_" + 32 hex + ".png"
        }

        private sealed class RecordingImporter : IImageImporter
        {
            public string LastSource;
            public string LastTargetDir;

            public string Import(string sourcePath, string imagesDir)
            {
                LastSource = sourcePath;
                LastTargetDir = imagesDir;
                return "bg_from_importer.png";
            }
        }
    }
}
