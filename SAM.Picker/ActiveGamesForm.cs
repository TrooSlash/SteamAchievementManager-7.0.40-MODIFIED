using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Windows.Forms;

namespace SAM.Picker
{
    internal class ActiveGamesForm : Form
    {
        private class GameEntry
        {
            public GameInfo Info;
            public Process Process;
            public DateTime StartTime;
            public bool IsPaused;
            public ListViewItem ListItem;
            public TimeSpan AccumulatedTime;
        }

        private ListView _GamesList;
        private Button _StopAllButton;
        private Button _CloseButton;
        private Timer _UpdateTimer;
        private Label _SummaryLabel;
        private readonly List<GameEntry> _Entries = new();
        private readonly IdleSettings _Settings;
        private readonly List<GameInfo> _AllGames;

        // Mode state
        private int _BatchIndex;
        private int _GameIndex;
        private DateTime _IdleStartTime;
        private DateTime _BatchStartTime;
        private DateTime _LastRestartTime;
        private bool _SchedulePaused;

        public ActiveGamesForm(List<GameInfo> games, IdleSettings settings)
        {
            _Settings = settings;
            _AllGames = games;
            InitUI();
            DarkTheme.Apply(this);
            StartIdleMode();
        }

        private void InitUI()
        {
            this.Text = Localization.Get("ActiveGamesManager");
            this.Size = new Size(700, 450);
            this.MinimumSize = new Size(550, 350);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.BackColor = DarkTheme.DarkBackground;
            this.ForeColor = DarkTheme.Text;
            this.Font = new Font("Segoe UI", 9F);

            string modeLabel = _Settings.Mode switch
            {
                IdleMode.Simple => Localization.Get("ModeSimple"),
                IdleMode.Sequential => Localization.Get("ModeSequential"),
                IdleMode.RoundRobin => Localization.Get("ModeRoundRobin"),
                IdleMode.TargetHours => Localization.Get("ModeTargetHours"),
                IdleMode.Schedule => Localization.Get("ModeSchedule"),
                IdleMode.AntiIdle => Localization.Get("ModeAntiIdle"),
                _ => Localization.Get("ModeIdle")
            };

            _SummaryLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 32,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(8, 0, 0, 0),
                BackColor = DarkTheme.Surface,
                ForeColor = DarkTheme.Text,
                Text = Localization.Get("ModeStarting", modeLabel)
            };

            _GamesList = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                MultiSelect = false,
                BackColor = DarkTheme.DarkBackground,
                ForeColor = DarkTheme.Text,
                BorderStyle = BorderStyle.None,
                HeaderStyle = ColumnHeaderStyle.Nonclickable,
                Font = new Font("Segoe UI", 9F)
            };
            _GamesList.Columns.Add(Localization.Get("ColGame"), 220);
            _GamesList.Columns.Add(Localization.Get("ColAppId"), 65, HorizontalAlignment.Center);
            _GamesList.Columns.Add(Localization.Get("ColStatus"), 90, HorizontalAlignment.Center);
            _GamesList.Columns.Add(Localization.Get("ColElapsed"), 80, HorizontalAlignment.Center);
            _GamesList.Columns.Add(Localization.Get("ColAction"), 100, HorizontalAlignment.Center);
            _GamesList.MouseClick += OnListClick;

            var buttonPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 45,
                BackColor = DarkTheme.Surface,
                Padding = new Padding(8, 8, 8, 8)
            };

            _StopAllButton = new Button
            {
                Text = Localization.Get("StopAll"),
                Width = 110,
                Height = 30,
                FlatStyle = FlatStyle.Flat,
                BackColor = DarkTheme.AccentDanger,
                ForeColor = Color.White,
                Cursor = Cursors.Hand,
                Location = new Point(8, 7)
            };
            _StopAllButton.FlatAppearance.BorderSize = 0;
            _StopAllButton.Click += OnStopAll;

            _CloseButton = new Button
            {
                Text = Localization.Get("Close"),
                Width = 80,
                Height = 30,
                FlatStyle = FlatStyle.Flat,
                BackColor = DarkTheme.Toolbar,
                ForeColor = DarkTheme.Text,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Right | AnchorStyles.Top
            };
            _CloseButton.FlatAppearance.BorderColor = DarkTheme.TextMuted;
            _CloseButton.Click += (s, e) => this.Close();

            buttonPanel.Controls.Add(_StopAllButton);
            buttonPanel.Controls.Add(_CloseButton);

            this.Controls.Add(_GamesList);
            this.Controls.Add(_SummaryLabel);
            this.Controls.Add(buttonPanel);

            this.Resize += (s, e) =>
            {
                _CloseButton.Location = new Point(buttonPanel.Width - _CloseButton.Width - 8, 7);
            };
            _CloseButton.Location = new Point(buttonPanel.Width - _CloseButton.Width - 8, 7);

            _UpdateTimer = new Timer { Interval = 1000 };
            _UpdateTimer.Tick += OnUpdateTimer;
            _UpdateTimer.Start();

            this.FormClosing += OnFormClosing;
        }

        private void StartIdleMode()
        {
            _IdleStartTime = DateTime.UtcNow;
            _BatchStartTime = DateTime.UtcNow;
            _LastRestartTime = DateTime.UtcNow;
            _BatchIndex = 0;
            _GameIndex = 0;
            _SchedulePaused = false;

            // Populate all entries (not launched yet)
            foreach (var game in _AllGames)
            {
                var entry = new GameEntry
                {
                    Info = game,
                    StartTime = DateTime.Now,
                    IsPaused = false,
                    AccumulatedTime = TimeSpan.Zero
                };

                var item = new ListViewItem(game.Name);
                item.SubItems.Add(game.Id.ToString());
                item.SubItems.Add(Localization.Get("StatusWaiting"));
                item.SubItems.Add("0:00");
                item.SubItems.Add("—");
                item.ForeColor = DarkTheme.TextMuted;
                item.BackColor = DarkTheme.DarkBackground;
                item.Tag = entry;
                entry.ListItem = item;

                _Entries.Add(entry);
                _GamesList.Items.Add(item);
            }

            // Launch initial batch based on mode
            switch (_Settings.Mode)
            {
                case IdleMode.Simple:
                    LaunchBatch(0, _Settings.MaxGames > 0 ? _Settings.MaxGames : _AllGames.Count);
                    break;
                case IdleMode.Sequential:
                    LaunchEntry(_Entries[0]);
                    break;
                case IdleMode.RoundRobin:
                case IdleMode.TargetHours:
                case IdleMode.AntiIdle:
                    LaunchBatch(0, _Settings.MaxGames > 0 ? _Settings.MaxGames : _AllGames.Count);
                    break;
                case IdleMode.Schedule:
                    if (IsWithinSchedule())
                    {
                        LaunchBatch(0, _Settings.MaxGames > 0 ? _Settings.MaxGames : _AllGames.Count);
                    }
                    else
                    {
                        _SchedulePaused = true;
                    }
                    break;
            }

            UpdateSummary();
        }

        private void LaunchBatch(int startIndex, int count)
        {
            int end = Math.Min(startIndex + count, _Entries.Count);
            for (int i = startIndex; i < end; i++)
            {
                LaunchEntry(_Entries[i]);
            }
            _BatchStartTime = DateTime.UtcNow;
        }

        private void LaunchEntry(GameEntry entry)
        {
            if (entry.Process != null) return;
            try
            {
                Environment.SetEnvironmentVariable("SAM_LANGUAGE", Localization.Current == Localization.Language.Russian ? "Russian" : "English");
                var psi = new ProcessStartInfo
                {
                    FileName = "SAM.Game.exe",
                    Arguments = $"{entry.Info.Id.ToString(CultureInfo.InvariantCulture)} --idle",
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true
                };
                entry.Process = Process.Start(psi);
            }
            catch
            {
                entry.Process = null;
            }

            entry.StartTime = DateTime.Now;
            entry.IsPaused = false;
            if (entry.Process != null)
            {
                entry.ListItem.SubItems[2].Text = Localization.Get("StatusRunning");
                entry.ListItem.SubItems[4].Text = Localization.Get("ActionPauseStop");
                entry.ListItem.ForeColor = DarkTheme.AccentSecondary;
            }
            else
            {
                entry.ListItem.SubItems[2].Text = Localization.Get("StatusFailed");
                entry.ListItem.SubItems[4].Text = "—";
                entry.ListItem.ForeColor = DarkTheme.AccentDanger;
            }
        }

        private void KillBatchEntries(int startIndex, int count)
        {
            int end = Math.Min(startIndex + count, _Entries.Count);
            for (int i = startIndex; i < end; i++)
            {
                var entry = _Entries[i];
                if (entry.Process != null && !entry.IsPaused)
                {
                    entry.AccumulatedTime += DateTime.Now - entry.StartTime;
                }
                KillProcess(entry);
                entry.Process = null;
                entry.IsPaused = false;
                entry.ListItem.SubItems[2].Text = Localization.Get("StatusWaiting");
                entry.ListItem.SubItems[4].Text = "—";
                entry.ListItem.ForeColor = DarkTheme.TextMuted;
            }
        }

        private void KillAllRunning()
        {
            foreach (var entry in _Entries)
            {
                if (entry.Process != null)
                {
                    if (!entry.IsPaused)
                        entry.AccumulatedTime += DateTime.Now - entry.StartTime;
                    KillProcess(entry);
                    entry.Process = null;
                }
            }
        }

        private void OnListClick(object sender, MouseEventArgs e)
        {
            var hit = _GamesList.HitTest(e.Location);
            if (hit.Item == null) return;

            var entry = hit.Item.Tag as GameEntry;
            if (entry == null || entry.Process == null) return;

            int actionColLeft = 0;
            int actionColIdx = 4;
            for (int c = 0; c < actionColIdx && c < _GamesList.Columns.Count; c++)
                actionColLeft += _GamesList.Columns[c].Width;

            if (e.X < actionColLeft) return;

            int actionColMid = actionColLeft + _GamesList.Columns[actionColIdx].Width / 2;

            if (e.X < actionColMid)
                TogglePause(entry);
            else
                StopGame(entry);
        }

        private void TogglePause(GameEntry entry)
        {
            if (entry.IsPaused)
            {
                try
                {
                    Environment.SetEnvironmentVariable("SAM_LANGUAGE", Localization.Current == Localization.Language.Russian ? "Russian" : "English");
                    var psi = new ProcessStartInfo
                    {
                        FileName = "SAM.Game.exe",
                        Arguments = $"{entry.Info.Id.ToString(CultureInfo.InvariantCulture)} --idle",
                        WindowStyle = ProcessWindowStyle.Hidden,
                        CreateNoWindow = true
                    };
                    entry.Process = Process.Start(psi);
                    entry.StartTime = DateTime.Now;
                    entry.IsPaused = false;
                    entry.ListItem.SubItems[2].Text = Localization.Get("StatusRunning");
                    entry.ListItem.SubItems[4].Text = Localization.Get("ActionPauseStop");
                    entry.ListItem.ForeColor = DarkTheme.AccentSecondary;
                }
                catch { }
            }
            else
            {
                entry.AccumulatedTime += DateTime.Now - entry.StartTime;
                KillProcess(entry);
                entry.IsPaused = true;
                entry.ListItem.SubItems[2].Text = Localization.Get("StatusPaused");
                entry.ListItem.SubItems[4].Text = Localization.Get("ActionResumeStop");
                entry.ListItem.ForeColor = DarkTheme.AccentWarning;
            }

            UpdateSummary();
        }

        private void StopGame(GameEntry entry)
        {
            if (!entry.IsPaused)
            {
                entry.AccumulatedTime += DateTime.Now - entry.StartTime;
            }
            KillProcess(entry);
            entry.Process = null;
            entry.IsPaused = false;
            entry.ListItem.SubItems[2].Text = Localization.Get("StatusStopped");
            entry.ListItem.SubItems[4].Text = "—";
            entry.ListItem.ForeColor = DarkTheme.TextMuted;
            UpdateSummary();
        }

        private void KillProcess(GameEntry entry)
        {
            if (entry.Process == null) return;
            try
            {
                if (!entry.Process.HasExited)
                {
                    // Signal graceful shutdown via named event
                    string eventName = $"Local\\SAM_Idle_Stop_{entry.Info.Id}";
                    try
                    {
                        using var stopEvent = System.Threading.EventWaitHandle.OpenExisting(eventName);
                        stopEvent.Set();
                    }
                    catch { }

                    // Wait for graceful exit (3 sec), then force kill
                    if (!entry.Process.WaitForExit(3000))
                    {
                        try { entry.Process.Kill(); } catch { }
                        entry.Process.WaitForExit(1000);
                    }
                }
            }
            catch { }
            finally
            {
                try { entry.Process?.Dispose(); } catch { }
            }
        }

        private void OnStopAll(object sender, EventArgs e)
        {
            foreach (var entry in _Entries)
            {
                if (entry.Process != null)
                {
                    StopGame(entry);
                }
            }
        }

        private void OnUpdateTimer(object sender, EventArgs e)
        {
            // Update elapsed times for running entries
            foreach (var entry in _Entries)
            {
                if (entry.Process == null) continue;

                try
                {
                    if (entry.Process.HasExited && !entry.IsPaused)
                    {
                        entry.AccumulatedTime += DateTime.Now - entry.StartTime;
                        try { entry.Process.Dispose(); } catch { }
                        entry.Process = null;
                        entry.ListItem.SubItems[2].Text = Localization.Get("StatusExited");
                        entry.ListItem.SubItems[4].Text = "—";
                        entry.ListItem.ForeColor = DarkTheme.AccentDanger;
                        continue;
                    }
                }
                catch { continue; }

                if (!entry.IsPaused)
                {
                    var elapsed = entry.AccumulatedTime + (DateTime.Now - entry.StartTime);
                    entry.ListItem.SubItems[3].Text = FormatElapsed(elapsed);
                }
            }

            // Mode-specific logic
            var totalElapsed = DateTime.UtcNow - _IdleStartTime;
            var batchElapsed = DateTime.UtcNow - _BatchStartTime;

            switch (_Settings.Mode)
            {
                case IdleMode.Simple:
                    TickSimple(totalElapsed);
                    break;
                case IdleMode.Sequential:
                    TickSequential(batchElapsed);
                    break;
                case IdleMode.RoundRobin:
                    TickRoundRobin(batchElapsed, totalElapsed);
                    break;
                case IdleMode.TargetHours:
                    TickTargetHours(batchElapsed);
                    break;
                case IdleMode.Schedule:
                    TickSchedule(totalElapsed);
                    break;
                case IdleMode.AntiIdle:
                    TickAntiIdle(totalElapsed);
                    break;
            }

            UpdateSummary();
        }

        private void TickSimple(TimeSpan elapsed)
        {
            if (CountRunning() == 0)
            {
                AutoStop();
                return;
            }
            if (!_Settings.Indefinite && _Settings.Hours > 0 && elapsed.TotalHours >= _Settings.Hours)
            {
                AutoStop();
            }
        }

        private void TickSequential(TimeSpan batchElapsed)
        {
            if (batchElapsed.TotalHours >= _Settings.Hours)
            {
                // Stop current, move to next
                if (_GameIndex < _Entries.Count)
                    StopGame(_Entries[_GameIndex]);

                _GameIndex++;
                if (_GameIndex >= _Entries.Count)
                {
                    AutoStop();
                    return;
                }

                LaunchEntry(_Entries[_GameIndex]);
                _BatchStartTime = DateTime.UtcNow;
            }

            // Restart if process died
            if (_GameIndex < _Entries.Count && _Entries[_GameIndex].Process == null
                && _Entries[_GameIndex].ListItem.SubItems[2].Text != Localization.Get("StatusStopped"))
            {
                LaunchEntry(_Entries[_GameIndex]);
            }
        }

        private void TickRoundRobin(TimeSpan batchElapsed, TimeSpan totalElapsed)
        {
            int batchSize = _Settings.MaxGames > 0 ? _Settings.MaxGames : _Entries.Count;

            if (batchElapsed.TotalMinutes >= _Settings.RotateMinutes)
            {
                KillBatchEntries(_BatchIndex, batchSize);
                _BatchIndex += batchSize;
                if (_BatchIndex >= _Entries.Count)
                    _BatchIndex = 0;
                LaunchBatch(_BatchIndex, batchSize);
            }

            if (!_Settings.Indefinite && _Settings.Hours > 0 && totalElapsed.TotalHours >= _Settings.Hours)
            {
                AutoStop();
            }
        }

        private void TickTargetHours(TimeSpan batchElapsed)
        {
            int batchSize = _Settings.MaxGames > 0 ? _Settings.MaxGames : _Entries.Count;

            if (batchElapsed.TotalHours >= _Settings.Hours)
            {
                KillBatchEntries(_BatchIndex, batchSize);
                _BatchIndex += batchSize;

                if (_BatchIndex >= _Entries.Count)
                {
                    AutoStop();
                    return;
                }

                LaunchBatch(_BatchIndex, batchSize);
            }

            // Restart dead processes in current batch
            int end = Math.Min(_BatchIndex + batchSize, _Entries.Count);
            for (int i = _BatchIndex; i < end; i++)
            {
                var entry = _Entries[i];
                if (entry.Process == null && entry.ListItem.SubItems[2].Text != Localization.Get("StatusStopped"))
                {
                    LaunchEntry(entry);
                }
            }
        }

        private void TickSchedule(TimeSpan totalElapsed)
        {
            bool inWindow = IsWithinSchedule();
            int batchSize = _Settings.MaxGames > 0 ? _Settings.MaxGames : _Entries.Count;

            if (inWindow && _SchedulePaused)
            {
                _SchedulePaused = false;
                LaunchBatch(0, batchSize);
            }
            else if (!inWindow && !_SchedulePaused)
            {
                // Kill all running and update status to show they are paused due to schedule
                foreach (var entry in _Entries)
                {
                    if (entry.Process != null)
                    {
                        if (!entry.IsPaused)
                            entry.AccumulatedTime += DateTime.Now - entry.StartTime;
                        KillProcess(entry);
                        entry.Process = null;
                        entry.ListItem.SubItems[2].Text = Localization.Get("StatusWaiting");
                        entry.ListItem.SubItems[4].Text = "—";
                        entry.ListItem.ForeColor = DarkTheme.TextMuted;
                    }
                }
                _SchedulePaused = true;
            }

            if (!_SchedulePaused && !_Settings.Indefinite && _Settings.Hours > 0)
            {
                var batchElapsed = DateTime.UtcNow - _BatchStartTime;
                if (batchElapsed.TotalHours >= _Settings.Hours)
                {
                    AutoStop();
                }
            }
        }

        private void TickAntiIdle(TimeSpan totalElapsed)
        {
            int batchSize = _Settings.MaxGames > 0 ? _Settings.MaxGames : _Entries.Count;
            var sinceLast = DateTime.UtcNow - _LastRestartTime;

            if (sinceLast.TotalMinutes >= _Settings.RestartMinutes)
            {
                KillAllRunning();
                _UpdateTimer.Stop();
                var restartTimer = new Timer { Interval = 2000 };
                restartTimer.Tick += (rs, re) =>
                {
                    restartTimer.Stop();
                    restartTimer.Dispose();
                    // Re-launch all entries from scratch
                    foreach (var entry in _Entries)
                    {
                        entry.Process = null;
                        entry.AccumulatedTime = TimeSpan.Zero;
                    }
                    LaunchBatch(0, batchSize);
                    _LastRestartTime = DateTime.UtcNow;
                    _UpdateTimer.Start();
                };
                restartTimer.Start();
                return;
            }

            if (!_Settings.Indefinite && _Settings.Hours > 0 && totalElapsed.TotalHours >= _Settings.Hours)
            {
                AutoStop();
            }
        }

        private bool IsWithinSchedule()
        {
            var now = DateTime.Now;
            int nowMin = now.Hour * 60 + now.Minute;
            int startMin = _Settings.ScheduleStartHour * 60 + _Settings.ScheduleStartMinute;
            int endMin = _Settings.ScheduleEndHour * 60 + _Settings.ScheduleEndMinute;

            // Use <= for endMin to include the final minute of the schedule
            if (startMin <= endMin)
                return nowMin >= startMin && nowMin <= endMin;
            else
                return nowMin >= startMin || nowMin <= endMin;
        }

        private void AutoStop()
        {
            OnStopAll(null, null);
            _UpdateTimer.Stop();
            _SummaryLabel.Text = Localization.Get("IdleComplete");
        }

        private int CountRunning()
        {
            int count = 0;
            foreach (var entry in _Entries)
            {
                if (entry.Process != null && !entry.IsPaused)
                {
                    try { if (!entry.Process.HasExited) count++; } catch { }
                }
            }
            return count;
        }

        private void UpdateSummary()
        {
            int running = 0, paused = 0, stopped = 0;
            foreach (var entry in _Entries)
            {
                if (entry.Process != null && !entry.IsPaused)
                {
                    try { if (!entry.Process.HasExited) { running++; continue; } } catch { }
                }
                if (entry.IsPaused) paused++;
                else if (entry.Process == null) stopped++;
            }

            var totalElapsed = DateTime.UtcNow - _IdleStartTime;
            string time = FormatElapsed(totalElapsed);

            string modeInfo = _Settings.Mode switch
            {
                IdleMode.Simple => $"\U0001F3AE {Localization.Get("ModeSimple")}",
                IdleMode.Sequential => $"\U0001F4CB {Localization.Get("ModeSequential")} ({_GameIndex + 1}/{_Entries.Count})",
                IdleMode.RoundRobin =>
                    $"\U0001F504 {Localization.Get("ModeRoundRobin")} (batch {(_BatchIndex / Math.Max(_Settings.MaxGames, 1)) + 1}" +
                    $"/{(int)Math.Ceiling((double)_Entries.Count / Math.Max(_Settings.MaxGames, 1))})",
                IdleMode.TargetHours =>
                    $"\U0001F3AF {Localization.Get("ModeTargetHours")} (batch {(_BatchIndex / Math.Max(_Settings.MaxGames, 1)) + 1}" +
                    $"/{(int)Math.Ceiling((double)_Entries.Count / Math.Max(_Settings.MaxGames, 1))})",
                IdleMode.Schedule =>
                    $"\U0001F550 {Localization.Get("ModeSchedule")} ({_Settings.ScheduleStartHour:D2}:{_Settings.ScheduleStartMinute:D2}" +
                    $"\u2014{_Settings.ScheduleEndHour:D2}:{_Settings.ScheduleEndMinute:D2})" +
                    (_SchedulePaused ? " \u23F8" : ""),
                IdleMode.AntiIdle => $"\U0001F6E1 {Localization.Get("ModeAntiIdle")}",
                _ => Localization.Get("ModeIdle")
            };

            _SummaryLabel.Text = $"  {modeInfo}  |  ▶ {running}  ⏸ {paused}  ⏹ {stopped}  |  ⏱ {time}";
        }

        private static string FormatElapsed(TimeSpan ts)
        {
            if (ts.TotalHours >= 1)
                return $"{(int)ts.TotalHours}:{ts.Minutes:D2}:{ts.Seconds:D2}";
            return $"{ts.Minutes}:{ts.Seconds:D2}";
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            _UpdateTimer.Stop();

            bool hasRunning = false;
            foreach (var entry in _Entries)
            {
                if (entry.Process != null)
                {
                    try { if (!entry.Process.HasExited) { hasRunning = true; break; } } catch { }
                }
            }

            if (hasRunning)
            {
                var result = MessageBox.Show(this,
                    Localization.Get("ConfirmCloseMessage"),
                    Localization.Get("ConfirmClose"),
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (result == DialogResult.No)
                {
                    e.Cancel = true;
                    _UpdateTimer.Start();
                    return;
                }

                OnStopAll(null, null);
            }

            // Clean up orphaned appmanifest files created during idle
            CleanupOrphanedManifests();

            _UpdateTimer.Dispose();
        }

        private void CleanupOrphanedManifests()
        {
            try
            {
                string steamPath = API.Steam.GetInstallPath();
                if (string.IsNullOrEmpty(steamPath)) return;

                // Collect all steamapps directories (main + library folders)
                var steamappsDirs = new List<string>();
                string mainSteamapps = Path.Combine(steamPath, "steamapps");
                if (Directory.Exists(mainSteamapps))
                    steamappsDirs.Add(mainSteamapps);

                // Check libraryfolders.vdf for additional library paths
                string libraryVdf = Path.Combine(mainSteamapps, "libraryfolders.vdf");
                if (File.Exists(libraryVdf))
                {
                    foreach (var line in File.ReadLines(libraryVdf))
                    {
                        string trimmed = line.Trim();
                        if (trimmed.StartsWith("\"path\""))
                        {
                            int firstQuote = trimmed.IndexOf('"', 6);
                            int lastQuote = trimmed.LastIndexOf('"');
                            if (firstQuote >= 0 && lastQuote > firstQuote)
                            {
                                string libPath = trimmed.Substring(firstQuote + 1, lastQuote - firstQuote - 1)
                                    .Replace("\\\\", "\\");
                                string libSteamapps = Path.Combine(libPath, "steamapps");
                                if (Directory.Exists(libSteamapps) && !steamappsDirs.Contains(libSteamapps))
                                    steamappsDirs.Add(libSteamapps);
                            }
                        }
                    }
                }

                // For each idled game, check if manifest exists without actual game files
                var idledAppIds = new HashSet<uint>();
                foreach (var entry in _Entries)
                    idledAppIds.Add(entry.Info.Id);

                foreach (var dir in steamappsDirs)
                {
                    foreach (var appId in idledAppIds)
                    {
                        string manifest = Path.Combine(dir, $"appmanifest_{appId}.acf");
                        if (!File.Exists(manifest)) continue;

                        // Read installdir from manifest
                        string installDir = null;
                        foreach (var line in File.ReadLines(manifest))
                        {
                            string t = line.Trim();
                            if (t.StartsWith("\"installdir\""))
                            {
                                int fq = t.IndexOf('"', 12);
                                int lq = t.LastIndexOf('"');
                                if (fq >= 0 && lq > fq)
                                    installDir = t.Substring(fq + 1, lq - fq - 1);
                                break;
                            }
                        }

                        if (string.IsNullOrEmpty(installDir)) continue;

                        string fullInstallPath = Path.Combine(dir, "common", installDir);

                        // Delete manifest only if install directory is empty or doesn't exist
                        bool isEmpty = !Directory.Exists(fullInstallPath) ||
                                       Directory.GetFileSystemEntries(fullInstallPath).Length == 0;

                        if (isEmpty)
                        {
                            try
                            {
                                File.Delete(manifest);
                                if (Directory.Exists(fullInstallPath))
                                    Directory.Delete(fullInstallPath, false);
                            }
                            catch { }
                        }
                    }
                }
            }
            catch { }
        }
    }
}
