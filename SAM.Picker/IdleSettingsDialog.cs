/* Copyright (c) 2024 Rick (rick 'at' gibbed 'dot' us)
 *
 * This software is provided 'as-is', without any express or implied
 * warranty. In no event will the authors be held liable for any damages
 * arising from the use of this software.
 *
 * Permission is granted to anyone to use this software for any purpose,
 * including commercial applications, and to alter it and redistribute it
 * freely, subject to the following restrictions:
 *
 * 1. The origin of this software must not be misrepresented; you must not
 *    claim that you wrote the original software. If you use this software
 *    in a product, an acknowledgment in the product documentation would
 *    be appreciated but is not required.
 *
 * 2. Altered source versions must be plainly marked as such, and must not
 *    be misrepresented as being the original software.
 *
 * 3. This notice may not be removed or altered from any source
 *    distribution.
 */

using System;
using System.Drawing;
using System.Windows.Forms;

namespace SAM.Picker
{
    internal enum IdleMode
    {
        Simple,
        Sequential,
        RoundRobin,
        TargetHours,
        Schedule,
        AntiIdle,
    }

    internal class IdleSettings
    {
        public IdleMode Mode { get; set; }
        public double Hours { get; set; }
        public int MaxGames { get; set; }
        public int RotateMinutes { get; set; }
        public int RestartMinutes { get; set; }
        public int ScheduleStartHour { get; set; }
        public int ScheduleStartMinute { get; set; }
        public int ScheduleEndHour { get; set; }
        public int ScheduleEndMinute { get; set; }
        public bool Indefinite { get; set; }
    }

    internal class IdleSettingsDialog : Form
    {
        public IdleSettings Settings { get; private set; }

        // Mode descriptions loaded from Localization
        private static string GetModeName(int idx) => Localization.Get("ModeName" + idx);
        private static string GetModeDescription(int idx) => Localization.Get("ModeDesc" + idx);

        private readonly ComboBox _ModeComboBox;
        private readonly Label _DescriptionLabel;
        private readonly Button _InfoButton;

        // Parameters
        private readonly Label _HoursLabel;
        private readonly NumericUpDown _HoursInput;
        private readonly CheckBox _IndefiniteCheckBox;
        private readonly Label _MaxGamesLabel;
        private readonly NumericUpDown _MaxGamesInput;
        private readonly Label _RotateLabel;
        private readonly NumericUpDown _RotateInput;
        private readonly Label _RestartLabel;
        private readonly NumericUpDown _RestartInput;
        private readonly Label _ScheduleStartLabel;
        private readonly NumericUpDown _ScheduleStartHourInput;
        private readonly NumericUpDown _ScheduleStartMinuteInput;
        private readonly Label _ScheduleEndLabel;
        private readonly NumericUpDown _ScheduleEndHourInput;
        private readonly NumericUpDown _ScheduleEndMinuteInput;
        private readonly Label _ScheduleColon1;
        private readonly Label _ScheduleColon2;

        private readonly int _AvailableGames;

        public IdleSettingsDialog(int availableGames)
        {
            _AvailableGames = availableGames;

            this.Text = Localization.Get("IdleSettings");
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ClientSize = new Size(440, 380);
            this.BackColor = DarkTheme.DarkBackground;
            this.ForeColor = DarkTheme.Text;

            // === Mode selection ===
            var modeLabel = new Label
            {
                Text = Localization.Get("IdleModeLabel"),
                Location = new Point(15, 15),
                Size = new Size(80, 20),
                ForeColor = DarkTheme.TextBright,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            };

            _ModeComboBox = new ComboBox
            {
                Location = new Point(100, 12),
                Size = new Size(270, 23),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = DarkTheme.Surface,
                ForeColor = DarkTheme.TextBright,
                FlatStyle = FlatStyle.Flat,
            };
            _ModeComboBox.Items.AddRange(new object[] {
                GetModeName(0), GetModeName(1), GetModeName(2),
                GetModeName(3), GetModeName(4), GetModeName(5)
            });
            _ModeComboBox.SelectedIndexChanged += OnModeChanged;

            _InfoButton = new Button
            {
                Text = "?",
                Location = new Point(378, 11),
                Size = new Size(28, 25),
                BackColor = DarkTheme.Accent,
                ForeColor = DarkTheme.TextBright,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Cursor = Cursors.Hand,
            };
            _InfoButton.FlatAppearance.BorderSize = 0;
            _InfoButton.Click += OnInfoClick;

            // === Description box ===
            _DescriptionLabel = new Label
            {
                Location = new Point(15, 45),
                Size = new Size(410, 72),
                ForeColor = Color.FromArgb(140, 180, 220),
                BackColor = Color.FromArgb(30, 35, 45),
                Font = new Font("Segoe UI", 8.5f),
                Padding = new Padding(8, 6, 8, 6),
                BorderStyle = BorderStyle.FixedSingle,
            };

            // === Parameters area (y = 125) ===
            int py = 128;

            _HoursLabel = new Label
            {
                Text = Localization.Get("IdleHours"),
                Location = new Point(15, py + 3),
                Size = new Size(140, 20),
                ForeColor = DarkTheme.Text,
            };

            _HoursInput = new NumericUpDown
            {
                Location = new Point(160, py),
                Size = new Size(80, 23),
                Minimum = 0.5m,
                Maximum = 9999,
                Value = 2,
                DecimalPlaces = 1,
                Increment = 0.5m,
                BackColor = DarkTheme.Surface,
                ForeColor = DarkTheme.TextBright,
            };

            _IndefiniteCheckBox = new CheckBox
            {
                Text = Localization.Get("Indefinite"),
                Location = new Point(250, py + 2),
                Size = new Size(250, 22),
                ForeColor = DarkTheme.Text,
            };
            _IndefiniteCheckBox.CheckedChanged += (s, e) =>
            {
                _HoursInput.Enabled = !_IndefiniteCheckBox.Checked;
            };

            py += 35;

            _MaxGamesLabel = new Label
            {
                Text = Localization.Get("MaxSimultaneous"),
                Location = new Point(15, py + 3),
                Size = new Size(175, 20),
                ForeColor = DarkTheme.Text,
            };

            _MaxGamesInput = new NumericUpDown
            {
                Location = new Point(195, py),
                Size = new Size(60, 23),
                Minimum = 1,
                Maximum = 32,
                Value = Math.Min(availableGames, 32),
                BackColor = DarkTheme.Surface,
                ForeColor = DarkTheme.TextBright,
            };

            py += 35;

            _RotateLabel = new Label
            {
                Text = Localization.Get("RotationInterval"),
                Location = new Point(15, py + 3),
                Size = new Size(195, 20),
                ForeColor = DarkTheme.Text,
            };

            _RotateInput = new NumericUpDown
            {
                Location = new Point(215, py),
                Size = new Size(60, 23),
                Minimum = 5,
                Maximum = 1440,
                Value = 30,
                BackColor = DarkTheme.Surface,
                ForeColor = DarkTheme.TextBright,
            };

            _RestartLabel = new Label
            {
                Text = Localization.Get("RestartEvery"),
                Location = new Point(15, py + 3),
                Size = new Size(200, 20),
                ForeColor = DarkTheme.Text,
            };

            _RestartInput = new NumericUpDown
            {
                Location = new Point(220, py),
                Size = new Size(60, 23),
                Minimum = 10,
                Maximum = 1440,
                Value = 30,
                BackColor = DarkTheme.Surface,
                ForeColor = DarkTheme.TextBright,
            };

            // Schedule controls
            _ScheduleStartLabel = new Label
            {
                Text = Localization.Get("StartHHMM"),
                Location = new Point(15, py + 3),
                Size = new Size(120, 20),
                ForeColor = DarkTheme.Text,
            };

            _ScheduleStartHourInput = new NumericUpDown
            {
                Location = new Point(140, py),
                Size = new Size(48, 23),
                Minimum = 0,
                Maximum = 23,
                Value = 23,
                BackColor = DarkTheme.Surface,
                ForeColor = DarkTheme.TextBright,
            };

            _ScheduleColon1 = new Label
            {
                Text = ":",
                Location = new Point(190, py + 3),
                Size = new Size(10, 20),
                ForeColor = DarkTheme.TextBright,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            };

            _ScheduleStartMinuteInput = new NumericUpDown
            {
                Location = new Point(202, py),
                Size = new Size(48, 23),
                Minimum = 0,
                Maximum = 59,
                Value = 0,
                BackColor = DarkTheme.Surface,
                ForeColor = DarkTheme.TextBright,
            };

            py += 30;

            _ScheduleEndLabel = new Label
            {
                Text = Localization.Get("EndHHMM"),
                Location = new Point(15, py + 3),
                Size = new Size(120, 20),
                ForeColor = DarkTheme.Text,
            };

            _ScheduleEndHourInput = new NumericUpDown
            {
                Location = new Point(140, py),
                Size = new Size(48, 23),
                Minimum = 0,
                Maximum = 23,
                Value = 7,
                BackColor = DarkTheme.Surface,
                ForeColor = DarkTheme.TextBright,
            };

            _ScheduleColon2 = new Label
            {
                Text = ":",
                Location = new Point(190, py + 3),
                Size = new Size(10, 20),
                ForeColor = DarkTheme.TextBright,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            };

            _ScheduleEndMinuteInput = new NumericUpDown
            {
                Location = new Point(202, py),
                Size = new Size(48, 23),
                Minimum = 0,
                Maximum = 59,
                Value = 0,
                BackColor = DarkTheme.Surface,
                ForeColor = DarkTheme.TextBright,
            };

            // === Info summary label ===
            var infoLabel = new Label
            {
                Text = Localization.Get("AvailableGames", availableGames),
                Location = new Point(15, 310),
                Size = new Size(410, 20),
                ForeColor = DarkTheme.Border,
                Font = new Font("Segoe UI", 8f),
            };

            // === Buttons ===
            var okButton = new Button
            {
                Text = Localization.Get("StartButton"),
                DialogResult = DialogResult.OK,
                Location = new Point(210, 340),
                Size = new Size(110, 30),
                BackColor = DarkTheme.Accent,
                ForeColor = DarkTheme.TextBright,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            };
            okButton.FlatAppearance.BorderSize = 0;

            var cancelButton = new Button
            {
                Text = Localization.Get("CancelButton"),
                DialogResult = DialogResult.Cancel,
                Location = new Point(330, 340),
                Size = new Size(85, 30),
                BackColor = DarkTheme.Toolbar,
                ForeColor = DarkTheme.Text,
                FlatStyle = FlatStyle.Flat,
            };
            cancelButton.FlatAppearance.BorderSize = 0;

            this.Controls.AddRange(new Control[]
            {
                modeLabel, _ModeComboBox, _InfoButton, _DescriptionLabel,
                _HoursLabel, _HoursInput, _IndefiniteCheckBox,
                _MaxGamesLabel, _MaxGamesInput,
                _RotateLabel, _RotateInput,
                _RestartLabel, _RestartInput,
                _ScheduleStartLabel, _ScheduleStartHourInput, _ScheduleColon1, _ScheduleStartMinuteInput,
                _ScheduleEndLabel, _ScheduleEndHourInput, _ScheduleColon2, _ScheduleEndMinuteInput,
                infoLabel, okButton, cancelButton
            });

            this.AcceptButton = okButton;
            this.CancelButton = cancelButton;

            // Default to Simple
            _ModeComboBox.SelectedIndex = 0;

            this.FormClosing += (s, e) =>
            {
                if (this.DialogResult == DialogResult.OK)
                {
                    this.Settings = new IdleSettings
                    {
                        Mode = (IdleMode)_ModeComboBox.SelectedIndex,
                        Hours = _IndefiniteCheckBox.Checked ? 0 : (double)_HoursInput.Value,
                        MaxGames = (int)_MaxGamesInput.Value,
                        RotateMinutes = (int)_RotateInput.Value,
                        RestartMinutes = (int)_RestartInput.Value,
                        ScheduleStartHour = (int)_ScheduleStartHourInput.Value,
                        ScheduleStartMinute = (int)_ScheduleStartMinuteInput.Value,
                        ScheduleEndHour = (int)_ScheduleEndHourInput.Value,
                        ScheduleEndMinute = (int)_ScheduleEndMinuteInput.Value,
                        Indefinite = _IndefiniteCheckBox.Checked,
                    };
                }
            };
        }

        private void OnModeChanged(object sender, EventArgs e)
        {
            int idx = _ModeComboBox.SelectedIndex;
            if (idx < 0 || idx > 5) return;

            _DescriptionLabel.Text = GetModeDescription(idx);
            var mode = (IdleMode)idx;

            // Show/hide controls per mode
            bool showHours = mode != IdleMode.RoundRobin;
            bool showMaxGames = mode != IdleMode.Sequential;
            bool showIndefinite = mode == IdleMode.Simple || mode == IdleMode.AntiIdle || mode == IdleMode.RoundRobin;
            bool showRotate = mode == IdleMode.RoundRobin;
            bool showRestart = mode == IdleMode.AntiIdle;
            bool showSchedule = mode == IdleMode.Schedule;

            _HoursLabel.Visible = showHours;
            _HoursInput.Visible = showHours;
            _IndefiniteCheckBox.Visible = showIndefinite;
            _MaxGamesLabel.Visible = showMaxGames;
            _MaxGamesInput.Visible = showMaxGames;
            _RotateLabel.Visible = showRotate;
            _RotateInput.Visible = showRotate;
            _RestartLabel.Visible = showRestart;
            _RestartInput.Visible = showRestart;
            _ScheduleStartLabel.Visible = showSchedule;
            _ScheduleStartHourInput.Visible = showSchedule;
            _ScheduleStartMinuteInput.Visible = showSchedule;
            _ScheduleColon1.Visible = showSchedule;
            _ScheduleEndLabel.Visible = showSchedule;
            _ScheduleEndHourInput.Visible = showSchedule;
            _ScheduleEndMinuteInput.Visible = showSchedule;
            _ScheduleColon2.Visible = showSchedule;

            // Adjust hours label text per mode
            switch (mode)
            {
                case IdleMode.Simple:
                    _HoursLabel.Text = Localization.Get("IdleHours");
                    break;
                case IdleMode.Sequential:
                    _HoursLabel.Text = Localization.Get("HoursPerGame");
                    break;
                case IdleMode.TargetHours:
                    _HoursLabel.Text = Localization.Get("TargetHoursPerBatch");
                    break;
                case IdleMode.Schedule:
                    _HoursLabel.Text = Localization.Get("IdleHoursPerSession");
                    break;
                case IdleMode.AntiIdle:
                    _HoursLabel.Text = Localization.Get("TotalIdleTime");
                    break;
            }
        }

        private void OnInfoClick(object sender, EventArgs e)
        {
            int idx = _ModeComboBox.SelectedIndex;
            if (idx < 0) return;

            string fullInfo = Localization.Get("DetailedInfo" + idx);
            MessageBox.Show(this, fullInfo, GetModeName(idx),
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

    }
}
