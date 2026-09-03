using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Windows.Forms;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace KerkenezCalendar.Services
{
    /// <summary>
    /// Delivers persistent Windows toast notifications that remain in the Windows Action Center
    /// until dismissed or clicked by the user, with automatic fallback to system tray balloon tips.
    /// </summary>
    public static class CalendarNotificationService
    {
        public const string AppDisplayName = "Kerkenez Calendar";
        private static string? _cachedAppId;
        private static bool _isRegistered = false;
        private static readonly object _lock = new();

        [DllImport("shell32.dll", SetLastError = true)]
        private static extern void SetCurrentProcessExplicitAppUserModelID([MarshalAs(UnmanagedType.LPWStr)] string AppID);

        public static string StartMenuShortcutPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Programs),
            "Kerkenez Calendar.lnk");

        public static string ResolveAppId()
        {
            if (_cachedAppId != null) return _cachedAppId;

            try
            {
                if (File.Exists(StartMenuShortcutPath))
                {
                    Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
                    if (shellType != null)
                    {
                        dynamic shell = Activator.CreateInstance(shellType)!;
                        dynamic shortcut = shell.CreateShortcut(StartMenuShortcutPath);
                        string target = (string)shortcut.TargetPath;
                        if (!string.IsNullOrWhiteSpace(target) && File.Exists(target))
                        {
                            return _cachedAppId = target;
                        }
                    }
                }
                else
                {
                    EnsureStartMenuShortcut();
                }
            }
            catch { }

            string exePath = Environment.ProcessPath ?? Application.ExecutablePath;
            if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
            {
                string candidate = Path.Combine(AppContext.BaseDirectory, "KerkenezCalendar.exe");
                if (File.Exists(candidate)) exePath = candidate;
            }

            return _cachedAppId = exePath;
        }

        public static void EnsureStartMenuShortcut()
        {
            try
            {
                string exePath = Environment.ProcessPath ?? Application.ExecutablePath;
                if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
                {
                    string candidate = Path.Combine(AppContext.BaseDirectory, "KerkenezCalendar.exe");
                    if (File.Exists(candidate)) exePath = candidate;
                }

                if (!File.Exists(exePath)) return;

                Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) return;

                string dir = Path.GetDirectoryName(StartMenuShortcutPath) ?? "";
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                dynamic shell = Activator.CreateInstance(shellType)!;
                dynamic shortcut = shell.CreateShortcut(StartMenuShortcutPath);
                shortcut.TargetPath = exePath;
                shortcut.WorkingDirectory = Path.GetDirectoryName(exePath);
                shortcut.Description = "Kerkenez Calendar - Lightweight Desktop Calendar";
                shortcut.IconLocation = exePath + ",0";
                shortcut.Save();
            }
            catch { }
        }

        public static void EnsureRegistered()
        {
            if (_isRegistered) return;

            lock (_lock)
            {
                if (_isRegistered) return;

                try
                {
                    string appId = ResolveAppId();
                    SetCurrentProcessExplicitAppUserModelID(appId);
                    _isRegistered = true;
                }
                catch { }
            }
        }

        /// <summary>
        /// Displays a persistent Windows toast notification that stays in Action Center history.
        /// </summary>
        public static bool ShowPersistentNotification(
            string title,
            string message,
            bool isReminder = true,
            string? tag = null,
            Action? onClick = null,
            Action? fallbackAction = null)
        {
            try
            {
                EnsureRegistered();
                string appId = ResolveAppId();

                string safeTitle = SecurityElement.Escape(title ?? AppDisplayName);
                string safeMessage = SecurityElement.Escape(message ?? "");

                // Using scenario="reminder" gives it persistent Action Center retention and reminder behavior
                string toastXml = isReminder
                    ? $@"
<toast scenario=""reminder"">
    <visual>
        <binding template=""ToastGeneric"">
            <text>{safeTitle}</text>
            <text>{safeMessage}</text>
        </binding>
    </visual>
    <actions>
        <action content=""Open Calendar"" arguments=""open"" activationType=""foreground""/>
        <action content=""Dismiss"" arguments=""dismiss"" activationType=""system""/>
    </actions>
</toast>"
                    : $@"
<toast duration=""short"">
    <visual>
        <binding template=""ToastGeneric"">
            <text>{safeTitle}</text>
            <text>{safeMessage}</text>
        </binding>
    </visual>
</toast>";

                var xmlDoc = new XmlDocument();
                xmlDoc.LoadXml(toastXml);

                var toast = new ToastNotification(xmlDoc);
                if (!string.IsNullOrEmpty(tag))
                {
                    toast.Tag = tag;
                    toast.Group = "KerkenezCalendarReminders";
                }

                // Keep reminder in Windows Action Center for up to 3 days or until dismissed
                toast.ExpirationTime = DateTimeOffset.Now.AddDays(3);

                toast.Activated += (s, e) =>
                {
                    try
                    {
                        onClick?.Invoke();
                    }
                    catch { }
                };

                var notifier = ToastNotificationManager.CreateToastNotifier(appId);
                notifier.Show(toast);
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CalendarNotificationService] Toast error: {ex.Message}");
                try
                {
                    fallbackAction?.Invoke();
                }
                catch { }
                return false;
            }
        }
    }
}
