using System;
using System.Text.Json.Serialization;

namespace KerkenezCalendar.Models
{
    public class CalendarEvent
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Title { get; set; } = "New Event";
        public string Description { get; set; } = "";
        public string Location { get; set; } = "";
        public DateTime StartDate { get; set; } = DateTime.Today.AddHours(9);
        public DateTime EndDate { get; set; } = DateTime.Today.AddHours(10);
        public bool IsAllDay { get; set; } = false;

        /// <summary>
        /// Reminder offset in minutes before event start.
        /// -1 = None, 0 = At time of event, 5 = 5m before, 15 = 15m before, 60 = 1h before, 1440 = 1 day before.
        /// </summary>
        public int ReminderMinutesBefore { get; set; } = 15;

        public string? AccountId { get; set; }
        public string Category { get; set; } = "Work";
        public string ColorTag { get; set; } = "#0078D7";
        public string Recurrence { get; set; } = "None";
        public bool IsCompleted { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [JsonIgnore]
        public DateTime? EffectiveReminderTime
        {
            get
            {
                if (ReminderMinutesBefore < 0) return null;
                return StartDate.AddMinutes(-ReminderMinutesBefore);
            }
        }

        public string GetTimeRangeDisplayText(bool use24Hour = false)
        {
            if (IsAllDay)
            {
                return "All Day";
            }
            return use24Hour
                ? $"{StartDate:HH:mm} - {EndDate:HH:mm}"
                : $"{StartDate:hh:mm tt} - {EndDate:hh:mm tt}";
        }

        public string GetReminderDisplayText()
        {
            return ReminderMinutesBefore switch
            {
                -1 => "None",
                0 => "At time of event",
                5 => "5 minutes before",
                10 => "10 minutes before",
                15 => "15 minutes before",
                30 => "30 minutes before",
                60 => "1 hour before",
                120 => "2 hours before",
                1440 => "1 day before",
                2880 => "2 days before",
                10080 => "1 week before",
                _ => $"{ReminderMinutesBefore} minutes before"
            };
        }

        public override string ToString()
        {
            return $"{Title} ({GetTimeRangeDisplayText()})";
        }
    }
}
