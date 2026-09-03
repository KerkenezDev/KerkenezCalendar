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
    public class MonthCalendarView : UserControl
    {
        private readonly CalendarEventService _eventService;
        private readonly CalendarConfigService _configService;

        private DateTime _currentMonth;
        private DateTime _selectedDate;
        private int _hoveredDayIndex = -1;

        public event Action<DateTime>? DateSelected;
        public event Action<DateTime>? CreateEventRequested;
        public event Action? SyncAccountsRequested;
        public event Action<CalendarEvent>? EditEventRequested;
        public event Action<CalendarEvent>? DeleteEventRequested;

        private Panel _topBar = null!;
        private Label _lblMonthYear = null!;
        private Button _btnSync = null!;
        private Button _btnToday = null!;
        private Button _btnPrev = null!;
        private Button _btnNext = null!;
        private Panel _gridPanel = null!;

        private readonly List<DayCellInfo> _dayCells = new List<DayCellInfo>();

        private class DayCellInfo
        {
            public DateTime Date { get; set; }
            public bool IsCurrentMonth { get; set; }
            public bool IsToday { get; set; }
            public bool IsSelected { get; set; }
            public Rectangle Bounds { get; set; }
            public List<CalendarEvent> Events { get; set; } = new List<CalendarEvent>();
        }

        public DateTime SelectedDate => _selectedDate;

        public MonthCalendarView(CalendarEventService eventService, CalendarConfigService configService)
        {
            _eventService = eventService;
            _configService = configService;

            _currentMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            _selectedDate = DateTime.Today;

            InitializeComponent();

            _eventService.EventsChanged += () =>
            {
                if (this.IsHandleCreated)
                {
                    this.BeginInvoke(new Action(RefreshCalendarGrid));
                }
            };
        }

        private void InitializeComponent()
        {
            this.Dock = DockStyle.Fill;
            this.BackColor = Color.White;
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular);

            float scale = this.DeviceDpi / 96f;

            // 1. Month Header Toolbar
            int headerH = (int)(46 * scale);
            _topBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = headerH,
                Padding = new Padding((int)(14 * scale), (int)(6 * scale), (int)(14 * scale), (int)(6 * scale)),
                BackColor = Color.White
            };

            _topBar.Paint += (s, e) =>
            {
                using var p = new Pen(Color.FromArgb(226, 230, 234), 1);
                e.Graphics.DrawLine(p, 0, _topBar.Height - 1, _topBar.Width, _topBar.Height - 1);
            };

            _lblMonthYear = new Label
            {
                Text = _currentMonth.ToString("MMMM yyyy"),
                Font = new Font("Segoe UI", 13F, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 30, 30),
                AutoSize = true,
                Dock = DockStyle.Left,
                TextAlign = ContentAlignment.MiddleLeft
            };

            var navFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = true,
                Margin = new Padding(0)
            };

            string refreshGlyph = "\uE72C";
            Font refreshFont;
            try
            {
                if (FontFamily.Families.Any(f => f.Name.Equals("Segoe MDL2 Assets", StringComparison.OrdinalIgnoreCase)))
                {
                    refreshFont = new Font("Segoe MDL2 Assets", 9.5F, FontStyle.Regular);
                }
                else
                {
                    refreshGlyph = "⟳";
                    refreshFont = new Font("Segoe UI", 11F, FontStyle.Bold);
                }
            }
            catch
            {
                refreshGlyph = "⟳";
                refreshFont = new Font("Segoe UI", 11F, FontStyle.Bold);
            }

            _btnSync = new Button
            {
                Text = refreshGlyph,
                Font = refreshFont,
                Width = (int)(32 * scale),
                Height = (int)(30 * scale),
                Margin = new Padding(0, 0, (int)(6 * scale), 0),
                FlatStyle = FlatStyle.System,
                Cursor = Cursors.Hand
            };
            var toolTip = new ToolTip();
            toolTip.SetToolTip(_btnSync, "Sync with accounts");
            _btnSync.Click += (s, e) => SyncAccountsRequested?.Invoke();

            _btnToday = new Button
            {
                Text = "Today",
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding((int)(10 * scale), (int)(4 * scale), (int)(10 * scale), (int)(4 * scale)),
                Margin = new Padding(0, 0, (int)(8 * scale), 0),
                FlatStyle = FlatStyle.System,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                Cursor = Cursors.Hand
            };
            _btnToday.Click += (s, e) =>
            {
                _currentMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                _selectedDate = DateTime.Today;
                RefreshCalendarGrid();
                DateSelected?.Invoke(_selectedDate);
            };

            _btnPrev = new Button
            {
                Text = "◀",
                Width = (int)(32 * scale),
                Height = (int)(30 * scale),
                Margin = new Padding(0, 0, (int)(4 * scale), 0),
                FlatStyle = FlatStyle.System,
                Cursor = Cursors.Hand
            };
            _btnPrev.Click += (s, e) =>
            {
                _currentMonth = _currentMonth.AddMonths(-1);
                RefreshCalendarGrid();
            };

            _btnNext = new Button
            {
                Text = "▶",
                Width = (int)(32 * scale),
                Height = (int)(30 * scale),
                Margin = new Padding(0),
                FlatStyle = FlatStyle.System,
                Cursor = Cursors.Hand
            };
            _btnNext.Click += (s, e) =>
            {
                _currentMonth = _currentMonth.AddMonths(1);
                RefreshCalendarGrid();
            };

            navFlow.Controls.Add(_btnSync);
            navFlow.Controls.Add(_btnToday);
            navFlow.Controls.Add(_btnPrev);
            navFlow.Controls.Add(_btnNext);

            _topBar.Controls.Add(navFlow);
            _topBar.Controls.Add(_lblMonthYear);

            // 2. Grid Panel for Days
            _gridPanel = new DoubleBufferedPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White
            };

            _gridPanel.Paint += OnGridPanelPaint;
            _gridPanel.Resize += (s, e) => RefreshCellBounds();
            _gridPanel.MouseMove += OnGridMouseMove;
            _gridPanel.MouseLeave += OnGridMouseLeave;
            _gridPanel.MouseClick += OnGridMouseClick;
            _gridPanel.MouseDoubleClick += OnGridMouseDoubleClick;

            this.Controls.Add(_gridPanel);
            this.Controls.Add(_topBar);

            RefreshCalendarGrid();
        }

        public void SelectDate(DateTime date)
        {
            _selectedDate = date;
            if (date.Year != _currentMonth.Year || date.Month != _currentMonth.Month)
            {
                _currentMonth = new DateTime(date.Year, date.Month, 1);
            }
            RefreshCalendarGrid();
            DateSelected?.Invoke(_selectedDate);
        }

        public void RefreshCalendarGrid()
        {
            _lblMonthYear.Text = _currentMonth.ToString("MMMM yyyy");
            _dayCells.Clear();

            DateTime firstDayOfMonth = new DateTime(_currentMonth.Year, _currentMonth.Month, 1);
            DayOfWeek startOfWeek = _configService.Settings.StartOfWeek;

            int offset = ((int)firstDayOfMonth.DayOfWeek - (int)startOfWeek + 7) % 7;
            DateTime gridStart = firstDayOfMonth.AddDays(-offset);

            var monthEvents = _eventService.GetAllEvents();

            for (int i = 0; i < 42; i++) // 6 weeks * 7 days
            {
                DateTime day = gridStart.AddDays(i);
                var cellEvents = monthEvents
                    .Where(e => e.StartDate.Date == day.Date || (e.StartDate.Date <= day.Date && e.EndDate.Date >= day.Date))
                    .OrderBy(e => !e.IsAllDay)
                    .ThenBy(e => e.StartDate)
                    .ToList();

                _dayCells.Add(new DayCellInfo
                {
                    Date = day,
                    IsCurrentMonth = (day.Month == _currentMonth.Month),
                    IsToday = (day.Date == DateTime.Today),
                    IsSelected = (day.Date == _selectedDate.Date),
                    Events = cellEvents
                });
            }

            RefreshCellBounds();
            _gridPanel.Invalidate();
        }

        private void RefreshCellBounds()
        {
            if (_dayCells.Count == 0 || _gridPanel.Width <= 0 || _gridPanel.Height <= 0) return;

            float scale = this.DeviceDpi / 96f;
            int headerH = (int)(24 * scale); // Day of week header row height
            int availableH = Math.Max(0, _gridPanel.Height - headerH);

            int totalW = _gridPanel.Width;
            int totalH = _gridPanel.Height;

            for (int row = 0; row < 6; row++)
            {
                int y = headerH + (row * availableH / 6);
                int nextY = headerH + ((row + 1) * availableH / 6);
                int h = (row == 5) ? (totalH - y) : (nextY - y);

                for (int col = 0; col < 7; col++)
                {
                    int index = (row * 7) + col;
                    if (index < _dayCells.Count)
                    {
                        int x = col * totalW / 7;
                        int nextX = (col + 1) * totalW / 7;
                        int w = (col == 6) ? (totalW - x) : (nextX - x);

                        _dayCells[index].Bounds = new Rectangle(x, y, w, h);
                    }
                }
            }
        }

        private void OnGridPanelPaint(object? sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            float scale = this.DeviceDpi / 96f;
            int headerH = (int)(24 * scale);
            int totalW = _gridPanel.Width;

            // 1. Draw Day of Week Header Row
            DayOfWeek startOfWeek = _configService.Settings.StartOfWeek;
            using (var headerBg = new SolidBrush(Color.FromArgb(248, 249, 250)))
            using (var headerTextBrush = new SolidBrush(Color.FromArgb(100, 106, 115)))
            using (var headerFont = new Font("Segoe UI", (totalW < 400 ? 7.5F : 8.5F), FontStyle.Bold))
            using (var borderPen = new Pen(Color.FromArgb(226, 230, 234)))
            {
                g.FillRectangle(headerBg, 0, 0, totalW, headerH);
                g.DrawLine(borderPen, 0, headerH - 1, totalW, headerH - 1);

                for (int col = 0; col < 7; col++)
                {
                    DayOfWeek dow = (DayOfWeek)(((int)startOfWeek + col) % 7);
                    string dayName = totalW < 400
                        ? dow.ToString().Substring(0, 1).ToUpperInvariant()
                        : dow.ToString().Substring(0, 3).ToUpperInvariant();

                    int x = col * totalW / 7;
                    int nextX = (col + 1) * totalW / 7;
                    int w = (col == 6) ? (totalW - x) : (nextX - x);
                    var rect = new Rectangle(x, 0, w, headerH);
                    var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString(dayName, headerFont, headerTextBrush, rect, sf);
                }
            }

            // 2. Draw Day Cells
            using var gridPen = new Pen(Color.FromArgb(235, 238, 242), 1);
            using var dayNumFont = new Font("Segoe UI", 9F, FontStyle.Regular);
            using var dayNumBoldFont = new Font("Segoe UI", 9F, FontStyle.Bold);
            using var dayNumSmallFont = new Font("Segoe UI", 7.5F, FontStyle.Bold);
            using var eventFont = new Font("Segoe UI", 7.5F, FontStyle.Regular);

            for (int i = 0; i < _dayCells.Count; i++)
            {
                var cell = _dayCells[i];
                var b = cell.Bounds;
                if (b.Width <= 0 || b.Height <= 0) continue;

                // Cell background
                if (cell.IsSelected)
                {
                    using var selBrush = new SolidBrush(Color.FromArgb(232, 242, 252));
                    g.FillRectangle(selBrush, b);
                }
                else if (i == _hoveredDayIndex)
                {
                    using var hovBrush = new SolidBrush(Color.FromArgb(244, 247, 250));
                    g.FillRectangle(hovBrush, b);
                }
                else if (!cell.IsCurrentMonth)
                {
                    using var nonCurrentBrush = new SolidBrush(Color.FromArgb(250, 251, 252));
                    g.FillRectangle(nonCurrentBrush, b);
                }
                else
                {
                    g.FillRectangle(Brushes.White, b);
                }

                // Cell grid borders
                g.DrawRectangle(gridPen, b);

                // Selected border highlight
                if (cell.IsSelected)
                {
                    using var selPen = new Pen(Color.FromArgb(0, 120, 215), 2);
                    g.DrawRectangle(selPen, b.Left + 1, b.Top + 1, b.Width - 2, b.Height - 2);
                }

                // Draw Day Number (responsive sizing)
                bool isCompact = (b.Height < (int)(52 * scale) || b.Width < (int)(42 * scale));
                int badgeSize = isCompact
                    ? Math.Min((int)(18 * scale), Math.Min(b.Width - 4, b.Height - 4))
                    : (int)(22 * scale);
                badgeSize = Math.Max(badgeSize, 12);

                var numRect = new Rectangle(b.Left + (int)(3 * scale), b.Top + (int)(3 * scale), badgeSize, badgeSize);
                var curDayFont = (badgeSize < (int)(20 * scale)) ? dayNumSmallFont : (cell.IsSelected || cell.IsToday ? dayNumBoldFont : dayNumFont);

                if (cell.IsToday)
                {
                    // Circle accent for Today
                    using var todayBrush = new SolidBrush(Color.FromArgb(0, 120, 215));
                    g.FillEllipse(todayBrush, numRect);

                    using var todayTextBrush = new SolidBrush(Color.White);
                    var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString(cell.Date.Day.ToString(), curDayFont, todayTextBrush, numRect, sf);
                }
                else
                {
                    Color textColor = cell.IsCurrentMonth ? Color.FromArgb(40, 40, 40) : Color.FromArgb(170, 175, 180);
                    using var textBrush = new SolidBrush(textColor);
                    var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString(cell.Date.Day.ToString(), curDayFont, textBrush, numRect, sf);
                }

                // Event drawing: Full chips when space permits, compact indicator dots when space is tight
                int availH = b.Height - badgeSize - (int)(10 * scale);
                int eventH = (int)(16 * scale);
                int maxEventsShown = (availH >= eventH) ? (availH / eventH) : 0;

                if (maxEventsShown <= 0)
                {
                    // Compact Dot Indicators (up to 4 dots)
                    if (cell.Events.Count > 0 && b.Height >= (int)(24 * scale))
                    {
                        int dotSize = Math.Max(3, (int)(4 * scale));
                        int dotCount = Math.Min(cell.Events.Count, 4);
                        int spacing = (int)(3 * scale);
                        int totalDotsW = dotCount * dotSize + (dotCount - 1) * spacing;
                        int dotX = b.Left + (b.Width - totalDotsW) / 2;
                        int dotY = b.Bottom - dotSize - (int)(4 * scale);

                        for (int d = 0; d < dotCount; d++)
                        {
                            var ev = cell.Events[d];
                            Color c = Color.FromArgb(0, 120, 215);
                            if (!string.IsNullOrEmpty(ev.ColorTag))
                            {
                                try { c = ColorTranslator.FromHtml(ev.ColorTag); } catch { }
                            }
                            using var dotBrush = new SolidBrush(c);
                            g.FillEllipse(dotBrush, dotX + d * (dotSize + spacing), dotY, dotSize, dotSize);
                        }
                    }
                }
                else
                {
                    // Full Event Chips
                    int eventY = b.Top + badgeSize + (int)(5 * scale);
                    int shownCount = Math.Min(cell.Events.Count, maxEventsShown);

                    for (int eIdx = 0; eIdx < shownCount; eIdx++)
                    {
                        if (eIdx == maxEventsShown - 1 && cell.Events.Count > maxEventsShown)
                        {
                            // More indicator
                            int remaining = cell.Events.Count - eIdx;
                            var moreRect = new Rectangle(b.Left + (int)(4 * scale), eventY, b.Width - (int)(8 * scale), eventH - 2);
                            using var moreBrush = new SolidBrush(Color.FromArgb(110, 115, 122));
                            var sfMore = new StringFormat
                            {
                                Alignment = StringAlignment.Near,
                                LineAlignment = StringAlignment.Center,
                                Trimming = StringTrimming.EllipsisCharacter,
                                FormatFlags = StringFormatFlags.NoWrap
                            };
                            g.DrawString($"+{remaining} more", eventFont, moreBrush, moreRect, sfMore);
                            break;
                        }

                        var ev = cell.Events[eIdx];
                        var evRect = new Rectangle(b.Left + (int)(4 * scale), eventY, b.Width - (int)(8 * scale), eventH - 2);

                        // Parse color tag
                        Color chipBg = Color.FromArgb(235, 240, 248);
                        Color chipBorder = Color.FromArgb(0, 120, 215);
                        try
                        {
                            if (!string.IsNullOrEmpty(ev.ColorTag))
                            {
                                var c = ColorTranslator.FromHtml(ev.ColorTag);
                                chipBorder = c;
                                chipBg = Color.FromArgb(30, c.R, c.G, c.B);
                            }
                        }
                        catch { }

                        using var chipBgBrush = new SolidBrush(chipBg);
                        g.FillRoundedRectangle(chipBgBrush, evRect, 3);

                        // Left color bar
                        using var barBrush = new SolidBrush(chipBorder);
                        g.FillRoundedRectangle(barBrush, new Rectangle(evRect.Left, evRect.Top, 3, evRect.Height), 1);

                        // Event title text
                        bool use24 = _configService.Settings.TimeFormat24Hour;
                        string timePrefix = use24 ? $"{ev.StartDate:HH:mm}" : $"{ev.StartDate:h:mm tt}";
                        string titleText = ev.IsAllDay ? ev.Title : $"{timePrefix} {ev.Title}";
                        var textRect = new Rectangle(evRect.Left + 5, evRect.Top, evRect.Width - 6, evRect.Height);
                        using var titleBrush = new SolidBrush(Color.FromArgb(30, 30, 30));
                        var sf = new StringFormat
                        {
                            Alignment = StringAlignment.Near,
                            LineAlignment = StringAlignment.Center,
                            Trimming = StringTrimming.EllipsisCharacter,
                            FormatFlags = StringFormatFlags.NoWrap
                        };
                        g.DrawString(titleText, eventFont, titleBrush, textRect, sf);

                        eventY += eventH;
                    }
                }
            }
        }

        private void OnGridMouseMove(object? sender, MouseEventArgs e)
        {
            int newHover = -1;
            for (int i = 0; i < _dayCells.Count; i++)
            {
                if (_dayCells[i].Bounds.Contains(e.Location))
                {
                    newHover = i;
                    break;
                }
            }

            if (newHover != _hoveredDayIndex)
            {
                _hoveredDayIndex = newHover;
                _gridPanel.Invalidate();
            }
        }

        private void OnGridMouseLeave(object? sender, EventArgs e)
        {
            _hoveredDayIndex = -1;
            _gridPanel.Invalidate();
        }

        private void OnGridMouseClick(object? sender, MouseEventArgs e)
        {
            for (int i = 0; i < _dayCells.Count; i++)
            {
                var cell = _dayCells[i];
                if (cell.Bounds.Contains(e.Location))
                {
                    _selectedDate = cell.Date;
                    for (int j = 0; j < _dayCells.Count; j++)
                    {
                        _dayCells[j].IsSelected = (_dayCells[j].Date.Date == _selectedDate.Date);
                    }
                    _gridPanel.Invalidate();
                    DateSelected?.Invoke(_selectedDate);

                    if (e.Button == MouseButtons.Right)
                    {
                        float scale = this.DeviceDpi / 96f;
                        int headerAreaH = (int)(26 * scale);
                        int eventH = (int)(18 * scale);
                        int eventY = cell.Bounds.Top + headerAreaH;
                        int maxEvents = Math.Max(1, (cell.Bounds.Height - headerAreaH - (int)(4 * scale)) / eventH);
                        int visibleCount = Math.Min(cell.Events.Count, maxEvents);

                        CalendarEvent? clickedEvent = null;
                        for (int evIdx = 0; evIdx < visibleCount; evIdx++)
                        {
                            var evRect = new Rectangle(cell.Bounds.Left + 3, eventY + 1, cell.Bounds.Width - 6, eventH - 2);
                            if (evRect.Contains(e.Location))
                            {
                                clickedEvent = cell.Events[evIdx];
                                break;
                            }
                            eventY += eventH;
                        }

                        var menu = new ContextMenuStrip();
                        if (clickedEvent != null)
                        {
                            var itemTitle = new ToolStripMenuItem($"Event: {clickedEvent.Title}") { Enabled = false, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
                            var itemEdit = new ToolStripMenuItem("✏️ Edit Event", null, (s, args) => EditEventRequested?.Invoke(clickedEvent));
                            var itemDelete = new ToolStripMenuItem("🗑️ Delete Event", null, (s, args) => DeleteEventRequested?.Invoke(clickedEvent));
                            menu.Items.Add(itemTitle);
                            menu.Items.Add(new ToolStripSeparator());
                            menu.Items.Add(itemEdit);
                            menu.Items.Add(itemDelete);
                        }
                        else
                        {
                            var itemAdd = new ToolStripMenuItem($"➕ Add Event on {cell.Date:MMM d}...", null, (s, args) => CreateEventRequested?.Invoke(cell.Date));
                            var itemToday = new ToolStripMenuItem("📅 Go to Today", null, (s, args) =>
                            {
                                _currentMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                                _selectedDate = DateTime.Today;
                                RefreshCalendarGrid();
                                DateSelected?.Invoke(_selectedDate);
                            });
                            menu.Items.Add(itemAdd);
                            menu.Items.Add(itemToday);
                        }
                        menu.Show(_gridPanel, e.Location);
                    }

                    return;
                }
            }
        }

        private void OnGridMouseDoubleClick(object? sender, MouseEventArgs e)
        {
            for (int i = 0; i < _dayCells.Count; i++)
            {
                if (_dayCells[i].Bounds.Contains(e.Location))
                {
                    _selectedDate = _dayCells[i].Date;
                    DateSelected?.Invoke(_selectedDate);
                    CreateEventRequested?.Invoke(_selectedDate);
                    return;
                }
            }
        }
    }
}
