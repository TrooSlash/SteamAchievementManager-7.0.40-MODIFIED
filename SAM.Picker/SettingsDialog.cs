using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Serilog;

namespace SAM.Picker
{
    internal class SettingsDialog : Form
    {
        public Localization.Language SelectedLanguage { get; private set; }
        public bool IsTileView { get; private set; }
        public string ApiKey { get; private set; }
        public string SelectedLogLevel { get; private set; }

        private readonly ComboBox _LanguageCombo;
        private readonly RadioButton _ListRadio;
        private readonly RadioButton _TilesRadio;
        private readonly TextBox _ApiKeyTextBox;
        private readonly ComboBox _LogLevelCombo;

        // Log level values that map to Serilog LogEventLevel names
        private static readonly string[] LogLevelValues = { "Debug", "Information", "Warning", "Error" };

        public SettingsDialog(Localization.Language currentLang, bool isTileView)
        {
            SelectedLanguage = currentLang;
            IsTileView = isTileView;
            ApiKey = AppSettings.SteamApiKey;
            SelectedLogLevel = AppSettings.LogLevel;

            this.Text = "\u2699 " + Localization.Get("Settings");
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ClientSize = new Size(420, 466);
            this.BackColor = DarkTheme.DarkBackground;
            this.ForeColor = DarkTheme.Text;
            this.Font = new Font("Segoe UI", 9f);

            // Language
            var langLabel = new Label
            {
                Text = Localization.Get("Language"),
                Location = new Point(15, 18),
                Size = new Size(80, 20),
                ForeColor = DarkTheme.Text,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            };

            _LanguageCombo = new ComboBox
            {
                Location = new Point(100, 14),
                Size = new Size(180, 23),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = DarkTheme.Surface,
                ForeColor = DarkTheme.Text,
                FlatStyle = FlatStyle.Flat,
            };
            _LanguageCombo.Items.AddRange(new object[] { "English", "\u0420\u0443\u0441\u0441\u043A\u0438\u0439" });
            _LanguageCombo.SelectedIndex = currentLang == Localization.Language.Russian ? 1 : 0;

            // View Mode
            var viewLabel = new Label
            {
                Text = Localization.Get("ViewMode"),
                Location = new Point(15, 53),
                Size = new Size(160, 20),
                ForeColor = DarkTheme.Text,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            };

            _ListRadio = new RadioButton
            {
                Text = Localization.Get("ListMode"),
                Location = new Point(20, 75),
                Size = new Size(120, 22),
                ForeColor = DarkTheme.Text,
                Checked = !isTileView,
            };

            _TilesRadio = new RadioButton
            {
                Text = Localization.Get("TilesMode"),
                Location = new Point(150, 75),
                Size = new Size(120, 22),
                ForeColor = DarkTheme.Text,
                Checked = isTileView,
            };

            // Steam API Key
            var apiLabel = new Label
            {
                Text = Localization.Get("SteamApiKey"),
                Location = new Point(15, 113),
                Size = new Size(390, 20),
                ForeColor = DarkTheme.Text,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            };

            _ApiKeyTextBox = new TextBox
            {
                Location = new Point(15, 135),
                Size = new Size(320, 23),
                BackColor = DarkTheme.Surface,
                ForeColor = DarkTheme.Text,
                BorderStyle = BorderStyle.FixedSingle,
                Text = AppSettings.SteamApiKey,
                Font = new Font("Consolas", 9f),
                UseSystemPasswordChar = true,
            };

            var showHideButton = new Button
            {
                Text = "\uD83D\uDC41",
                Location = new Point(338, 134),
                Size = new Size(30, 25),
                BackColor = DarkTheme.Surface,
                ForeColor = DarkTheme.TextSecondary,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 10f),
            };
            showHideButton.FlatAppearance.BorderColor = DarkTheme.Border;
            showHideButton.FlatAppearance.BorderSize = 1;
            showHideButton.Click += (s, e) =>
            {
                _ApiKeyTextBox.UseSystemPasswordChar = !_ApiKeyTextBox.UseSystemPasswordChar;
                Log.Debug("User toggled API key visibility: {Visible}", !_ApiKeyTextBox.UseSystemPasswordChar);
                showHideButton.ForeColor = _ApiKeyTextBox.UseSystemPasswordChar
                    ? DarkTheme.TextSecondary
                    : DarkTheme.Accent;
            };

            var testApiButton = new Button
            {
                Text = Localization.Get("TestApi"),
                Location = new Point(372, 134),
                Size = new Size(33, 25),
                BackColor = DarkTheme.Toolbar,
                ForeColor = DarkTheme.Text,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8f),
            };
            testApiButton.FlatAppearance.BorderColor = DarkTheme.Border;
            testApiButton.FlatAppearance.BorderSize = 1;
            testApiButton.Click += (s, e) =>
            {
                Log.Information("User clicked Test API key");
                string key = _ApiKeyTextBox.Text.Trim();
                if (string.IsNullOrEmpty(key))
                {
                    MessageBox.Show(this, Localization.Get("ApiKeyEmpty"),
                        Localization.Get("TestApi"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                testApiButton.Enabled = false;
                testApiButton.Text = "...";
                var worker = new System.ComponentModel.BackgroundWorker();
                worker.DoWork += (ws, we) =>
                {
                    we.Result = SteamWebApi.GetPlayerSummary(key, 76561198006409530);
                };
                worker.RunWorkerCompleted += (ws, we) =>
                {
                    testApiButton.Enabled = true;
                    testApiButton.Text = Localization.Get("TestApi");
                    if (we.Error != null || we.Result == null)
                    {
                        Log.Warning("API key test failed");
                        testApiButton.ForeColor = DarkTheme.AccentDanger;
                        MessageBox.Show(this, Localization.Get("ApiKeyInvalid"),
                            Localization.Get("TestApi"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    else
                    {
                        Log.Information("API key test succeeded");
                        testApiButton.ForeColor = DarkTheme.AccentSecondary;
                        MessageBox.Show(this, Localization.Get("ApiKeyValid"),
                            Localization.Get("TestApi"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                };
                worker.RunWorkerAsync();
            };

            var apiHint = new Label
            {
                Text = Localization.Get("SteamApiKeyHint"),
                Location = new Point(15, 162),
                Size = new Size(390, 130),
                ForeColor = DarkTheme.TextSecondary,
                Font = new Font("Segoe UI", 8f),
            };

            var apiLink = new LinkLabel
            {
                Text = "steamcommunity.com/dev/apikey",
                Location = new Point(15, 297),
                Size = new Size(390, 18),
                LinkColor = DarkTheme.Accent,
                ActiveLinkColor = DarkTheme.AccentSecondary,
                Font = new Font("Segoe UI", 8f),
            };
            apiLink.LinkClicked += (s, e) =>
            {
                Log.Debug("User clicked Steam API key link");
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://steamcommunity.com/dev/apikey",
                    UseShellExecute = true
                });
            };

            // Log Level
            var logLevelLabel = new Label
            {
                Text = Localization.Get("LogLevel"),
                Location = new Point(15, 333),
                Size = new Size(160, 20),
                ForeColor = DarkTheme.Text,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            };

            _LogLevelCombo = new ComboBox
            {
                Location = new Point(180, 329),
                Size = new Size(130, 23),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = DarkTheme.Surface,
                ForeColor = DarkTheme.Text,
                FlatStyle = FlatStyle.Flat,
            };
            _LogLevelCombo.Items.AddRange(new object[]
            {
                Localization.Get("LogLevelDebug"),
                Localization.Get("LogLevelInformation"),
                Localization.Get("LogLevelWarning"),
                Localization.Get("LogLevelError"),
            });
            // Select current log level
            int logLevelIndex = System.Array.IndexOf(LogLevelValues, AppSettings.LogLevel);
            _LogLevelCombo.SelectedIndex = logLevelIndex >= 0 ? logLevelIndex : 1;

            var openLogsButton = new Button
            {
                Text = Localization.Get("OpenLogFolder"),
                Location = new Point(320, 328),
                Size = new Size(85, 25),
                BackColor = DarkTheme.Toolbar,
                ForeColor = DarkTheme.Text,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8f),
            };
            openLogsButton.FlatAppearance.BorderColor = DarkTheme.Border;
            openLogsButton.FlatAppearance.BorderSize = 1;
            openLogsButton.Click += (s, e) =>
            {
                Log.Debug("User clicked Open Logs folder");
                string logDir = LogSetup.LogDirectory;
                if (Directory.Exists(logDir) && Directory.GetFiles(logDir).Length > 0)
                {
                    Process.Start("explorer.exe", logDir);
                }
                else
                {
                    MessageBox.Show(this, Localization.Get("NoLogsYet"),
                        Localization.Get("Information"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };

            // Buttons
            var okButton = new Button
            {
                Text = Localization.Get("OK"),
                DialogResult = DialogResult.OK,
                Location = new Point(230, 420),
                Size = new Size(80, 30),
                BackColor = DarkTheme.Accent,
                ForeColor = DarkTheme.Text,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            };
            okButton.FlatAppearance.BorderSize = 0;

            var cancelButton = new Button
            {
                Text = Localization.Get("Cancel"),
                DialogResult = DialogResult.Cancel,
                Location = new Point(320, 420),
                Size = new Size(80, 30),
                BackColor = DarkTheme.Toolbar,
                ForeColor = DarkTheme.Text,
                FlatStyle = FlatStyle.Flat,
            };
            cancelButton.FlatAppearance.BorderSize = 0;

            this.Controls.AddRange(new Control[]
            {
                langLabel, _LanguageCombo,
                viewLabel, _ListRadio, _TilesRadio,
                apiLabel, _ApiKeyTextBox, showHideButton, testApiButton, apiHint, apiLink,
                logLevelLabel, _LogLevelCombo, openLogsButton,
                okButton, cancelButton
            });

            this.AcceptButton = okButton;
            this.CancelButton = cancelButton;

            this.FormClosing += (s, e) =>
            {
                if (this.DialogResult == DialogResult.OK)
                {
                    SelectedLanguage = _LanguageCombo.SelectedIndex == 1
                        ? Localization.Language.Russian
                        : Localization.Language.English;
                    IsTileView = _TilesRadio.Checked;
                    ApiKey = _ApiKeyTextBox.Text.Trim();
                    SelectedLogLevel = LogLevelValues[_LogLevelCombo.SelectedIndex];
                }
            };
        }
    }
}
