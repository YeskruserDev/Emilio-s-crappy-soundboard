using System;
using System.IO;

namespace EmiliosSoundboard.Tests
{
    /// <summary>Gives each test its own throwaway library folder and cleans it up afterwards.</summary>
    internal sealed class TempLibrary : IDisposable
    {
        public string Root { get; private set; }

        public TempLibrary()
        {
            Root = Path.Combine(Path.GetTempPath(), "es-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        public DataStore NewStore()
        {
            return new DataStore(Root);
        }

        /// <summary>Writes a small dummy audio file and returns its full path.</summary>
        public string WriteAudio(string fileName, string contents = "not-real-audio")
        {
            string path = Path.Combine(Root, fileName);
            File.WriteAllText(path, contents);
            return path;
        }

        public string WriteImage(string fileName, string contents = "not-real-image")
        {
            string path = Path.Combine(Root, fileName);
            File.WriteAllText(path, contents);
            return path;
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Root)) Directory.Delete(Root, true);
            }
            catch
            {
                // A leftover temp folder must never fail a test run.
            }
        }
    }
}
