using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace NetStuck
{
    // Logical 96-DPI design dimensions. Framework autoscaling owns containers;
    // LayoutPixels is used only for dimensions computed after construction.
    static class V2Design
    {
        public const int SpaceXL = 24;
        public const int SidebarWidth = 184;
        public const int MinimumSidebarWidth = 168;
        public const int PageHeaderHeight = 84;
        public const int NavigationHeight = 38;
        public const int SectionGap = 12;
        public const int FieldGap = 6;
        public const int ToolbarGap = 8;
        public const int IconSize = 20;
        public const int ConfigurationWidth = 340;
        public const int PingActionWidth = 96;
        public const int ProbeCardHeight = 312;
        public const float PageTitleSize = 22f;
        public const float BrandTitleSize = 13f;
    }

    // Keep native page ownership, keyboard traversal, disposal and persisted
    // 1.x tab indices. Visible navigation is supplied by accessible buttons.
    sealed class WorkspaceTabs : TabControl
    {
        public override Rectangle DisplayRectangle { get { return ClientRectangle; } }
        protected override void WndProc(ref Message message)
        {
            if (message.Msg == 0x1328 && !DesignMode) { message.Result = (IntPtr)1; return; }
            base.WndProc(ref message);
        }
    }

    public sealed partial class MainForm
    {
        readonly Dictionary<string, Button> navigationV2 = new Dictionary<string, Button>(StringComparer.OrdinalIgnoreCase);
        Label pageTitleV2, pageDescriptionV2;
        Panel sidebarV2;
        Button presetsV2, loadTargetsV2;
        SplitContainer pingHistoryDetailSplitV2;
        TextBox pingHistoryDetailV2;
        NumericUpDown settingsZoomV2;
        Button pingCardsButtonV2;
        bool synchronizingSettingsV2;
        readonly Font navigationRegularV2 = new Font(UiTokens.FontFamily, UiTokens.ActionFontSize);
        readonly Font navigationSelectedV2 = new Font(UiTokens.FontFamily, UiTokens.ActionFontSize, FontStyle.Bold);
        readonly Dictionary<Button, string> navigationGlyphsV2 = new Dictionary<Button, string>();
        readonly Dictionary<Button, int> navigationIconColorsV2 = new Dictionary<Button, int>();
        bool presentationDisposedV2;

        void ComposeShellV2(Control header, TableLayoutPanel headerLayout, Label title, Label description,
            PictureBox logo, Label version)
        {
            header.Height = V2Design.PageHeaderHeight;
            header.BackColor = Canvas;
            header.Padding = new Padding(UiTokens.SpaceLg, UiTokens.SpaceSm, UiTokens.SpaceLg, UiTokens.SpaceSm);
            pageTitleV2 = title; pageDescriptionV2 = description;
            title.Font = new Font(UiTokens.FontFamily, V2Design.PageTitleSize, FontStyle.Bold);
            title.TextAlign = ContentAlignment.MiddleLeft;
            description.Font = new Font(UiTokens.FontFamily, UiTokens.CaptionFontSize);
            var identity = (TableLayoutPanel)title.Parent;
            identity.RowStyles[0].Height = 38;
            identity.Margin = new Padding(0);
            headerLayout.Controls.Remove(logo); headerLayout.Controls.Remove(version);
            // Identity + compact workflow actions; the theme selector remains native.
            headerLayout.ColumnStyles.Clear(); headerLayout.ColumnCount = 3;
            headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            Control theme = headerLayout.Controls.Cast<Control>().First(c => c != identity);
            headerLayout.Controls.Remove(identity); headerLayout.Controls.Remove(theme);
            headerLayout.Controls.Add(identity, 0, 0);
            var tools = new FlowLayoutPanel { AutoSize = true, Anchor = AnchorStyles.Right, WrapContents = false, Margin = new Padding(0) };
            presetsV2 = ActionButton("Presets", false, 88);
            presetsV2.Name = "pagePresets";
            presetsV2.Click += delegate
            {
                var menu = new ContextMenuStrip();
                menu.Items.Add("Load selected saved list", null, delegate { LoadSelectedProfile(null, EventArgs.Empty); });
                menu.Items.Add("Save current targets as a list", null, delegate { SaveCurrentProfile(null, EventArgs.Empty); });
                ShowV2Menu(menu, presetsV2);
            };
            loadTargetsV2 = ActionButton("Load targets", false, 112);
            loadTargetsV2.Name = "loadTargetsFile";
            loadTargetsV2.Click += async delegate { await LoadTargetsFileV2(); };
            var options = ActionButton("Options", false, 88); options.Name = "pageOptions";
            options.Click += delegate
            {
                var menu = new ContextMenuStrip();
                menu.Items.Add("Settings", null, delegate { NavigateV2("Settings"); });
                menu.Items.Add("Updates / version recovery", null, delegate { NavigateV2("Updates"); });
                menu.Items.Add(new ToolStripSeparator());
                menu.Items.Add("Light theme", null, delegate { SetApplicationTheme(false); SaveAppState(); });
                menu.Items.Add("Dark theme", null, delegate { SetApplicationTheme(true); SaveAppState(); });
                ShowV2Menu(menu, options);
            };
            tools.Controls.AddRange(new Control[] { presetsV2, loadTargetsV2, options });
            headerLayout.Controls.Add(tools, 1, 0); headerLayout.Controls.Add(theme, 2, 0);

            sidebarV2 = new Panel { Name = "applicationSidebar", Dock = DockStyle.Fill, BackColor = UiTokens.SurfaceSubtle,
                AccessibleName = "Main navigation", AccessibleRole = AccessibleRole.Grouping, Padding = new Padding(UiTokens.SpaceSm) };
            var brand = new TableLayoutPanel { Dock = DockStyle.Top, Height = 64, ColumnCount = 2, RowCount = 2, Margin = new Padding(0) };
            brand.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 36)); brand.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            brand.RowStyles.Add(new RowStyle(SizeType.Percent, 55)); brand.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
            logo.Size = new Size(30, 30); logo.Anchor = AnchorStyles.Left;
            brand.Controls.Add(logo, 0, 0); brand.SetRowSpan(logo, 2);
            brand.Controls.Add(new Label { Text = AppName, Dock = DockStyle.Fill, Font = new Font(UiTokens.FontFamily, V2Design.BrandTitleSize, FontStyle.Bold),
                TextAlign = ContentAlignment.BottomLeft, ForeColor = TextMain }, 1, 0);
            version.BackColor = Color.Empty; version.ForeColor = TextMuted; version.Padding = new Padding(0); version.Margin = new Padding(0);
            version.Anchor = AnchorStyles.Left; brand.Controls.Add(version, 1, 1);
            var navigationViewport = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            var navigation = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1, RowCount = 0, Margin = new Padding(0), Padding = new Padding(0) };
            navigation.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            AddNavigationV2(navigation, "Home", "Home", "\uE80F", false);
            AddNavigationGroupV2(navigation, "NETWORK MONITOR");
            AddNavigationV2(navigation, "Live Ping", "Ping Live", "\uE9D9", true);
            AddNavigationV2(navigation, "Traceroute", "Traceroute", "\uE8AB", true);
            AddNavigationV2(navigation, "DNS Resolver", "DNS Resolver", "\uE721", true);
            AddNavigationGroupV2(navigation, "NETWORK TOOLS");
            AddNavigationV2(navigation, "Config Collector", "Config Collector", "\uE8B7", false);
            AddNavigationV2(navigation, "MAC / WAN Lookup", "MAC / WAN Lookup", "\uE774", false);
            AddNavigationV2(navigation, "Calculators", "Calculators", "\uE8EF", false);
            AddNavigationV2(navigation, "Event Log", "Event Log", "\uE8A5", false);
            AddNavigationV2(navigation, "Updates", "Updates / recovery", "\uE895", false);
            AddNavigationV2(navigation, "Settings", "Settings", "\uE713", false);
            navigationViewport.Controls.Add(navigation);
            var footer = new Label { Dock = DockStyle.Bottom, Height = 64, Text = "NetStuck\r\nPortable network tools", ForeColor = TextMuted,
                Font = new Font(UiTokens.FontFamily, UiTokens.CaptionFontSize), TextAlign = ContentAlignment.BottomLeft, Padding = new Padding(8, 0, 0, 8) };
            sidebarV2.Controls.Add(navigationViewport); sidebarV2.Controls.Add(footer); sidebarV2.Controls.Add(brand);

            Controls.Remove(header); Controls.Remove(tabs);
            var workspace = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0) };
            workspace.Controls.Add(tabs); workspace.Controls.Add(header);
            var shell = new TableLayoutPanel { Name = "applicationShellV2", Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = new Padding(0) };
            shell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, V2Design.SidebarWidth));
            shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            shell.Controls.Add(sidebarV2, 0, 0); shell.Controls.Add(workspace, 1, 0);
            Controls.Add(shell); shell.BringToFront();
            tabs.SelectedIndexChanged += delegate { UpdateNavigationV2(); };
            tabs.SizeMode = TabSizeMode.Fixed; tabs.ItemSize = new Size(1, 1); tabs.Padding = Point.Empty; tabs.Appearance = TabAppearance.FlatButtons;
            // Updates text includes new-release/activity notices without changing page identity.
            tabs.ControlAdded += delegate(object sender, ControlEventArgs e) { e.Control.TextChanged += delegate { UpdateNavigationV2(); }; };
            KeyPreview = true;
            KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.Control && e.Shift && e.KeyCode == Keys.U) { NavigateV2("Updates"); e.Handled = e.SuppressKeyPress = true; }
            };
            Microsoft.Win32.SystemEvents.UserPreferenceChanged += PreferenceChangedV2;
            Disposed += delegate { Microsoft.Win32.SystemEvents.UserPreferenceChanged -= PreferenceChangedV2; };
        }

        void AddNavigationGroupV2(TableLayoutPanel parent, string text)
        {
            int row = parent.RowCount++;
            parent.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            parent.Controls.Add(new Label { Text = text, Dock = DockStyle.Fill, AutoSize = true, ForeColor = TextMuted,
                Font = new Font(UiTokens.FontFamily, 7.5f, FontStyle.Bold), Padding = new Padding(8, 14, 0, 5), Margin = new Padding(0) }, 0, row);
        }

        void AddNavigationV2(TableLayoutPanel parent, string id, string caption, string icon, bool nested)
        {
            int row = parent.RowCount++;
            parent.RowStyles.Add(new RowStyle(SizeType.Absolute, V2Design.NavigationHeight));
            var button = ActionButton(caption, false, 0);
            button.Name = "navigation-" + id.Replace(" ", "-").Replace("/", "-");
            button.AccessibleName = caption; button.AccessibleDescription = "Open " + caption + ".";
            button.Dock = DockStyle.Fill; button.TabIndex = row;
            button.Margin = new Padding(nested ? 8 : 0, 2, 0, 2); button.Padding = new Padding(6, 0, 0, 0);
            button.FlatAppearance.BorderSize = 0; button.TextAlign = ContentAlignment.MiddleLeft;
            button.TextImageRelation = TextImageRelation.ImageBeforeText; button.ImageAlign = ContentAlignment.MiddleLeft;
            navigationGlyphsV2.Add(button, icon);
            button.Click += delegate { NavigateV2(id); };
            navigationV2.Add(id, button); parent.Controls.Add(button, 0, row);
            // Native buttons retain accessible roles, keyboard activation and focus cues.
            button.Paint += delegate(object sender, PaintEventArgs e)
            {
                bool selected = pagesByName.ContainsKey(id) && tabs.SelectedTab == pagesByName[id];
                if (selected) using (var pen = new Pen(SystemInformation.HighContrast ? SystemColors.Highlight : Accent, 3))
                    e.Graphics.DrawLine(pen, 1, 4, 1, button.Height - 5);
                if (button.Focused) ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(button.ClientRectangle, -3, -3), button.ForeColor, button.BackColor);
            };
        }

        void NavigateV2(string id)
        {
            TabPage page;
            if (pagesByName.TryGetValue(id, out page)) { tabs.SelectedTab = page; UpdateNavigationV2(); }
        }

        void UpdateNavigationV2()
        {
            if (pageTitleV2 == null || tabs.SelectedTab == null) return;
            string selected = pagesByName.FirstOrDefault(pair => pair.Value == tabs.SelectedTab).Key;
            if (selected == null) return;
            pageTitleV2.Text = selected == "Live Ping" ? "Ping Live" : selected == "Updates" ? "Updates & recovery" : selected;
            pageDescriptionV2.Text = PageDescriptionV2(selected);
            if (pingCardsButtonV2 != null) pingCardsButtonV2.Text = pingRoot.RowStyles[0].Height < 1 ? "Show cards" : "Hide cards";
            presetsV2.Visible = loadTargetsV2.Visible = selected == "Live Ping";
            foreach (var pair in navigationV2)
            {
                bool active = pair.Key == selected;
                pair.Value.BackColor = active ? UiPalette.Selection : UiTokens.SurfaceSubtle;
                pair.Value.ForeColor = active ? UiPalette.SelectionText : TextMain;
                pair.Value.Font = active ? navigationSelectedV2 : navigationRegularV2;
                UpdateNavigationIconV2(pair.Value);
                int count; bool running = tabActivityCounts.TryGetValue(pair.Key, out count) && count > 0;
                string caption = pair.Key == "Live Ping" ? "Ping Live" : pair.Key == "Updates" ? "Updates / recovery" : pair.Key;
                pair.Value.Text = (running ? "\u25CF " : "") + caption;
                pair.Value.AccessibleDescription = "Open " + caption + (active ? ". Selected." : ".") + (running ? " Monitoring is running." : "");
            }
            if (settingsZoomV2 != null && !synchronizingSettingsV2)
            {
                synchronizingSettingsV2 = true;
                settingsZoomV2.Value = Math.Max(settingsZoomV2.Minimum, Math.Min(settingsZoomV2.Maximum, (decimal)Math.Round(zoomScale * 100)));
                synchronizingSettingsV2 = false;
            }
        }

        static string PageDescriptionV2(string id)
        {
            switch (id)
            {
                case "Live Ping": return "Monitor multiple hosts in real time with searchable results and event history";
                case "Traceroute": return "Two independent route sessions with hop statistics and change history";
                case "Config Collector": return "Collect device configurations with secure authentication and bounded parallel work";
                case "Calculators": return "IPv4 subnet planning and network unit conversions";
                case "Event Log": return "Search, filter and export application events";
                case "Settings": return "Appearance, text size and local preferences";
                case "Updates": return "Verified GitHub updates and recovery to an earlier version";
                case "DNS Resolver": return "Resolve forward and reverse DNS with optional continuous polling";
                case "MAC / WAN Lookup": return "Find MAC vendors and public IP ownership information";
                default: return "Choose a workspace to troubleshoot and review your network";
            }
        }

        void UpdateNavigationIconV2(Button button)
        {
            int prior;
            if (navigationIconColorsV2.TryGetValue(button, out prior) && prior == button.ForeColor.ToArgb()) return;
            var bitmap = new Bitmap(V2Design.IconSize, V2Design.IconSize);
            using (var graphics = Graphics.FromImage(bitmap)) using (var font = new Font("Segoe MDL2 Assets", 11f)) using (var brush = new SolidBrush(button.ForeColor))
            {
                graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                graphics.DrawString(navigationGlyphsV2[button], font, brush, new RectangleF(0, 1, bitmap.Width, bitmap.Height));
            }
            Image old = button.Image; button.Image = bitmap; if (old != null) old.Dispose();
            navigationIconColorsV2[button] = button.ForeColor.ToArgb();
        }

        void PreferenceChangedV2(object sender, Microsoft.Win32.UserPreferenceChangedEventArgs e)
        {
            if (appClosing || !IsHandleCreated || IsDisposed) return;
            try { BeginInvoke((Action)delegate { if (!appClosing && !IsDisposed) SetApplicationTheme(UiPalette.Dark); }); } catch (InvalidOperationException) { }
        }

        void DisposePresentationV2()
        {
            if (presentationDisposedV2) return;
            presentationDisposedV2 = true;
            foreach (Button button in navigationV2.Values) if (button.Image != null) { button.Image.Dispose(); button.Image = null; }
            navigationRegularV2.Dispose(); navigationSelectedV2.Dispose();
        }

        void ShowV2Menu(ContextMenuStrip menu, Control owner)
        {
            ApplyTheme(menu);
            menu.Closed += delegate { menu.Dispose(); };
            menu.Show(owner, new Point(0, owner.Height));
        }

        async Task LoadTargetsFileV2()
        {
            if (pingCancellation != null) { MessageBox.Show(this, "Stop Ping before replacing its targets.", AppName); return; }
            using (var dialog = new OpenFileDialog { Title = "Load Ping targets", Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*" })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    string path = dialog.FileName;
                    string text = await Task.Run(delegate { if (new FileInfo(path).Length > 256 * 1024) throw new IOException("Target files must be smaller than 256 KiB."); return File.ReadAllText(path); });
                    if (!appClosing && pingCancellation == null) targetInput.Text = text;
                }
                catch { if (!appClosing) MessageBox.Show(this, "Could not read the target file. Check its size and access permissions.", AppName); }
            }
        }

        void BuildHomePageV2()
        {
            var page = NewPage("Home");
            var card = Card(); card.AutoScroll = true;
            var rows = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, RowCount = 0, Padding = new Padding(12) };
            rows.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190)); rows.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            foreach (string id in new[] { "Live Ping", "Traceroute", "Config Collector", "DNS Resolver", "MAC / WAN Lookup", "Calculators", "Event Log", "Updates", "Settings" })
            {
                string destination = id; int row = rows.RowCount++;
                rows.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                var action = ActionButton(id == "Live Ping" ? "Open Ping Live" : "Open " + id, false, 178);
                action.Margin = new Padding(0, 8, 8, 8); action.Click += delegate { NavigateV2(destination); };
                var help = new Label { Text = PageDescriptionV2(id), AutoSize = true, Dock = DockStyle.Fill, ForeColor = TextMuted, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(8) };
                rows.Controls.Add(action, 0, row); rows.Controls.Add(help, 1, row);
            }
            card.Controls.Add(rows); card.Controls.Add(SectionHeader("Your network workspace", "All tools retain their existing workflows. Start with a destination below."));
            page.Controls.Add(card);
        }

        void BuildSettingsPageV2()
        {
            var page = NewPage("Settings"); var card = Card();
            var rows = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, RowCount = 5, Padding = new Padding(8) };
            rows.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); rows.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int i = 0; i < 5; i++) rows.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var theme = new ComboBox { Name = "settingsTheme", AccessibleName = "Settings theme", DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };
            theme.Items.AddRange(new object[] { "Light", "Dark" }); theme.SelectedIndex = UiPalette.Dark ? 1 : 0;
            theme.SelectedIndexChanged += delegate { if (!changingTheme) { SetApplicationTheme(theme.SelectedIndex == 1); SaveAppState(); } };
            themeSelector.SelectedIndexChanged += delegate { if (theme.SelectedIndex != themeSelector.SelectedIndex) theme.SelectedIndex = themeSelector.SelectedIndex; };
            settingsZoomV2 = NumberField(70, 180, 100, 10); settingsZoomV2.Dock = DockStyle.Left; settingsZoomV2.Width = 200;
            settingsZoomV2.AccessibleName = "Text and results zoom percent";
            settingsZoomV2.ValueChanged += delegate { if (!synchronizingSettingsV2) { zoomScale = (float)settingsZoomV2.Value / 100f; ApplyZoom(); SaveAppState(); } };
            var recovery = ActionButton("Updates / downgrade", false, 200); recovery.Click += delegate { NavigateV2("Updates"); };
            var save = ActionButton("Save preferences", true, 200); save.Click += delegate { SaveAppState(); appStatus.Text = stateSaveSucceeded ? "Preferences saved" : "Preferences could not be saved"; };
            rows.Controls.Add(FieldLabel("Appearance"), 0, 0); rows.Controls.Add(theme, 1, 0);
            rows.Controls.Add(FieldLabel("Text / results zoom (%)"), 0, 1); rows.Controls.Add(settingsZoomV2, 1, 1);
            rows.Controls.Add(FieldLabel("Version recovery"), 0, 2); rows.Controls.Add(recovery, 1, 2);
            rows.Controls.Add(new Label { Text = "Ctrl + mouse wheel also changes result text size. Windows display scaling follows your system settings.\r\nWindows High Contrast uses system colors. Preferences and saved lists stay on this computer.", AutoSize = true, Dock = DockStyle.Fill, ForeColor = TextMuted, Padding = new Padding(0, 20, 0, 20) }, 0, 3);
            rows.SetColumnSpan(rows.GetControlFromPosition(0, 3), 2);
            rows.Controls.Add(save, 1, 4);
            card.Controls.Add(rows); card.Controls.Add(SectionHeader("Appearance & preferences", "Choose a readable theme and text size. Changes are applied immediately."));
            page.Controls.Add(card);
        }

        void FinishPresentationV2()
        {
            BuildHomePageV2(); BuildSettingsPageV2();
            // Shared native grids retain binding/copy/edit semantics and user column widths.
            foreach (var pair in pagesByName) ConfigureAccessibilityV2(pair.Value);
            UpdateNavigationV2();
        }

        void ConfigureAccessibilityV2(Control parent)
        {
            int order = 0;
            foreach (Control control in parent.Controls.Cast<Control>().OrderBy(c => c.Top).ThenBy(c => c.Left))
            {
                if (String.IsNullOrWhiteSpace(control.AccessibleName))
                {
                    if (control is Button || control is CheckBox) control.AccessibleName = UiAccessibility.ActionName(control.Text);
                    else if (control is DataGridView) control.AccessibleName = parent.AccessibleName + " results";
                    else if (control is TextBox || control is ComboBox || control is NumericUpDown || control is RichTextBox)
                    {
                        var table = parent as TableLayoutPanel;
                        Control label = null;
                        if (table != null)
                        {
                            var cell = table.GetPositionFromControl(control);
                            if (cell.Column > 0) label = table.GetControlFromPosition(cell.Column - 1, cell.Row);
                            if (!(label is Label) && cell.Row > 0) label = table.GetControlFromPosition(cell.Column, cell.Row - 1);
                        }
                        control.AccessibleName = label is Label ? label.Text : parent.AccessibleName + " input";
                    }
                }
                // Preserve deliberately assigned pilot field order. Else use container order.
                order++;
                ConfigureAccessibilityV2(control);
            }
        }

        void ComposePingV2(TabPage page, SplitContainer split, Panel inputCard, Control inputHeader,
            Control hint, TableLayoutPanel profiles, TableLayoutPanel settings, Control actions,
            Panel resultCard, TableLayoutPanel toolbar, FlowLayoutPanel toolbarActions, Control historyBar)
        {
            SuspendLayout();
            // Existing controls and handlers move together; probes/binding are untouched.
            var targets = Card(); targets.Name = "pingTargetsCardV2";
            targets.Controls.Add(targetInput); targets.Controls.Add(hint); targets.Controls.Add(profiles); targets.Controls.Add(inputHeader);
            targetInput.Font = new Font(UiTokens.MonospaceFontFamily, 9f);
            targetInput.AccessibleName = "Ping targets"; profileCombo.AccessibleName = "Saved Ping lists";
            foreach (Button button in profiles.Controls.OfType<Button>()) button.Margin = new Padding(1);
            var probes = Card(); probes.Name = "pingProbeCardV2";
            settings.Dock = DockStyle.Fill;
            settings.ColumnStyles[0].SizeType = SizeType.AutoSize; settings.ColumnStyles[1].Width = 100;
            settings.Padding = new Padding(0, 4, 0, 0);
            for (int row = 0; row < settings.RowStyles.Count; row++) settings.RowStyles[row].Height = row == 6 ? 35 : 29;
            probes.Controls.Add(settings); probes.Controls.Add(SectionHeader("Probe settings", "Cadence, protocol and resolver"));
            var inputs = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = new Padding(0) };
            inputs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            inputs.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); inputs.RowStyles.Add(new RowStyle(SizeType.Absolute, V2Design.SectionGap));
            inputs.RowStyles.Add(new RowStyle(SizeType.Absolute, V2Design.ProbeCardHeight));
            targets.Margin = probes.Margin = new Padding(0);
            inputs.Controls.Add(targets, 0, 0); inputs.Controls.Add(probes, 0, 2);
            // Reparent first, then dispose the unused one-card input scaffold.
            var configuration = new Panel { Dock = DockStyle.Fill, BackColor = Canvas, Margin = new Padding(0) };
            configuration.Controls.Add(inputs); configuration.Controls.Add(actions);
            ProtectInputCard(configuration, actions, 584);
            split.Panel1.Controls.Clear(); split.Panel1.Controls.Add(configuration); inputCard.Dispose();

            pingResultSplit.Parent.Controls.Remove(pingResultSplit);
            resultCard.Controls.Add(pingGrid);
            pingGrid.BringToFront();
            pingResultSplit.Panel1.Controls.Add(resultCard);
            var history = Card(); history.Name = "pingHistoryCardV2";
            history.Controls.Add(historyBar);
            pingHistoryDetailSplitV2 = new SplitContainer { Dock = DockStyle.Fill, SplitterWidth = UiTokens.SplitterWidth, FixedPanel = FixedPanel.Panel2 };
            pingHistoryDetailV2 = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, BorderStyle = BorderStyle.None,
                ScrollBars = ScrollBars.Vertical, Text = "Select a history row to view its detail.", AccessibleName = "Selected Ping event detail" };
            pingHistoryDetailSplitV2.Panel1.Controls.Add(pingHistoryGrid); pingHistoryDetailSplitV2.Panel2.Controls.Add(pingHistoryDetailV2);
            history.Controls.Add(pingHistoryDetailSplitV2); pingHistoryDetailSplitV2.BringToFront();
            pingResultSplit.Panel2.Controls.Add(history);
            split.Panel2.Controls.Clear(); split.Panel2.Controls.Add(pingResultSplit);
            ConfigureResponsiveSplit(pingHistoryDetailSplitV2, 650, 350, 190);
            pingHistoryDetailSplitV2.SizeChanged += delegate { pingHistoryDetailSplitV2.Panel2Collapsed = pingHistoryDetailSplitV2.Width < LayoutPixels(900); };
            pingHistoryGrid.SelectionChanged += delegate
            {
                pingHistoryDetailV2.Text = pingHistoryGrid.CurrentRow == null
                    ? "Select a history row to view its detail."
                    : Convert.ToString(pingHistoryGrid.CurrentRow.Cells["Detail"].Value);
            };
            var bar = historyBar as TableLayoutPanel;
            if (bar != null)
            {
                bar.ColumnCount = 3; bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 104));
                var details = ActionButton("Details", false, V2Design.PingActionWidth);
                details.Anchor = AnchorStyles.None; details.Click += delegate { ShowPingEventDetailV2(); };
                bar.Controls.Add(details, 2, 0);
            }
            pingHistoryTitle.AutoEllipsis = true; pingHistoryTitle.AccessibleName = "Selected target Ping history";
            ConfigurePingToolbarV2(toolbarActions);
            foreach (Button button in DescendantsV2(page).OfType<Button>()) button.Width = V2Design.PingActionWidth;
            // The summary is opt-in for a clean, data-first default; persisted preference wins.
            pingRoot.RowStyles[0].Height = 0;
            foreach (Button button in toolbarActions.Controls.OfType<Button>()) if (button.Text == "Hide cards") button.Text = "Show cards";
            foreach (DataGridViewColumn column in pingGrid.Columns)
                if (column.ValueType == typeof(int) || column.Name.EndsWith("Ms") || new[] { "Seq", "Sent", "Received", "Lost", "LossPct", "Port" }.Contains(column.Name)) column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            ResumeLayout(true);
        }

        static IEnumerable<Control> DescendantsV2(Control root)
        {
            yield return root;
            foreach (Control child in root.Controls) foreach (Control nested in DescendantsV2(child)) yield return nested;
        }

        float CreateGraphicsDpiV2() { using (var graphics = CreateGraphics()) return graphics.DpiX; }

        void ConfigurePingToolbarV2(FlowLayoutPanel actions)
        {
            var secondary = actions.Controls.OfType<Button>().Where(b => b.Text == "Clear" || b.Text == "Hide cards" || b.Text == "Columns").ToArray();
            pingCardsButtonV2 = secondary.Single(b => b.Text == "Hide cards");
            var more = ActionButton("More", false, V2Design.PingActionWidth);
            more.Name = "pingResultsMore";
            more.Click += delegate
            {
                var menu = new ContextMenuStrip();
                foreach (Button button in secondary)
                {
                    Button original = button;
                    // PerformClick requires a visible button. Invoke the existing action
                    // by temporarily exposing it, then restore the responsive layout.
                    menu.Items.Add(button.Text, null, delegate { bool visible = original.Visible; original.Visible = true; original.PerformClick(); original.Visible = visible; });
                }
                ShowV2Menu(menu, more);
            };
            actions.Controls.Add(more); actions.Controls.SetChildIndex(more, 0);
            actions.SizeChanged += delegate
            {
                bool compact = actions.ClientSize.Width < LayoutPixels(560);
                more.Visible = compact;
                foreach (Button button in secondary) button.Visible = !compact;
            };
        }

        void ShowPingEventDetailV2()
        {
            using (var dialog = new Form { Text = "Ping event detail", Size = new Size(560, 320), StartPosition = FormStartPosition.CenterParent,
                Font = Font, MinimizeBox = false, MaximizeBox = false, ShowInTaskbar = false, AutoScaleDimensions = new SizeF(96, 96), AutoScaleMode = AutoScaleMode.Dpi })
            {
                dialog.Controls.Add(new TextBox { Dock = DockStyle.Fill, ReadOnly = true, Multiline = true, ScrollBars = ScrollBars.Vertical,
                    Text = pingHistoryDetailV2.Text, AccessibleName = "Ping event detail" });
                ApplyTheme(dialog); dialog.ShowDialog(this);
            }
        }
    }
}
