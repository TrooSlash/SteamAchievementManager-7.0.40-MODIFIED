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
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Windows.Forms;
using Serilog;
using static SAM.Game.InvariantShorthand;
using APITypes = SAM.API.Types;

namespace SAM.Game
{
    internal partial class Manager : Form
    {
        private readonly long _GameId;
        private readonly API.Client _SteamClient;

        private readonly WebClient _IconDownloader = new();

        private readonly List<Stats.AchievementInfo> _IconQueue = new();
        private readonly List<Stats.StatDefinition> _StatDefinitions = new();

        private readonly List<Stats.AchievementDefinition> _AchievementDefinitions = new();

        private readonly BindingList<Stats.StatInfo> _Statistics = new();

        private Dictionary<string, float> _GlobalPercentages;
        private bool _IsVacProtected;
        private bool _VacClickWarned;

        private readonly ToolTip _AchievementToolTip = new() { InitialDelay = 400, ReshowDelay = 200 };
        private ListViewItem _LastTooltipItem;
        private bool _AllProtectedWarningShown;

        private readonly API.Callbacks.UserStatsReceived _UserStatsReceivedCallback;

        //private API.Callback<APITypes.UserStatsStored> UserStatsStoredCallback;

        public Manager(long gameId, API.Client client)
        {
            this.InitializeComponent();

            DarkTheme.Apply(this);
            this.Font = new Font("Segoe UI", 9f);
            this.ApplyLocalization();

            this._MainTabControl.SelectedTab = this._AchievementsTabPage;

            this._AchievementImageList.Images.Add("Blank", new Bitmap(32, 32));

            this._StatisticsDataGridView.AutoGenerateColumns = false;

            this._StatisticsDataGridView.Columns.Add("name", GameLocalization.Get("Name"));
            this._StatisticsDataGridView.Columns[0].ReadOnly = true;
            this._StatisticsDataGridView.Columns[0].Width = 200;
            this._StatisticsDataGridView.Columns[0].DataPropertyName = "DisplayName";

            this._StatisticsDataGridView.Columns.Add("value", GameLocalization.Get("Value"));
            this._StatisticsDataGridView.Columns[1].ReadOnly = this._EnableStatsEditingCheckBox.Checked == false;
            this._StatisticsDataGridView.Columns[1].Width = 90;
            this._StatisticsDataGridView.Columns[1].DataPropertyName = "Value";

            this._StatisticsDataGridView.Columns.Add("extra", GameLocalization.Get("Extra"));
            this._StatisticsDataGridView.Columns[2].ReadOnly = true;
            this._StatisticsDataGridView.Columns[2].Width = 200;
            this._StatisticsDataGridView.Columns[2].DataPropertyName = "Extra";

            this._StatisticsDataGridView.DataSource = new BindingSource()
            {
                DataSource = this._Statistics,
            };

            this._GameId = gameId;
            this._SteamClient = client;

            this._IconDownloader.DownloadDataCompleted += this.OnIconDownload;
            this._AchievementListView.MouseMove += this.OnAchievementMouseMove;

            string name = this._SteamClient.SteamApps001.GetAppData((uint)this._GameId, "name");
            if (name != null)
            {
                base.Text += " | " + name;
            }
            else
            {
                base.Text += " | " + this._GameId.ToString(CultureInfo.InvariantCulture);
            }

            Log.Information("Manager created for AppId {AppId}, game name: {GameName}", this._GameId, name ?? "(unknown)");

            this._UserStatsReceivedCallback = client.CreateAndRegisterCallback<API.Callbacks.UserStatsReceived>();
            this._UserStatsReceivedCallback.OnRun += this.OnUserStatsReceived;

            //this.UserStatsStoredCallback = new API.Callback(1102, new API.Callback.CallbackFunction(this.OnUserStatsStored));

            this.RefreshStats();
            this.FetchGlobalPercentages();
            this.CheckVacStatus();
        }

        private void UpdateTitleProtectedCount(int protectedCount)
        {
            string marker = $" | \u26A0 {protectedCount} {GameLocalization.Get("ProtectedAchievements")}";
            if (base.Text.Contains("\u26A0") == false)
            {
                base.Text += marker;
            }
        }

        private void CheckVacStatus()
        {
            var worker = new BackgroundWorker();
            worker.DoWork += (s, e) =>
            {
                try
                {
                    using (var client = new WebClient())
                    {
                        string url = string.Format(
                            "https://store.steampowered.com/api/appdetails?appids={0}&filters=categories",
                            this._GameId);
                        string json = client.DownloadString(url);

                        // Check for VAC category (id 8) or anti-cheat mentions
                        bool hasVac = false;

                        // Category 8 = Valve Anti-Cheat enabled
                        var catBlocks = System.Text.RegularExpressions.Regex.Matches(
                            json, @"\{[^{}]*""id""\s*:\s*(\d+)[^{}]*\}");
                        foreach (System.Text.RegularExpressions.Match block in catBlocks)
                        {
                            if (block.Groups[1].Value == "8")
                            {
                                hasVac = true;
                                break;
                            }
                        }

                        // Also check for EAC/BattlEye in description text
                        if (!hasVac)
                        {
                            string lower = json.ToLowerInvariant();
                            if (lower.Contains("easy anti-cheat") ||
                                lower.Contains("easyanticheat") ||
                                lower.Contains("battleye") ||
                                lower.Contains("anti-cheat"))
                            {
                                hasVac = true;
                            }
                        }

                        Log.Information("VAC status check for AppId {AppId}: {IsVacProtected}", this._GameId, hasVac);
                        e.Result = hasVac;
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to check VAC status for AppId {AppId}", this._GameId);
                    e.Result = false;
                }
            };
            worker.RunWorkerCompleted += (s, e) =>
            {
                if (e.Result is bool isVac && isVac)
                {
                    this._IsVacProtected = true;
                    ShowVacWarning();
                }
            };
            worker.RunWorkerAsync();
        }

        private void ShowVacWarning()
        {
            if (!this.IsHandleCreated) return;
            this._VacWarningPanel.Visible = true;
        }

        private void OnVacOverride(object sender, EventArgs e)
        {
            Log.Information("User clicked VAC Override for AppId {AppId}", this._GameId);
            var result = MessageBox.Show(
                this,
                GameLocalization.Get("VacOverrideConfirm"),
                "⚠ " + GameLocalization.Get("VacDetected"),
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result == DialogResult.Yes)
            {
                this._IsVacProtected = false;

                this._VacOverrideButton.Enabled = false;
                this._VacOverrideButton.Text = GameLocalization.Get("VacOverrideDone");

                this._VacWarningLabel.Text = GameLocalization.Get("VacWarningOverridden");
                this._VacWarningPanel.BackColor = Color.FromArgb(60, 50, 10);
                this._VacWarningLabel.ForeColor = Color.FromArgb(255, 220, 120);
            }
        }

        private void FetchGlobalPercentages()
        {
            var worker = new BackgroundWorker();
            worker.DoWork += (s, e) =>
            {
                try
                {
                    string url = string.Format(
                        "https://api.steampowered.com/ISteamUserStats/GetGlobalAchievementPercentagesForApp/v0002/?gameid={0}",
                        this._GameId);
                    using (var client = new WebClient())
                    {
                        string json = client.DownloadString(url);
                        var result = new Dictionary<string, float>();
                        var blocks = System.Text.RegularExpressions.Regex.Matches(
                            json, @"\{[^}]*""name""\s*:\s*""([^""]*)""\s*,\s*""percent""\s*:\s*""?([\d.]+)""?[^}]*\}");
                        foreach (System.Text.RegularExpressions.Match block in blocks)
                        {
                            string name = block.Groups[1].Value;
                            if (float.TryParse(block.Groups[2].Value,
                                System.Globalization.NumberStyles.Float,
                                CultureInfo.InvariantCulture, out float pct))
                            {
                                if (!result.ContainsKey(name))
                                    result[name] = pct;
                            }
                        }
                        e.Result = result;
                    }
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "Failed to fetch global achievement percentages for AppId {AppId}", this._GameId);
                    e.Result = null;
                }
            };
            worker.RunWorkerCompleted += (s, e) =>
            {
                if (e.Result is Dictionary<string, float> data)
                {
                    this._GlobalPercentages = data;
                    UpdateGlobalPercentColumn();
                }
            };
            worker.RunWorkerAsync();
        }

        private void UpdateGlobalPercentColumn()
        {
            if (this._GlobalPercentages == null) return;
            foreach (ListViewItem item in this._AchievementListView.Items)
            {
                if (item.Tag is Stats.AchievementInfo info)
                {
                    string pctText = "—";
                    if (this._GlobalPercentages.TryGetValue(info.Id, out float pct))
                    {
                        pctText = pct.ToString("F1", CultureInfo.InvariantCulture) + "%";
                    }
                    if (item.SubItems.Count >= 4)
                        item.SubItems[3].Text = pctText;
                }
            }
        }

        private void AddAchievementIcon(Stats.AchievementInfo info, Image icon)
        {
            if (icon == null)
            {
                info.ImageIndex = 0;
            }
            else
            {
                info.ImageIndex = this._AchievementImageList.Images.Count;
                this._AchievementImageList.Images.Add(info.IsAchieved == true ? info.IconNormal : info.IconLocked, icon);
            }
        }

        private void OnIconDownload(object sender, DownloadDataCompletedEventArgs e)
        {
            if (e.Error == null && e.Cancelled == false)
            {
                var info = (Stats.AchievementInfo)e.UserState;

                Bitmap bitmap;
                try
                {
                    using (var stream = new MemoryStream(e.Result, false))
                    {
                        bitmap = new Bitmap(stream);
                    }
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "Failed to decode icon bitmap for achievement");
                    bitmap = null;
                }

                this.AddAchievementIcon(info, bitmap);
                this._AchievementListView.Update();
            }
            else if (e.Error != null)
            {
                Log.Debug(e.Error, "Icon download failed");
            }

            this.DownloadNextIcon();
        }

        private void DownloadNextIcon()
        {
            if (this._IconQueue.Count == 0)
            {
                this._DownloadStatusLabel.Visible = false;
                return;
            }

            if (this._IconDownloader.IsBusy == true)
            {
                return;
            }

            this._DownloadStatusLabel.Text = GameLocalization.Get("DownloadingIcons", this._IconQueue.Count);
            this._DownloadStatusLabel.Visible = true;

            var info = this._IconQueue[0];
            this._IconQueue.RemoveAt(0);


            this._IconDownloader.DownloadDataAsync(
                new Uri(_($"https://cdn.steamstatic.com/steamcommunity/public/images/apps/{this._GameId}/{(info.IsAchieved == true ? info.IconNormal : info.IconLocked)}")),
                info);
        }

        private static string TranslateError(int id) => id switch
        {
            2 => GameLocalization.Get("GenericError"),
            _ => _($"{id}"),
        };

        private static string GetLocalizedString(KeyValue kv, string language, string defaultValue)
        {
            var name = kv[language].AsString("");
            if (string.IsNullOrEmpty(name) == false)
            {
                return name;
            }

            if (language != "english")
            {
                name = kv["english"].AsString("");
                if (string.IsNullOrEmpty(name) == false)
                {
                    return name;
                }
            }

            name = kv.AsString("");
            if (string.IsNullOrEmpty(name) == false)
            {
                return name;
            }

            return defaultValue;
        }

        private bool LoadUserGameStatsSchema()
        {
            string path;
            try
            {
                string fileName = _($"UserGameStatsSchema_{this._GameId}.bin");
                path = API.Steam.GetInstallPath();
                path = Path.Combine(path, "appcache", "stats", fileName);
                if (File.Exists(path) == false)
                {
                    Log.Warning("User game stats schema file not found at {Path} for AppId {AppId}", path, this._GameId);
                    return false;
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to locate user game stats schema for AppId {AppId}", this._GameId);
                return false;
            }

            var kv = KeyValue.LoadAsBinary(path);
            if (kv == null)
            {
                Log.Warning("Failed to parse user game stats schema from {Path} for AppId {AppId}", path, this._GameId);
                return false;
            }

            var currentLanguage = this._SteamClient.SteamApps008.GetCurrentGameLanguage();

            this._AchievementDefinitions.Clear();
            this._StatDefinitions.Clear();

            var stats = kv[this._GameId.ToString(CultureInfo.InvariantCulture)]["stats"];
            if (stats.Valid == false || stats.Children == null)
            {
                Log.Warning("Stats node is invalid or has no children in schema for AppId {AppId}", this._GameId);
                return false;
            }

            foreach (var stat in stats.Children)
            {
                if (stat.Valid == false)
                {
                    continue;
                }

                APITypes.UserStatType type;

                // schema in the new format?
                var typeNode = stat["type"];
                if (typeNode.Valid == true && typeNode.Type == KeyValueType.String)
                {
                    if (Enum.TryParse((string)typeNode.Value, true, out type) == false)
                    {
                        type = APITypes.UserStatType.Invalid;
                    }
                }
                else
                {
                    type = APITypes.UserStatType.Invalid;
                }

                // schema in the old format?
                if (type == APITypes.UserStatType.Invalid)
                {
                    var typeIntNode = stat["type_int"];
                    var rawType = typeIntNode.Valid == true
                        ? typeIntNode.AsInteger(0)
                        : typeNode.AsInteger(0);
                    type = (APITypes.UserStatType)rawType;
                }

                switch (type)
                {
                    case APITypes.UserStatType.Invalid:
                    {
                        break;
                    }

                    case APITypes.UserStatType.Integer:
                    {
                        var id = stat["name"].AsString("");
                        string name = GetLocalizedString(stat["display"]["name"], currentLanguage, id);

                        this._StatDefinitions.Add(new Stats.IntegerStatDefinition()
                        {
                            Id = stat["name"].AsString(""),
                            DisplayName = name,
                            MinValue = stat["min"].AsInteger(int.MinValue),
                            MaxValue = stat["max"].AsInteger(int.MaxValue),
                            MaxChange = stat["maxchange"].AsInteger(0),
                            IncrementOnly = stat["incrementonly"].AsBoolean(false),
                            SetByTrustedGameServer = stat["bSetByTrustedGS"].AsBoolean(false),
                            DefaultValue = stat["default"].AsInteger(0),
                            Permission = stat["permission"].AsInteger(0),
                        });
                        break;
                    }

                    case APITypes.UserStatType.Float:
                    case APITypes.UserStatType.AverageRate:
                    {
                        var id = stat["name"].AsString("");
                        string name = GetLocalizedString(stat["display"]["name"], currentLanguage, id);

                        this._StatDefinitions.Add(new Stats.FloatStatDefinition()
                        {
                            Id = stat["name"].AsString(""),
                            DisplayName = name,
                            MinValue = stat["min"].AsFloat(float.MinValue),
                            MaxValue = stat["max"].AsFloat(float.MaxValue),
                            MaxChange = stat["maxchange"].AsFloat(0.0f),
                            IncrementOnly = stat["incrementonly"].AsBoolean(false),
                            DefaultValue = stat["default"].AsFloat(0.0f),
                            Permission = stat["permission"].AsInteger(0),
                        });
                        break;
                    }

                    case APITypes.UserStatType.Achievements:
                    case APITypes.UserStatType.GroupAchievements:
                    {
                        if (stat.Children != null)
                        {
                            foreach (var bits in stat.Children.Where(
                                b => string.Compare(b.Name, "bits", StringComparison.InvariantCultureIgnoreCase) == 0))
                            {
                                if (bits.Valid == false ||
                                    bits.Children == null)
                                {
                                    continue;
                                }

                                foreach (var bit in bits.Children)
                                {
                                    string id = bit["name"].AsString("");
                                    string name = GetLocalizedString(bit["display"]["name"], currentLanguage, id);
                                    string desc = GetLocalizedString(bit["display"]["desc"], currentLanguage, "");

                                    this._AchievementDefinitions.Add(new()
                                    {
                                        Id = id,
                                        Name = name,
                                        Description = desc,
                                        IconNormal = bit["display"]["icon"].AsString(""),
                                        IconLocked = bit["display"]["icon_gray"].AsString(""),
                                        IsHidden = bit["display"]["hidden"].AsBoolean(false),
                                        Permission = bit["permission"].AsInteger(0),
                                    });
                                }
                            }
                        }

                        break;
                    }

                    default:
                    {
                        throw new InvalidOperationException("invalid stat type");
                    }
                }
            }

            return true;
        }

        private void OnUserStatsReceived(APITypes.UserStatsReceived param)
        {
            if (param.Result != 1)
            {
                Log.Error("Failed to receive user stats for AppId {AppId}, result code: {ResultCode}", this._GameId, param.Result);
                this._GameStatusLabel.Text = GameLocalization.Get("ErrorRetrievingStats", TranslateError(param.Result));
                this.EnableInput();
                return;
            }

            if (this.LoadUserGameStatsSchema() == false)
            {
                Log.Warning("Failed to load user game stats schema for AppId {AppId}", this._GameId);
                this._GameStatusLabel.Text = GameLocalization.Get("FailedLoadSchema");
                this.EnableInput();
                return;
            }

            try
            {
                this.GetAchievements();
            }
            catch (Exception e)
            {
                Log.Error(e, "Error retrieving achievements for AppId {AppId}", this._GameId);
                this._GameStatusLabel.Text = GameLocalization.Get("ErrorAchievementsRetrieval");
                this.EnableInput();
                MessageBox.Show(
                    GameLocalization.Get("ErrorAchievementsRetrieval") + ":\n" + e,
                    GameLocalization.Get("Error"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            try
            {
                this.GetStatistics();
            }
            catch (Exception e)
            {
                Log.Error(e, "Error retrieving statistics for AppId {AppId}", this._GameId);
                this._GameStatusLabel.Text = GameLocalization.Get("ErrorStatsRetrieval");
                this.EnableInput();
                MessageBox.Show(
                    GameLocalization.Get("ErrorStatsRetrieval") + ":\n" + e,
                    GameLocalization.Get("Error"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            if (this._Statistics.Count == 0)
            {
                this._MainTabControl.TabPages.Remove(this._StatisticsTabPage);
            }
            else if (!this._MainTabControl.TabPages.Contains(this._StatisticsTabPage))
            {
                this._MainTabControl.TabPages.Add(this._StatisticsTabPage);
            }

            // Count protected achievements and update title bar
            int protectedCount = 0;
            foreach (ListViewItem item in this._AchievementListView.Items)
            {
                if (item.Tag is Stats.AchievementInfo ai && (ai.Permission & 3) != 0)
                {
                    protectedCount++;
                }
            }

            Log.Information("Successfully received user stats for AppId {AppId}: {AchievementCount} achievements ({ProtectedCount} protected), {StatCount} statistics",
                this._GameId, this._AchievementListView.Items.Count, protectedCount, this._StatisticsDataGridView.Rows.Count);

            if (protectedCount > 0)
            {
                this.UpdateTitleProtectedCount(protectedCount);
                API.ProtectedGamesCache.MarkProtected((uint)this._GameId);
            }

            int totalAchievements = this._AchievementListView.Items.Count;

            // Warn if ALL achievements are protected — nothing can be modified (show once per session)
            if (protectedCount > 0 && protectedCount == totalAchievements && !this._AllProtectedWarningShown)
            {
                this._AllProtectedWarningShown = true;
                Log.Warning("All {Count} achievements are protected for AppId {AppId}", protectedCount, this._GameId);
                MessageBox.Show(
                    this,
                    GameLocalization.Get("AllProtectedWarning"),
                    "\u26A0 " + GameLocalization.Get("Information"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }

            this._GameStatusLabel.Text = GameLocalization.Get("RetrievedStats", totalAchievements, this._StatisticsDataGridView.Rows.Count);
            this.EnableInput();
        }

        private void RefreshStats()
        {
            this._AchievementListView.Items.Clear();
            this._StatisticsDataGridView.Rows.Clear();

            var steamId = this._SteamClient.SteamUser.GetSteamId();

            // This still triggers the UserStatsReceived callback, in addition to the callresult.
            // No need to implement callresults for the time being.
            var callHandle = this._SteamClient.SteamUserStats.RequestUserStats(steamId);
            if (callHandle == API.CallHandle.Invalid)
            {
                MessageBox.Show(this, "Failed.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            this._GameStatusLabel.Text = GameLocalization.Get("RetrievingStats");
            this.DisableInput();
        }

        private bool _IsUpdatingAchievementList;

        private void GetAchievements()
        {
            var textSearch = this._MatchingStringTextBox.Text.Length > 0
                ? this._MatchingStringTextBox.Text
                : null;

            this._IsUpdatingAchievementList = true;

            this._AchievementListView.Items.Clear();
            this._AchievementListView.BeginUpdate();
            //this.Achievements.Clear();

            bool wantLocked = this._DisplayLockedOnlyButton.Checked == true;
            bool wantUnlocked = this._DisplayUnlockedOnlyButton.Checked == true;
            bool hideProtected = this._HideProtectedButton.Checked == true;

            foreach (var def in this._AchievementDefinitions)
            {
                if (string.IsNullOrEmpty(def.Id) == true)
                {
                    continue;
                }

                if (this._SteamClient.SteamUserStats.GetAchievementAndUnlockTime(
                    def.Id,
                    out bool isAchieved,
                    out var unlockTime) == false)
                {
                    continue;
                }

                bool wanted = (wantLocked == false && wantUnlocked == false) || isAchieved switch
                {
                    true => wantUnlocked,
                    false => wantLocked,
                };
                if (wanted == false)
                {
                    continue;
                }

                if (hideProtected && (def.Permission & 3) != 0)
                {
                    continue;
                }

                if (textSearch != null)
                {
                    if (def.Name.IndexOf(textSearch, StringComparison.OrdinalIgnoreCase) < 0 &&
                        def.Description.IndexOf(textSearch, StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }
                }

                Stats.AchievementInfo info = new()
                {
                    Id = def.Id,
                    IsAchieved = isAchieved,
                    IsCheckedInUI = isAchieved,
                    UnlockTime = isAchieved == true && unlockTime > 0
                        ? DateTimeOffset.FromUnixTimeSeconds(unlockTime).LocalDateTime
                        : null,
                    IconNormal = string.IsNullOrEmpty(def.IconNormal) ? null : def.IconNormal,
                    IconLocked = string.IsNullOrEmpty(def.IconLocked) ? def.IconNormal : def.IconLocked,
                    Permission = def.Permission,
                    Name = def.Name,
                    Description = def.Description,
                };

                ListViewItem item = new()
                {
                    Tag = info,
                    Text = info.Name,
                };

                info.Item = item;

                if (item.Text.StartsWith("#", StringComparison.InvariantCulture) == true)
                {
                    item.Text = info.Id;
                    item.SubItems.Add("");
                }
                else
                {
                    item.SubItems.Add(info.Description);
                }

                item.SubItems.Add(info.UnlockTime.HasValue == true
                    ? info.UnlockTime.Value.ToString()
                    : "");

                // Global unlock percentage
                string pctText = "—";
                if (this._GlobalPercentages != null && this._GlobalPercentages.TryGetValue(def.Id, out float pct))
                {
                    pctText = pct.ToString("F1", CultureInfo.InvariantCulture) + "%";
                }
                item.SubItems.Add(pctText);

                info.ImageIndex = 0;

                this.AddAchievementToIconQueue(info, false);
                this._AchievementListView.Items.Add(item);
            }

            this._AchievementListView.EndUpdate();
            this._IsUpdatingAchievementList = false;

            this.DownloadNextIcon();
        }

        private void GetStatistics()
        {
            this._Statistics.Clear();
            foreach (var stat in this._StatDefinitions)
            {
                if (string.IsNullOrEmpty(stat.Id) == true)
                {
                    continue;
                }

                if (stat is Stats.IntegerStatDefinition intStat)
                {
                    if (this._SteamClient.SteamUserStats.GetStatValue(intStat.Id, out int value) == false)
                    {
                        continue;
                    }
                    this._Statistics.Add(new Stats.IntStatInfo()
                    {
                        Id = intStat.Id,
                        DisplayName = intStat.DisplayName,
                        IntValue = value,
                        OriginalValue = value,
                        IsIncrementOnly = intStat.IncrementOnly,
                        Permission = intStat.Permission,
                    });
                }
                else if (stat is Stats.FloatStatDefinition floatStat)
                {
                    if (this._SteamClient.SteamUserStats.GetStatValue(floatStat.Id, out float value) == false)
                    {
                        continue;
                    }
                    this._Statistics.Add(new Stats.FloatStatInfo()
                    {
                        Id = floatStat.Id,
                        DisplayName = floatStat.DisplayName,
                        FloatValue = value,
                        OriginalValue = value,
                        IsIncrementOnly = floatStat.IncrementOnly,
                        Permission = floatStat.Permission,
                    });
                }
            }
        }

        private void AddAchievementToIconQueue(Stats.AchievementInfo info, bool startDownload)
        {
            int imageIndex = this._AchievementImageList.Images.IndexOfKey(
                info.IsAchieved == true ? info.IconNormal : info.IconLocked);

            if (imageIndex >= 0)
            {
                info.ImageIndex = imageIndex;
            }
            else
            {
                this._IconQueue.Add(info);

                if (startDownload == true)
                {
                    this.DownloadNextIcon();
                }
            }
        }

        private int StoreAchievements()
        {
            if (this._AchievementListView.Items.Count == 0)
            {
                return 0;
            }

            List<Stats.AchievementInfo> achievements = new();
            foreach (ListViewItem item in this._AchievementListView.Items)
            {
                if (item.Tag is not Stats.AchievementInfo achievementInfo ||
                    achievementInfo.IsAchieved == achievementInfo.IsCheckedInUI)
                {
                    continue;
                }

                achievementInfo.IsAchieved = achievementInfo.IsCheckedInUI;
                achievements.Add(achievementInfo);
            }

            if (achievements.Count == 0)
            {
                return 0;
            }

            foreach (var info in achievements)
            {
                if (this._SteamClient.SteamUserStats.SetAchievement(info.Id, info.IsAchieved) == false)
                {
                    Log.Error("Failed to set achievement {AchievementId} to {IsAchieved} for AppId {AppId}", info.Id, info.IsAchieved, this._GameId);
                    MessageBox.Show(
                        this,
                        GameLocalization.Get("ErrorSettingState", info.Id),
                        GameLocalization.Get("Error"),
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return -1;
                }
            }

            Log.Information("Storing {AchievementCount} achievement change(s) for AppId {AppId}", achievements.Count, this._GameId);
            return achievements.Count;
        }

        private int StoreStatistics()
        {
            if (this._Statistics.Count == 0)
            {
                return 0;
            }

            var statistics = this._Statistics.Where(stat => stat.IsModified == true).ToList();
            if (statistics.Count == 0)
            {
                return 0;
            }

            foreach (var stat in statistics)
            {
                if (stat is Stats.IntStatInfo intStat)
                {
                    if (this._SteamClient.SteamUserStats.SetStatValue(
                        intStat.Id,
                        intStat.IntValue) == false)
                    {
                        Log.Error("Failed to set integer stat {StatId} for AppId {AppId}", stat.Id, this._GameId);
                        MessageBox.Show(
                            this,
                            GameLocalization.Get("ErrorSettingValue", stat.Id),
                            GameLocalization.Get("Error"),
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                        return -1;
                    }
                }
                else if (stat is Stats.FloatStatInfo floatStat)
                {
                    if (this._SteamClient.SteamUserStats.SetStatValue(
                        floatStat.Id,
                        floatStat.FloatValue) == false)
                    {
                        Log.Error("Failed to set float stat {StatId} for AppId {AppId}", stat.Id, this._GameId);
                        MessageBox.Show(
                            this,
                            GameLocalization.Get("ErrorSettingValue", stat.Id),
                            GameLocalization.Get("Error"),
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                        return -1;
                    }
                }
                else
                {
                    throw new InvalidOperationException("unsupported stat type");
                }
            }

            Log.Information("Storing {StatCount} statistic change(s) for AppId {AppId}", statistics.Count, this._GameId);
            return statistics.Count;
        }

        private void DisableInput()
        {
            this._ReloadButton.Enabled = false;
            this._StoreButton.Enabled = false;
        }

        private void EnableInput()
        {
            this._ReloadButton.Enabled = true;
            this._StoreButton.Enabled = true;
        }

        private void OnTimer(object sender, EventArgs e)
        {
            this._CallbackTimer.Enabled = false;
            this._SteamClient.RunCallbacks(false);
            this._CallbackTimer.Enabled = true;
        }

        private void OnRefresh(object sender, EventArgs e)
        {
            Log.Information("User clicked Refresh for AppId {AppId}", this._GameId);
            this.RefreshStats();
        }

        private void OnLockAll(object sender, EventArgs e)
        {
            Log.Information("User clicked Lock All for AppId {AppId}", this._GameId);
            if (this._IsVacProtected && !ConfirmVacAction()) return;

            foreach (ListViewItem item in this._AchievementListView.Items)
            {
                if (item.Tag is Stats.AchievementInfo info && (info.Permission & 3) == 0)
                    info.IsCheckedInUI = false;
            }
            this._AchievementListView.Invalidate();
        }

        private void OnInvertAll(object sender, EventArgs e)
        {
            Log.Information("User clicked Invert All for AppId {AppId}", this._GameId);
            if (this._IsVacProtected && !ConfirmVacAction()) return;

            foreach (ListViewItem item in this._AchievementListView.Items)
            {
                if (item.Tag is Stats.AchievementInfo info && (info.Permission & 3) == 0)
                    info.IsCheckedInUI = !info.IsCheckedInUI;
            }
            this._AchievementListView.Invalidate();
        }

        private void OnUnlockAll(object sender, EventArgs e)
        {
            Log.Information("User clicked Unlock All for AppId {AppId}", this._GameId);
            if (this._IsVacProtected)
            {
                var result = MessageBox.Show(
                    this,
                    GameLocalization.Get("VacUnlockAllWarning"),
                    "⚠ " + GameLocalization.Get("VacDetected"),
                    MessageBoxButtons.YesNoCancel,
                    MessageBoxIcon.Warning);

                if (result == DialogResult.Cancel)
                    return;

                if (result == DialogResult.No)
                {
                    // Unlock all but skip — do nothing, user declined
                    return;
                }

                // DialogResult.Yes — user accepts risks, proceed
            }

            foreach (ListViewItem item in this._AchievementListView.Items)
            {
                if (item.Tag is Stats.AchievementInfo info && (info.Permission & 3) == 0)
                    info.IsCheckedInUI = true;
            }
            this._AchievementListView.Invalidate();
        }

        private bool ConfirmVacAction()
        {
            return MessageBox.Show(
                this,
                GameLocalization.Get("VacActionWarning"),
                "⚠ " + GameLocalization.Get("VacDetected"),
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) == DialogResult.Yes;
        }

        private bool Store()
        {
            if (this._SteamClient.SteamUserStats.StoreStats() == false)
            {
                Log.Error("StoreStats call failed for AppId {AppId}", this._GameId);
                MessageBox.Show(
                    this,
                    GameLocalization.Get("ErrorStoring"),
                    GameLocalization.Get("Error"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return false;
            }

            Log.Information("StoreStats succeeded for AppId {AppId}", this._GameId);
            return true;
        }

        private void OnStore(object sender, EventArgs e)
        {
            Log.Information("User clicked Store for AppId {AppId}", this._GameId);
            if (this._IsVacProtected)
            {
                var result = MessageBox.Show(
                    this,
                    GameLocalization.Get("VacStoreWarning"),
                    "⚠ " + GameLocalization.Get("VacDetected"),
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);
                if (result == DialogResult.No) return;
            }

            int achievements = this.StoreAchievements();
            if (achievements < 0)
            {
                this.RefreshStats();
                return;
            }

            int stats = this.StoreStatistics();
            if (stats < 0)
            {
                this.RefreshStats();
                return;
            }

            if (this.Store() == false)
            {
                this.RefreshStats();
                return;
            }

            MessageBox.Show(
                this,
                GameLocalization.Get("StoredStats", achievements, stats),
                GameLocalization.Get("Information"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            this.RefreshStats();
        }

        private void OnStatDataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            if (e.Context != DataGridViewDataErrorContexts.Commit)
            {
                return;
            }

            var view = (DataGridView)sender;
            if (e.Exception is Stats.StatIsProtectedException)
            {
                e.ThrowException = false;
                e.Cancel = true;
                view.Rows[e.RowIndex].ErrorText = GameLocalization.Get("StatProtected");
            }
            else
            {
                e.ThrowException = false;
                e.Cancel = true;
                view.Rows[e.RowIndex].ErrorText = GameLocalization.Get("InvalidValue");
            }
        }

        private void OnStatAgreementChecked(object sender, EventArgs e)
        {
            Log.Information("User toggled stats editing: {Enabled} for AppId {AppId}", this._EnableStatsEditingCheckBox.Checked, this._GameId);
            if (this._IsVacProtected && this._EnableStatsEditingCheckBox.Checked)
            {
                if (!ConfirmVacAction())
                {
                    this._EnableStatsEditingCheckBox.Checked = false;
                    return;
                }
            }
            this._StatisticsDataGridView.Columns[1].ReadOnly = this._EnableStatsEditingCheckBox.Checked == false;
        }

        private void OnStatCellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            var view = (DataGridView)sender;
            view.Rows[e.RowIndex].ErrorText = "";
        }

        private void OnResetAllStats(object sender, EventArgs e)
        {
            if (MessageBox.Show(
                GameLocalization.Get("ConfirmResetStats"),
                GameLocalization.Get("Warning"),
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) == DialogResult.No)
            {
                return;
            }

            bool achievementsToo = DialogResult.Yes == MessageBox.Show(
                GameLocalization.Get("ConfirmResetAchievements"),
                GameLocalization.Get("Question"),
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (MessageBox.Show(
                GameLocalization.Get("ReallyReallySure"),
                GameLocalization.Get("Warning"),
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Error) == DialogResult.No)
            {
                return;
            }

            Log.Warning("Resetting all stats for AppId {AppId}, includeAchievements: {IncludeAchievements}", this._GameId, achievementsToo);

            if (this._SteamClient.SteamUserStats.ResetAllStats(achievementsToo) == false)
            {
                MessageBox.Show(this, "Failed.", GameLocalization.Get("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            this.RefreshStats();
        }

        private void OnDisplayUncheckedOnly(object sender, EventArgs e)
        {
            Log.Debug("User toggled filter: show unlocked only");
            if ((sender as ToolStripButton).Checked == true)
            {
                this._DisplayLockedOnlyButton.Checked = false;
            }

            this.GetAchievements();
        }

        private void OnDisplayCheckedOnly(object sender, EventArgs e)
        {
            Log.Debug("User toggled filter: show locked only");
            if ((sender as ToolStripButton).Checked == true)
            {
                this._DisplayUnlockedOnlyButton.Checked = false;
            }

            this.GetAchievements();
        }

        private void OnHideProtectedClick(object sender, EventArgs e)
        {
            Log.Debug("User toggled filter: hide protected = {State}", this._HideProtectedButton.Checked);
            this.GetAchievements();
        }

        private void OnFilterUpdate(object sender, KeyEventArgs e)
        {
            Log.Debug("Achievement filter updated");
            this.GetAchievements();
        }

        private void OnAchievementMouseMove(object sender, MouseEventArgs e)
        {
            var hit = this._AchievementListView.HitTest(e.Location);
            if (hit.Item == this._LastTooltipItem) return;

            this._LastTooltipItem = hit.Item;

            if (hit.Item?.Tag is Stats.AchievementInfo info && (info.Permission & 3) != 0)
            {
                this._AchievementToolTip.SetToolTip(this._AchievementListView,
                    GameLocalization.Get("ProtectedTooltip"));
            }
            else
            {
                this._AchievementToolTip.SetToolTip(this._AchievementListView, null);
            }
        }

        private void OnAchievementMouseClick(object sender, MouseEventArgs e)
        {
            var hit = this._AchievementListView.HitTest(e.Location);
            if (hit.Item == null) return;

            // Check if click is in checkbox area (first ~22px of the row)
            if (e.X < hit.Item.Bounds.X + 22)
            {
                if (this._IsUpdatingAchievementList) return;

                if (this._IsVacProtected && !this._VacClickWarned)
                {
                    var result = MessageBox.Show(
                        this,
                        GameLocalization.Get("VacClickWarning"),
                        "⚠ " + GameLocalization.Get("VacDetected"),
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning);
                    if (result == DialogResult.No) return;
                    this._VacClickWarned = true;
                }

                if (hit.Item.Tag is Stats.AchievementInfo info)
                {
                    if ((info.Permission & 3) != 0)
                    {
                        MessageBox.Show(
                            this,
                            GameLocalization.Get("ProtectedAchievement"),
                            GameLocalization.Get("Error"),
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                        return;
                    }

                    info.IsCheckedInUI = !info.IsCheckedInUI;
                    Log.Debug("Achievement toggled: {Name} -> {State}", info.Id, info.IsCheckedInUI ? "unlocked" : "locked");
                    this._AchievementListView.Invalidate();
                }
            }
        }

        private void OnDrawAchievementColumnHeader(object sender, DrawListViewColumnHeaderEventArgs e)
        {
            using (var brush = new SolidBrush(DarkTheme.Toolbar))
                e.Graphics.FillRectangle(brush, e.Bounds);
            using (var pen = new Pen(DarkTheme.Border))
                e.Graphics.DrawLine(pen, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);

            var textBounds = new Rectangle(e.Bounds.X + 4, e.Bounds.Y, e.Bounds.Width - 8, e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, e.Header.Text, this.Font, textBounds, DarkTheme.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        private void OnDrawAchievementItem(object sender, DrawListViewItemEventArgs e)
        {
            bool isChecked = e.Item.Tag is Stats.AchievementInfo a && a.IsCheckedInUI;
            bool isProtected = e.Item.Tag is Stats.AchievementInfo ap && (ap.Permission & 3) != 0;
            bool isOddRow = e.ItemIndex % 2 == 1;

            Color bgColor;
            if (e.Item.Selected)
                bgColor = DarkTheme.Selection;
            else if (isProtected)
                bgColor = DarkTheme.DangerBackground;
            else if (isOddRow)
                bgColor = Color.FromArgb(28, 30, 38);
            else
                bgColor = DarkTheme.DarkBackground;

            using (var brush = new SolidBrush(bgColor))
                e.Graphics.FillRectangle(brush, e.Bounds);
        }

        private void OnDrawAchievementSubItem(object sender, DrawListViewSubItemEventArgs e)
        {
            bool isSelected = e.Item.Selected;
            bool isChecked = e.Item.Tag is Stats.AchievementInfo aic && aic.IsCheckedInUI;
            bool isProtected = e.Item.Tag is Stats.AchievementInfo ai && (ai.Permission & 3) != 0;
            bool isOddRow = e.ItemIndex % 2 == 1;

            // Row background — match game list style
            Color bgColor;
            if (isSelected)
                bgColor = DarkTheme.Selection;
            else if (isProtected)
                bgColor = DarkTheme.DangerBackground;
            else if (isOddRow)
                bgColor = Color.FromArgb(28, 30, 38); // alternating row
            else
                bgColor = DarkTheme.DarkBackground;

            using (var brush = new SolidBrush(bgColor))
                e.Graphics.FillRectangle(brush, e.Bounds);

            // Subtle separator
            using (var pen = new Pen(Color.FromArgb(35, 38, 46)))
                e.Graphics.DrawLine(pen, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);

            if (e.ColumnIndex == 0)
            {
                // Draw custom checkbox
                int checkSize = 14;
                int checkY = e.Bounds.Y + (e.Bounds.Height - checkSize) / 2;
                int checkX = e.Bounds.X + 4;
                var checkRect = new Rectangle(checkX, checkY, checkSize, checkSize);

                if (isChecked)
                {
                    using (var brush = new SolidBrush(DarkTheme.AccentSecondary))
                        e.Graphics.FillRectangle(brush, checkRect);
                    using (var pen = new Pen(Color.White, 2f))
                    {
                        e.Graphics.DrawLine(pen, checkX + 3, checkY + 7, checkX + 6, checkY + 10);
                        e.Graphics.DrawLine(pen, checkX + 6, checkY + 10, checkX + 11, checkY + 4);
                    }
                }
                else
                {
                    using (var pen = new Pen(DarkTheme.TextMuted, 1f))
                        e.Graphics.DrawRectangle(pen, checkRect);
                }

                // Draw icon
                int iconX = checkX + checkSize + 6;
                int iconSize = 32;
                int iconY = e.Bounds.Y + (e.Bounds.Height - iconSize) / 2;

                if (e.Item.Tag is Stats.AchievementInfo info && info.ImageIndex >= 0 &&
                    info.ImageIndex < this._AchievementImageList.Images.Count)
                {
                    var img = this._AchievementImageList.Images[info.ImageIndex];
                    e.Graphics.DrawImage(img, iconX, iconY, iconSize, iconSize);

                    // Draw lock overlay for protected achievements
                    if (isProtected)
                    {
                        DrawLockOverlay(e.Graphics, iconX, iconY, iconSize);
                    }
                }

                // Draw name text (with lock prefix for protected)
                int textX = iconX + iconSize + 6;
                int textWidth = e.Bounds.Right - textX;
                if (textWidth > 0)
                {
                    string displayName = isProtected
                        ? "\U0001F512 " + e.Item.Text
                        : e.Item.Text;
                    var textRect = new Rectangle(textX, e.Bounds.Y, textWidth, e.Bounds.Height);
                    Color textColor = isProtected
                        ? DarkTheme.ProtectedText
                        : isChecked ? DarkTheme.AccentSecondary : DarkTheme.Text;
                    TextRenderer.DrawText(e.Graphics, displayName, this.Font, textRect, textColor,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                }
            }
            else
            {
                Color textColor = e.ColumnIndex == 2 ? DarkTheme.TextSecondary : DarkTheme.Text;
                var textRect = new Rectangle(e.Bounds.X + 4, e.Bounds.Y, e.Bounds.Width - 8, e.Bounds.Height);
                string text = e.SubItem?.Text ?? "";
                TextRenderer.DrawText(e.Graphics, text, this.Font, textRect, textColor,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
        }

        private static void DrawLockOverlay(Graphics g, int iconX, int iconY, int iconSize)
        {
            // Small lock icon in bottom-right corner of the achievement icon
            int lockSize = 14;
            int lx = iconX + iconSize - lockSize - 1;
            int ly = iconY + iconSize - lockSize - 1;

            // Background circle
            using (var bgBrush = new SolidBrush(Color.FromArgb(200, 30, 30, 30)))
                g.FillEllipse(bgBrush, lx - 1, ly - 1, lockSize + 2, lockSize + 2);

            // Lock body
            int bodyW = 8, bodyH = 6;
            int bodyX = lx + (lockSize - bodyW) / 2;
            int bodyY = ly + lockSize - bodyH - 2;
            using (var brush = new SolidBrush(Color.FromArgb(220, 180, 60)))
                g.FillRectangle(brush, bodyX, bodyY, bodyW, bodyH);

            // Lock shackle (arc)
            int shackleW = 6, shackleH = 5;
            int shackleX = bodyX + (bodyW - shackleW) / 2;
            int shackleY = bodyY - shackleH + 1;
            using (var pen = new Pen(Color.FromArgb(220, 180, 60), 1.5f))
                g.DrawArc(pen, shackleX, shackleY, shackleW, shackleH * 2, 180, 180);
        }

        private void ApplyLocalization()
        {
            this._StoreButton.Text = GameLocalization.Get("CommitChanges");
            this._ReloadButton.Text = GameLocalization.Get("Refresh");
            this._ResetButton.Text = GameLocalization.Get("Reset");
            this._AchievementsTabPage.Text = GameLocalization.Get("Achievements");
            this._StatisticsTabPage.Text = GameLocalization.Get("Statistics");
            this._AchievementNameColumnHeader.Text = GameLocalization.Get("Name");
            this._AchievementDescriptionColumnHeader.Text = GameLocalization.Get("Description");
            this._AchievementUnlockTimeColumnHeader.Text = GameLocalization.Get("UnlockTime");
            this._AchievementGlobalPercentColumnHeader.Text = GameLocalization.Get("GlobalPercent");
            this._DisplayLabel.Text = GameLocalization.Get("ShowOnly");
            this._DisplayLockedOnlyButton.Text = GameLocalization.Get("Locked");
            this._DisplayUnlockedOnlyButton.Text = GameLocalization.Get("Unlocked");
            this._HideProtectedButton.Text = GameLocalization.Get("HideProtected");
            this._MatchingStringLabel.Text = GameLocalization.Get("Filter");
            this._LockAllButton.Text = GameLocalization.Get("LockAll");
            this._InvertAllButton.Text = GameLocalization.Get("InvertAll");
            this._UnlockAllButton.Text = GameLocalization.Get("UnlockAll");
            this._DownloadStatusLabel.Text = GameLocalization.Get("DownloadStatus");
            this._EnableStatsEditingCheckBox.Text = GameLocalization.Get("StatsEditingAgreement");
            this._VacWarningLabel.Text = GameLocalization.Get("VacWarningText");
            this._VacOverrideButton.Text = GameLocalization.Get("VacOverrideBtn");
        }
    }
}
