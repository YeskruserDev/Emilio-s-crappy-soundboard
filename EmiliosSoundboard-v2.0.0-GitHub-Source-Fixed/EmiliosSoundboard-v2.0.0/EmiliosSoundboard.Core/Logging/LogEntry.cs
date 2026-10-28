using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace EmiliosSoundboard
{
    public enum LogLevel
    {
        Trace = 0,
        Debug = 1,
        Info = 2,
        Warn = 3,
        Error = 4,
        Fatal = 5,
    }

    /// <summary>One logged event. Immutable and cheap to keep in the in-memory ring.</summary>
    public sealed class LogEntry
    {
        public DateTime Time { get; private set; }
        public LogLevel Level { get; private set; }
        public string Category { get; private set; }
        public string Message { get; private set; }
        public string Error { get; private set; }

        public LogEntry(DateTime time, LogLevel level, string category, string message, string error)
        {
            Time = time;
            Level = level;
            Category = category;
            Message = message;
            Error = error;
        }

        /// <summary>The one-line form written to disk: "2026-10-28 09:41:12.512 INFO  [ui      ] ...".</summary>
        public string ToLine()
        {
            var sb = new StringBuilder(128);
            sb.Append(Time.ToString("yyyy-MM-dd HH:mm:ss.fff"));
            sb.Append(' ');
            sb.Append(LevelText(Level));
            sb.Append(" [");
            sb.Append(Pad(Category, 8));
            sb.Append("] ");
            sb.Append(Collapse(Message));
            if (!string.IsNullOrEmpty(Error))
            {
                sb.Append(Environment.NewLine);
                sb.Append(Indent(Error));
            }
            return sb.ToString();
        }

        public override string ToString()
        {
            return ToLine();
        }

        public static string LevelText(LogLevel level)
        {
            switch (level)
            {
                case LogLevel.Trace: return "TRACE";
                case LogLevel.Debug: return "DEBUG";
                case LogLevel.Info: return "INFO ";
                case LogLevel.Warn: return "WARN ";
                case LogLevel.Error: return "ERROR";
                default: return "FATAL";
            }
        }

        internal static string Pad(string value, int width)
        {
            string v = value ?? "";
            return v.Length >= width ? v.Substring(0, width) : v + new string(' ', width - v.Length);
        }

        /// <summary>Keeps one event on one line so the log stays greppable.</summary>
        internal static string Collapse(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return value.Replace("\r\n", "\n").Replace('\r', '\n').Replace("\n", " / ");
        }

        /// <summary>Indents the multi-line exception block under the event.</summary>
        internal static string Indent(string value)
        {
            string[] lines = (value ?? "").Replace("\r\n", "\n").Split('\n');
            var sb = new StringBuilder(value.Length + lines.Length * 4);
            for (int i = 0; i < lines.Length; i++)
            {
                sb.Append("    ");
                sb.Append(lines[i].TrimEnd());
                if (i < lines.Length - 1) sb.Append(Environment.NewLine);
            }
            return sb.ToString();
        }
    }
}
