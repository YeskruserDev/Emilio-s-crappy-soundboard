using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace EmiliosSoundboard
{
    /// <summary>
    /// Small platform boundary for filesystem and folder-opening behaviour. The current UI/audio
    /// stack is still Windows-only; these helpers are porting groundwork, not a Linux build claim.
    /// </summary>
    internal static class PlatformServices
    {
        public static string GetAppDataDirectory()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                string xdg = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
                if (!string.IsNullOrWhiteSpace(xdg)) return xdg;
                string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                return Path.Combine(string.IsNullOrWhiteSpace(home) ? "." : home, ".local", "share");
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                return Path.Combine(string.IsNullOrWhiteSpace(home) ? "." : home, "Library", "Application Support");
            }

            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return string.IsNullOrWhiteSpace(local) ? AppContext.BaseDirectory : local;
        }

        public static void OpenFolder(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath)) return;
            Directory.CreateDirectory(folderPath);

            string command;
            string arguments;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                command = "explorer.exe";
                arguments = "\"" + folderPath + "\"";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                command = "open";
                arguments = "\"" + folderPath + "\"";
            }
            else
            {
                command = "xdg-open";
                arguments = "\"" + folderPath + "\"";
            }

            Process.Start(new ProcessStartInfo(command, arguments) { UseShellExecute = false });
        }
    }
}
