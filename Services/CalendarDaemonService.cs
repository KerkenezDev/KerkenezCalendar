using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KerkenezCalendar.Models;

namespace KerkenezCalendar.Services
{
    /// <summary>
    /// Event-driven, high-performance background daemon service.
    /// Eliminates all disk polling loops: events are cached in-memory and reloaded only
    /// when signaled by IPC or FileSystemWatcher.
    /// Reminders are scheduled via precision one-shot timers with zero idle CPU or memory churn.
    /// </summary>
    public class CalendarDaemonService : IDisposable
    {
        private readonly CalendarConfigService _configService;
        private readonly CalendarEventService _eventService;
        private readonly HashSet<string> _notifiedReminderKeys = new HashSet<string>();
        private readonly object _stateLock = new object();

        private List<CalendarEvent> _cachedEvents = new List<CalendarEvent>();
        private CalendarSettings _cachedSettings = new CalendarSettings();
        private CalendarEvent? _nextReminder;
        private string _lastStatusText = "";

        private System.Threading.Timer? _precisionTimer;
        private System.Threading.Timer? _minuteHeartbeatTimer;
        private System.Threading.Timer? _periodicSyncTimer;
        private System.Threading.Timer? _fileWatcherDebounceTimer;
        private FileSystemWatcher? _fileWatcher;
        private bool _isDisposed;

        public event Action<CalendarEvent, string>? ReminderTriggered;
        public event Action<string>? StatusUpdated;
        public event Action<int, int, bool>? AccountsSynced;

        public CalendarDaemonService(CalendarConfigService? configService = null, CalendarEventService? eventService = null)
        {
            _configService = configService ?? new CalendarConfigService();
            _eventService = eventService ?? new CalendarEventService(_configService);
        }

        public void Start()
        {
            if (_isDisposed) return;

            // 1. Initial load from disk
            ReloadFromDisk(initialLoad: true);

            // 2. Setup FileSystemWatcher as a safety net for external file updates
            InitializeFileWatcher();

            // 3. Setup zero-allocation 60-second ticker for tooltip updates and minute cadence
            _minuteHeartbeatTimer = new System.Threading.Timer(OnMinuteHeartbeat, null, 60000, 60000);
        }

        public void Stop()
        {
            _precisionTimer?.Dispose();
            _precisionTimer = null;

            _minuteHeartbeatTimer?.Dispose();
            _minuteHeartbeatTimer = null;

            _periodicSyncTimer?.Dispose();
            _periodicSyncTimer = null;

            _fileWatcherDebounceTimer?.Dispose();
            _fileWatcherDebounceTimer = null;

            if (_fileWatcher != null)
            {
                _fileWatcher.EnableRaisingEvents = false;
                _fileWatcher.Dispose();
                _fileWatcher = null;
            }
        }

        /// <summary>
        /// Reloads data from disk into memory. Called ONLY when an actual file change occurs,
        /// when an IPC signal is received, or on manual sync. Never called in a polling loop.
        /// </summary>
        public void ReloadFromDisk(bool initialLoad = false)
        {
            if (_isDisposed) return;

            lock (_stateLock)
            {
                try
                {
                    _eventService.LoadEvents();
                    _configService.LoadConfig();

                    _cachedEvents = _eventService.GetAllEvents();
                    _cachedSettings = _configService.Settings;

                    CheckDueRemindersAndScheduleNext();
                    UpdatePeriodicSyncSchedule();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[CalendarDaemonService] Reload error: {ex.Message}");
                }
            }

            // Perform single trim after reloading to keep working set flat
            if (!initialLoad)
            {
                NativeMethods.TrimWorkingSet();
            }
        }

        private void UpdatePeriodicSyncSchedule()
        {
            if (_cachedSettings.EnablePeriodicSync && _cachedSettings.SyncIntervalMinutes > 0)
            {
                TimeSpan interval = TimeSpan.FromMinutes(_cachedSettings.SyncIntervalMinutes);
                if (_periodicSyncTimer == null)
                {
                    _periodicSyncTimer = new System.Threading.Timer(_ => PerformAccountSync(silent: true), null, interval, interval);
                }
                else
                {
                    _periodicSyncTimer.Change(interval, interval);
                }
            }
            else
            {
                _periodicSyncTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            }
        }

        /// <summary>
        /// Executes synchronization with configured email accounts on predetermined minutes.
        /// </summary>
        public void PerformAccountSync(bool silent = true)
        {
            if (_isDisposed) return;

            lock (_stateLock)
            {
                try
                {
                    _configService.LoadConfig();
                    _eventService.LoadEvents();

                    _cachedEvents = _eventService.GetAllEvents();
                    _cachedSettings = _configService.Settings;

                    var accounts = _configService.GetAccounts();
                    int activeAccounts = accounts.Count(a => a.IsEnabled);
                    int totalEvents = _cachedEvents.Count;

                    CheckDueRemindersAndScheduleNext();
                    AccountsSynced?.Invoke(activeAccounts, totalEvents, silent);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[CalendarDaemonService] Account sync error: {ex.Message}");
                }
            }

            NativeMethods.TrimWorkingSet();
        }

        private void InitializeFileWatcher()
        {
            try
            {
                string folder = CalendarConfigService.CalendarFolder;
                if (!Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                }

                _fileWatcher = new FileSystemWatcher(folder)
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
                    Filter = "*.*",
                    IncludeSubdirectories = false,
                    EnableRaisingEvents = true
                };

                FileSystemEventHandler onFileChanged = (s, e) =>
                {
                    string name = Path.GetFileName(e.FullPath);
                    if (name.Equals("events.dat", StringComparison.OrdinalIgnoreCase) ||
                        name.Equals("config.json", StringComparison.OrdinalIgnoreCase) ||
                        name.Equals("events.json", StringComparison.OrdinalIgnoreCase))
                    {
                        // Debounce by 250ms to allow multi-file writes to settle
                        _fileWatcherDebounceTimer?.Dispose();
                        _fileWatcherDebounceTimer = new System.Threading.Timer(_ =>
                        {
                            ReloadFromDisk(initialLoad: false);
                        }, null, 250, Timeout.Infinite);
                    }
                };

                _fileWatcher.Changed += onFileChanged;
                _fileWatcher.Created += onFileChanged;
                _fileWatcher.Renamed += (s, e) => onFileChanged(s, e);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CalendarDaemonService] FileWatcher error: {ex.Message}");
            }
        }

        /// <summary>
        /// Pure in-memory check: evaluates if any reminder is due and precision-schedules the next timer.
        /// Zero disk I/O, zero DPAPI calls, zero JSON parsing.
        /// </summary>
        private void CheckDueRemindersAndScheduleNext()
        {
            DateTime now = DateTime.Now;

            if (!_cachedSettings.EnableTrayNotifications)
            {
                UpdateStatus("Reminders paused (Notifications disabled)");
                _precisionTimer?.Change(Timeout.Infinite, Timeout.Infinite);
                return;
            }

            CalendarEvent? upcomingNext = null;
            DateTime? earliestUpcomingTime = null;

            foreach (var ev in _cachedEvents)
            {
                if (ev.IsCompleted || ev.ReminderMinutesBefore < 0) continue;

                DateTime? reminderTime = null;
                DateTime? eventStart = null;
                CalendarEvent eventCandidate = ev;

                if (!ev.IsRecurring)
                {
                    reminderTime = ev.EffectiveReminderTime;
                    eventStart = ev.StartDate;
                }
                else
                {
                    DateTime? occ = RecurrenceHelper.GetNextOccurrence(ev, now.AddMinutes(-30));
                    if (occ.HasValue)
                    {
                        reminderTime = occ.Value.AddMinutes(-ev.ReminderMinutesBefore);
                        eventStart = occ.Value;
                        eventCandidate = ev.CloneOccurrence(occ.Value.Date);
                    }
                }

                if (!reminderTime.HasValue || !eventStart.HasValue) continue;

                // 1. Is this reminder due right now? (Within trigger window, not older than 30 mins)
                if (now >= reminderTime.Value.AddSeconds(-30) && now <= reminderTime.Value.AddMinutes(30))
                {
                    string reminderKey = $"{ev.Id}_{reminderTime.Value.Ticks}";
                    if (!_notifiedReminderKeys.Contains(reminderKey))
                    {
                        _notifiedReminderKeys.Add(reminderKey);

                        bool use24 = _cachedSettings.TimeFormat24Hour;
                        string timeDesc = ev.IsAllDay ? "All Day" : eventStart.Value.ToString(use24 ? "HH:mm" : "hh:mm tt");
                        string message = ev.ReminderMinutesBefore == 0
                            ? $"{ev.Title} is starting now ({timeDesc})"
                            : $"{ev.Title} starts in {ev.ReminderMinutesBefore} minutes ({timeDesc})";

                        if (_cachedSettings.PlaySoundOnReminder)
                        {
                            try { System.Media.SystemSounds.Asterisk.Play(); } catch { }
                        }

                        ReminderTriggered?.Invoke(eventCandidate, message);

                        if (ev.IsRecurring)
                        {
                            ev.NextOccurrence = RecurrenceHelper.GetNextOccurrence(ev, now.AddSeconds(1));
                        }
                    }
                }

                // 2. Track next upcoming reminder strictly in the future
                DateTime? futureReminderTime = reminderTime;
                if (ev.IsRecurring && reminderTime.Value <= now)
                {
                    DateTime? nextFutureOcc = RecurrenceHelper.GetNextOccurrence(ev, now);
                    if (nextFutureOcc.HasValue)
                    {
                        futureReminderTime = nextFutureOcc.Value.AddMinutes(-ev.ReminderMinutesBefore);
                        eventCandidate = ev.CloneOccurrence(nextFutureOcc.Value.Date);
                    }
                }

                if (futureReminderTime.HasValue && futureReminderTime.Value > now)
                {
                    if (!earliestUpcomingTime.HasValue || futureReminderTime.Value < earliestUpcomingTime.Value)
                    {
                        earliestUpcomingTime = futureReminderTime.Value;
                        upcomingNext = eventCandidate;
                    }
                }
            }

            _nextReminder = upcomingNext;

            // Trim historical notification keys if oversized
            if (_notifiedReminderKeys.Count > 500)
            {
                _notifiedReminderKeys.Clear();
            }

            // Update tooltip text
            UpdateTooltipStatus(now);

            // Precision one-shot timer scheduling
            if (earliestUpcomingTime.HasValue)
            {
                TimeSpan delay = earliestUpcomingTime.Value - now;
                if (delay < TimeSpan.FromSeconds(1)) delay = TimeSpan.FromSeconds(1);

                // Cap delay at 24 hours to prevent 32-bit millisecond overflow
                if (delay.TotalHours > 24) delay = TimeSpan.FromHours(24);

                if (_precisionTimer == null)
                {
                    _precisionTimer = new System.Threading.Timer(OnPrecisionTimerFired, null, delay, Timeout.InfiniteTimeSpan);
                }
                else
                {
                    _precisionTimer.Change(delay, Timeout.InfiniteTimeSpan);
                }
            }
            else
            {
                _precisionTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            }
        }

        private void OnPrecisionTimerFired(object? state)
        {
            if (_isDisposed) return;

            lock (_stateLock)
            {
                CheckDueRemindersAndScheduleNext();
            }
        }

        private void OnMinuteHeartbeat(object? state)
        {
            if (_isDisposed) return;

            lock (_stateLock)
            {
                // Lightweight in-memory check without touching disk
                CheckDueRemindersAndScheduleNext();
            }
        }

        private void UpdateTooltipStatus(DateTime now)
        {
            if (_nextReminder != null && _nextReminder.EffectiveReminderTime.HasValue)
            {
                var diff = _nextReminder.EffectiveReminderTime.Value - now;
                string diffStr;
                if (diff.TotalMinutes < 1)
                {
                    diffStr = "in <1m";
                }
                else if (diff.TotalMinutes < 60)
                {
                    diffStr = $"in {Math.Max(1, (int)diff.TotalMinutes)}m";
                }
                else
                {
                    diffStr = $"in {Math.Max(1, (int)diff.TotalHours)}h";
                }

                UpdateStatus($"Next: {_nextReminder.Title} ({diffStr})");
            }
            else
            {
                UpdateStatus("No upcoming reminders");
            }
        }

        private void UpdateStatus(string status)
        {
            if (_lastStatusText != status)
            {
                _lastStatusText = status;
                StatusUpdated?.Invoke(status);
            }
        }

        /// <summary>
        /// Handles system time changes (e.g. user changes clock or DST shift) via Win32 WM_TIMECHANGE.
        /// </summary>
        public void OnSystemTimeChanged()
        {
            lock (_stateLock)
            {
                CheckDueRemindersAndScheduleNext();
            }
        }

        /// <summary>
        /// Handles system resume from sleep/hibernation via Win32 WM_POWERBROADCAST.
        /// </summary>
        public void OnPowerResumed()
        {
            lock (_stateLock)
            {
                CheckDueRemindersAndScheduleNext();
            }
            NativeMethods.TrimWorkingSet();
        }

        public CalendarEvent? GetNextReminder()
        {
            lock (_stateLock)
            {
                return _nextReminder;
            }
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            Stop();
        }
    }
}
