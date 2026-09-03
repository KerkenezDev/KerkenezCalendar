using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using KerkenezCalendar.Models;
using KerkenezCalendar.Services;

namespace KerkenezCalendar.UI.Controls
{
    public class ChosenEventView : UserControl
    {
        private readonly CalendarEventService _eventService;
        private readonly CalendarConfigService _configService;

        private CalendarEvent? _currentEvent;
        private DateTime _currentDate = DateTime.Today;

        public event Action<CalendarEvent>? EditRequested;
        public event Action<CalendarEvent>? DeleteRequested;

        // View Mode Controls
        private Panel _pnlEventViewer = null!;
        private Panel _contentPanel = null!;
        private Panel _headerPanel = null!;
        private Label _lblTitle = null!;
        private Label _lblTime = null!;
        private Label _lblReminder = null!;
        private Label _lblLocation = null!;
        private Label _lblAccount = null!;
        private Label _lblCategory = null!;
        private TableLayoutPanel _metaTable = null!;
        private Label _lblDescHeader = null!;
        private Panel _descContainer = null!;
        private TextBox _txtDescription = null!;
        private Button _btnEdit = null!;
        private Button _btnDelete = null!;

        // Empty / Quick-Add Mode Controls
        private Panel _pnlEmptyOrQuickAdd = null!;
        private TextBox _txtQuickTitle = null!;
        private DateTimePicker _dtpQuickDate = null!;
        private TimePickerBox _dtpQuickTime = null!;
        private ComboBox _cboQuickReminder = null!;
        private Button _btnQuickSave = null!;

        public ChosenEventView(CalendarEventService eventService, CalendarConfigService configService)
        {
            _eventService = eventService;
            _configService = configService;

            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Dock = DockStyle.Fill;
            this.BackColor = Color.White;
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular);

            float scale = this.DeviceDpi / 96f;

            // 1. Event Viewer Panel (Outer AutoScroll viewport)
            _pnlEventViewer = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                AutoScroll = true
            };

            // Inner content panel that dynamically adjusts its height to avoid clipping
            _contentPanel = new Panel
            {
                Location = new Point(0, 0),
                BackColor = Color.White,
                Padding = new Padding((int)(14 * scale), (int)(10 * scale), (int)(14 * scale), (int)(12 * scale))
            };

            // Top Header: Title and Action Buttons
            _headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = (int)(36 * scale),
                BackColor = Color.Transparent
            };

            _lblTitle = new Label
            {
                Text = "Selected Event Title",
                Font = new Font("Segoe UI", 11.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 30, 30),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true
            };

            var actionsFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = true,
                Margin = new Padding(0)
            };

            _btnEdit = new Button
            {
                Text = "✏️ Edit",
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding((int)(8 * scale), (int)(4 * scale), (int)(8 * scale), (int)(4 * scale)),
                Margin = new Padding(0, 0, (int)(6 * scale), 0),
                FlatStyle = FlatStyle.System,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnEdit.Click += (s, e) =>
            {
                if (_currentEvent != null) EditRequested?.Invoke(_currentEvent);
            };

            _btnDelete = new Button
            {
                Text = "🗑️ Delete",
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding((int)(8 * scale), (int)(4 * scale), (int)(8 * scale), (int)(4 * scale)),
                Margin = new Padding(0),
                FlatStyle = FlatStyle.System,
                Cursor = Cursors.Hand
            };
            _btnDelete.Click += (s, e) =>
            {
                if (_currentEvent != null) DeleteRequested?.Invoke(_currentEvent);
            };

            actionsFlow.Controls.Add(_btnEdit);
            actionsFlow.Controls.Add(_btnDelete);

            _headerPanel.Controls.Add(_lblTitle);
            _headerPanel.Controls.Add(actionsFlow);

            // Metadata Grid (Compact 4-column layout: ~60px total height)
            _metaTable = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 4,
                RowCount = 3,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(0, (int)(4 * scale), 0, (int)(6 * scale)),
                BackColor = Color.Transparent
            };
            _metaTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, (int)(92 * scale)));
            _metaTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            _metaTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, (int)(92 * scale)));
            _metaTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));

            Label MakeMetaLabel(string text) => new Label
            {
                Text = text,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(100, 106, 115),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Height = (int)(20 * scale)
            };

            Label MakeValueLabel() => new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                ForeColor = Color.FromArgb(40, 40, 40),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                Height = (int)(20 * scale)
            };

            _lblTime = MakeValueLabel();
            _lblReminder = MakeValueLabel();
            _lblLocation = MakeValueLabel();
            _lblAccount = MakeValueLabel();
            _lblCategory = MakeValueLabel();

            // Row 0: Time across cols 1 to 3
            _metaTable.Controls.Add(MakeMetaLabel("🕒 Time:"), 0, 0);
            _metaTable.Controls.Add(_lblTime, 1, 0);
            _metaTable.SetColumnSpan(_lblTime, 3);

            // Row 1: Reminder & Location
            _metaTable.Controls.Add(MakeMetaLabel("🔔 Reminder:"), 0, 1);
            _metaTable.Controls.Add(_lblReminder, 1, 1);
            _metaTable.Controls.Add(MakeMetaLabel("📍 Location:"), 2, 1);
            _metaTable.Controls.Add(_lblLocation, 3, 1);

            // Row 2: Category & Account
            _metaTable.Controls.Add(MakeMetaLabel("🏷️ Category:"), 0, 2);
            _metaTable.Controls.Add(_lblCategory, 1, 2);
            _metaTable.Controls.Add(MakeMetaLabel("👤 Account:"), 2, 2);
            _metaTable.Controls.Add(_lblAccount, 3, 2);

            // Description / Notes Box
            _lblDescHeader = new Label
            {
                Text = "Description & Notes",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(100, 106, 115),
                Dock = DockStyle.Top,
                Height = (int)(22 * scale),
                Padding = new Padding(0, (int)(4 * scale), 0, 0)
            };

            _descContainer = new Panel
            {
                Dock = DockStyle.Top,
                Height = (int)(110 * scale),
                Padding = new Padding(0, (int)(2 * scale), 0, 0)
            };

            _txtDescription = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = Color.FromArgb(250, 251, 252),
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                ForeColor = Color.FromArgb(45, 50, 58)
            };

            _txtDescription.MouseWheel += (s, e) =>
            {
                if (_pnlEventViewer.VerticalScroll.Visible)
                {
                    int newY = -_pnlEventViewer.AutoScrollPosition.Y - (e.Delta / 2);
                    _pnlEventViewer.AutoScrollPosition = new Point(0, Math.Max(0, newY));
                }
            };

            _descContainer.Controls.Add(_txtDescription);

            _contentPanel.Controls.Add(_descContainer);
            _contentPanel.Controls.Add(_lblDescHeader);
            _contentPanel.Controls.Add(_metaTable);
            _contentPanel.Controls.Add(_headerPanel);

            _pnlEventViewer.Controls.Add(_contentPanel);
            _pnlEventViewer.Resize += (s, e) => UpdateEventViewerLayout();

            // 2. Empty / Quick-Add Panel
            _pnlEmptyOrQuickAdd = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding((int)(20 * scale)),
                BackColor = Color.White,
                AutoScroll = true
            };

            var lblEmptyTitle = new Label
            {
                Text = "Quick Add Event",
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 30, 30),
                Dock = DockStyle.Top,
                Height = (int)(26 * scale)
            };

            var lblEmptySub = new Label
            {
                Text = "No event selected. Fill the quick form below or click '+ Add Event' in that day's schedule.",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                ForeColor = Color.FromArgb(100, 106, 115),
                Dock = DockStyle.Top,
                Height = (int)(24 * scale)
            };

            var quickTable = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 2,
                RowCount = 4,
                AutoSize = true,
                Padding = new Padding(0, (int)(10 * scale), 0, 0)
            };
            quickTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, (int)(120 * scale)));
            quickTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

            Label MakeFormLabel(string txt) => new Label
            {
                Text = txt,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(70, 75, 82),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Height = (int)(28 * scale)
            };

            _txtQuickTitle = new TextBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9F),
                PlaceholderText = "Event title..."
            };

            _dtpQuickDate = new DateTimePicker
            {
                Dock = DockStyle.Left,
                Format = DateTimePickerFormat.Short,
                Width = (int)(130 * scale)
            };

            bool use24 = _configService.Settings.TimeFormat24Hour;
            _dtpQuickTime = new TimePickerBox(use24, scale);

            _cboQuickReminder = new ComboBox
            {
                Dock = DockStyle.Left,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = (int)(180 * scale)
            };
            _cboQuickReminder.Items.AddRange(new object[]
            {
                "None",
                "At time of event",
                "5 minutes before",
                "10 minutes before",
                "15 minutes before",
                "30 minutes before",
                "1 hour before",
                "2 hours before",
                "1 day before"
            });
            _cboQuickReminder.SelectedIndex = 4; // 15m default

            _btnQuickSave = new Button
            {
                Text = "💾 Save Event",
                AutoSize = true,
                Padding = new Padding((int)(12 * scale), (int)(5 * scale), (int)(12 * scale), (int)(5 * scale)),
                FlatStyle = FlatStyle.System,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnQuickSave.Click += OnQuickSaveClicked;

            quickTable.Controls.Add(MakeFormLabel("Title:"), 0, 0);
            quickTable.Controls.Add(_txtQuickTitle, 1, 0);

            quickTable.Controls.Add(MakeFormLabel("Date & Time:"), 0, 1);
            var dateTimeFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, Margin = new Padding(0) };
            dateTimeFlow.Controls.Add(_dtpQuickDate);
            dateTimeFlow.Controls.Add(_dtpQuickTime);
            quickTable.Controls.Add(dateTimeFlow, 1, 1);

            quickTable.Controls.Add(MakeFormLabel("Time to Remember:"), 0, 2);
            quickTable.Controls.Add(_cboQuickReminder, 1, 2);

            var saveFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, Margin = new Padding(0, (int)(8 * scale), 0, 0) };
            saveFlow.Controls.Add(_btnQuickSave);
            quickTable.Controls.Add(new Label(), 0, 3);
            quickTable.Controls.Add(saveFlow, 1, 3);

            _pnlEmptyOrQuickAdd.Controls.Add(quickTable);
            _pnlEmptyOrQuickAdd.Controls.Add(lblEmptySub);
            _pnlEmptyOrQuickAdd.Controls.Add(lblEmptyTitle);

            this.Controls.Add(_pnlEventViewer);
            this.Controls.Add(_pnlEmptyOrQuickAdd);

            SetEvent(null);
        }

        public void SetDateContext(DateTime date)
        {
            _currentDate = date;
            _dtpQuickDate.Value = date;
        }

        public void SetEvent(CalendarEvent? ev)
        {
            _currentEvent = ev;
            if (ev == null)
            {
                _pnlEventViewer.Visible = false;
                _pnlEmptyOrQuickAdd.Visible = true;
                _pnlEmptyOrQuickAdd.BringToFront();
                _dtpQuickDate.Value = _currentDate;
            }
            else
            {
                _pnlEmptyOrQuickAdd.Visible = false;
                _pnlEventViewer.Visible = true;
                _pnlEventViewer.BringToFront();

                _lblTitle.Text = ev.Title;
                _lblTime.Text = $"{ev.StartDate:dddd, MMMM d, yyyy}  •  {ev.GetTimeRangeDisplayText(_configService.Settings.TimeFormat24Hour)}";
                _lblReminder.Text = ev.GetReminderDisplayText();
                _lblLocation.Text = string.IsNullOrWhiteSpace(ev.Location) ? "(None)" : ev.Location;

                // Lookup account name
                string accountName = "Local Calendar (Default)";
                if (!string.IsNullOrEmpty(ev.AccountId))
                {
                    var acc = _configService.GetAccounts().FirstOrDefault(a => a.Id == ev.AccountId);
                    if (acc != null)
                    {
                        accountName = $"{acc.Name} ({acc.Email})";
                    }
                }
                _lblAccount.Text = accountName;

                _lblCategory.Text = ev.Category;
                if (!string.IsNullOrEmpty(ev.ColorTag))
                {
                    try
                    {
                        _lblCategory.ForeColor = ColorTranslator.FromHtml(ev.ColorTag);
                    }
                    catch
                    {
                        _lblCategory.ForeColor = Color.FromArgb(40, 40, 40);
                    }
                }
                else
                {
                    _lblCategory.ForeColor = Color.FromArgb(40, 40, 40);
                }

                _txtDescription.Text = string.IsNullOrWhiteSpace(ev.Description) ? "No additional notes." : ev.Description;

                UpdateEventViewerLayout();
            }
        }

        public void UpdateEventViewerLayout()
        {
            if (_pnlEventViewer == null || _contentPanel == null || _pnlEventViewer.ClientSize.Width <= 0) return;

            float scale = this.DeviceDpi / 96f;
            int availW = _pnlEventViewer.ClientSize.Width;
            int availH = _pnlEventViewer.ClientSize.Height;

            _contentPanel.Width = availW;

            int headerH = _headerPanel.Height;
            int metaH = _metaTable.GetPreferredSize(new Size(availW, 0)).Height;
            int descLabelH = _lblDescHeader.Height;
            int padV = _contentPanel.Padding.Vertical;
            int fixedH = headerH + metaH + descLabelH + padV + (int)(8 * scale);

            int minDescH = (int)(95 * scale);
            int remainingForDesc = availH - fixedH;

            if (remainingForDesc >= minDescH)
            {
                _descContainer.Height = remainingForDesc;
                _contentPanel.Height = availH;
            }
            else
            {
                _descContainer.Height = minDescH;
                _contentPanel.Height = fixedH + minDescH;
            }
        }

        public void RefreshDisplay()
        {
            _dtpQuickTime.Use24HourFormat = _configService.Settings.TimeFormat24Hour;

            if (_currentEvent != null)
            {
                SetEvent(_currentEvent);
            }
        }

        private void OnQuickSaveClicked(object? sender, EventArgs e)
        {
            string title = _txtQuickTitle.Text.Trim();
            if (string.IsNullOrWhiteSpace(title))
            {
                MessageBox.Show("Please enter an event title.", "Kerkenez Calendar", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            DateTime selectedDate = _dtpQuickDate.Value.Date;
            TimeSpan selectedTime = _dtpQuickTime.SelectedTime;
            DateTime start = selectedDate.Add(selectedTime);
            DateTime end = start.AddHours(1);

            int reminderMinutes = _cboQuickReminder.SelectedIndex switch
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
                _ => 15
            };

            var newEv = new CalendarEvent
            {
                Title = title,
                StartDate = start,
                EndDate = end,
                ReminderMinutesBefore = reminderMinutes,
                Category = "General",
                ColorTag = "#0078D7"
            };

            _eventService.AddEvent(newEv);
            _txtQuickTitle.Text = "";
            SetEvent(newEv);
        }
    }
}
