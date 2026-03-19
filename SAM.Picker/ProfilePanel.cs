using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace SAM.Picker
{
    internal class ProfilePanel : Panel
    {
        private Bitmap _avatar;
        private string _personaName;
        private int _personaState;
        private string _countryCode;
        private int _level;
        private int _xp;
        private int _xpNeeded;
        private int _badgeCount;

        public ProfilePanel()
        {
            DoubleBuffered = true;
            BackColor = DarkTheme.Surface;
            Height = 80;
            Dock = DockStyle.Top;
            Visible = false;
        }

        public void SetData(PlayerSummary summary, int level, BadgeInfo badges)
        {
            _personaName = summary.PersonaName;
            _personaState = summary.PersonaState;
            _countryCode = summary.CountryCode;
            _level = badges.PlayerLevel;
            _xp = badges.PlayerXp;
            _xpNeeded = badges.PlayerXpNeededToLevelUp;
            _badgeCount = badges.BadgeCount;
            Visible = true;
            Invalidate();
        }

        public void SetAvatar(Bitmap avatar)
        {
            _avatar = avatar;
            Invalidate();
        }

        private static Color GetStatusColor(int state)
        {
            switch (state)
            {
                case 1: return Color.FromArgb(0, 217, 163);
                case 2: return Color.FromArgb(255, 107, 107);
                case 3:
                case 4: return Color.FromArgb(255, 179, 71);
                case 5:
                case 6: return Color.FromArgb(0, 217, 163);
                default: return Color.FromArgb(95, 99, 104);
            }
        }

        private static string GetStatusText(int state)
        {
            switch (state)
            {
                case 1: return "Online";
                case 2: return "Busy";
                case 3: return "Away";
                case 4: return "Snooze";
                case 5: return "Looking to Trade";
                case 6: return "Looking to Play";
                default: return "Offline";
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            // Bottom border
            using (var pen = new Pen(DarkTheme.Border))
            {
                g.DrawLine(pen, 0, Height - 1, Width, Height - 1);
            }

            if (_personaName == null)
                return;

            int x = 8;
            int avatarSize = 64;
            int avatarY = (Height - 1 - avatarSize) / 2;

            // Avatar
            if (_avatar != null)
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(_avatar, x, avatarY, avatarSize, avatarSize);
            }
            else
            {
                using (var brush = new SolidBrush(DarkTheme.Toolbar))
                {
                    g.FillRectangle(brush, x, avatarY, avatarSize, avatarSize);
                }
            }

            x += avatarSize + 10;

            // Persona name
            using (var nameFont = new Font("Segoe UI", 12f, FontStyle.Bold))
            using (var nameBrush = new SolidBrush(DarkTheme.TextBright))
            {
                var nameSize = g.MeasureString(_personaName, nameFont);
                g.DrawString(_personaName, nameFont, nameBrush, x, avatarY + 2);

                // Status dot + text below the name
                int statusY = avatarY + (int)nameSize.Height + 4;
                Color dotColor = GetStatusColor(_personaState);
                using (var dotBrush = new SolidBrush(dotColor))
                {
                    g.FillEllipse(dotBrush, x, statusY + 2, 8, 8);
                }

                string statusText = GetStatusText(_personaState);
                using (var statusFont = new Font("Segoe UI", 8.25f))
                using (var statusBrush = new SolidBrush(DarkTheme.TextSecondary))
                {
                    g.DrawString(statusText, statusFont, statusBrush, x + 12, statusY);
                }

                // Country code next to status
                if (!string.IsNullOrEmpty(_countryCode))
                {
                    using (var measureFont = new Font("Segoe UI", 8.25f))
                    {
                        var statusSize = g.MeasureString(statusText, measureFont);
                        using (var countryBrush = new SolidBrush(DarkTheme.TextSecondary))
                        {
                            g.DrawString(_countryCode, measureFont, countryBrush,
                                x + 12 + statusSize.Width + 6, statusY);
                        }
                    }
                }

                // Right section starts after name area
                int rightX = x + (int)nameSize.Width + 30;
                if (rightX < 250) rightX = 250;

                // Level badge circle (28x28)
                int badgeSize = 28;
                int badgeY = avatarY + (avatarSize - badgeSize) / 2;
                using (var accentBrush = new SolidBrush(DarkTheme.Accent))
                {
                    g.FillEllipse(accentBrush, rightX, badgeY, badgeSize, badgeSize);
                }
                string levelText = _level.ToString();
                using (var levelFont = new Font("Segoe UI", 9f, FontStyle.Bold))
                using (var whiteBrush = new SolidBrush(Color.White))
                {
                    var levelSize = g.MeasureString(levelText, levelFont);
                    float lx = rightX + (badgeSize - levelSize.Width) / 2;
                    float ly = badgeY + (badgeSize - levelSize.Height) / 2;
                    g.DrawString(levelText, levelFont, whiteBrush, lx, ly);
                }

                // XP progress bar
                int barX = rightX + badgeSize + 14;
                int barWidth = 100;
                int barHeight = 8;
                int barY = badgeY + 4;

                int totalXp = _xp + _xpNeeded;
                float progress = totalXp > 0 ? (float)_xp / totalXp : 0f;
                int fillWidth = (int)(barWidth * progress);

                using (var bgBrush = new SolidBrush(DarkTheme.Border))
                {
                    g.FillRectangle(bgBrush, barX, barY, barWidth, barHeight);
                }
                if (fillWidth > 0)
                {
                    using (var fillBrush = new SolidBrush(DarkTheme.Accent))
                    {
                        g.FillRectangle(fillBrush, barX, barY, fillWidth, barHeight);
                    }
                }

                // XP text
                string xpText = string.Format("XP: {0}/{1}", _xp, totalXp);
                using (var xpFont = new Font("Segoe UI", 7.5f))
                using (var xpBrush = new SolidBrush(DarkTheme.TextSecondary))
                {
                    g.DrawString(xpText, xpFont, xpBrush, barX, barY + barHeight + 2);
                }

                // Badge count
                int badgeCountX = barX + barWidth + 14;
                string badgeCountText = string.Format("Badges: {0}", _badgeCount);
                using (var bcFont = new Font("Segoe UI", 8.25f))
                using (var bcBrush = new SolidBrush(DarkTheme.TextSecondary))
                {
                    g.DrawString(badgeCountText, bcFont, bcBrush, badgeCountX, badgeY + 6);
                }
            }
        }
    }
}
