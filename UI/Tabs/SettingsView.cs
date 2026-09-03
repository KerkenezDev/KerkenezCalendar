using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using KerkenezCalendar.Models;
using KerkenezCalendar.Services;
using KerkenezCalendar.UI.Controls;

namespace KerkenezCalendar.UI.Tabs
{
    public class SettingsView : UserControl
    {
        private readonly CalendarConfigService _configService;
        private readonly CalendarEventService _eventService;

        // UI & Layout Controls
        private CheckBox _chkCollapseSidebarByDefault = null!;
        private NumericUpDown _numWindowWidthScale = null!;
        private NumericUpDown _numWindowHeightScale = null!;
        private Button _btnApplyWindowSizeNow = null!;
        private Label _lblScalePreview = null!;
        private Button _btnCreateShortcuts = null!;

        // Calendar Preferences Controls
        private ComboBox _cboStartOfWeek = null!;
        private ComboBox _cboDefaultReminder = null!;
        private CheckBox _chkTimeFormat24Hour = null!;
        private CheckBox _chkShowWeekend = null!;
        private ComboBox _cboDefaultCategory = null!;

        // System Tray Daemon Controls
        private CheckBox _chkAlwaysKeepOn = null!;
        private CheckBox _chkEnableTrayNotifs = null!;
        private NumericUpDown _numTrayInterval = null!;
        private CheckBox _chkStartWithWindows = null!;
        private CheckBox _chkPlaySound = null!;
        private Button _btnRestartDaemon = null!;

        // Bottom Actions
        private Button _btnSave = null!;
        private Button _btnResetDefaults = null!;
        private Label _lblStatus = null!;

        public event Action? SettingsSaved;

        public SettingsView(CalendarConfigService configService, CalendarEventService eventService)
        {
            _configService = configService;
            _eventService = eventService;

            InitializeComponent();
            LoadSettings();
            UpdateScalePreview();
        }

        private void InitializeComponent()
        {
            this.Dock = DockStyle.Fill;
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            this.BackColor = Color.FromArgb(248, 249, 250);

            float scale = this.DeviceDpi / 96f;

            var scroll = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                Padding = new Padding((int)(20 * scale), (int)(16 * scale), (int)(20 * scale), (int)(20 * scale)),
                BackColor = Color.FromArgb(248, 249, 250)
            };

            var mainContainer = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Padding = new Padding(0)
            };

            int cardW = (int)(620 * scale);

            // ==================== 1. User Interface & Window Layout Card ====================
            var pnlUiCard = CreateCardPanel(cardW, scale);
            var lblSecUi = CreateSectionHeader("🖥️  User Interface & Window Layout", scale);

            _chkCollapseSidebarByDefault = new CheckBox
            {
                Text = "Collapse sidebar navigation by default",
                AutoSize = true,
                Margin = new Padding(0, 0, 0, (int)(8 * scale)),
                Font = new Font("Segoe UI", 9F)
            };

            var lblScalingHeader = new Label
            {
                Text = "Window Scaling (% of screen working area):",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(40, 40, 40),
                AutoSize = true,
                Margin = new Padding(0, (int)(4 * scale), 0, (int)(2 * scale))
            };

            var lblScalingDesc = new Label
            {
                Text = "Controls initial and default window proportions relative to your display working area.",
                Font = new Font("Segoe UI", 8.25F, FontStyle.Regular),
                ForeColor = Color.FromArgb(110, 115, 122),
                AutoSize = true,
                Margin = new Padding(0, 0, 0, (int)(8 * scale))
            };

            var rowScaleControls = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0, 0, 0, (int)(8 * scale))
            };

            var lblWidth = new Label { Text = "Width (%):", AutoSize = true, Margin = new Padding(0, 4, 6, 0), Font = new Font("Segoe UI", 9F) };
            _numWindowWidthScale = new NumericUpDown
            {
                Width = (int)(75 * scale),
                DecimalPlaces = 1,
                Minimum = 30.0M,
                Maximum = 100.0M,
                Increment = 0.5M,
                Value = 60.0M,
                Font = new Font("Segoe UI", 9F),
                Margin = new Padding(0, 0, (int)(12 * scale), 0)
            };
            _numWindowWidthScale.ValueChanged += (s, e) => UpdateScalePreview();

            var lblHeight = new Label { Text = "Height (%):", AutoSize = true, Margin = new Padding(0, 4, 6, 0), Font = new Font("Segoe UI", 9F) };
            _numWindowHeightScale = new NumericUpDown
            {
                Width = (int)(75 * scale),
                DecimalPlaces = 1,
                Minimum = 30.0M,
                Maximum = 100.0M,
                Increment = 0.5M,
                Value = 56.0M,
                Font = new Font("Segoe UI", 9F),
                Margin = new Padding(0, 0, (int)(12 * scale), 0)
            };
            _numWindowHeightScale.ValueChanged += (s, e) => UpdateScalePreview();

            _btnApplyWindowSizeNow = new Button
            {
                Text = "⚡ Resize Active Window",
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding((int)(10 * scale), (int)(4 * scale), (int)(10 * scale), (int)(4 * scale)),
                FlatStyle = FlatStyle.System,
                Cursor = Cursors.Hand
            };
            _btnApplyWindowSizeNow.Click += OnApplyWindowSizeNowClick;

            rowScaleControls.Controls.Add(lblWidth);
            rowScaleControls.Controls.Add(_numWindowWidthScale);
            rowScaleControls.Controls.Add(lblHeight);
            rowScaleControls.Controls.Add(_numWindowHeightScale);
            rowScaleControls.Controls.Add(_btnApplyWindowSizeNow);

            // Preset chips
            var rowPresets = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                Margin = new Padding(0, 0, 0, (int)(6 * scale))
            };

            var lblPresets = new Label { Text = "Presets:", AutoSize = true, Margin = new Padding(0, 4, 6, 0), Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), ForeColor = Color.FromArgb(80, 80, 80) };
            var btnPresetDefault = CreatePresetChip("60% × 56% (Default)", 60.0M, 56.0M, scale);
            var btnPresetCompact = CreatePresetChip("50% × 50% (Compact)", 50.0M, 50.0M, scale);
            var btnPresetLarge = CreatePresetChip("75% × 70% (Large)", 75.0M, 70.0M, scale);
            var btnPresetMax = CreatePresetChip("95% × 90% (Near Max)", 95.0M, 90.0M, scale);

            rowPresets.Controls.Add(lblPresets);
            rowPresets.Controls.Add(btnPresetDefault);
            rowPresets.Controls.Add(btnPresetCompact);
            rowPresets.Controls.Add(btnPresetLarge);
            rowPresets.Controls.Add(btnPresetMax);

            _lblScalePreview = new Label
            {
                Text = "",
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = Color.FromArgb(90, 95, 105),
                Margin = new Padding(0, 0, 0, (int)(10 * scale))
            };

            _btnCreateShortcuts = new Button
            {
                Text = "📌  Add Desktop & Start Menu Shortcuts",
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding((int)(10 * scale), (int)(5 * scale), (int)(10 * scale), (int)(5 * scale)),
                Margin = new Padding(0, (int)(4 * scale), 0, 0),
                FlatStyle = FlatStyle.System,
                Cursor = Cursors.Hand
            };
            _btnCreateShortcuts.Click += (s, e) =>
            {
                bool ok = StartupRegistrationService.CreateShortcuts();
                if (ok)
                {
                    MessageBox.Show(this, "Shortcuts for Kerkenez Calendar were successfully created on your Desktop and Start Menu!", "Shortcuts Created", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show(this, "Could not create shortcuts.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            };

            pnlUiCard.Controls.Add(lblSecUi);
            pnlUiCard.Controls.Add(_chkCollapseSidebarByDefault);
            pnlUiCard.Controls.Add(lblScalingHeader);
            pnlUiCard.Controls.Add(lblScalingDesc);
            pnlUiCard.Controls.Add(rowScaleControls);
            pnlUiCard.Controls.Add(rowPresets);
            pnlUiCard.Controls.Add(_lblScalePreview);
            pnlUiCard.Controls.Add(_btnCreateShortcuts);

            // ==================== 2. Calendar Preferences Card ====================
            var pnlCalCard = CreateCardPanel(cardW, scale);
            var lblSecCal = CreateSectionHeader("📅  Calendar Preferences", scale);

            var tableCal = new TableLayoutPanel
            {
                AutoSize = true,
                ColumnCount = 2,
                RowCount = 5,
                Margin = new Padding(0)
            };
            tableCal.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, (int)(180 * scale)));
            tableCal.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

            Label MakeCalLabel(string text) => new Label { Text = text, AutoSize = true, Margin = new Padding(0, 5, 0, 0), Font = new Font("Segoe UI", 9F) };

            _cboStartOfWeek = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = (int)(160 * scale), Margin = new Padding(0, 0, 0, (int)(6 * scale)) };
            _cboStartOfWeek.Items.Add("Monday");
            _cboStartOfWeek.Items.Add("Sunday");

            _cboDefaultReminder = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = (int)(200 * scale), Margin = new Padding(0, 0, 0, (int)(6 * scale)) };
            _cboDefaultReminder.Items.AddRange(new object[]
            {
                "None",
                "At time of event (0 min)",
                "5 minutes before",
                "10 minutes before",
                "15 minutes before",
                "30 minutes before",
                "1 hour before",
                "2 hours before",
                "1 day before"
            });

            _chkTimeFormat24Hour = new CheckBox { Text = "Use 24-hour clock (e.g. 14:30 instead of 02:30 PM)", AutoSize = true, Margin = new Padding(0, 2, 0, (int)(6 * scale)) };
            _chkShowWeekend = new CheckBox { Text = "Show Saturday and Sunday in calendar views", AutoSize = true, Margin = new Padding(0, 2, 0, (int)(6 * scale)) };

            _cboDefaultCategory = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = (int)(160 * scale), Margin = new Padding(0, 0, 0, (int)(4 * scale)) };
            _cboDefaultCategory.Items.AddRange(new object[] { "Work", "Personal", "Important", "Meeting", "Birthday", "General" });

            tableCal.Controls.Add(MakeCalLabel("First day of week:"), 0, 0);
            tableCal.Controls.Add(_cboStartOfWeek, 1, 0);

            tableCal.Controls.Add(MakeCalLabel("Default reminder:"), 0, 1);
            tableCal.Controls.Add(_cboDefaultReminder, 1, 1);

            tableCal.Controls.Add(MakeCalLabel("Time format:"), 0, 2);
            tableCal.Controls.Add(_chkTimeFormat24Hour, 1, 2);

            tableCal.Controls.Add(MakeCalLabel("Weekends:"), 0, 3);
            tableCal.Controls.Add(_chkShowWeekend, 1, 3);

            tableCal.Controls.Add(MakeCalLabel("Default category:"), 0, 4);
            tableCal.Controls.Add(_cboDefaultCategory, 1, 4);

            pnlCalCard.Controls.Add(lblSecCal);
            pnlCalCard.Controls.Add(tableCal);

            // ==================== 3. System Tray Daemon & Notifications Card ====================
            var pnlTrayCard = CreateCardPanel(cardW, scale);
            var lblSecTray = CreateSectionHeader("🔔  System Tray Daemon & Notifications", scale);

            _chkAlwaysKeepOn = new CheckBox
            {
                Text = "Always keep on (Run system tray daemon in background independently)",
                AutoSize = true,
                Checked = true,
                Margin = new Padding(0, 0, 0, (int)(6 * scale)),
                Font = new Font("Segoe UI", 9F)
            };

            _chkEnableTrayNotifs = new CheckBox
            {
                Text = "Enable Windows desktop balloon/toast notifications",
                AutoSize = true,
                Checked = true,
                Margin = new Padding(0, 0, 0, (int)(6 * scale)),
                Font = new Font("Segoe UI", 9F)
            };

            _chkPlaySound = new CheckBox
            {
                Text = "Play alert sound when reminder notification triggers",
                AutoSize = true,
                Checked = true,
                Margin = new Padding(0, 0, 0, (int)(8 * scale)),
                Font = new Font("Segoe UI", 9F)
            };

            var rowTrayInterval = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0, 0, 0, (int)(8 * scale))
            };
            var lblInterval = new Label { Text = "Reminder check interval (minutes):", AutoSize = true, Margin = new Padding(0, 4, 8, 0), Font = new Font("Segoe UI", 9F) };
            _numTrayInterval = new NumericUpDown { Width = (int)(75 * scale), Minimum = 1, Maximum = 120, Value = 5, Font = new Font("Segoe UI", 9F) };
            rowTrayInterval.Controls.Add(lblInterval);
            rowTrayInterval.Controls.Add(_numTrayInterval);

            _chkStartWithWindows = new CheckBox
            {
                Text = "Start system tray daemon on user Windows log-in",
                AutoSize = true,
                Checked = false,
                Margin = new Padding(0, 0, 0, (int)(10 * scale)),
                Font = new Font("Segoe UI", 9F)
            };

            var rowDaemonAction = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0, (int)(2 * scale), 0, 0)
            };

            _btnRestartDaemon = new Button
            {
                Text = "🔄  Restart / Start Tray Daemon",
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding((int)(12 * scale), (int)(5 * scale), (int)(12 * scale), (int)(5 * scale)),
                Margin = new Padding(0, 0, (int)(8 * scale), 0),
                FlatStyle = FlatStyle.System,
                Cursor = Cursors.Hand
            };
            _btnRestartDaemon.Click += OnRestartDaemonClick;

            rowDaemonAction.Controls.Add(_btnRestartDaemon);

            pnlTrayCard.Controls.Add(lblSecTray);
            pnlTrayCard.Controls.Add(_chkAlwaysKeepOn);
            pnlTrayCard.Controls.Add(_chkEnableTrayNotifs);
            pnlTrayCard.Controls.Add(_chkPlaySound);
            pnlTrayCard.Controls.Add(rowTrayInterval);
            pnlTrayCard.Controls.Add(_chkStartWithWindows);
            pnlTrayCard.Controls.Add(rowDaemonAction);

            // ==================== 4. Bottom Action Buttons ====================
            var pnlBottomBar = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0, (int)(8 * scale), 0, (int)(24 * scale))
            };

            _btnSave = new Button
            {
                Text = "💾  Save Settings",
                AutoSize = true,
                Padding = new Padding((int)(16 * scale), (int)(6 * scale), (int)(16 * scale), (int)(6 * scale)),
                Margin = new Padding(0, 0, (int)(8 * scale), 0),
                FlatStyle = FlatStyle.System,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnSave.Click += OnSaveSettingsClick;

            _btnResetDefaults = new Button
            {
                Text = "↺  Reset Defaults",
                AutoSize = true,
                Padding = new Padding((int)(12 * scale), (int)(6 * scale), (int)(12 * scale), (int)(6 * scale)),
                Margin = new Padding(0, 0, (int)(12 * scale), 0),
                FlatStyle = FlatStyle.System,
                Cursor = Cursors.Hand
            };
            _btnResetDefaults.Click += (s, e) =>
            {
                var res = MessageBox.Show(this, "Reset all settings to default values?", "Confirm Reset", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (res == DialogResult.Yes)
                {
                    _configService.SaveConfig(CalendarSettings.CreateDefault());
                    LoadSettings();
                    UpdateScalePreview();
                    _lblStatus.Text = "Settings reset to defaults.";
                }
            };

            _lblStatus = new Label
            {
                Text = "",
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Italic),
                ForeColor = Color.FromArgb(46, 125, 50),
                Margin = new Padding(0, (int)(8 * scale), 0, 0)
            };

            pnlBottomBar.Controls.Add(_btnSave);
            pnlBottomBar.Controls.Add(_btnResetDefaults);
            pnlBottomBar.Controls.Add(_lblStatus);

            mainContainer.Controls.Add(pnlUiCard);
            mainContainer.Controls.Add(pnlCalCard);
            mainContainer.Controls.Add(pnlTrayCard);
            mainContainer.Controls.Add(pnlBottomBar);

            scroll.Controls.Add(mainContainer);
            this.Controls.Add(scroll);
        }

        private FlowLayoutPanel CreateCardPanel(int width, float scale)
        {
            var pnl = new FlowLayoutPanel
            {
                Width = width,
                AutoSize = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = Color.White,
                Margin = new Padding(0, 0, 0, (int)(14 * scale)),
                Padding = new Padding((int)(14 * scale), (int)(12 * scale), (int)(14 * scale), (int)(14 * scale))
            };

            pnl.Paint += (s, e) =>
            {
                using var pen = new Pen(Color.FromArgb(226, 230, 235), 1);
                e.Graphics.DrawRoundedRectangle(pen, new Rectangle(0, 0, pnl.Width - 1, pnl.Height - 1), 6);
            };

            return pnl;
        }

        private Label CreateSectionHeader(string text, float scale)
        {
            return new Label
            {
                Text = text,
                Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 30, 30),
                AutoSize = true,
                Margin = new Padding(0, 0, 0, (int)(10 * scale))
            };
        }

        private Button CreatePresetChip(string label, decimal w, decimal h, float scale)
        {
            var btn = new Button
            {
                Text = label,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding((int)(6 * scale), (int)(3 * scale), (int)(6 * scale), (int)(3 * scale)),
                Margin = new Padding(0, 0, (int)(6 * scale), (int)(4 * scale)),
                FlatStyle = FlatStyle.System,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 8F)
            };
            btn.Click += (s, e) =>
            {
                _numWindowWidthScale.Value = w;
                _numWindowHeightScale.Value = h;
                UpdateScalePreview();
            };
            return btn;
        }

        private void UpdateScalePreview()
        {
            var screen = Screen.FromControl(this) ?? Screen.PrimaryScreen;
            var workingArea = screen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);

            double wScale = (double)_numWindowWidthScale.Value / 100.0;
            double hScale = (double)_numWindowHeightScale.Value / 100.0;

            int targetW = (int)Math.Round(workingArea.Width * wScale);
            int targetH = (int)Math.Round(workingArea.Height * hScale);

            targetW = Math.Clamp(targetW, 960, workingArea.Width);
            targetH = Math.Clamp(targetH, 540, workingArea.Height);

            _lblScalePreview.Text = $"Calculated dimensions for {workingArea.Width} × {workingArea.Height} display: {targetW} × {targetH} px";
        }

        private void OnApplyWindowSizeNowClick(object? sender, EventArgs e)
        {
            if (this.FindForm() is MainForm mainForm)
            {
                double wScale = (double)_numWindowWidthScale.Value / 100.0;
                double hScale = (double)_numWindowHeightScale.Value / 100.0;

                _configService.Settings.WindowWidthScale = wScale;
                _configService.Settings.WindowHeightScale = hScale;
                _configService.Settings.WindowWidth = 0; // Trigger scale recalculation
                _configService.Settings.WindowHeight = 0;
                _configService.SaveConfig();

                mainForm.ApplyConfiguredLayout(true);
            }
        }

        private void OnRestartDaemonClick(object? sender, EventArgs e)
        {
            try
            {
                CalendarDaemonHelper.RestartDaemon();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Error launching daemon: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void LoadSettings()
        {
            var s = _configService.Settings;

            _chkCollapseSidebarByDefault.Checked = s.CollapseSidebarByDefault;

            _numWindowWidthScale.Value = (decimal)Math.Clamp(s.WindowWidthScale * 100.0, 30.0, 100.0);
            _numWindowHeightScale.Value = (decimal)Math.Clamp(s.WindowHeightScale * 100.0, 30.0, 100.0);

            _cboStartOfWeek.SelectedIndex = (s.StartOfWeek == DayOfWeek.Sunday) ? 1 : 0;

            _cboDefaultReminder.SelectedIndex = s.DefaultReminderMinutes switch
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
                _ => 4
            };

            _chkTimeFormat24Hour.Checked = s.TimeFormat24Hour;
            _chkShowWeekend.Checked = s.ShowWeekend;

            int catIdx = _cboDefaultCategory.Items.IndexOf(s.DefaultCategory);
            _cboDefaultCategory.SelectedIndex = catIdx >= 0 ? catIdx : 0;

            _chkAlwaysKeepOn.Checked = s.AlwaysKeepOn;
            _chkEnableTrayNotifs.Checked = s.EnableTrayNotifications;
            _chkPlaySound.Checked = s.PlaySoundOnReminder;
            _numTrayInterval.Value = Math.Clamp(s.TrayRefreshIntervalMinutes, 1, 120);
            _chkStartWithWindows.Checked = s.StartWithWindows || StartupRegistrationService.IsStartupEnabled();
        }

        private void OnSaveSettingsClick(object? sender, EventArgs e)
        {
            var s = _configService.Settings;

            s.CollapseSidebarByDefault = _chkCollapseSidebarByDefault.Checked;
            s.WindowWidthScale = (double)_numWindowWidthScale.Value / 100.0;
            s.WindowHeightScale = (double)_numWindowHeightScale.Value / 100.0;

            s.StartOfWeek = (_cboStartOfWeek.SelectedIndex == 1) ? DayOfWeek.Sunday : DayOfWeek.Monday;

            s.DefaultReminderMinutes = _cboDefaultReminder.SelectedIndex switch
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

            s.TimeFormat24Hour = _chkTimeFormat24Hour.Checked;
            s.ShowWeekend = _chkShowWeekend.Checked;
            s.DefaultCategory = _cboDefaultCategory.SelectedItem?.ToString() ?? "Work";

            s.AlwaysKeepOn = _chkAlwaysKeepOn.Checked;
            s.EnableTrayNotifications = _chkEnableTrayNotifs.Checked;
            s.PlaySoundOnReminder = _chkPlaySound.Checked;
            s.TrayRefreshIntervalMinutes = (int)_numTrayInterval.Value;
            s.StartWithWindows = _chkStartWithWindows.Checked;

            // Sync registry
            StartupRegistrationService.SetStartupEnabled(s.StartWithWindows);

            // Save configuration
            _configService.SaveConfig(s);

            // Handle daemon state based on AlwaysKeepOn
            if (!s.AlwaysKeepOn)
            {
                CalendarDaemonHelper.StopDaemon();
            }
            else if (!CalendarDaemonHelper.IsDaemonRunning())
            {
                CalendarDaemonHelper.StartDaemon();
            }

            _lblStatus.Text = "Settings saved successfully.";
            SettingsSaved?.Invoke();
        }
    }
}
