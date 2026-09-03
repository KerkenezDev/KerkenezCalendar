using System;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;

namespace KerkenezCalendar.Services
{
    public static class CalendarDaemonHelper
    {
        public static bool IsDaemonRunning()
        {
            try
            {
                bool running = Mutex.TryOpenExisting(NativeCalendarTrayDaemon.TrayDaemonMutexName, out var m);
                m?.Dispose();
                return running;
            }
            catch
            {
                return false;
            }
        }

        public static void StartDaemon()
        {
            if (IsDaemonRunning()) return;

            try
            {
                string exePath = Environment.ProcessPath ?? Application.ExecutablePath;
                if (string.IsNullOrEmpty(exePath) || !exePath.EndsWith("KerkenezCalendar.exe", StringComparison.OrdinalIgnoreCase))
                {
                    string candidate = System.IO.Path.Combine(AppContext.BaseDirectory, "KerkenezCalendar.exe");
                    if (System.IO.File.Exists(candidate)) exePath = candidate;
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = "--daemon",
                    CreateNoWindow = true,
                    UseShellExecute = false
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CalendarDaemonHelper] Start error: {ex.Message}");
            }
        }

        public static void StopDaemon()
        {
            // 1. Signal graceful exit event
            try
            {
                if (EventWaitHandle.TryOpenExisting(NativeCalendarTrayDaemon.ExitEventName, out var exitEv))
                {
                    exitEv.Set();
                    exitEv.Dispose();
                }
            }
            catch { }

            Thread.Sleep(200);

            // 2. Terminate any remaining background daemon processes
            try
            {
                var curProc = Process.GetCurrentProcess();
                foreach (var proc in Process.GetProcessesByName("KerkenezCalendar"))
                {
                    if (proc.Id != curProc.Id)
                    {
                        try
                        {
                            // A background tray daemon has MainWindowHandle == IntPtr.Zero
                            if (proc.MainWindowHandle == IntPtr.Zero)
                            {
                                proc.Kill();
                                proc.WaitForExit(500);
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }

        public static void RestartDaemon()
        {
            StopDaemon();
            Thread.Sleep(300);
            StartDaemon();
        }
    }
}
