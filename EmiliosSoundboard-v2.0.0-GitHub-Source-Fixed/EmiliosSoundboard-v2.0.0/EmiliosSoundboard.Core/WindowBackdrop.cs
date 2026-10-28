using System;

namespace EmiliosSoundboard
{
    /// <summary>Window material for the app's dialogs.</summary>
    public enum BackdropKind
    {
        Off = 0,
        Mica = 1,
        Acrylic = 2,
    }

    /// <summary>
    /// Maps the stored setting to a <see cref="BackdropKind"/> and back. Lives in the core so the
    /// round-trip is covered by tests; the DWM calls themselves are platform code in the app.
    /// </summary>
    public static class WindowBackdrop
    {
        public const string Default = "Mica";

        /// <summary>Parses the stored setting. Unknown or empty values fall back to the default.</summary>
        public static BackdropKind Parse(string value)
        {
            string v = (value ?? "").Trim();
            if (string.Equals(v, "Off", StringComparison.OrdinalIgnoreCase)) return BackdropKind.Off;
            if (string.Equals(v, "None", StringComparison.OrdinalIgnoreCase)) return BackdropKind.Off;
            if (string.Equals(v, "Acrylic", StringComparison.OrdinalIgnoreCase)) return BackdropKind.Acrylic;
            if (string.Equals(v, "Mica", StringComparison.OrdinalIgnoreCase)) return BackdropKind.Mica;
            return BackdropKind.Mica;
        }

        /// <summary>The exact string that <see cref="Parse"/> turns into <paramref name="kind"/>.</summary>
        public static string Describe(BackdropKind kind)
        {
            switch (kind)
            {
                case BackdropKind.Acrylic: return "Acrylic";
                case BackdropKind.Mica: return "Mica";
                default: return "Off";
            }
        }
    }
}
