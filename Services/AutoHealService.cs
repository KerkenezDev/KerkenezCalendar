using System;
using System.IO;

namespace KerkenezCalendar.Services
{
    public static class AutoHealService
    {
        /// <summary>
        /// Performs ultra-lightweight auto-healing every time Kerkenez Calendar starts.
        /// Checks and heals the location of existing registry keys (Startup Run key, Uninstall key)
        /// and existing shortcuts (Desktop, Start Menu) ONLY IF they currently exist on the user's system.
        /// Does NOT create any missing shortcuts or registry keys if they are not already present.
        /// </summary>
        public static void AutoHeal()
        {
            try
            {
                string exePath = StartupRegistrationService.GetExecutablePath();
                if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath)) return;

                // 1. Heal Windows Startup Run registry key only if it currently exists
                StartupRegistrationService.HealStartupRunKeyIfExists(exePath);

                // 2. Heal Windows Installed Apps Uninstall registry key only if it currently exists
                UninstallRegistrationService.HealUninstallEntryIfExists(exePath);

                // 3. Heal Desktop and Start Menu shortcuts only if each exists
                StartupRegistrationService.HealShortcutsIfExist(exePath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AutoHealService] Auto-heal error: {ex.Message}");
            }
        }
    }
}
