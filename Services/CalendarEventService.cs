using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using KerkenezCalendar.Models;

namespace KerkenezCalendar.Services
{
    public class CalendarEventService
    {
        private readonly CalendarConfigService _configService;
        private readonly List<CalendarEvent> _events = new List<CalendarEvent>();
        private readonly object _lock = new object();

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        public event Action? EventsChanged;

        public CalendarEventService(CalendarConfigService configService)
        {
            _configService = configService;
            LoadEvents();
        }

        public void LoadEvents()
        {
            lock (_lock)
            {
                _events.Clear();
                try
                {
                    string filePath = CalendarConfigService.EventsFilePath;
                    if (File.Exists(filePath))
                    {
                        var loaded = EventCryptoService.LoadFromEncryptedFile(filePath);
                        if (loaded != null && loaded.Count > 0)
                        {
                            bool modified = false;
                            foreach (var ev in loaded)
                            {
                                if (ev.Description != null && ev.Description.Contains("sub-megabyte"))
                                {
                                    ev.Description = ev.Description.Replace("with sub-megabyte footprint.", "with low memory footprint.")
                                                                   .Replace("sub-megabyte", "low memory");
                                    modified = true;
                                }
                            }
                            _events.AddRange(loaded);
                            if (modified) SaveEventsInternal();
                        }
                    }
                    else if (File.Exists(CalendarConfigService.LegacyEventsJsonPath))
                    {
                        // Backward compatibility fallback: read JSON and migrate to DPAPI .dat
                        string json = File.ReadAllText(CalendarConfigService.LegacyEventsJsonPath);
                        var loaded = JsonSerializer.Deserialize<List<CalendarEvent>>(json, JsonOptions);
                        if (loaded != null && loaded.Count > 0)
                        {
                            _events.AddRange(loaded);
                            SaveEventsInternal();
                            try { File.Delete(CalendarConfigService.LegacyEventsJsonPath); } catch { }
                        }
                    }
                    else
                    {
                        // Seed initial demo events so the user has immediate visual feedback
                        SeedDemoEvents();
                        SaveEventsInternal();
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[CalendarEventService] Load error: {ex.Message}");
                }
            }
            EventsChanged?.Invoke();
        }

        private void SeedDemoEvents()
        {
            DateTime today = DateTime.Today;

            _events.Add(new CalendarEvent
            {
                Title = "Welcome to Kerkenez Calendar",
                Description = "Lightweight Win32 desktop calendar in the family of KerkenezDev utility programs. Features independent background system tray daemon.",
                Location = "Local Desktop",
                StartDate = today.AddHours(9).AddMinutes(30),
                EndDate = today.AddHours(10).AddMinutes(30),
                ReminderMinutesBefore = 15,
                Category = "Important",
                ColorTag = "#D83B01"
            });

            _events.Add(new CalendarEvent
            {
                Title = "Team Standup & Planning",
                Description = "Review upcoming development roadmap and project tasks.",
                Location = "Meeting Room A",
                StartDate = today.AddHours(14),
                EndDate = today.AddHours(15),
                ReminderMinutesBefore = 10,
                Category = "Work",
                ColorTag = "#0078D7"
            });

            _events.Add(new CalendarEvent
            {
                Title = "All-Day Project Review",
                Description = "Sprint retrospective and milestone demo.",
                Location = "Virtual",
                StartDate = today.AddDays(2),
                EndDate = today.AddDays(2),
                IsAllDay = true,
                ReminderMinutesBefore = 1440, // 1 day before
                Category = "Work",
                ColorTag = "#107C41"
            });
        }

        public bool SaveEvents()
        {
            bool result;
            lock (_lock)
            {
                result = SaveEventsInternal();
            }
            if (result)
            {
                EventsChanged?.Invoke();
            }
            return result;
        }

        private bool SaveEventsInternal()
        {
            string filePath = CalendarConfigService.EventsFilePath;
            return EventCryptoService.SaveToEncryptedFile(filePath, _events);
        }

        public List<CalendarEvent> GetAllEvents()
        {
            lock (_lock)
            {
                return _events.OrderBy(e => e.StartDate).ToList();
            }
        }

        public List<CalendarEvent> GetEventsForDate(DateTime date)
        {
            DateTime targetDate = date.Date;
            lock (_lock)
            {
                return _events
                    .Where(e => e.StartDate.Date == targetDate || (e.StartDate.Date <= targetDate && e.EndDate.Date >= targetDate))
                    .OrderBy(e => !e.IsAllDay) // All day first
                    .ThenBy(e => e.StartDate)
                    .ToList();
            }
        }

        public List<CalendarEvent> GetEventsForMonth(int year, int month)
        {
            DateTime firstDay = new DateTime(year, month, 1);
            DateTime lastDay = firstDay.AddMonths(1).AddDays(-1);

            lock (_lock)
            {
                return _events
                    .Where(e => (e.StartDate.Date >= firstDay && e.StartDate.Date <= lastDay) ||
                                (e.EndDate.Date >= firstDay && e.EndDate.Date <= lastDay) ||
                                (e.StartDate.Date <= firstDay && e.EndDate.Date >= lastDay))
                    .OrderBy(e => e.StartDate)
                    .ToList();
            }
        }

        public void AddEvent(CalendarEvent ev)
        {
            if (ev == null) return;
            lock (_lock)
            {
                _events.Add(ev);
                SaveEventsInternal();
            }
            EventsChanged?.Invoke();
        }

        public void UpdateEvent(CalendarEvent ev)
        {
            if (ev == null) return;
            lock (_lock)
            {
                int index = _events.FindIndex(e => e.Id == ev.Id);
                if (index >= 0)
                {
                    _events[index] = ev;
                    SaveEventsInternal();
                }
            }
            EventsChanged?.Invoke();
        }

        public bool DeleteEvent(string id)
        {
            bool removed = false;
            lock (_lock)
            {
                int count = _events.RemoveAll(e => e.Id == id);
                if (count > 0)
                {
                    removed = true;
                    SaveEventsInternal();
                }
            }
            if (removed)
            {
                EventsChanged?.Invoke();
            }
            return removed;
        }

        public CalendarEvent? GetNextUpcomingReminder(DateTime referenceTime)
        {
            lock (_lock)
            {
                return _events
                    .Where(e => e.EffectiveReminderTime.HasValue && e.EffectiveReminderTime.Value > referenceTime && !e.IsCompleted)
                    .OrderBy(e => e.EffectiveReminderTime!.Value)
                    .FirstOrDefault();
            }
        }

        public string ExportToIcs()
        {
            var sb = new StringBuilder();
            sb.AppendLine("BEGIN:VCALENDAR");
            sb.AppendLine("VERSION:2.0");
            sb.AppendLine("PRODID:-//Kerkenez//Kerkenez Calendar 1.0//EN");
            sb.AppendLine("CALSCALE:GREGORIAN");
            sb.AppendLine("METHOD:PUBLISH");

            lock (_lock)
            {
                foreach (var ev in _events)
                {
                    sb.AppendLine("BEGIN:VEVENT");
                    sb.AppendLine($"UID:{ev.Id}@kerkenez.local");
                    sb.AppendLine($"DTSTAMP:{DateTime.UtcNow:yyyyMMdd\\THHmmss\\Z}");

                    if (ev.IsAllDay)
                    {
                        sb.AppendLine($"DTSTART;VALUE=DATE:{ev.StartDate:yyyyMMdd}");
                        sb.AppendLine($"DTEND;VALUE=DATE:{ev.EndDate.AddDays(1):yyyyMMdd}");
                    }
                    else
                    {
                        sb.AppendLine($"DTSTART:{ev.StartDate:yyyyMMdd\\THHmmss}");
                        sb.AppendLine($"DTEND:{ev.EndDate:yyyyMMdd\\THHmmss}");
                    }

                    sb.AppendLine($"SUMMARY:{EscapeIcs(ev.Title)}");
                    if (!string.IsNullOrWhiteSpace(ev.Description))
                    {
                        sb.AppendLine($"DESCRIPTION:{EscapeIcs(ev.Description)}");
                    }
                    if (!string.IsNullOrWhiteSpace(ev.Location))
                    {
                        sb.AppendLine($"LOCATION:{EscapeIcs(ev.Location)}");
                    }
                    if (ev.ReminderMinutesBefore >= 0)
                    {
                        sb.AppendLine("BEGIN:VALARM");
                        sb.AppendLine("ACTION:DISPLAY");
                        sb.AppendLine($"DESCRIPTION:Reminder for {EscapeIcs(ev.Title)}");
                        sb.AppendLine($"-TRIGGER:PT{ev.ReminderMinutesBefore}M");
                        sb.AppendLine("END:VALARM");
                    }

                    sb.AppendLine("END:VEVENT");
                }
            }

            sb.AppendLine("END:VCALENDAR");
            return sb.ToString();
        }

        private static string EscapeIcs(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return text.Replace("\\", "\\\\").Replace(";", "\\;").Replace(",", "\\,").Replace("\r\n", "\\n").Replace("\n", "\\n");
        }
    }
}
