using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace KerkenezCalendar.UI.Controls
{
    public class SidebarNav : Panel
    {
        public const int ExpandedWidth = 195;
        public const int CollapsedWidth = 60;

        public event EventHandler<int>? TabChanged;
        public event EventHandler<bool>? CollapsedChanged;

        private readonly string[] _tabTitles = new[]
        {
            "Calendar",
            "Agenda",
            "Accounts",
            "Settings",
            "Live Logs"
        };

        private readonly string[] _tabIcons = new[]
        {
            "\uE787", // Calendar
            "\uE71D", // Agenda / List
            "\uE716", // People / Accounts
            "\uE713", // Settings gear
            "\uE756"  // Console / Live Logs
        };

        private static string? _iconFontFamily;
        private static string GetIconFontFamily()
        {
            if (_iconFontFamily != null) return _iconFontFamily;

            try
            {
                using var installedFonts = new System.Drawing.Text.InstalledFontCollection();
                var set = new HashSet<string>(installedFonts.Families.Select(f => f.Name), StringComparer.OrdinalIgnoreCase);
                if (set.Contains("Segoe Fluent Icons")) return _iconFontFamily = "Segoe Fluent Icons";
                if (set.Contains("Segoe MDL2 Assets")) return _iconFontFamily = "Segoe MDL2 Assets";
            }
            catch { }

            return _iconFontFamily = "Segoe UI Symbol";
        }

        private int _selectedIndex = 0;
        private int _hoveredIndex = -1;
        private bool _isToggleHovered = false;
        private bool _isCollapsed = false;

        private readonly System.Windows.Forms.Timer _animTimer;
        private int _startWidth;
        private int _targetWidth;
        private int _animFrame = 0;
        private const int TotalAnimFrames = 6;

        private readonly ToolTip _toolTip;
        private string _currentToolTipText = "";

        private readonly Color _bgColor = Color.FromArgb(240, 242, 245);
        private readonly Color _activeBgColor = Color.FromArgb(255, 255, 255);
        private readonly Color _hoverBgColor = Color.FromArgb(230, 233, 238);
        private readonly Color _btnHoverBgColor = Color.FromArgb(220, 224, 230);
        private readonly Color _textColor = Color.FromArgb(50, 54, 62);
        private readonly Color _activeTextColor = Color.FromArgb(0, 102, 204);
        private readonly Color _accentColor = Color.FromArgb(0, 120, 215);
        private readonly Color _borderColor = Color.FromArgb(218, 222, 228);

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int SelectedIndex
        {
            get => _selectedIndex;
            set
            {
                if (_selectedIndex != value && value >= 0 && value < _tabTitles.Length)
                {
                    _selectedIndex = value;
                    Invalidate();
                    TabChanged?.Invoke(this, _selectedIndex);
                }
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool IsCollapsed
        {
            get => _isCollapsed;
            set
            {
                if (_isCollapsed != value)
                {
                    _isCollapsed = value;
                    StartAnimation(_isCollapsed ? CollapsedWidth : ExpandedWidth);
                    CollapsedChanged?.Invoke(this, _isCollapsed);
                }
            }
        }

        public SidebarNav()
        {
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            this.Dock = DockStyle.Left;
            this.Width = ExpandedWidth;
            this.BackColor = _bgColor;
            this.Cursor = Cursors.Default;

            _toolTip = new ToolTip
            {
                InitialDelay = 400,
                ReshowDelay = 100,
                AutoPopDelay = 3000
            };

            _animTimer = new System.Windows.Forms.Timer { Interval = 15 };
            _animTimer.Tick += OnAnimTick;
        }

        private void StartAnimation(int targetW)
        {
            _startWidth = this.Width;
            _targetWidth = targetW;
            _animFrame = 0;
            _animTimer.Start();
        }

        private void OnAnimTick(object? sender, EventArgs e)
        {
            _animFrame++;
            float t = (float)_animFrame / TotalAnimFrames;
            t = (float)Math.Sin(t * Math.PI / 2); // Ease out
            this.Width = (int)(_startWidth + (_targetWidth - _startWidth) * t);

            if (_animFrame >= TotalAnimFrames)
            {
                _animTimer.Stop();
                this.Width = _targetWidth;
            }
            Invalidate();
        }

        private int HeaderHeight => (int)(56 * (this.DeviceDpi / 96f));
        private int ItemHeight => (int)(42 * (this.DeviceDpi / 96f));
        private int ToggleBtnHeight => (int)(40 * (this.DeviceDpi / 96f));

        private Rectangle GetToggleRect()
        {
            float scale = this.DeviceDpi / 96f;
            int sz = (int)(28 * scale);
            bool isWide = !_isCollapsed && Width >= (int)(130 * scale);

            if (isWide)
            {
                return new Rectangle(Width - sz - (int)(10 * scale), (HeaderHeight - sz) / 2, sz, sz);
            }
            else
            {
                return new Rectangle((Width - sz) / 2, (HeaderHeight - sz) / 2, sz, sz);
            }
        }

        private Rectangle GetItemRect(int index)
        {
            float scale = this.DeviceDpi / 96f;
            int margin = (int)(6 * scale);
            int itemH = ItemHeight - (int)(4 * scale);

            if (index == 4) // Live Logs placed at the bottom of the sidebar
            {
                int yBottom = Height - ItemHeight - (int)(10 * scale);
                return new Rectangle(margin, yBottom, Width - (margin * 2), itemH);
            }

            int y = HeaderHeight + (index * ItemHeight);
            return new Rectangle(margin, y, Width - (margin * 2), itemH);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            float scale = this.DeviceDpi / 96f;
            string iconFont = GetIconFontFamily();

            // Right border line
            using (var borderPen = new Pen(_borderColor, 1))
            {
                g.DrawLine(borderPen, Width - 1, 0, Width - 1, Height);
            }

            // Top Header: App Branding / Hamburger Toggle
            var toggleRect = GetToggleRect();
            bool isWide = !_isCollapsed && Width >= (int)(130 * scale);

            if (_isToggleHovered)
            {
                using var hoverBrush = new SolidBrush(_btnHoverBgColor);
                g.FillRoundedRectangle(hoverBrush, toggleRect, 4);
            }

            using (var icoFont = new Font(iconFont, 11F, FontStyle.Regular))
            using (var textBrush = new SolidBrush(_textColor))
            {
                var sfCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString(isWide ? "\uE700" : "\uE700", icoFont, textBrush, toggleRect, sfCenter);
            }

            // App Title & Subtitle if Expanded
            if (isWide)
            {
                int textMaxWidth = toggleRect.Left - (int)(18 * scale);
                if (textMaxWidth > 30)
                {
                    using var titleFont = new Font("Segoe UI", 10.5F, FontStyle.Bold);
                    using var subFont = new Font("Segoe UI", 8F, FontStyle.Regular);
                    using var titleBrush = new SolidBrush(Color.FromArgb(25, 25, 25));
                    using var subBrush = new SolidBrush(Color.FromArgb(115, 120, 130));

                    var sfTitle = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
                    var titleRect = new Rectangle((int)(14 * scale), (int)(10 * scale), textMaxWidth, (int)(20 * scale));
                    var subRect = new Rectangle((int)(14 * scale), (int)(31 * scale), textMaxWidth, (int)(16 * scale));

                    g.DrawString("Kerkenez", titleFont, titleBrush, titleRect, sfTitle);
                    g.DrawString("Calendar", subFont, subBrush, subRect, sfTitle);
                }
            }

            // Navigation Tabs
            for (int i = 0; i < _tabTitles.Length; i++)
            {
                var rect = GetItemRect(i);
                bool isSelected = (i == _selectedIndex);
                bool isHovered = (i == _hoveredIndex);

                // Divider above Live Logs
                if (i == 4)
                {
                    int sepY = rect.Top - (int)(8 * scale);
                    if (sepY > HeaderHeight + (4 * ItemHeight))
                    {
                        using var sepPen = new Pen(_borderColor, 1);
                        g.DrawLine(sepPen, (int)(10 * scale), sepY, Width - (int)(10 * scale), sepY);
                    }
                }

                if (isSelected)
                {
                    using var activeBrush = new SolidBrush(_activeBgColor);
                    g.FillRoundedRectangle(activeBrush, rect, 6);

                    using var pen = new Pen(_borderColor, 1);
                    g.DrawRoundedRectangle(pen, rect, 6);

                    // Left active accent bar
                    using var accentBrush = new SolidBrush(_accentColor);
                    g.FillRoundedRectangle(accentBrush, new Rectangle(rect.Left + 1, rect.Top + 6, 3, rect.Height - 12), 2);
                }
                else if (isHovered)
                {
                    using var hoverBrush = new SolidBrush(_hoverBgColor);
                    g.FillRoundedRectangle(hoverBrush, rect, 6);
                }

                // Tab Icon
                int iconSize = (int)(22 * scale);
                var iconRect = new Rectangle(rect.Left + (int)(10 * scale), rect.Top + (rect.Height - iconSize) / 2, iconSize, iconSize);

                using (var tabIcoFont = new Font(iconFont, 11F, FontStyle.Regular))
                using (var iconBrush = new SolidBrush(isSelected ? _activeTextColor : _textColor))
                {
                    var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString(_tabIcons[i], tabIcoFont, iconBrush, iconRect, sf);
                }

                // Tab Title if Expanded
                if (!_isCollapsed && Width >= 110)
                {
                    var textRect = new Rectangle(iconRect.Right + (int)(10 * scale), rect.Top, rect.Width - iconRect.Width - (int)(16 * scale), rect.Height);
                    using var font = new Font("Segoe UI", 9.25F, isSelected ? FontStyle.Bold : FontStyle.Regular);
                    using var textBrush = new SolidBrush(isSelected ? _activeTextColor : _textColor);
                    var sf = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };
                    g.DrawString(_tabTitles[i], font, textBrush, textRect, sf);
                }
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            bool toggleHover = GetToggleRect().Contains(e.Location);
            if (toggleHover != _isToggleHovered)
            {
                _isToggleHovered = toggleHover;
                Invalidate();
            }

            int newHover = -1;
            for (int i = 0; i < _tabTitles.Length; i++)
            {
                if (GetItemRect(i).Contains(e.Location))
                {
                    newHover = i;
                    break;
                }
            }

            if (newHover != _hoveredIndex)
            {
                _hoveredIndex = newHover;
                Invalidate();

                if (_isCollapsed && _hoveredIndex >= 0)
                {
                    string tip = _tabTitles[_hoveredIndex];
                    if (_currentToolTipText != tip)
                    {
                        _currentToolTipText = tip;
                        _toolTip.SetToolTip(this, tip);
                    }
                }
                else if (_hoveredIndex < 0)
                {
                    _currentToolTipText = "";
                    _toolTip.SetToolTip(this, null);
                }
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hoveredIndex = -1;
            _isToggleHovered = false;
            _currentToolTipText = "";
            _toolTip.SetToolTip(this, null);
            Invalidate();
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);

            if (GetToggleRect().Contains(e.Location))
            {
                IsCollapsed = !IsCollapsed;
                return;
            }

            for (int i = 0; i < _tabTitles.Length; i++)
            {
                if (GetItemRect(i).Contains(e.Location))
                {
                    SelectedIndex = i;
                    return;
                }
            }
        }
    }
}

