using System;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;

namespace KerkenezCalendar.Services
{
    public static class StartupRegistrationService
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string RunValueName = "KerkenezCalendarTray";

        public static bool IsStartupEnabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
                if (key != null)
                {
                    var val = key.GetValue(RunValueName) as string;
                    return !string.IsNullOrWhiteSpace(val);
                }
            }
            catch { }
            return false;
        }

        public static void SetStartupEnabled(bool enable)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
                if (key == null) return;

                if (enable)
                {
                    string exePath = Application.ExecutablePath;
                    string command = $"\"{exePath}\" --daemon";
                    key.SetValue(RunValueName, command);
                }
                else
                {
                    key.DeleteValue(RunValueName, false);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[StartupRegistrationService] Error setting startup: {ex.Message}");
            }
        }

        public static bool CreateShortcuts()
        {
            try
            {
                string exePath = Application.ExecutablePath;
                string appName = "Kerkenez Calendar";

                // Desktop shortcut
                string desktopDir = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                string desktopLnk = Path.Combine(desktopDir, $"{appName}.lnk");
                CreateShellLink(desktopLnk, exePath);

                // Start Menu Programs shortcut
                string programsDir = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
                string startMenuLnk = Path.Combine(programsDir, $"{appName}.lnk");
                CreateShellLink(startMenuLnk, exePath);

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[StartupRegistrationService] Shortcut error: {ex.Message}");
                return false;
            }
        }

        private static void CreateShellLink(string shortcutPath, string targetPath)
        {
            try
            {
                Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) return;

                dynamic shell = Activator.CreateInstance(shellType)!;
                dynamic shortcut = shell.CreateShortcut(shortcutPath);
                shortcut.TargetPath = targetPath;
                shortcut.WorkingDirectory = Path.GetDirectoryName(targetPath);
                shortcut.Description = "Kerkenez Calendar";
                shortcut.Save();
            }
            catch { }
        }
    }
}
