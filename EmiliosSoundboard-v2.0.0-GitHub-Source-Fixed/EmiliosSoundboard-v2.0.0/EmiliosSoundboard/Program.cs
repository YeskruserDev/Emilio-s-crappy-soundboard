using System;
using System.IO;
using System.Windows.Forms;

namespace EmiliosSoundboard
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (s, e) => ReportError(e.Exception, false);
            AppDomain.CurrentDomain.UnhandledException += (s, e) => ReportError(e.ExceptionObject as Exception, true);

            try
            {
                Application.Run(new MainForm());
            }
            catch (Exception ex)
            {
                ReportError(ex, true);
            }
        }

        private static void ReportError(Exception ex, bool fatal)
        {
            string text = ex == null ? "Unknown error" : ex.ToString();
            try
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EmiliosSoundboard");
                Directory.CreateDirectory(dir);
                File.AppendAllText(Path.Combine(dir, "error.log"),
                    DateTime.Now.ToString("u") + Environment.NewLine + text + Environment.NewLine + Environment.NewLine);
            }
            catch
            {
                // Logging must never cause another crash.
            }

            try
            {
                MessageBox.Show(
                    "Something went wrong:\n\n" + (ex == null ? "Unknown error" : ex.Message) +
                    "\n\nDetails were saved to %LOCALAPPDATA%\\EmiliosSoundboard\\error.log" +
                    (fatal ? "\n\nThe app needs to close." : ""),
                    "Emilio's Soundboard", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch
            {
            }
        }
    }
}
