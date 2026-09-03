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
            // 1. Handle --daemon or --tray switch (Background System Tray Daemon)
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

            // 2. Normal Execution (Main GUI Application)
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
    }
}
