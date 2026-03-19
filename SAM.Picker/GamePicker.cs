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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Windows.Forms;
using System.Xml.XPath;
using static SAM.Picker.InvariantShorthand;
using APITypes = SAM.API.Types;

namespace SAM.Picker
{
    internal partial class GamePicker : Form
    {
        private readonly API.Client _SteamClient;

        private readonly Dictionary<uint, GameInfo> _Games;
        private readonly List<GameInfo> _FilteredGames;

        private readonly object _LogoLock;
        private readonly HashSet<string> _LogosAttempting;
        private readonly HashSet<string> _LogosAttempted;
        private readonly ConcurrentQueue<GameInfo> _LogoQueue;

        private readonly API.Callbacks.AppDataChanged _AppDataChangedCallback;

        private bool _IsListView = true;
        private int _SortColumn = 0;
        private bool _SortAscending = true;
        private bool _SuppressItemCheck = false;
        private int _ActiveLogoDownloads;
        private const int MaxParallelLogoDownloads = 6;

        public GamePicker(API.Client client)
        {
            this._Games = new();
            this._FilteredGames = new();
            this._LogoLock = new();
            this._LogosAttempting = new();
            this._LogosAttempted = new();
            this._LogoQueue = new();

            this.InitializeComponent();

            DarkTheme.Apply(this);

            this._GameListView.ItemCheck += OnGameListItemCheck;
            this._GameListView.ColumnClick += OnColumnClick;

            Bitmap blank = new(this._LogoImageList.ImageSize.Width, this._LogoImageList.ImageSize.Height);
            using (var g = Graphics.FromImage(blank))
            {
                g.Clear(Color.DimGray);
            }

            this._LogoImageList.Images.Add("Blank", blank);

            Bitmap blankSmall = new(32, 32);
            using (var g = Graphics.FromImage(blankSmall))
            {
                g.Clear(Color.DimGray);
            }
            this._SmallIconImageList.Images.Add("Blank", blankSmall);

            this._SteamClient = client;

            AppSettings.Load();

            this._AppDataChangedCallback = client.CreateAndRegisterCallback<API.Callbacks.AppDataChanged>();
            this._AppDataChangedCallback.OnRun += this.OnAppDataChanged;

            this.AddGames();
            this.LoadProfileAsync();
        }

        private void LoadProfileAsync()
        {
            string apiKey = AppSettings.SteamApiKey;
            if (string.IsNullOrWhiteSpace(apiKey)) return;

            ulong steamId = this._SteamClient.SteamUser.GetSteamId();

            var worker = new BackgroundWorker();
            worker.DoWork += (s, e) =>
            {
                var summary = SteamWebApi.GetPlayerSummary(apiKey, steamId);
                var level = SteamWebApi.GetSteamLevel(apiKey, steamId);
                var badges = SteamWebApi.GetBadges(apiKey, steamId);
                Bitmap avatar = null;
                if (summary.HasValue && !string.IsNullOrEmpty(summary.Value.AvatarFullUrl))
                {
                    avatar = SteamWebApi.DownloadImage(summary.Value.AvatarFullUrl);
                }
                e.Result = new object[] { summary, level, badges, avatar };
            };
            worker.RunWorkerCompleted += (s, e) =>
            {
                if (e.Error != null || e.Result == null) return;
                var results = (object[])e.Result;
                var summary = (PlayerSummary?)results[0];
                var level = (int?)results[1];
                var badges = (BadgeInfo?)results[2];
                var avatar = (Bitmap)results[3];

                if (summary.HasValue && badges.HasValue)
                {
                    this._ProfilePanel.SetData(summary.Value, level ?? 0, badges.Value);
                    if (avatar != null)
                    {
                        this._ProfilePanel.SetAvatar(avatar);
                    }
                }
            };
            worker.RunWorkerAsync();
        }

        private void OnAppDataChanged(APITypes.AppDataChanged param)
        {
            if (param.Result == false)
            {
                return;
            }

            if (this._Games.TryGetValue(param.Id, out var game) == false)
            {
                return;
            }

            game.Name = this._SteamClient.SteamApps001.GetAppData(game.Id, "name");

            this.AddGameToLogoQueue(game);
            this.DownloadNextLogo();
        }

        private void DoDownloadList(object sender, DoWorkEventArgs e)
        {
            // Phase 1: Fast local scan — show games immediately
            if (this.IsHandleCreated)
            {
                this.BeginInvoke((Action)(() => this._PickerStatusLabel.Text = Localization.Get("ScanningLocalGames")));
            }
            this.ScanLocalSteamGames();

            // Load playtime data early
            try
            {
                ulong steamId = this._SteamClient.SteamUser.GetSteamId();
                this._PlaytimeData = PlaytimeReader.Read(steamId);
                foreach (var kv in this._PlaytimeData)
                {
                    if (this._Games.TryGetValue(kv.Key, out var game))
                    {
                        game.PlaytimeMinutes = kv.Value.PlaytimeMinutes;
                        game.LastPlayedTimestamp = kv.Value.LastPlayedTimestamp;
                    }
                }
            }
            catch { }

            // Show local games immediately while XML downloads
            if (this._Games.Count > 0 && this.IsHandleCreated)
            {
                this.BeginInvoke((Action)(() =>
                {
                    this.RefreshGames();
                    this.DownloadNextLogo();
                }));
            }

            // Phase 2: Download XML for game types (network, slower)
            if (this.IsHandleCreated)
            {
                this.BeginInvoke((Action)(() => this._PickerStatusLabel.Text = Localization.Get("DownloadingGameList")));
            }

            try
            {
                byte[] bytes;
                using (WebClient downloader = new())
                {
                    bytes = downloader.DownloadData(new Uri("https://gib.me/sam/games.xml"));
                }

                int newGames = 0;
                using (MemoryStream stream = new(bytes, false))
                {
                    XPathDocument document = new(stream);
                    var navigator = document.CreateNavigator();
                    var nodes = navigator.Select("/games/game");
                    while (nodes.MoveNext() == true)
                    {
                        string type = nodes.Current.GetAttribute("type", "");
                        if (string.IsNullOrEmpty(type) == true)
                        {
                            type = "normal";
                        }
                        uint appId = (uint)nodes.Current.ValueAsLong;

                        if (this._Games.TryGetValue(appId, out var existing))
                        {
                            // Update type for already-loaded game
                            existing.Type = type;
                        }
                        else
                        {
                            int before = this._Games.Count;
                            this.AddGame(appId, type);
                            if (this._Games.Count > before) newGames++;
                        }
                    }
                }

                // Apply playtime to newly added games
                if (newGames > 0 && this._PlaytimeData != null)
                {
                    foreach (var kv in this._PlaytimeData)
                    {
                        if (this._Games.TryGetValue(kv.Key, out var game))
                        {
                            game.PlaytimeMinutes = kv.Value.PlaytimeMinutes;
                            game.LastPlayedTimestamp = kv.Value.LastPlayedTimestamp;
                        }
                    }
                }
            }
            catch { }

            // Phase 3: Fetch additional games from Web API
            this.FetchGamesFromWebApi();
        }

        private void ScanLocalSteamGames()
        {
            try
            {
                string steamPath = API.Steam.GetInstallPath();
                if (string.IsNullOrEmpty(steamPath)) return;

                var steamappsDirs = new List<string>();
                string mainSteamapps = Path.Combine(steamPath, "steamapps");
                if (Directory.Exists(mainSteamapps))
                    steamappsDirs.Add(mainSteamapps);

                // Check libraryfolders.vdf for additional library paths
                string libraryVdf = Path.Combine(mainSteamapps, "libraryfolders.vdf");
                if (File.Exists(libraryVdf))
                {
                    try
                    {
                        string vdfContent = File.ReadAllText(libraryVdf);
                        var pathMatches = System.Text.RegularExpressions.Regex.Matches(
                            vdfContent, @"""path""\s+""([^""]+)""");
                        foreach (System.Text.RegularExpressions.Match m in pathMatches)
                        {
                            string libPath = m.Groups[1].Value.Replace("\\\\", "\\");
                            string libSteamapps = Path.Combine(libPath, "steamapps");
                            if (Directory.Exists(libSteamapps) && !steamappsDirs.Contains(libSteamapps))
                                steamappsDirs.Add(libSteamapps);
                        }
                    }
                    catch { }
                }

                // Scan appmanifest files
                foreach (var dir in steamappsDirs)
                {
                    try
                    {
                        foreach (var file in Directory.GetFiles(dir, "appmanifest_*.acf"))
                        {
                            string fileName = Path.GetFileNameWithoutExtension(file);
                            string idStr = fileName.Replace("appmanifest_", "");
                            if (uint.TryParse(idStr, out uint appId) && !this._Games.ContainsKey(appId))
                            {
                                this.AddGame(appId, "normal");
                            }
                        }
                    }
                    catch { }
                }

                // Scan localconfig.vdf for app IDs (catches uninstalled but played games)
                try
                {
                    ulong steamId = this._SteamClient.SteamUser.GetSteamId();
                    uint accountId = (uint)(steamId & 0xFFFFFFFF);
                    string configPath = Path.Combine(steamPath, "userdata",
                        accountId.ToString(), "config", "localconfig.vdf");
                    if (File.Exists(configPath))
                    {
                        string content = File.ReadAllText(configPath);
                        var appMatches = System.Text.RegularExpressions.Regex.Matches(
                            content, @"""(\d+)""\s*\{[^}]*""(LastPlayed|Playtime|playtime_forever)""");
                        foreach (System.Text.RegularExpressions.Match m in appMatches)
                        {
                            if (uint.TryParse(m.Groups[1].Value, out uint appId) &&
                                appId > 0 && !this._Games.ContainsKey(appId))
                            {
                                this.AddGame(appId, "normal");
                            }
                        }
                    }
                }
                catch { }

                // Scan Windows Registry (HKCU\Software\Valve\Steam\Apps)
                try
                {
                    using (var steamKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam\Apps"))
                    {
                        if (steamKey != null)
                        {
                            foreach (var subKeyName in steamKey.GetSubKeyNames())
                            {
                                if (uint.TryParse(subKeyName, out uint appId) &&
                                    appId > 0 && !this._Games.ContainsKey(appId))
                                {
                                    this.AddGame(appId, "normal");
                                }
                            }
                        }
                    }
                }
                catch { }
            }
            catch { }
        }

        private void FetchGamesFromWebApi()
        {
            string apiKey = AppSettings.SteamApiKey;
            if (string.IsNullOrWhiteSpace(apiKey)) return;

            try
            {
                ulong steamId = this._SteamClient.SteamUser.GetSteamId();
                string url = $"https://api.steampowered.com/IPlayerService/GetOwnedGames/v0001/?key={apiKey}&steamid={steamId}&include_appinfo=0&include_played_free_games=1&format=json";

                if (this.IsHandleCreated)
                {
                    this.BeginInvoke((Action)(() =>
                        this._PickerStatusLabel.Text = Localization.Get("FetchingFromApi")));
                }

                string json;
                using (WebClient wc = new())
                {
                    json = wc.DownloadString(url);
                }

                // Simple JSON parsing for appid values (no JSON library dependency)
                var matches = System.Text.RegularExpressions.Regex.Matches(
                    json, @"""appid""\s*:\s*(\d+)");
                int added = 0;
                foreach (System.Text.RegularExpressions.Match m in matches)
                {
                    if (uint.TryParse(m.Groups[1].Value, out uint appId) &&
                        appId > 0 && !this._Games.ContainsKey(appId))
                    {
                        this.AddGame(appId, "normal");
                        if (this._Games.ContainsKey(appId)) added++;
                    }
                }

                if (added > 0 && this.IsHandleCreated)
                {
                    this.BeginInvoke((Action)(() =>
                        this._PickerStatusLabel.Text = string.Format(
                            Localization.Get("ApiGamesFound"), added)));
                }
            }
            catch { }
        }

        private Dictionary<uint, AppLocalData> _PlaytimeData = new();

        private void OnDownloadList(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Error != null || e.Cancelled == true)
            {
                this.AddDefaultGames();
                MessageBox.Show(
                    e.Error?.ToString() ?? Localization.Get("DownloadCancelled"),
                    Localization.Get("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            // Apply playtime to any games added during XML/API phase
            try
            {
                if (this._PlaytimeData != null)
                {
                    foreach (var kv in this._PlaytimeData)
                    {
                        if (this._Games.TryGetValue(kv.Key, out var game))
                        {
                            game.PlaytimeMinutes = kv.Value.PlaytimeMinutes;
                            game.LastPlayedTimestamp = kv.Value.LastPlayedTimestamp;
                        }
                    }
                }
            }
            catch { }

            // Final refresh with correct types from XML
            this.RefreshGames();
            this._RefreshGamesButton.Enabled = true;
            this.DownloadNextLogo();
            this.LoadAchievementsAsync();
        }

        private void RefreshGames()
        {
            var nameSearch = this._SearchGameTextBox.Text.Length > 0
                ? this._SearchGameTextBox.Text
                : null;

            var wantNormals = this._FilterGamesMenuItem.Checked == true;
            var wantDemos = this._FilterDemosMenuItem.Checked == true;
            var wantMods = this._FilterModsMenuItem.Checked == true;
            var wantJunk = this._FilterJunkMenuItem.Checked == true;

            this._FilteredGames.Clear();
            foreach (var info in this._Games.Values)
            {
                if (nameSearch != null &&
                    info.Name.IndexOf(nameSearch, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                bool wanted = info.Type switch
                {
                    "normal" => wantNormals,
                    "demo" => wantDemos,
                    "mod" => wantMods,
                    "junk" => wantJunk,
                    _ => true,
                };
                if (wanted == false)
                {
                    continue;
                }

                this._FilteredGames.Add(info);
            }

            // Apply sorting
            this.SortFilteredGames();

            if (this._IsListView)
            {
                // Ensure columns exist
                bool hasApi = !string.IsNullOrWhiteSpace(AppSettings.SteamApiKey);
                if (this._GameListView.Columns.Count == 0)
                {
                    this._GameListView.Columns.Add(Localization.Get("ColGame") + " \u25B2", 250);
                    this._GameListView.Columns.Add(Localization.Get("ColAppId"), 65, HorizontalAlignment.Center);
                    this._GameListView.Columns.Add(Localization.Get("ColType"), 60, HorizontalAlignment.Center);
                    this._GameListView.Columns.Add(Localization.Get("ColHours"), 75, HorizontalAlignment.Center);
                    this._GameListView.Columns.Add(Localization.Get("ColLastPlayed"), 90, HorizontalAlignment.Center);
                    if (hasApi)
                        this._GameListView.Columns.Add(Localization.Get("ColAchievements"), 90, HorizontalAlignment.Center);
                    this._GameListView.HeaderStyle = ColumnHeaderStyle.Clickable;
                }

                this._SuppressItemCheck = true;
                this._GameListView.Items.Clear();
                foreach (var game in this._FilteredGames)
                {
                    var item = new ListViewItem(game.Name);
                    item.ImageIndex = game.ImageIndex;
                    item.SubItems.Add(game.Id.ToString());
                    item.SubItems.Add(game.Type);
                    item.SubItems.Add(PlaytimeReader.FormatPlaytime(game.PlaytimeMinutes));
                    item.SubItems.Add(PlaytimeReader.FormatLastPlayed(game.LastPlayedTimestamp));
                    if (hasApi)
                        item.SubItems.Add(FormatAchievements(game));
                    item.Tag = game;
                    item.ForeColor = DarkTheme.Text;
                    item.BackColor = DarkTheme.DarkBackground;
                    item.Checked = game.IsChecked;
                    this._GameListView.Items.Add(item);
                }
                this._SuppressItemCheck = false;

                // Queue icon downloads for games without icons
                foreach (var game in this._FilteredGames)
                {
                    this.AddGameToLogoQueue(game);
                }
                this.DownloadNextLogo();
            }
            else
            {
                this._GameListView.VirtualListSize = this._FilteredGames.Count;
            }
            this._PickerStatusLabel.Text =
                Localization.Get("DisplayingGames", this._GameListView.Items.Count, this._Games.Count);

            if (this._GameListView.Items.Count > 0)
            {
                this._GameListView.Items[0].Selected = true;
                this._GameListView.Select();
            }
        }

        private static string FormatAchievements(GameInfo game)
        {
            if (!game.AchievementsLoaded) return "\u23F3";
            if (game.AchievementsTotal == 0) return "\u2014";
            return $"{game.AchievementsUnlocked}/{game.AchievementsTotal}";
        }

        private void LoadAchievementsAsync()
        {
            string apiKey = AppSettings.SteamApiKey;
            if (string.IsNullOrWhiteSpace(apiKey)) return;

            ulong steamId = this._SteamClient.SteamUser.GetSteamId();
            var gamesToLoad = new List<GameInfo>();
            foreach (var game in this._Games.Values)
            {
                if (!game.AchievementsLoaded)
                    gamesToLoad.Add(game);
            }
            if (gamesToLoad.Count == 0) return;

            int total = gamesToLoad.Count;
            int completed = 0;

            var worker = new BackgroundWorker();
            worker.DoWork += (s, e) =>
            {
                var semaphore = new System.Threading.SemaphoreSlim(8);
                var tasks = new System.Collections.Generic.List<System.Threading.Tasks.Task>();

                foreach (var game in gamesToLoad)
                {
                    semaphore.Wait();
                    var capturedGame = game;
                    var task = System.Threading.Tasks.Task.Run(() =>
                    {
                        try
                        {
                            var data = SteamWebApi.GetPlayerAchievements(apiKey, steamId, capturedGame.Id);
                            if (data.HasValue)
                            {
                                capturedGame.AchievementsTotal = data.Value.TotalAchievements;
                                capturedGame.AchievementsUnlocked = data.Value.UnlockedAchievements;
                            }
                        }
                        catch { }
                        finally
                        {
                            capturedGame.AchievementsLoaded = true;
                            semaphore.Release();
                            int done = System.Threading.Interlocked.Increment(ref completed);
                            if (done % 10 == 0 || done == total)
                            {
                                try
                                {
                                    this.BeginInvoke((Action)(() =>
                                    {
                                        this._PickerStatusLabel.Text = string.Format(
                                            Localization.Get("LoadingAchievements"), done, total);
                                        // Update visible items incrementally
                                        UpdateAchievementColumn();
                                    }));
                                }
                                catch { }
                            }
                        }
                    });
                    tasks.Add(task);
                }

                System.Threading.Tasks.Task.WaitAll(tasks.ToArray());
            };
            worker.RunWorkerCompleted += (s, e) =>
            {
                if (!this.IsHandleCreated) return;
                UpdateAchievementColumn();
                this._PickerStatusLabel.Text = string.Format(
                    Localization.Get("AchievementsLoaded"), total);
            };
            worker.RunWorkerAsync();
        }

        private void UpdateAchievementColumn()
        {
            if (this._IsListView && this._GameListView.Columns.Count >= 6)
            {
                foreach (ListViewItem item in this._GameListView.Items)
                {
                    if (item.Tag is GameInfo info && info.AchievementsLoaded && item.SubItems.Count >= 6)
                    {
                        item.SubItems[5].Text = FormatAchievements(info);
                    }
                }
            }
        }

        private void SortFilteredGames()
        {
            switch (this._SortColumn)
            {
                case 0:
                    this._FilteredGames.Sort((a, b) =>
                        this._SortAscending
                            ? string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase)
                            : string.Compare(b.Name, a.Name, StringComparison.OrdinalIgnoreCase));
                    break;
                case 3:
                    this._FilteredGames.Sort((a, b) =>
                        this._SortAscending
                            ? a.PlaytimeMinutes.CompareTo(b.PlaytimeMinutes)
                            : b.PlaytimeMinutes.CompareTo(a.PlaytimeMinutes));
                    break;
                case 4:
                    this._FilteredGames.Sort((a, b) =>
                        this._SortAscending
                            ? a.LastPlayedTimestamp.CompareTo(b.LastPlayedTimestamp)
                            : b.LastPlayedTimestamp.CompareTo(a.LastPlayedTimestamp));
                    break;
                case 5:
                    this._FilteredGames.Sort((a, b) =>
                        this._SortAscending
                            ? a.AchievementsUnlocked.CompareTo(b.AchievementsUnlocked)
                            : b.AchievementsUnlocked.CompareTo(a.AchievementsUnlocked));
                    break;
                default:
                    this._FilteredGames.Sort((a, b) =>
                        string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
                    break;
            }
        }

        private class GameListComparer : System.Collections.IComparer
        {
            private readonly int _Column;
            private readonly bool _Ascending;

            public GameListComparer(int column, bool ascending)
            {
                this._Column = column;
                this._Ascending = ascending;
            }

            public int Compare(object x, object y)
            {
                var itemA = (ListViewItem)x;
                var itemB = (ListViewItem)y;
                var a = (GameInfo)itemA.Tag;
                var b = (GameInfo)itemB.Tag;

                int result = this._Column switch
                {
                    0 => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase),
                    3 => a.PlaytimeMinutes.CompareTo(b.PlaytimeMinutes),
                    4 => a.LastPlayedTimestamp.CompareTo(b.LastPlayedTimestamp),
                    5 => a.AchievementsUnlocked.CompareTo(b.AchievementsUnlocked),
                    _ => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase),
                };

                return this._Ascending ? result : -result;
            }
        }

        private void OnColumnClick(object sender, ColumnClickEventArgs e)
        {
            if (!this._IsListView)
                return;

            if (e.Column != 0 && e.Column != 3 && e.Column != 4 && e.Column != 5)
                return;

            if (this._SortColumn == e.Column)
            {
                this._SortAscending = !this._SortAscending;
            }
            else
            {
                this._SortColumn = e.Column;
                this._SortAscending = true;
            }

            string[] baseNames = {
                Localization.Get("ColGame"), Localization.Get("ColAppId"),
                Localization.Get("ColType"), Localization.Get("ColHours"),
                Localization.Get("ColLastPlayed"), Localization.Get("ColAchievements")
            };
            for (int i = 0; i < this._GameListView.Columns.Count && i < baseNames.Length; i++)
            {
                if (i == this._SortColumn)
                {
                    this._GameListView.Columns[i].Text = baseNames[i] + (this._SortAscending ? " ▲" : " ▼");
                }
                else
                {
                    this._GameListView.Columns[i].Text = baseNames[i];
                }
            }

            this._GameListView.ListViewItemSorter = new GameListComparer(this._SortColumn, this._SortAscending);
            this._GameListView.Sort();
        }

        private void OnGameListViewRetrieveVirtualItem(object sender, RetrieveVirtualItemEventArgs e)
        {
            if (this._IsListView || e.ItemIndex < 0 || e.ItemIndex >= this._FilteredGames.Count)
            {
                e.Item = new ListViewItem(""); // fallback dummy item
                return;
            }
            var info = this._FilteredGames[e.ItemIndex];
            e.Item = info.Item = new()
            {
                Text = info.Name,
                ImageIndex = info.ImageIndex,
            };
        
        }

        private void OnGameListViewSearchForVirtualItem(object sender, SearchForVirtualItemEventArgs e)
        {
            if (this._IsListView) return;
            if (e.Direction != SearchDirectionHint.Down || e.IsTextSearch == false)
            {
                return;
            }

            var count = this._FilteredGames.Count;
            if (count < 2)
            {
                return;
            }

            var text = e.Text;
            int startIndex = e.StartIndex;

            Predicate<GameInfo> predicate;
            /*if (e.IsPrefixSearch == true)*/
            {
                predicate = gi => gi.Name != null && gi.Name.StartsWith(text, StringComparison.CurrentCultureIgnoreCase);
            }
            /*else
            {
                predicate = gi => gi.Name != null && string.Compare(gi.Name, text, StringComparison.CurrentCultureIgnoreCase) == 0;
            }*/

            int index;
            if (e.StartIndex >= count)
            {
                // starting from the last item in the list
                index = this._FilteredGames.FindIndex(0, startIndex - 1, predicate);
            }
            else if (startIndex <= 0)
            {
                // starting from the first item in the list
                index = this._FilteredGames.FindIndex(0, count, predicate);
            }
            else
            {
                index = this._FilteredGames.FindIndex(startIndex, count - startIndex, predicate);
                if (index < 0)
                {
                    index = this._FilteredGames.FindIndex(0, startIndex - 1, predicate);
                }
            }

            e.Index = index < 0 ? -1 : index;
        }

        private void DoDownloadLogo(object sender, DoWorkEventArgs e)
        {
            // Legacy handler — no longer used (parallel download via Task.Run)
        }

        private void OnDownloadLogo(object sender, RunWorkerCompletedEventArgs e)
        {
            // Legacy handler — no longer used (parallel download via Task.Run)
        }

        private void DownloadNextLogo()
        {
            lock (this._LogoLock)
            {
                while (this._ActiveLogoDownloads < MaxParallelLogoDownloads)
                {
                    GameInfo info = DequeueNextLogo();
                    if (info == null)
                    {
                        if (this._ActiveLogoDownloads == 0)
                            this._DownloadStatusLabel.Visible = false;
                        return;
                    }

                    this._ActiveLogoDownloads++;
                    this._LogosAttempted.Add(info.ImageUrl);

                    var capturedInfo = info;
                    System.Threading.Tasks.Task.Run(() => DownloadLogoTask(capturedInfo));
                }

                int remaining = this._ActiveLogoDownloads + this._LogoQueue.Count;
                this._DownloadStatusLabel.Text = Localization.Get("DownloadingIcons", remaining);
                this._DownloadStatusLabel.Visible = true;
            }
        }

        private GameInfo DequeueNextLogo()
        {
            // Must be called within _LogoLock
            while (true)
            {
                if (this._LogoQueue.TryDequeue(out var info) == false)
                    return null;

                if (info.Item == null && !this._IsListView)
                    continue;

                if (!this._IsListView)
                {
                    Rectangle itemBounds;
                    try { itemBounds = info.Item.Bounds; }
                    catch (ArgumentOutOfRangeException) { continue; }

                    if (this._FilteredGames.Contains(info) == false ||
                        itemBounds.IntersectsWith(this._GameListView.ClientRectangle) == false)
                    {
                        this._LogosAttempting.Remove(info.ImageUrl);
                        continue;
                    }
                }

                return info;
            }
        }

        private void DownloadLogoTask(GameInfo info)
        {
            Bitmap bitmap = null;
            try
            {
                using (WebClient downloader = new())
                {
                    var data = downloader.DownloadData(new Uri(info.ImageUrl));
                    using (MemoryStream stream = new(data, false))
                    {
                        bitmap = new Bitmap(stream);
                    }
                }
            }
            catch { }

            try
            {
                if (this.IsHandleCreated)
                    this.BeginInvoke((Action)(() => OnLogoDownloadComplete(info, bitmap)));
            }
            catch { }
        }

        private void OnLogoDownloadComplete(GameInfo info, Bitmap bitmap)
        {
            lock (this._LogoLock)
                this._ActiveLogoDownloads--;

            if (bitmap == null)
            {
                this.DownloadNextLogo();
                return;
            }

            if (!this._Games.TryGetValue(info.Id, out var gameInfo))
            {
                bitmap.Dispose();
                this.DownloadNextLogo();
                return;
            }

            this._GameListView.BeginUpdate();
            var imageIndex = this._LogoImageList.Images.Count;
            this._LogoImageList.Images.Add(gameInfo.ImageUrl, bitmap);

            Bitmap smallIcon = new(32, 32);
            using (var g = Graphics.FromImage(smallIcon))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.DrawImage(bitmap, 0, 0, 32, 32);
            }
            this._SmallIconImageList.Images.Add(gameInfo.ImageUrl, smallIcon);

            // ImageList creates internal copy, safe to dispose originals
            bitmap.Dispose();
            smallIcon.Dispose();

            gameInfo.ImageIndex = imageIndex;

            if (this._IsListView)
            {
                foreach (ListViewItem item in this._GameListView.Items)
                {
                    if (item.Tag == gameInfo)
                    {
                        item.ImageIndex = imageIndex;
                        break;
                    }
                }
            }

            this._GameListView.EndUpdate();

            this.DownloadNextLogo();
        }

        private string GetGameImageUrl(uint id)
        {
            string candidate;

            var currentLanguage = this._SteamClient.SteamApps008.GetCurrentGameLanguage();

            candidate = this._SteamClient.SteamApps001.GetAppData(id, _($"small_capsule/{currentLanguage}"));
            if (string.IsNullOrEmpty(candidate) == false)
            {
                return _($"https://shared.cloudflare.steamstatic.com/store_item_assets/steam/apps/{id}/{candidate}");
            }

            if (currentLanguage != "english")
            {
                candidate = this._SteamClient.SteamApps001.GetAppData(id, "small_capsule/english");
                if (string.IsNullOrEmpty(candidate) == false)
                {
                    return _($"https://shared.cloudflare.steamstatic.com/store_item_assets/steam/apps/{id}/{candidate}");
                }
            }

            candidate = this._SteamClient.SteamApps001.GetAppData(id, "logo");
            if (string.IsNullOrEmpty(candidate) == false)
            {
                return _($"https://cdn.steamstatic.com/steamcommunity/public/images/apps/{id}/{candidate}.jpg");
            }

            return null;
        }

        private void AddGameToLogoQueue(GameInfo info)
        {
            if (info.ImageIndex > 0)
            {
                return;
            }

            var imageUrl = GetGameImageUrl(info.Id);
            if (string.IsNullOrEmpty(imageUrl) == true)
            {
                return;
            }

            info.ImageUrl = imageUrl;

            int imageIndex = this._LogoImageList.Images.IndexOfKey(imageUrl);
            if (imageIndex >= 0)
            {
                info.ImageIndex = imageIndex;
            }
            else
            {
                lock (this._LogoLock)
                {
                    if (this._LogosAttempting.Contains(imageUrl) == false &&
                        this._LogosAttempted.Contains(imageUrl) == false)
                    {
                        this._LogosAttempting.Add(imageUrl);
                        this._LogoQueue.Enqueue(info);
                    }
                }
            }
        }

        private bool OwnsGame(uint id)
        {
            return this._SteamClient.SteamApps008.IsSubscribedApp(id);
        }

        private void AddGame(uint id, string type)
        {
            if (this._Games.ContainsKey(id) == true)
            {
                return;
            }

            if (this.OwnsGame(id) == false)
            {
                return;
            }

            GameInfo info = new(id, type);
            info.Name = this._SteamClient.SteamApps001.GetAppData(info.Id, "name");

            // Restore cached image index if available
            var imageUrl = GetGameImageUrl(info.Id);
            if (!string.IsNullOrEmpty(imageUrl))
            {
                info.ImageUrl = imageUrl;
                int cachedIndex = this._LogoImageList.Images.IndexOfKey(imageUrl);
                if (cachedIndex >= 0)
                {
                    info.ImageIndex = cachedIndex;
                }
            }

            this._Games.Add(id, info);
        }

        private void AddGames()
        {
            this._Games.Clear();
            // Clear in-progress attempts but keep _LogosAttempted intact
            // so cached images in _LogoImageList are reused via AddGameToLogoQueue
            lock (this._LogoLock)
            {
                this._LogosAttempting.Clear();
            }
            while (this._LogoQueue.TryDequeue(out var _unused)) { }
            this._RefreshGamesButton.Enabled = false;
            this._ListWorker.RunWorkerAsync();
        }

        private void AddDefaultGames()
        {
            this.AddGame(480, "normal"); // Spacewar
        }

        private void OnTimer(object sender, EventArgs e)
        {
            this._CallbackTimer.Enabled = false;
            this._SteamClient.RunCallbacks(false);
            this._CallbackTimer.Enabled = true;
        }

        private void OnActivateGame(object sender, EventArgs e)
        {
            var focusedItem = (sender as MyListView)?.FocusedItem;
            if (focusedItem == null) return;

            GameInfo info;
            if (this._IsListView)
            {
                info = focusedItem.Tag as GameInfo;
            }
            else
            {
                var index = focusedItem.Index;
                if (index < 0 || index >= this._FilteredGames.Count) return;
                info = this._FilteredGames[index];
            }

            if (info == null) return;

            try
            {
                Environment.SetEnvironmentVariable("SAM_LANGUAGE", Localization.Current == Localization.Language.Russian ? "Russian" : "English");
                Process.Start("SAM.Game.exe", info.Id.ToString(CultureInfo.InvariantCulture));
            }
            catch (Win32Exception)
            {
                MessageBox.Show(
                    this,
                    Localization.Get("FailedToStartSAMGame"),
                    Localization.Get("Error"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void OnRefresh(object sender, EventArgs e)
        {
            this._AddGameTextBox.Text = "";
            this.AddGames();
        }

        private void OnAddGame(object sender, EventArgs e)
        {
            uint id;

            if (uint.TryParse(this._AddGameTextBox.Text, out id) == false)
            {
                MessageBox.Show(
                    this,
                    Localization.Get("EnterValidGameId"),
                    Localization.Get("Error"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            if (this.OwnsGame(id) == false)
            {
                MessageBox.Show(this, Localization.Get("DontOwnGame"), Localization.Get("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            while (this._LogoQueue.TryDequeue(out var logo) == true)
            {
                // clear the download queue because we will be showing only one app
                lock (this._LogoLock)
                {
                    this._LogosAttempted.Remove(logo.ImageUrl);
                }
            }

            this._AddGameTextBox.Text = "";
            this._Games.Clear();
            this.AddGame(id, "normal");
            this._FilterGamesMenuItem.Checked = true;
            this.RefreshGames();
            this.DownloadNextLogo();
        }

        private void OnFilterUpdate(object sender, EventArgs e)
        {
            this.RefreshGames();

            // Compatibility with _GameListView SearchForVirtualItemEventHandler (otherwise _SearchGameTextBox loose focus on KeyUp)
            this._SearchGameTextBox.Focus();
        }

        private void OnToggleView(object sender, EventArgs e)
        {
            _IsListView = !_IsListView;

            this._GameListView.BeginUpdate();

            if (_IsListView)
            {
                // Clear virtual state BEFORE disabling VirtualMode
                this._GameListView.SelectedIndices.Clear();
                this._GameListView.VirtualListSize = 0;
                this._GameListView.VirtualMode = false;

                this._GameListView.OwnerDraw = false;
                this._GameListView.View = View.Details;
                this._GameListView.MultiSelect = true;
                this._GameListView.CheckBoxes = true;
                this._GameListView.FullRowSelect = true;
                this._GameListView.SmallImageList = this._SmallIconImageList;
                this._GameListView.HeaderStyle = ColumnHeaderStyle.Nonclickable;

                // Setup columns
                bool hasApi = !string.IsNullOrWhiteSpace(AppSettings.SteamApiKey);
                this._GameListView.Columns.Clear();
                this._GameListView.Columns.Add(Localization.Get("ColGame"), 250);
                this._GameListView.Columns.Add(Localization.Get("ColAppId"), 65, HorizontalAlignment.Center);
                this._GameListView.Columns.Add(Localization.Get("ColType"), 60, HorizontalAlignment.Center);
                this._GameListView.Columns.Add(Localization.Get("ColHours"), 75, HorizontalAlignment.Center);
                this._GameListView.Columns.Add(Localization.Get("ColLastPlayed"), 90, HorizontalAlignment.Center);
                if (hasApi)
                    this._GameListView.Columns.Add(Localization.Get("ColAchievements"), 90, HorizontalAlignment.Center);

                // Populate items
                this._SuppressItemCheck = true;
                this._GameListView.Items.Clear();
                foreach (var game in this._FilteredGames)
                {
                    var item = new ListViewItem(game.Name);
                    item.ImageIndex = game.ImageIndex;
                    item.SubItems.Add(game.Id.ToString());
                    item.SubItems.Add(game.Type);
                    item.SubItems.Add(PlaytimeReader.FormatPlaytime(game.PlaytimeMinutes));
                    item.SubItems.Add(PlaytimeReader.FormatLastPlayed(game.LastPlayedTimestamp));
                    if (hasApi)
                        item.SubItems.Add(FormatAchievements(game));
                    item.Tag = game;
                    item.ForeColor = DarkTheme.Text;
                    item.BackColor = DarkTheme.DarkBackground;
                    item.Checked = game.IsChecked;
                    this._GameListView.Items.Add(item);
                }
                this._SuppressItemCheck = false;

                // Queue icon downloads for games without icons
                foreach (var game in this._FilteredGames)
                {
                    this.AddGameToLogoQueue(game);
                }
                this.DownloadNextLogo();
            }
            else
            {
                this._GameListView.Items.Clear();
                this._GameListView.Columns.Clear();
                this._GameListView.CheckBoxes = false;
                this._GameListView.MultiSelect = false;
                this._GameListView.FullRowSelect = false;
                this._GameListView.SmallImageList = this._LogoImageList;
                this._GameListView.OwnerDraw = true;
                this._GameListView.TileSize = new Size(184, 69);
                this._GameListView.View = View.LargeIcon;
                this._GameListView.VirtualMode = true;
                this._GameListView.VirtualListSize = this._FilteredGames.Count;
            }

            this._GameListView.EndUpdate();

            this._PickerStatusLabel.Text = Localization.Get("DisplayingGames", this._FilteredGames.Count, this._Games.Count);
        }

        private void OnGameListItemCheck(object sender, ItemCheckEventArgs e)
        {
            if (this._SuppressItemCheck) return;

            if (e.NewValue == CheckState.Checked)
            {
                int checkedCount = this._GameListView.CheckedIndices.Count;
                if (checkedCount >= 32)
                {
                    e.NewValue = CheckState.Unchecked;
                    MessageBox.Show(this, Localization.Get("MaxGamesWarning"),
                        Localization.Get("LimitReached"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            // Persist checkbox state on GameInfo model
            if (e.Index >= 0 && e.Index < this._GameListView.Items.Count)
            {
                var item = this._GameListView.Items[e.Index];
                if (item.Tag is GameInfo info)
                {
                    info.IsChecked = (e.NewValue == CheckState.Checked);
                }
            }

            // Update counter after event completes
            this.BeginInvoke((Action)UpdateCheckedCount);
        }

        private void UpdateCheckedCount()
        {
            int count = 0;
            foreach (var game in this._Games.Values)
            {
                if (game.IsChecked) count++;
            }
            this._CheckedCountLabel.Text = $"\u2611 {count}/32";

            if (count == 0)
                this._CheckedCountLabel.ForeColor = Color.FromArgb(140, 160, 180);
            else if (count >= 32)
                this._CheckedCountLabel.ForeColor = Color.FromArgb(255, 100, 100);
            else
                this._CheckedCountLabel.ForeColor = Color.FromArgb(100, 200, 130);
        }

        private void OnGameListViewDrawItem(object sender, DrawListViewItemEventArgs e)
        {
            if (!e.Item.Bounds.IntersectsWith(this._GameListView.ClientRectangle))
            {
                return;
            }

            var info = this._FilteredGames[e.ItemIndex];
            if (info.ImageIndex <= 0)
            {
                this.AddGameToLogoQueue(info);
                this.DownloadNextLogo();
            }

            var g = e.Graphics;
            var bounds = e.Bounds;
            bool isSelected = e.Item.Selected;

            // Card background
            using (var bgBrush = new SolidBrush(isSelected ? DarkTheme.Selection : DarkTheme.CardBackground))
            {
                g.FillRectangle(bgBrush, bounds);
            }

            // Draw game image with slight margin
            int margin = 2;
            if (e.Item.ImageIndex >= 0 && e.Item.ImageIndex < this._LogoImageList.Images.Count)
            {
                var imgW = Math.Min(this._LogoImageList.ImageSize.Width, bounds.Width - margin * 2);
                var imgH = Math.Min(this._LogoImageList.ImageSize.Height, bounds.Height - margin * 2);
                var imgRect = new Rectangle(bounds.X + margin, bounds.Y + margin, imgW, imgH);
                g.DrawImage(this._LogoImageList.Images[e.Item.ImageIndex], imgRect);
            }

            // Game name overlay at bottom
            int textHeight = 20;
            if (bounds.Height > textHeight + margin)
            {
                var textRect = new Rectangle(bounds.X, bounds.Bottom - textHeight, bounds.Width, textHeight);
                using (var overlayBrush = new SolidBrush(Color.FromArgb(180, 24, 26, 32)))
                {
                    g.FillRectangle(overlayBrush, textRect);
                }
                using (var font = new Font("Segoe UI", 8f))
                using (var textBrush = new SolidBrush(DarkTheme.TextBright))
                {
                    var sf = new StringFormat
                    {
                        Trimming = StringTrimming.EllipsisCharacter,
                        FormatFlags = StringFormatFlags.NoWrap,
                        LineAlignment = StringAlignment.Center
                    };
                    var nameRect = new RectangleF(textRect.X + 6, textRect.Y, textRect.Width - 12, textRect.Height);
                    g.DrawString(e.Item.Text, font, textBrush, nameRect, sf);
                }
            }

            // Selected: accent left border (4px strip)
            if (isSelected)
            {
                using (var accentBrush = new SolidBrush(DarkTheme.Accent))
                {
                    g.FillRectangle(accentBrush, bounds.X, bounds.Y, 4, bounds.Height);
                }
            }

            // Subtle bottom border
            using (var borderPen = new Pen(DarkTheme.Border))
            {
                g.DrawLine(borderPen, bounds.X, bounds.Bottom - 1, bounds.Right - 1, bounds.Bottom - 1);
            }
        }

        private void OnUnlockAllGames(object sender, EventArgs e)
        {
            if (this._FilteredGames.Count == 0)
            {
                MessageBox.Show(this, Localization.Get("NoGamesToProcess"), Localization.Get("Information"),
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var result = MessageBox.Show(
                this,
                Localization.Get("UnlockAllConfirm", this._FilteredGames.Count),
                Localization.Get("UnlockAllTitle"),
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result != DialogResult.Yes)
            {
                return;
            }

            var games = this._FilteredGames.ToList();
            this._PickerToolStrip.Enabled = false;

            var worker = new BackgroundWorker();
            worker.WorkerReportsProgress = true;
            worker.DoWork += (s, args) =>
            {
                var gameList = (List<GameInfo>)args.Argument;
                int success = 0;
                int failed = 0;

                for (int i = 0; i < gameList.Count; i++)
                {
                    var game = gameList[i];
                    worker.ReportProgress(i * 100 / gameList.Count,
                        Localization.Get("Processing", i + 1, gameList.Count, game.Name));

                    try
                    {
                        var psi = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "SAM.Game.exe",
                            Arguments = $"{game.Id} --unlock-all",
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            RedirectStandardOutput = true,
                        };

                        using (var proc = System.Diagnostics.Process.Start(psi))
                        {
                            proc.WaitForExit(30000); // 30 second timeout per game
                            if (!proc.HasExited)
                            {
                                proc.Kill();
                                failed++;
                            }
                            else if (proc.ExitCode == 0)
                            {
                                success++;
                            }
                            else
                            {
                                failed++;
                            }
                        }
                    }
                    catch (Exception)
                    {
                        failed++;
                    }
                }

                args.Result = new int[] { success, failed };
            };
            worker.ProgressChanged += (s, args) =>
            {
                this._PickerStatusLabel.Text = (string)args.UserState;
            };
            worker.RunWorkerCompleted += (s, args) =>
            {
                this._PickerToolStrip.Enabled = true;

                if (args.Error != null)
                {
                    MessageBox.Show(this, $"Error: {args.Error.Message}", Localization.Get("Error"),
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                else if (args.Result is int[] counts)
                {
                    MessageBox.Show(this,
                        Localization.Get("BatchComplete", counts[0], counts[1]),
                        Localization.Get("Results"),
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                this._PickerStatusLabel.Text =
                    Localization.Get("DisplayingGames", this._GameListView.Items.Count, this._Games.Count);
            };

            worker.RunWorkerAsync(games);
        }

        private void OnIdleGames(object sender, EventArgs e)
        {
            List<GameInfo> games;
            if (this._IsListView && this._GameListView.CheckedItems.Count > 0)
            {
                games = new List<GameInfo>();
                foreach (ListViewItem item in this._GameListView.CheckedItems)
                {
                    if (item.Tag is GameInfo gi) games.Add(gi);
                }
            }
            else
            {
                games = this._FilteredGames.ToList();
            }

            if (games.Count == 0)
            {
                MessageBox.Show(this, Localization.Get("NoGamesSelected"), Localization.Get("Info"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (var dialog = new IdleSettingsDialog(games.Count))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK || dialog.Settings == null) return;
                var form = new ActiveGamesForm(games, dialog.Settings);
                form.Show(this);
            }
        }

        private void OnSettings(object sender, EventArgs e)
        {
            using (var dialog = new SettingsDialog(Localization.Current, !_IsListView))
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    bool langChanged = Localization.Current != dialog.SelectedLanguage;
                    bool viewChanged = (!_IsListView) != dialog.IsTileView;
                    bool apiKeyChanged = AppSettings.SteamApiKey != dialog.ApiKey;

                    Localization.Current = dialog.SelectedLanguage;

                    if (apiKeyChanged)
                    {
                        AppSettings.SteamApiKey = dialog.ApiKey;
                        AppSettings.Save();
                        // Force column rebuild to add/remove achievements column
                        this._GameListView.Columns.Clear();
                    }

                    if (viewChanged)
                    {
                        OnToggleView(null, null);
                    }

                    if (langChanged)
                    {
                        ApplyLocalization();
                    }

                    // Refresh game list if API key changed to update columns
                    if (apiKeyChanged && !viewChanged)
                    {
                        RefreshGames();
                    }

                    if (apiKeyChanged)
                    {
                        if (!string.IsNullOrWhiteSpace(AppSettings.SteamApiKey))
                        {
                            // Load profile, fetch API games, reload achievements
                            LoadProfileAsync();
                            var apiWorker = new System.ComponentModel.BackgroundWorker();
                            apiWorker.DoWork += (ws, we) => { FetchGamesFromWebApi(); };
                            apiWorker.RunWorkerCompleted += (ws, we) =>
                            {
                                // Apply playtime to newly added games
                                if (this._PlaytimeData != null)
                                {
                                    foreach (var kv in this._PlaytimeData)
                                    {
                                        if (this._Games.TryGetValue(kv.Key, out var game))
                                        {
                                            game.PlaytimeMinutes = kv.Value.PlaytimeMinutes;
                                            game.LastPlayedTimestamp = kv.Value.LastPlayedTimestamp;
                                        }
                                    }
                                }
                                RefreshGames();
                                DownloadNextLogo();
                                LoadAchievementsAsync();
                            };
                            apiWorker.RunWorkerAsync();
                        }
                        else
                        {
                            // API key removed — hide profile panel
                            this._ProfilePanel.Visible = false;
                            RefreshGames();
                        }
                    }
                }
            }
        }

        private void ApplyLocalization()
        {
            this._RefreshGamesButton.Text = Localization.Get("RefreshGames");
            this._AddGameButton.Text = Localization.Get("AddGame");
            this._FindGamesLabel.Text = Localization.Get("Filter");
            this._FilterGamesMenuItem.Text = Localization.Get("ShowGames");
            this._FilterDemosMenuItem.Text = Localization.Get("ShowDemos");
            this._FilterModsMenuItem.Text = Localization.Get("ShowMods");
            this._FilterJunkMenuItem.Text = Localization.Get("ShowJunk");
            this._SettingsButton.ToolTipText = Localization.Get("Settings");
            this._UnlockAllGamesButton.Text = Localization.Get("UnlockAllGames");
            this._UnlockAllGamesButton.ToolTipText = Localization.Get("UnlockAllGamesTooltip");
            this._IdleGamesButton.Text = Localization.Get("IdleGames");
            this._IdleGamesButton.ToolTipText = Localization.Get("IdleGamesTooltip");
            this._FilterDropDownButton.Text = Localization.Get("GameFiltering");

            if (this._IsListView && this._GameListView.Columns.Count >= 6)
            {
                string[] baseNames = {
                    Localization.Get("ColGame"), Localization.Get("ColAppId"),
                    Localization.Get("ColType"), Localization.Get("ColHours"),
                    Localization.Get("ColLastPlayed"), Localization.Get("ColAchievements")
                };
                for (int i = 0; i < this._GameListView.Columns.Count && i < baseNames.Length; i++)
                {
                    if (i == this._SortColumn)
                        this._GameListView.Columns[i].Text = baseNames[i] + (this._SortAscending ? " \u25B2" : " \u25BC");
                    else
                        this._GameListView.Columns[i].Text = baseNames[i];
                }
            }

            this._PickerStatusLabel.Text =
                Localization.Get("DisplayingGames", this._GameListView.Items.Count, this._Games.Count);
        }

    }
}
