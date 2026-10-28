using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace EmiliosSoundboard
{
    /// <summary>
    /// Append-only file log with daily files, size-based rollover, age-based pruning and an
    /// in-memory ring of recent entries for the diagnostics UI.
    /// </summary>
    /// <remarks>
    /// Logging must never break the app: every file operation is guarded, failures are recorded in
    /// <see cref="LastWriteError"/> instead of being thrown. Volume is low (events, not frames), so
    /// each entry is appended with the file opened and closed again. That trades a little time for
    /// surviving crashes and multiple processes writing to the same folder.
    /// </remarks>
    public sealed class AppLogger : IDisposable
    {
        private readonly object sync = new object();
        private readonly Queue<LogEntry> ring = new Queue<LogEntry>();
        private readonly string prefix;

        private LogLevel minimumLevel = LogLevel.Info;
        private bool disposed;
        private string openDay;
        private int openPart;

        public string Directory { get; private set; }
        public int RetentionDays { get; private set; }
        public long MaxFileBytes { get; private set; }
        public int RingCapacity { get; private set; }

        /// <summary>Set when the log could not be written. The app keeps running.</summary>
        public string LastWriteError { get; private set; }

        /// <summary>Number of entries written since the logger was created.</summary>
        public long WrittenCount { get; private set; }

        public AppLogger(string directory, string filePrefix = "app",
                         int retentionDays = 7, long maxFileBytes = 1024 * 1024, int ringCapacity = 400)
        {
            if (string.IsNullOrWhiteSpace(directory))
                throw new ArgumentException("A log folder is required.", "directory");

            Directory = directory;
            prefix = string.IsNullOrWhiteSpace(filePrefix) ? "app" : filePrefix.Trim();
            RetentionDays = Math.Max(1, retentionDays);
            MaxFileBytes = Math.Max(16 * 1024, maxFileBytes);
            RingCapacity = Math.Max(10, ringCapacity);

            TryCreateDirectory();
            Purge();
        }

        public LogLevel MinimumLevel
        {
            get { lock (sync) return minimumLevel; }
            set { lock (sync) minimumLevel = value; }
        }

        /// <summary>File that new entries go to right now, e.g. ".../logs/app-2026-10-28.log".</summary>
        public string CurrentFile
        {
            get
            {
                lock (sync)
                {
                    DateTime today = DateTime.Now.Date;
                    return PathFor(today, openDay == DayKey(today) ? openPart : 0);
                }
            }
        }

        /// <summary>One-line summary for the diagnostics panel, never throws.</summary>
        public string Describe()
        {
            try
            {
                string current = CurrentFile;
                long size = 0;
                if (File.Exists(current)) size = new FileInfo(current).Length;
                int files = 0;
                try { files = System.IO.Directory.GetFiles(Directory, prefix + "-*.log").Length; }
                catch { }

                return string.Format(CultureInfo.InvariantCulture,
                    "{0} ({1}, {2} file(s), {3} entries this session, level {4})",
                    Path.GetFileName(current), DataStore.FormatBytes(size), files, WrittenCount, minimumLevel);
            }
            catch (Exception ex)
            {
                return "log unavailable: " + ex.Message;
            }
        }

        // ------------------------------------------------------------------ writing

        public void Write(LogLevel level, string category, string message, Exception error = null)
        {
            if (level < MinimumLevel) return;

            var entry = new LogEntry(DateTime.Now, level, category ?? "app", message ?? "", FormatError(error));
            Append(entry);
        }

        public void Trace(string category, string message) { Write(LogLevel.Trace, category, message); }
        public void Debug(string category, string message) { Write(LogLevel.Debug, category, message); }
        public void Info(string category, string message) { Write(LogLevel.Info, category, message); }
        public void Warn(string category, string message, Exception error = null) { Write(LogLevel.Warn, category, message, error); }
        public void Error(string category, string message, Exception error = null) { Write(LogLevel.Error, category, message, error); }
        public void Fatal(string category, string message, Exception error = null) { Write(LogLevel.Fatal, category, message, error); }

        private void Append(LogEntry entry)
        {
            lock (sync)
            {
                if (disposed) return;

                ring.Enqueue(entry);
                while (ring.Count > RingCapacity) ring.Dequeue();

                try
                {
                    string day = DayKey(entry.Time);
                    if (day != openDay)
                    {
                        openDay = day;
                        openPart = 0;
                    }

                    string path = PathFor(entry.Time.Date, openPart);
                    long length = File.Exists(path) ? new FileInfo(path).Length : 0;
                    if (length >= MaxFileBytes)
                    {
                        openPart++;
                        path = PathFor(entry.Time.Date, openPart);
                    }

                    EnsureDirectory();
                    File.AppendAllText(path, entry.ToLine() + Environment.NewLine, new UTF8Encoding(false));
                    WrittenCount++;
                    LastWriteError = null;
                }
                catch (Exception ex)
                {
                    // Never rethrow from logging. Record once, keep going.
                    LastWriteError = ex.Message;
                }
            }
        }

        // ------------------------------------------------------------------ reading

        /// <summary>Newest entries first, for the in-app view.</summary>
        public IReadOnlyList<LogEntry> Recent(int maximum = 50)
        {
            lock (sync)
            {
                int take = Math.Max(1, Math.Min(maximum, ring.Count));
                return ring.Reverse().Take(take).ToList();
            }
        }

        /// <summary>Last N lines of the current file, for "copy log" in the diagnostics panel.</summary>
        public string Tail(int lines = 200)
        {
            try
            {
                var recent = Recent(lines);
                var sb = new StringBuilder();
                foreach (var e in recent.Reverse())
                    sb.AppendLine(e.ToLine());
                return sb.ToString();
            }
            catch (Exception ex)
            {
                return "could not read the in-memory log: " + ex.Message;
            }
        }

        // ------------------------------------------------------------------ files

        /// <summary>Deletes log files older than the retention window. Safe to call any time.</summary>
        public void Purge()
        {
            try
            {
                if (!System.IO.Directory.Exists(Directory)) return;
                DateTime cutoff = DateTime.Now.AddDays(-RetentionDays);
                foreach (string file in System.IO.Directory.GetFiles(Directory, prefix + "-*.log"))
                {
                    try
                    {
                        if (File.GetLastWriteTime(file) < cutoff) File.Delete(file);
                    }
                    catch
                    {
                        // A file we cannot delete is not worth failing over.
                    }
                }
            }
            catch
            {
                // Purge is best effort.
            }
        }

        private string PathFor(DateTime day, int part)
        {
            string name = prefix + "-" + day.ToString("yyyy-MM-dd") + (part > 0 ? "." + part : "") + ".log";
            return Path.Combine(Directory, name);
        }

        private static string DayKey(DateTime time)
        {
            return time.ToString("yyyy-MM-dd");
        }

        private void TryCreateDirectory()
        {
            try { EnsureDirectory(); }
            catch (Exception ex) { LastWriteError = ex.Message; }
        }

        private void EnsureDirectory()
        {
            if (!System.IO.Directory.Exists(Directory)) System.IO.Directory.CreateDirectory(Directory);
        }

        private static string FormatError(Exception error)
        {
            if (error == null) return null;

            var sb = new StringBuilder();
            Exception current = error;
            int depth = 0;
            while (current != null && depth < 6)
            {
                if (depth > 0) sb.Append(Environment.NewLine).Append("---> ");
                sb.Append(current.GetType().Name);
                sb.Append(": ");
                sb.Append(current.Message);
                if (depth == 0 && !string.IsNullOrEmpty(current.StackTrace))
                {
                    sb.Append(Environment.NewLine);
                    sb.Append(current.StackTrace);
                }

                var aggregate = current as AggregateException;
                if (aggregate != null && aggregate.InnerExceptions.Count > 1)
                {
                    sb.Append(" (+").Append(aggregate.InnerExceptions.Count - 1).Append(" more)");
                }

                current = current.InnerException;
                depth++;
            }
            return sb.ToString();
        }

        public void Dispose()
        {
            lock (sync)
            {
                disposed = true;
            }
        }
    }
}
