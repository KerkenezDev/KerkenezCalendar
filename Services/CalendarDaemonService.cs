using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KerkenezCalendar.Models;

namespace KerkenezCalendar.Services
{
    public class CalendarDaemonService : IDisposable
    {
        private readonly CalendarConfigService _configService;
        private readonly CalendarEventService _eventService;
        private readonly HashSet<string> _notifiedReminderKeys = new HashSet<string>();
        private readonly SemaphoreSlim _checkLock = new SemaphoreSlim(1, 1);
        private System.Threading.Timer? _pollTimer;
        private bool _isDisposed;

        public event Action<CalendarEvent, string>? ReminderTriggered;
        public event Action<string>? StatusUpdated;

        public CalendarDaemonService(CalendarConfigService? configService = null, CalendarEventService? eventService = null)
        {
            _configService = configService ?? new CalendarConfigService();
            _eventService = eventService ?? new CalendarEventService(_configService);
        }

        public void Start()
        {
            if (_isDisposed) return;
            ScheduleNextCheck(TimeSpan.FromSeconds(2));
        }

        public void Stop()
        {
            _pollTimer?.Dispose();
            _pollTimer = null;
        }

        private void ScheduleNextCheck(TimeSpan delay)
        {
            if (_isDisposed) return;

            _pollTimer?.Dispose();
            _pollTimer = new System.Threading.Timer(async _ =>
            {
                await CheckRemindersAsync();
                ScheduleNextCheck(TimeSpan.FromSeconds(30)); // 30-second reminder cadence
            }, null, (int)delay.TotalMilliseconds, Timeout.Infinite);
        }

        public async Task CheckRemindersAsync()
        {
            if (!await _checkLock.WaitAsync(0)) return;

            try
            {
                // Reload events to catch any external updates or changes from Main UI
                _eventService.LoadEvents();
                _configService.LoadConfig();

                if (!_configService.Settings.EnableTrayNotifications)
                {
                    StatusUpdated?.Invoke("Reminders paused (Notifications disabled)");
                    return;
                }

                DateTime now = DateTime.Now;
                var allEvents = _eventService.GetAllEvents();

                foreach (var ev in allEvents)
                {
                    if (ev.IsCompleted || ev.ReminderMinutesBefore < 0) continue;

                    DateTime? reminderTime = ev.EffectiveReminderTime;
                    if (!reminderTime.HasValue) continue;

                    // Check if reminder is due right now (within a 2-minute trigger window, not expired past 30 mins)
                    if (now >= reminderTime.Value.AddSeconds(-30) && now <= reminderTime.Value.AddMinutes(30))
                    {
                        string reminderKey = $"{ev.Id}_{reminderTime.Value.Ticks}";
                        if (!_notifiedReminderKeys.Contains(reminderKey))
                        {
                            _notifiedReminderKeys.Add(reminderKey);

                            bool use24 = _configService.Settings.TimeFormat24Hour;
                            string timeDesc = ev.IsAllDay ? "All Day" : ev.StartDate.ToString(use24 ? "HH:mm" : "hh:mm tt");
                            string message = ev.ReminderMinutesBefore == 0
                                ? $"{ev.Title} is starting now ({timeDesc})"
                                : $"{ev.Title} starts in {ev.ReminderMinutesBefore} minutes ({timeDesc})";

                            if (_configService.Settings.PlaySoundOnReminder)
                            {
                                try { System.Media.SystemSounds.Asterisk.Play(); } catch { }
                            }

                            ReminderTriggered?.Invoke(ev, message);
                        }
                    }
                }

                // Trim historical keys to prevent memory leak
                if (_notifiedReminderKeys.Count > 500)
                {
                    _notifiedReminderKeys.Clear();
                }

                // Status update for tooltip
                var next = _eventService.GetNextUpcomingReminder(now);
                if (next != null && next.EffectiveReminderTime.HasValue)
                {
                    var diff = next.EffectiveReminderTime.Value - now;
                    string diffStr = diff.TotalMinutes < 60
                        ? $"in {Math.Max(1, (int)diff.TotalMinutes)}m"
                        : $"in {Math.Max(1, (int)diff.TotalHours)}h";
                    StatusUpdated?.Invoke($"Next: {next.Title} ({diffStr})");
                }
                else
                {
                    StatusUpdated?.Invoke("No upcoming reminders");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CalendarDaemonService] Check error: {ex.Message}");
            }
            finally
            {
                _checkLock.Release();
            }
        }

        public CalendarEvent? GetNextReminder()
        {
            return _eventService.GetNextUpcomingReminder(DateTime.Now);
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            Stop();
            _checkLock.Dispose();
        }
    }
}
