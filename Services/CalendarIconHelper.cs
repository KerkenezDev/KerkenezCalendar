using System;
using System.Drawing;
using System.IO;
using System.Reflection;

namespace KerkenezCalendar.Services
{
    public static class CalendarIconHelper
    {
        private static Icon? _cachedIcon;

        public static Icon GetApplicationIcon()
        {
            if (_cachedIcon != null) return _cachedIcon;

            try
            {
                // Try embedded resource
                var asm = Assembly.GetExecutingAssembly();
                using var stream = asm.GetManifestResourceStream("KerkenezCalendar.app.ico");
                if (stream != null)
                {
                    _cachedIcon = new Icon(stream);
                    return _cachedIcon;
                }
            }
            catch { }

            try
            {
                // Try disk file
                string localIco = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
                if (File.Exists(localIco))
                {
                    _cachedIcon = new Icon(localIco);
                    return _cachedIcon;
                }
            }
            catch { }

            // Dynamic fallback: Generate clean 16x16 calendar icon in memory
            try
            {
                using var bmp = new Bitmap(16, 16);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.Transparent);
                    // Page
                    g.FillRectangle(Brushes.White, 1, 1, 14, 14);
                    // Red banner
                    g.FillRectangle(Brushes.Crimson, 1, 1, 14, 4);
                    // Border
                    g.DrawRectangle(Pens.Gray, 1, 1, 13, 13);
                    // Grid dots
                    g.FillRectangle(Brushes.Black, 4, 7, 2, 2);
                    g.FillRectangle(Brushes.Black, 7, 7, 2, 2);
                    g.FillRectangle(Brushes.Black, 10, 7, 2, 2);
                    g.FillRectangle(Brushes.Black, 4, 10, 2, 2);
                    g.FillRectangle(Brushes.Black, 7, 10, 2, 2);
                    g.FillRectangle(Brushes.Black, 10, 10, 2, 2);
                }
                IntPtr hIcon = bmp.GetHicon();
                _cachedIcon = Icon.FromHandle(hIcon);
                return _cachedIcon;
            }
            catch
            {
                return SystemIcons.Application;
            }
        }
    }
}
