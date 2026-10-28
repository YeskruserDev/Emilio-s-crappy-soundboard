using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;

namespace EmiliosSoundboard
{
    /// <summary>
    /// Owns the %LOCALAPPDATA%\EmiliosSoundboard folder: loading/saving the library and copying
    /// audio and images in and out. Only files inside the app's own folders are ever deleted.
    /// </summary>
    /// <remarks>
    /// Deliberately free of System.Drawing so it can live in the cross-platform core assembly and
    /// be unit-tested on any OS. Image decoding is supplied by the front end through
    /// <see cref="ImageImporter"/>.
    /// </remarks>
    public sealed class DataStore
    {
        public string Root { get; private set; }
        public string SoundsDir { get; private set; }
        public string ImagesDir { get; private set; }
        public string ConfigDir { get; private set; }
        public string ConfigPath { get; private set; }

        public LibraryData Data { get; private set; }

        /// <summary>Set when the saved library was unreadable and a fresh one was started.</summary>
        public string LoadWarning { get; private set; }

        public string LastSaveError { get; private set; }

        /// <summary>
        /// Optional image pipeline. When null, imported images are copied through unchanged
        /// instead of being re-encoded and downscaled.
        /// </summary>
        public IImageImporter ImageImporter { get; set; }

        public DataStore()
            : this(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EmiliosSoundboard"))
        {
        }

        /// <summary>Creates a store rooted at an explicit folder. Used by tests.</summary>
        public DataStore(string root)
        {
            if (string.IsNullOrWhiteSpace(root))
                throw new ArgumentException("A storage folder is required.", "root");

            Root = root;
            SoundsDir = Path.Combine(Root, "Sounds");
            ImagesDir = Path.Combine(Root, "Backgrounds");
            ConfigDir = Path.Combine(Root, "Config");
            ConfigPath = Path.Combine(ConfigDir, "library.xml");

            Directory.CreateDirectory(SoundsDir);
            Directory.CreateDirectory(ImagesDir);
            Directory.CreateDirectory(ConfigDir);

            Data = new LibraryData();
        }

        // ------------------------------------------------------------------ load / save

        public void Load()
        {
            LoadWarning = null;
            LibraryData loaded = null;

            if (File.Exists(ConfigPath))
            {
                try
                {
                    var serializer = new XmlSerializer(typeof(LibraryData));
                    using (var fs = new FileStream(ConfigPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        loaded = serializer.Deserialize(fs) as LibraryData;
                    }
                }
                catch (Exception)
                {
                    loaded = null;
                    string backup = null;
                    try
                    {
                        backup = Path.Combine(ConfigDir, "library.corrupt-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".xml");
                        File.Copy(ConfigPath, backup, true);
                    }
                    catch
                    {
                        backup = null;
                    }

                    LoadWarning = "Your saved sound list could not be read, so the app started with an empty list." +
                                  (backup != null ? "\n\nA copy of the damaged file was kept here:\n" + backup : "") +
                                  "\n\nYour imported audio files are still in:\n" + SoundsDir;
                }
            }

            Data = loaded ?? new LibraryData();
            Sanitize();
        }

        private void Sanitize()
        {
            if (Data.Settings == null) Data.Settings = new AppSettings();
            Data.Settings.Intensity = Math.Max(0, Math.Min(100, Data.Settings.Intensity));
            if (Data.Sounds == null) Data.Sounds = new List<SoundItem>();

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var clean = new List<SoundItem>();

            foreach (var s in Data.Sounds)
            {
                if (s == null) continue;

                if (string.IsNullOrWhiteSpace(s.Id) || !seen.Add(s.Id))
                {
                    s.Id = Guid.NewGuid().ToString("N");
                    seen.Add(s.Id);
                }

                s.Name = string.IsNullOrWhiteSpace(s.Name) ? "Untitled" : s.Name.Trim();
                s.AudioFile = SafeFileName(s.AudioFile);
                s.ImageFile = SafeFileName(s.ImageFile);

                if (s.AudioFile.Length == 0)
                    continue; // nothing to play and nothing to repair

                s.AudioMissing = !File.Exists(Path.Combine(SoundsDir, s.AudioFile));

                if (s.ImageFile.Length > 0 && !File.Exists(Path.Combine(ImagesDir, s.ImageFile)))
                    s.ImageFile = "";

                clean.Add(s);
            }

            Data.Sounds = clean;
        }

        private static string SafeFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "";
            string name = Path.GetFileName(value.Trim());
            return name ?? "";
        }

        public bool Save()
        {
            LastSaveError = null;
            try
            {
                Directory.CreateDirectory(ConfigDir);
                string tmp = ConfigPath + ".tmp";

                var serializer = new XmlSerializer(typeof(LibraryData));
                var settings = new XmlWriterSettings { Indent = true, Encoding = new System.Text.UTF8Encoding(false) };
                using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var writer = XmlWriter.Create(fs, settings))
                {
                    serializer.Serialize(writer, Data);
                }

                if (File.Exists(ConfigPath))
                    File.Replace(tmp, ConfigPath, ConfigPath + ".bak");
                else
                    File.Move(tmp, ConfigPath);
                return true;
            }
            catch (Exception ex)
            {
                LastSaveError = ex.Message;
                return false;
            }
        }

        // ------------------------------------------------------------------ paths and names

        public string AudioPath(SoundItem item)
        {
            return Path.Combine(SoundsDir, SafeFileName(item.AudioFile));
        }

        public string ImagePath(SoundItem item)
        {
            if (string.IsNullOrEmpty(item.ImageFile)) return null;
            return Path.Combine(ImagesDir, SafeFileName(item.ImageFile));
        }

        /// <summary>Returns a name that no other sound uses (adds " (2)", " (3)"... if needed).</summary>
        public string UniqueName(string desired, SoundItem exclude)
        {
            string baseName = string.IsNullOrWhiteSpace(desired) ? "Untitled" : desired.Trim();
            if (baseName.Length > 60) baseName = baseName.Substring(0, 60).Trim();

            string candidate = baseName;
            int n = 2;
            while (Data.Sounds.Any(s => !ReferenceEquals(s, exclude) &&
                                        string.Equals(s.Name, candidate, StringComparison.OrdinalIgnoreCase)))
            {
                candidate = baseName + " (" + n + ")";
                n++;
            }
            return candidate;
        }

        // ------------------------------------------------------------------ importing

        /// <summary>Copies the audio (and optional image) into the app folder. Safe to call from a background thread.</summary>
        public ImportResult ImportNew(string audioSource, string name, string imageSource)
        {
            var result = new ImportResult();
            var item = new SoundItem { Name = name };

            string ext = Path.GetExtension(audioSource);
            ext = string.IsNullOrEmpty(ext) ? ".audio" : ext.ToLowerInvariant();
            string audioName = Guid.NewGuid().ToString("N") + ext;
            File.Copy(audioSource, Path.Combine(SoundsDir, audioName), false);
            item.AudioFile = audioName;

            if (!string.IsNullOrEmpty(imageSource))
            {
                try
                {
                    item.ImageFile = ImportImage(imageSource);
                }
                catch (Exception ex)
                {
                    item.ImageFile = "";
                    result.Warning = "The sound was added, but the background image could not be used: " + ex.Message;
                }
            }

            result.Item = item;
            return result;
        }

        /// <summary>
        /// Copies an image into the Backgrounds folder and returns the new file name.
        /// The front end's <see cref="IImageImporter"/> does the decoding/resizing; without one the
        /// file is copied through untouched so the library stays usable.
        /// </summary>
        public string ImportImage(string sourcePath)
        {
            if (ImageImporter != null)
                return ImageImporter.Import(sourcePath, ImagesDir);

            string ext = Path.GetExtension(sourcePath);
            if (string.IsNullOrEmpty(ext)) ext = ".img";
            string passthrough = NewImageName(ext);
            File.Copy(sourcePath, Path.Combine(ImagesDir, passthrough), false);
            return passthrough;
        }

        /// <summary>Builds a fresh, collision-free name for a stored background image.</summary>
        public static string NewImageName(string extension)
        {
            string ext = string.IsNullOrWhiteSpace(extension) ? ".png" : extension.ToLowerInvariant();
            if (!ext.StartsWith(".")) ext = "." + ext;
            return "bg_" + Guid.NewGuid().ToString("N") + ext;
        }

        // ------------------------------------------------------------------ deleting

        /// <summary>Removes the sound from the library and deletes only its own managed files.</summary>
        /// <returns>true if every file was removed (or was already gone).</returns>
        public bool RemoveSound(SoundItem item)
        {
            Data.Sounds.Remove(item);
            bool ok = DeleteManagedFile(SoundsDir, item.AudioFile, true);
            ok &= DeleteImageFile(item.ImageFile);
            return ok;
        }

        public bool DeleteImageFile(string fileName)
        {
            return DeleteManagedFile(ImagesDir, fileName, false);
        }

        private bool DeleteManagedFile(string dir, string fileName, bool isAudio)
        {
            if (string.IsNullOrWhiteSpace(fileName)) return true;

            // Must be a bare file name; anything with folders is refused.
            if (!string.Equals(Path.GetFileName(fileName), fileName, StringComparison.Ordinal)) return false;

            // Never delete a file another sound still uses.
            bool stillUsed = Data.Sounds.Any(s => string.Equals(
                isAudio ? s.AudioFile : s.ImageFile, fileName, StringComparison.OrdinalIgnoreCase));
            if (stillUsed) return true;

            try
            {
                string dirFull = Path.GetFullPath(dir).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                string full = Path.GetFullPath(Path.Combine(dir, fileName));
                if (!full.StartsWith(dirFull, StringComparison.OrdinalIgnoreCase)) return false;

                if (File.Exists(full)) File.Delete(full);
                return true;
            }
            catch
            {
                return false;
            }
        }

        // ------------------------------------------------------------------ info

        public long GetStorageBytes()
        {
            return DirSize(SoundsDir) + DirSize(ImagesDir);
        }

        private static long DirSize(string dir)
        {
            try
            {
                return new DirectoryInfo(dir).EnumerateFiles().Sum(f => f.Length);
            }
            catch
            {
                return 0;
            }
        }

        public static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return bytes + " B";
            if (bytes < 1024 * 1024) return (bytes / 1024.0).ToString("0.#") + " KB";
            if (bytes < 1024L * 1024 * 1024) return (bytes / (1024.0 * 1024.0)).ToString("0.#") + " MB";
            return (bytes / (1024.0 * 1024.0 * 1024.0)).ToString("0.##") + " GB";
        }
    }
}
