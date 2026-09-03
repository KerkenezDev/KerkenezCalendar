using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using KerkenezCalendar.Models;
using KerkenezCalendar.Services;
using KerkenezCalendar.UI.Controls;

namespace KerkenezCalendar.UI.Dialogs
{
    public class EventEditDialog : Form
    {
        private readonly CalendarConfigService _configService;
        private readonly CalendarEvent _event;
        private readonly bool _isEditMode;

        private TextBox _txtTitle = null!;
        private DateTimePicker _dtpDate = null!;
        private CheckBox _chkAllDay = null!;
        private TimePickerBox _cboStartTime = null!;
        private TimePickerBox _cboEndTime = null!;
        private ComboBox _cboReminder = null!;
        private ComboBox _cboAccount = null!;
        private ComboBox _cboCategory = null!;
        private TextBox _txtLocation = null!;
        private TextBox _txtDescription = null!;
        private Button _btnSave = null!;
        private Button _btnCancel = null!;

        public CalendarEvent ResultEvent => _event;

        public EventEditDialog(CalendarConfigService configService, CalendarEvent? existingEvent = null, DateTime? initialDate = null)
        {
            _configService = configService;
            _isEditMode = (existingEvent != null);

            _event = existingEvent != null
                ? new CalendarEvent
                {
                    Id = existingEvent.Id,
                    Title = existingEvent.Title,
                    Description = existingEvent.Description,
                    Location = existingEvent.Location,
                    StartDate = existingEvent.StartDate,
                    EndDate = existingEvent.EndDate,
                    IsAllDay = existingEvent.IsAllDay,
                    ReminderMinutesBefore = existingEvent.ReminderMinutesBefore,
                    AccountId = existingEvent.AccountId,
                    Category = existingEvent.Category,
                    ColorTag = existingEvent.ColorTag,
                    Recurrence = existingEvent.Recurrence,
                    IsCompleted = existingEvent.IsCompleted,
                    CreatedAt = existingEvent.CreatedAt
                }
                : new CalendarEvent
                {
                    StartDate = (initialDate ?? DateTime.Today).Date.AddHours(9),
                    EndDate = (initialDate ?? DateTime.Today).Date.AddHours(10),
                    Category = _configService.Settings.DefaultCategory,
                    ReminderMinutesBefore = _configService.Settings.DefaultReminderMinutes
                };

            InitializeComponent();
            PopulateData();
        }

        private void InitializeComponent()
        {
            float scale = this.DeviceDpi / 96f;

            this.Text = _isEditMode ? "Edit Event" : "Create New Event";
            this.Icon = CalendarIconHelper.GetApplicationIcon();
            this.Size = new Size((int)(560 * scale), (int)(580 * scale));
            this.MinimumSize = new Size((int)(500 * scale), (int)(520 * scale));
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            this.BackColor = Color.FromArgb(248, 249, 250);

            var scrollPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.FromArgb(248, 249, 250)
            };

            var mainTable = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Padding = new Padding((int)(16 * scale), (int)(12 * scale), (int)(16 * scale), (int)(8 * scale)),
                ColumnCount = 2,
                RowCount = 9,
                AutoSize = true
            };
            mainTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, (int)(130 * scale)));
            mainTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

            Label MakeHeader(string txt) => new Label
            {
                Text = txt,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(60, 64, 70),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Height = (int)(32 * scale)
            };

            _txtTitle = new TextBox { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9.5F) };

            _dtpDate = new DateTimePicker { Dock = DockStyle.Left, Format = DateTimePickerFormat.Long, Width = (int)(260 * scale) };

            var timePanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                WrapContents = false,
                FlowDirection = FlowDirection.LeftToRight,
                Margin = new Padding(0)
            };

            _chkAllDay = new CheckBox
            {
                Text = "All Day",
                AutoSize = true,
                Margin = new Padding(0, (int)(4 * scale), (int)(10 * scale), 0)
            };

            bool use24 = _configService.Settings.TimeFormat24Hour;
            _cboStartTime = new TimePickerBox(use24, scale);
            var lblTo = new Label
            {
                Text = "to",
                AutoSize = true,
                Margin = new Padding((int)(4 * scale), (int)(5 * scale), (int)(4 * scale), 0)
            };
            _cboEndTime = new TimePickerBox(use24, scale);

            // Auto-advance EndTime when StartTime changes
            _cboStartTime.TimeChanged += (s, e) =>
            {
                var newStart = _cboStartTime.SelectedTime;
                var currentEnd = _cboEndTime.SelectedTime;
                if (currentEnd <= newStart)
                {
                    var updatedEnd = newStart.Add(TimeSpan.FromHours(1));
                    if (updatedEnd >= TimeSpan.FromDays(1)) updatedEnd = new TimeSpan(23, 59, 0);
                    _cboEndTime.SelectedTime = updatedEnd;
                }
            };

            var pnlTimePickers = new FlowLayoutPanel
            {
                AutoSize = true,
                WrapContents = false,
                FlowDirection = FlowDirection.LeftToRight,
                Margin = new Padding(0)
            };
            pnlTimePickers.Controls.Add(_cboStartTime);
            pnlTimePickers.Controls.Add(lblTo);
            pnlTimePickers.Controls.Add(_cboEndTime);

            var lblAllDayDesc = new Label
            {
                Text = "(Full-day event)",
                AutoSize = true,
                ForeColor = Color.FromArgb(115, 120, 130),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Italic),
                Margin = new Padding(0, (int)(5 * scale), 0, 0),
                Visible = false
            };

            _chkAllDay.CheckedChanged += (s, e) =>
            {
                bool isAllDay = _chkAllDay.Checked;
                pnlTimePickers.Visible = !isAllDay;
                lblAllDayDesc.Visible = isAllDay;
            };

            timePanel.Controls.Add(_chkAllDay);
            timePanel.Controls.Add(pnlTimePickers);
            timePanel.Controls.Add(lblAllDayDesc);

            // Reminder ComboBox
            _cboReminder = new ComboBox { Dock = DockStyle.Left, DropDownStyle = ComboBoxStyle.DropDownList, Width = (int)(220 * scale) };
            _cboReminder.Items.AddRange(new object[]
            {
                "None",
                "At time of event (0 min)",
                "5 minutes before",
                "10 minutes before",
                "15 minutes before",
                "30 minutes before",
                "1 hour before",
                "2 hours before",
                "1 day before",
                "2 days before",
                "1 week before"
            });

            // Account ComboBox
            _cboAccount = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            _cboAccount.Items.Add("Local Calendar (No email account)");
            var accounts = _configService.GetAccounts();
            foreach (var acc in accounts)
            {
                _cboAccount.Items.Add($"{acc.Name} ({acc.Email})");
            }
            _cboAccount.SelectedIndex = 0;

            // Category ComboBox
            _cboCategory = new ComboBox { Dock = DockStyle.Left, DropDownStyle = ComboBoxStyle.DropDownList, Width = (int)(180 * scale) };
            _cboCategory.Items.AddRange(new object[] { "Work", "Personal", "Important", "Meeting", "Birthday", "General" });
            _cboCategory.SelectedIndex = 0;

            _txtLocation = new TextBox { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9F) };

            _txtDescription = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                Height = (int)(90 * scale),
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Segoe UI", 9F)
            };

            mainTable.Controls.Add(MakeHeader("Title:"), 0, 0);
            mainTable.Controls.Add(_txtTitle, 1, 0);

            mainTable.Controls.Add(MakeHeader("Date:"), 0, 1);
            mainTable.Controls.Add(_dtpDate, 1, 1);

            mainTable.Controls.Add(MakeHeader("Event Time:"), 0, 2);
            mainTable.Controls.Add(timePanel, 1, 2);

            mainTable.Controls.Add(MakeHeader("Time to Remember:"), 0, 3);
            mainTable.Controls.Add(_cboReminder, 1, 3);

            mainTable.Controls.Add(MakeHeader("Account:"), 0, 4);
            mainTable.Controls.Add(_cboAccount, 1, 4);

            mainTable.Controls.Add(MakeHeader("Category:"), 0, 5);
            mainTable.Controls.Add(_cboCategory, 1, 5);

            mainTable.Controls.Add(MakeHeader("Location:"), 0, 6);
            mainTable.Controls.Add(_txtLocation, 1, 6);

            mainTable.Controls.Add(MakeHeader("Description:"), 0, 7);
            mainTable.Controls.Add(_txtDescription, 1, 7);

            scrollPanel.Controls.Add(mainTable);

            // Bottom Buttons
            var bottomPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = (int)(52 * scale),
                Padding = new Padding((int)(16 * scale), (int)(10 * scale), (int)(16 * scale), (int)(10 * scale)),
                BackColor = Color.White
            };

            bottomPanel.Paint += (s, e) =>
            {
                using var p = new Pen(Color.FromArgb(226, 230, 234), 1);
                e.Graphics.DrawLine(p, 0, 0, bottomPanel.Width, 0);
            };

            var btnFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = true,
                Margin = new Padding(0)
            };

            _btnSave = new Button
            {
                Text = _isEditMode ? "Save Changes" : "Create Event",
                AutoSize = true,
                Padding = new Padding((int)(14 * scale), (int)(5 * scale), (int)(14 * scale), (int)(5 * scale)),
                Margin = new Padding(0, 0, (int)(8 * scale), 0),
                FlatStyle = FlatStyle.System,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnSave.Click += OnSaveClicked;

            _btnCancel = new Button
            {
                Text = "Cancel",
                AutoSize = true,
                Padding = new Padding((int)(12 * scale), (int)(5 * scale), (int)(12 * scale), (int)(5 * scale)),
                Margin = new Padding(0),
                FlatStyle = FlatStyle.System,
                Cursor = Cursors.Hand
            };
            _btnCancel.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };

            btnFlow.Controls.Add(_btnSave);
            btnFlow.Controls.Add(_btnCancel);
            bottomPanel.Controls.Add(btnFlow);

            this.Controls.Add(scrollPanel);
            this.Controls.Add(bottomPanel);
        }

        private void PopulateData()
        {
            _txtTitle.Text = _event.Title;
            _dtpDate.Value = _event.StartDate.Date;
            _chkAllDay.Checked = _event.IsAllDay;
            _cboStartTime.SelectedTime = _event.StartDate.TimeOfDay;
            _cboEndTime.SelectedTime = _event.EndDate.TimeOfDay;
            _txtLocation.Text = _event.Location;
            _txtDescription.Text = _event.Description;

            _cboReminder.SelectedIndex = _event.ReminderMinutesBefore switch
            {
                -1 => 0,
                0 => 1,
                5 => 2,
                10 => 3,
                15 => 4,
                30 => 5,
                60 => 6,
                120 => 7,
                1440 => 8,
                2880 => 9,
                10080 => 10,
                _ => 4
            };

            int catIdx = _cboCategory.Items.IndexOf(_event.Category);
            _cboCategory.SelectedIndex = catIdx >= 0 ? catIdx : 0;

            var accounts = _configService.GetAccounts();
            if (!string.IsNullOrEmpty(_event.AccountId))
            {
                int accIdx = accounts.FindIndex(a => a.Id == _event.AccountId);
                if (accIdx >= 0)
                {
                    _cboAccount.SelectedIndex = accIdx + 1;
                }
            }
        }

        private void OnSaveClicked(object? sender, EventArgs e)
        {
            string title = _txtTitle.Text.Trim();
            if (string.IsNullOrWhiteSpace(title))
            {
                MessageBox.Show(this, "Please enter an event title.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _txtTitle.Focus();
                return;
            }

            DateTime date = _dtpDate.Value.Date;
            bool isAllDay = _chkAllDay.Checked;

            DateTime start = isAllDay ? date : date.Add(_cboStartTime.SelectedTime);
            DateTime end = isAllDay ? date.AddDays(1).AddSeconds(-1) : date.Add(_cboEndTime.SelectedTime);

            if (!isAllDay && end <= start)
            {
                end = start.AddHours(1);
            }

            int reminderMinutes = _cboReminder.SelectedIndex switch
            {
                0 => -1,
                1 => 0,
                2 => 5,
                3 => 10,
                4 => 15,
                5 => 30,
                6 => 60,
                7 => 120,
                8 => 1440,
                9 => 2880,
                10 => 10080,
                _ => 15
            };

            string? accountId = null;
            if (_cboAccount.SelectedIndex > 0)
            {
                var accounts = _configService.GetAccounts();
                int idx = _cboAccount.SelectedIndex - 1;
                if (idx >= 0 && idx < accounts.Count)
                {
                    accountId = accounts[idx].Id;
                }
            }

            string category = _cboCategory.SelectedItem?.ToString() ?? "General";
            string colorTag = category switch
            {
                "Work" => "#0078D7",
                "Personal" => "#107C41",
                "Important" => "#D83B01",
                "Meeting" => "#8764B8",
                "Birthday" => "#E3008C",
                _ => "#0078D7"
            };

            _event.Title = title;
            _event.StartDate = start;
            _event.EndDate = end;
            _event.IsAllDay = isAllDay;
            _event.ReminderMinutesBefore = reminderMinutes;
            _event.AccountId = accountId;
            _event.Category = category;
            _event.ColorTag = colorTag;
            _event.Location = _txtLocation.Text.Trim();
            _event.Description = _txtDescription.Text.Trim();

            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}
