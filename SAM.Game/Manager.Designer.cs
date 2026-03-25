namespace SAM.Game
{
    partial class Manager
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            if (disposing)
            {
                _IconDownloader?.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.Windows.Forms.ToolStripSeparator _ToolStripSeparator1;
            System.Windows.Forms.ToolStripSeparator _ToolStripSeparator2;
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Manager));
            this._MainToolStrip = new System.Windows.Forms.ToolStrip();
            this._StoreButton = new System.Windows.Forms.ToolStripButton();
            this._ReloadButton = new System.Windows.Forms.ToolStripButton();
            this._ResetButton = new System.Windows.Forms.ToolStripButton();
            this._AchievementImageList = new System.Windows.Forms.ImageList(this.components);
            this._MainStatusStrip = new System.Windows.Forms.StatusStrip();
            this._CountryStatusLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this._GameStatusLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this._DownloadStatusLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this._CallbackTimer = new System.Windows.Forms.Timer(this.components);
            this._MainTabControl = new System.Windows.Forms.TabControl();
            this._AchievementsTabPage = new System.Windows.Forms.TabPage();
            this._AchievementListView = new SAM.Game.DoubleBufferedListView();
            this._AchievementNameColumnHeader = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this._AchievementDescriptionColumnHeader = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this._AchievementUnlockTimeColumnHeader = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this._AchievementGlobalPercentColumnHeader = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this._VacWarningPanel = new System.Windows.Forms.Panel();
            this._VacWarningLabel = new System.Windows.Forms.Label();
            this._VacOverrideButton = new System.Windows.Forms.Button();
            this._AchievementsToolStrip = new System.Windows.Forms.ToolStrip();
            this._LockAllButton = new System.Windows.Forms.ToolStripButton();
            this._InvertAllButton = new System.Windows.Forms.ToolStripButton();
            this._UnlockAllButton = new System.Windows.Forms.ToolStripButton();
            this._DisplayLabel = new System.Windows.Forms.ToolStripLabel();
            this._DisplayLockedOnlyButton = new System.Windows.Forms.ToolStripButton();
            this._DisplayUnlockedOnlyButton = new System.Windows.Forms.ToolStripButton();
            this._HideProtectedButton = new System.Windows.Forms.ToolStripButton();
            this._MatchingStringLabel = new System.Windows.Forms.ToolStripLabel();
            this._MatchingStringTextBox = new System.Windows.Forms.ToolStripTextBox();
            this._StatisticsTabPage = new System.Windows.Forms.TabPage();
            this._EnableStatsEditingCheckBox = new System.Windows.Forms.CheckBox();
            this._StatisticsDataGridView = new System.Windows.Forms.DataGridView();
            _ToolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            _ToolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            this._MainToolStrip.SuspendLayout();
            this._MainStatusStrip.SuspendLayout();
            this._MainTabControl.SuspendLayout();
            this._AchievementsTabPage.SuspendLayout();
            this._AchievementsToolStrip.SuspendLayout();
            this._StatisticsTabPage.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this._StatisticsDataGridView)).BeginInit();
            this.SuspendLayout();
            // 
            // _ToolStripSeparator1
            // 
            _ToolStripSeparator1.Name = "_ToolStripSeparator1";
            _ToolStripSeparator1.Size = new System.Drawing.Size(6, 25);
            // 
            // _ToolStripSeparator2
            // 
            _ToolStripSeparator2.Name = "_ToolStripSeparator2";
            _ToolStripSeparator2.Size = new System.Drawing.Size(6, 25);
            // 
            // _MainToolStrip
            // 
            this._MainToolStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this._StoreButton,
            this._ReloadButton,
            this._ResetButton});
            this._MainToolStrip.Location = new System.Drawing.Point(0, 0);
            this._MainToolStrip.Name = "_MainToolStrip";
            this._MainToolStrip.Size = new System.Drawing.Size(712, 25);
            this._MainToolStrip.TabIndex = 1;
            // 
            // _StoreButton
            // 
            this._StoreButton.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this._StoreButton.Enabled = false;
            this._StoreButton.Image = global::SAM.Game.Resources.Save;
            this._StoreButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this._StoreButton.Name = "_StoreButton";
            this._StoreButton.Size = new System.Drawing.Size(120, 22);
            this._StoreButton.Text = "Commit Changes";
            this._StoreButton.ToolTipText = "Store achievements and statistics for active game.";
            this._StoreButton.Click += new System.EventHandler(this.OnStore);
            // 
            // _ReloadButton
            // 
            this._ReloadButton.Enabled = false;
            this._ReloadButton.Image = global::SAM.Game.Resources.Refresh;
            this._ReloadButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this._ReloadButton.Name = "_ReloadButton";
            this._ReloadButton.Size = new System.Drawing.Size(66, 22);
            this._ReloadButton.Text = "Refresh";
            this._ReloadButton.ToolTipText = "Refresh achievements and statistics for active game.";
            this._ReloadButton.Click += new System.EventHandler(this.OnRefresh);
            // 
            // _ResetButton
            // 
            this._ResetButton.Image = global::SAM.Game.Resources.Reset;
            this._ResetButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this._ResetButton.Name = "_ResetButton";
            this._ResetButton.Size = new System.Drawing.Size(55, 22);
            this._ResetButton.Text = "Reset";
            this._ResetButton.ToolTipText = "Reset achievements and/or statistics for active game.";
            this._ResetButton.Click += new System.EventHandler(this.OnResetAllStats);
            // 
            // _AchievementImageList
            // 
            this._AchievementImageList.ColorDepth = System.Windows.Forms.ColorDepth.Depth24Bit;
            this._AchievementImageList.ImageSize = new System.Drawing.Size(32, 32);
            this._AchievementImageList.TransparentColor = System.Drawing.Color.Transparent;
            // 
            // _MainStatusStrip
            // 
            this._MainStatusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this._CountryStatusLabel,
            this._GameStatusLabel,
            this._DownloadStatusLabel});
            this._MainStatusStrip.Location = new System.Drawing.Point(0, 370);
            this._MainStatusStrip.Name = "_MainStatusStrip";
            this._MainStatusStrip.Size = new System.Drawing.Size(712, 22);
            this._MainStatusStrip.TabIndex = 4;
            // 
            // _CountryStatusLabel
            // 
            this._CountryStatusLabel.Name = "_CountryStatusLabel";
            this._CountryStatusLabel.Size = new System.Drawing.Size(0, 17);
            // 
            // _GameStatusLabel
            // 
            this._GameStatusLabel.Name = "_GameStatusLabel";
            this._GameStatusLabel.Size = new System.Drawing.Size(555, 17);
            this._GameStatusLabel.Spring = true;
            this._GameStatusLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // _DownloadStatusLabel
            // 
            this._DownloadStatusLabel.Image = global::SAM.Game.Resources.Download;
            this._DownloadStatusLabel.Name = "_DownloadStatusLabel";
            this._DownloadStatusLabel.Size = new System.Drawing.Size(111, 17);
            this._DownloadStatusLabel.Text = "Download status";
            this._DownloadStatusLabel.Visible = false;
            // 
            // _CallbackTimer
            // 
            this._CallbackTimer.Enabled = true;
            this._CallbackTimer.Tick += new System.EventHandler(this.OnTimer);
            // 
            // _VacWarningPanel
            // 
            this._VacWarningPanel.BackColor = DarkTheme.DangerBackground;
            this._VacWarningPanel.Controls.Add(this._VacWarningLabel);
            this._VacWarningPanel.Controls.Add(this._VacOverrideButton);
            this._VacWarningPanel.Dock = System.Windows.Forms.DockStyle.Top;
            this._VacWarningPanel.Location = new System.Drawing.Point(0, 25);
            this._VacWarningPanel.Name = "_VacWarningPanel";
            this._VacWarningPanel.Padding = new System.Windows.Forms.Padding(8, 4, 8, 4);
            this._VacWarningPanel.Size = new System.Drawing.Size(712, 56);
            this._VacWarningPanel.Visible = false;
            // 
            // _VacWarningLabel
            // 
            this._VacWarningLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this._VacWarningLabel.ForeColor = DarkTheme.DangerText;
            this._VacWarningLabel.Font = new System.Drawing.Font("Segoe UI", 8F, System.Drawing.FontStyle.Bold);
            this._VacWarningLabel.Name = "_VacWarningLabel";
            this._VacWarningLabel.Size = new System.Drawing.Size(560, 48);
            this._VacWarningLabel.Text = "⚠ VAC / Anti-Cheat Protected — Editing achievements or statistics may result in a ban.";
            this._VacWarningLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // _VacOverrideButton
            // 
            this._VacOverrideButton.BackColor = DarkTheme.DangerSurface;
            this._VacOverrideButton.Dock = System.Windows.Forms.DockStyle.Right;
            this._VacOverrideButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this._VacOverrideButton.ForeColor = DarkTheme.DangerText;
            this._VacOverrideButton.Name = "_VacOverrideButton";
            this._VacOverrideButton.Size = new System.Drawing.Size(130, 48);
            this._VacOverrideButton.Text = "I understand the risks";
            this._VacOverrideButton.UseVisualStyleBackColor = false;
            this._VacOverrideButton.Click += new System.EventHandler(this.OnVacOverride);
            // 
            // _MainTabControl
            // 
            this._MainTabControl.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this._MainTabControl.Controls.Add(this._AchievementsTabPage);
            this._MainTabControl.Controls.Add(this._StatisticsTabPage);
            this._MainTabControl.Location = new System.Drawing.Point(8, 33);
            this._MainTabControl.Name = "_MainTabControl";
            this._MainTabControl.SelectedIndex = 0;
            this._MainTabControl.Size = new System.Drawing.Size(696, 334);
            this._MainTabControl.TabIndex = 5;
            // 
            // _AchievementsTabPage
            // 
            this._AchievementsTabPage.Controls.Add(this._AchievementListView);
            this._AchievementsTabPage.Controls.Add(this._AchievementsToolStrip);
            this._AchievementsTabPage.Location = new System.Drawing.Point(4, 22);
            this._AchievementsTabPage.Name = "_AchievementsTabPage";
            this._AchievementsTabPage.Padding = new System.Windows.Forms.Padding(3);
            this._AchievementsTabPage.Size = new System.Drawing.Size(688, 308);
            this._AchievementsTabPage.TabIndex = 0;
            this._AchievementsTabPage.Text = "Achievements";
            // 
            // _AchievementListView
            // 
            this._AchievementListView.Activation = System.Windows.Forms.ItemActivation.OneClick;
            this._AchievementListView.BackColor = DarkTheme.DarkBackground;
            this._AchievementListView.BackgroundImageTiled = true;
            this._AchievementListView.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this._AchievementListView.CheckBoxes = false;
            this._AchievementListView.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this._AchievementNameColumnHeader,
            this._AchievementDescriptionColumnHeader,
            this._AchievementUnlockTimeColumnHeader,
            this._AchievementGlobalPercentColumnHeader});
            this._AchievementListView.Dock = System.Windows.Forms.DockStyle.Fill;
            this._AchievementListView.ForeColor = DarkTheme.Text;
            this._AchievementListView.FullRowSelect = true;
            this._AchievementListView.GridLines = false;
            this._AchievementListView.HideSelection = false;
            this._AchievementListView.LargeImageList = this._AchievementImageList;
            this._AchievementListView.Location = new System.Drawing.Point(3, 28);
            this._AchievementListView.Name = "_AchievementListView";
            this._AchievementListView.OwnerDraw = true;
            this._AchievementListView.Size = new System.Drawing.Size(682, 277);
            this._AchievementListView.SmallImageList = this._AchievementImageList;
            this._AchievementListView.Sorting = System.Windows.Forms.SortOrder.Ascending;
            this._AchievementListView.TabIndex = 4;
            this._AchievementListView.UseCompatibleStateImageBehavior = false;
            this._AchievementListView.View = System.Windows.Forms.View.Details;
            this._AchievementListView.MouseClick += new System.Windows.Forms.MouseEventHandler(this.OnAchievementMouseClick);
            this._AchievementListView.DrawItem += new System.Windows.Forms.DrawListViewItemEventHandler(this.OnDrawAchievementItem);
            this._AchievementListView.DrawSubItem += new System.Windows.Forms.DrawListViewSubItemEventHandler(this.OnDrawAchievementSubItem);
            this._AchievementListView.DrawColumnHeader += new System.Windows.Forms.DrawListViewColumnHeaderEventHandler(this.OnDrawAchievementColumnHeader);
            // 
            // _AchievementNameColumnHeader
            // 
            this._AchievementNameColumnHeader.Text = "Name";
            this._AchievementNameColumnHeader.Width = 160;
            // 
            // _AchievementDescriptionColumnHeader
            // 
            this._AchievementDescriptionColumnHeader.Text = "Description";
            this._AchievementDescriptionColumnHeader.Width = 230;
            // 
            // _AchievementUnlockTimeColumnHeader
            // 
            this._AchievementUnlockTimeColumnHeader.Text = "Unlock Time";
            this._AchievementUnlockTimeColumnHeader.Width = 140;
            // 
            // _AchievementGlobalPercentColumnHeader
            // 
            this._AchievementGlobalPercentColumnHeader.Text = "Global %";
            this._AchievementGlobalPercentColumnHeader.Width = 70;
            // 
            // _AchievementsToolStrip
            // 
            this._AchievementsToolStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this._LockAllButton,
            this._InvertAllButton,
            this._UnlockAllButton,
            _ToolStripSeparator1,
            this._DisplayLabel,
            this._DisplayLockedOnlyButton,
            this._DisplayUnlockedOnlyButton,
            this._HideProtectedButton,
            _ToolStripSeparator2,
            this._MatchingStringLabel,
            this._MatchingStringTextBox});
            this._AchievementsToolStrip.Location = new System.Drawing.Point(3, 3);
            this._AchievementsToolStrip.Name = "_AchievementsToolStrip";
            this._AchievementsToolStrip.Size = new System.Drawing.Size(682, 25);
            this._AchievementsToolStrip.TabIndex = 5;
            // 
            // _LockAllButton
            // 
            this._LockAllButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this._LockAllButton.Image = global::SAM.Game.Resources.Lock;
            this._LockAllButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this._LockAllButton.Name = "_LockAllButton";
            this._LockAllButton.Size = new System.Drawing.Size(23, 22);
            this._LockAllButton.Text = "Lock All";
            this._LockAllButton.ToolTipText = "Lock all achievements.";
            this._LockAllButton.Click += new System.EventHandler(this.OnLockAll);
            // 
            // _InvertAllButton
            // 
            this._InvertAllButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this._InvertAllButton.Image = global::SAM.Game.Resources.Invert;
            this._InvertAllButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this._InvertAllButton.Name = "_InvertAllButton";
            this._InvertAllButton.Size = new System.Drawing.Size(23, 22);
            this._InvertAllButton.Text = "Invert All";
            this._InvertAllButton.ToolTipText = "Invert all achievements.";
            this._InvertAllButton.Click += new System.EventHandler(this.OnInvertAll);
            // 
            // _UnlockAllButton
            // 
            this._UnlockAllButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this._UnlockAllButton.Image = global::SAM.Game.Resources.Unlock;
            this._UnlockAllButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this._UnlockAllButton.Name = "_UnlockAllButton";
            this._UnlockAllButton.Size = new System.Drawing.Size(23, 22);
            this._UnlockAllButton.Text = "Unlock All";
            this._UnlockAllButton.ToolTipText = "Unlock all achievements.";
            this._UnlockAllButton.Click += new System.EventHandler(this.OnUnlockAll);
            // 
            // _DisplayLabel
            // 
            this._DisplayLabel.Font = new System.Drawing.Font("Segoe UI", 9F);
            this._DisplayLabel.Name = "_DisplayLabel";
            this._DisplayLabel.Size = new System.Drawing.Size(62, 22);
            this._DisplayLabel.Text = "Show only";
            // 
            // _DisplayLockedOnlyButton
            // 
            this._DisplayLockedOnlyButton.CheckOnClick = true;
            this._DisplayLockedOnlyButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this._DisplayLockedOnlyButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this._DisplayLockedOnlyButton.Name = "_DisplayLockedOnlyButton";
            this._DisplayLockedOnlyButton.Size = new System.Drawing.Size(46, 22);
            this._DisplayLockedOnlyButton.Text = "locked";
            this._DisplayLockedOnlyButton.Click += new System.EventHandler(this.OnDisplayCheckedOnly);
            // 
            // _DisplayUnlockedOnlyButton
            // 
            this._DisplayUnlockedOnlyButton.CheckOnClick = true;
            this._DisplayUnlockedOnlyButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this._DisplayUnlockedOnlyButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this._DisplayUnlockedOnlyButton.Name = "_DisplayUnlockedOnlyButton";
            this._DisplayUnlockedOnlyButton.Size = new System.Drawing.Size(60, 22);
            this._DisplayUnlockedOnlyButton.Text = "unlocked";
            this._DisplayUnlockedOnlyButton.Click += new System.EventHandler(this.OnDisplayUncheckedOnly);
            //
            // _HideProtectedButton
            //
            this._HideProtectedButton.CheckOnClick = true;
            this._HideProtectedButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this._HideProtectedButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this._HideProtectedButton.Name = "_HideProtectedButton";
            this._HideProtectedButton.Size = new System.Drawing.Size(90, 22);
            this._HideProtectedButton.Text = "hide protected";
            this._HideProtectedButton.Click += new System.EventHandler(this.OnHideProtectedClick);
            //
            // _MatchingStringLabel
            // 
            this._MatchingStringLabel.Font = new System.Drawing.Font("Segoe UI", 9F);
            this._MatchingStringLabel.Name = "_MatchingStringLabel";
            this._MatchingStringLabel.Size = new System.Drawing.Size(33, 22);
            this._MatchingStringLabel.Text = "Filter";
            // 
            // _MatchingStringTextBox
            // 
            this._MatchingStringTextBox.Font = new System.Drawing.Font("Segoe UI", 9F);
            this._MatchingStringTextBox.Name = "_MatchingStringTextBox";
            this._MatchingStringTextBox.Size = new System.Drawing.Size(100, 25);
            this._MatchingStringTextBox.ToolTipText = "Type at least 3 characters that must appear in the name or description";
            this._MatchingStringTextBox.KeyUp += new System.Windows.Forms.KeyEventHandler(this.OnFilterUpdate);
            // 
            // _StatisticsTabPage
            // 
            this._StatisticsTabPage.Controls.Add(this._EnableStatsEditingCheckBox);
            this._StatisticsTabPage.Controls.Add(this._StatisticsDataGridView);
            this._StatisticsTabPage.Location = new System.Drawing.Point(4, 22);
            this._StatisticsTabPage.Name = "_StatisticsTabPage";
            this._StatisticsTabPage.Padding = new System.Windows.Forms.Padding(3);
            this._StatisticsTabPage.Size = new System.Drawing.Size(688, 308);
            this._StatisticsTabPage.TabIndex = 1;
            this._StatisticsTabPage.Text = "Statistics";
            // 
            // _EnableStatsEditingCheckBox
            // 
            this._EnableStatsEditingCheckBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this._EnableStatsEditingCheckBox.AutoSize = true;
            this._EnableStatsEditingCheckBox.Location = new System.Drawing.Point(6, 285);
            this._EnableStatsEditingCheckBox.Name = "_EnableStatsEditingCheckBox";
            this._EnableStatsEditingCheckBox.Size = new System.Drawing.Size(512, 17);
            this._EnableStatsEditingCheckBox.TabIndex = 1;
            this._EnableStatsEditingCheckBox.Text = "I understand by modifying the values of stats, I may screw things up and can\'t bl" +
    "ame anyone but myself.";
            this._EnableStatsEditingCheckBox.UseVisualStyleBackColor = true;
            this._EnableStatsEditingCheckBox.CheckedChanged += new System.EventHandler(this.OnStatAgreementChecked);
            // 
            // _StatisticsDataGridView
            // 
            this._StatisticsDataGridView.AllowUserToAddRows = false;
            this._StatisticsDataGridView.AllowUserToDeleteRows = false;
            this._StatisticsDataGridView.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this._StatisticsDataGridView.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this._StatisticsDataGridView.Location = new System.Drawing.Point(6, 6);
            this._StatisticsDataGridView.Name = "_StatisticsDataGridView";
            this._StatisticsDataGridView.Size = new System.Drawing.Size(596, 273);
            this._StatisticsDataGridView.TabIndex = 0;
            this._StatisticsDataGridView.CellEndEdit += new System.Windows.Forms.DataGridViewCellEventHandler(this.OnStatCellEndEdit);
            this._StatisticsDataGridView.DataError += new System.Windows.Forms.DataGridViewDataErrorEventHandler(this.OnStatDataError);
            // 
            // Manager
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(712, 392);
            this.Controls.Add(this._MainToolStrip);
            this.Controls.Add(this._VacWarningPanel);
            this.Controls.Add(this._MainTabControl);
            this.Controls.Add(this._MainStatusStrip);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MinimumSize = new System.Drawing.Size(640, 50);
            this.Name = "Manager";
            this.Text = "SAM Evolved";
            this._MainToolStrip.ResumeLayout(false);
            this._MainToolStrip.PerformLayout();
            this._MainStatusStrip.ResumeLayout(false);
            this._MainStatusStrip.PerformLayout();
            this._MainTabControl.ResumeLayout(false);
            this._AchievementsTabPage.ResumeLayout(false);
            this._AchievementsTabPage.PerformLayout();
            this._AchievementsToolStrip.ResumeLayout(false);
            this._AchievementsToolStrip.PerformLayout();
            this._StatisticsTabPage.ResumeLayout(false);
            this._StatisticsTabPage.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this._StatisticsDataGridView)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ToolStrip _MainToolStrip;
        private System.Windows.Forms.ToolStripButton _StoreButton;
        private System.Windows.Forms.ToolStripButton _ReloadButton;
        private System.Windows.Forms.StatusStrip _MainStatusStrip;
        private System.Windows.Forms.ToolStripStatusLabel _CountryStatusLabel;
        private System.Windows.Forms.ToolStripStatusLabel _GameStatusLabel;
        private System.Windows.Forms.ImageList _AchievementImageList;
        private System.Windows.Forms.Timer _CallbackTimer;
        private System.Windows.Forms.TabControl _MainTabControl;
        private System.Windows.Forms.TabPage _AchievementsTabPage;
        private System.Windows.Forms.TabPage _StatisticsTabPage;
        private DoubleBufferedListView _AchievementListView;
        private System.Windows.Forms.ColumnHeader _AchievementNameColumnHeader;
        private System.Windows.Forms.ColumnHeader _AchievementDescriptionColumnHeader;
        private System.Windows.Forms.ToolStrip _AchievementsToolStrip;
        private System.Windows.Forms.ToolStripButton _LockAllButton;
        private System.Windows.Forms.ToolStripButton _InvertAllButton;
        private System.Windows.Forms.ToolStripButton _UnlockAllButton;
        private System.Windows.Forms.DataGridView _StatisticsDataGridView;
        private System.Windows.Forms.ToolStripButton _ResetButton;
        private System.Windows.Forms.ToolStripStatusLabel _DownloadStatusLabel;
        private System.Windows.Forms.ToolStripLabel _DisplayLabel;
        private System.Windows.Forms.ToolStripButton _DisplayUnlockedOnlyButton;
        private System.Windows.Forms.ToolStripButton _DisplayLockedOnlyButton;
        private System.Windows.Forms.ToolStripButton _HideProtectedButton;
        private System.Windows.Forms.ToolStripLabel _MatchingStringLabel;
        private System.Windows.Forms.ToolStripTextBox _MatchingStringTextBox;
        private System.Windows.Forms.ColumnHeader _AchievementUnlockTimeColumnHeader;
        private System.Windows.Forms.ColumnHeader _AchievementGlobalPercentColumnHeader;
        private System.Windows.Forms.CheckBox _EnableStatsEditingCheckBox;
        private System.Windows.Forms.Panel _VacWarningPanel;
        private System.Windows.Forms.Label _VacWarningLabel;
        private System.Windows.Forms.Button _VacOverrideButton;
    }
}
