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

            this._AppDataChangedCallback = client.CreateAndRegisterCallback<API.Callbacks.AppDataChanged>();
            this._AppDataChangedCallback.OnRun += this.OnAppDataChanged;

            this.AddGames();
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
            if (this.IsHandleCreated)
            {
                this.BeginInvoke((Action)(() => this._PickerStatusLabel.Text = Localization.Get("DownloadingGameList")));
            }

            byte[] bytes;
            using (WebClient downloader = new())
            {
                bytes = downloader.DownloadData(new Uri("https://gib.me/sam/games.xml"));
            }

            List<KeyValuePair<uint, string>> pairs = new();
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
                    pairs.Add(new((uint)nodes.Current.ValueAsLong, type));
                }
            }

            if (this.IsHandleCreated)
            {
                this.BeginInvoke((Action)(() => this._PickerStatusLabel.Text = Localization.Get("CheckingOwnership")));
            }
            int total = pairs.Count;
            int processed = 0;
            int lastPercent = -1;
            foreach (var kv in pairs)
            {
                this.AddGame(kv.Key, kv.Value);
                processed++;
                int percent = (processed * 100) / total;
                if (percent != lastPercent && percent % 10 == 0 && this.IsHandleCreated)
                {
                    lastPercent = percent;
                    this.BeginInvoke((Action)(() =>
                        this._PickerStatusLabel.Text = Localization.Get("CheckingOwnershipPercent", percent)));
                }
            }
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

            // Load playtime from localconfig.vdf
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

            this.RefreshGames();
            this._RefreshGamesButton.Enabled = true;
            this.DownloadNextLogo();
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
                if (this._GameListView.Columns.Count == 0)
                {
                    this._GameListView.Columns.Add(Localization.Get("ColGame") + " \u25B2", 250);
                    this._GameListView.Columns.Add(Localization.Get("ColAppId"), 65, HorizontalAlignment.Center);
                    this._GameListView.Columns.Add(Localization.Get("ColType"), 60, HorizontalAlignment.Center);
                    this._GameListView.Columns.Add(Localization.Get("ColHours"), 75, HorizontalAlignment.Center);
                    this._GameListView.Columns.Add(Localization.Get("ColLastPlayed"), 90, HorizontalAlignment.Center);
                    this._GameListView.HeaderStyle = ColumnHeaderStyle.Clickable;
                }

                this._GameListView.Items.Clear();
                foreach (var game in this._FilteredGames)
                {
                    var item = new ListViewItem(game.Name);
                    item.ImageIndex = game.ImageIndex;
                    item.SubItems.Add(game.Id.ToString());
                    item.SubItems.Add(game.Type);
                    item.SubItems.Add(PlaytimeReader.FormatPlaytime(game.PlaytimeMinutes));
                    item.SubItems.Add(PlaytimeReader.FormatLastPlayed(game.LastPlayedTimestamp));
                    item.Tag = game;
                    item.ForeColor = DarkTheme.Text;
                    item.BackColor = DarkTheme.DarkBackground;
                    this._GameListView.Items.Add(item);
                }

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
                    _ => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase),
                };

                return this._Ascending ? result : -result;
            }
        }

        private void OnColumnClick(object sender, ColumnClickEventArgs e)
        {
            if (!this._IsListView)
                return;

            if (e.Column != 0 && e.Column != 3 && e.Column != 4)
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
                Localization.Get("ColLastPlayed")
            };
            for (int i = 0; i < this._GameListView.Columns.Count; i++)
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
            var info = (GameInfo)e.Argument;

            lock (this._LogoLock)
            {
                this._LogosAttempted.Add(info.ImageUrl);
            }

            using (WebClient downloader = new())
            {
                try
                {
                    var data = downloader.DownloadData(new Uri(info.ImageUrl));
                    using (MemoryStream stream = new(data, false))
                    {
                        Bitmap bitmap = new(stream);
                        e.Result = new LogoInfo(info.Id, bitmap);
                    }
                }
                catch (Exception)
                {
                    e.Result = new LogoInfo(info.Id, null);
                }
            }
        }

        private void OnDownloadLogo(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Error != null || e.Cancelled == true)
            {
                return;
            }

            if (e.Result is LogoInfo logoInfo &&
                logoInfo.Bitmap != null &&
                this._Games.TryGetValue(logoInfo.Id, out var gameInfo) == true)
            {
                this._GameListView.BeginUpdate();
                var imageIndex = this._LogoImageList.Images.Count;
                this._LogoImageList.Images.Add(gameInfo.ImageUrl, logoInfo.Bitmap);

                // Create 32x32 thumbnail for list view
                Bitmap smallIcon = new(32, 32);
                using (var g = Graphics.FromImage(smallIcon))
                {
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.DrawImage(logoInfo.Bitmap, 0, 0, 32, 32);
                }
                this._SmallIconImageList.Images.Add(gameInfo.ImageUrl, smallIcon);

                // Dispose originals — ImageList made internal copies
                logoInfo.Bitmap.Dispose();
                smallIcon.Dispose();

                gameInfo.ImageIndex = imageIndex;

                // Update list view item icon if in list mode
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
            }

            this.DownloadNextLogo();
        }

        private void DownloadNextLogo()
        {
            lock (this._LogoLock)
            {

                if (this._LogoWorker.IsBusy == true)
                {
                    return;
                }

                GameInfo info;
                while (true)
                {
                    if (this._LogoQueue.TryDequeue(out info) == false)
                    {
                        this._DownloadStatusLabel.Visible = false;
                        return;
                    }

                    if (info.Item == null && !this._IsListView)
                    {
                        continue;
                    }

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

                    break;
                }

                this._DownloadStatusLabel.Text = Localization.Get("DownloadingIcons", 1 + this._LogoQueue.Count);
                this._DownloadStatusLabel.Visible = true;

                this._LogoWorker.RunWorkerAsync(info);
            }
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

            // Restore cached image index if available (no new API calls)
            if (!string.IsNullOrEmpty(info.ImageUrl))
            {
                int cachedIndex = this._LogoImageList.Images.IndexOfKey(info.ImageUrl);
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
            // Clear failed download attempts so they can be retried,
            // but keep _LogoImageList and _SmallIconImageList intact (cached images persist)
            lock (this._LogoLock)
            {
                this._LogosAttempting.Clear();
                this._LogosAttempted.Clear();
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
                this._GameListView.Columns.Clear();
                this._GameListView.Columns.Add(Localization.Get("ColGame"), 250);
                this._GameListView.Columns.Add(Localization.Get("ColAppId"), 65, HorizontalAlignment.Center);
                this._GameListView.Columns.Add(Localization.Get("ColType"), 60, HorizontalAlignment.Center);
                this._GameListView.Columns.Add(Localization.Get("ColHours"), 75, HorizontalAlignment.Center);
                this._GameListView.Columns.Add(Localization.Get("ColLastPlayed"), 90, HorizontalAlignment.Center);

                // Populate items
                this._GameListView.Items.Clear();
                foreach (var game in this._FilteredGames)
                {
                    var item = new ListViewItem(game.Name);
                    item.ImageIndex = game.ImageIndex;
                    item.SubItems.Add(game.Id.ToString());
                    item.SubItems.Add(game.Type);
                    item.SubItems.Add(PlaytimeReader.FormatPlaytime(game.PlaytimeMinutes));
                    item.SubItems.Add(PlaytimeReader.FormatLastPlayed(game.LastPlayedTimestamp));
                    item.Tag = game;
                    item.ForeColor = DarkTheme.Text;
                    item.BackColor = DarkTheme.DarkBackground;
                    this._GameListView.Items.Add(item);
                }

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
            if (e.NewValue == CheckState.Checked)
            {
                int checkedCount = this._GameListView.CheckedIndices.Count;
                if (checkedCount >= 32)
                {
                    e.NewValue = CheckState.Unchecked;
                    MessageBox.Show(this, Localization.Get("MaxGamesWarning"),
                        Localization.Get("LimitReached"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
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

                    Localization.Current = dialog.SelectedLanguage;

                    if (viewChanged)
                    {
                        OnToggleView(null, null);
                    }

                    if (langChanged)
                    {
                        ApplyLocalization();
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

            if (this._IsListView && this._GameListView.Columns.Count >= 5)
            {
                string[] baseNames = {
                    Localization.Get("ColGame"), Localization.Get("ColAppId"),
                    Localization.Get("ColType"), Localization.Get("ColHours"),
                    Localization.Get("ColLastPlayed")
                };
                for (int i = 0; i < this._GameListView.Columns.Count; i++)
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
