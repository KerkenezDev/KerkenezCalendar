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

        public static string StartMenuShortcutPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Programs),
            "Kerkenez Calendar.lnk");

        public static string DesktopShortcutPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            "Kerkenez Calendar.lnk");

        public static bool ShortcutsExist => File.Exists(StartMenuShortcutPath) || File.Exists(DesktopShortcutPath);

        public static string GetExecutablePath()
        {
            // 1. Check Environment.ProcessPath or Application.ExecutablePath if it's already KerkenezCalendar.exe
            string? exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath))
            {
                try { exePath = Application.ExecutablePath; } catch { }
            }

            if (!string.IsNullOrEmpty(exePath) &&
                Path.GetFileName(exePath).Equals("KerkenezCalendar.exe", StringComparison.OrdinalIgnoreCase) &&
                File.Exists(exePath))
            {
                return exePath;
            }

            // 2. Check the directory where this assembly itself is located
            try
            {
#pragma warning disable IL3000
                string asmLocation = typeof(StartupRegistrationService).Assembly.Location;
#pragma warning restore IL3000
                if (!string.IsNullOrEmpty(asmLocation))
                {
                    string asmDir = Path.GetDirectoryName(asmLocation) ?? "";
                    string candidate = Path.Combine(asmDir, "KerkenezCalendar.exe");
                    if (File.Exists(candidate)) return candidate;
                }
            }
            catch { }

            // 3. Check AppContext.BaseDirectory
            try
            {
                string baseDir = AppContext.BaseDirectory;
                if (!string.IsNullOrEmpty(baseDir))
                {
                    string candidate = Path.Combine(baseDir, "KerkenezCalendar.exe");
                    if (File.Exists(candidate)) return candidate;
                }
            }
            catch { }

            // 4. Check AppDomain.CurrentDomain.BaseDirectory
            try
            {
                string domainDir = AppDomain.CurrentDomain.BaseDirectory;
                if (!string.IsNullOrEmpty(domainDir))
                {
                    string candidate = Path.Combine(domainDir, "KerkenezCalendar.exe");
                    if (File.Exists(candidate)) return candidate;
                }
            }
            catch { }

            // 5. Check known publish or ProgramFiles folders
            string[] knownPaths = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Programs", "ProgramFiles", "KerkenezCalendar", "publish", "KerkenezCalendar.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Programs", "ProgramFiles", "KerkenezCalendar-dev", "publish", "KerkenezCalendar.exe")
            };
            foreach (var kp in knownPaths)
            {
                if (File.Exists(kp)) return kp;
            }

            // Fallback to whatever exePath was if not empty
            return exePath ?? "";
        }

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
                    string exePath = GetExecutablePath();
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

        public static bool CreateShortcuts(bool createDesktop = true, bool createStartMenu = true)
        {
            try
            {
                string exePath = GetExecutablePath();
                if (!File.Exists(exePath)) return false;

                bool allOk = true;

                if (createDesktop)
                {
                    if (!CreateShellLink(DesktopShortcutPath, exePath))
                        allOk = false;
                }

                if (createStartMenu)
                {
                    if (!CreateShellLink(StartMenuShortcutPath, exePath))
                        allOk = false;
                }

                NativeMethods.RefreshShellIcons();
                return allOk;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[StartupRegistrationService] Shortcut error: {ex.Message}");
                return false;
            }
        }

        private static bool CreateShellLink(string shortcutPath, string targetPath)
        {
            try
            {
                string dir = Path.GetDirectoryName(shortcutPath) ?? "";
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                // If file exists, delete it first to ensure fresh icon and properties without stale cache
                if (File.Exists(shortcutPath))
                {
                    try { File.Delete(shortcutPath); } catch { }
                }

                Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) return false;

                dynamic shell = Activator.CreateInstance(shellType)!;
                dynamic shortcut = shell.CreateShortcut(shortcutPath);
                shortcut.TargetPath = targetPath;
                shortcut.WorkingDirectory = Path.GetDirectoryName(targetPath);
                shortcut.Description = "Kerkenez Calendar - Lightweight Desktop Calendar";
                shortcut.IconLocation = targetPath + ",0";
                shortcut.Save();
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[StartupRegistrationService] CreateShellLink error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Heals the HKCU Run startup entry only if it currently exists, updating its path to the current executable.
        /// Does nothing if the entry does not exist.
        /// </summary>
        public static bool HealStartupRunKeyIfExists(string exePath)
        {
            try
            {
                using var runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
                if (runKey == null) return false;

                var currentVal = runKey.GetValue(RunValueName) as string;
                var legacyVal = runKey.GetValue("KerkenezCalendar") as string;

                // Only update if startup was already registered
                if (!string.IsNullOrWhiteSpace(currentVal) || !string.IsNullOrWhiteSpace(legacyVal))
                {
                    string expectedVal = $"\"{exePath}\" --daemon";

                    bool changed = false;
                    if (!string.Equals(currentVal, expectedVal, StringComparison.OrdinalIgnoreCase))
                    {
                        runKey.SetValue(RunValueName, expectedVal);
                        changed = true;
                    }

                    if (!string.IsNullOrWhiteSpace(legacyVal))
                    {
                        runKey.DeleteValue("KerkenezCalendar", false);
                        changed = true;
                    }

                    return changed;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[StartupRegistrationService] HealStartupRunKey error: {ex.Message}");
            }
            return false;
        }

        /// <summary>
        /// Checks Desktop and Start Menu shortcuts independently and heals their target path, icon,
        /// and working directory ONLY IF they already exist on disk.
        /// Does NOT create any missing shortcuts.
        /// </summary>
        public static bool HealShortcutsIfExist(string exePath)
        {
            try
            {
                bool desktopExists = File.Exists(DesktopShortcutPath);
                bool startMenuExists = File.Exists(StartMenuShortcutPath);

                if (!desktopExists && !startMenuExists) return false;

                bool anyChanged = false;

                if (desktopExists)
                {
                    if (HealSingleShortcut(DesktopShortcutPath, exePath))
                        anyChanged = true;
                }

                if (startMenuExists)
                {
                    if (HealSingleShortcut(StartMenuShortcutPath, exePath))
                        anyChanged = true;
                }

                if (anyChanged)
                {
                    NativeMethods.RefreshShellIcons();
                }

                return anyChanged;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[StartupRegistrationService] HealShortcuts error: {ex.Message}");
                return false;
            }
        }

        private static bool HealSingleShortcut(string shortcutPath, string exePath)
        {
            try
            {
                Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) return false;

                dynamic shell = Activator.CreateInstance(shellType)!;
                dynamic shortcut = shell.CreateShortcut(shortcutPath);

                string currentTarget = (string)shortcut.TargetPath;
                string currentIcon = (string)shortcut.IconLocation;
                string expectedIcon = exePath + ",0";
                string expectedWorkDir = Path.GetDirectoryName(exePath) ?? "";

                if (!string.Equals(currentTarget, exePath, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(currentIcon, expectedIcon, StringComparison.OrdinalIgnoreCase))
                {
                    shortcut.TargetPath = exePath;
                    shortcut.WorkingDirectory = expectedWorkDir;
                    shortcut.Description = "Kerkenez Calendar - Lightweight Desktop Calendar";
                    shortcut.IconLocation = expectedIcon;
                    shortcut.Save();
                    return true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[StartupRegistrationService] HealSingleShortcut error: {ex.Message}");
            }
            return false;
        }

        public static void UpdateShortcutsIfMoved()
        {
            string exePath = GetExecutablePath();
            if (!string.IsNullOrWhiteSpace(exePath) && File.Exists(exePath))
            {
                HealShortcutsIfExist(exePath);
            }
        }

        public static void DeleteShortcuts()
        {
            var candidatePaths = new[]
            {
                StartMenuShortcutPath,
                DesktopShortcutPath,
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Kerkenez Calendar.lnk"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), "Kerkenez Calendar.lnk"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory), "Kerkenez Calendar.lnk")
            };

            foreach (var path in candidatePaths)
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                    {
                        File.Delete(path);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[StartupRegistrationService] Error deleting shortcut at '{path}': {ex.Message}");
                }
            }

            NativeMethods.RefreshShellIcons();
        }
    }
}
