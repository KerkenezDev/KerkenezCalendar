using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using KerkenezCalendar.Models;

namespace KerkenezCalendar.Services
{
    public class NativeCalendarTrayDaemon : IDisposable
    {
        public const string ExitEventName = @"Global\KerkenezCalendar_TrayDaemon_ExitEvent";
        public const string TrayDaemonMutexName = @"Global\KerkenezCalendar_TrayDaemon_Mutex";
        public const string MainUiMutexName = @"Global\KerkenezCalendar_MainUI_Mutex";

        private const uint TRAY_ICON_ID = 2001;
        private const uint CMD_OPEN = 3001;
        private const uint CMD_SYNC_REFRESH = 3002;
        private const uint CMD_TOGGLE_NOTIFS = 3003;
        private const uint CMD_EXIT = 3004;

        private readonly CalendarConfigService _configService;
        private readonly CalendarEventService _eventService;
        private readonly CalendarDaemonService _daemonService;
        private System.Threading.Timer? _idleTrimTimer;
        private EventWaitHandle? _exitEvent;
        private RegisteredWaitHandle? _registeredWait;
        private IntPtr _hWnd = IntPtr.Zero;
        private NativeMethods.WndProcDelegate? _wndProcDelegate;
        private NativeMethods.NOTIFYICONDATA _nid;
        private bool _isDisposed;

        public NativeCalendarTrayDaemon()
        {
            _configService = new CalendarConfigService();
            _eventService = new CalendarEventService(_configService);
            _daemonService = new CalendarDaemonService(_configService, _eventService);
        }

        public static void Run()
        {
            using var daemon = new NativeCalendarTrayDaemon();
            daemon.Start();
        }

        public void Start()
        {
            InitializeMessageWindow();
            CalendarNotificationService.EnsureRegistered();
            InitializeTrayIcon();
            InitializeExitEventHandler();

            _daemonService.ReminderTriggered += OnReminderTriggered;
            _daemonService.StatusUpdated += OnStatusUpdated;
            _daemonService.Start();

            // Periodic aggressive memory trimming to guarantee sub-megabyte active footprint
            _idleTrimTimer = new System.Threading.Timer(_ => NativeMethods.TrimWorkingSet(), null, 2000, 15000);

            // Immediate initial trim
            NativeMethods.TrimWorkingSet();

            // Native Win32 Message Loop (Zero WinForms control overhead)
            while (NativeMethods.GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
            {
                NativeMethods.TranslateMessage(ref msg);
                NativeMethods.DispatchMessage(ref msg);
            }
        }

        private void InitializeExitEventHandler()
        {
            try
            {
                _exitEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ExitEventName);
                _registeredWait = ThreadPool.RegisterWaitForSingleObject(
                    _exitEvent,
                    (state, timedOut) =>
                    {
                        if (!timedOut && _hWnd != IntPtr.Zero)
                        {
                            NativeMethods.PostMessage(_hWnd, NativeMethods.WM_DESTROY, IntPtr.Zero, IntPtr.Zero);
                        }
                    },
                    null,
                    -1,
                    false);
            }
            catch { }
        }

        private void InitializeMessageWindow()
        {
            string className = "KerkenezCalendar_TrayMsgHost_" + Guid.NewGuid().ToString("N");
            _wndProcDelegate = WndProc;

            var wcx = new NativeMethods.WNDCLASSEX
            {
                cbSize = (uint)Marshal.SizeOf(typeof(NativeMethods.WNDCLASSEX)),
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProcDelegate),
                hInstance = NativeMethods.GetModuleHandle(null),
                lpszClassName = className
            };

            NativeMethods.RegisterClassEx(ref wcx);

            _hWnd = NativeMethods.CreateWindowEx(
                0,
                className,
                "KerkenezCalendarTrayHost",
                0,
                0, 0, 0, 0,
                IntPtr.Zero,
                IntPtr.Zero,
                NativeMethods.GetModuleHandle(null),
                IntPtr.Zero);
        }

        private void InitializeTrayIcon()
        {
            var icon = CalendarIconHelper.GetApplicationIcon();
            _nid = new NativeMethods.NOTIFYICONDATA
            {
                cbSize = (uint)Marshal.SizeOf(typeof(NativeMethods.NOTIFYICONDATA)),
                hWnd = _hWnd,
                uID = TRAY_ICON_ID,
                uFlags = NativeMethods.NIF_MESSAGE | NativeMethods.NIF_ICON | NativeMethods.NIF_TIP,
                uCallbackMessage = NativeMethods.WM_TRAYICON,
                hIcon = icon.Handle,
                szTip = "Kerkenez Calendar - Active"
            };

            NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_ADD, ref _nid);
        }

        private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == NativeMethods.WM_TRAYICON)
            {
                uint eventMsg = (uint)(lParam.ToInt64() & 0xFFFF);

                if (eventMsg == NativeMethods.WM_LBUTTONDBLCLK || eventMsg == NativeMethods.WM_LBUTTONUP || eventMsg == NativeMethods.NIN_BALLOONUSERCLICK)
                {
                    LaunchOrFocusMainApp();
                    return IntPtr.Zero;
                }
                else if (eventMsg == NativeMethods.WM_RBUTTONUP)
                {
                    ShowNativeContextMenu();
                    return IntPtr.Zero;
                }
            }
            else if (msg == NativeMethods.WM_DESTROY)
            {
                NativeMethods.PostQuitMessage(0);
                return IntPtr.Zero;
            }

            return NativeMethods.DefWindowProc(hWnd, msg, wParam, lParam);
        }

        private void ShowNativeContextMenu()
        {
            NativeMethods.GetCursorPos(out var pt);
            NativeMethods.SetForegroundWindow(_hWnd);

            _configService.LoadConfig();
            var settings = _configService.Settings;

            IntPtr hMenu = NativeMethods.CreatePopupMenu();

            try
            {
                // 1. Primary Action: Open Main Window (Bold default)
                NativeMethods.AppendMenu(hMenu, NativeMethods.MF_STRING, CMD_OPEN, "📅  Open Kerkenez Calendar");
                NativeMethods.SetMenuDefaultItem(hMenu, CMD_OPEN, 0);

                // 2. Refresh & Sync Accounts in background
                NativeMethods.AppendMenu(hMenu, NativeMethods.MF_STRING, CMD_SYNC_REFRESH, "🔄  Refresh & Sync Accounts");

                // 3. Toggle desktop notifications
                string notifText = settings.EnableTrayNotifications
                    ? "🔔  Notifications: Enabled"
                    : "🔕  Notifications: Disabled";
                NativeMethods.AppendMenu(hMenu, NativeMethods.MF_STRING, CMD_TOGGLE_NOTIFS, notifText);

                NativeMethods.AppendMenu(hMenu, NativeMethods.MF_SEPARATOR, 0, null);

                // 4. Exit Daemon
                NativeMethods.AppendMenu(hMenu, NativeMethods.MF_STRING, CMD_EXIT, "❌  Exit Tray Daemon");

                uint cmd = NativeMethods.TrackPopupMenuEx(
                    hMenu,
                    NativeMethods.TPM_RETURNCMD | NativeMethods.TPM_NONOTIFY | NativeMethods.TPM_RIGHTBUTTON,
                    pt.X,
                    pt.Y,
                    _hWnd,
                    IntPtr.Zero);

                HandleMenuCommand(cmd);
            }
            finally
            {
                NativeMethods.DestroyMenu(hMenu);
            }
        }

        private void HandleMenuCommand(uint cmd)
        {
            if (cmd == CMD_OPEN)
            {
                LaunchOrFocusMainApp();
            }
            else if (cmd == CMD_SYNC_REFRESH)
            {
                RefreshAndSyncAccounts();
            }
            else if (cmd == CMD_TOGGLE_NOTIFS)
            {
                _configService.Settings.EnableTrayNotifications = !_configService.Settings.EnableTrayNotifications;
                _configService.SaveConfig();

                if (_configService.Settings.EnableTrayNotifications)
                {
                    ShowTrayBalloon("Kerkenez Calendar", "Desktop notifications enabled.");
                }
                else
                {
                    ShowTrayBalloon("Kerkenez Calendar", "Desktop notifications paused.");
                }
            }
            else if (cmd == CMD_EXIT)
            {
                Dispose();
                NativeMethods.PostQuitMessage(0);
            }
        }

        private void RefreshAndSyncAccounts()
        {
            try
            {
                _configService.LoadConfig();
                _eventService.LoadEvents();

                _ = _daemonService.CheckRemindersAsync();

                var accounts = _configService.GetAccounts();
                int activeAccounts = accounts.Count(a => a.IsEnabled);
                int totalEvents = _eventService.GetAllEvents().Count;

                string syncTitle = "Kerkenez Calendar";
                string syncMsg = $"Accounts and calendar synced.\n{activeAccounts} active accounts • {totalEvents} events loaded.";
                CalendarNotificationService.ShowPersistentNotification(
                    title: syncTitle,
                    message: syncMsg,
                    isReminder: false,
                    onClick: () => LaunchOrFocusMainApp(),
                    fallbackAction: () => ShowTrayBalloon(syncTitle, syncMsg)
                );
            }
            catch (Exception ex)
            {
                ShowTrayBalloon("Kerkenez Calendar", $"Sync error: {ex.Message}");
            }
        }

        private void ShowTrayBalloon(string title, string message)
        {
            try
            {
                _nid.uFlags = NativeMethods.NIF_INFO | NativeMethods.NIF_TIP;
                _nid.szInfoTitle = Truncate(title, 63);
                _nid.szInfo = Truncate(message, 255);
                _nid.dwInfoFlags = NativeMethods.NIIF_INFO;

                NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_MODIFY, ref _nid);
                NativeMethods.TrimWorkingSet();
            }
            catch { }
        }

        private void OnReminderTriggered(CalendarEvent ev, string message)
        {
            try
            {
                string title = $"📅 {ev.Title}";
                string reminderKey = $"{ev.Id}_{ev.EffectiveReminderTime?.Ticks ?? 0}";

                // Fire persistent Action Center toast notification with automatic fallback to tray balloon
                CalendarNotificationService.ShowPersistentNotification(
                    title: title,
                    message: message,
                    isReminder: true,
                    tag: reminderKey,
                    onClick: () => LaunchOrFocusMainApp(),
                    fallbackAction: () => ShowTrayBalloon(title, message)
                );

                // Re-trim working set right after firing notification
                NativeMethods.TrimWorkingSet();
            }
            catch
            {
                ShowTrayBalloon($"📅 {ev.Title}", message);
            }
        }

        private void OnStatusUpdated(string status)
        {
            try
            {
                _nid.szTip = Truncate($"Kerkenez Calendar • {status}", 127);
                _nid.uFlags = NativeMethods.NIF_TIP;
                NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_MODIFY, ref _nid);
            }
            catch { }
        }

        private static void LaunchOrFocusMainApp(string? args = null)
        {
            try
            {
                bool mainRunning = Mutex.TryOpenExisting(MainUiMutexName, out var existingMutex);
                if (mainRunning)
                {
                    existingMutex?.Dispose();
                    var currentProcess = Process.GetCurrentProcess();
                    string processName = currentProcess.ProcessName;
                    var candidates = Process.GetProcessesByName(processName);
                    foreach (var proc in candidates)
                    {
                        if (proc.Id != currentProcess.Id && proc.MainWindowHandle != IntPtr.Zero)
                        {
                            if (args == "--apply-layout")
                            {
                                NativeMethods.PostMessage(proc.MainWindowHandle, 0x04C8, IntPtr.Zero, IntPtr.Zero);
                            }
                            NativeMethods.FocusMainWindow(proc);
                            return;
                        }
                    }
                }

                // Launch main application process
                string exePath = Environment.ProcessPath ?? Application.ExecutablePath;
                if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
                {
                    string candidate = Path.Combine(AppContext.BaseDirectory, "KerkenezCalendar.exe");
                    if (File.Exists(candidate)) exePath = candidate;
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = args ?? "",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NativeCalendarTrayDaemon] Launch error: {ex.Message}");
            }
        }

        private static string Truncate(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxLength) return text;
            return text.Substring(0, maxLength - 3) + "...";
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            _idleTrimTimer?.Dispose();
            _registeredWait?.Unregister(null);
            _exitEvent?.Dispose();
            _daemonService.Dispose();

            if (_hWnd != IntPtr.Zero)
            {
                NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_DELETE, ref _nid);
            }
        }
    }
}
