using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace EmiliosSoundboard
{
    /// <summary>
    /// Applies the Windows 11 DWM system backdrop (Mica / Acrylic) and rounded corners to a form.
    /// </summary>
    /// <remarks>
    /// Mica and Acrylic are drawn by the compositor, not by GDI+, so this affects the window frame
    /// and title bar. The client area of these dialogs is painted by WinForms and therefore stays
    /// opaque; the material never shows through the painted content.
    /// Everything is a no-op below Windows 11 22H2 (build 22621), where the attributes are rejected.
    /// </remarks>
    internal static class WindowEffects
    {
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;

        private const int DWMSBT_MAINWINDOW = 2;      // Mica
        private const int DWMSBT_TRANSIENTWINDOW = 3; // Acrylic
        private const int DWMSBT_NONE = 1;

        private const int DWMWCP_ROUND = 2;

        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_FRAMECHANGED = 0x0020;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter,
                                                int x, int y, int cx, int cy, uint flags);

        /// <summary>True on Windows 11 22H2 or newer, where the backdrop attributes exist.</summary>
        public static bool Supported
        {
            get
            {
                if (!OperatingSystem.IsWindows()) return false;
                return Environment.OSVersion.Version.Build >= 22621;
            }
        }

        public static BackdropKind Parse(string value)
        {
            return WindowBackdrop.Parse(value);
        }

        public static string Describe(BackdropKind kind)
        {
            return WindowBackdrop.Describe(kind);
        }

        /// <summary>
        /// Applies (or clears) the backdrop. Returns true when the material was accepted by DWM.
        /// Safe to call repeatedly, and safe on Windows 10 where it simply returns false.
        /// </summary>
        public static bool Apply(Form form, BackdropKind kind)
        {
            if (form == null || !form.IsHandleCreated) return false;

            try
            {
                IntPtr hwnd = form.Handle;

                int dark = 1;
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));

                int corners = DWMWCP_ROUND;
                DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref corners, sizeof(int));

                if (kind == BackdropKind.Off || !Supported)
                {
                    int none = DWMSBT_NONE;
                    DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref none, sizeof(int));
                    return false;
                }

                int type = kind == BackdropKind.Acrylic ? DWMSBT_TRANSIENTWINDOW : DWMSBT_MAINWINDOW;
                return DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref type, sizeof(int)) == 0;
            }
            catch (DllNotFoundException)
            {
                return false;   // not Windows at all
            }
            catch (EntryPointNotFoundException)
            {
                return false;
            }
        }

        /// <summary>Asks DWM to redraw the frame so a changed material shows up immediately.</summary>
        public static void RefreshFrame(Form form)
        {
            if (form == null || !form.IsHandleCreated) return;
            try
            {
                SetWindowPos(form.Handle, IntPtr.Zero, 0, 0, 0, 0,
                             SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);
                form.Invalidate(true);
            }
            catch
            {
                // Cosmetic only.
            }
        }
    }
}
