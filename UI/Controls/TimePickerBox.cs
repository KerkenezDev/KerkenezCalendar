using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace KerkenezCalendar.UI.Controls
{
    public class TimePickerBox : ComboBox
    {
        private TimeSpan _selectedTime = new TimeSpan(9, 0, 0);
        private bool _use24Hour = false;
        private bool _isFormatting = false;
        private bool _hasSelectedOnFocus = false;

        public event EventHandler? TimeChanged;

        public TimeSpan SelectedTime
        {
            get => _selectedTime;
            set
            {
                var normalized = NormalizeTime(value);
                if (_selectedTime != normalized)
                {
                    _selectedTime = normalized;
                    UpdateTextFromTime();
                    TimeChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public bool Use24HourFormat
        {
            get => _use24Hour;
            set
            {
                if (_use24Hour != value)
                {
                    _use24Hour = value;
                    PopulateIntervals();
                    UpdateTextFromTime();
                }
            }
        }

        public TimePickerBox(bool use24Hour = false, float scale = 1.0f)
        {
            _use24Hour = use24Hour;

            this.DropDownStyle = ComboBoxStyle.DropDown;
            this.Font = new Font("Segoe UI", 9.5F, FontStyle.Regular);
            this.Width = (int)((_use24Hour ? 90 : 120) * scale);
            this.MaxDropDownItems = 8;
            this.IntegralHeight = false;

            PopulateIntervals();
            UpdateTextFromTime();
        }

        public void PopulateIntervals()
        {
            this.Items.Clear();
            for (int h = 0; h < 24; h++)
            {
                for (int m = 0; m < 60; m += 30)
                {
                    var ts = new TimeSpan(h, m, 0);
                    this.Items.Add(FormatTime(ts, _use24Hour));
                }
            }
        }

        public static string FormatTime(TimeSpan time, bool use24Hour)
        {
            int h = Math.Clamp(time.Hours, 0, 23);
            int m = Math.Clamp(time.Minutes, 0, 59);
            if (use24Hour)
            {
                return $"{h:D2}:{m:D2}";
            }
            else
            {
                int displayHour = h % 12;
                if (displayHour == 0) displayHour = 12;
                string tt = h >= 12 ? "PM" : "AM";
                return $"{displayHour:D2}:{m:D2} {tt}";
            }
        }

        private static TimeSpan NormalizeTime(TimeSpan time)
        {
            if (time < TimeSpan.Zero) return TimeSpan.Zero;
            if (time >= TimeSpan.FromDays(1)) return new TimeSpan(23, 59, 0);
            return new TimeSpan(time.Hours, time.Minutes, 0);
        }

        private void UpdateTextFromTime()
        {
            _isFormatting = true;
            try
            {
                string formatted = FormatTime(_selectedTime, _use24Hour);
                this.Text = formatted;
                int idx = this.Items.IndexOf(formatted);
                if (idx >= 0 && this.SelectedIndex != idx)
                {
                    this.SelectedIndex = idx;
                }
            }
            finally
            {
                _isFormatting = false;
            }
        }

        protected override void OnSelectedIndexChanged(EventArgs e)
        {
            base.OnSelectedIndexChanged(e);
            if (_isFormatting) return;

            if (this.SelectedItem is string str && TryParseTime(str, _use24Hour, out var ts))
            {
                if (_selectedTime != ts)
                {
                    _selectedTime = ts;
                    TimeChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        protected override void OnEnter(EventArgs e)
        {
            base.OnEnter(e);
            _hasSelectedOnFocus = false;
            this.BeginInvoke(new Action(SelectAllText));
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (!_hasSelectedOnFocus)
            {
                SelectAllText();
                _hasSelectedOnFocus = true;
            }
        }

        private void SelectAllText()
        {
            this.SelectAll();
        }

        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            base.OnKeyPress(e);

            if (e.Handled) return;

            // Allow backspace, enter, tab, delete
            if (char.IsControl(e.KeyChar))
            {
                if (e.KeyChar == (char)Keys.Enter)
                {
                    CommitText();
                    e.Handled = true;
                }
                else if (e.KeyChar == '\b')
                {
                    // If user was auto-advanced to "0X:" and hits Backspace, clean out the auto-padded prefix
                    if (this.SelectionStart == 3 && this.Text.Length == 3 && this.Text.EndsWith(':'))
                    {
                        this.Text = "";
                        this.SelectionStart = 0;
                        e.Handled = true;
                        return;
                    }
                }
                return;
            }

            // If user typed a digit
            if (char.IsDigit(e.KeyChar))
            {
                int selStart = this.SelectionStart;
                int selLength = this.SelectionLength;
                string current = this.Text;
                int maxFirstHourDigit = _use24Hour ? 2 : 1;
                int digitVal = e.KeyChar - '0';

                // Case 1: Fresh start (entire text selected or empty box or cursor at start)
                if ((selLength == current.Length && selLength > 0) || current.Length == 0 || (selStart == 0 && selLength == 0))
                {
                    // If the typed digit CANNOT be the first digit of a 2-digit hour:
                    // (e.g., 3-9 in 24h mode, or 2-9 in 12h mode)
                    // automatically format as "0X:" and jump straight to the minute!
                    if (digitVal > maxFirstHourDigit)
                    {
                        this.Text = $"0{e.KeyChar}:";
                        this.SelectionStart = 3;
                        this.SelectionLength = 0;
                        e.Handled = true;
                        return;
                    }

                    this.Text = e.KeyChar.ToString();
                    this.SelectionStart = 1;
                    this.SelectionLength = 0;
                    e.Handled = true;
                    return;
                }

                // Case 2: User typed 1st digit of hour, now typing 2nd digit of hour
                if (selStart == 1 && (current.Length == 1 || (current.Length > 1 && !current.Contains(':'))))
                {
                    string firstChar = current.Substring(0, 1);
                    // In 24h mode: if first is '2' and 2nd is > 3 (e.g. 24-29), format as 02:X
                    if (_use24Hour && firstChar == "2" && digitVal > 3)
                    {
                        this.Text = $"02:{e.KeyChar}";
                        this.SelectionStart = 4;
                        this.SelectionLength = 0;
                        e.Handled = true;
                        return;
                    }
                    // In 12h mode: if first is '1' and 2nd is > 2 (e.g. 13-19), format as 01:X
                    if (!_use24Hour && firstChar == "1" && digitVal > 2)
                    {
                        this.Text = $"01:{e.KeyChar}";
                        this.SelectionStart = 4;
                        this.SelectionLength = 0;
                        e.Handled = true;
                        return;
                    }

                    string twoDigits = firstChar + e.KeyChar;
                    this.Text = twoDigits + ":";
                    this.SelectionStart = 3;
                    this.SelectionLength = 0;
                    e.Handled = true;
                    return;
                }

                // Case 3: User is at position 2 and typing colon is needed
                if (selStart == 2 && !current.Contains(':'))
                {
                    this.Text = current + ":" + e.KeyChar;
                    this.SelectionStart = 4;
                    this.SelectionLength = 0;
                    e.Handled = true;
                    return;
                }

                // Case 4: User is at position 3 after colon typing 1st minute digit (e.g. "09:" + "3" -> "09:3")
                if (selStart == 3 && current.Length >= 3 && current.Contains(':'))
                {
                    string prefix = current.Substring(0, 3);
                    this.Text = prefix + e.KeyChar;
                    this.SelectionStart = 4;
                    this.SelectionLength = 0;
                    e.Handled = true;
                    return;
                }

                // Case 5: User is typing the final minute digit (e.g. "09:3" + "0" -> "09:30")
                if (selStart == 4 && current.Length >= 4 && current.Contains(':'))
                {
                    string prefix = current.Substring(0, 4);
                    this.Text = prefix + e.KeyChar;
                    this.SelectionStart = 5;
                    this.SelectionLength = 0;
                    CommitText();
                    this.SelectAll();
                    e.Handled = true;
                    return;
                }
            }
            else if (e.KeyChar == ':' || e.KeyChar == ' ' || e.KeyChar == 'a' || e.KeyChar == 'A' || e.KeyChar == 'p' || e.KeyChar == 'P' || e.KeyChar == 'm' || e.KeyChar == 'M')
            {
                // Allow separators and AM/PM modifiers
                return;
            }
            else
            {
                // Disallow invalid characters
                e.Handled = true;
            }
        }

        protected override void OnLeave(EventArgs e)
        {
            base.OnLeave(e);
            CommitText();
        }

        public void CommitText()
        {
            string raw = this.Text.Trim();
            if (TryParseTime(raw, _use24Hour, out var parsed))
            {
                bool changed = (_selectedTime != parsed);
                _selectedTime = parsed;
                UpdateTextFromTime();
                if (changed)
                {
                    TimeChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            else
            {
                UpdateTextFromTime(); // Revert to last valid time
            }
        }

        public static bool TryParseTime(string input, bool use24Hour, out TimeSpan result)
        {
            result = TimeSpan.Zero;
            if (string.IsNullOrWhiteSpace(input)) return false;

            string s = input.Trim();

            // Direct DateTime.TryParse
            if (DateTime.TryParse(s, CultureInfo.CurrentCulture, DateTimeStyles.None, out var dt) ||
                DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
            {
                result = dt.TimeOfDay;
                return true;
            }

            // Check for PM / AM flags
            bool isPm = s.EndsWith("pm", StringComparison.OrdinalIgnoreCase) || s.EndsWith("p", StringComparison.OrdinalIgnoreCase);
            bool isAm = s.EndsWith("am", StringComparison.OrdinalIgnoreCase) || s.EndsWith("a", StringComparison.OrdinalIgnoreCase);
            s = s.TrimEnd('p', 'P', 'm', 'M', 'a', 'A', ' ').Trim();

            // Digits-only check: "9", "930", "14", "1430"
            if (int.TryParse(s, out int num))
            {
                int h = 0, m = 0;
                if (s.Length <= 2)
                {
                    h = num;
                    m = 0;
                }
                else if (s.Length == 3)
                {
                    h = num / 100;
                    m = num % 100;
                }
                else if (s.Length >= 4)
                {
                    h = num / 100;
                    m = num % 100;
                }

                if (isPm && h < 12) h += 12;
                if (isAm && h == 12) h = 0;

                if (h >= 0 && h < 24 && m >= 0 && m < 60)
                {
                    result = new TimeSpan(h, m, 0);
                    return true;
                }
            }

            // H:mm check
            string[] parts = s.Split(':');
            if (parts.Length == 2 && int.TryParse(parts[0], out int hour) && int.TryParse(parts[1], out int minute))
            {
                if (isPm && hour < 12) hour += 12;
                if (isAm && hour == 12) hour = 0;

                if (hour >= 0 && hour < 24 && minute >= 0 && minute < 60)
                {
                    result = new TimeSpan(hour, minute, 0);
                    return true;
                }
            }

            return false;
        }
    }
}
