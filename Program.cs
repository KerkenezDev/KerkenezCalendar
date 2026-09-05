using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using KerkenezCalendar.Services;
using KerkenezCalendar.UI;

namespace KerkenezCalendar
{
    static class Program
    {
        private const string MainUiMutexName = @"Global\KerkenezCalendar_MainUI_Mutex";
        private const string TrayDaemonMutexName = @"Global\KerkenezCalendar_TrayDaemon_Mutex";

        [STAThread]
        static void Main(string[] args)
        {
            // 1. Lightweight auto-healing of existing registry keys and shortcuts if executable moved
            AutoHealService.AutoHeal();

            // 2. Handle --uninstall switch
            if (args != null && args.Any(a =>
                a.Equals("--uninstall", StringComparison.OrdinalIgnoreCase) ||
                a.Equals("/uninstall", StringComparison.OrdinalIgnoreCase) ||
                a.Equals("-uninstall", StringComparison.OrdinalIgnoreCase)))
            {
                bool isQuiet = args.Any(a =>
                    a.Equals("--quiet", StringComparison.OrdinalIgnoreCase) ||
                    a.Equals("/quiet", StringComparison.OrdinalIgnoreCase) ||
                    a.Equals("-quiet", StringComparison.OrdinalIgnoreCase) ||
                    a.Equals("--silent", StringComparison.OrdinalIgnoreCase) ||
                    a.Equals("/silent", StringComparison.OrdinalIgnoreCase) ||
                    a.Equals("-silent", StringComparison.OrdinalIgnoreCase) ||
                    a.Equals("-s", StringComparison.OrdinalIgnoreCase) ||
                    a.Equals("-q", StringComparison.OrdinalIgnoreCase));

                if (!isQuiet)
                {
                    ApplicationConfiguration.Initialize();
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                }
                HandleUninstall(isQuiet);
                return;
            }

            // 3. Handle --daemon or --tray switch (Background System Tray Daemon)
            bool isDaemonMode = args != null && args.Any(a =>
                a.Equals("--daemon", StringComparison.OrdinalIgnoreCase) ||
                a.Equals("/daemon", StringComparison.OrdinalIgnoreCase) ||
                a.Equals("-daemon", StringComparison.OrdinalIgnoreCase) ||
                a.Equals("--tray", StringComparison.OrdinalIgnoreCase) ||
                a.Equals("/tray", StringComparison.OrdinalIgnoreCase) ||
                a.Equals("-tray", StringComparison.OrdinalIgnoreCase));

            if (isDaemonMode)
            {
                RunDaemonMode();
                return;
            }

            // 3. Normal Execution (Main GUI Application)
            RunMainUiMode(args);
        }

        private static void RunDaemonMode()
        {
            using var mutex = new Mutex(true, TrayDaemonMutexName, out bool createdNew);
            if (!createdNew)
            {
                // Another daemon instance is already active
                return;
            }

            try
            {
                NativeCalendarTrayDaemon.Run();
            }
            finally
            {
                try { mutex.ReleaseMutex(); } catch { }
            }
        }

        private static void RunMainUiMode(string[]? args)
        {
            // Check if another instance of the Main UI is already running
            using var mainMutex = new Mutex(true, MainUiMutexName, out bool createdNewMain);
            if (!createdNewMain)
            {
                // Focus the existing main window and exit
                FocusExistingMainWindow();
                return;
            }

            try
            {
                ApplicationConfiguration.Initialize();
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                CalendarNotificationService.EnsureRegistered();

                var configService = new CalendarConfigService();
                var eventService = new CalendarEventService(configService);

                var mainForm = new MainForm(configService, eventService);
                Application.Run(mainForm);
            }
            finally
            {
                try { mainMutex.ReleaseMutex(); } catch { }
            }
        }

        private static void FocusExistingMainWindow()
        {
            try
            {
                var currentProcess = Process.GetCurrentProcess();
                string processName = currentProcess.ProcessName;
                var candidates = Process.GetProcessesByName(processName);

                foreach (var proc in candidates)
                {
                    if (proc.Id != currentProcess.Id && proc.MainWindowHandle != IntPtr.Zero)
                    {
                        NativeMethods.FocusMainWindow(proc);
                        return;
                    }
                }
            }
            catch { }
        }

        private static void HandleUninstall(bool isQuiet)
        {
            if (isQuiet)
            {
                try
                {
                    PerformUninstall();
                }
                catch { }
                return;
            }

            var res = MessageBox.Show(
                "Are you sure you want to uninstall Kerkenez Calendar?\n\nThis will stop background daemons, remove Desktop and Start Menu shortcuts, delete Windows startup entries, remove Installed Apps registration, and delete calendar configuration and events from %APPDATA%\\Kerkenez\\calendar.",
                "Uninstall Kerkenez Calendar",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (res == DialogResult.Yes)
            {
                bool success = PerformUninstall();
                if (success)
                {
                    MessageBox.Show(
                        "Kerkenez Calendar shortcuts, startup entries, Windows Installed Apps registration, and calendar data have been successfully removed.",
                        "Uninstall Complete",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show(
                        "Failed to completely remove all configuration files or shortcuts.",
                        "Uninstall Warning",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
            }
        }

        private static bool PerformUninstall()
        {
            try
            {
                // 1. Stop background tray daemon if running
                CalendarDaemonHelper.StopDaemon();

                // 2. Remove Windows startup run key
                StartupRegistrationService.SetStartupEnabled(false);

                // 3. Remove Windows Installed Apps registry entry
                UninstallRegistrationService.Unregister();

                // 4. Remove Desktop and Start Menu shortcuts
                StartupRegistrationService.DeleteShortcuts();

                // 5. Remove calendar folder (%APPDATA%\Kerkenez\calendar)
                if (System.IO.Directory.Exists(CalendarConfigService.CalendarFolder))
                {
                    System.IO.Directory.Delete(CalendarConfigService.CalendarFolder, true);
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Program] Error during uninstall: {ex.Message}");
                return false;
            }
        }
    }
}
