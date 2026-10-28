using System;
using System.Collections.Generic;

namespace EmiliosSoundboard
{
    /// <summary>
    /// Turns a stored device <em>name</em> into a device index for the audio backend.
    /// </summary>
    /// <remarks>
    /// Names are stored rather than indexes because the indexes Windows reports are not stable:
    /// plugging in a headset can renumber everything, and a saved index then points at the wrong
    /// device. If a name no longer exists the caller falls back to the system default, which is the
    /// only safe behaviour when the user's audio setup changed.
    /// </remarks>
    public static class AudioOutputPicker
    {
        /// <summary>Stored value meaning "whatever Windows is using right now".</summary>
        public const string SystemDefault = "System default";

        /// <summary>Stored value meaning "follow the board's device" for a single sound.</summary>
        public const string FollowBoard = "Same as the board";

        /// <summary>The value the audio backend expects for the system default device.</summary>
        public const int DefaultIndex = -1;

        /// <summary>
        /// Returns the index of the stored device in <paramref name="deviceNames"/>, or
        /// <see cref="DefaultIndex"/> when it is unset, blank, the default, or gone.
        /// </summary>
        public static int ResolveIndex(string stored, IList<string> deviceNames)
        {
            if (string.IsNullOrWhiteSpace(stored)) return DefaultIndex;
            if (deviceNames == null || deviceNames.Count == 0) return DefaultIndex;

            string wanted = stored.Trim();
            if (string.Equals(wanted, SystemDefault, StringComparison.OrdinalIgnoreCase)) return DefaultIndex;
            if (string.Equals(wanted, FollowBoard, StringComparison.OrdinalIgnoreCase)) return DefaultIndex;

            for (int i = 0; i < deviceNames.Count; i++)
            {
                if (string.Equals((deviceNames[i] ?? "").Trim(), wanted, StringComparison.OrdinalIgnoreCase)) return i;
            }

            // Driver names get rewritten between OS updates ("Speakers (Realtek(R) Audio)" vs
            // "Speakers (Realtek High Definition Audio)"), and the legacy API truncates long names.
            // Accept a single unambiguous partial match rather than silently going to the default.
            int match = DefaultIndex;
            int matches = 0;
            for (int i = 0; i < deviceNames.Count; i++)
            {
                string name = (deviceNames[i] ?? "").Trim();
                if (name.Length == 0) continue;
                if (name.IndexOf(wanted, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    wanted.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    match = i;
                    matches++;
                }
            }

            return matches == 1 ? match : DefaultIndex;
        }

        /// <summary>Human-readable name for a resolved index, used in log messages.</summary>
        public static string Describe(string stored, int resolvedIndex, IList<string> deviceNames)
        {
            if (resolvedIndex >= 0 && deviceNames != null && resolvedIndex < deviceNames.Count)
                return deviceNames[resolvedIndex];

            if (string.IsNullOrWhiteSpace(stored)) return SystemDefault;
            return stored.Trim();
        }

        /// <summary>The two entries every device picker starts with.</summary>
        public static List<string> Options(IList<string> deviceNames)
        {
            var list = new List<string> { SystemDefault };
            if (deviceNames == null) return list;

            for (int i = 0; i < deviceNames.Count; i++)
            {
                string name = (deviceNames[i] ?? "").Trim();
                if (name.Length == 0 || Contains(list, name)) continue;
                list.Add(name);
            }
            return list;
        }

        private static bool Contains(List<string> list, string value)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (string.Equals(list[i], value, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }
    }
}
