using System;
using System.Collections.Generic;
using NAudio.Wave;

namespace EmiliosSoundboard
{
    /// <summary>
    /// Lists the Windows playback devices and resolves the stored device name to the index the
    /// audio backend needs.
    /// </summary>
    /// <remarks>
    /// Uses the same waveOut family the player already uses, so the device list matches exactly what
    /// playback can address. The winmm names are truncated to 31 characters by Windows; that is what
    /// gets stored, and <see cref="AudioOutputPicker.ResolveIndex"/> copes with either direction of
    /// truncation.
    /// </remarks>
    internal static class AudioOutput
    {
        public sealed class Device
        {
            public int Index;
            public string Name;
            public override string ToString() { return Name; }
        }

        private static readonly object sync = new object();
        private static List<Device> cache;

        /// <summary>Playback devices, in the order the audio backend reports them.</summary>
        public static List<Device> Devices(bool refresh = false)
        {
            lock (sync)
            {
                if (cache != null && !refresh) return cache;

                var list = new List<Device>();
                try
                {
                    int count = WaveOut.DeviceCount;
                    for (int i = 0; i < count; i++)
                    {
                        string name = "Output device " + (i + 1);
                        try
                        {
                            var caps = WaveOut.GetCapabilities(i);
                            if (!string.IsNullOrWhiteSpace(caps.ProductName)) name = caps.ProductName.Trim();
                        }
                        catch (Exception ex)
                        {
                            Log.Debug("audio", "could not read the name of output device " + i + ": " + ex.Message);
                        }
                        list.Add(new Device { Index = i, Name = name });
                    }

                    if (list.Count == 0)
                        Log.Warn("audio", "Windows reported no playback devices; sounds will fall back to the system default.");
                    else
                        Log.Debug("audio", list.Count + " playback device(s): " + string.Join(" | ", list.ConvertAll(d => d.Name).ToArray()));
                }
                catch (Exception ex)
                {
                    Log.Error("audio", "listing playback devices failed", ex);
                }

                cache = list;
                return cache;
            }
        }

        public static List<string> Names(bool refresh = false)
        {
            return Devices(refresh).ConvertAll(d => d.Name);
        }

        /// <summary>Device index for a stored name, or -1 for the system default.</summary>
        public static int Resolve(string storedName)
        {
            List<string> names = Names(false);
            int index = AudioOutputPicker.ResolveIndex(storedName, names);

            if (index < 0 && !string.IsNullOrWhiteSpace(storedName) &&
                !string.Equals(storedName.Trim(), AudioOutputPicker.SystemDefault, StringComparison.OrdinalIgnoreCase))
            {
                // Stored device is gone (unplugged headset, renamed driver). Play through the default
                // instead of failing, but say so once so it shows up in a bug report.
                Log.Warn("audio", "output device \"" + storedName + "\" is not available; using the system default.");
            }

            return index;
        }

        /// <summary>Name for an index, falling back to a readable description.</summary>
        public static string Describe(int index)
        {
            var devices = Devices(false);
            if (index >= 0 && index < devices.Count) return devices[index].Name;
            return AudioOutputPicker.SystemDefault;
        }

        /// <summary>Forces the next call to re-read the device list.</summary>
        public static void Refresh()
        {
            Devices(true);
        }
    }
}
