using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using KerkenezCalendar.Models;
using KerkenezCalendar.Services;
using KerkenezCalendar.UI.Controls;

namespace KerkenezCalendar.UI.Tabs
{
    public class AccountsView : UserControl
    {
        private readonly CalendarConfigService _configService;

        private FlowLayoutPanel _pnlCards = null!;
        private Button _btnAddAccount = null!;
        private Label _lblEmpty = null!;

        public event Action? AccountsChanged;

        public AccountsView(CalendarConfigService configService)
        {
            _configService = configService;

            InitializeComponent();
            LoadAccounts();
        }

        private void InitializeComponent()
        {
            this.Dock = DockStyle.Fill;
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            this.BackColor = Color.FromArgb(248, 249, 250);

            float scale = this.DeviceDpi / 96f;

            // Top Action Toolbar
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
                Text = "Configured Email Accounts (Shared)",
                Dock = DockStyle.Left,
                AutoSize = true,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 30, 30),
                TextAlign = ContentAlignment.MiddleLeft
            };

            _btnAddAccount = new Button
            {
                Text = "➕ Add Account",
                Dock = DockStyle.Right,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding((int)(12 * scale), (int)(4 * scale), (int)(12 * scale), (int)(4 * scale)),
                FlatStyle = FlatStyle.System,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnAddAccount.Click += OnAddAccountClick;

            topPanel.Controls.Add(lblTitle);
            topPanel.Controls.Add(_btnAddAccount);

            // Information banner explaining shared accounts.dat
            var infoBanner = new Panel
            {
                Dock = DockStyle.Top,
                Height = (int)(42 * scale),
                BackColor = Color.FromArgb(238, 245, 254),
                Padding = new Padding((int)(14 * scale), (int)(10 * scale), (int)(14 * scale), (int)(10 * scale))
            };

            var lblInfoText = new Label
            {
                Text = "🔒 Stored securely in %APPDATA%\\Kerkenez\\accounts.dat (DPAPI encrypted). Shared directly with Kerkenez Mail (EmailSummarizer).",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                ForeColor = Color.FromArgb(0, 102, 204),
                TextAlign = ContentAlignment.MiddleLeft
            };
            infoBanner.Controls.Add(lblInfoText);

            // Scrollable cards list
            _pnlCards = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Padding = new Padding((int)(14 * scale)),
                BackColor = Color.FromArgb(248, 249, 250)
            };

            _lblEmpty = new Label
            {
                Text = "No email accounts configured yet in %APPDATA%\\Kerkenez\\accounts.dat.\r\nEvents default to Local Calendar. Click 'Add Account' to link an email account.",
                Font = new Font("Segoe UI", 10F, FontStyle.Regular),
                ForeColor = Color.FromArgb(120, 125, 133),
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Fill,
                Visible = false
            };

            this.Controls.Add(_pnlCards);
            this.Controls.Add(_lblEmpty);
            this.Controls.Add(infoBanner);
            this.Controls.Add(topPanel);
        }

        public void LoadAccounts()
        {
            _pnlCards.SuspendLayout();
            _pnlCards.Controls.Clear();

            var accounts = _configService.GetAccounts();
            float scale = this.DeviceDpi / 96f;

            if (accounts.Count == 0)
            {
                _pnlCards.Visible = false;
                _lblEmpty.Visible = true;
                _lblEmpty.BringToFront();
            }
            else
            {
                _lblEmpty.Visible = false;
                _pnlCards.Visible = true;
                _pnlCards.BringToFront();

                int cardW = Math.Max(320, _pnlCards.ClientSize.Width - (int)(32 * scale));

                foreach (var acc in accounts)
                {
                    var card = CreateAccountCard(acc, cardW, scale);
                    _pnlCards.Controls.Add(card);
                }
            }

            _pnlCards.ResumeLayout(true);
        }

        private Control CreateAccountCard(EmailAccount acc, int cardW, float scale)
        {
            var card = new Panel
            {
                Width = cardW,
                Height = (int)(76 * scale),
                Margin = new Padding(0, 0, 0, (int)(10 * scale)),
                BackColor = Color.White,
                Padding = new Padding((int)(14 * scale), (int)(10 * scale), (int)(14 * scale), (int)(10 * scale))
            };

            card.Paint += (s, e) =>
            {
                using var p = new Pen(Color.FromArgb(226, 230, 234), 1);
                e.Graphics.DrawRoundedRectangle(p, new Rectangle(0, 0, card.Width - 1, card.Height - 1), 6);
            };

            var chkEnabled = new CheckBox
            {
                Checked = acc.IsEnabled,
                AutoSize = true,
                Location = new Point((int)(12 * scale), (int)(14 * scale)),
                Cursor = Cursors.Hand
            };

            int textLeft = (int)(38 * scale);

            var lblName = new Label
            {
                Text = acc.IsEnabled ? acc.Name : $"{acc.Name} (Disabled)",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = acc.IsEnabled ? Color.FromArgb(30, 30, 30) : Color.FromArgb(140, 140, 140),
                AutoSize = true,
                Location = new Point(textLeft, (int)(10 * scale))
            };

            var lblEmail = new Label
            {
                Text = string.IsNullOrEmpty(acc.Email) ? "(No email specified)" : acc.Email,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                ForeColor = acc.IsEnabled ? Color.FromArgb(70, 75, 82) : Color.FromArgb(160, 160, 160),
                AutoSize = true,
                Location = new Point(textLeft, (int)(32 * scale))
            };

            var lblHost = new Label
            {
                Text = $"{acc.Host}:{acc.Port} • {(acc.UseSsl ? "SSL" : "Plain")}",
                Font = new Font("Segoe UI", 8F, FontStyle.Regular),
                ForeColor = Color.FromArgb(140, 145, 150),
                AutoSize = true,
                Location = new Point(textLeft, (int)(52 * scale))
            };

            chkEnabled.CheckedChanged += (s, e) =>
            {
                acc.IsEnabled = chkEnabled.Checked;
                lblName.Text = acc.IsEnabled ? acc.Name : $"{acc.Name} (Disabled)";
                lblName.ForeColor = acc.IsEnabled ? Color.FromArgb(30, 30, 30) : Color.FromArgb(140, 140, 140);
                lblEmail.ForeColor = acc.IsEnabled ? Color.FromArgb(70, 75, 82) : Color.FromArgb(160, 160, 160);

                var allAccounts = _configService.GetAccounts();
                var target = allAccounts.FirstOrDefault(a => a.Id == acc.Id);
                if (target != null)
                {
                    target.IsEnabled = acc.IsEnabled;
                    _configService.SaveAccounts(allAccounts);
                }

                if (acc.IsEnabled)
                {
                    if (!_configService.Settings.AccountIds.Contains(acc.Id))
                        _configService.Settings.AccountIds.Add(acc.Id);
                }
                else
                {
                    _configService.Settings.AccountIds.Remove(acc.Id);
                }
                _configService.SaveConfig();

                AccountsChanged?.Invoke();
            };

            var btnDelete = new Button
            {
                Text = "🗑️ Remove",
                AutoSize = true,
                Padding = new Padding((int)(8 * scale), (int)(4 * scale), (int)(8 * scale), (int)(4 * scale)),
                FlatStyle = FlatStyle.System,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Right | AnchorStyles.Top,
                Location = new Point(cardW - (int)(100 * scale), (int)(20 * scale))
            };

            btnDelete.Click += (s, e) =>
            {
                var res = MessageBox.Show(this, $"Are you sure you want to remove '{acc.Name}'?\nThis will remove it from %APPDATA%\\Kerkenez\\accounts.dat.", "Confirm Remove", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (res == DialogResult.Yes)
                {
                    var all = _configService.GetAccounts();
                    all.RemoveAll(a => a.Id == acc.Id);
                    _configService.SaveAccounts(all);
                    _configService.Settings.AccountIds.Remove(acc.Id);
                    _configService.SaveConfig();
                    LoadAccounts();
                    AccountsChanged?.Invoke();
                }
            };

            card.Controls.Add(chkEnabled);
            card.Controls.Add(lblName);
            card.Controls.Add(lblEmail);
            card.Controls.Add(lblHost);
            card.Controls.Add(btnDelete);

            return card;
        }

        private void OnAddAccountClick(object? sender, EventArgs e)
        {
            using var dlg = new Form
            {
                Text = "Add Account",
                Size = new Size(420, 320),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                Font = new Font("Segoe UI", 9F),
                BackColor = Color.FromArgb(248, 249, 250)
            };

            var table = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(20), ColumnCount = 2, RowCount = 4 };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            var txtName = new TextBox { Dock = DockStyle.Fill, Text = "Work Account" };
            var txtEmail = new TextBox { Dock = DockStyle.Fill, PlaceholderText = "user@example.com" };
            var txtHost = new TextBox { Dock = DockStyle.Fill, Text = "imap.gmail.com" };

            table.Controls.Add(new Label { Text = "Name:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
            table.Controls.Add(txtName, 1, 0);
            table.Controls.Add(new Label { Text = "Email:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 1);
            table.Controls.Add(txtEmail, 1, 1);
            table.Controls.Add(new Label { Text = "IMAP Host:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 2);
            table.Controls.Add(txtHost, 1, 2);

            var btnFlow = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 48, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(12) };
            var btnCancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
            var btnSave = new Button { Text = "Save Account", DialogResult = DialogResult.OK, AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            btnFlow.Controls.Add(btnCancel);
            btnFlow.Controls.Add(btnSave);

            dlg.Controls.Add(table);
            dlg.Controls.Add(btnFlow);

            if (dlg.ShowDialog(this) == DialogResult.OK && !string.IsNullOrWhiteSpace(txtEmail.Text))
            {
                var newAcc = new EmailAccount
                {
                    Name = string.IsNullOrWhiteSpace(txtName.Text) ? "Account" : txtName.Text.Trim(),
                    Email = txtEmail.Text.Trim(),
                    Host = string.IsNullOrWhiteSpace(txtHost.Text) ? "imap.gmail.com" : txtHost.Text.Trim()
                };

                var all = _configService.GetAccounts();
                all.Add(newAcc);
                _configService.SaveAccounts(all);
                LoadAccounts();
                AccountsChanged?.Invoke();
            }
        }
    }
}
