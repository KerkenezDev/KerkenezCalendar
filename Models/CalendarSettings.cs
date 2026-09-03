using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace KerkenezCalendar.Models
{
    public class CalendarSettings
    {
        public List<string> AccountIds { get; set; } = new List<string>();

        // UI & Layout Options (identical to EmailSummarizer config.json)
        public bool CollapseSidebarByDefault { get; set; } = false;
        public double WindowWidthScale { get; set; } = 0.60;
        public double WindowHeightScale { get; set; } = 0.56;
        public int WindowWidth { get; set; } = 0;
        public int WindowHeight { get; set; } = 0;

        // System Tray Daemon & Notification Options (identical to EmailSummarizer config.json)
        public bool AlwaysKeepOn { get; set; } = true;

        [JsonIgnore]
        public bool AlwaysKeepDaemonRunning
        {
            get => AlwaysKeepOn;
            set => AlwaysKeepOn = value;
        }

        public bool EnableTrayNotifications { get; set; } = true;
        public int TrayRefreshIntervalMinutes { get; set; } = 5;
        public bool StartWithWindows { get; set; } = false;
        public bool PlaySoundOnReminder { get; set; } = true;

        // Calendar Specific Preferences
        public DayOfWeek StartOfWeek { get; set; } = DayOfWeek.Monday;
        public int DefaultReminderMinutes { get; set; } = 15;
        public bool TimeFormat24Hour { get; set; } = false;
        public bool ShowWeekend { get; set; } = true;
        public string DefaultCategory { get; set; } = "Work";

        public static CalendarSettings CreateDefault()
        {
            return new CalendarSettings();
        }
    }
}
