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

        // Recurrence controls
        private Button _btnToggleRecurrence = null!;
        private Panel _pnlRecurrenceContent = null!;
        private bool _isRecurrenceExpanded = false;
        private ComboBox _cboRecurrenceFreq = null!;
        private NumericUpDown _numInterval = null!;
        private Label _lblIntervalUnit = null!;
        private FlowLayoutPanel _pnlWeeklyDays = null!;
        private readonly CheckBox[] _chkDaysOfWeek = new CheckBox[7];
        private FlowLayoutPanel _pnlMonthlyOptions = null!;
        private RadioButton _rdoMonthlyDay = null!;
        private NumericUpDown _numMonthlyDay = null!;
        private RadioButton _rdoMonthlyWeekday = null!;
        private ComboBox _cboMonthlyNth = null!;
        private ComboBox _cboMonthlyWeekday = null!;
        private FlowLayoutPanel _pnlYearlyOptions = null!;
        private RadioButton _rdoYearlyDay = null!;
        private ComboBox _cboYearlyMonth = null!;
        private NumericUpDown _numYearlyDay = null!;
        private RadioButton _rdoYearlyWeekday = null!;
        private ComboBox _cboYearlyNth = null!;
        private ComboBox _cboYearlyWeekday = null!;
        private ComboBox _cboYearlyWeekdayMonth = null!;
        private FlowLayoutPanel _pnlEndCondition = null!;
        private RadioButton _rdoEndNever = null!;
        private RadioButton _rdoEndUntil = null!;
        private DateTimePicker _dtpUntilDate = null!;
        private Label _lblRecurrenceSummary = null!;
        private Label _lblRRulePreview = null!;

        private Button _btnSave = null!;
        private Button _btnCancel = null!;
        private Label _lblDateRecurrenceNote = null!;
        private bool _isSyncingDate;

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
                    RecurrenceRule = existingEvent.RecurrenceRule,
                    RecurrenceEnd = existingEvent.RecurrenceEnd,
                    NextOccurrence = existingEvent.NextOccurrence,
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
                RowCount = 8,
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
            _lblDateRecurrenceNote = new Label
            {
                Text = "(controlled by recurrence)",
                Font = new Font("Segoe UI", 8F, FontStyle.Italic),
                ForeColor = Color.FromArgb(110, 115, 125),
                AutoSize = true,
                Margin = new Padding((int)(6 * scale), (int)(6 * scale), 0, 0),
                Visible = false
            };

            _dtpDate.ValueChanged += (s, e) =>
            {
                if (_isSyncingDate) return;
                if (_cboRecurrenceFreq != null && _cboRecurrenceFreq.SelectedIndex == 0)
                {
                    _numMonthlyDay.Value = Math.Clamp(_dtpDate.Value.Day, 1, 31);
                    _cboYearlyMonth.SelectedIndex = Math.Clamp(_dtpDate.Value.Month - 1, 0, 11);
                    _numYearlyDay.Value = Math.Clamp(_dtpDate.Value.Day, 1, 31);
                    for (int i = 0; i < 7; i++)
                    {
                        if (_chkDaysOfWeek[i]?.Tag is DayOfWeek dw)
                        {
                            _chkDaysOfWeek[i].Checked = (dw == _dtpDate.Value.DayOfWeek);
                        }
                    }
                }
                UpdateRecurrencePreview();
            };

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

            // Recurrence Section (Directly under Date, Default Collapsed)
            var pnlRecurrenceWrapper = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Margin = new Padding(0, (int)(4 * scale), 0, (int)(2 * scale))
            };

            _btnToggleRecurrence = new Button
            {
                Text = "🔁  Repeating Task: Does not repeat  ▼",
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding((int)(10 * scale), (int)(5 * scale), (int)(10 * scale), (int)(5 * scale)),
                FlatStyle = FlatStyle.System,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 9F)
            };
            _btnToggleRecurrence.Click += (s, e) =>
            {
                _isRecurrenceExpanded = !_isRecurrenceExpanded;
                if (_isRecurrenceExpanded && _cboRecurrenceFreq.SelectedIndex == 0)
                {
                    _cboRecurrenceFreq.SelectedIndex = 2; // Default to Weekly so all recurrence options appear immediately
                }
                _pnlRecurrenceContent.Visible = _isRecurrenceExpanded;
                UpdateRecurrenceToggleHeader();
                UpdateDateChooserEnabledState();
            };

            BuildRecurrencePanel(scale);

            pnlRecurrenceWrapper.Controls.Add(_btnToggleRecurrence);
            pnlRecurrenceWrapper.Controls.Add(_pnlRecurrenceContent);

            var datePanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Margin = new Padding(0)
            };

            var datePickerRow = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0, 0, 0, (int)(2 * scale))
            };
            datePickerRow.Controls.Add(_dtpDate);
            datePickerRow.Controls.Add(_lblDateRecurrenceNote);

            datePanel.Controls.Add(datePickerRow);
            datePanel.Controls.Add(pnlRecurrenceWrapper);

            var lblDateHeader = new Label
            {
                Text = "Date:",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(60, 64, 70),
                Dock = DockStyle.Top,
                TextAlign = ContentAlignment.MiddleLeft,
                Height = (int)(32 * scale)
            };

            mainTable.Controls.Add(MakeHeader("Title:"), 0, 0);
            mainTable.Controls.Add(_txtTitle, 1, 0);

            mainTable.Controls.Add(lblDateHeader, 0, 1);
            mainTable.Controls.Add(datePanel, 1, 1);

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

            if (_isEditMode)
            {
                var btnDelete = new Button
                {
                    Text = "🗑️ Delete Event",
                    Dock = DockStyle.Left,
                    AutoSize = true,
                    Padding = new Padding((int)(12 * scale), (int)(5 * scale), (int)(12 * scale), (int)(5 * scale)),
                    FlatStyle = FlatStyle.System,
                    Font = new Font("Segoe UI", 9F),
                    ForeColor = Color.FromArgb(192, 0, 0),
                    Cursor = Cursors.Hand
                };
                btnDelete.Click += (s, e) =>
                {
                    string prompt = (_event.IsVirtualOccurrence || _event.IsRecurring)
                        ? $"'{_event.Title}' is a recurring event. Are you sure you want to delete this event series?"
                        : $"Are you sure you want to delete '{_event.Title}'?";
                    var res = MessageBox.Show(this, prompt, "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (res == DialogResult.Yes)
                    {
                        this.DialogResult = DialogResult.Abort;
                        this.Close();
                    }
                };
                bottomPanel.Controls.Add(btnDelete);
            }

            this.Controls.Add(scrollPanel);
            this.Controls.Add(bottomPanel);
        }

        private void BuildRecurrencePanel(float scale)
        {
            _pnlRecurrenceContent = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Visible = false,
                BackColor = Color.White,
                Padding = new Padding((int)(12 * scale)),
                Margin = new Padding(0, (int)(4 * scale), 0, (int)(4 * scale))
            };

            _pnlRecurrenceContent.Paint += (s, e) =>
            {
                using var pen = new Pen(Color.FromArgb(218, 222, 229), 1);
                e.Graphics.DrawRoundedRectangle(pen, new Rectangle(0, 0, _pnlRecurrenceContent.Width - 1, _pnlRecurrenceContent.Height - 1), 6);
            };

            // 1. Frequency and Interval Row
            var rowFreq = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0, 0, 0, (int)(8 * scale))
            };

            var lblFreq = new Label { Text = "Repeats:", AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold), Margin = new Padding(0, 4, 6, 0) };
            _cboRecurrenceFreq = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = (int)(110 * scale) };
            _cboRecurrenceFreq.Items.AddRange(new object[] { "None", "Daily", "Weekly", "Monthly", "Yearly" });
            _cboRecurrenceFreq.SelectedIndex = 0;

            var lblEvery = new Label { Text = "Every:", AutoSize = true, Margin = new Padding((int)(10 * scale), 4, 6, 0) };
            _numInterval = new NumericUpDown { Width = (int)(55 * scale), Minimum = 1, Maximum = 99, Value = 1 };
            _lblIntervalUnit = new Label { Text = "day(s)", AutoSize = true, Margin = new Padding(6, 4, 0, 0) };

            rowFreq.Controls.Add(lblFreq);
            rowFreq.Controls.Add(_cboRecurrenceFreq);
            rowFreq.Controls.Add(lblEvery);
            rowFreq.Controls.Add(_numInterval);
            rowFreq.Controls.Add(_lblIntervalUnit);

            // 2. Weekly Days Panel
            _pnlWeeklyDays = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Visible = false,
                Margin = new Padding(0, 0, 0, (int)(8 * scale))
            };
            var lblWeeklyDaysTitle = new Label { Text = "Repeat on:", AutoSize = true, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), Margin = new Padding(0, 2, 0, 4) };
            var flowDays = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                Margin = new Padding(0)
            };

            string[] dayLabels = { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };
            DayOfWeek[] dayEnums = { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday };
            for (int i = 0; i < 7; i++)
            {
                var chk = new CheckBox
                {
                    Text = dayLabels[i],
                    Tag = dayEnums[i],
                    AutoSize = true,
                    Margin = new Padding(0, 2, (int)(6 * scale), 0),
                    Font = new Font("Segoe UI", 8.5F)
                };
                chk.CheckedChanged += (s, e) => UpdateRecurrencePreview();
                _chkDaysOfWeek[i] = chk;
                flowDays.Controls.Add(chk);
            }
            _pnlWeeklyDays.Controls.Add(lblWeeklyDaysTitle);
            _pnlWeeklyDays.Controls.Add(flowDays);

            // 3. Monthly Options Panel
            _pnlMonthlyOptions = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Visible = false,
                Margin = new Padding(0, 0, 0, (int)(8 * scale))
            };

            var rowMonthlyDay = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = new Padding(0, 0, 0, 4) };
            _rdoMonthlyDay = new RadioButton { Text = "On day", AutoSize = true, Checked = true, Margin = new Padding(0, 3, 4, 0) };
            _numMonthlyDay = new NumericUpDown { Minimum = 1, Maximum = 31, Value = 1, Width = (int)(55 * scale) };
            var lblMonthlyDaySuffix = new Label { Text = "of every month", AutoSize = true, Margin = new Padding(6, 4, 0, 0) };
            rowMonthlyDay.Controls.Add(_rdoMonthlyDay);
            rowMonthlyDay.Controls.Add(_numMonthlyDay);
            rowMonthlyDay.Controls.Add(lblMonthlyDaySuffix);

            var rowMonthlyWeekday = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = new Padding(0, 0, 0, 4) };
            _rdoMonthlyWeekday = new RadioButton { Text = "On the", AutoSize = true, Margin = new Padding(0, 3, 4, 0) };
            _cboMonthlyNth = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = (int)(65 * scale), Enabled = false };
            _cboMonthlyNth.Items.AddRange(new object[] { "1st", "2nd", "3rd", "4th", "Last" });
            _cboMonthlyNth.SelectedIndex = 0;
            _cboMonthlyWeekday = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = (int)(105 * scale), Enabled = false };
            _cboMonthlyWeekday.Items.AddRange(new object[] { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" });
            _cboMonthlyWeekday.SelectedIndex = 1; // Tuesday
            var lblMonthlyWdSuffix = new Label { Text = "of every month", AutoSize = true, Margin = new Padding(6, 4, 0, 0) };
            rowMonthlyWeekday.Controls.Add(_rdoMonthlyWeekday);
            rowMonthlyWeekday.Controls.Add(_cboMonthlyNth);
            rowMonthlyWeekday.Controls.Add(_cboMonthlyWeekday);
            rowMonthlyWeekday.Controls.Add(lblMonthlyWdSuffix);

            _rdoMonthlyDay.CheckedChanged += (s, e) =>
            {
                _numMonthlyDay.Enabled = _rdoMonthlyDay.Checked;
                _cboMonthlyNth.Enabled = !_rdoMonthlyDay.Checked;
                _cboMonthlyWeekday.Enabled = !_rdoMonthlyDay.Checked;
                UpdateRecurrencePreview();
            };
            _rdoMonthlyWeekday.CheckedChanged += (s, e) =>
            {
                _numMonthlyDay.Enabled = !_rdoMonthlyWeekday.Checked;
                _cboMonthlyNth.Enabled = _rdoMonthlyWeekday.Checked;
                _cboMonthlyWeekday.Enabled = _rdoMonthlyWeekday.Checked;
                UpdateRecurrencePreview();
            };

            _pnlMonthlyOptions.Controls.Add(rowMonthlyDay);
            _pnlMonthlyOptions.Controls.Add(rowMonthlyWeekday);

            // 4. Yearly Options Panel
            _pnlYearlyOptions = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Visible = false,
                Margin = new Padding(0, 0, 0, (int)(8 * scale))
            };

            var rowYearlyDay = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = new Padding(0, 0, 0, 4) };
            _rdoYearlyDay = new RadioButton { Text = "Every year on", AutoSize = true, Checked = true, Margin = new Padding(0, 3, 4, 0) };
            _cboYearlyMonth = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = (int)(105 * scale) };
            string[] months = { "January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December" };
            _cboYearlyMonth.Items.AddRange(months);
            _cboYearlyMonth.SelectedIndex = 0;
            _numYearlyDay = new NumericUpDown { Minimum = 1, Maximum = 31, Value = 1, Width = (int)(55 * scale) };
            rowYearlyDay.Controls.Add(_rdoYearlyDay);
            rowYearlyDay.Controls.Add(_cboYearlyMonth);
            rowYearlyDay.Controls.Add(_numYearlyDay);

            var rowYearlyWeekday = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = new Padding(0, 0, 0, 4) };
            _rdoYearlyWeekday = new RadioButton { Text = "On the", AutoSize = true, Margin = new Padding(0, 3, 4, 0) };
            _cboYearlyNth = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = (int)(65 * scale), Enabled = false };
            _cboYearlyNth.Items.AddRange(new object[] { "1st", "2nd", "3rd", "4th", "Last" });
            _cboYearlyNth.SelectedIndex = 0;
            _cboYearlyWeekday = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = (int)(100 * scale), Enabled = false };
            _cboYearlyWeekday.Items.AddRange(new object[] { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" });
            _cboYearlyWeekday.SelectedIndex = 3; // Thursday
            var lblYearlyOf = new Label { Text = "of", AutoSize = true, Margin = new Padding(4, 4, 4, 0) };
            _cboYearlyWeekdayMonth = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = (int)(105 * scale), Enabled = false };
            _cboYearlyWeekdayMonth.Items.AddRange(months);
            _cboYearlyWeekdayMonth.SelectedIndex = 10; // November
            rowYearlyWeekday.Controls.Add(_rdoYearlyWeekday);
            rowYearlyWeekday.Controls.Add(_cboYearlyNth);
            rowYearlyWeekday.Controls.Add(_cboYearlyWeekday);
            rowYearlyWeekday.Controls.Add(lblYearlyOf);
            rowYearlyWeekday.Controls.Add(_cboYearlyWeekdayMonth);

            _rdoYearlyDay.CheckedChanged += (s, e) =>
            {
                _cboYearlyMonth.Enabled = _rdoYearlyDay.Checked;
                _numYearlyDay.Enabled = _rdoYearlyDay.Checked;
                _cboYearlyNth.Enabled = !_rdoYearlyDay.Checked;
                _cboYearlyWeekday.Enabled = !_rdoYearlyDay.Checked;
                _cboYearlyWeekdayMonth.Enabled = !_rdoYearlyDay.Checked;
                UpdateRecurrencePreview();
            };
            _rdoYearlyWeekday.CheckedChanged += (s, e) =>
            {
                _cboYearlyMonth.Enabled = !_rdoYearlyWeekday.Checked;
                _numYearlyDay.Enabled = !_rdoYearlyWeekday.Checked;
                _cboYearlyNth.Enabled = _rdoYearlyWeekday.Checked;
                _cboYearlyWeekday.Enabled = _rdoYearlyWeekday.Checked;
                _cboYearlyWeekdayMonth.Enabled = _rdoYearlyWeekday.Checked;
                UpdateRecurrencePreview();
            };

            _pnlYearlyOptions.Controls.Add(rowYearlyDay);
            _pnlYearlyOptions.Controls.Add(rowYearlyWeekday);

            // 5. End Condition Row
            _pnlEndCondition = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                Visible = false,
                Margin = new Padding(0, 0, 0, (int)(8 * scale))
            };
            var lblEnds = new Label { Text = "Ends:", AutoSize = true, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), Margin = new Padding(0, 4, 8, 0) };
            _rdoEndNever = new RadioButton { Text = "Never", AutoSize = true, Checked = true, Margin = new Padding(0, 3, 10, 0) };
            _rdoEndUntil = new RadioButton { Text = "Until date:", AutoSize = true, Margin = new Padding(0, 3, 4, 0) };
            _dtpUntilDate = new DateTimePicker { Format = DateTimePickerFormat.Short, Width = (int)(110 * scale), Enabled = false, Value = DateTime.Today.AddMonths(3) };

            _rdoEndNever.CheckedChanged += (s, e) =>
            {
                _dtpUntilDate.Enabled = _rdoEndUntil.Checked;
                UpdateRecurrencePreview();
            };
            _rdoEndUntil.CheckedChanged += (s, e) =>
            {
                _dtpUntilDate.Enabled = _rdoEndUntil.Checked;
                UpdateRecurrencePreview();
            };

            _pnlEndCondition.Controls.Add(lblEnds);
            _pnlEndCondition.Controls.Add(_rdoEndNever);
            _pnlEndCondition.Controls.Add(_rdoEndUntil);
            _pnlEndCondition.Controls.Add(_dtpUntilDate);

            // 6. Live Preview
            var pnlPreview = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Margin = new Padding(0, 4, 0, 0)
            };
            _lblRecurrenceSummary = new Label { Text = "Does not repeat", AutoSize = true, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), ForeColor = Color.FromArgb(0, 120, 215), Margin = new Padding(0, 0, 0, 2) };
            _lblRRulePreview = new Label { Text = "", AutoSize = true, Font = new Font("Consolas", 8F), ForeColor = Color.FromArgb(120, 125, 135) };
            pnlPreview.Controls.Add(_lblRecurrenceSummary);
            pnlPreview.Controls.Add(_lblRRulePreview);

            // Event handlers for live updates
            _cboRecurrenceFreq.SelectedIndexChanged += OnRecurrenceFreqChanged;
            _numInterval.ValueChanged += (s, e) => UpdateRecurrencePreview();
            _numMonthlyDay.ValueChanged += (s, e) => UpdateRecurrencePreview();
            _cboMonthlyNth.SelectedIndexChanged += (s, e) => UpdateRecurrencePreview();
            _cboMonthlyWeekday.SelectedIndexChanged += (s, e) => UpdateRecurrencePreview();
            _cboYearlyMonth.SelectedIndexChanged += (s, e) => UpdateRecurrencePreview();
            _numYearlyDay.ValueChanged += (s, e) => UpdateRecurrencePreview();
            _cboYearlyNth.SelectedIndexChanged += (s, e) => UpdateRecurrencePreview();
            _cboYearlyWeekday.SelectedIndexChanged += (s, e) => UpdateRecurrencePreview();
            _cboYearlyWeekdayMonth.SelectedIndexChanged += (s, e) => UpdateRecurrencePreview();
            _dtpUntilDate.ValueChanged += (s, e) => UpdateRecurrencePreview();

            _pnlRecurrenceContent.Controls.Add(rowFreq);
            _pnlRecurrenceContent.Controls.Add(_pnlWeeklyDays);
            _pnlRecurrenceContent.Controls.Add(_pnlMonthlyOptions);
            _pnlRecurrenceContent.Controls.Add(_pnlYearlyOptions);
            _pnlRecurrenceContent.Controls.Add(_pnlEndCondition);
            _pnlRecurrenceContent.Controls.Add(pnlPreview);
        }

        private void OnRecurrenceFreqChanged(object? sender, EventArgs e)
        {
            string freq = _cboRecurrenceFreq.SelectedItem?.ToString() ?? "None";
            bool isRecurring = !freq.Equals("None", StringComparison.OrdinalIgnoreCase);

            _lblIntervalUnit.Text = freq switch
            {
                "Daily" => "day(s)",
                "Weekly" => "week(s)",
                "Monthly" => "month(s)",
                "Yearly" => "year(s)",
                _ => "day(s)"
            };

            _pnlWeeklyDays.Visible = freq.Equals("Weekly", StringComparison.OrdinalIgnoreCase);
            _pnlMonthlyOptions.Visible = freq.Equals("Monthly", StringComparison.OrdinalIgnoreCase);
            _pnlYearlyOptions.Visible = freq.Equals("Yearly", StringComparison.OrdinalIgnoreCase);
            _pnlEndCondition.Visible = isRecurring;

            UpdateRecurrencePreview();
        }

        private RecurrencePattern BuildPatternFromUI()
        {
            var p = new RecurrencePattern
            {
                Frequency = _cboRecurrenceFreq.SelectedItem?.ToString() ?? "None",
                Interval = (int)_numInterval.Value
            };

            if (!p.IsActive) return p;

            if (p.Frequency.Equals("Weekly", StringComparison.OrdinalIgnoreCase))
            {
                for (int i = 0; i < 7; i++)
                {
                    if (_chkDaysOfWeek[i] != null && _chkDaysOfWeek[i].Checked && _chkDaysOfWeek[i].Tag is DayOfWeek dw)
                    {
                        p.DaysOfWeek.Add(dw);
                    }
                }
                if (p.DaysOfWeek.Count == 0)
                {
                    p.DaysOfWeek.Add(_dtpDate.Value.DayOfWeek);
                }
            }
            else if (p.Frequency.Equals("Monthly", StringComparison.OrdinalIgnoreCase))
            {
                if (_rdoMonthlyDay.Checked)
                {
                    p.DayOfMonth = (int)_numMonthlyDay.Value;
                }
                else
                {
                    p.WeekOfMonth = _cboMonthlyNth.SelectedIndex switch
                    {
                        0 => 1,
                        1 => 2,
                        2 => 3,
                        3 => 4,
                        4 => -1,
                        _ => 1
                    };
                    p.SpecificWeekday = (DayOfWeek)Enum.Parse(typeof(DayOfWeek), _cboMonthlyWeekday.SelectedItem?.ToString() ?? "Monday");
                }
            }
            else if (p.Frequency.Equals("Yearly", StringComparison.OrdinalIgnoreCase))
            {
                if (_rdoYearlyDay.Checked)
                {
                    p.MonthOfYear = _cboYearlyMonth.SelectedIndex + 1;
                    p.DayOfMonth = (int)_numYearlyDay.Value;
                }
                else
                {
                    p.WeekOfMonth = _cboYearlyNth.SelectedIndex switch
                    {
                        0 => 1,
                        1 => 2,
                        2 => 3,
                        3 => 4,
                        4 => -1,
                        _ => 1
                    };
                    p.SpecificWeekday = (DayOfWeek)Enum.Parse(typeof(DayOfWeek), _cboYearlyWeekday.SelectedItem?.ToString() ?? "Thursday");
                    p.MonthOfYear = _cboYearlyWeekdayMonth.SelectedIndex + 1;
                }
            }

            if (_rdoEndUntil.Checked)
            {
                p.UntilDate = _dtpUntilDate.Value.Date.AddDays(1).AddSeconds(-1);
            }

            return p;
        }

        private void SyncDateWithRecurrencePattern(RecurrencePattern pattern)
        {
            _isSyncingDate = true;
            try
            {
                DateTime current = _dtpDate.Value;
                if (pattern.Frequency.Equals("Monthly", StringComparison.OrdinalIgnoreCase))
                {
                    if (pattern.DayOfMonth.HasValue)
                    {
                        int maxDays = DateTime.DaysInMonth(current.Year, current.Month);
                        int day = Math.Clamp(pattern.DayOfMonth.Value, 1, maxDays);
                        _dtpDate.Value = new DateTime(current.Year, current.Month, day);
                    }
                    else if (pattern.WeekOfMonth.HasValue && pattern.SpecificWeekday.HasValue)
                    {
                        _dtpDate.Value = RecurrenceHelper.GetNthWeekdayOfMonth(current.Year, current.Month, pattern.SpecificWeekday.Value, pattern.WeekOfMonth.Value);
                    }
                }
                else if (pattern.Frequency.Equals("Yearly", StringComparison.OrdinalIgnoreCase))
                {
                    int month = Math.Clamp(pattern.MonthOfYear ?? current.Month, 1, 12);
                    if (pattern.DayOfMonth.HasValue)
                    {
                        int maxDays = DateTime.DaysInMonth(current.Year, month);
                        int day = Math.Clamp(pattern.DayOfMonth.Value, 1, maxDays);
                        _dtpDate.Value = new DateTime(current.Year, month, day);
                    }
                    else if (pattern.WeekOfMonth.HasValue && pattern.SpecificWeekday.HasValue)
                    {
                        _dtpDate.Value = RecurrenceHelper.GetNthWeekdayOfMonth(current.Year, month, pattern.SpecificWeekday.Value, pattern.WeekOfMonth.Value);
                    }
                }
                else if (pattern.Frequency.Equals("Weekly", StringComparison.OrdinalIgnoreCase) && pattern.DaysOfWeek.Count > 0)
                {
                    if (!pattern.DaysOfWeek.Contains(current.DayOfWeek))
                    {
                        for (int offset = 1; offset <= 7; offset++)
                        {
                            var candidate = current.AddDays(offset);
                            if (pattern.DaysOfWeek.Contains(candidate.DayOfWeek))
                            {
                                _dtpDate.Value = candidate;
                                break;
                            }
                        }
                    }
                }
            }
            catch
            {
                // Fallback safe
            }
            finally
            {
                _isSyncingDate = false;
            }
        }

        private void UpdateDateChooserEnabledState()
        {
            var pattern = BuildPatternFromUI();
            bool isRecurring = pattern.IsActive;
            _dtpDate.Enabled = !isRecurring;
            if (_lblDateRecurrenceNote != null)
            {
                _lblDateRecurrenceNote.Visible = isRecurring;
            }
        }

        private void UpdateRecurrencePreview()
        {
            var pattern = BuildPatternFromUI();
            if (!pattern.IsActive)
            {
                _lblRecurrenceSummary.Text = "Does not repeat";
                _lblRRulePreview.Text = "";
            }
            else
            {
                _lblRecurrenceSummary.Text = RecurrenceHelper.GetHumanReadableDescription(pattern, _dtpDate.Value);
                _lblRRulePreview.Text = "RRULE:" + RecurrenceHelper.ToRRule(pattern);
                SyncDateWithRecurrencePattern(pattern);
            }

            UpdateRecurrenceToggleHeader();
            UpdateDateChooserEnabledState();
        }

        private void UpdateRecurrenceToggleHeader()
        {
            var pattern = BuildPatternFromUI();
            string state = pattern.IsActive ? pattern.Frequency : "Does not repeat";
            string arrow = _isRecurrenceExpanded ? "▲" : "▼";
            _btnToggleRecurrence.Text = $"🔁  Repeating Task: {state}  {arrow}";
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

            // Populate recurrence
            if (_event.IsRecurring)
            {
                var p = RecurrenceHelper.ParseRRule(_event.RecurrenceRule, _event.StartDate);
                int freqIdx = p.Frequency switch
                {
                    "Daily" => 1,
                    "Weekly" => 2,
                    "Monthly" => 3,
                    "Yearly" => 4,
                    _ => 0
                };
                _cboRecurrenceFreq.SelectedIndex = freqIdx;
                _numInterval.Value = Math.Clamp(p.Interval, 1, 99);

                if (p.Frequency.Equals("Weekly", StringComparison.OrdinalIgnoreCase))
                {
                    for (int i = 0; i < 7; i++)
                    {
                        if (_chkDaysOfWeek[i]?.Tag is DayOfWeek dw)
                        {
                            _chkDaysOfWeek[i].Checked = p.DaysOfWeek.Contains(dw);
                        }
                    }
                }
                else if (p.Frequency.Equals("Monthly", StringComparison.OrdinalIgnoreCase))
                {
                    if (p.WeekOfMonth.HasValue && p.SpecificWeekday.HasValue)
                    {
                        _rdoMonthlyWeekday.Checked = true;
                        _cboMonthlyNth.SelectedIndex = p.WeekOfMonth.Value switch { 1 => 0, 2 => 1, 3 => 2, 4 => 3, -1 => 4, _ => 0 };
                        _cboMonthlyWeekday.SelectedItem = p.SpecificWeekday.Value.ToString();
                    }
                    else
                    {
                        _rdoMonthlyDay.Checked = true;
                        _numMonthlyDay.Value = Math.Clamp(p.DayOfMonth ?? _event.StartDate.Day, 1, 31);
                    }
                }
                else if (p.Frequency.Equals("Yearly", StringComparison.OrdinalIgnoreCase))
                {
                    if (p.WeekOfMonth.HasValue && p.SpecificWeekday.HasValue)
                    {
                        _rdoYearlyWeekday.Checked = true;
                        _cboYearlyNth.SelectedIndex = p.WeekOfMonth.Value switch { 1 => 0, 2 => 1, 3 => 2, 4 => 3, -1 => 4, _ => 0 };
                        _cboYearlyWeekday.SelectedItem = p.SpecificWeekday.Value.ToString();
                        _cboYearlyWeekdayMonth.SelectedIndex = Math.Clamp((p.MonthOfYear ?? _event.StartDate.Month) - 1, 0, 11);
                    }
                    else
                    {
                        _rdoYearlyDay.Checked = true;
                        _cboYearlyMonth.SelectedIndex = Math.Clamp((p.MonthOfYear ?? _event.StartDate.Month) - 1, 0, 11);
                        _numYearlyDay.Value = Math.Clamp(p.DayOfMonth ?? _event.StartDate.Day, 1, 31);
                    }
                }

                if (p.UntilDate.HasValue)
                {
                    _rdoEndUntil.Checked = true;
                    _dtpUntilDate.Value = p.UntilDate.Value;
                }
                else
                {
                    _rdoEndNever.Checked = true;
                }

                _isRecurrenceExpanded = true;
                _pnlRecurrenceContent.Visible = true;
            }
            else
            {
                _cboRecurrenceFreq.SelectedIndex = 0;
                _numMonthlyDay.Value = Math.Clamp(_event.StartDate.Day, 1, 31);
                _cboYearlyMonth.SelectedIndex = Math.Clamp(_event.StartDate.Month - 1, 0, 11);
                _numYearlyDay.Value = Math.Clamp(_event.StartDate.Day, 1, 31);
                for (int i = 0; i < 7; i++)
                {
                    if (_chkDaysOfWeek[i]?.Tag is DayOfWeek dw)
                    {
                        _chkDaysOfWeek[i].Checked = (dw == _event.StartDate.DayOfWeek);
                    }
                }
            }

            UpdateRecurrencePreview();
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

            // Save recurrence settings
            var pattern = BuildPatternFromUI();
            if (!pattern.IsActive)
            {
                _event.Recurrence = "None";
                _event.RecurrenceRule = null;
                _event.RecurrenceEnd = null;
                _event.NextOccurrence = null;
            }
            else
            {
                _event.Recurrence = pattern.Frequency;
                _event.RecurrenceRule = RecurrenceHelper.ToRRule(pattern);
                _event.RecurrenceEnd = pattern.UntilDate;
                RecurrenceHelper.UpdateNextOccurrence(_event, DateTime.Now);
            }

            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}
