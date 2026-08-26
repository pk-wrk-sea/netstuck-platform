using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

[assembly: AssemblyTitle("NetStuck")]
[assembly: AssemblyProduct("NetStuck")]
[assembly: AssemblyDescription("Network reachability and diagnostics")]
[assembly: AssemblyCompany("NetStuck Project")]
[assembly: AssemblyVersion("1.2.3.0")]
[assembly: AssemblyFileVersion("1.2.3.0")]

namespace NetStuck
{
    public sealed class SavedProfile
    {
        public string Name { get; set; }
        public string Targets { get; set; }
        public int IntervalMs { get; set; }
        public int TimeoutMs { get; set; }
        public bool UseCustomDns { get; set; }
        public string CustomDns { get; set; }
    }

    sealed class ProfileCollection
    {
        public List<SavedProfile> Profiles { get; set; }
    }

    sealed class HopStats
    {
        public int Sent;
        public int Received;
        public double Last;
        public double Best = Double.MaxValue;
        public double Worst;
        public double Total;
        public double PreviousLatency = -1;
        public double JitterTotal;
        public int JitterSamples;
        public int RouteChanges;
        public string Address = "";
        public string Hostname = "";
    }

    sealed class HopProbe
    {
        public int Hop;
        public string Address = "";
        public double Latency = -1;
        public IPStatus IpStatus = IPStatus.Unknown;
        public string Status = "Timeout";
        public bool Responded;
        public bool Reached;
    }

    sealed class MacLookupResult
    {
        public string Vendor = "";
        public string Status = "ERROR";
        public bool Cacheable;
    }

    public sealed partial class MainForm : Form
    {
        const string AppName = "NetStuck";
        const string AppVersion = "v.1.2.3";
        const int MaxExpandedTargets = 1024;

        readonly Color Canvas = Color.FromArgb(245, 247, 250);
        readonly Color Surface = Color.White;
        readonly Color Border = Color.FromArgb(218, 224, 232);
        readonly Color TextMain = Color.FromArgb(30, 41, 59);
        readonly Color TextMuted = Color.FromArgb(100, 116, 139);
        readonly Color Accent = Color.FromArgb(37, 99, 235);
        readonly Color Success = Color.FromArgb(22, 163, 74);
        readonly Color Danger = Color.FromArgb(220, 38, 38);
        readonly Color Warning = Color.FromArgb(217, 119, 6);

        readonly TabControl tabs = new TabControl();
        readonly Dictionary<string, TabPage> pagesByName = new Dictionary<string, TabPage>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, int> tabActivityCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        readonly ToolStripStatusLabel appStatus = new ToolStripStatusLabel("Ready");
        readonly ToolStripStatusLabel clockStatus = new ToolStripStatusLabel();
        readonly System.Windows.Forms.Timer clockTimer = new System.Windows.Forms.Timer();

        TextBox targetInput;
        ComboBox profileCombo;
        Label profileInfo;
        readonly List<SavedProfile> savedProfiles = new List<SavedProfile>();
        string profilePath;
        NumericUpDown pingInterval;
        NumericUpDown pingTimeout;
        CheckBox pingUseCustomDns;
        TextBox pingDnsServer;
        Button pingStartButton;
        Button pingStopButton;
        TextBox pingSearch;
        ComboBox pingStatusFilter;
        DataGridView pingGrid;
        DataTable pingTable;
        BindingSource pingSource;
        CancellationTokenSource pingCancellation;
        Label metricTargets;
        Label metricUp;
        Label metricDown;
        Label metricPings;
        Label metricLoss;
        DataGridView pingHistoryGrid;
        DataTable pingHistoryTable;
        DataTable pingHistoryDisplayTable;
        BindingSource pingHistorySource;
        Label pingHistoryTitle;
        SplitContainer pingResultSplit;
        TableLayoutPanel pingRoot;
        readonly Dictionary<string, string> lastTargetStatus = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        ComboBox traceTarget;
        NumericUpDown traceMaxHops;
        NumericUpDown traceTimeout;
        NumericUpDown traceInterval;
        CheckBox traceContinuous;
        CheckBox traceResolveNames;
        CheckBox traceUseCustomDns;
        TextBox traceDnsServer;
        Button traceStartButton;
        Button traceStopButton;
        DataGridView traceGrid;
        DataTable traceTable;
        CancellationTokenSource traceCancellation;
        Label traceCycleLabel;
        Label traceDestinationLabel;
        Label traceStateLabel;
        string traceHopInfoText = "";

        TextBox dnsInput;
        CheckBox dnsUseCustom;
        TextBox dnsServer;
        DataGridView dnsGrid;
        DataTable dnsTable;
        NumericUpDown dnsPollInterval;
        Button dnsResolveButton;
        Button dnsPollButton;
        Button dnsStopButton;
        CancellationTokenSource dnsCancellation;

        TextBox macInput;
        DataGridView macGrid;
        DataTable macTable;
        Button macLookupButton;
        readonly Dictionary<string, string> macVendorCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string macCachePath;
        DateTime lastMacApiRequestUtc = DateTime.MinValue;
        TextBox wanInput;
        DataGridView wanGrid;
        DataTable wanTable;
        Button wanLookupButton;

        TextBox subnetInput;
        TextBox subnetOutput;
        TextBox unitValue;
        ComboBox unitFrom;
        ComboBox unitTo;
        Label unitOutput;

        DataGridView logGrid;
        DataTable logTable;
        BindingSource logSource;
        TextBox logSearch;
        ComboBox logLevel;

        public MainForm()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            DoubleBuffered = true;
            SuspendLayout();
            Text = AppName;
            Width = 1460;
            Height = 900;
            MinimumSize = new Size(1100, 700);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Canvas;
            ForeColor = TextMain;
            Font = new Font("Segoe UI", 9.25f);
            gridBoldFont = new Font("Segoe UI Semibold", 9.25f, FontStyle.Bold);
            // A 40 ms UI batch keeps high-volume /24 sessions smooth while probe
            // workers continue to run on their own monotonic schedule.
            pingUiTimer = new System.Windows.Forms.Timer { Interval = 40 };
            pingUiTimer.Tick += delegate
            {
                DrainPingUpdates();
                if (stopPingUiTimerAfterDrain && pingUiUpdates.IsEmpty) pingUiTimer.Stop();
            };
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { Icon = SystemIcons.Error; }
            AutoScaleMode = AutoScaleMode.Dpi;
            profilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppName, "profiles.json");
            string stateOverride = Environment.GetEnvironmentVariable("NETSTUCK_TEST_STATE_PATH");
            statePath = String.IsNullOrWhiteSpace(stateOverride)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppName, "state.json")
                : stateOverride;
            macCachePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppName, "mac-vendors.json");
            LoadMacVendorCache();
            InitializeTraceLookupCacheV120();

            BuildShell();
            BuildPingPage();
            BuildTracePage();
            BuildDnsPage();
            BuildLookupPage();
            BuildCalculatorPage();
            BuildCollectorPage();
            BuildLogPage();
            BuildUpdatesPage();
            LoadProfiles();
            ApplyTheme(this);
            LoadAppState();
            EnableCtrlWheelZoom(this);
            ResumeLayout(true);
            FormClosing += OnFormClosing;
            Shown += async delegate { await Task.WhenAll(SynchronizeClockAsync(), RefreshNetworkIdentityAsync()); };
            Log("INFO", "Application", "Application started — version " + AppVersion);
        }

        void BuildShell()
        {
            var header = new Panel { Dock = DockStyle.Top, Height = 72, BackColor = Surface, Padding = new Padding(20, 10, 20, 8) };
            header.Paint += delegate(object sender, PaintEventArgs e) { using (var pen = new Pen(Border)) e.Graphics.DrawLine(pen, 0, header.Height - 1, header.Width, header.Height - 1); };
            var logo = new PictureBox { Location = new Point(18, 13), Size = new Size(40, 40), SizeMode = PictureBoxSizeMode.Zoom, Image = Icon.ToBitmap() };
            var title = new Label { Text = AppName, AutoSize = true, Font = new Font("Segoe UI Semibold", 17f), ForeColor = TextMain, Location = new Point(66, 9) };
            var sub = new Label { Text = "Network reachability and diagnostics", AutoSize = true, ForeColor = TextMuted, Location = new Point(68, 42) };
            var version = new Label { Text = AppVersion, AutoSize = true, BackColor = Color.FromArgb(239, 246, 255), ForeColor = Accent, Padding = new Padding(9, 4, 9, 4) };
            version.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            header.Resize += delegate
            {
                version.Location = new Point(header.Width - version.Width - 22, 22);
            };
            header.Controls.AddRange(new Control[] { logo, title, sub, version });

            tabs.Dock = DockStyle.Fill;
            tabs.Font = new Font("Segoe UI Semibold", 9.5f);
            tabs.Padding = new Point(18, 7);
            tabs.Appearance = TabAppearance.Normal;
            tabs.ShowToolTips = true;
            tabs.DrawMode = TabDrawMode.OwnerDrawFixed;
            tabs.DrawItem += DrawMainTab;

            var status = new StatusStrip { BackColor = Surface, ForeColor = TextMuted, SizingGrip = false };
            var dot = new ToolStripStatusLabel("\u25CF") { ForeColor = Success };
            var spring = new ToolStripStatusLabel { Spring = true };
            timeSourceStatus = new ToolStripStatusLabel("Time: checking NTP…");
            var localDivider = new ToolStripSeparator { Margin = new Padding(8, 3, 8, 3) };
            var publicDivider = new ToolStripSeparator { Margin = new Padding(8, 3, 8, 3) };
            status.Items.AddRange(new ToolStripItem[] { dot, appStatus, spring, timeSourceStatus, clockStatus, localDivider, localIpStatus, publicDivider, publicIpStatus });
            clockTimer.Interval = 1000;
            clockTimer.Tick += delegate { UpdateClockDisplay(); };
            clockTimer.Start();

            Controls.Add(tabs);
            Controls.Add(header);
            Controls.Add(status);
        }

        void BuildPingPageLegacyV102()
        {
            var page = NewPage("Live Ping");
            pingRoot = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, BackColor = Canvas };
            pingRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
            pingRoot.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var metrics = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 5, RowCount = 1, Padding = new Padding(0, 0, 0, 6) };
            metrics.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            for (int i = 0; i < 5; i++) metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
            metricTargets = MetricCard(metrics, 0, "TARGETS", "0", TextMain);
            metricUp = MetricCard(metrics, 1, "REACHABLE", "0", Success);
            metricDown = MetricCard(metrics, 2, "UNREACHABLE", "0", Danger);
            metricPings = MetricCard(metrics, 3, "TOTAL PINGS", "0", Accent);
            metricLoss = MetricCard(metrics, 4, "PACKET LOSS", "0.0%", Warning);

            var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, SplitterDistance = 375, SplitterWidth = 8, FixedPanel = FixedPanel.Panel1, BackColor = Canvas };
            split.Panel1.Padding = new Padding(0, 0, 4, 0);
            split.Panel2.Padding = new Padding(4, 0, 0, 0);

            var inputCard = Card();
            var inputHeader = SectionHeader("Targets + saved lists", "Host, IP or CIDR — description is optional");
            inputHeader.Dock = DockStyle.Top;
            targetInput = new TextBox { Multiline = true, AcceptsReturn = true, AcceptsTab = false, WordWrap = false, ScrollBars = ScrollBars.Both, Dock = DockStyle.Fill, Font = new Font("Consolas", 10f), BorderStyle = BorderStyle.FixedSingle };
            targetInput.Text = "8.8.8.8 Google DNS\r\n1.1.1.1 Cloudflare DNS\r\n10.100.10.0/24 Branch network";
            var targetHint = new Label { Text = "Examples: 10.10.10.1 SW-01  •  10.100.10.0/24", Dock = DockStyle.Bottom, Height = 30, ForeColor = TextMuted, Padding = new Padding(0, 7, 0, 0) };

            var settings = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 176, ColumnCount = 2, RowCount = 5, Padding = new Padding(0, 10, 0, 0) };
            settings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48));
            settings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52));
            settings.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            settings.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            settings.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            settings.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            settings.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
            pingInterval = NumberField(250, 60000, 1000, 250);
            pingTimeout = NumberField(100, 30000, 1500, 100);
            settings.Controls.Add(FieldLabel("Interval (ms)"), 0, 0); settings.Controls.Add(pingInterval, 1, 0);
            settings.Controls.Add(FieldLabel("Timeout (ms)"), 0, 1); settings.Controls.Add(pingTimeout, 1, 1);
            pingUseCustomDns = new CheckBox { Text = "Use custom DNS", AutoSize = true, Anchor = AnchorStyles.Left };
            pingDnsServer = new TextBox { Text = "8.8.8.8", Dock = DockStyle.Fill, Enabled = false };
            pingUseCustomDns.CheckedChanged += delegate { pingDnsServer.Enabled = pingUseCustomDns.Checked; };
            settings.Controls.Add(pingUseCustomDns, 0, 2); settings.SetColumnSpan(pingUseCustomDns, 2);
            settings.Controls.Add(pingDnsServer, 0, 3); settings.SetColumnSpan(pingDnsServer, 2);
            var actionBar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
            actionBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65)); actionBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
            actionBar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            pingStartButton = ActionButton("▶  START MONITORING", true, 0);
            pingStopButton = DangerButton("■  STOP", 0); pingStopButton.Enabled = false;
            pingStartButton.Dock = pingStopButton.Dock = DockStyle.Fill;
            pingStartButton.Click += StartPing;
            pingStopButton.Click += RequestPingStop;
            actionBar.Controls.Add(pingStartButton, 0, 0); actionBar.Controls.Add(pingStopButton, 1, 0);
            settings.Controls.Add(actionBar, 0, 4); settings.SetColumnSpan(actionBar, 2);

            var profilePanel = new TableLayoutPanel { Dock = DockStyle.Top, Height = 106, ColumnCount = 3, RowCount = 3, Padding = new Padding(0, 4, 0, 8) };
            profilePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44));
            profilePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28));
            profilePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28));
            profilePanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 31));
            profilePanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            profilePanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 27));
            profileCombo = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            var loadProfile = ActionButton("Load", false, 0); loadProfile.Dock = DockStyle.Fill; loadProfile.Click += LoadSelectedProfile;
            var saveProfile = ActionButton("Save list", true, 0); saveProfile.Dock = DockStyle.Fill; saveProfile.Click += SaveCurrentProfile;
            var deleteProfile = DangerButton("Delete", 0); deleteProfile.Dock = DockStyle.Fill; deleteProfile.Click += DeleteSelectedProfile;
            profileInfo = new Label { Text = "Profiles are saved locally on this PC", Dock = DockStyle.Fill, ForeColor = TextMuted, TextAlign = ContentAlignment.MiddleLeft };
            profilePanel.Controls.Add(profileCombo, 0, 0); profilePanel.SetColumnSpan(profileCombo, 3);
            profilePanel.Controls.Add(loadProfile, 0, 1); profilePanel.Controls.Add(saveProfile, 1, 1); profilePanel.Controls.Add(deleteProfile, 2, 1);
            profilePanel.Controls.Add(profileInfo, 0, 2); profilePanel.SetColumnSpan(profileInfo, 3);

            inputCard.Controls.Add(targetInput);
            inputCard.Controls.Add(targetHint);
            inputCard.Controls.Add(settings);
            inputCard.Controls.Add(profilePanel);
            inputCard.Controls.Add(inputHeader);
            split.Panel1.Controls.Add(inputCard);

            var resultCard = Card();
            var toolbar = new TableLayoutPanel { Dock = DockStyle.Top, Height = 55, ColumnCount = 7, Padding = new Padding(0, 8, 0, 8) };
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 85));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 95));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
            pingSearch = new TextBox { Dock = DockStyle.Fill };
            Cue(pingSearch, "Filter host, description, IP, status or error");
            pingSearch.TextChanged += delegate { ApplyPingFilter(); };
            pingStatusFilter = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
            pingStatusFilter.Items.AddRange(new object[] { "All status", "Reachable", "Unreachable", "Waiting" }); pingStatusFilter.SelectedIndex = 0;
            pingStatusFilter.SelectedIndexChanged += delegate { ApplyPingFilter(); };
            var copy = ActionButton("Copy", false, 0); var export = ActionButton("Export CSV", false, 0); var columns = ActionButton("Columns", false, 0);
            var cards = ActionButton("Hide cards", false, 0); var clear = ActionButton("Clear", false, 0);
            copy.Dock = export.Dock = columns.Dock = cards.Dock = clear.Dock = DockStyle.Fill;
            copy.Click += CopyPingRows; export.Click += ExportPingCsv; clear.Click += delegate { if (pingCancellation == null) { ClearPing(); ResetPingHistoryForNewSession(); } };
            columns.Click += ShowPingColumnChooser;
            cards.Click += delegate { TogglePingCards(cards); };
            toolbar.Controls.Add(pingSearch, 0, 0); toolbar.Controls.Add(pingStatusFilter, 1, 0); toolbar.Controls.Add(copy, 2, 0); toolbar.Controls.Add(export, 3, 0);
            toolbar.Controls.Add(columns, 4, 0); toolbar.Controls.Add(cards, 5, 0); toolbar.Controls.Add(clear, 6, 0);

            pingTable = CreatePingTable();
            pingSource = new BindingSource { DataSource = pingTable };
            pingGrid = DataGrid();
            pingGrid.AutoGenerateColumns = false;
            AddGridColumn(pingGrid, "Status", "Status", 108);
            AddGridColumn(pingGrid, "Host", "Host", 170);
            AddGridColumn(pingGrid, "Description", "Description", 190);
            AddGridColumn(pingGrid, "Resolved IP", "ResolvedIp", 135);
            AddGridColumn(pingGrid, "Last", "LastMs", 78);
            AddGridColumn(pingGrid, "Average", "AvgMs", 88);
            AddGridColumn(pingGrid, "Min", "MinMs", 72);
            AddGridColumn(pingGrid, "Max", "MaxMs", 72);
            AddGridColumn(pingGrid, "Sent", "Sent", 72);
            AddGridColumn(pingGrid, "Received", "Received", 82);
            AddGridColumn(pingGrid, "Lost", "Lost", 68);
            AddGridColumn(pingGrid, "Loss", "LossPct", 78);
            AddGridColumn(pingGrid, "Last ping", "LastPing", 148);
            AddGridColumn(pingGrid, "Last success", "LastSuccess", 148);
            AddGridColumn(pingGrid, "Reachable since", "ReachableSince", 148);
            AddGridColumn(pingGrid, "Unreachable since", "UnreachableSince", 148);
            AddGridColumn(pingGrid, "Result / error", "Error", 250);
            pingGrid.DataSource = pingSource;
            pingGrid.CellFormatting += FormatPingCell;
            pingGrid.KeyDown += GridCopyShortcut;
            pingGrid.CellClick += SelectPingHistory;

            pingHistoryTable = CreatePingHistoryTable();
            pingHistoryDisplayTable = CreatePingHistoryTable();
            pingHistorySource = new BindingSource { DataSource = pingHistoryDisplayTable };
            pingHistoryGrid = DataGrid(); pingHistoryGrid.AutoGenerateColumns = false;
            AddGridColumn(pingHistoryGrid, "Timestamp", "Time", 178);
            AddGridColumn(pingHistoryGrid, "Host", "Host", 145);
            AddGridColumn(pingHistoryGrid, "Resolved IP", "ResolvedIp", 130);
            AddGridColumn(pingHistoryGrid, "Latency", "LatencyMs", 90);
            AddGridColumn(pingHistoryGrid, "TTL", "Ttl", 65);
            AddGridColumn(pingHistoryGrid, "Result", "Result", 105);
            AddGridColumn(pingHistoryGrid, "Sequence", "Sequence", 78);
            AddGridColumn(pingHistoryGrid, "Detail", "Detail", 260);
            pingHistoryGrid.DataSource = pingHistorySource;
            pingHistoryGrid.CellFormatting += FormatPingHistoryCell;
            pingHistoryGrid.KeyDown += GridCopyShortcut;
            var historyBar = new TableLayoutPanel { Dock = DockStyle.Top, Height = 42, ColumnCount = 2, Padding = new Padding(2, 4, 2, 4) };
            historyBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            historyBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            pingHistoryTitle = new Label { Text = "PING HISTORY  •  click a target row above to view", Dock = DockStyle.Fill, ForeColor = TextMuted, Font = new Font("Segoe UI Semibold", 8.5f), TextAlign = ContentAlignment.MiddleLeft };
            historyBar.Controls.Add(pingHistoryTitle, 0, 0);
            var exportHistory = ActionButton("Export log", false, 0); exportHistory.Dock = DockStyle.Fill; exportHistory.Click += ExportPingHistory;
            historyBar.Controls.Add(exportHistory, 1, 0);
            pingResultSplit = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterWidth = 8, BackColor = Canvas };
            pingResultSplit.Panel1.Controls.Add(pingGrid);
            pingResultSplit.Panel2.Controls.Add(pingHistoryGrid);
            pingResultSplit.Panel2.Controls.Add(historyBar);
            resultCard.Controls.Add(pingResultSplit);
            resultCard.Controls.Add(toolbar);
            resultCard.Controls.Add(SectionHeader("Realtime results", "Sort, filter, reorder and resize columns • drag the horizontal divider to resize ping history"));
            split.Panel2.Controls.Add(resultCard);
            pingRoot.Controls.Add(metrics, 0, 0);
            pingRoot.Controls.Add(split, 0, 1);
            page.Controls.Add(pingRoot);
            ConfigureSplit(page, split, 375, 310, 600);
            ConfigureHorizontalSplit(resultCard, pingResultSplit, 330, 220, 190);
        }

        void BuildTracePageLegacyV102()
        {
            var page = NewPage("Traceroute");
            var card = Card();
            var controls = new TableLayoutPanel { Dock = DockStyle.Top, Height = 118, ColumnCount = 5, RowCount = 2, Padding = new Padding(0, 5, 0, 7) };
            controls.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            controls.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
            controls.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            controls.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            controls.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 250));
            controls.RowStyles.Add(new RowStyle(SizeType.Absolute, 61));
            controls.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
            traceTarget = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDown, Text = "8.8.8.8" };
            traceMaxHops = NumberField(1, 64, 24, 1);
            traceTimeout = NumberField(200, 10000, 1000, 100);
            traceInterval = NumberField(250, 60000, 1000, 250);
            traceContinuous = new CheckBox { Text = "Continuous", Checked = true, AutoSize = true, Anchor = AnchorStyles.Left };
            traceResolveNames = new CheckBox { Text = "Resolve names", Checked = true, AutoSize = true, Anchor = AnchorStyles.Left };
            traceUseCustomDns = new CheckBox { Text = "Custom DNS", AutoSize = true, Anchor = AnchorStyles.Left };
            traceDnsServer = new TextBox { Text = "8.8.8.8", Dock = DockStyle.Fill, Enabled = false };
            traceUseCustomDns.CheckedChanged += delegate { traceDnsServer.Enabled = traceUseCustomDns.Checked; };
            traceStartButton = ActionButton("▶  START TRACE", true, 0); traceStartButton.Dock = DockStyle.Fill;
            traceStopButton = DangerButton("■  STOP", 0); traceStopButton.Dock = DockStyle.Fill; traceStopButton.Enabled = false;
            traceStartButton.Click += StartTrace; traceStopButton.Click += RequestTraceStop;
            controls.Controls.Add(LabeledField("Target", traceTarget), 0, 0);
            controls.Controls.Add(LabeledField("Max hops", traceMaxHops), 1, 0);
            controls.Controls.Add(LabeledField("Timeout (ms)", traceTimeout), 2, 0);
            controls.Controls.Add(LabeledField("Interval (ms)", traceInterval), 3, 0);
            var traceButtons = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Padding = new Padding(4, 19, 0, 2) };
            traceButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 64)); traceButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36));
            traceButtons.Controls.Add(traceStartButton, 0, 0); traceButtons.Controls.Add(traceStopButton, 1, 0);
            controls.Controls.Add(traceButtons, 4, 0);
            var traceOptions = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(2, 7, 0, 0) };
            traceContinuous.Margin = new Padding(0, 4, 22, 0);
            traceResolveNames.Margin = new Padding(0, 4, 22, 0);
            traceUseCustomDns.Margin = new Padding(0, 4, 8, 0);
            traceDnsServer.Dock = DockStyle.None; traceDnsServer.Width = 150; traceDnsServer.Margin = new Padding(0, 1, 0, 0);
            traceOptions.Controls.AddRange(new Control[] { traceContinuous, traceResolveNames, traceUseCustomDns, traceDnsServer });
            controls.Controls.Add(traceOptions, 0, 1); controls.SetColumnSpan(traceOptions, 5);

            var info = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 36, BackColor = Color.FromArgb(248, 250, 252), Padding = new Padding(8, 7, 8, 0) };
            traceCycleLabel = new Label { Text = "Cycle: 0", AutoSize = true, ForeColor = TextMuted };
            traceDestinationLabel = new Label { Text = "Destination: —", AutoSize = true, ForeColor = TextMuted, Margin = new Padding(25, 0, 0, 0) };
            traceStateLabel = new Label { Text = "Target: Waiting", AutoSize = true, ForeColor = Warning, Font = new Font("Segoe UI Semibold", 9), Margin = new Padding(25, 0, 0, 0) };
            var hopInfo = ActionButton("Hop descriptions…", false, 135); hopInfo.Height = 25; hopInfo.Margin = new Padding(25, 0, 0, 0); hopInfo.Click += ShowHopDescriptions;
            info.Controls.AddRange(new Control[] { traceCycleLabel, traceDestinationLabel, traceStateLabel, hopInfo });
            traceTable = CreateTraceTable();
            traceGrid = DataGrid(); traceGrid.AutoGenerateColumns = false;
            AddGridColumn(traceGrid, "Hop", "Hop", 55); AddGridColumn(traceGrid, "Address", "Address", 145); AddGridColumn(traceGrid, "Hostname", "Hostname", 210); AddGridColumn(traceGrid, "Description", "Description", 210);
            AddGridColumn(traceGrid, "Status", "Status", 90); AddGridColumn(traceGrid, "Last", "LastMs", 75); AddGridColumn(traceGrid, "Best", "BestMs", 75);
            AddGridColumn(traceGrid, "Average", "AvgMs", 85); AddGridColumn(traceGrid, "Worst", "WorstMs", 80); AddGridColumn(traceGrid, "Jitter", "JitterMs", 78);
            AddGridColumn(traceGrid, "Sent", "Sent", 65); AddGridColumn(traceGrid, "Received", "Received", 78); AddGridColumn(traceGrid, "Loss", "LossPct", 75);
            AddGridColumn(traceGrid, "Route changes", "RouteChanges", 105); AddGridColumn(traceGrid, "Updated", "Updated", 100);
            traceGrid.DataSource = traceTable; traceGrid.CellFormatting += FormatTraceCell; traceGrid.KeyDown += GridCopyShortcut;
            card.Controls.Add(traceGrid); card.Controls.Add(info); card.Controls.Add(controls); card.Controls.Add(SectionHeader("Realtime Traceroute", "Per-hop latency, loss, jitter and route-change tracking"));
            page.Controls.Add(card);
        }

        void BuildDnsPage()
        {
            var page = NewPage("DNS Resolver");
            var split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 350, SplitterWidth = 8, FixedPanel = FixedPanel.Panel1, BackColor = Canvas };
            split.Panel1.Padding = new Padding(0, 0, 4, 0); split.Panel2.Padding = new Padding(4, 0, 0, 0);
            var left = Card();
            dnsInput = new TextBox { Multiline = true, Dock = DockStyle.Fill, WordWrap = false, ScrollBars = ScrollBars.Both, Font = new Font("Consolas", 10), BorderStyle = BorderStyle.FixedSingle, Text = "example.com Public website\r\n8.8.8.8 Google PTR" };
            var options = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 154, RowCount = 4, ColumnCount = 2, Padding = new Padding(0, 8, 0, 0) };
            options.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55)); options.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
            options.RowStyles.Add(new RowStyle(SizeType.Absolute, 27));
            options.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            options.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            options.RowStyles.Add(new RowStyle(SizeType.Absolute, 47));
            dnsUseCustom = new CheckBox { Text = "Use custom DNS for forward + reverse", AutoSize = true, Anchor = AnchorStyles.Left };
            dnsPollInterval = NumberField(250, 60000, 2000, 250);
            dnsServer = new TextBox { Text = "8.8.8.8", Dock = DockStyle.Fill, Enabled = false };
            dnsUseCustom.CheckedChanged += delegate { dnsServer.Enabled = dnsUseCustom.Checked; };
            dnsResolveButton = ActionButton("Resolve once", true, 0); dnsResolveButton.Dock = DockStyle.Fill; dnsResolveButton.Click += ResolveDnsOnce;
            dnsPollButton = ActionButton("Start polling", false, 0); dnsPollButton.Dock = DockStyle.Fill; dnsPollButton.Click += StartDnsPolling;
            dnsStopButton = DangerButton("STOP", 0); dnsStopButton.Dock = DockStyle.Fill; dnsStopButton.Enabled = false; dnsStopButton.Click += StopDnsPolling;
            options.Controls.Add(dnsUseCustom, 0, 0); options.SetColumnSpan(dnsUseCustom, 2);
            options.Controls.Add(dnsServer, 0, 1); options.SetColumnSpan(dnsServer, 2);
            options.Controls.Add(FieldLabel("Poll interval (ms)"), 0, 2); options.Controls.Add(dnsPollInterval, 1, 2);
            var dnsActions = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Padding = new Padding(0, 5, 0, 5) };
            dnsActions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38)); dnsActions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34)); dnsActions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28));
            dnsActions.Controls.Add(dnsResolveButton, 0, 0); dnsActions.Controls.Add(dnsPollButton, 1, 0); dnsActions.Controls.Add(dnsStopButton, 2, 0);
            options.Controls.Add(dnsActions, 0, 3); options.SetColumnSpan(dnsActions, 2);
            left.Controls.Add(dnsInput); left.Controls.Add(options); left.Controls.Add(SectionHeader("Queries", "Hostname or IP, optional description"));
            dnsTable = CreateDnsMonitorTable();
            dnsGrid = DataGrid(); dnsGrid.AutoGenerateColumns = false;
            AddGridColumn(dnsGrid, "Query", "Query", 165); AddGridColumn(dnsGrid, "Description", "Description", 150); AddGridColumn(dnsGrid, "Type", "Type", 75);
            AddGridColumn(dnsGrid, "Status", "Status", 90); AddGridColumn(dnsGrid, "Answer", "Answer", 300); AddGridColumn(dnsGrid, "Last", "LatencyMs", 72);
            AddGridColumn(dnsGrid, "Min", "MinMs", 68); AddGridColumn(dnsGrid, "Average", "AvgMs", 78); AddGridColumn(dnsGrid, "Max", "MaxMs", 68);
            AddGridColumn(dnsGrid, "Polls", "PollCount", 60); AddGridColumn(dnsGrid, "Server", "Server", 125); AddGridColumn(dnsGrid, "Updated", "Updated", 90);
            dnsGrid.DataSource = dnsTable; dnsGrid.CellFormatting += FormatDnsCell; dnsGrid.KeyDown += GridCopyShortcut;
            var right = Card(); right.Controls.Add(dnsGrid); right.Controls.Add(SectionHeader("Results", "Forward A/AAAA + reverse PTR with query-latency polling"));
            split.Panel1.Controls.Add(left); split.Panel2.Controls.Add(right); page.Controls.Add(split);
            ConfigureSplit(page, split, 350, 280, 560);
        }

        void BuildLookupPage()
        {
            var page = NewPage("MAC / WAN Lookup");
            var split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 680, SplitterWidth = 8, FixedPanel = FixedPanel.None, BackColor = Canvas };
            split.Panel1.Padding = new Padding(0, 0, 4, 0); split.Panel2.Padding = new Padding(4, 0, 0, 0);
            var macCard = Card();
            macInput = new TextBox { Multiline = true, Dock = DockStyle.Top, Height = 125, WordWrap = false, ScrollBars = ScrollBars.Both, Font = new Font("Consolas", 9.5f), BorderStyle = BorderStyle.FixedSingle, Text = "10   00:11:22:33:44:55   DYNAMIC   Gi1/0/1" };
            macLookupButton = ActionButton("Lookup vendors", true, 180); macLookupButton.Click += LookupMac;
            macTable = new DataTable(); macTable.Columns.Add("Mac"); macTable.Columns.Add("Vendor"); macTable.Columns.Add("Status");
            macGrid = DataGrid(); macGrid.AutoGenerateColumns = false; AddGridColumn(macGrid, "MAC address", "Mac", 190); AddGridColumn(macGrid, "Vendor / result", "Vendor", 360); AddGridColumn(macGrid, "Status", "Status", 100);
            macGrid.DataSource = macTable; macGrid.CellFormatting += FormatStatusCell; macGrid.KeyDown += GridCopyShortcut;
            macCard.Controls.Add(macGrid); macCard.Controls.Add(CompactActionBar(macLookupButton)); macCard.Controls.Add(macInput); macCard.Controls.Add(SectionHeader("MAC Vendor", "Paste dirty CLI output — common MAC formats are extracted automatically"));
            var wanCard = Card();
            wanInput = new TextBox { Multiline = true, Dock = DockStyle.Top, Height = 125, WordWrap = false, ScrollBars = ScrollBars.Both, Font = new Font("Consolas", 9.5f), BorderStyle = BorderStyle.FixedSingle, Text = "8.8.8.8\r\n1.1.1.1" };
            wanLookupButton = ActionButton("Lookup WAN", true, 150); wanLookupButton.Click += LookupWan;
            wanTable = new DataTable();
            foreach (string name in new[] { "IP", "Status", "Country", "Region", "ISP", "Organization", "ASN", "Timezone", "Result" }) wanTable.Columns.Add(name);
            wanGrid = DataGrid(); wanGrid.AutoGenerateColumns = false;
            AddGridColumn(wanGrid, "IP", "IP", 125); AddGridColumn(wanGrid, "Status", "Status", 80); AddGridColumn(wanGrid, "Country", "Country", 130); AddGridColumn(wanGrid, "Region", "Region", 140);
            AddGridColumn(wanGrid, "ISP", "ISP", 210); AddGridColumn(wanGrid, "Organization", "Organization", 210); AddGridColumn(wanGrid, "ASN", "ASN", 110); AddGridColumn(wanGrid, "Timezone", "Timezone", 150); AddGridColumn(wanGrid, "Result", "Result", 190);
            wanGrid.DataSource = wanTable; wanGrid.CellFormatting += FormatStatusCell; wanGrid.KeyDown += GridCopyShortcut;
            wanCard.Controls.Add(wanGrid); wanCard.Controls.Add(CompactActionBar(wanLookupButton)); wanCard.Controls.Add(wanInput); wanCard.Controls.Add(SectionHeader("WAN IP Intelligence", "Country, region, ISP, organization and ASN"));
            split.Panel1.Controls.Add(macCard); split.Panel2.Controls.Add(wanCard); page.Controls.Add(split);
            ConfigureEqualSplit(page, split, 360, 360);
        }

        void BuildCalculatorPage()
        {
            var page = NewPage("Calculators");
            var split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 680, SplitterWidth = 8, FixedPanel = FixedPanel.None, BackColor = Canvas };
            split.Panel1.Padding = new Padding(0, 0, 4, 0); split.Panel2.Padding = new Padding(4, 0, 0, 0);
            var subnetCard = Card();
            var subnetBar = new TableLayoutPanel { Dock = DockStyle.Top, Height = 50, ColumnCount = 2, Padding = new Padding(0, 7, 0, 7) };
            subnetBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); subnetBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 155));
            subnetInput = new TextBox { Dock = DockStyle.Fill, Text = "192.168.1.1/24", Font = new Font("Consolas", 10) };
            var calculate = ActionButton("Calculate", true, 0); calculate.Dock = DockStyle.Fill; calculate.Click += CalculateSubnet;
            subnetBar.Controls.Add(subnetInput, 0, 0); subnetBar.Controls.Add(calculate, 1, 0);
            subnetOutput = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Font = new Font("Consolas", 10.5f), BorderStyle = BorderStyle.FixedSingle };
            subnetCard.Controls.Add(subnetOutput); subnetCard.Controls.Add(subnetBar); subnetCard.Controls.Add(SectionHeader("IPv4 Subnet Calculator", "Accepts address/prefix, address /prefix, or address subnet-mask"));
            var converterCard = Card();
            var converter = new TableLayoutPanel { Dock = DockStyle.Top, Height = 220, ColumnCount = 2, RowCount = 5, Padding = new Padding(0, 10, 0, 10) };
            converter.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); converter.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            converter.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
            converter.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            converter.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            converter.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            converter.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
            unitValue = new TextBox { Dock = DockStyle.Fill, Text = "1000" };
            string[] units = { "bit", "Kbit", "Mbit", "Gbit", "Tbit", "Byte", "KB", "MB", "GB", "TB", "KiB", "MiB", "GiB" };
            unitFrom = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList }; unitFrom.Items.AddRange(units); unitFrom.SelectedItem = "Mbit";
            unitTo = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList }; unitTo.Items.AddRange(units); unitTo.SelectedItem = "Gbit";
            var convert = ActionButton("Convert", true, 135); convert.Dock = DockStyle.Right; convert.Click += ConvertUnits;
            var convertAction = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 4, 0, 4) }; convertAction.Controls.Add(convert);
            unitOutput = new Label { Text = "Result: —", Dock = DockStyle.Fill, Font = new Font("Segoe UI Semibold", 15), ForeColor = Accent, TextAlign = ContentAlignment.MiddleLeft };
            converter.Controls.Add(FieldLabel("Value"), 0, 0); converter.SetColumnSpan(converter.GetControlFromPosition(0, 0), 2);
            converter.Controls.Add(unitValue, 0, 1); converter.SetColumnSpan(unitValue, 2);
            converter.Controls.Add(unitFrom, 0, 2); converter.Controls.Add(unitTo, 1, 2);
            converter.Controls.Add(convertAction, 0, 3); converter.SetColumnSpan(convertAction, 2);
            converter.Controls.Add(unitOutput, 0, 4); converter.SetColumnSpan(unitOutput, 2);
            var reference = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, Font = new Font("Consolas", 9.5f), BorderStyle = BorderStyle.FixedSingle, Text =
                "QUICK REFERENCE\r\n\r\n/16   255.255.0.0       65,534 usable\r\n/17   255.255.128.0     32,766 usable\r\n/18   255.255.192.0     16,382 usable\r\n/19   255.255.224.0      8,190 usable\r\n/20   255.255.240.0      4,094 usable\r\n/21   255.255.248.0      2,046 usable\r\n/22   255.255.252.0      1,022 usable\r\n/23   255.255.254.0        510 usable\r\n/24   255.255.255.0        254 usable\r\n/25   255.255.255.128      126 usable\r\n/26   255.255.255.192       62 usable\r\n/27   255.255.255.224       30 usable\r\n/28   255.255.255.240       14 usable\r\n/29   255.255.255.248        6 usable\r\n/30   255.255.255.252        2 usable\r\n/31   255.255.255.254        2 point-to-point\r\n/32   255.255.255.255        1 host\r\n\r\nDecimal SI: 1 Mbit = 1,000,000 bits\r\nBinary IEC: 1 MiB = 1,048,576 bytes" };
            converterCard.Controls.Add(reference); converterCard.Controls.Add(converter); converterCard.Controls.Add(SectionHeader("Unit Converter", "Network rate and storage units"));
            split.Panel1.Controls.Add(subnetCard); split.Panel2.Controls.Add(converterCard); page.Controls.Add(split);
            ConfigureEqualSplit(page, split, 360, 360);
        }

        void BuildLogPage()
        {
            var page = NewPage("Event Log");
            var card = Card();
            var toolbar = new TableLayoutPanel { Dock = DockStyle.Top, Height = 55, ColumnCount = 4, Padding = new Padding(0, 8, 0, 8) };
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130)); toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 115)); toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
            logSearch = new TextBox { Dock = DockStyle.Fill }; Cue(logSearch, "Filter source or message"); logSearch.TextChanged += delegate { ApplyLogFilter(); };
            logLevel = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList }; logLevel.Items.AddRange(new object[] { "All levels", "INFO", "ACTION", "WARNING", "ERROR" }); logLevel.SelectedIndex = 0; logLevel.SelectedIndexChanged += delegate { ApplyLogFilter(); };
            var export = ActionButton("Export log", false, 0); export.Dock = DockStyle.Fill; export.Click += ExportLog;
            var clear = ActionButton("Clear", false, 0); clear.Dock = DockStyle.Fill; clear.Click += delegate { logTable.Rows.Clear(); };
            toolbar.Controls.Add(logSearch, 0, 0); toolbar.Controls.Add(logLevel, 1, 0); toolbar.Controls.Add(export, 2, 0); toolbar.Controls.Add(clear, 3, 0);
            logTable = new DataTable(); logTable.Columns.Add("Time", typeof(DateTime)); logTable.Columns.Add("Level"); logTable.Columns.Add("Source"); logTable.Columns.Add("Message");
            logSource = new BindingSource { DataSource = logTable };
            logGrid = DataGrid(); logGrid.AutoGenerateColumns = false;
            AddGridColumn(logGrid, "Timestamp", "Time", 175); AddGridColumn(logGrid, "Level", "Level", 90); AddGridColumn(logGrid, "Source", "Source", 130); AddGridColumn(logGrid, "Message", "Message", 700);
            logGrid.DataSource = logSource; logGrid.CellFormatting += FormatLogCell; logGrid.KeyDown += GridCopyShortcut;
            card.Controls.Add(logGrid); card.Controls.Add(toolbar); card.Controls.Add(SectionHeader("Realtime event log", "Operational events and state changes; routine successful pings are summarized in the Ping table"));
            page.Controls.Add(card);
        }

        void BuildUpdatesPage()
        {
            var page = NewPage("Updates");
            var card = Card();
            var notes = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 10),
                BackColor = Surface,
                Text =
                    "NetStuck v.1.2.3 (Current)\r\n" +
                    "Traceroute deterministic input alignment\r\n\r\n" +
                    "- Aligned Traceroute inputs in adjacent responsive grid columns.\r\n" +
                    "- Moved Protocol, Port and Packet size together so the protocol dropdown cannot cover timing inputs.\r\n" +
                    "- Preserved the dedicated action row and narrow-window safeguards.\r\n\r\n" +
                    "NetStuck v.1.2.2\r\n" +
                    "Traceroute responsive-layout correction\r\n\r\n" +
                    "- Prevented timing inputs and action buttons from overlapping at narrow widths or high DPI.\r\n" +
                    "- Standardized enabled and disabled Traceroute input backgrounds.\r\n" +
                    "- Kept all actions inside the bordered panel on a dedicated responsive row.\r\n\r\n" +
                    "NetStuck v.1.2.1\r\n" +
                    "Traceroute control-panel layout refinement\r\n\r\n" +
                    "- Rebuilt the Traceroute settings area as one bordered two-row control panel.\r\n" +
                    "- Kept Target, Protocol, Port and Packet Size aligned on the first row.\r\n" +
                    "- Placed Max Hops, Timeout and Interval on the left of the second row, with equal Start, Pause and Stop actions aligned on the right.\r\n" +
                    "- Reduced unused space without changing polling behavior.\r\n\r\n" +
                    "NetStuck v.1.2.0\r\n" +
                    "High-volume monitoring and collector performance\r\n\r\n" +
                    "- Exported Config Collector failures as CSV with IP, status, protocol, username and error detail only.\r\n" +
                    "- Batched terminal rendering and streamed large SSH/Telnet captures directly to disk.\r\n" +
                    "- Updated only changed Traceroute hops and added adaptive TTL probing for stable routes.\r\n" +
                    "- Added persistent ISP and reverse-DNS caches with expiry.\r\n" +
                    "- Balanced Traceroute Protocol, Port, Packet Size and timing input widths.\r\n" +
                    "- Added an overnight soak-test harness and removed the dormant Log Sanitizer source.\r\n\r\n" +
                    "NetStuck v.1.1.0\r\n" +
                    "Collector diagnostics and Traceroute usability\r\n\r\n" +
                    "- Added Config Collector diagnostic-log export with device result and terminal error details.\r\n" +
                    "- Removed the Log Sanitizer menu.\r\n" +
                    "- Preserved Traceroute vertical/horizontal scroll and current-cell position during polling refreshes.\r\n" +
                    "- Rebuilt Traceroute input borders and spacing so Protocol, Port, Packet Size and timing fields are not clipped.\r\n" +
                    "- Retained prompt-aware SSH collection, one literal DOMAIN\\username separator and safe AUTH fallback.\r\n\r\n" +
                    "NetStuck v.1.0.4\r\n" +
                    "Polling performance and operation-state refinement\r\n\r\n" +
                    "- Moved Local/Public IP information to the bottom-right status area with visual separators.\r\n" +
                    "- Changed Live Ping to direct ICMP/TCP selection, TCP-only custom port, and a main Packet Size field.\r\n" +
                    "- Added clear START/MONITORING, PAUSE/RESUME and STOP NOW/STOPPING button states.\r\n" +
                    "- Changed Live Ping status text to ICMP OK, Unreachable, Connected or TCP Timeout.\r\n" +
                    "- Corrected 250/1000 ms polling with bounded fixed-cadence probes, including timeout conditions.\r\n" +
                    "- Reflowed Traceroute inputs and actions into three clear rows so dropdowns and buttons do not overlap.\r\n" +
                    "- Added green running dots for Live Ping, Traceroute and Config Collector tabs.\r\n" +
                    "- Added cached ISP/ASN owner descriptions for public Traceroute hops.\r\n\r\n" +
                    "NetStuck v.1.0.3\r\n" +
                    "Dual traceroute sessions and advanced live probes\r\n\r\n" +
                    "- Added My Local IP and My Public IP to the global status bar.\r\n" +
                    "- Added Live Ping Pause, detected Source IP choices, sequence numbers, full-row selection and Advanced ICMP/TCP/UDP probes.\r\n" +
                    "- Added two independent continuous Traceroute sessions with Pause, packet size and TCP/UDP destination service checks.\r\n" +
                    "- Added a resizable, filterable Traceroute Event Log for route, DNS, service and error changes.\r\n" +
                    "- Removed visible Custom DNS, Continuous and Resolve-name options from Traceroute; continuous polling and system DNS are automatic.\r\n\r\n" +
                    "NetStuck v.1.0.2\r\n" +
                    "Collector authentication patch\r\n\r\n" +
                    "• Added Cisco keyboard-interactive SSH fallback when Plink batch authentication cannot answer the password prompt.\r\n" +
                    "• Normalized DOMAIN\\username separators and added a runtime literal-backslash diagnostic.\r\n" +
                    "• Added a child-process argv integration test so the actual Plink argument path is verified.\r\n" +
                    "• Added one automatic retry for transient SSH connection abort/reset/timeout errors.\r\n\r\n" +
                    "NetStuck v.1.0.1\r\n" +
                    "Patch release\r\n\r\n" +
                    "• Enabled mouse text selection and copying in result tables.\r\n" +
                    "• Ping History now preserves manual scrolling and resets for every new monitoring session.\r\n" +
                    "• Added running indicators to active feature tabs.\r\n" +
                    "• Refined action-button sizing and Traceroute, DNS, MAC/WAN and Calculator layouts.\r\n" +
                    "• Improved MAC vendor lookup with OUI caching, throttling, retry handling and clearer statuses.\r\n" +
                    "• Improved DOMAIN\\username command-line quoting in SSH Config Collector.\r\n\r\n" +
                    "NetStuck v.1.0.0\r\n" +
                    "First stable release\r\n\r\n" +
                    "• Realtime Live Ping, per-target history and saved in-app target profiles.\r\n" +
                    "• Realtime per-hop Traceroute, DNS Resolver and polling.\r\n" +
                    "• MAC/WAN lookup, network calculators and Config Collector.\r\n" +
                    "• Persistent application state, NTP-first clock, zoom and exportable logs."
            };
            card.Controls.Add(notes);
            card.Controls.Add(SectionHeader("Updates", "Short release notes for each NetStuck version"));
            page.Controls.Add(card);
        }

        async void StartPing(object sender, EventArgs e)
        {
            List<TargetSpec> targets;
            try { targets = NetOpsCore.ExpandTargets(targetInput.Text, MaxExpandedTargets); }
            catch (Exception ex) { MessageBox.Show(this, FriendlyError(ex), AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            if (targets.Count == 0) { MessageBox.Show(this, "Enter at least one valid target.", AppName, MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            if (targets.Count > 512 && MessageBox.Show(this, "This session will monitor " + targets.Count + " targets.\r\n\r\nContinue?", AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            string dns = pingUseCustomDns.Checked ? pingDnsServer.Text.Trim() : "";
            if (pingUseCustomDns.Checked && !IsValidIp(dns)) { MessageBox.Show(this, "Custom DNS must be a valid IP address.", AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            int interval = (int)pingInterval.Value;
            int timeout = (int)pingTimeout.Value;
            string protocol = pingProtocol == null || String.IsNullOrWhiteSpace(pingProtocol.Text) ? "ICMP" : pingProtocol.Text;
            int port = pingPort == null ? 0 : (int)pingPort.Value;
            int packetSize = pingPacketSize == null ? 32 : (int)pingPacketSize.Value;
            string selectedSource = SelectedPingSourceAddress();
            PingUiUpdate discardedUpdate;
            while (pingUiUpdates.TryDequeue(out discardedUpdate)) { }
            ResetPingHistoryForNewSession();
            ClearPing();
            int sequence = 0;
            foreach (TargetSpec target in targets)
            {
                DataRow row = pingTable.NewRow();
                row["Status"] = "Waiting"; row["Host"] = target.Host; row["Description"] = target.Description; row["ResolvedIp"] = "";
                row["Seq"] = ++sequence; row["SourceIp"] = selectedSource; row["Protocol"] = protocol; row["Port"] = protocol == "ICMP" ? (object)DBNull.Value : port;
                row["Sent"] = 0L; row["Received"] = 0L; row["Lost"] = 0L; row["LossPct"] = 0d; row["Error"] = "";
                pingTable.Rows.Add(row);
                lastTargetStatus[target.Host] = "Waiting";
            }
            UpdatePingMetrics();
            pingCancellation = new CancellationTokenSource();
            CancellationTokenSource pingSession = pingCancellation;
            SetPingRunning(true);
            pingPaused = false;
            Log("ACTION", "Ping", "Started " + targets.Count + " target(s), " + protocol + (protocol == "ICMP" ? "" : "/" + port) + ", interval " + interval + " ms, timeout " + timeout + " ms");
            try
            {
                Task[] jobs = targets.Select((t, index) => PingLoopV103(t, interval, timeout, dns, protocol, port, packetSize,
                    selectedSource, ComputePingStaggerMs(index, targets.Count, interval), pingSession.Token)).ToArray();
                await Task.WhenAll(jobs);
            }
            catch (OperationCanceledException) { }
            finally
            {
                pingSession.Dispose();
                CompletePingSession(pingSession);
            }
        }

        void CompletePingSession(CancellationTokenSource session)
        {
            if (InvokeRequired)
            {
                try { BeginInvoke(new Action<CancellationTokenSource>(CompletePingSession), session); }
                catch { if (Object.ReferenceEquals(pingCancellation, session)) pingCancellation = null; }
                return;
            }
            if (!Object.ReferenceEquals(pingCancellation, session)) return;
            if (!appClosing && !IsDisposed && !Disposing)
            {
                SetPingRunning(false);
                Log("INFO", "Ping", "Monitoring stopped");
            }
            pingCancellation = null;
        }

        async Task PingLoop(TargetSpec target, int interval, int timeout, string customDns, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                PingReply reply = null;
                string resolved = "";
                string error = "";
                try
                {
                    string destination = target.Host;
                    IPAddress ip;
                    if (!IPAddress.TryParse(destination, out ip))
                    {
                        List<string> answers = await ResolveForward(target.Host, customDns).ConfigureAwait(false);
                        if (answers.Count == 0) throw new InvalidOperationException("DNS resolution failed");
                        destination = answers[0]; resolved = destination;
                    }
                    else resolved = ip.ToString();
                    using (var ping = new Ping()) reply = await ping.SendPingAsync(destination, timeout).ConfigureAwait(false);
                }
                catch (Exception ex) { error = FriendlyError(ex); }

                bool up = reply != null && reply.Status == IPStatus.Success;
                long latency = up ? reply.RoundtripTime : -1;
                string result = up ? "Reply from " + reply.Address : (reply == null ? error : reply.Status.ToString());
                int ttl = up && reply.Options != null ? reply.Options.Ttl : 0;
                pingUiUpdates.Enqueue(new PingUiUpdate
                {
                    Host = target.Host, Resolved = resolved, Up = up, Latency = latency,
                    Ttl = ttl, Detail = result, EventTime = DateTime.Now
                });
                await Task.Delay(interval, token).ConfigureAwait(false);
            }
        }

        void UpdatePingRow(string host, string resolved, bool up, long latency, int ttl, string detail, DateTime eventTime)
        {
            UpdatePingRowCoreV105(host, resolved, up, latency, ttl, detail, eventTime, true);
        }

        void UpdatePingRowCoreV105(string host, string resolved, bool up, long latency, int ttl, string detail, DateTime eventTime, bool updateCurrentState)
        {
            DataRow row = pingTable.Rows.Find(host);
            if (row == null) return;
            long sent = Convert.ToInt64(row["Sent"]) + 1;
            long received = Convert.ToInt64(row["Received"]) + (up ? 1 : 0);
            long lost = sent - received;
            row["Sent"] = sent; row["Received"] = received; row["Lost"] = lost; row["LossPct"] = Math.Round(lost * 100d / sent, 2);
            if (updateCurrentState)
            {
                row["Status"] = up ? "Reachable" : "Unreachable"; row["ResolvedIp"] = resolved; row["LastPing"] = eventTime; row["Error"] = detail;
            }
            if (up)
            {
                if (updateCurrentState) { row["LastMs"] = latency; row["LastSuccess"] = eventTime; }
                long min = row.IsNull("MinMs") ? latency : Math.Min(Convert.ToInt64(row["MinMs"]), latency);
                long max = row.IsNull("MaxMs") ? latency : Math.Max(Convert.ToInt64(row["MaxMs"]), latency);
                double previousTotal = row.IsNull("TotalMs") ? 0d : Convert.ToDouble(row["TotalMs"]);
                double total = previousTotal + latency;
                row["MinMs"] = min; row["MaxMs"] = max; row["TotalMs"] = total; row["AvgMs"] = Math.Round(total / received, 2);
            }
            else if (updateCurrentState) row["LastMs"] = DBNull.Value;
            if (updateCurrentState)
            {
                string old;
                if (!lastTargetStatus.TryGetValue(host, out old)) old = "Waiting";
                string current = up ? "Reachable" : "Unreachable";
                if (!String.Equals(old, current, StringComparison.OrdinalIgnoreCase))
                {
                    if (up) row["ReachableSince"] = eventTime; else row["UnreachableSince"] = eventTime;
                    Log(up ? "INFO" : "ERROR", "Ping", host + " changed " + old + " -> " + current + " (" + detail + ")");
                    lastTargetStatus[host] = current;
                }
            }
            object[] historyValues = { eventTime, host, resolved, up ? (object)latency : DBNull.Value, ttl == 0 ? (object)DBNull.Value : ttl, up ? "Succeeded" : "Failed", sent, detail };
            pingHistoryTable.Rows.Add(historyValues);
            if (String.Equals(selectedPingHistoryHost, host, StringComparison.OrdinalIgnoreCase))
            {
                pingHistoryDisplayTable.Rows.Add((object[])historyValues.Clone());
                if (pingHistoryDisplayTable.Rows.Count > 10000) pingHistoryDisplayTable.Rows.RemoveAt(0);
            }
            if (pingHistoryTable.Rows.Count > 10000) pingHistoryTable.Rows.RemoveAt(0);
            if ((DateTime.UtcNow - lastPingMetricsUpdate).TotalMilliseconds >= 300)
            {
                lastPingMetricsUpdate = DateTime.UtcNow;
                UpdatePingMetrics();
            }
        }

        async void StartTrace(object sender, EventArgs e)
        {
            string host = traceTarget.Text.Trim();
            if (host.Length == 0) return;
            RefreshHopDescriptions();
            string customDns = traceUseCustomDns.Checked ? traceDnsServer.Text.Trim() : "";
            if (traceUseCustomDns.Checked && !IsValidIp(customDns)) { MessageBox.Show(this, "Custom DNS must be a valid IP address."); return; }
            int maxHops = (int)traceMaxHops.Value, timeout = (int)traceTimeout.Value, interval = (int)traceInterval.Value;
            bool resolveNames = traceResolveNames.Checked, continuous = traceContinuous.Checked;
            RememberTraceTarget(host);
            traceTable.Rows.Clear();
            traceCancellation = new CancellationTokenSource();
            SetTraceRunning(true);
            traceStateLabel.Text = "Target: Discovering";
            traceStateLabel.ForeColor = Warning;
            Log("ACTION", "Traceroute", "Realtime trace started: " + host);
            var stats = new Dictionary<int, HopStats>();
            int cycle = 0;
            try
            {
                string destination = host;
                IPAddress parsed;
                if (!IPAddress.TryParse(destination, out parsed))
                {
                    List<string> answers = await ResolveForward(host, customDns);
                    if (answers.Count == 0) throw new InvalidOperationException("Unable to resolve trace target.");
                    destination = answers[0];
                }
                traceDestinationLabel.Text = "Destination: " + destination;
                int activeHops = maxHops;
                do
                {
                    cycle++; traceCycleLabel.Text = "Cycle: " + cycle;
                    Task<HopProbe>[] probeTasks = Enumerable.Range(1, activeHops).Select(ttl => ProbeHopAsync(destination, ttl, timeout)).ToArray();
                    HopProbe[] probes = await Task.WhenAll(probeTasks);
                    int reachedHop = probes.Where(p => p.Reached).Select(p => p.Hop).DefaultIfEmpty(0).Min();
                    if (reachedHop > 0)
                    {
                        activeHops = reachedHop;
                        foreach (DataRow extra in traceTable.Rows.Cast<DataRow>().Where(r => Convert.ToInt32(r["Hop"]) > reachedHop).ToArray()) traceTable.Rows.Remove(extra);
                        foreach (int extra in stats.Keys.Where(h => h > reachedHop).ToArray()) stats.Remove(extra);
                    }
                    foreach (HopProbe probe in probes.Where(p => reachedHop == 0 || p.Hop <= reachedHop).OrderBy(p => p.Hop))
                    {
                        HopStats stat;
                        if (!stats.TryGetValue(probe.Hop, out stat)) { stat = new HopStats(); stats[probe.Hop] = stat; }
                        stat.Sent++;
                        if (probe.Responded)
                        {
                            stat.Received++; stat.Last = probe.Latency; stat.Total += probe.Latency; stat.Best = Math.Min(stat.Best, probe.Latency); stat.Worst = Math.Max(stat.Worst, probe.Latency);
                            if (stat.PreviousLatency >= 0)
                            {
                                stat.JitterTotal += Math.Abs(probe.Latency - stat.PreviousLatency);
                                stat.JitterSamples++;
                            }
                            stat.PreviousLatency = probe.Latency;
                            bool addressChanged = stat.Address.Length > 0 && !String.Equals(stat.Address, probe.Address, StringComparison.OrdinalIgnoreCase);
                            if (addressChanged)
                            {
                                stat.RouteChanges++;
                                Log("WARNING", "Traceroute", "Hop " + probe.Hop + " route changed " + stat.Address + " → " + probe.Address);
                            }
                            if (!String.Equals(stat.Address, probe.Address, StringComparison.OrdinalIgnoreCase))
                            {
                                stat.Address = probe.Address; stat.Hostname = "";
                                if (resolveNames && probe.Address.Length > 0) ResolveHopName(stat, probe.Hop, probe.Address, customDns);
                            }
                        }
                        UpsertTrace(probe.Hop, stat, probe.Status);
                    }
                    bool targetReached = probes.Any(p => p.Reached);
                    bool targetUnreachable = probes.Any(p => p.Status == "Unreachable");
                    traceStateLabel.Text = targetReached ? "Target: Reachable" : targetUnreachable ? "Target: Unreachable" : "Target: No final reply";
                    traceStateLabel.ForeColor = targetReached ? Success : targetUnreachable ? Danger : Warning;
                    if (!continuous) break;
                    await Task.Delay(interval, traceCancellation.Token);
                } while (!traceCancellation.IsCancellationRequested);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { Log("ERROR", "Traceroute", ex.Message); MessageBox.Show(this, ex.Message, "Traceroute", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            finally
            {
                traceCancellation.Dispose(); traceCancellation = null;
                if (!appClosing && !IsDisposed && !Disposing)
                {
                    SetTraceRunning(false);
                    Log("INFO", "Traceroute", "Trace stopped");
                }
            }
        }

        async Task<HopProbe> ProbeHopAsync(string destination, int hop, int timeout)
        {
            var result = new HopProbe { Hop = hop };
            try
            {
                PingReply reply;
                var watch = Stopwatch.StartNew();
                using (var ping = new Ping())
                    reply = await ping.SendPingAsync(destination, timeout, new byte[32], new PingOptions(hop, true)).ConfigureAwait(false);
                watch.Stop();
                result.IpStatus = reply.Status;
                result.Address = reply.Address == null ? "" : reply.Address.ToString();
                // Windows commonly reports RoundtripTime=0 for TTL Expired. Stopwatch
                // measures the actual request/reply elapsed time for intermediate hops.
                result.Latency = reply.RoundtripTime > 0 ? reply.RoundtripTime : Math.Max(0.1, watch.Elapsed.TotalMilliseconds);
                result.Reached = reply.Status == IPStatus.Success;
                bool unreachable = reply.Status == IPStatus.DestinationHostUnreachable || reply.Status == IPStatus.DestinationNetworkUnreachable || reply.Status == IPStatus.DestinationPortUnreachable || reply.Status == IPStatus.DestinationProtocolUnreachable;
                result.Responded = result.Reached || reply.Status == IPStatus.TtlExpired || unreachable;
                result.Status = result.Reached ? "Reached" : reply.Status == IPStatus.TtlExpired ? "Reply" : unreachable ? "Unreachable" : reply.Status == IPStatus.TimedOut ? "Timeout" : reply.Status.ToString();
            }
            catch (Exception ex) { result.Status = FriendlyError(ex); }
            return result;
        }

        async void ResolveHopName(HopStats stat, int hop, string address, string customDns)
        {
            try
            {
                string hostname = await ResolveReverse(IPAddress.Parse(address), customDns);
                if (String.Equals(stat.Address, address, StringComparison.OrdinalIgnoreCase)) stat.Hostname = hostname;
                DataRow row = traceTable.Rows.Find(hop);
                if (row != null && String.Equals(Convert.ToString(row["Address"]), address, StringComparison.OrdinalIgnoreCase)) row["Hostname"] = hostname;
            }
            catch { }
        }

        void UpsertTrace(int hop, HopStats s, string status)
        {
            DataRow row = traceTable.Rows.Find(hop);
            if (row == null) { row = traceTable.NewRow(); row["Hop"] = hop; traceTable.Rows.Add(row); }
            row["Address"] = s.Address.Length == 0 ? "*" : s.Address; row["Hostname"] = s.Hostname; row["Description"] = GetHopDescription(s.Address); row["Status"] = status;
            row["LastMs"] = s.Received == 0 ? (object)DBNull.Value : Math.Round(s.Last, 2); row["BestMs"] = s.Received == 0 ? (object)DBNull.Value : Math.Round(s.Best, 2);
            row["AvgMs"] = s.Received == 0 ? (object)DBNull.Value : Math.Round(s.Total / (double)s.Received, 2); row["WorstMs"] = s.Received == 0 ? (object)DBNull.Value : Math.Round(s.Worst, 2);
            row["JitterMs"] = s.JitterSamples == 0 ? (object)DBNull.Value : Math.Round(s.JitterTotal / s.JitterSamples, 2);
            row["Sent"] = s.Sent; row["Received"] = s.Received; row["LossPct"] = Math.Round((s.Sent - s.Received) * 100d / s.Sent, 2);
            row["RouteChanges"] = s.RouteChanges; row["Updated"] = DateTime.Now.ToString("HH:mm:ss");
        }

        async void ResolveDns(object sender, EventArgs e)
        {
            List<TargetSpec> queries = NetOpsCore.ParseTargets(dnsInput.Text);
            dnsTable.Rows.Clear();
            string server = dnsUseCustom.Checked ? dnsServer.Text.Trim() : "";
            if (dnsUseCustom.Checked && !IsValidIp(server)) { MessageBox.Show(this, "Custom DNS must be a valid IP address."); return; }
            appStatus.Text = "Resolving DNS…";
            foreach (TargetSpec query in queries)
            {
                IPAddress ip;
                try
                {
                    if (IPAddress.TryParse(query.Host, out ip))
                    {
                        string answer = "";
                        try { answer = await ResolveReverse(ip, server); } catch { }
                        dnsTable.Rows.Add(query.Host, query.Description, "PTR", answer.Length > 0 ? "OK" : "NOT FOUND", answer.Length > 0 ? answer : "No PTR record", server.Length > 0 ? server : "System DNS", DateTime.Now.ToString("HH:mm:ss"));
                    }
                    else
                    {
                        List<string> answers = await ResolveForward(query.Host, server);
                        dnsTable.Rows.Add(query.Host, query.Description, "A / AAAA", answers.Count > 0 ? "OK" : "NOT FOUND", answers.Count > 0 ? String.Join(", ", answers) : "No answer", server.Length > 0 ? server : "System DNS", DateTime.Now.ToString("HH:mm:ss"));
                    }
                }
                catch (Exception ex) { dnsTable.Rows.Add(query.Host, query.Description, "—", "ERROR", FriendlyError(ex), server.Length > 0 ? server : "System DNS", DateTime.Now.ToString("HH:mm:ss")); }
            }
            appStatus.Text = "Ready"; Log("INFO", "DNS", "Resolved " + queries.Count + " query(s)");
        }

        async Task<List<string>> ResolveForward(string host, string customDns)
        {
            if (String.IsNullOrWhiteSpace(customDns))
                return (await Dns.GetHostAddressesAsync(host)).Select(a => a.ToString()).Distinct().ToList();
            var psi = new ProcessStartInfo("nslookup.exe", "\"" + host + "\" " + customDns)
                { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
            using (Process p = Process.Start(psi))
            {
                Task<string> outputTask = p.StandardOutput.ReadToEndAsync();
                Task<string> errorTask = p.StandardError.ReadToEndAsync();
                Task wait = Task.Run(delegate { p.WaitForExit(); });
                if (await Task.WhenAny(wait, Task.Delay(6500)) != wait)
                {
                    try { p.Kill(); } catch { }
                    throw new TimeoutException("Custom DNS query exceeded 6.5 seconds.");
                }
                string output = await outputTask;
                await errorTask;
                return NetOpsCore.ExtractIPv4(output).Where(ip => !String.Equals(ip, customDns, StringComparison.OrdinalIgnoreCase)).Distinct().ToList();
            }
        }

        async Task<string> ResolveReverse(IPAddress address, string customDns)
        {
            if (String.IsNullOrWhiteSpace(customDns))
                return (await Dns.GetHostEntryAsync(address)).HostName;
            var psi = new ProcessStartInfo("nslookup.exe", address + " " + customDns)
                { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
            using (Process p = Process.Start(psi))
            {
                Task<string> outputTask = p.StandardOutput.ReadToEndAsync();
                Task<string> errorTask = p.StandardError.ReadToEndAsync();
                Task wait = Task.Run(delegate { p.WaitForExit(); });
                if (await Task.WhenAny(wait, Task.Delay(6500)) != wait)
                {
                    try { p.Kill(); } catch { }
                    throw new TimeoutException("Custom reverse-DNS query exceeded 6.5 seconds.");
                }
                string output = await outputTask;
                await errorTask;
                Match match = Regex.Match(output, @"(?im)^\s*name\s*=\s*(\S+)\s*$");
                return match.Success ? match.Groups[1].Value.TrimEnd('.') : "";
            }
        }

        async void LookupMac(object sender, EventArgs e)
        {
            List<string> macs = NetOpsCore.ExtractMacs(macInput.Text);
            macTable.Rows.Clear();
            if (macs.Count == 0) { MessageBox.Show(this, "No valid MAC addresses were found."); return; }
            var rows = new Dictionary<string, DataRow>(StringComparer.OrdinalIgnoreCase);
            foreach (string mac in macs)
            {
                DataRow row = macTable.Rows.Add(mac, "Waiting for vendor lookup…", "SEARCHING");
                rows[mac] = row;
            }
            appStatus.Text = "Looking up MAC vendors…";
            macLookupButton.Enabled = false;
            SetTabActivity("MAC / WAN Lookup", true);
            bool cacheChanged = false;
            try
            {
                foreach (string mac in macs)
                {
                    DataRow row = rows[mac];
                    try
                    {
                        string oui = Regex.Replace(mac, "[^0-9A-Fa-f]", "").ToUpperInvariant().Substring(0, 6);
                        byte firstByte = Byte.Parse(oui.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                        if ((firstByte & 1) != 0)
                        {
                            row["Vendor"] = "Multicast address — no vendor lookup";
                            row["Status"] = "SKIPPED";
                            continue;
                        }
                        if ((firstByte & 2) != 0)
                        {
                            row["Vendor"] = "Locally administered / randomized MAC";
                            row["Status"] = "LOCAL";
                            continue;
                        }

                        string cached;
                        if (macVendorCache.TryGetValue(oui, out cached))
                        {
                            row["Vendor"] = cached.Length == 0 ? "No registered vendor found" : cached;
                            row["Status"] = cached.Length == 0 ? "NOT FOUND" : "CACHED";
                            continue;
                        }

                        MacLookupResult result = await LookupMacVendorApiAsync(oui);
                        row["Vendor"] = result.Vendor;
                        row["Status"] = result.Status;
                        if (result.Cacheable)
                        {
                            macVendorCache[oui] = result.Status == "NOT FOUND" ? "" : result.Vendor;
                            cacheChanged = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        row["Vendor"] = FriendlyError(ex);
                        row["Status"] = "ERROR";
                    }
                }
            }
            finally
            {
                if (cacheChanged) SaveMacVendorCache();
                macLookupButton.Enabled = true;
                SetTabActivity("MAC / WAN Lookup", false);
                appStatus.Text = "Ready";
            }
            Log("INFO", "MAC", "Processed " + macs.Count + " MAC address(es)");
        }

        async void LookupWan(object sender, EventArgs e)
        {
            List<string> ips = NetOpsCore.ExtractIPv4(wanInput.Text);
            wanTable.Rows.Clear();
            if (ips.Count == 0) { MessageBox.Show(this, "No valid IPv4 addresses were found."); return; }
            appStatus.Text = "Looking up WAN IPs…";
            wanLookupButton.Enabled = false;
            SetTabActivity("MAC / WAN Lookup", true);
            try
            {
                var serializer = new JavaScriptSerializer();
                using (WebClient web = NewWebClient())
                {
                    foreach (string ip in ips)
                    {
                        try
                        {
                            string json = await web.DownloadStringTaskAsync("https://ipwho.is/" + ip);
                            var data = serializer.Deserialize<Dictionary<string, object>>(json);
                            bool ok = data.ContainsKey("success") && Convert.ToBoolean(data["success"]);
                            var connection = data.ContainsKey("connection") ? data["connection"] as Dictionary<string, object> : null;
                            var timezone = data.ContainsKey("timezone") ? data["timezone"] as Dictionary<string, object> : null;
                            wanTable.Rows.Add(ip, ok ? "OK" : "ERROR", Value(data, "country"), Value(data, "region"), Value(connection, "isp"), Value(connection, "org"), Value(connection, "asn"), Value(timezone, "id"), ok ? "Public IP details" : Value(data, "message"));
                        }
                        catch (Exception ex) { wanTable.Rows.Add(ip, "ERROR", "", "", "", "", "", "", FriendlyError(ex)); }
                    }
                }
            }
            finally
            {
                wanLookupButton.Enabled = true;
                SetTabActivity("MAC / WAN Lookup", false);
                appStatus.Text = "Ready";
            }
            Log("INFO", "WAN", "Looked up " + ips.Count + " IP address(es)");
        }

        async Task<MacLookupResult> LookupMacVendorApiAsync(string oui)
        {
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                int throttleDelay = 130 - (int)(DateTime.UtcNow - lastMacApiRequestUtc).TotalMilliseconds;
                if (throttleDelay > 0) await Task.Delay(throttleDelay);
                lastMacApiRequestUtc = DateTime.UtcNow;
                int retryDelay = -1;

                using (WebClient web = NewWebClient())
                {
                    try
                    {
                        string json = await web.DownloadStringTaskAsync("https://api.maclookup.app/v2/macs/" + Uri.EscapeDataString(oui));
                        var data = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(json);
                        bool hasFound = data != null && data.ContainsKey("found");
                        bool success = data != null && data.ContainsKey("success") && Convert.ToBoolean(data["success"], CultureInfo.InvariantCulture);
                        bool found = hasFound && Convert.ToBoolean(data["found"], CultureInfo.InvariantCulture);
                        string company = Value(data, "company").Trim();
                        if (success && found)
                        {
                            if (company.Length == 0) company = "Registered vendor (company name unavailable)";
                            return new MacLookupResult { Vendor = company, Status = "OK", Cacheable = true };
                        }
                        if (success && hasFound && !found)
                            return new MacLookupResult { Vendor = "No registered vendor found", Status = "NOT FOUND", Cacheable = true };
                        string error = Value(data, "error");
                        return new MacLookupResult { Vendor = error.Length == 0 ? "Unexpected response from vendor service" : error, Status = "ERROR" };
                    }
                    catch (WebException ex)
                    {
                        var response = ex.Response as HttpWebResponse;
                        int statusCode = response == null ? 0 : (int)response.StatusCode;
                        if (statusCode == 404)
                        {
                            if (response != null) response.Close();
                            return new MacLookupResult { Vendor = "No registered vendor found", Status = "NOT FOUND", Cacheable = true };
                        }
                        if (statusCode == 409 || statusCode == 429)
                        {
                            retryDelay = GetRetryDelayMs(response, attempt);
                            if (response != null) response.Close();
                            if (attempt >= 3)
                                return new MacLookupResult { Vendor = "API rate limit reached — try again shortly", Status = "RATE LIMITED" };
                        }
                        else
                        {
                            if (response != null) response.Close();
                            return new MacLookupResult { Vendor = FriendlyError(ex), Status = "ERROR" };
                        }
                    }
                }
                if (retryDelay >= 0) await Task.Delay(retryDelay);
            }
            return new MacLookupResult { Vendor = "API rate limit reached — try again shortly", Status = "RATE LIMITED" };
        }

        static int GetRetryDelayMs(HttpWebResponse response, int attempt)
        {
            string value = response == null ? "" : response.Headers[HttpResponseHeader.RetryAfter];
            int seconds;
            if (Int32.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out seconds))
                return Math.Max(250, Math.Min(5000, seconds * 1000));
            DateTime retryAt;
            if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out retryAt))
                return Math.Max(250, Math.Min(5000, (int)(retryAt.ToUniversalTime() - DateTime.UtcNow).TotalMilliseconds));
            return Math.Min(5000, 600 * attempt);
        }

        void LoadMacVendorCache()
        {
            try
            {
                if (!File.Exists(macCachePath)) return;
                var saved = new JavaScriptSerializer().Deserialize<Dictionary<string, string>>(File.ReadAllText(macCachePath, Encoding.UTF8));
                if (saved == null) return;
                foreach (KeyValuePair<string, string> pair in saved)
                    if (Regex.IsMatch(pair.Key ?? "", "^[0-9A-Fa-f]{6}$")) macVendorCache[pair.Key.ToUpperInvariant()] = pair.Value ?? "";
            }
            catch { }
        }

        void SaveMacVendorCache()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(macCachePath));
                File.WriteAllText(macCachePath, new JavaScriptSerializer().Serialize(macVendorCache), new UTF8Encoding(false));
            }
            catch { }
        }

        void CalculateSubnet(object sender, EventArgs e)
        {
            try
            {
                SubnetResult r = NetOpsCore.CalculateSubnet(subnetInput.Text);
                subnetOutput.Text =
                    "Address          " + r.Address + "\r\n" +
                    "CIDR             /" + r.Prefix + "\r\n" +
                    "Subnet mask      " + r.Mask + "\r\n" +
                    "Wildcard mask    " + r.Wildcard + "\r\n" +
                    "Network          " + r.Network + "\r\n" +
                    "Broadcast        " + r.Broadcast + "\r\n" +
                    "First usable     " + r.FirstUsable + "\r\n" +
                    "Last usable      " + r.LastUsable + "\r\n" +
                    "Total addresses  " + r.Total.ToString("N0") + "\r\n" +
                    "Usable hosts     " + r.Usable.ToString("N0") + "\r\n" +
                    "Address class    " + r.AddressClass + "\r\n" +
                    "Private address  " + (r.IsPrivate ? "Yes" : "No");
            }
            catch (Exception ex) { subnetOutput.Text = "Invalid input\r\n\r\n" + FriendlyError(ex) + "\r\n\r\nAccepted examples:\r\n192.168.1.1/24\r\n192.168.1.1 /24\r\n192.168.1.1 255.255.255.0"; }
        }

        void ConvertUnits(object sender, EventArgs e)
        {
            double value;
            if (!Double.TryParse(unitValue.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && !Double.TryParse(unitValue.Text, out value)) { unitOutput.Text = "Result: invalid number"; return; }
            double result = NetOpsCore.ConvertUnit(value, unitFrom.Text, unitTo.Text);
            unitOutput.Text = "Result: " + result.ToString("0.########", CultureInfo.InvariantCulture) + " " + unitTo.Text;
        }

        void ApplyPingFilter()
        {
            if (pingSource == null) return;
            var filters = new List<string>();
            string search = pingSearch.Text.Trim();
            if (search.Length > 0)
            {
                search = EscapeFilter(search);
                filters.Add("(Host LIKE '%" + search + "%' OR Description LIKE '%" + search + "%' OR ResolvedIp LIKE '%" + search + "%' OR Status LIKE '%" + search + "%' OR Error LIKE '%" + search + "%')");
            }
            if (pingStatusFilter.SelectedIndex > 0) filters.Add("Status = '" + EscapeFilter(Convert.ToString(pingStatusFilter.SelectedItem)) + "'");
            pingSource.Filter = String.Join(" AND ", filters);
        }

        void ApplyLogFilter()
        {
            if (logSource == null) return;
            var filters = new List<string>();
            string search = logSearch.Text.Trim();
            if (search.Length > 0) { search = EscapeFilter(search); filters.Add("(Source LIKE '%" + search + "%' OR Message LIKE '%" + search + "%')"); }
            if (logLevel.SelectedIndex > 0) filters.Add("Level = '" + logLevel.SelectedItem + "'");
            logSource.Filter = String.Join(" AND ", filters);
        }

        void UpdatePingMetrics()
        {
            int targets = pingTable.Rows.Count;
            int up = pingTable.AsEnumerable().Count(r => IsSuccessfulPingStatus(r.Field<string>("Status")));
            int down = pingTable.AsEnumerable().Count(r => IsFailedPingStatus(r.Field<string>("Status")));
            long sent = pingTable.AsEnumerable().Sum(r => r.Field<long>("Sent"));
            long received = pingTable.AsEnumerable().Sum(r => r.Field<long>("Received"));
            double loss = sent == 0 ? 0 : (sent - received) * 100d / sent;
            metricTargets.Text = targets.ToString("N0"); metricUp.Text = up.ToString("N0"); metricDown.Text = down.ToString("N0"); metricPings.Text = sent.ToString("N0"); metricLoss.Text = loss.ToString("0.0") + "%";
        }

        void SetPingRunning(bool running)
        {
            if (appClosing || IsDisposed || Disposing) return;
            SetTabActivity("Live Ping", running);
            pingStartButton.Enabled = !running; pingStopButton.Enabled = running; targetInput.ReadOnly = running;
            if (pingPauseButton != null) pingPauseButton.Enabled = running;
            pingInterval.Enabled = pingTimeout.Enabled = pingUseCustomDns.Enabled = !running; pingDnsServer.Enabled = !running && pingUseCustomDns.Checked;
            if (pingSourceIp != null) pingSourceIp.Enabled = !running;
            if (pingProtocol != null) pingProtocol.Enabled = !running;
            if (pingPacketSize != null) pingPacketSize.Enabled = !running;
            if (pingPort != null) pingPort.Enabled = !running && pingProtocol.Text == "TCP";
            profileCombo.Enabled = !running;
            if (running)
            {
                stopPingUiTimerAfterDrain = false;
                pingUiTimer.Start();
                pingStartButton.Text = "MONITORING";
                pingStartButton.BackColor = Color.FromArgb(220, 252, 231); pingStartButton.ForeColor = Color.FromArgb(21, 128, 61);
                pingStartButton.FlatAppearance.BorderColor = Color.FromArgb(134, 239, 172);
                pingStopButton.Text = "STOP NOW"; pingStopButton.BackColor = Danger; pingStopButton.ForeColor = Color.White;
                pingStopButton.FlatAppearance.BorderColor = Danger;
                appStatus.Text = "Ping monitoring active — " + pingTable.Rows.Count + " targets";
            }
            else
            {
                pingPaused = false;
                stopPingUiTimerAfterDrain = true;
                DrainPingUpdates();
                if (pingUiUpdates.IsEmpty) pingUiTimer.Stop();
                if (pingPauseButton != null) { pingPauseButton.Text = "PAUSE"; pingPauseButton.BackColor = Surface; pingPauseButton.ForeColor = TextMain; pingPauseButton.FlatAppearance.BorderColor = Border; }
                pingStartButton.Text = "START"; pingStartButton.BackColor = Accent; pingStartButton.ForeColor = Color.White; pingStartButton.FlatAppearance.BorderColor = Accent;
                pingStopButton.Text = "STOP"; pingStopButton.BackColor = Surface; pingStopButton.ForeColor = Danger;
                pingStopButton.FlatAppearance.BorderColor = Color.FromArgb(252, 165, 165);
                appStatus.Text = "Ready";
            }
        }

        void SetTraceRunning(bool running)
        {
            SetTabActivity("Traceroute", running);
            traceStartButton.Enabled = !running; traceStopButton.Enabled = running;
            traceTarget.Enabled = !running; traceMaxHops.Enabled = traceTimeout.Enabled = traceInterval.Enabled = !running;
            traceContinuous.Enabled = traceResolveNames.Enabled = traceUseCustomDns.Enabled = !running; traceDnsServer.Enabled = !running && traceUseCustomDns.Checked;
            if (running)
            {
                traceStartButton.Text = "●  TRACE RUNNING"; traceStartButton.BackColor = Success; traceStartButton.ForeColor = Color.White;
                traceStopButton.Text = "■  STOP NOW"; traceStopButton.BackColor = Danger; traceStopButton.ForeColor = Color.White; traceStopButton.FlatAppearance.BorderColor = Danger;
                appStatus.Text = "Traceroute probes active";
            }
            else
            {
                traceStartButton.Text = "▶  START TRACE"; traceStartButton.BackColor = Accent; traceStartButton.ForeColor = Color.White;
                traceStopButton.Text = "■  STOP"; traceStopButton.BackColor = Surface; traceStopButton.ForeColor = Danger; traceStopButton.FlatAppearance.BorderColor = Color.FromArgb(252, 165, 165);
                appStatus.Text = "Ready";
            }
        }

        void RequestPingStop(object sender, EventArgs e)
        {
            if (pingCancellation == null) return;
            pingPaused = false;
            pingStopButton.Enabled = false; pingStopButton.Text = "STOPPING"; pingStopButton.BackColor = Warning; pingStopButton.ForeColor = Color.White;
            pingStopButton.FlatAppearance.BorderColor = Warning;
            appStatus.Text = "Stopping ping workers…";
            pingCancellation.Cancel();
        }

        void RequestTraceStop(object sender, EventArgs e)
        {
            if (traceCancellation == null) return;
            traceStopButton.Enabled = false; traceStopButton.Text = "Stopping…"; traceStopButton.BackColor = Warning; traceStopButton.ForeColor = Color.White;
            appStatus.Text = "Stopping traceroute probes…";
            var timer = new System.Windows.Forms.Timer { Interval = 200 };
            timer.Tick += delegate { timer.Stop(); timer.Dispose(); if (traceCancellation != null) traceCancellation.Cancel(); };
            timer.Start();
        }

        void ClearPing()
        {
            pingTable.Rows.Clear(); lastTargetStatus.Clear(); latestPingOrderV105.Clear(); UpdatePingMetrics();
        }

        void ResetPingHistoryForNewSession()
        {
            selectedPingHistoryHost = "";
            pingHistoryTable.Rows.Clear();
            pingHistoryDisplayTable.Rows.Clear();
            pingHistoryTitle.Text = "PING HISTORY  •  click a target row above to view";
        }

        void CopyPingRows(object sender, EventArgs e)
        {
            if (pingGrid.GetCellCount(DataGridViewElementStates.Selected) == 0) pingGrid.SelectAll();
            try { Clipboard.SetDataObject(pingGrid.GetClipboardContent()); appStatus.Text = "Ping rows copied"; } catch { }
        }

        void ExportPingCsv(object sender, EventArgs e)
        {
            ExportDataTable(pingTable, "netstuck-ping-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".csv");
        }

        void ExportPingHistory(object sender, EventArgs e)
        {
            DataView view = pingHistorySource == null ? null : pingHistorySource.List as DataView;
            DataTable visible = view == null ? pingHistoryTable : view.ToTable();
            if (visible.Rows.Count == 0) { MessageBox.Show(this, "Select a target row with ping history first.", AppName, MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            ExportDataTable(visible, "netstuck-ping-history-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".csv");
        }
        void ExportLog(object sender, EventArgs e) { ExportDataTable(logTable, "netstuck-log-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".csv"); }

        void ExportDataTable(DataTable table, string filename)
        {
            using (var dialog = new SaveFileDialog { Filter = "CSV file (*.csv)|*.csv|All files (*.*)|*.*", FileName = filename })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                var lines = new List<string>();
                lines.Add(String.Join(",", table.Columns.Cast<DataColumn>().Select(c => NetOpsCore.CsvEscape(c.ColumnName))));
                foreach (DataRow row in table.Rows) lines.Add(String.Join(",", table.Columns.Cast<DataColumn>().Select(c => NetOpsCore.CsvEscape(row[c]))));
                File.WriteAllLines(dialog.FileName, lines, new UTF8Encoding(true));
                Log("ACTION", "Export", "Saved " + dialog.FileName);
            }
        }

        void LoadProfiles()
        {
            savedProfiles.Clear();
            try
            {
                if (File.Exists(profilePath))
                {
                    ProfileCollection collection = new JavaScriptSerializer().Deserialize<ProfileCollection>(File.ReadAllText(profilePath));
                    if (collection != null && collection.Profiles != null) savedProfiles.AddRange(collection.Profiles.Where(p => p != null && !String.IsNullOrWhiteSpace(p.Name)));
                }
            }
            catch (Exception ex) { Log("WARNING", "Profiles", "Could not load saved lists: " + FriendlyError(ex)); }
            RefreshProfiles();
        }

        void SaveProfiles()
        {
            string directory = Path.GetDirectoryName(profilePath);
            Directory.CreateDirectory(directory);
            File.WriteAllText(profilePath, new JavaScriptSerializer().Serialize(new ProfileCollection { Profiles = savedProfiles }), new UTF8Encoding(false));
        }

        void RefreshProfiles()
        {
            string selected = profileCombo.SelectedItem == null ? "" : profileCombo.SelectedItem.ToString();
            profileCombo.Items.Clear();
            foreach (SavedProfile profile in savedProfiles.OrderBy(p => p.Name)) profileCombo.Items.Add(profile.Name);
            if (profileCombo.Items.Count > 0)
            {
                int index = profileCombo.Items.IndexOf(selected);
                profileCombo.SelectedIndex = index >= 0 ? index : 0;
            }
            profileInfo.Text = savedProfiles.Count + " saved list" + (savedProfiles.Count == 1 ? "" : "s") + " • stored locally";
        }

        void SaveCurrentProfile(object sender, EventArgs e)
        {
            if (pingCancellation != null) return;
            try { if (NetOpsCore.ExpandTargets(targetInput.Text, MaxExpandedTargets).Count == 0) throw new InvalidOperationException("The target list is empty."); }
            catch (Exception ex) { MessageBox.Show(this, FriendlyError(ex), AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            string suggested = profileCombo.SelectedItem == null ? "" : profileCombo.SelectedItem.ToString();
            string name = PromptForName("Save target list", "Profile name", suggested);
            if (String.IsNullOrWhiteSpace(name)) return;
            SavedProfile existing = savedProfiles.FirstOrDefault(p => String.Equals(p.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));
            if (existing != null && MessageBox.Show(this, "Replace saved list \"" + existing.Name + "\"?", AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            if (existing == null) { existing = new SavedProfile(); savedProfiles.Add(existing); }
            existing.Name = name.Trim(); existing.Targets = targetInput.Text; existing.IntervalMs = (int)pingInterval.Value; existing.TimeoutMs = (int)pingTimeout.Value; existing.UseCustomDns = pingUseCustomDns.Checked; existing.CustomDns = pingDnsServer.Text;
            try { SaveProfiles(); RefreshProfiles(); profileCombo.SelectedItem = existing.Name; Log("ACTION", "Profiles", "Saved list: " + existing.Name); }
            catch (Exception ex) { MessageBox.Show(this, "Unable to save list.\r\n\r\n" + FriendlyError(ex), AppName, MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        void LoadSelectedProfile(object sender, EventArgs e)
        {
            if (pingCancellation != null || profileCombo.SelectedItem == null) return;
            SavedProfile profile = savedProfiles.FirstOrDefault(p => String.Equals(p.Name, profileCombo.SelectedItem.ToString(), StringComparison.OrdinalIgnoreCase));
            if (profile == null) return;
            targetInput.Text = profile.Targets ?? ""; pingInterval.Value = Clamp(profile.IntervalMs, pingInterval.Minimum, pingInterval.Maximum); pingTimeout.Value = Clamp(profile.TimeoutMs, pingTimeout.Minimum, pingTimeout.Maximum);
            pingUseCustomDns.Checked = profile.UseCustomDns; pingDnsServer.Text = profile.CustomDns ?? "";
            profileInfo.Text = "Loaded: " + profile.Name; Log("ACTION", "Profiles", "Loaded list: " + profile.Name);
        }

        void DeleteSelectedProfile(object sender, EventArgs e)
        {
            if (pingCancellation != null || profileCombo.SelectedItem == null) return;
            string name = profileCombo.SelectedItem.ToString();
            if (MessageBox.Show(this, "Delete saved list \"" + name + "\"?", AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            savedProfiles.RemoveAll(p => String.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
            try { SaveProfiles(); RefreshProfiles(); Log("ACTION", "Profiles", "Deleted list: " + name); }
            catch (Exception ex) { MessageBox.Show(this, FriendlyError(ex), AppName, MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        void Log(string level, string source, string message)
        {
            if (appClosing || IsDisposed || Disposing) return;
            if (InvokeRequired) { try { BeginInvoke((Action)(() => Log(level, source, message))); } catch { } return; }
            if (logTable == null) return;
            logTable.Rows.Add(DateTime.Now, level, source, message);
            if (logTable.Rows.Count > 10000) logTable.Rows.RemoveAt(0);
        }

        void GridCopyShortcut(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.C)
            {
                try { Clipboard.SetDataObject(((DataGridView)sender).GetClipboardContent()); e.Handled = true; } catch { }
            }
        }

        void FormatPingCell(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0) return;
            string property = pingGrid.Columns[e.ColumnIndex].DataPropertyName;
            if ((property == "LastMs" || property == "AvgMs" || property == "MinMs" || property == "MaxMs") && e.Value != null && e.Value != DBNull.Value) e.Value = FormatMsValue(e.Value);
            if (property == "LossPct" && e.Value != null && e.Value != DBNull.Value) e.Value = Convert.ToDouble(e.Value).ToString("0.00") + "%";
            if ((property == "LastPing" || property == "LastSuccess" || property == "ReachableSince" || property == "UnreachableSince") && e.Value != null && e.Value != DBNull.Value) e.Value = Convert.ToDateTime(e.Value).ToString("yyyy-MM-dd HH:mm:ss");
            string status = Convert.ToString(pingGrid.Rows[e.RowIndex].Cells["Status"].Value);
            if (property == "Status")
            {
                e.CellStyle.Font = gridBoldFont;
                bool success = IsSuccessfulPingStatus(status);
                bool failed = IsFailedPingStatus(status);
                e.CellStyle.ForeColor = success ? Success : failed ? Danger : Warning;
                e.CellStyle.BackColor = success ? Color.FromArgb(240, 253, 244) : failed ? Color.FromArgb(254, 242, 242) : Color.FromArgb(255, 251, 235);
            }
        }

        void FormatPingHistoryCell(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0) return;
            string property = pingHistoryGrid.Columns[e.ColumnIndex].DataPropertyName;
            if (property == "Time" && e.Value != null && e.Value != DBNull.Value) e.Value = Convert.ToDateTime(e.Value).ToString("M/d/yyyy h:mm:ss.fff tt");
            if (property == "LatencyMs" && e.Value != null && e.Value != DBNull.Value) e.Value = FormatMsValue(e.Value);
            if (property == "Result")
            {
                string result = Convert.ToString(e.Value); e.CellStyle.Font = gridBoldFont;
                e.CellStyle.ForeColor = result == "Succeeded" ? Success : Danger;
            }
        }

        void FormatTraceCell(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0) return;
            DataGridView grid = (DataGridView)sender;
            string property = grid.Columns[e.ColumnIndex].DataPropertyName;
            if ((property == "LastMs" || property == "BestMs" || property == "AvgMs" || property == "WorstMs" || property == "JitterMs") && e.Value != null && e.Value != DBNull.Value) e.Value = FormatMsValue(e.Value);
            if (property == "LossPct" && e.Value != null && e.Value != DBNull.Value) e.Value = Convert.ToDouble(e.Value).ToString("0.00") + "%";
            if (property == "Status")
            {
                string status = Convert.ToString(e.Value);
                e.CellStyle.Font = gridBoldFont; e.CellStyle.ForeColor = (status == "Timeout" || status == "Unreachable") ? Danger : status == "Reached" ? Success : Accent;
            }
        }

        void FormatStatusCell(object sender, DataGridViewCellFormattingEventArgs e)
        {
            DataGridView grid = (DataGridView)sender;
            if (e.RowIndex < 0 || !grid.Columns.Cast<DataGridViewColumn>().Any(c => c.DataPropertyName == "Status")) return;
            DataGridViewColumn statusCol = grid.Columns.Cast<DataGridViewColumn>().First(c => c.DataPropertyName == "Status");
            if (e.ColumnIndex != statusCol.Index) return;
            string status = Convert.ToString(e.Value);
            e.CellStyle.Font = gridBoldFont;
            e.CellStyle.ForeColor = status == "OK" || status == "CACHED" ? Success :
                status == "SEARCHING" || status == "NOT FOUND" || status == "LOCAL" || status == "SKIPPED" || status == "RATE LIMITED" ? Warning : Danger;
        }

        void FormatLogCell(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0) return;
            string property = logGrid.Columns[e.ColumnIndex].DataPropertyName;
            if (property == "Time" && e.Value != null) e.Value = Convert.ToDateTime(e.Value).ToString("yyyy-MM-dd HH:mm:ss.fff");
            if (property == "Level")
            {
                string level = Convert.ToString(e.Value); e.CellStyle.Font = gridBoldFont;
                e.CellStyle.ForeColor = level == "ERROR" ? Danger : level == "WARNING" ? Warning : level == "ACTION" ? Accent : TextMuted;
            }
        }

        DataTable CreatePingTable()
        {
            var t = new DataTable();
            t.Columns.Add("Status"); t.Columns.Add("Host"); t.Columns.Add("Description"); t.Columns.Add("ResolvedIp");
            t.Columns.Add("LastMs", typeof(long)); t.Columns.Add("AvgMs", typeof(double)); t.Columns.Add("MinMs", typeof(long)); t.Columns.Add("MaxMs", typeof(long)); t.Columns.Add("TotalMs", typeof(double));
            t.Columns.Add("Sent", typeof(long)); t.Columns.Add("Received", typeof(long)); t.Columns.Add("Lost", typeof(long)); t.Columns.Add("LossPct", typeof(double));
            t.Columns.Add("LastPing", typeof(DateTime)); t.Columns.Add("LastSuccess", typeof(DateTime)); t.Columns.Add("ReachableSince", typeof(DateTime)); t.Columns.Add("UnreachableSince", typeof(DateTime)); t.Columns.Add("Error");
            t.PrimaryKey = new[] { t.Columns["Host"] }; return t;
        }

        DataTable CreatePingHistoryTable()
        {
            var t = new DataTable();
            t.Columns.Add("Time", typeof(DateTime)); t.Columns.Add("Host"); t.Columns.Add("ResolvedIp"); t.Columns.Add("LatencyMs", typeof(long));
            t.Columns.Add("Ttl", typeof(int)); t.Columns.Add("Result"); t.Columns.Add("Sequence", typeof(long)); t.Columns.Add("Detail");
            return t;
        }

        DataTable CreateTraceTable()
        {
            var t = new DataTable(); t.Columns.Add("Hop", typeof(int)); t.Columns.Add("Address"); t.Columns.Add("Hostname"); t.Columns.Add("Description"); t.Columns.Add("Status");
            t.Columns.Add("LastMs", typeof(double)); t.Columns.Add("BestMs", typeof(double)); t.Columns.Add("AvgMs", typeof(double)); t.Columns.Add("WorstMs", typeof(double)); t.Columns.Add("JitterMs", typeof(double));
            t.Columns.Add("Sent", typeof(int)); t.Columns.Add("Received", typeof(int)); t.Columns.Add("LossPct", typeof(double)); t.Columns.Add("RouteChanges", typeof(int)); t.Columns.Add("Updated"); t.PrimaryKey = new[] { t.Columns["Hop"] }; return t;
        }

        DataTable CreateDnsTable()
        {
            var t = new DataTable(); foreach (string name in new[] { "Query", "Description", "Type", "Status", "Answer", "Server", "Time" }) t.Columns.Add(name); return t;
        }

        TabPage NewPage(string text)
        {
            var page = new TabPage(text) { Name = "page-" + text.Replace(" ", "-").Replace("/", "-"), BackColor = Canvas, Padding = new Padding(12) };
            pagesByName[text] = page;
            tabs.TabPages.Add(page);
            return page;
        }

        Panel Card()
        {
            var panel = new Panel { Dock = DockStyle.Fill, BackColor = Surface, Padding = new Padding(12) };
            panel.Paint += delegate(object sender, PaintEventArgs e) { using (var pen = new Pen(Border)) e.Graphics.DrawRectangle(pen, 0, 0, panel.Width - 1, panel.Height - 1); };
            return panel;
        }

        Panel SectionHeader(string title, string subtitle)
        {
            var panel = new Panel { Dock = DockStyle.Top, Height = 55, BackColor = Surface };
            panel.Controls.Add(new Label { Text = title, Font = new Font("Segoe UI Semibold", 11.5f), ForeColor = TextMain, AutoSize = true, Location = new Point(0, 4) });
            panel.Controls.Add(new Label { Text = subtitle, ForeColor = TextMuted, AutoSize = true, Location = new Point(1, 29) });
            return panel;
        }

        Label MetricCard(TableLayoutPanel parent, int column, string caption, string value, Color valueColor)
        {
            var card = new Panel { Dock = DockStyle.Fill, BackColor = Surface, Margin = new Padding(column == 0 ? 0 : 5, 0, column == 4 ? 0 : 5, 0), Padding = new Padding(10, 3, 10, 3) };
            card.Paint += delegate(object sender, PaintEventArgs e) { using (var pen = new Pen(Border)) e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1); };
            var cap = new Label { Text = caption, Dock = DockStyle.Top, Height = 17, ForeColor = TextMuted, Font = new Font("Segoe UI Semibold", 8f), TextAlign = ContentAlignment.MiddleCenter };
            var val = new Label { Text = value, Dock = DockStyle.Bottom, Height = 31, ForeColor = valueColor, Font = new Font("Segoe UI Semibold", 13f), TextAlign = ContentAlignment.MiddleCenter };
            card.Controls.Add(val); card.Controls.Add(cap); parent.Controls.Add(card, column, 0); return val;
        }

        Button ActionButton(string text, bool primary, int width)
        {
            var button = new Button
            {
                Text = text, Height = 34, Width = width, FlatStyle = FlatStyle.Flat,
                BackColor = primary ? Accent : Surface, ForeColor = primary ? Color.White : TextMain,
                Font = new Font("Segoe UI Semibold", 9f), Cursor = Cursors.Hand, Margin = new Padding(4, 1, 4, 1),
                TextAlign = ContentAlignment.MiddleCenter, Padding = new Padding(0), AutoEllipsis = true,
                UseCompatibleTextRendering = false
            };
            button.FlatAppearance.BorderColor = primary ? Accent : Border; button.FlatAppearance.BorderSize = 1; return button;
        }

        Panel CompactActionBar(Button action)
        {
            var bar = new Panel { Dock = DockStyle.Top, Height = 48, BackColor = Surface, Padding = new Padding(0, 7, 0, 7) };
            action.Dock = DockStyle.Right;
            bar.Controls.Add(action);
            return bar;
        }

        Button DangerButton(string text, int width)
        {
            var button = ActionButton(text, false, width); button.ForeColor = Danger; button.FlatAppearance.BorderColor = Color.FromArgb(252, 165, 165); return button;
        }

        Label FieldLabel(string text) { return new Label { Text = text, AutoSize = true, ForeColor = TextMuted, Anchor = AnchorStyles.Left, Margin = new Padding(0, 5, 4, 3) }; }

        Control LabeledField(string label, Control input)
        {
            var field = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = new Padding(0, 0, 8, 0) };
            field.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
            field.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            input.Dock = DockStyle.Fill;
            field.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, ForeColor = TextMuted, TextAlign = ContentAlignment.BottomLeft }, 0, 0);
            field.Controls.Add(input, 0, 1);
            return field;
        }

        NumericUpDown NumberField(decimal min, decimal max, decimal value, decimal increment)
        {
            return new NumericUpDown { Minimum = min, Maximum = max, Value = value, Increment = increment, Dock = DockStyle.Fill, ThousandsSeparator = false, BorderStyle = BorderStyle.FixedSingle };
        }

        DataGridView DataGrid()
        {
            var grid = new DataGridView
            {
                Dock = DockStyle.Fill, BackgroundColor = Surface, BorderStyle = BorderStyle.FixedSingle, GridColor = Border,
                AllowUserToAddRows = false, AllowUserToDeleteRows = false, AllowUserToOrderColumns = true, AllowUserToResizeColumns = true, AllowUserToResizeRows = false,
                ReadOnly = false, EditMode = DataGridViewEditMode.EditOnEnter, RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.CellSelect, MultiSelect = true,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None, AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None, ScrollBars = ScrollBars.Both,
                EnableHeadersVisualStyles = false, ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableAlwaysIncludeHeaderText, ColumnHeadersHeight = 36, ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing
            };
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 252); grid.ColumnHeadersDefaultCellStyle.ForeColor = TextMain; grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 9f); grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(248, 250, 252);
            grid.DefaultCellStyle.BackColor = Surface; grid.DefaultCellStyle.ForeColor = TextMain; grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(219, 234, 254); grid.DefaultCellStyle.SelectionForeColor = TextMain; grid.DefaultCellStyle.Padding = new Padding(4, 1, 4, 1);
            grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(250, 251, 253); grid.RowTemplate.Height = 32;
            grid.EditingControlShowing += MakeGridEditorSelectable;
            ConfigureFastGrid(grid);
            return grid;
        }

        void AddGridColumn(DataGridView grid, string header, string property, int width)
        {
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = property, HeaderText = header, DataPropertyName = property, ReadOnly = false, Width = width, MinimumWidth = 45, SortMode = DataGridViewColumnSortMode.Automatic, Resizable = DataGridViewTriState.True });
        }

        void ConfigureSplit(TabPage page, SplitContainer split, int desired, int panel1Min, int panel2Min)
        {
            bool fullyConfigured = false;
            LayoutEventHandler handler = null;
            handler = delegate
            {
                int available = split.ClientSize.Width;
                if (fullyConfigured || available <= split.SplitterWidth) return;

                int usable = available - split.SplitterWidth;
                int effectiveLeftMin = Math.Min(panel1Min, usable);
                int effectiveRightMin = Math.Min(panel2Min, Math.Max(0, usable - effectiveLeftMin));
                int maximumDistance = Math.Max(0, usable - effectiveRightMin);
                int distance = Math.Max(effectiveLeftMin, Math.Min(desired, maximumDistance));

                // During startup a hidden tab or a constrained desktop can be
                // narrower than both requested panes. Keep the input pane usable
                // and retry on a later layout instead of leaving the default 25% split.
                split.Panel1MinSize = 0;
                split.Panel2MinSize = 0;
                split.SplitterDistance = Math.Max(0, Math.Min(usable, distance));
                split.Panel1MinSize = Math.Min(panel1Min, split.SplitterDistance);
                split.Panel2MinSize = Math.Min(panel2Min, Math.Max(0, usable - split.SplitterDistance));

                fullyConfigured = available >= panel1Min + panel2Min + split.SplitterWidth;
                if (fullyConfigured) page.Layout -= handler;
            };
            page.Layout += handler;
        }

        void ConfigureHorizontalSplit(Control host, SplitContainer split, int desired, int panel1Min, int panel2Min)
        {
            bool configured = false;
            LayoutEventHandler handler = null;
            handler = delegate
            {
                if (configured || split.ClientSize.Height < panel1Min + panel2Min + split.SplitterWidth) return;
                int distance = Math.Min(desired, split.ClientSize.Height - panel2Min - split.SplitterWidth);
                split.SplitterDistance = Math.Max(panel1Min, distance);
                split.Panel1MinSize = panel1Min; split.Panel2MinSize = panel2Min;
                configured = true; host.Layout -= handler;
            };
            host.Layout += handler;
        }

        string PromptForName(string title, string label, string initial)
        {
            using (var dialog = new Form { Text = title, Width = 430, Height = 180, StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false, ShowInTaskbar = false, BackColor = Canvas, Font = Font })
            {
                var prompt = new Label { Text = label, AutoSize = true, Location = new Point(20, 18), ForeColor = TextMain };
                var input = new TextBox { Text = initial ?? "", Location = new Point(20, 45), Width = 374 };
                var ok = ActionButton("Save", true, 100); ok.Location = new Point(186, 88); ok.DialogResult = DialogResult.OK;
                var cancel = ActionButton("Cancel", false, 100); cancel.Location = new Point(294, 88); cancel.DialogResult = DialogResult.Cancel;
                dialog.Controls.AddRange(new Control[] { prompt, input, ok, cancel }); dialog.AcceptButton = ok; dialog.CancelButton = cancel;
                dialog.Shown += delegate { input.Focus(); input.SelectAll(); };
                return dialog.ShowDialog(this) == DialogResult.OK ? input.Text.Trim() : null;
            }
        }

        void ApplyTheme(Control root)
        {
            foreach (Control control in root.Controls)
            {
                if (control is TextBox)
                {
                    control.BackColor = ((TextBox)control).ReadOnly ? Color.FromArgb(248, 250, 252) : Color.White; control.ForeColor = TextMain;
                }
                else if (control is ComboBox || control is NumericUpDown) { control.BackColor = Color.White; control.ForeColor = TextMain; }
                ApplyTheme(control);
            }
        }

        void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            appClosing = true;
            SaveAppState();
            if (pingCancellation != null) pingCancellation.Cancel();
            if (traceCancellation != null) traceCancellation.Cancel();
            StopV103Sessions();
            if (dnsCancellation != null) dnsCancellation.Cancel();
            if (collectorCancellation != null) collectorCancellation.Cancel();
            if (collectorTerminalTimerV120 != null) { collectorTerminalTimerV120.Stop(); collectorTerminalTimerV120.Dispose(); }
            StopTraceLookupCacheV120();
            if (pingUiTimer != null) { pingUiTimer.Stop(); pingUiTimer.Dispose(); }
            if (gridBoldFont != null) gridBoldFont.Dispose();
        }

        static WebClient NewWebClient()
        {
            var web = new WebClient(); web.Headers[HttpRequestHeader.UserAgent] = AppName + "/" + AppVersion; web.Encoding = Encoding.UTF8; return web;
        }

        static bool IsValidIp(string value) { IPAddress address; return IPAddress.TryParse(value, out address); }
        static string Value(Dictionary<string, object> data, string key) { return data != null && data.ContainsKey(key) && data[key] != null ? Convert.ToString(data[key], CultureInfo.InvariantCulture) : ""; }
        static decimal Clamp(decimal value, decimal min, decimal max) { return Math.Max(min, Math.Min(max, value)); }
        static string EscapeFilter(string value) { return value.Replace("'", "''").Replace("[", "[[]").Replace("%", "[%]").Replace("*", "[*]"); }
        static string FriendlyError(Exception ex)
        {
            if (ex is AggregateException) ex = ((AggregateException)ex).GetBaseException();
            var web = ex as WebException;
            if (web != null && web.Response is HttpWebResponse) return ((HttpWebResponse)web.Response).StatusCode + " — " + web.Message;
            return ex.Message;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);
        static void Cue(TextBox box, string text)
        {
            box.HandleCreated += delegate { SendMessage(box.Handle, 0x1501, (IntPtr)1, text); };
            if (box.IsHandleCreated) SendMessage(box.Handle, 0x1501, (IntPtr)1, text);
        }
    }

    static class Program
    {
        [STAThread]
        static void Main()
        {
            ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
