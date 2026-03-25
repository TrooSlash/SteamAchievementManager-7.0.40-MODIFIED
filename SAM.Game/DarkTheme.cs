/* Dark theme support for WinForms controls — 2026 modern dark palette */

using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SAM.Game
{
    internal static class DarkTheme
    {
        public static readonly Color DarkBackground = Color.FromArgb(24, 26, 32);    // #181A20
        public static readonly Color Surface = Color.FromArgb(30, 32, 40);           // #1E2028
        public static readonly Color Toolbar = Color.FromArgb(37, 40, 48);           // #252830
        public static readonly Color Accent = Color.FromArgb(108, 99, 255);          // #6C63FF
        public static readonly Color AccentSecondary = Color.FromArgb(0, 217, 163);  // #00D9A3
        public static readonly Color AccentWarning = Color.FromArgb(255, 179, 71);   // #FFB347
        public static readonly Color AccentDanger = Color.FromArgb(255, 107, 107);   // #FF6B6B
        public static readonly Color Text = Color.FromArgb(232, 234, 237);           // #E8EAED
        public static readonly Color TextSecondary = Color.FromArgb(154, 160, 166);  // #9AA0A6
        public static readonly Color TextMuted = Color.FromArgb(95, 99, 104);        // #5F6368
        public static readonly Color Border = Color.FromArgb(45, 48, 56);            // #2D3038
        public static readonly Color Hover = Color.FromArgb(42, 45, 54);             // #2A2D36
        public static readonly Color Pressed = Color.FromArgb(108, 99, 255);         // #6C63FF
        public static readonly Color StatusBar = Color.FromArgb(37, 40, 48);         // #252830
        public static readonly Color Selection = Color.FromArgb(46, 43, 74);         // #2E2B4A
        public static readonly Color DangerBackground = Color.FromArgb(80, 20, 20);  // #501414
        public static readonly Color DangerSurface = Color.FromArgb(60, 15, 15);     // #3C0F0F
        public static readonly Color DangerText = Color.FromArgb(255, 180, 180);     // #FFB4B4
        public static readonly Color ProtectedText = Color.FromArgb(180, 140, 100);  // #B48C64

        public static void Apply(Form form)
        {
            form.BackColor = DarkBackground;
            form.ForeColor = Text;
            ApplyRecursive(form);
        }

        private static void ApplyRecursive(Control parent)
        {
            foreach (Control control in parent.Controls)
            {
                switch (control)
                {
                    case StatusStrip ss:
                        ss.Renderer = new DarkToolStripRenderer();
                        ss.BackColor = StatusBar;
                        ss.ForeColor = Text;
                        foreach (ToolStripItem item in ss.Items)
                        {
                            item.ForeColor = Text;
                        }
                        break;

                    case ToolStrip ts:
                        ts.Renderer = new DarkToolStripRenderer();
                        ts.BackColor = Toolbar;
                        ts.ForeColor = Text;
                        foreach (ToolStripItem item in ts.Items)
                        {
                            item.ForeColor = Text;
                            if (item is ToolStripTextBox tb)
                            {
                                tb.BackColor = Surface;
                                tb.ForeColor = Text;
                                tb.BorderStyle = BorderStyle.FixedSingle;
                            }
                            if (item is ToolStripComboBox cb)
                            {
                                cb.BackColor = Surface;
                                cb.ForeColor = Text;
                            }
                        }
                        break;

                    case TabControl tc:
                        tc.DrawMode = TabDrawMode.OwnerDrawFixed;
                        tc.DrawItem += DrawTabControlItem;
                        tc.Paint += PaintTabControlBorder;
                        foreach (TabPage tp in tc.TabPages)
                        {
                            tp.BackColor = DarkBackground;
                            tp.ForeColor = Text;
                        }
                        break;

                    case DataGridView dgv:
                        dgv.BackgroundColor = DarkBackground;
                        dgv.GridColor = Border;
                        dgv.DefaultCellStyle.BackColor = Surface;
                        dgv.DefaultCellStyle.ForeColor = Text;
                        dgv.DefaultCellStyle.SelectionBackColor = Selection;
                        dgv.DefaultCellStyle.SelectionForeColor = Text;
                        dgv.ColumnHeadersDefaultCellStyle.BackColor = Toolbar;
                        dgv.ColumnHeadersDefaultCellStyle.ForeColor = Text;
                        dgv.EnableHeadersVisualStyles = false;
                        dgv.RowHeadersDefaultCellStyle.BackColor = Toolbar;
                        dgv.RowHeadersDefaultCellStyle.ForeColor = Text;
                        dgv.BorderStyle = BorderStyle.None;
                        break;

                    case CheckBox cb:
                        cb.ForeColor = Text;
                        cb.BackColor = DarkBackground;
                        break;

                    case TextBox textBox:
                        textBox.BackColor = Surface;
                        textBox.ForeColor = Text;
                        textBox.BorderStyle = BorderStyle.FixedSingle;
                        break;

                    case ComboBox comboBox:
                        comboBox.BackColor = Surface;
                        comboBox.ForeColor = Text;
                        comboBox.FlatStyle = FlatStyle.Flat;
                        break;

                    case Button btn:
                        btn.BackColor = Toolbar;
                        btn.ForeColor = Text;
                        btn.FlatStyle = FlatStyle.Flat;
                        btn.FlatAppearance.BorderColor = Border;
                        btn.FlatAppearance.MouseOverBackColor = Hover;
                        btn.FlatAppearance.MouseDownBackColor = Accent;
                        break;

                    case ListView lv:
                        lv.BackColor = DarkBackground;
                        lv.ForeColor = Text;
                        break;
                }

                ApplyRecursive(control);
            }
        }

        private static void DrawTabControlItem(object sender, DrawItemEventArgs e)
        {
            var tc = (TabControl)sender;
            var tab = tc.TabPages[e.Index];
            var isSelected = tc.SelectedIndex == e.Index;

            using (var brush = new SolidBrush(isSelected ? DarkBackground : Toolbar))
            {
                e.Graphics.FillRectangle(brush, e.Bounds);
            }

            if (isSelected)
            {
                using (var accentPen = new Pen(Accent, 2))
                {
                    e.Graphics.DrawLine(accentPen, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
                }
            }

            using (var brush = new SolidBrush(isSelected ? Text : TextSecondary))
            {
                var sf = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };
                e.Graphics.DrawString(tab.Text, e.Font, brush, e.Bounds, sf);
            }
        }

        private static void PaintTabControlBorder(object sender, PaintEventArgs e)
        {
            var tc = (TabControl)sender;
            // Paint over the native 3D border around the tab content area
            using (var pen = new Pen(DarkBackground, 4))
            {
                var contentRect = tc.SelectedTab?.Bounds ?? tc.DisplayRectangle;
                // The content area border is drawn by Windows just outside DisplayRectangle
                var borderRect = Rectangle.Inflate(tc.DisplayRectangle, 2, 2);
                e.Graphics.DrawRectangle(pen, borderRect);
            }
        }

        internal static GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            var path = new GraphicsPath();
            if (radius <= 0)
            {
                path.AddRectangle(bounds);
                return path;
            }
            int d = radius * 2;
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    internal class DarkToolStripRenderer : ToolStripProfessionalRenderer
    {
        public DarkToolStripRenderer() : base(new DarkColorTable()) { }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = DarkTheme.Text;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            Color bg = e.ToolStrip is StatusStrip ? DarkTheme.StatusBar : DarkTheme.Toolbar;
            using (var brush = new SolidBrush(bg))
            {
                e.Graphics.FillRectangle(brush, e.AffectedBounds);
            }
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            // suppress default border
        }

        protected override void OnRenderButtonBackground(ToolStripItemRenderEventArgs e)
        {
            var rect = new Rectangle(2, 2, e.Item.Size.Width - 4, e.Item.Size.Height - 4);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            Color bgColor;
            if (e.Item.Pressed)
                bgColor = DarkTheme.Pressed;
            else if (e.Item.Selected)
                bgColor = DarkTheme.Hover;
            else if (e.Item is ToolStripButton tsb && tsb.Checked)
                bgColor = DarkTheme.Accent;
            else
                bgColor = Color.Transparent;

            if (bgColor != Color.Transparent)
            {
                using (var path = DarkTheme.RoundedRect(rect, 4))
                using (var brush = new SolidBrush(bgColor))
                {
                    e.Graphics.FillPath(brush, path);
                }
            }

            e.Graphics.SmoothingMode = SmoothingMode.Default;
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            using (var pen = new Pen(DarkTheme.Border))
            {
                if (e.Vertical)
                {
                    int x = e.Item.Width / 2;
                    e.Graphics.DrawLine(pen, x, 4, x, e.Item.Height - 4);
                }
                else
                {
                    int y = e.Item.Height / 2;
                    e.Graphics.DrawLine(pen, 4, y, e.Item.Width - 4, y);
                }
            }
        }

        protected override void OnRenderDropDownButtonBackground(ToolStripItemRenderEventArgs e)
        {
            var rect = new Rectangle(2, 2, e.Item.Size.Width - 4, e.Item.Size.Height - 4);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            if (e.Item.Selected || e.Item.Pressed)
            {
                Color bgColor = e.Item.Pressed ? DarkTheme.Pressed : DarkTheme.Hover;
                using (var path = DarkTheme.RoundedRect(rect, 4))
                using (var brush = new SolidBrush(bgColor))
                {
                    e.Graphics.FillPath(brush, path);
                }
            }

            e.Graphics.SmoothingMode = SmoothingMode.Default;
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            var rect = new Rectangle(Point.Empty, e.Item.Size);
            Color bgColor = e.Item.Selected ? DarkTheme.Hover : DarkTheme.Toolbar;
            using (var brush = new SolidBrush(bgColor))
            {
                e.Graphics.FillRectangle(brush, rect);
            }
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            var rect = e.ImageRectangle;
            rect.Inflate(2, 2);
            using (var brush = new SolidBrush(DarkTheme.Accent))
            {
                e.Graphics.FillRectangle(brush, rect);
            }
            base.OnRenderItemCheck(e);
        }

        protected override void OnRenderStatusStripSizingGrip(ToolStripRenderEventArgs e)
        {
            // suppress
        }
    }

    internal class DarkColorTable : ProfessionalColorTable
    {
        public override Color MenuBorder => DarkTheme.Border;
        public override Color MenuItemBorder => DarkTheme.Border;
        public override Color MenuItemSelected => DarkTheme.Hover;
        public override Color MenuItemSelectedGradientBegin => DarkTheme.Hover;
        public override Color MenuItemSelectedGradientEnd => DarkTheme.Hover;
        public override Color MenuItemPressedGradientBegin => DarkTheme.Pressed;
        public override Color MenuItemPressedGradientEnd => DarkTheme.Pressed;
        public override Color MenuStripGradientBegin => DarkTheme.Toolbar;
        public override Color MenuStripGradientEnd => DarkTheme.Toolbar;
        public override Color ToolStripBorder => DarkTheme.Border;
        public override Color ToolStripDropDownBackground => DarkTheme.Toolbar;
        public override Color ToolStripGradientBegin => DarkTheme.Toolbar;
        public override Color ToolStripGradientEnd => DarkTheme.Toolbar;
        public override Color ToolStripGradientMiddle => DarkTheme.Toolbar;
        public override Color ImageMarginGradientBegin => DarkTheme.Toolbar;
        public override Color ImageMarginGradientEnd => DarkTheme.Toolbar;
        public override Color ImageMarginGradientMiddle => DarkTheme.Toolbar;
        public override Color SeparatorDark => DarkTheme.Border;
        public override Color SeparatorLight => DarkTheme.Border;
        public override Color StatusStripGradientBegin => DarkTheme.StatusBar;
        public override Color StatusStripGradientEnd => DarkTheme.StatusBar;
        public override Color GripDark => DarkTheme.Border;
        public override Color GripLight => DarkTheme.Toolbar;
        public override Color ButtonSelectedHighlight => DarkTheme.Hover;
        public override Color ButtonSelectedHighlightBorder => DarkTheme.Border;
        public override Color ButtonPressedHighlight => DarkTheme.Pressed;
        public override Color ButtonPressedHighlightBorder => DarkTheme.Border;
        public override Color ButtonCheckedHighlight => DarkTheme.Accent;
        public override Color ButtonCheckedHighlightBorder => DarkTheme.Border;
        public override Color ButtonSelectedBorder => DarkTheme.Border;
        public override Color ButtonSelectedGradientBegin => DarkTheme.Hover;
        public override Color ButtonSelectedGradientEnd => DarkTheme.Hover;
        public override Color ButtonSelectedGradientMiddle => DarkTheme.Hover;
        public override Color ButtonPressedBorder => DarkTheme.Accent;
        public override Color ButtonPressedGradientBegin => DarkTheme.Pressed;
        public override Color ButtonPressedGradientEnd => DarkTheme.Pressed;
        public override Color ButtonPressedGradientMiddle => DarkTheme.Pressed;
        public override Color ButtonCheckedGradientBegin => DarkTheme.Accent;
        public override Color ButtonCheckedGradientEnd => DarkTheme.Accent;
        public override Color ButtonCheckedGradientMiddle => DarkTheme.Accent;
        public override Color CheckBackground => DarkTheme.Accent;
        public override Color CheckPressedBackground => DarkTheme.Pressed;
        public override Color CheckSelectedBackground => DarkTheme.Accent;
        public override Color OverflowButtonGradientBegin => DarkTheme.Toolbar;
        public override Color OverflowButtonGradientEnd => DarkTheme.Toolbar;
        public override Color OverflowButtonGradientMiddle => DarkTheme.Toolbar;
        public override Color RaftingContainerGradientBegin => DarkTheme.Toolbar;
        public override Color RaftingContainerGradientEnd => DarkTheme.Toolbar;
    }
}
