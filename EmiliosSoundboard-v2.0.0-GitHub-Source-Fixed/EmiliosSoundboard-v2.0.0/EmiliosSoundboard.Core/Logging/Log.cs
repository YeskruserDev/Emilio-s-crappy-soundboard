using System;

namespace EmiliosSoundboard
{
    /// <summary>
    /// Static access point for logging so call sites stay short and a missing logger is harmless.
    /// </summary>
    /// <remarks>
    /// The logger is installed once at startup. Before that, and in unit tests, every call is a
    /// no-op, which means core code can log freely without null checks.
    /// </remarks>
    public static class Log
    {
        private static readonly object sync = new object();
        private static AppLogger current;

        public static AppLogger Current
        {
            get { lock (sync) return current; }
        }

        public static bool IsActive
        {
            get { lock (sync) return current != null; }
        }

        public static LogLevel Level
        {
            get { var l = Current; return l == null ? LogLevel.Info : l.MinimumLevel; }
        }

        public static void Install(AppLogger logger)
        {
            lock (sync) { current = logger; }
        }

        public static void Uninstall()
        {
            lock (sync) { current = null; }
        }

        public static void Trace(string category, string message) { Write(LogLevel.Trace, category, message, null); }
        public static void Debug(string category, string message) { Write(LogLevel.Debug, category, message, null); }
        public static void Info(string category, string message) { Write(LogLevel.Info, category, message, null); }
        public static void Warn(string category, string message, Exception error = null) { Write(LogLevel.Warn, category, message, error); }
        public static void Error(string category, string message, Exception error = null) { Write(LogLevel.Error, category, message, error); }
        public static void Fatal(string category, string message, Exception error = null) { Write(LogLevel.Fatal, category, message, error); }

        public static void Write(LogLevel level, string category, string message, Exception error)
        {
            var logger = Current;
            if (logger == null) return;

            try { logger.Write(level, category, message, error); }
            catch
            {
                // Logging is never allowed to take the app down.
            }
        }
    }
}
