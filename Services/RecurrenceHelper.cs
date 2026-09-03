using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using KerkenezCalendar.Models;

namespace KerkenezCalendar.Services
{
    public class RecurrencePattern
    {
        public string Frequency { get; set; } = "None"; // "None", "Daily", "Weekly", "Monthly", "Yearly"
        public int Interval { get; set; } = 1;
        public List<DayOfWeek> DaysOfWeek { get; set; } = new();
        public int? DayOfMonth { get; set; }
        public int? WeekOfMonth { get; set; } // 1, 2, 3, 4, -1 (last)
        public DayOfWeek? SpecificWeekday { get; set; }
        public int? MonthOfYear { get; set; }
        public DateTime? UntilDate { get; set; }

        public bool IsActive => !string.IsNullOrEmpty(Frequency) && !Frequency.Equals("None", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// RFC 5545 compliant recurrence engine for generating, parsing, and projecting recurring calendar events.
    /// </summary>
    public static class RecurrenceHelper
    {
        private static readonly Dictionary<string, DayOfWeek> IcsDayMap = new(StringComparer.OrdinalIgnoreCase)
        {
            ["MO"] = DayOfWeek.Monday,
            ["TU"] = DayOfWeek.Tuesday,
            ["WE"] = DayOfWeek.Wednesday,
            ["TH"] = DayOfWeek.Thursday,
            ["FR"] = DayOfWeek.Friday,
            ["SA"] = DayOfWeek.Saturday,
            ["SU"] = DayOfWeek.Sunday
        };

        private static readonly Dictionary<DayOfWeek, string> DayIcsMap = new()
        {
            [DayOfWeek.Monday] = "MO",
            [DayOfWeek.Tuesday] = "TU",
            [DayOfWeek.Wednesday] = "WE",
            [DayOfWeek.Thursday] = "TH",
            [DayOfWeek.Friday] = "FR",
            [DayOfWeek.Saturday] = "SA",
            [DayOfWeek.Sunday] = "SU"
        };

        /// <summary>
        /// Serializes a recurrence pattern into an RFC 5545 standard RRULE string.
        /// </summary>
        public static string ToRRule(RecurrencePattern p)
        {
            if (!p.IsActive) return "";

            var parts = new List<string>
            {
                $"FREQ={p.Frequency.ToUpperInvariant()}"
            };

            if (p.Interval > 1)
            {
                parts.Add($"INTERVAL={p.Interval}");
            }

            if (p.Frequency.Equals("Weekly", StringComparison.OrdinalIgnoreCase) && p.DaysOfWeek.Count > 0)
            {
                var days = p.DaysOfWeek.Select(d => DayIcsMap[d]);
                parts.Add($"BYDAY={string.Join(",", days)}");
            }
            else if (p.Frequency.Equals("Monthly", StringComparison.OrdinalIgnoreCase))
            {
                if (p.WeekOfMonth.HasValue && p.SpecificWeekday.HasValue)
                {
                    string dayCode = DayIcsMap[p.SpecificWeekday.Value];
                    parts.Add($"BYSETPOS={p.WeekOfMonth.Value}");
                    parts.Add($"BYDAY={dayCode}");
                }
                else if (p.DayOfMonth.HasValue)
                {
                    parts.Add($"BYMONTHDAY={p.DayOfMonth.Value}");
                }
            }
            else if (p.Frequency.Equals("Yearly", StringComparison.OrdinalIgnoreCase))
            {
                if (p.MonthOfYear.HasValue)
                {
                    parts.Add($"BYMONTH={p.MonthOfYear.Value}");
                }

                if (p.WeekOfMonth.HasValue && p.SpecificWeekday.HasValue)
                {
                    string dayCode = DayIcsMap[p.SpecificWeekday.Value];
                    parts.Add($"BYSETPOS={p.WeekOfMonth.Value}");
                    parts.Add($"BYDAY={dayCode}");
                }
                else if (p.DayOfMonth.HasValue)
                {
                    parts.Add($"BYMONTHDAY={p.DayOfMonth.Value}");
                }
            }

            if (p.UntilDate.HasValue)
            {
                parts.Add($"UNTIL={p.UntilDate.Value.ToUniversalTime():yyyyMMdd\\THHmmss\\Z}");
            }

            return string.Join(";", parts);
        }

        /// <summary>
        /// Parses an RFC 5545 RRULE string into a structured RecurrencePattern.
        /// </summary>
        public static RecurrencePattern ParseRRule(string? rrule, DateTime? startDate = null)
        {
            var p = new RecurrencePattern();
            if (string.IsNullOrWhiteSpace(rrule)) return p;

            string clean = rrule.StartsWith("RRULE:", StringComparison.OrdinalIgnoreCase)
                ? rrule.Substring(6)
                : rrule;

            var tokens = clean.Split(';', StringSplitOptions.RemoveEmptyEntries);
            foreach (var token in tokens)
            {
                var kvp = token.Split('=', 2);
                if (kvp.Length != 2) continue;

                string key = kvp[0].Trim().ToUpperInvariant();
                string val = kvp[1].Trim();

                switch (key)
                {
                    case "FREQ":
                        p.Frequency = val.ToUpperInvariant() switch
                        {
                            "DAILY" => "Daily",
                            "WEEKLY" => "Weekly",
                            "MONTHLY" => "Monthly",
                            "YEARLY" => "Yearly",
                            _ => "None"
                        };
                        break;

                    case "INTERVAL":
                        if (int.TryParse(val, out int interval) && interval >= 1)
                        {
                            p.Interval = interval;
                        }
                        break;

                    case "BYDAY":
                        // Can be "MO,WE,FR" or with set position "2TU" or "-1FR"
                        var dayParts = val.Split(',', StringSplitOptions.RemoveEmptyEntries);
                        foreach (var dp in dayParts)
                        {
                            string dayStr = dp.Trim();
                            // Check if prefix has number e.g. "2TU" or "-1SU"
                            int numLen = 0;
                            while (numLen < dayStr.Length && (char.IsDigit(dayStr[numLen]) || dayStr[numLen] == '-'))
                            {
                                numLen++;
                            }

                            if (numLen > 0 && int.TryParse(dayStr.Substring(0, numLen), out int setPos))
                            {
                                p.WeekOfMonth = setPos;
                                string suffix = dayStr.Substring(numLen).ToUpperInvariant();
                                if (IcsDayMap.TryGetValue(suffix, out var dw))
                                {
                                    p.SpecificWeekday = dw;
                                    p.DaysOfWeek.Add(dw);
                                }
                            }
                            else if (IcsDayMap.TryGetValue(dayStr.ToUpperInvariant(), out var dw))
                            {
                                p.DaysOfWeek.Add(dw);
                                p.SpecificWeekday = dw;
                            }
                        }
                        break;

                    case "BYSETPOS":
                        if (int.TryParse(val, out int sp))
                        {
                            p.WeekOfMonth = sp;
                        }
                        break;

                    case "BYMONTHDAY":
                        if (int.TryParse(val, out int mday) && mday >= 1 && mday <= 31)
                        {
                            p.DayOfMonth = mday;
                        }
                        break;

                    case "BYMONTH":
                        if (int.TryParse(val, out int month) && month >= 1 && month <= 12)
                        {
                            p.MonthOfYear = month;
                        }
                        break;

                    case "UNTIL":
                        if (DateTime.TryParseExact(val, new[] { "yyyyMMdd\\THHmmss\\Z", "yyyyMMdd\\THHmmss", "yyyyMMdd" },
                            System.Globalization.CultureInfo.InvariantCulture,
                            System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
                            out DateTime dt))
                        {
                            p.UntilDate = dt.ToLocalTime();
                        }
                        break;
                }
            }

            // Default fallbacks from StartDate if fields are omitted
            if (startDate.HasValue)
            {
                if (p.Frequency.Equals("Weekly", StringComparison.OrdinalIgnoreCase) && p.DaysOfWeek.Count == 0)
                {
                    p.DaysOfWeek.Add(startDate.Value.DayOfWeek);
                }
                else if (p.Frequency.Equals("Monthly", StringComparison.OrdinalIgnoreCase) && !p.DayOfMonth.HasValue && !p.WeekOfMonth.HasValue)
                {
                    p.DayOfMonth = startDate.Value.Day;
                }
                else if (p.Frequency.Equals("Yearly", StringComparison.OrdinalIgnoreCase))
                {
                    p.MonthOfYear ??= startDate.Value.Month;
                    if (!p.DayOfMonth.HasValue && !p.WeekOfMonth.HasValue)
                    {
                        p.DayOfMonth = startDate.Value.Day;
                    }
                }
            }

            return p;
        }

        /// <summary>
        /// Calculates the date of the Nth weekday of a specific month.
        /// e.g. n = 1 -> 1st Tuesday, n = 2 -> 2nd Tuesday, n = -1 -> Last Tuesday.
        /// </summary>
        public static DateTime GetNthWeekdayOfMonth(int year, int month, DayOfWeek dayOfWeek, int n)
        {
            if (n >= 1 && n <= 5)
            {
                var firstOfMonth = new DateTime(year, month, 1);
                int offset = ((int)dayOfWeek - (int)firstOfMonth.DayOfWeek + 7) % 7;
                var target = firstOfMonth.AddDays(offset + (n - 1) * 7);
                if (target.Month == month)
                {
                    return target;
                }
            }

            if (n == -1) // Last occurrence in month
            {
                int daysInMonth = DateTime.DaysInMonth(year, month);
                var lastOfMonth = new DateTime(year, month, daysInMonth);
                int offset = ((int)lastOfMonth.DayOfWeek - (int)dayOfWeek + 7) % 7;
                return lastOfMonth.AddDays(-offset);
            }

            return new DateTime(year, month, 1);
        }

        /// <summary>
        /// Evaluates whether a recurring event occurs on a specific calendar date.
        /// </summary>
        public static bool IsOccurringOnDate(CalendarEvent ev, DateTime targetDate)
        {
            if (!ev.IsRecurring)
            {
                return ev.StartDate.Date == targetDate.Date;
            }

            DateTime target = targetDate.Date;
            DateTime start = ev.StartDate.Date;

            if (target < start) return false;
            if (ev.RecurrenceEnd.HasValue && target > ev.RecurrenceEnd.Value.Date) return false;

            var pattern = ParseRRule(ev.RecurrenceRule, ev.StartDate);
            if (!pattern.IsActive) return target == start;

            int interval = Math.Max(1, pattern.Interval);

            switch (pattern.Frequency)
            {
                case "Daily":
                    int dayDiff = (target - start).Days;
                    return (dayDiff % interval == 0);

                case "Weekly":
                    if (pattern.DaysOfWeek.Count == 0)
                    {
                        pattern.DaysOfWeek.Add(start.DayOfWeek);
                    }

                    if (!pattern.DaysOfWeek.Contains(target.DayOfWeek))
                    {
                        return false;
                    }

                    if (interval > 1)
                    {
                        // Calculate weeks elapsed relative to the start week
                        int weekStartOffset = ((int)start.DayOfWeek + 6) % 7;
                        DateTime startMonday = start.AddDays(-weekStartOffset);
                        int targetOffset = ((int)target.DayOfWeek + 6) % 7;
                        DateTime targetMonday = target.AddDays(-targetOffset);

                        int weeksElapsed = (int)Math.Round((targetMonday - startMonday).TotalDays / 7.0);
                        if (weeksElapsed < 0 || (weeksElapsed % interval != 0))
                        {
                            return false;
                        }
                    }
                    return true;

                case "Monthly":
                    int monthDiff = ((target.Year - start.Year) * 12) + (target.Month - start.Month);
                    if (monthDiff < 0 || (monthDiff % interval != 0)) return false;

                    if (pattern.WeekOfMonth.HasValue && pattern.SpecificWeekday.HasValue)
                    {
                        DateTime expectedDate = GetNthWeekdayOfMonth(target.Year, target.Month, pattern.SpecificWeekday.Value, pattern.WeekOfMonth.Value);
                        return (target == expectedDate.Date);
                    }

                    int targetDay = pattern.DayOfMonth ?? start.Day;
                    int daysInTargetMonth = DateTime.DaysInMonth(target.Year, target.Month);
                    int actualDay = Math.Min(targetDay, daysInTargetMonth);
                    return (target.Day == actualDay);

                case "Yearly":
                    int yearDiff = target.Year - start.Year;
                    if (yearDiff < 0 || (yearDiff % interval != 0)) return false;

                    int targetMonth = pattern.MonthOfYear ?? start.Month;
                    if (target.Month != targetMonth) return false;

                    if (pattern.WeekOfMonth.HasValue && pattern.SpecificWeekday.HasValue)
                    {
                        DateTime expectedDate = GetNthWeekdayOfMonth(target.Year, target.Month, pattern.SpecificWeekday.Value, pattern.WeekOfMonth.Value);
                        return (target == expectedDate.Date);
                    }

                    int tDay = pattern.DayOfMonth ?? start.Day;
                    int daysInM = DateTime.DaysInMonth(target.Year, target.Month);
                    int actDay = Math.Min(tDay, daysInM);
                    return (target.Day == actDay);

                default:
                    return target == start;
            }
        }

        /// <summary>
        /// Calculates the next upcoming occurrence strictly on or after referenceTime.
        /// Returns null if recurrence has expired.
        /// </summary>
        public static DateTime? GetNextOccurrence(CalendarEvent ev, DateTime referenceTime)
        {
            if (!ev.IsRecurring)
            {
                return (ev.StartDate >= referenceTime) ? ev.StartDate : null;
            }

            DateTime checkDate = (referenceTime.Date < ev.StartDate.Date) ? ev.StartDate.Date : referenceTime.Date;
            TimeSpan timeOfDay = ev.StartDate.TimeOfDay;

            // Search up to 5 years or until RecurrenceEnd
            DateTime limitDate = ev.RecurrenceEnd.HasValue
                ? ev.RecurrenceEnd.Value.Date
                : DateTime.Today.AddYears(5);

            DateTime current = checkDate;
            while (current <= limitDate)
            {
                if (IsOccurringOnDate(ev, current))
                {
                    DateTime occurrenceTimestamp = current.Add(timeOfDay);
                    if (occurrenceTimestamp >= referenceTime)
                    {
                        return occurrenceTimestamp;
                    }
                }
                current = current.AddDays(1);
            }

            return null;
        }

        /// <summary>
        /// Generates all occurrence dates falling within [rangeStart, rangeEnd].
        /// </summary>
        public static List<DateTime> GetOccurrencesInRange(CalendarEvent ev, DateTime rangeStart, DateTime rangeEnd)
        {
            var results = new List<DateTime>();
            if (!ev.IsRecurring)
            {
                if (ev.StartDate.Date >= rangeStart.Date && ev.StartDate.Date <= rangeEnd.Date)
                {
                    results.Add(ev.StartDate.Date);
                }
                return results;
            }

            DateTime start = (rangeStart.Date < ev.StartDate.Date) ? ev.StartDate.Date : rangeStart.Date;
            DateTime end = rangeEnd.Date;
            if (ev.RecurrenceEnd.HasValue && end > ev.RecurrenceEnd.Value.Date)
            {
                end = ev.RecurrenceEnd.Value.Date;
            }

            for (DateTime d = start; d <= end; d = d.AddDays(1))
            {
                if (IsOccurringOnDate(ev, d))
                {
                    results.Add(d);
                }
            }

            return results;
        }

        /// <summary>
        /// Updates the cached NextOccurrence property on an event relative to now.
        /// </summary>
        public static void UpdateNextOccurrence(CalendarEvent ev, DateTime now)
        {
            if (!ev.IsRecurring)
            {
                ev.NextOccurrence = null;
                return;
            }

            ev.NextOccurrence = GetNextOccurrence(ev, now);
        }

        /// <summary>
        /// Formats a human-readable English description of a recurrence pattern.
        /// </summary>
        public static string GetHumanReadableDescription(RecurrencePattern p, DateTime startDate)
        {
            if (!p.IsActive) return "Does not repeat";

            var sb = new StringBuilder("Repeats ");
            string intervalText = (p.Interval == 1) ? "" : $"every {p.Interval} ";

            switch (p.Frequency)
            {
                case "Daily":
                    sb.Append(p.Interval == 1 ? "daily" : $"{intervalText}days");
                    break;

                case "Weekly":
                    sb.Append(p.Interval == 1 ? "weekly on " : $"{intervalText}weeks on ");
                    var days = p.DaysOfWeek.Count > 0 ? p.DaysOfWeek : new List<DayOfWeek> { startDate.DayOfWeek };
                    sb.Append(string.Join(", ", days.Select(d => d.ToString())));
                    break;

                case "Monthly":
                    sb.Append(p.Interval == 1 ? "monthly " : $"{intervalText}months ");
                    if (p.WeekOfMonth.HasValue && p.SpecificWeekday.HasValue)
                    {
                        string nth = p.WeekOfMonth.Value switch
                        {
                            1 => "1st",
                            2 => "2nd",
                            3 => "3rd",
                            4 => "4th",
                            -1 => "last",
                            _ => $"{p.WeekOfMonth.Value}th"
                        };
                        sb.Append($"on the {nth} {p.SpecificWeekday.Value}");
                    }
                    else
                    {
                        int day = p.DayOfMonth ?? startDate.Day;
                        sb.Append($"on day {day}");
                    }
                    break;

                case "Yearly":
                    sb.Append(p.Interval == 1 ? "yearly " : $"{intervalText}years ");
                    int month = p.MonthOfYear ?? startDate.Month;
                    string monthName = new DateTime(2000, month, 1).ToString("MMMM");
                    if (p.WeekOfMonth.HasValue && p.SpecificWeekday.HasValue)
                    {
                        string nth = p.WeekOfMonth.Value switch
                        {
                            1 => "1st",
                            2 => "2nd",
                            3 => "3rd",
                            4 => "4th",
                            -1 => "last",
                            _ => $"{p.WeekOfMonth.Value}th"
                        };
                        sb.Append($"on the {nth} {p.SpecificWeekday.Value} of {monthName}");
                    }
                    else
                    {
                        int day = p.DayOfMonth ?? startDate.Day;
                        sb.Append($"on {monthName} {day}");
                    }
                    break;
            }

            if (p.UntilDate.HasValue)
            {
                sb.Append($" until {p.UntilDate.Value:MMM dd, yyyy}");
            }

            return sb.ToString();
        }
    }
}
