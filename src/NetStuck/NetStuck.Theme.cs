using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace NetStuck
{
    // Keep the original light colors as semantic keys so switching themes is reversible.
    static class UiPalette
    {
        public static bool Dark;
        public static bool IsDark { get { return Dark && !SystemInformation.HighContrast; } }
        static Color R(int rgb) { return Color.FromArgb((rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255); }
        static readonly Dictionary<int, Color> backgrounds = new Dictionary<int, Color> {
            {0xFFFFFF,R(0x1B2433)}, {0xF5F7FA,R(0x111827)}, {0xF8FAFC,R(0x222E40)},
            {0xFAFBFD,R(0x1E293B)}, {0xDBEAFE,R(0x214F82)}, {0xEFF6FF,R(0x1D3553)},
            {0xF0FDF4,R(0x173D2C)}, {0xFEF2F2,R(0x46252C)}, {0xFFFBEB,R(0x44371C)},
            {0xDCFCE7,R(0x174832)}, {0xE2E8F0,R(0x334155)}, {0xCBD5E1,R(0x42516A)},
            {0xFEE2E2,R(0x612B34)}, {0x2563EB,R(0x2563EB)}, {0x1D4ED8,R(0x1D4ED8)},
            {0xDC2626,R(0xB91C1C)}, {0x16A34A,R(0x166534)}, {0xD97706,R(0x92400E)},
            {0xF97316,R(0x9A3412)}
        };
        static readonly Dictionary<int, Color> foregrounds = new Dictionary<int, Color> {
            {0x000000,R(0xE5E7EB)}, {0x1E293B,R(0xE2E8F0)}, {0x475569,R(0xBCC9DB)},
            {0x64748B,R(0xA9B8CE)}, {0xDAE0E8,R(0x46566E)}, {0x94A3B8,R(0x8B9BB4)},
            {0xCBD5E1,R(0x52617A)}, {0x2563EB,R(0x93C5FD)}, {0x1D4ED8,R(0xA4CCFF)},
            {0x16A34A,R(0x86EFAC)}, {0x166534,R(0xA0F0BC)}, {0x15803D,R(0x91E8AC)},
            {0xDC2626,R(0xFCA5A5)}, {0xB91C1C,R(0xFFB4B4)}, {0xD97706,R(0xFCD34D)},
            {0x86EFAC,R(0x4C9D6D)}, {0xFCA5A5,R(0xBA6673)}, {0xF97316,R(0xFDBA74)}
        };
        static int Key(Color color) { return color.ToArgb() & 0xFFFFFF; }
        public static Color Background(Color light)
        {
            if (light.IsEmpty || light.A == 0) return light;
            if (SystemInformation.HighContrast) return SystemColors.Window;
            if (!IsDark) return light;
            if (light.IsSystemColor) return R(0x1B2433);
            Color dark; return backgrounds.TryGetValue(Key(light), out dark) ? dark : light;
        }
        public static Color Foreground(Color light)
        {
            if (light.IsEmpty || light.A == 0) return light;
            if (SystemInformation.HighContrast) return SystemColors.WindowText;
            if (!IsDark) return light;
            if (light.IsSystemColor) return light == SystemColors.GrayText ? R(0xA9B8CE) : R(0xE5E7EB);
            Color dark; return foregrounds.TryGetValue(Key(light), out dark) ? dark : light;
        }
        public static Color ToLight(Color current, bool background, bool wasDark)
        {
            if (!wasDark || current.IsEmpty || current.A == 0 || current.IsSystemColor) return current;
            foreach (var entry in background ? backgrounds : foregrounds)
                if (entry.Value.ToArgb() == current.ToArgb()) return R(entry.Key);
            // Status colors are also assigned as filled button backgrounds.
            if (background) foreach (var entry in foregrounds)
                if (entry.Value.ToArgb() == current.ToArgb()) return R(entry.Key);
            return current;
        }
        public static Color Selection { get { return SystemInformation.HighContrast ? SystemColors.Highlight : Background(R(0xDBEAFE)); } }
        public static Color SelectionText { get { return SystemInformation.HighContrast ? SystemColors.HighlightText : IsDark ? Color.White : UiTokens.Text; } }
    }

    sealed class ThemeColors
    {
        readonly Control control;
        Color lightBack, lightFore, lastBack, lastFore;
        Color lightBorder, lightHover, lightPressed, lastBorder, lastHover, lastPressed;
        bool inheritsBack, inheritsFore;
        bool applied;
        public ThemeColors(Control control) { this.control = control; }
        public void Capture(bool wasDark)
        {
            var properties = System.ComponentModel.TypeDescriptor.GetProperties(control);
            inheritsBack = !(control is Form) && !properties["BackColor"].ShouldSerializeValue(control);
            inheritsFore = !(control is Form) && !properties["ForeColor"].ShouldSerializeValue(control);
            if (!applied || control.BackColor != lastBack) lightBack = UiPalette.ToLight(control.BackColor, true, wasDark);
            if (!applied || control.ForeColor != lastFore) lightFore = UiPalette.ToLight(control.ForeColor, false, wasDark);
            var button = control as Button;
            if (button != null)
            {
                if (!applied || button.FlatAppearance.BorderColor != lastBorder) lightBorder = UiPalette.ToLight(button.FlatAppearance.BorderColor, false, wasDark);
                if (!applied || button.FlatAppearance.MouseOverBackColor != lastHover) lightHover = UiPalette.ToLight(button.FlatAppearance.MouseOverBackColor, true, wasDark);
                if (!applied || button.FlatAppearance.MouseDownBackColor != lastPressed) lightPressed = UiPalette.ToLight(button.FlatAppearance.MouseDownBackColor, true, wasDark);
            }
        }
        public void ApplyStored()
        {
            control.BackColor = inheritsBack ? Color.Empty : UiPalette.Background(lightBack);
            control.ForeColor = inheritsFore ? Color.Empty : UiPalette.Foreground(lightFore);
            lastBack = control.BackColor; lastFore = control.ForeColor; applied = true;
            var button = control as Button;
            if (button != null)
            {
                bool filled = lastFore.ToArgb() == Color.White.ToArgb();
                lastBorder = UiPalette.Foreground(lightBorder);
                lastHover = UiPalette.IsDark && lightHover.IsEmpty ? (filled ? ControlPaint.Light(lastBack, 0.08f) : UiTokens.HoverSurface) : UiPalette.Background(lightHover);
                lastPressed = UiPalette.IsDark && lightPressed.IsEmpty ? (filled ? ControlPaint.Dark(lastBack, 0.12f) : UiTokens.PressedSurface) : UiPalette.Background(lightPressed);
                button.FlatAppearance.BorderColor = lastBorder;
                button.FlatAppearance.MouseOverBackColor = lastHover;
                button.FlatAppearance.MouseDownBackColor = lastPressed;
            }
        }
    }

    public sealed partial class MainForm
    {
        readonly Dictionary<Control, ThemeColors> themeControls = new Dictionary<Control, ThemeColors>();
        ComboBox themeSelector;
        bool changingTheme;

        Control BuildThemeSelector()
        {
            var panel = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Anchor = AnchorStyles.Right, WrapContents = false, Margin = new Padding(12, 0, 0, 0) };
            panel.Controls.Add(new Label { Text = "Theme", AutoSize = true, Margin = new Padding(0, 6, 6, 0), ForeColor = TextMuted });
            themeSelector = new ComboBox { Name = "themeSelector", AccessibleName = "Application theme", AccessibleDescription = "Choose Light or Dark. Saved on this computer.", DropDownStyle = ComboBoxStyle.DropDownList, Width = 90, Margin = new Padding(0, 2, 0, 0) };
            themeSelector.Items.AddRange(new object[] { "Light", "Dark" });
            themeSelector.SelectedIndex = UiPalette.Dark ? 1 : 0;
            themeSelector.SelectedIndexChanged += delegate { if (!changingTheme) { SetApplicationTheme(themeSelector.SelectedIndex == 1); SaveAppState(); } };
            panel.Controls.Add(themeSelector); return panel;
        }

        void SetApplicationTheme(bool dark)
        {
            bool wasDark = UiPalette.IsDark;
            changingTheme = true;
            SuspendLayout();
            try
            {
                // Capture every inherited color before changing any parent surface.
                foreach (var entry in themeControls.ToArray()) if (!entry.Key.IsDisposed) entry.Value.Capture(wasDark);
                UiPalette.Dark = dark;
                if (themeSelector != null) themeSelector.SelectedIndex = dark ? 1 : 0;
                foreach (var entry in themeControls.ToArray())
                    if (!entry.Key.IsDisposed) { entry.Value.ApplyStored(); ApplyThemeDetails(entry.Key, wasDark); }
                Invalidate(true);
            }
            finally { ResumeLayout(true); changingTheme = false; }
        }

        void BindTheme(Control control)
        {
            if (control.IsDisposed || control is ScrollBar) return;
            // Terminal output has its own deliberate, readable dark palette in both modes.
            if (control == collectorTerminal) return;
            ThemeColors colors;
            if (!themeControls.TryGetValue(control, out colors))
            {
                colors = new ThemeColors(control); themeControls.Add(control, colors);
                control.Disposed += delegate { themeControls.Remove(control); };
                control.ControlAdded += delegate(object sender, ControlEventArgs e) { BindTheme(e.Control); };
                var combo = control as ComboBox;
                if (combo != null && combo.DrawMode == DrawMode.Normal)
                {
                    combo.DrawMode = DrawMode.OwnerDrawFixed;
                    combo.DrawItem += delegate(object sender, DrawItemEventArgs e)
                    {
                        bool selected = (e.State & DrawItemState.Selected) != 0;
                        using (var brush = new SolidBrush(selected ? UiPalette.Selection : Surface)) e.Graphics.FillRectangle(brush, e.Bounds);
                        string value = e.Index >= 0 && e.Index < combo.Items.Count ? combo.GetItemText(combo.Items[e.Index]) : combo.Text;
                        TextRenderer.DrawText(e.Graphics, value, combo.Font, e.Bounds, selected ? UiPalette.SelectionText : combo.Enabled ? TextMain : TextMuted, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                        if ((e.State & DrawItemState.Focus) != 0) e.DrawFocusRectangle();
                    };
                }
                var innerTabs = control as TabControl;
                if (innerTabs != null && innerTabs != tabs)
                {
                    innerTabs.DrawMode = TabDrawMode.OwnerDrawFixed;
                    innerTabs.DrawItem += delegate(object sender, DrawItemEventArgs e)
                    {
                        if (e.Index < 0 || e.Index >= innerTabs.TabCount) return;
                        bool selected = innerTabs.SelectedIndex == e.Index;
                        using (var brush = new SolidBrush(selected ? Surface : Canvas)) e.Graphics.FillRectangle(brush, e.Bounds);
                        TextRenderer.DrawText(e.Graphics, innerTabs.TabPages[e.Index].Text, innerTabs.Font, e.Bounds, selected ? TextMain : TextMuted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                        if (innerTabs.Focused && selected) ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(e.Bounds, -3, -3), TextMain, Surface);
                    };
                }
                var button = control as Button;
                if (button != null) button.Paint += delegate(object sender, PaintEventArgs e)
                {
                    if (button.Enabled || !UiPalette.IsDark) return;
                    using (var brush = new SolidBrush(Surface)) e.Graphics.FillRectangle(brush, button.ClientRectangle);
                    using (var pen = new Pen(Border)) e.Graphics.DrawRectangle(pen, 0, 0, button.Width - 1, button.Height - 1);
                    TextRenderer.DrawText(e.Graphics, button.Text, button.Font, Rectangle.Inflate(button.ClientRectangle, -3, -2), TextMuted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                };
            }
            colors.Capture(UiPalette.IsDark); colors.ApplyStored();
            ApplyThemeDetails(control, UiPalette.IsDark);
            foreach (Control child in control.Controls) BindTheme(child);
        }

        void ApplyThemeDetails(Control control, bool wasDark)
        {
            var grid = control as DataGridView;
            if (grid != null)
            {
                grid.BackgroundColor = Surface; grid.GridColor = Border; grid.EnableHeadersVisualStyles = false;
                grid.DefaultCellStyle.BackColor = Surface; grid.DefaultCellStyle.ForeColor = TextMain;
                grid.DefaultCellStyle.SelectionBackColor = UiPalette.Selection; grid.DefaultCellStyle.SelectionForeColor = UiPalette.SelectionText;
                grid.ColumnHeadersDefaultCellStyle.BackColor = UiTokens.SurfaceSubtle; grid.ColumnHeadersDefaultCellStyle.ForeColor = TextMain;
                grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = UiTokens.SurfaceSubtle; grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = TextMain;
                grid.AlternatingRowsDefaultCellStyle.BackColor = UiPalette.Background(Color.FromArgb(250, 251, 253));
                grid.RowHeadersDefaultCellStyle.BackColor = UiTokens.SurfaceSubtle; grid.RowHeadersDefaultCellStyle.ForeColor = TextMain;
                grid.Invalidate();
            }
            var input = control as TextBox;
            if (input != null) { input.BackColor = input.ReadOnly ? UiTokens.SurfaceSubtle : Surface; input.ForeColor = TextMain; }
            if (control is ComboBox || control is NumericUpDown || control is ListBox)
            { control.BackColor = Surface; control.ForeColor = TextMain; }
            var check = control as CheckBox;
            if (check != null) check.FlatStyle = UiPalette.IsDark ? FlatStyle.Flat : FlatStyle.Standard;
            var page = control as TabPage;
            if (page != null) page.UseVisualStyleBackColor = false;
            var button = control as Button;
            if (button != null)
            {
                button.UseVisualStyleBackColor = false;
                if (SystemInformation.HighContrast) { button.BackColor = SystemColors.Control; button.ForeColor = SystemColors.ControlText; }
                button.Invalidate();
            }
            var strip = control as ToolStrip;
            if (strip != null)
            {
                strip.BackColor = Surface; strip.ForeColor = TextMuted;
                strip.Renderer = new ToolStripSystemRenderer();
                foreach (ToolStripItem item in strip.Items)
                {
                    item.BackColor = Surface;
                    item.ForeColor = UiPalette.Foreground(UiPalette.ToLight(item.ForeColor, false, wasDark));
                }
            }
            var presenter = control as UiStatePresenter;
            if (presenter != null) presenter.RefreshTheme();
            control.Invalidate();
        }
    }
}
