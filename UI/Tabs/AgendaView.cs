using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using KerkenezCalendar.Models;
using KerkenezCalendar.Services;

namespace KerkenezCalendar.UI.Tabs
{
    public class AgendaView : UserControl
    {
        private readonly CalendarEventService _eventService;
        private readonly CalendarConfigService _configService;

        private TextBox _txtSearch = null!;
        private ListView _lvAgenda = null!;

        public event Action<CalendarEvent>? EventSelected;

        public AgendaView(CalendarEventService eventService, CalendarConfigService configService)
        {
            _eventService = eventService;
            _configService = configService;

            InitializeComponent();
            ReloadAgenda();

            _eventService.EventsChanged += () =>
            {
                if (this.IsHandleCreated)
                {
                    this.BeginInvoke(new Action(ReloadAgenda));
                }
            };
        }

        private void InitializeComponent()
        {
            this.Dock = DockStyle.Fill;
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            this.BackColor = Color.FromArgb(248, 249, 250);

            float scale = this.DeviceDpi / 96f;

            var topPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = (int)(50 * scale),
                Padding = new Padding((int)(14 * scale), (int)(10 * scale), (int)(14 * scale), (int)(10 * scale)),
                BackColor = Color.White
            };

            topPanel.Paint += (s, e) =>
            {
                using var p = new Pen(Color.FromArgb(226, 230, 234), 1);
                e.Graphics.DrawLine(p, 0, topPanel.Height - 1, topPanel.Width, topPanel.Height - 1);
            };

            var lblTitle = new Label
            {
                Text = "Upcoming Agenda",
                Dock = DockStyle.Left,
                AutoSize = true,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 30, 30),
                TextAlign = ContentAlignment.MiddleLeft
            };

            _txtSearch = new TextBox
            {
                Dock = DockStyle.Right,
                Width = (int)(220 * scale),
                PlaceholderText = "Search agenda..."
            };
            _txtSearch.TextChanged += (s, e) => ReloadAgenda();

            topPanel.Controls.Add(lblTitle);
            topPanel.Controls.Add(_txtSearch);

            _lvAgenda = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 9.25F, FontStyle.Regular),
                BackColor = Color.White
            };

            _lvAgenda.Columns.Add("Date", (int)(120 * scale));
            _lvAgenda.Columns.Add("Time", (int)(115 * scale));
            _lvAgenda.Columns.Add("Title", (int)(220 * scale));
            _lvAgenda.Columns.Add("Account", (int)(160 * scale));
            _lvAgenda.Columns.Add("Reminder", (int)(120 * scale));
            _lvAgenda.Columns.Add("Category", (int)(90 * scale));
            _lvAgenda.Columns.Add("Location", (int)(140 * scale));

            _lvAgenda.DoubleClick += (s, e) =>
            {
                if (_lvAgenda.SelectedItems.Count > 0 && _lvAgenda.SelectedItems[0].Tag is CalendarEvent ev)
                {
                    EventSelected?.Invoke(ev);
                }
            };

            this.Controls.Add(_lvAgenda);
            this.Controls.Add(topPanel);
        }

        public void ReloadAgenda()
        {
            _lvAgenda.BeginUpdate();
            _lvAgenda.Items.Clear();

            string filter = _txtSearch?.Text.Trim() ?? "";
            var events = _eventService.GetAllEvents();

            var accounts = _configService.GetAccounts();
            var accountDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var a in accounts)
            {
                if (!string.IsNullOrEmpty(a.Id))
                {
                    string label = string.IsNullOrWhiteSpace(a.Name)
                        ? a.Email
                        : (!string.IsNullOrWhiteSpace(a.Email) ? $"{a.Name} ({a.Email})" : a.Name);
                    accountDict[a.Id] = label;
                }
            }

            if (!string.IsNullOrEmpty(filter))
            {
                events = events.Where(e => e.Title.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                                           e.Description.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                                           e.Location.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                                           e.Category.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                                           (!string.IsNullOrEmpty(e.AccountId) && accountDict.TryGetValue(e.AccountId, out var an) && an.Contains(filter, StringComparison.OrdinalIgnoreCase)) ||
                                           (string.IsNullOrEmpty(e.AccountId) && "Local Calendar".Contains(filter, StringComparison.OrdinalIgnoreCase))).ToList();
            }

            foreach (var ev in events)
            {
                string accountText = "Local Calendar";
                if (!string.IsNullOrEmpty(ev.AccountId) && accountDict.TryGetValue(ev.AccountId, out var accName))
                {
                    accountText = accName;
                }

                var item = new ListViewItem(ev.StartDate.ToString("yyyy-MM-dd (ddd)"));
                item.SubItems.Add(ev.GetTimeRangeDisplayText(_configService.Settings.TimeFormat24Hour));
                item.SubItems.Add(ev.Title);
                item.SubItems.Add(accountText);
                item.SubItems.Add(ev.GetReminderDisplayText());
                item.SubItems.Add(ev.Category);
                item.SubItems.Add(ev.Location);
                item.Tag = ev;

                if (ev.StartDate.Date == DateTime.Today)
                {
                    item.Font = new Font(_lvAgenda.Font, FontStyle.Bold);
                    item.ForeColor = Color.FromArgb(0, 102, 204);
                }
                else if (ev.StartDate.Date < DateTime.Today)
                {
                    item.ForeColor = Color.FromArgb(140, 145, 150);
                }

                _lvAgenda.Items.Add(item);
            }

            _lvAgenda.EndUpdate();
        }
    }
}
