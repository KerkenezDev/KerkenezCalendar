using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using KerkenezCalendar.Models;
using KerkenezCalendar.Services;
using KerkenezCalendar.UI.Controls;
using KerkenezCalendar.UI.Dialogs;
using KerkenezCalendar.UI.Tabs;

namespace KerkenezCalendar.UI
{
    public class MainForm : Form
    {
        private readonly CalendarConfigService _configService;
        private readonly CalendarEventService _eventService;

        private SidebarNav _sidebar = null!;
        private Panel _contentPanel = null!;
        private StatusStrip _statusStrip = null!;
        private ToolStripStatusLabel _lblStatus = null!;
        private ToolStripStatusLabel _lblMetrics = null!;

        // Views
        private Panel _calendarTabContainer = null!;
        private SplitContainer _mainSplit = null!;
        private SplitContainer _middleSplit = null!;
        private MonthCalendarView _monthView = null!;
        private ChosenEventView _chosenEventView = null!;
        private DayEventsView _dayEventsView = null!;

        private AgendaView _agendaView = null!;
        private AccountsView _accountsView = null!;
        private SettingsView _settingsView = null!;
        private LogsView _logsView = null!;

        public MainForm(CalendarConfigService? configService = null, CalendarEventService? eventService = null)
        {
            this.AutoScaleMode = AutoScaleMode.Dpi;

            _configService = configService ?? new CalendarConfigService();
            _eventService = eventService ?? new CalendarEventService(_configService);

            InitializeComponent();
            WireEvents();

            this.Shown += (s, e) =>
            {
                UpdateStatusMetrics();

                Log("[*] Kerkenez Calendar initialized (.NET 10 Win32)");
                Log($"[*] Configuration: Scaling {_configService.Settings.WindowWidthScale * 100:F0}% × {_configService.Settings.WindowHeightScale * 100:F0}%");

                var accounts = _configService.GetAccounts();
                Log($"[✓] Accounts: {accounts.Count} accounts loaded ({accounts.Count(a => a.IsEnabled)} active)");

                var events = _eventService.GetAllEvents();
                Log($"[✓] Calendar: {events.Count} events decrypted from events.dat");

                if (_configService.Settings.AlwaysKeepOn)
                {
                    CalendarDaemonHelper.StartDaemon();
                    Log("[✓] Background tray daemon is running");
                }
                else
                {
                    CalendarDaemonHelper.StopDaemon();
                    Log("[*] Background tray daemon is disabled");
                }
            };
        }

        private void InitializeComponent()
        {
            this.Text = "Kerkenez Calendar";
            this.Icon = CalendarIconHelper.GetApplicationIcon();

            // Screen sizing matching Kerkenez Mail philosophy
            var currentScreen = Screen.FromPoint(Cursor.Position) ?? Screen.PrimaryScreen;
            var workingArea = currentScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);

            double widthScale = _configService.Settings.WindowWidthScale > 0.1 ? _configService.Settings.WindowWidthScale : 0.65;
            double heightScale = _configService.Settings.WindowHeightScale > 0.1 ? _configService.Settings.WindowHeightScale : 0.65;

            int targetWidth = _configService.Settings.WindowWidth >= 960
                ? _configService.Settings.WindowWidth
                : (int)Math.Round(workingArea.Width * widthScale);
            int targetHeight = _configService.Settings.WindowHeight >= 540
                ? _configService.Settings.WindowHeight
                : (int)Math.Round(workingArea.Height * heightScale);

            targetWidth = Math.Clamp(targetWidth, 960, workingArea.Width);
            targetHeight = Math.Clamp(targetHeight, 540, workingArea.Height);

            this.MinimumSize = new Size(960, 540);
            this.Size = new Size(targetWidth, targetHeight);
            this.StartPosition = FormStartPosition.Manual;
            this.Location = new Point(
                workingArea.Left + Math.Max(0, (workingArea.Width - targetWidth) / 2),
                workingArea.Top + Math.Max(0, (workingArea.Height - targetHeight) / 2));
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            this.BackColor = Color.FromArgb(248, 249, 250);

            float scale = this.DeviceDpi / 96f;

            // 1. Bottom Status Strip
            _statusStrip = new StatusStrip
            {
                Dock = DockStyle.Bottom,
                BackColor = Color.FromArgb(242, 244, 247),
                Font = new Font("Segoe UI", 8.5F),
                Height = (int)(26 * scale)
            };

            _lblStatus = new ToolStripStatusLabel
            {
                Text = "Ready",
                Spring = true,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(60, 65, 75)
            };

            _lblMetrics = new ToolStripStatusLabel
            {
                Text = "Today: 0 events",
                TextAlign = ContentAlignment.MiddleRight,
                ForeColor = Color.FromArgb(90, 95, 105),
                BorderSides = ToolStripStatusLabelBorderSides.Left,
                BorderStyle = Border3DStyle.Etched
            };

            _statusStrip.Items.AddRange(new ToolStripItem[] { _lblStatus, _lblMetrics });

            // 2. Left Collapsible Sidebar
            _sidebar = new SidebarNav();
            _sidebar.IsCollapsed = _configService.Settings.CollapseSidebarByDefault;
            _sidebar.TabChanged += (s, idx) => SwitchTab(idx);

            // 3. Central Content Panel
            _contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(248, 249, 250)
            };

            // 4. Tab 0: Calendar Workspace (Month View + Chosen Event + That Day's Events)
            BuildCalendarTab(scale);

            // 5. Secondary Tabs
            _agendaView = new AgendaView(_eventService, _configService) { Dock = DockStyle.Fill, Visible = false };
            _agendaView.EventSelected += OnEventSelectedFromExternal;

            _accountsView = new AccountsView(_configService) { Dock = DockStyle.Fill, Visible = false };
            _settingsView = new SettingsView(_configService, _eventService) { Dock = DockStyle.Fill, Visible = false };
            _settingsView.SettingsSaved += () =>
            {
                _sidebar.IsCollapsed = _configService.Settings.CollapseSidebarByDefault;
                _monthView.RefreshCalendarGrid();
                Log("[*] Settings saved and applied");
            };

            _logsView = new LogsView { Dock = DockStyle.Fill, Visible = false };

            _contentPanel.Controls.Add(_calendarTabContainer);
            _contentPanel.Controls.Add(_agendaView);
            _contentPanel.Controls.Add(_accountsView);
            _contentPanel.Controls.Add(_settingsView);
            _contentPanel.Controls.Add(_logsView);

            this.Controls.Add(_contentPanel);
            this.Controls.Add(_sidebar);
            this.Controls.Add(_statusStrip);

            this.FormClosing += (s, e) =>
            {
                _configService.Settings.WindowWidth = this.Width;
                _configService.Settings.WindowHeight = this.Height;
                _configService.SaveConfig();

                if (!_configService.Settings.AlwaysKeepOn)
                {
                    CalendarDaemonHelper.StopDaemon();
                }
            };
        }

        private void BuildCalendarTab(float scale)
        {
            _calendarTabContainer = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(248, 249, 250)
            };

            // Two-column split layout matching EmailSummarizer / Kerkenez Mail:
            // MainSplit: Left/Middle = Month View (top) + Chosen Event (bottom), Right = That Day's Scheduled Events
            _mainSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                FixedPanel = FixedPanel.Panel2,
                SplitterWidth = 6,
                Padding = new Padding((int)(10 * scale))
            };

            // Left/Middle horizontal split: Top = Month View, Bottom = Chosen Event
            _middleSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterWidth = 6
            };

            _monthView = new MonthCalendarView(_eventService, _configService) { Dock = DockStyle.Fill };
            _chosenEventView = new ChosenEventView(_eventService, _configService) { Dock = DockStyle.Fill };
            _dayEventsView = new DayEventsView(_eventService, _configService) { Dock = DockStyle.Fill };

            _middleSplit.Panel1.Controls.Add(_monthView);
            _middleSplit.Panel2.Controls.Add(_chosenEventView);

            _mainSplit.Panel1.Controls.Add(_middleSplit);
            _mainSplit.Panel2.Controls.Add(_dayEventsView);

            _calendarTabContainer.Controls.Add(_mainSplit);

            // Initial Splitter Distances after layout
            this.Load += (s, e) =>
            {
                try
                {
                    int totalW = _mainSplit.Width;
                    int rightPanelW = Math.Max(280, (int)(320 * scale));
                    if (totalW > rightPanelW + 200)
                    {
                        _mainSplit.SplitterDistance = totalW - rightPanelW;
                    }

                    int middleH = _middleSplit.Height;
                    if (middleH > 300)
                    {
                        _middleSplit.SplitterDistance = (int)(middleH * 0.52); // 52% month grid, 48% chosen event
                    }

                    if (_middleSplit.Height > 320)
                    {
                        _middleSplit.Panel1MinSize = Math.Min((int)(160 * scale), _middleSplit.Height / 2);
                        _middleSplit.Panel2MinSize = Math.Min((int)(140 * scale), _middleSplit.Height / 2);
                    }
                }
                catch { }
            };
        }

        private void WireEvents()
        {
            // When day clicked in month view -> update day events list and chosen event date context
            _monthView.DateSelected += date =>
            {
                _dayEventsView.SetDate(date);
                _chosenEventView.SetDateContext(date);
                UpdateStatusMetrics();
            };

            // When double click in month view -> open add event dialog
            _monthView.CreateEventRequested += date => OpenCreateEventDialog(date);

            // When user clicks 'Add Event' in day events view -> open add event dialog
            _dayEventsView.AddEventRequested += date => OpenCreateEventDialog(date);

            // When user selects an event from that day's schedule -> show in chosen event inspector
            _dayEventsView.EventSelected += ev =>
            {
                _chosenEventView.SetEvent(ev);
            };

            // When user double clicks an event card -> edit event
            _dayEventsView.EditEventRequested += ev => OpenEditEventDialog(ev);

            // When user clicks Edit button in chosen event view
            _chosenEventView.EditRequested += ev => OpenEditEventDialog(ev);

            // When user clicks Delete button in chosen event view
            _chosenEventView.DeleteRequested += ev =>
            {
                var res = MessageBox.Show(this, $"Are you sure you want to delete '{ev.Title}'?", "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (res == DialogResult.Yes)
                {
                    _eventService.DeleteEvent(ev.Id);
                    _chosenEventView.SetEvent(null);
                    _dayEventsView.ReloadEvents();
                    _monthView.RefreshCalendarGrid();
                    UpdateStatusMetrics();
                }
            };

            // Event service updates
            _eventService.EventsChanged += () =>
            {
                if (this.IsHandleCreated)
                {
                    this.BeginInvoke(new Action(UpdateStatusMetrics));
                }
            };

            // Settings updates (instantly reload all views with new time format, first day of week, etc.)
            _settingsView.SettingsSaved += () =>
            {
                _monthView.RefreshCalendarGrid();
                _dayEventsView.ReloadEvents();
                _chosenEventView.RefreshDisplay();
                _agendaView.ReloadAgenda();
                UpdateStatusMetrics();
            };
        }

        private void OnEventSelectedFromExternal(CalendarEvent ev)
        {
            _sidebar.SelectedIndex = 0; // Switch to Calendar tab
            _monthView.SelectDate(ev.StartDate);
            _dayEventsView.SelectEvent(ev);
            _chosenEventView.SetEvent(ev);
        }

        private void OpenCreateEventDialog(DateTime date)
        {
            using var dlg = new EventEditDialog(_configService, null, date);
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                _eventService.AddEvent(dlg.ResultEvent);
                _monthView.RefreshCalendarGrid();
                _dayEventsView.ReloadEvents();
                _chosenEventView.SetEvent(dlg.ResultEvent);
                UpdateStatusMetrics();
            }
        }

        private void OpenEditEventDialog(CalendarEvent ev)
        {
            using var dlg = new EventEditDialog(_configService, ev);
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                _eventService.UpdateEvent(dlg.ResultEvent);
                _monthView.RefreshCalendarGrid();
                _dayEventsView.ReloadEvents();
                _chosenEventView.SetEvent(dlg.ResultEvent);
                UpdateStatusMetrics();
            }
        }

        private void SwitchTab(int index)
        {
            _calendarTabContainer.Visible = (index == 0);
            _agendaView.Visible = (index == 1);
            _accountsView.Visible = (index == 2);
            _settingsView.Visible = (index == 3);
            _logsView.Visible = (index == 4);

            if (index == 1) _agendaView.ReloadAgenda();
            if (index == 2) _accountsView.LoadAccounts();
        }

        public void Log(string message)
        {
            _logsView?.AppendLog(message);
        }

        private void UpdateStatusStrip(string status, string metrics)
        {
            _lblStatus.Text = status;
            _lblMetrics.Text = metrics;
        }

        private void UpdateStatusMetrics()
        {
            int countToday = _eventService.GetEventsForDate(DateTime.Today).Count;
            var next = _eventService.GetNextUpcomingReminder(DateTime.Now);

            string metrics = next != null
                ? $"Next reminder: {next.Title} ({next.GetTimeRangeDisplayText(_configService.Settings.TimeFormat24Hour)})"
                : $"Today: {countToday} event{(countToday == 1 ? "" : "s")}";

            UpdateStatusStrip("Ready", metrics);
        }

        public void ApplyConfiguredLayout(bool resetPosition = false)
        {
            var currentScreen = Screen.FromPoint(Cursor.Position) ?? Screen.PrimaryScreen;
            var workingArea = currentScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);

            double widthScale = _configService.Settings.WindowWidthScale > 0.1 ? _configService.Settings.WindowWidthScale : 0.60;
            double heightScale = _configService.Settings.WindowHeightScale > 0.1 ? _configService.Settings.WindowHeightScale : 0.56;

            int targetWidth = (int)Math.Round(workingArea.Width * widthScale);
            int targetHeight = (int)Math.Round(workingArea.Height * heightScale);

            targetWidth = Math.Clamp(targetWidth, 960, workingArea.Width);
            targetHeight = Math.Clamp(targetHeight, 540, workingArea.Height);

            this.Size = new Size(targetWidth, targetHeight);
            if (resetPosition)
            {
                this.Location = new Point(
                    workingArea.Left + Math.Max(0, (workingArea.Width - targetWidth) / 2),
                    workingArea.Top + Math.Max(0, (workingArea.Height - targetHeight) / 2));
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x04C8) // Custom message from daemon to re-apply layout
            {
                _configService.LoadConfig();
                ApplyConfiguredLayout(true);
                _sidebar.IsCollapsed = _configService.Settings.CollapseSidebarByDefault;
                return;
            }
            base.WndProc(ref m);
        }

    }
}
