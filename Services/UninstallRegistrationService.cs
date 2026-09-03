using System;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;

namespace KerkenezCalendar.Services
{
    public static class UninstallRegistrationService
    {
        private const string UninstallKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\KerkenezCalendar";
        private const string DisplayName = "Kerkenez Calendar";
        private const string DisplayVersion = "1.0.0";
        private const string Publisher = "KerkenezDev";
        private const string UrlInfoAbout = "https://github.com/KerkenezDev/KerkenezCalendar";
        private const string HelpLink = "https://github.com/KerkenezDev/KerkenezCalendar";

        /// <summary>
        /// Registers or updates the application in HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\KerkenezCalendar
        /// so it shows up in Windows Settings -> Installed Apps and Control Panel -> Programs and Features.
        /// Also updates existing shortcuts if the executable path was moved.
        /// </summary>
        public static void RegisterOrUpdate()
        {
            try
            {
                string exePath = StartupRegistrationService.GetExecutablePath();
                if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath)) return;

                // 1. Update Uninstall Entry for Windows Installed Apps
                UpdateUninstallEntry(exePath);

                // 2. Update existing shortcuts if moved
                StartupRegistrationService.UpdateShortcutsIfMoved();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UninstallRegistrationService] Error during register/update: {ex.Message}");
            }
        }

        /// <summary>
        /// Heals the HKCU Uninstall registry key only if it currently exists, updating all paths to the current executable.
        /// Returns true if updated, false if key does not exist or was already up-to-date.
        /// </summary>
        public static bool HealUninstallEntryIfExists(string exePath)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(UninstallKeyPath, true);
                if (key == null) return false;

                string installDir = Path.GetDirectoryName(exePath) ?? "";
                string uninstallCmd = $"\"{exePath}\" --uninstall";
                string quietUninstallCmd = $"\"{exePath}\" --uninstall --quiet";
                string displayIcon = $"\"{exePath}\",0";

                var currentUninstall = key.GetValue("UninstallString") as string;
                var currentQuiet = key.GetValue("QuietUninstallString") as string;
                var currentLocation = key.GetValue("InstallLocation") as string;
                var currentIcon = key.GetValue("DisplayIcon") as string;

                if (!string.Equals(currentUninstall, uninstallCmd, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(currentQuiet, quietUninstallCmd, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(currentLocation, installDir, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(currentIcon, displayIcon, StringComparison.OrdinalIgnoreCase))
                {
                    key.SetValue("DisplayName", DisplayName);
                    key.SetValue("DisplayVersion", DisplayVersion);
                    key.SetValue("Publisher", Publisher);
                    key.SetValue("DisplayIcon", displayIcon);
                    key.SetValue("InstallLocation", installDir);
                    key.SetValue("UninstallString", uninstallCmd);
                    key.SetValue("QuietUninstallString", quietUninstallCmd);
                    key.SetValue("URLInfoAbout", UrlInfoAbout);
                    key.SetValue("HelpLink", HelpLink);
                    key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                    key.SetValue("NoRepair", 1, RegistryValueKind.DWord);

                    try
                    {
                        var fi = new FileInfo(exePath);
                        long sizeKb = fi.Length / 1024;
                        key.SetValue("EstimatedSize", (int)sizeKb, RegistryValueKind.DWord);
                    }
                    catch { }

                    return true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UninstallRegistrationService] HealUninstallEntry error: {ex.Message}");
            }
            return false;
        }

        private static void UpdateUninstallEntry(string exePath)
        {
            try
            {
                string installDir = Path.GetDirectoryName(exePath) ?? "";
                string uninstallCmd = $"\"{exePath}\" --uninstall";
                string quietUninstallCmd = $"\"{exePath}\" --uninstall --quiet";
                string displayIcon = $"\"{exePath}\",0";

                using var key = Registry.CurrentUser.CreateSubKey(UninstallKeyPath, true);
                if (key != null)
                {
                    var currentUninstall = key.GetValue("UninstallString") as string;
                    var currentQuiet = key.GetValue("QuietUninstallString") as string;
                    var currentLocation = key.GetValue("InstallLocation") as string;
                    var currentIcon = key.GetValue("DisplayIcon") as string;

                    if (!string.Equals(currentUninstall, uninstallCmd, StringComparison.OrdinalIgnoreCase) ||
                        !string.Equals(currentQuiet, quietUninstallCmd, StringComparison.OrdinalIgnoreCase) ||
                        !string.Equals(currentLocation, installDir, StringComparison.OrdinalIgnoreCase) ||
                        !string.Equals(currentIcon, displayIcon, StringComparison.OrdinalIgnoreCase))
                    {
                        key.SetValue("DisplayName", DisplayName);
                        key.SetValue("DisplayVersion", DisplayVersion);
                        key.SetValue("Publisher", Publisher);
                        key.SetValue("DisplayIcon", displayIcon);
                        key.SetValue("InstallLocation", installDir);
                        key.SetValue("UninstallString", uninstallCmd);
                        key.SetValue("QuietUninstallString", quietUninstallCmd);
                        key.SetValue("URLInfoAbout", UrlInfoAbout);
                        key.SetValue("HelpLink", HelpLink);
                        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);

                        if (key.GetValue("InstallDate") == null)
                        {
                            key.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
                        }

                        try
                        {
                            var fi = new FileInfo(exePath);
                            long sizeKb = fi.Length / 1024;
                            key.SetValue("EstimatedSize", (int)sizeKb, RegistryValueKind.DWord);
                        }
                        catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UninstallRegistrationService] Error updating uninstall entry: {ex.Message}");
            }
        }

        /// <summary>
        /// Deletes the application registration key from HKCU Uninstall registry.
        /// </summary>
        public static void Unregister()
        {
            try
            {
                Registry.CurrentUser.DeleteSubKeyTree(UninstallKeyPath, false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UninstallRegistrationService] Error unregistering app: {ex.Message}");
            }
        }
    }
}
