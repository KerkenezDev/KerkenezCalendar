using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using KerkenezCalendar.Models;
using KerkenezCalendar.Services;

namespace KerkenezCalendar.UI.Controls
{
    public class DayEventsView : UserControl
    {
        private readonly CalendarEventService _eventService;
        private readonly CalendarConfigService _configService;

        private DateTime _currentDate = DateTime.Today;
        private CalendarEvent? _selectedEvent;
        private List<CalendarEvent> _dayEvents = new List<CalendarEvent>();

        public event Action<CalendarEvent>? EventSelected;
        public event Action<DateTime>? AddEventRequested;
        public event Action<CalendarEvent>? EditEventRequested;
        public event Action<CalendarEvent>? DeleteEventRequested;

        private Panel _headerPanel = null!;
        private Label _lblDateTitle = null!;
        private Label _lblCount = null!;
        private Button _btnAddEvent = null!;
        private FlowLayoutPanel _pnlEventsList = null!;
        private Panel _pnlEmptyState = null!;

        public CalendarEvent? SelectedEvent => _selectedEvent;

        public DayEventsView(CalendarEventService eventService, CalendarConfigService configService)
        {
            _eventService = eventService;
            _configService = configService;

            InitializeComponent();

            _eventService.EventsChanged += () =>
            {
                if (this.IsHandleCreated)
                {
                    this.BeginInvoke(new Action(ReloadEvents));
                }
            };
        }

        private void InitializeComponent()
        {
            this.Dock = DockStyle.Fill;
            this.BackColor = Color.FromArgb(248, 249, 250);
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular);

            float scale = this.DeviceDpi / 96f;

            // 1. Header Toolbar
            int headerH = (int)(52 * scale);
            _headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = headerH,
                Padding = new Padding((int)(14 * scale), (int)(8 * scale), (int)(14 * scale), (int)(8 * scale)),
                BackColor = Color.White
            };

            _headerPanel.Paint += (s, e) =>
            {
                using var p = new Pen(Color.FromArgb(226, 230, 234), 1);
                e.Graphics.DrawLine(p, 0, _headerPanel.Height - 1, _headerPanel.Width, _headerPanel.Height - 1);
            };

            var titlesFlow = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent
            };

            _lblDateTitle = new Label
            {
                Text = _currentDate.ToString("dddd, MMM d"),
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 30, 30),
                Dock = DockStyle.Top,
                AutoSize = true
            };

            _lblCount = new Label
            {
                Text = "0 events scheduled",
                Font = new Font("Segoe UI", 8F, FontStyle.Regular),
                ForeColor = Color.FromArgb(100, 106, 115),
                Dock = DockStyle.Bottom,
                AutoSize = true
            };

            titlesFlow.Controls.Add(_lblCount);
            titlesFlow.Controls.Add(_lblDateTitle);

            _btnAddEvent = new Button
            {
                Text = "➕ Add Event",
                Dock = DockStyle.Right,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding((int)(10 * scale), (int)(4 * scale), (int)(10 * scale), (int)(4 * scale)),
                FlatStyle = FlatStyle.System,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnAddEvent.Click += (s, e) => AddEventRequested?.Invoke(_currentDate);

            _headerPanel.Controls.Add(titlesFlow);
            _headerPanel.Controls.Add(_btnAddEvent);

            // 2. Events List Container
            _pnlEventsList = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Padding = new Padding((int)(10 * scale)),
                BackColor = Color.FromArgb(248, 249, 250)
            };

            // Empty state container
            _pnlEmptyState = new Panel
            {
                Dock = DockStyle.Fill,
                Visible = false,
                BackColor = Color.FromArgb(248, 249, 250)
            };

            var lblEmptyText = new Label
            {
                Text = "No events scheduled for this day\r\nClick 'Add Event' or double click the calendar to create one.",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
                ForeColor = Color.FromArgb(120, 125, 133),
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Fill
            };
            _pnlEmptyState.Controls.Add(lblEmptyText);

            this.Controls.Add(_pnlEventsList);
            this.Controls.Add(_pnlEmptyState);
            this.Controls.Add(_headerPanel);

            ReloadEvents();
        }

        public void SetDate(DateTime date)
        {
            _currentDate = date;
            _lblDateTitle.Text = _currentDate.ToString("dddd, MMM d, yyyy");
            ReloadEvents();
        }

        public void ReloadEvents()
        {
            _pnlEventsList.SuspendLayout();
            _pnlEventsList.Controls.Clear();

            _dayEvents = _eventService.GetEventsForDate(_currentDate);

            int count = _dayEvents.Count;
            _lblCount.Text = count == 1 ? "1 event scheduled" : $"{count} events scheduled";

            if (count == 0)
            {
                _pnlEventsList.Visible = false;
                _pnlEmptyState.Visible = true;
                _pnlEmptyState.BringToFront();
            }
            else
            {
                _pnlEmptyState.Visible = false;
                _pnlEventsList.Visible = true;
                _pnlEventsList.BringToFront();

                float scale = this.DeviceDpi / 96f;
                int cardW = Math.Max(220, _pnlEventsList.ClientSize.Width - (int)(24 * scale));

                foreach (var ev in _dayEvents)
                {
                    var card = CreateEventCard(ev, cardW, scale);
                    _pnlEventsList.Controls.Add(card);
                }

                // If currently selected event is not in list, auto-select first
                if (_selectedEvent == null || !_dayEvents.Any(e => e.Id == _selectedEvent.Id))
                {
                    _selectedEvent = _dayEvents.FirstOrDefault();
                    if (_selectedEvent != null)
                    {
                        EventSelected?.Invoke(_selectedEvent);
                    }
                }
            }

            _pnlEventsList.ResumeLayout(true);
        }

        private Control CreateEventCard(CalendarEvent ev, int cardWidth, float scale)
        {
            bool isSelected = (_selectedEvent != null && _selectedEvent.Id == ev.Id);

            var card = new Panel
            {
                Width = cardWidth,
                Height = (int)(72 * scale),
                Margin = new Padding(0, 0, 0, (int)(8 * scale)),
                BackColor = isSelected ? Color.FromArgb(238, 245, 254) : Color.White,
                Cursor = Cursors.Hand,
                Padding = new Padding((int)(12 * scale), (int)(8 * scale), (int)(10 * scale), (int)(8 * scale))
            };

            // Custom border and category accent bar paint
            card.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                Color barColor = Color.FromArgb(0, 120, 215);
                try
                {
                    if (!string.IsNullOrEmpty(ev.ColorTag))
                    {
                        barColor = ColorTranslator.FromHtml(ev.ColorTag);
                    }
                }
                catch { }

                // Outer border
                Color borderColor = isSelected ? Color.FromArgb(0, 120, 215) : Color.FromArgb(226, 230, 235);
                using var borderPen = new Pen(borderColor, isSelected ? 2 : 1);
                g.DrawRoundedRectangle(borderPen, new Rectangle(0, 0, card.Width - 1, card.Height - 1), 5);

                // Left category color strip
                using var barBrush = new SolidBrush(barColor);
                g.FillRoundedRectangle(barBrush, new Rectangle(1, 1, 4, card.Height - 2), 2);
            };

            // Time badge
            var lblTime = new Label
            {
                Text = ev.GetTimeRangeDisplayText(_configService.Settings.TimeFormat24Hour),
                Font = new Font("Segoe UI", 8.25F, FontStyle.Bold),
                ForeColor = isSelected ? Color.FromArgb(0, 102, 204) : Color.FromArgb(80, 85, 92),
                AutoSize = true,
                Location = new Point((int)(12 * scale), (int)(8 * scale))
            };

            // Title
            var lblTitle = new Label
            {
                Text = ev.Title,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 30, 30),
                AutoSize = false,
                Width = cardWidth - (int)(30 * scale),
                Height = (int)(20 * scale),
                Location = new Point((int)(12 * scale), (int)(28 * scale)),
                AutoEllipsis = true
            };

            // Sub-info (Reminder & Location)
            string reminderText = ev.ReminderMinutesBefore >= 0 ? $"🔔 {ev.GetReminderDisplayText()}" : "";
            string locText = !string.IsNullOrWhiteSpace(ev.Location) ? $"📍 {ev.Location}" : "";
            string infoCombined = string.Join("  •  ", new[] { reminderText, locText }.Where(s => !string.IsNullOrEmpty(s)));

            var lblInfo = new Label
            {
                Text = infoCombined,
                Font = new Font("Segoe UI", 8F, FontStyle.Regular),
                ForeColor = Color.FromArgb(120, 125, 133),
                AutoSize = true,
                Location = new Point((int)(12 * scale), (int)(50 * scale))
            };

            card.Controls.Add(lblTime);
            card.Controls.Add(lblTitle);
            card.Controls.Add(lblInfo);

            var cardMenu = new ContextMenuStrip();
            var itemHeader = new ToolStripMenuItem($"Event: {ev.Title}") { Enabled = false, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            var itemEdit = new ToolStripMenuItem("✏️ Edit Event", null, (s, e) => EditEventRequested?.Invoke(ev));
            var itemDelete = new ToolStripMenuItem("🗑️ Delete Event", null, (s, e) => DeleteEventRequested?.Invoke(ev));
            cardMenu.Items.Add(itemHeader);
            cardMenu.Items.Add(new ToolStripSeparator());
            cardMenu.Items.Add(itemEdit);
            cardMenu.Items.Add(itemDelete);

            card.ContextMenuStrip = cardMenu;
            lblTime.ContextMenuStrip = cardMenu;
            lblTitle.ContextMenuStrip = cardMenu;
            lblInfo.ContextMenuStrip = cardMenu;

            void OnCardClicked(object? sender, EventArgs e)
            {
                _selectedEvent = ev;
                ReloadEvents(); // Refresh card selections
                EventSelected?.Invoke(ev);
            }

            void OnCardDoubleClicked(object? sender, EventArgs e)
            {
                _selectedEvent = ev;
                EditEventRequested?.Invoke(ev);
            }

            card.Click += OnCardClicked;
            lblTime.Click += OnCardClicked;
            lblTitle.Click += OnCardClicked;
            lblInfo.Click += OnCardClicked;

            card.DoubleClick += OnCardDoubleClicked;
            lblTitle.DoubleClick += OnCardDoubleClicked;

            return card;
        }

        public void SelectEvent(CalendarEvent ev)
        {
            _selectedEvent = ev;
            ReloadEvents();
        }
    }
}
