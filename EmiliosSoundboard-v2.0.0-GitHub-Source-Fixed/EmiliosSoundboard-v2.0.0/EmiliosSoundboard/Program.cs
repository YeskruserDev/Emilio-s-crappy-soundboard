using System;
using System.IO;
using System.Windows.Forms;

namespace EmiliosSoundboard
{
    internal static class Program
    {
        /// <summary>
        /// Folder where the rolling log lives, next to the user's library:
        /// %LOCALAPPDATA%\EmiliosSoundboard\logs
        /// </summary>
        internal static string LogFolder
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "EmiliosSoundboard", "logs");
            }
        }

        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            AppLogger logger = null;
            try
            {
                logger = new AppLogger(LogFolder);
                logger.MinimumLevel = HasVerboseFlag(args) ? LogLevel.Debug : LogLevel.Info;
                Log.Install(logger);
            }
            catch (Exception ex)
            {
                // A broken log folder must not stop the app from opening.
                logger = null;
                MessageBox.Show(
                    "The log file could not be started, so problems will not be recorded:\n\n" + ex.Message,
                    "Emilio's Soundboard", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (s, e) => ReportError(e.Exception, false);
            AppDomain.CurrentDomain.UnhandledException += (s, e) => ReportError(e.ExceptionObject as Exception, true);
            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                Log.Error("task", "an unobserved background task failed", e.Exception);
                e.SetObserved();
            };
            AppDomain.CurrentDomain.ProcessExit += (s, e) =>
            {
                Log.Info("app", "closing");
                if (Log.Current != null) Log.Current.Dispose();
            };

            Log.Info("app", "starting Emilio's Soundboard " + Application.ProductVersion +
                            " on " + Environment.OSVersion.VersionString +
                            " (" + (Environment.Is64BitProcess ? "64-bit" : "32-bit") + " process)");
            Log.Info("app", "log folder: " + LogFolder);

            try
            {
                Application.Run(new MainForm());
            }
            catch (Exception ex)
            {
                ReportError(ex, true);
            }
            finally
            {
                Log.Info("app", "stopped");
                if (logger != null) logger.Dispose();
            }
        }

        private static bool HasVerboseFlag(string[] args)
        {
            if (args == null) return false;
            foreach (string arg in args)
            {
                string a = (arg ?? "").Trim().TrimStart('-', '/');
                if (string.Equals(a, "verbose", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(a, "debug", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(a, "log", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static void ReportError(Exception ex, bool fatal)
        {
            Log.Fatal("app", fatal ? "unhandled exception, the app has to close" : "unhandled exception in the UI thread", ex);

            string detail = Log.Current == null
                ? "No log is being recorded."
                : "Details were saved to:\n" + Log.Current.CurrentFile;

            try
            {
                MessageBox.Show(
                    "Something went wrong:\n\n" + (ex == null ? "Unknown error" : ex.Message) +
                    "\n\n" + detail + "\n\nOpen Settings, then Diagnostics, to copy the log into a bug report." +
                    (fatal ? "\n\nThe app needs to close." : ""),
                    "Emilio's Soundboard", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch
            {
                // Nothing left to try.
            }
        }
    }
}
