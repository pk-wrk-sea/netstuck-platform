using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace NetStuck
{
    sealed class SourceAddressOption
    {
        public string Address = "";
        public string Label = "Automatic (Windows routing)";
        public override string ToString() { return Label; }
    }

    sealed class NetworkIdentityCacheV103
    {
        public string PublicIp { get; set; }
        public DateTime RetrievedUtc { get; set; }
    }

    sealed class ServiceProbeResult
    {
        public bool Reachable;
        public bool Indeterminate;
        public double LatencyMs = -1;
        public int Ttl;
        public string SourceIp = "";
        public string Detail = "";
    }

    sealed class PingResolutionCacheV105
    {
        public readonly SemaphoreSlim Gate = new SemaphoreSlim(1, 1);
        public IPAddress Destination;
        public DateTime RefreshAfterUtc = DateTime.MinValue;
    }

    sealed class TraceCycleResultV105
    {
        public int Cycle;
        public HopProbe[] Probes;
        public ServiceProbeResult Service;
    }

    sealed class TraceGridPositionV110
    {
        public int FirstDisplayedRow = -1;
        public int HorizontalOffset;
        public int CurrentHop = -1;
        public string CurrentColumn = "";
    }

    sealed class TraceLookupCacheItemV120
    {
        public string Value { get; set; }
        public DateTime RetrievedUtc { get; set; }
    }

    sealed class TraceLookupCacheV120
    {
        public Dictionary<string, TraceLookupCacheItemV120> Providers { get; set; }
        public Dictionary<string, TraceLookupCacheItemV120> Dns { get; set; }
    }

    sealed class TraceSessionV103
    {
        public int Number;
        public TabPage Page;
        public ComboBox Target;
        public ComboBox Protocol;
        public NumericUpDown Port;
        public NumericUpDown PacketSize;
        public NumericUpDown MaxHops;
        public NumericUpDown Timeout;
        public NumericUpDown Interval;
        public Button Start;
        public Button Pause;
        public Button Stop;
        public Button HopDescriptions;
        public Label Cycle;
        public Label Destination;
        public Label State;
        public DataGridView Grid;
        public DataTable Table;
        public BindingSource Source;
        public DataGridView EventGrid;
        public DataTable EventTable;
        public BindingSource EventSource;
        public ComboBox EventFilter;
        public CancellationTokenSource Cancellation;
        public bool Paused;
        public int CycleNumber;
        public int LastAppliedCycle;
        public readonly Dictionary<int, HopStats> Stats = new Dictionary<int, HopStats>();
        public readonly Dictionary<int, int> LatestCycleByHop = new Dictionary<int, int>();
        public readonly Dictionary<int, string> LastResolvedNames = new Dictionary<int, string>();
        public string LastState = "";
        public int KnownDestinationHop;
        public int LastFullProbeCycle;
        public int ForceFullProbeCycles;
    }

    public sealed partial class MainForm
    {
        readonly ToolStripStatusLabel localIpStatus = new ToolStripStatusLabel("My Local IP: detecting...")
        {
            AutoSize = false, Width = 180, TextAlign = ContentAlignment.MiddleLeft, Overflow = ToolStripItemOverflow.Never,
            Margin = new Padding(0, 0, 2, 0), Padding = new Padding(4, 0, 4, 0)
        };
        readonly ToolStripStatusLabel publicIpStatus = new ToolStripStatusLabel("My Public IP: detecting...")
        {
            AutoSize = false, Width = 190, TextAlign = ContentAlignment.MiddleLeft, Overflow = ToolStripItemOverflow.Never,
            Margin = new Padding(0, 0, 4, 0), Padding = new Padding(4, 0, 4, 0)
        };

        ComboBox pingSourceIp;
        ComboBox pingProtocol;
        NumericUpDown pingPort;
        NumericUpDown pingPacketSize;
        Button pingPauseButton;
        volatile bool pingPaused;

        readonly List<TraceSessionV103> traceSessionsV103 = new List<TraceSessionV103>();
        TabControl traceSessionTabs;
        string networkIdentityCachePathV103;
        readonly Dictionary<string, string> traceProviderCacheV104 = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        readonly HashSet<string> traceProviderPendingV104 = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        readonly SemaphoreSlim traceProviderGateV104 = new SemaphoreSlim(2, 2);
        readonly Dictionary<string, TraceLookupCacheItemV120> traceProviderEntriesV120 = new Dictionary<string, TraceLookupCacheItemV120>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, TraceLookupCacheItemV120> traceDnsEntriesV120 = new Dictionary<string, TraceLookupCacheItemV120>(StringComparer.OrdinalIgnoreCase);
        string traceLookupCachePathV120;
        readonly object traceLookupCacheLockV120 = new object();
        System.Threading.Timer traceLookupSaveTimerV120;

        void InitializeTraceLookupCacheV120()
        {
            string directory = Path.GetDirectoryName(statePath);
            if (String.IsNullOrWhiteSpace(directory)) directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppName);
            traceLookupCachePathV120 = Path.Combine(directory, "trace-lookups.json");
            try
            {
                if (!File.Exists(traceLookupCachePathV120)) return;
                TraceLookupCacheV120 saved = new JavaScriptSerializer().Deserialize<TraceLookupCacheV120>(File.ReadAllText(traceLookupCachePathV120, Encoding.UTF8));
                if (saved == null) return;
                if (saved.Providers != null)
                    foreach (KeyValuePair<string, TraceLookupCacheItemV120> item in saved.Providers) traceProviderEntriesV120[item.Key] = item.Value;
                if (saved.Dns != null)
                    foreach (KeyValuePair<string, TraceLookupCacheItemV120> item in saved.Dns) traceDnsEntriesV120[item.Key] = item.Value;
                foreach (KeyValuePair<string, TraceLookupCacheItemV120> item in traceProviderEntriesV120)
                    if (IsTraceLookupFreshV120(item.Value, ProviderCacheTtlV120(item.Value))) traceProviderCacheV104[item.Key] = item.Value.Value ?? "";
            }
            catch { }
        }

        static TimeSpan ProviderCacheTtlV120(TraceLookupCacheItemV120 item)
        {
            return item != null && (item.Value ?? "").IndexOf("unavailable", StringComparison.OrdinalIgnoreCase) >= 0
                ? TimeSpan.FromMinutes(30) : TimeSpan.FromHours(24);
        }

        static bool IsTraceLookupFreshV120(TraceLookupCacheItemV120 item, TimeSpan ttl)
        {
            return item != null && !String.IsNullOrWhiteSpace(item.Value) && item.RetrievedUtc > DateTime.UtcNow.Subtract(ttl);
        }

        void ScheduleTraceLookupCacheSaveV120()
        {
            lock (traceLookupCacheLockV120)
            {
                if (traceLookupSaveTimerV120 == null)
                    traceLookupSaveTimerV120 = new System.Threading.Timer(delegate { SaveTraceLookupCacheV120(); }, null, 1000, Timeout.Infinite);
                else traceLookupSaveTimerV120.Change(1000, Timeout.Infinite);
            }
        }

        void SaveTraceLookupCacheV120()
        {
            try
            {
                TraceLookupCacheV120 snapshot;
                lock (traceLookupCacheLockV120)
                {
                    snapshot = new TraceLookupCacheV120
                    {
                        Providers = traceProviderEntriesV120.OrderByDescending(item => item.Value == null ? DateTime.MinValue : item.Value.RetrievedUtc)
                            .Take(1000).ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase),
                        Dns = traceDnsEntriesV120.OrderByDescending(item => item.Value == null ? DateTime.MinValue : item.Value.RetrievedUtc)
                            .Take(1000).ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase)
                    };
                }
                Directory.CreateDirectory(Path.GetDirectoryName(traceLookupCachePathV120));
                string temporary = traceLookupCachePathV120 + ".tmp";
                File.WriteAllText(temporary, new JavaScriptSerializer().Serialize(snapshot), new UTF8Encoding(false));
                if (File.Exists(traceLookupCachePathV120)) File.Delete(traceLookupCachePathV120);
                File.Move(temporary, traceLookupCachePathV120);
            }
            catch { }
        }

        void StopTraceLookupCacheV120()
        {
            lock (traceLookupCacheLockV120)
            {
                if (traceLookupSaveTimerV120 != null) { traceLookupSaveTimerV120.Dispose(); traceLookupSaveTimerV120 = null; }
            }
            SaveTraceLookupCacheV120();
        }

        void BuildPingPage()
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
            metricPings = MetricCard(metrics, 3, "TOTAL PROBES", "0", Accent);
            metricLoss = MetricCard(metrics, 4, "PROBE LOSS", "0.0%", Warning);

            var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, SplitterDistance = 395, SplitterWidth = 8, FixedPanel = FixedPanel.Panel1, BackColor = Canvas };
            split.Panel1.Padding = new Padding(0, 0, 4, 0);
            split.Panel2.Padding = new Padding(4, 0, 0, 0);

            var inputCard = Card();
            var inputHeader = SectionHeader("Targets + saved lists", "Host, IP or CIDR - description is optional");
            inputHeader.Dock = DockStyle.Top;
            targetInput = new TextBox { Multiline = true, AcceptsReturn = true, WordWrap = false, ScrollBars = ScrollBars.Both, Dock = DockStyle.Fill, Font = new Font("Consolas", 10f), BorderStyle = BorderStyle.FixedSingle };
            targetInput.Text = "8.8.8.8 Google DNS\r\n1.1.1.1 Cloudflare DNS\r\n10.100.10.0/24 Branch network";
            var targetHint = new Label { Text = "Examples: 10.10.10.1 SW-01  |  10.100.10.0/24", Dock = DockStyle.Bottom, Height = 27, ForeColor = TextMuted, Padding = new Padding(0, 6, 0, 0) };

            var profilePanel = new TableLayoutPanel { Dock = DockStyle.Top, Height = 106, ColumnCount = 3, RowCount = 3, Padding = new Padding(0, 4, 0, 8) };
            profilePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333f));
            profilePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333f));
            profilePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.334f));
            profilePanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 31));
            profilePanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            profilePanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 27));
            profileCombo = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            var loadProfile = ActionButton("Load", false, 104); loadProfile.Anchor = AnchorStyles.None; loadProfile.Click += LoadSelectedProfile;
            var saveProfile = ActionButton("Save list", true, 104); saveProfile.Anchor = AnchorStyles.None; saveProfile.Click += SaveCurrentProfile;
            var deleteProfile = DangerButton("Delete", 104); deleteProfile.Anchor = AnchorStyles.None; deleteProfile.Click += DeleteSelectedProfile;
            profileInfo = new Label { Text = "Profiles are saved locally on this PC", Dock = DockStyle.Fill, ForeColor = TextMuted, TextAlign = ContentAlignment.MiddleLeft };
            profilePanel.Controls.Add(profileCombo, 0, 0); profilePanel.SetColumnSpan(profileCombo, 3);
            profilePanel.Controls.Add(loadProfile, 0, 1); profilePanel.Controls.Add(saveProfile, 1, 1); profilePanel.Controls.Add(deleteProfile, 2, 1);
            profilePanel.Controls.Add(profileInfo, 0, 2); profilePanel.SetColumnSpan(profileInfo, 3);

            var settings = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 286, ColumnCount = 2, RowCount = 8, Padding = new Padding(0, 8, 0, 0) };
            settings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
            settings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
            foreach (int height in new[] { 31, 31, 31, 34, 31, 31, 38, 45 }) settings.RowStyles.Add(new RowStyle(SizeType.Absolute, height));

            pingInterval = NumberField(250, 60000, 1000, 250);
            pingTimeout = NumberField(100, 30000, 1500, 100);
            pingPacketSize = NumberField(0, 65500, 32, 8);
            pingSourceIp = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            PopulateSourceAddresses(pingSourceIp);
            var sourceTip = new ToolTip(); sourceTip.SetToolTip(pingSourceIp, "TCP binds the selected source IP. ICMP uses the source selected by Windows routing.");
            settings.Controls.Add(FieldLabel("Interval (ms)"), 0, 0); settings.Controls.Add(pingInterval, 1, 0);
            settings.Controls.Add(FieldLabel("Timeout (ms)"), 0, 1); settings.Controls.Add(pingTimeout, 1, 1);
            settings.Controls.Add(FieldLabel("Packet size (bytes)"), 0, 2); settings.Controls.Add(pingPacketSize, 1, 2);
            settings.Controls.Add(FieldLabel("Source IP"), 0, 3); settings.Controls.Add(pingSourceIp, 1, 3);

            pingProtocol = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            pingProtocol.Items.AddRange(new object[] { "ICMP", "TCP" }); pingProtocol.SelectedIndex = 0;
            pingPort = NumberField(1, 65535, 443, 1); pingPort.Enabled = false;
            pingProtocol.SelectedIndexChanged += delegate { UpdatePingAdvancedState(); };
            settings.Controls.Add(FieldLabel("Probe protocol"), 0, 4); settings.Controls.Add(pingProtocol, 1, 4);
            settings.Controls.Add(FieldLabel("TCP custom port"), 0, 5); settings.Controls.Add(pingPort, 1, 5);

            pingUseCustomDns = new CheckBox { Text = "Use custom DNS", AutoSize = true, Anchor = AnchorStyles.Left };
            pingDnsServer = new TextBox { Text = "8.8.8.8", Dock = DockStyle.Fill, Enabled = false };
            pingUseCustomDns.CheckedChanged += delegate { pingDnsServer.Enabled = pingUseCustomDns.Checked && pingCancellation == null; };
            var dnsLine = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
            dnsLine.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 135)); dnsLine.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            dnsLine.Controls.Add(pingUseCustomDns, 0, 0); dnsLine.Controls.Add(pingDnsServer, 1, 0);
            settings.Controls.Add(dnsLine, 0, 6); settings.SetColumnSpan(dnsLine, 2);

            var actionBar = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Padding = new Padding(0, 4, 0, 2) };
            pingStartButton = ActionButton("START", true, 104);
            pingPauseButton = ActionButton("PAUSE", false, 104); pingPauseButton.Enabled = false; pingPauseButton.ForeColor = TextMain;
            pingStopButton = DangerButton("STOP", 104); pingStopButton.Enabled = false;
            pingStartButton.Click += StartPing; pingPauseButton.Click += TogglePingPause; pingStopButton.Click += RequestPingStop;
            actionBar.Controls.AddRange(new Control[] { pingStartButton, pingPauseButton, pingStopButton });
            settings.Controls.Add(actionBar, 0, 7); settings.SetColumnSpan(actionBar, 2);

            inputCard.Controls.Add(targetInput);
            inputCard.Controls.Add(targetHint);
            inputCard.Controls.Add(settings);
            inputCard.Controls.Add(profilePanel);
            inputCard.Controls.Add(inputHeader);
            split.Panel1.Controls.Add(inputCard);

            var resultCard = Card();
            var toolbar = new TableLayoutPanel { Dock = DockStyle.Top, Height = 91, ColumnCount = 1, RowCount = 2, Padding = new Padding(0, 5, 0, 5) };
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            toolbar.RowStyles.Add(new RowStyle(SizeType.Absolute, 38)); toolbar.RowStyles.Add(new RowStyle(SizeType.Absolute, 43));
            var searchLine = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = new Padding(0) };
            searchLine.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); searchLine.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
            var toolbarActions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, Margin = new Padding(0), Padding = new Padding(0, 4, 0, 0) };
            pingSearch = new TextBox { Dock = DockStyle.Fill };
            Cue(pingSearch, "Filter host, description, IP, status or error");
            pingSearch.TextChanged += delegate { ApplyPingFilter(); };
            pingStatusFilter = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
            pingStatusFilter.Items.AddRange(new object[] { "All status", "ICMP OK", "Connected", "Unreachable", "TCP Timeout", "Waiting" }); pingStatusFilter.SelectedIndex = 0;
            pingStatusFilter.SelectedIndexChanged += delegate { ApplyPingFilter(); };
            var copy = ActionButton("Copy", false, 104); var export = ActionButton("Export CSV", false, 104); var columns = ActionButton("Columns", false, 104);
            var cards = ActionButton("Hide cards", false, 104); var clear = ActionButton("Clear", false, 104);
            copy.Click += CopyPingRows; export.Click += ExportPingCsv; clear.Click += delegate { if (pingCancellation == null) { ClearPing(); ResetPingHistoryForNewSession(); } };
            columns.Click += ShowPingColumnChooser; cards.Click += delegate { TogglePingCards(cards); };
            searchLine.Controls.Add(pingSearch, 0, 0); searchLine.Controls.Add(pingStatusFilter, 1, 0);
            toolbarActions.Controls.AddRange(new Control[] { clear, cards, columns, export, copy });
            toolbar.Controls.Add(searchLine, 0, 0); toolbar.Controls.Add(toolbarActions, 0, 1);

            pingTable = CreatePingTableV103();
            pingSource = new BindingSource { DataSource = pingTable };
            pingGrid = DataGrid(); pingGrid.AutoGenerateColumns = false; pingGrid.ReadOnly = true; pingGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            AddGridColumn(pingGrid, "Seq.", "Seq", 55);
            AddGridColumn(pingGrid, "Status", "Status", 108);
            AddGridColumn(pingGrid, "Host", "Host", 145);
            AddGridColumn(pingGrid, "Description", "Description", 165);
            AddGridColumn(pingGrid, "Resolved IP", "ResolvedIp", 125);
            AddGridColumn(pingGrid, "Source IP", "SourceIp", 125);
            AddGridColumn(pingGrid, "Protocol", "Protocol", 75);
            AddGridColumn(pingGrid, "Port", "Port", 65);
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
            AddGridColumn(pingGrid, "Result / error", "Error", 280);
            pingGrid.DataSource = pingSource; pingGrid.CellFormatting += FormatPingCell; pingGrid.KeyDown += GridCopyShortcut; pingGrid.CellClick += SelectPingHistory;

            pingHistoryTable = CreatePingHistoryTable(); pingHistoryDisplayTable = CreatePingHistoryTable();
            pingHistorySource = new BindingSource { DataSource = pingHistoryDisplayTable };
            pingHistoryGrid = DataGrid(); pingHistoryGrid.AutoGenerateColumns = false;
            AddGridColumn(pingHistoryGrid, "Timestamp", "Time", 178); AddGridColumn(pingHistoryGrid, "Host", "Host", 145); AddGridColumn(pingHistoryGrid, "Resolved IP", "ResolvedIp", 130);
            AddGridColumn(pingHistoryGrid, "Latency", "LatencyMs", 90); AddGridColumn(pingHistoryGrid, "TTL", "Ttl", 65); AddGridColumn(pingHistoryGrid, "Result", "Result", 105);
            AddGridColumn(pingHistoryGrid, "Sequence", "Sequence", 78); AddGridColumn(pingHistoryGrid, "Detail", "Detail", 310);
            pingHistoryGrid.DataSource = pingHistorySource; pingHistoryGrid.CellFormatting += FormatPingHistoryCell; pingHistoryGrid.KeyDown += GridCopyShortcut;
            var historyBar = new TableLayoutPanel { Dock = DockStyle.Top, Height = 42, ColumnCount = 2, Padding = new Padding(2, 4, 2, 4) };
            historyBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); historyBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));
            pingHistoryTitle = new Label { Text = "PING HISTORY  |  click a target row above to view", Dock = DockStyle.Fill, ForeColor = TextMuted, Font = new Font("Segoe UI Semibold", 8.5f), TextAlign = ContentAlignment.MiddleLeft };
            historyBar.Controls.Add(pingHistoryTitle, 0, 0);
            var exportHistory = ActionButton("Export log", false, 104); exportHistory.Anchor = AnchorStyles.None; exportHistory.Click += ExportPingHistory; historyBar.Controls.Add(exportHistory, 1, 0);
            pingResultSplit = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterWidth = 8, BackColor = Canvas };
            pingResultSplit.Panel1.Controls.Add(pingGrid); pingResultSplit.Panel2.Controls.Add(pingHistoryGrid); pingResultSplit.Panel2.Controls.Add(historyBar);
            resultCard.Controls.Add(pingResultSplit); resultCard.Controls.Add(toolbar); resultCard.Controls.Add(SectionHeader("Realtime results", "Click once to highlight a full row; sort, filter, reorder and resize columns"));
            split.Panel2.Controls.Add(resultCard);
            pingRoot.Controls.Add(metrics, 0, 0); pingRoot.Controls.Add(split, 0, 1); page.Controls.Add(pingRoot);
            ConfigureSplit(page, split, 395, 340, 600); ConfigureHorizontalSplit(resultCard, pingResultSplit, 330, 220, 190);
        }

        void BuildTracePage()
        {
            var page = NewPage("Traceroute");
            traceSessionTabs = new TabControl { Dock = DockStyle.Fill, Font = new Font("Segoe UI Semibold", 9f), Padding = new Point(16, 6) };
            traceSessionsV103.Clear();
            traceSessionsV103.Add(CreateTraceSession(1));
            traceSessionsV103.Add(CreateTraceSession(2));
            page.Controls.Add(traceSessionTabs);

            TraceSessionV103 primary = traceSessionsV103[0];
            traceTarget = primary.Target; traceMaxHops = primary.MaxHops; traceTimeout = primary.Timeout; traceInterval = primary.Interval;
            traceStartButton = primary.Start; traceStopButton = primary.Stop; traceGrid = primary.Grid; traceTable = primary.Table;
            traceCycleLabel = primary.Cycle; traceDestinationLabel = primary.Destination; traceStateLabel = primary.State;
            traceContinuous = new CheckBox { Checked = true }; traceResolveNames = new CheckBox { Checked = true };
            traceUseCustomDns = new CheckBox { Checked = false }; traceDnsServer = new TextBox { Text = "" };
        }

        TraceSessionV103 CreateTraceSession(int number)
        {
            var session = new TraceSessionV103 { Number = number };
            session.Page = new TabPage("Session " + number) { BackColor = Canvas, Padding = new Padding(8) };
            traceSessionTabs.TabPages.Add(session.Page);
            var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, SplitterWidth = 8, BackColor = Canvas, FixedPanel = FixedPanel.None };
            split.Panel1.Padding = new Padding(0, 0, 4, 0); split.Panel2.Padding = new Padding(4, 0, 0, 0);
            session.Page.Controls.Add(split);
            ConfigureSplit(session.Page, split, 920, 690, 285);

            var resultCard = Card();
            var controls = new TableLayoutPanel
            {
                Dock = DockStyle.Top, Height = 184, ColumnCount = 1, RowCount = 3,
                Padding = new Padding(12, 8, 12, 8), BackColor = Color.FromArgb(248, 250, 252),
                Margin = new Padding(0), Tag = "TraceControlPanelV123"
            };
            controls.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            controls.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
            controls.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
            controls.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            controls.Paint += delegate(object sender, PaintEventArgs e)
            {
                using (var pen = new Pen(Color.FromArgb(203, 213, 225)))
                    e.Graphics.DrawRectangle(pen, 0, 0, Math.Max(0, controls.Width - 1), Math.Max(0, controls.Height - 1));
            };
            var primaryFields = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1,
                Margin = new Padding(0), Padding = new Padding(0), GrowStyle = TableLayoutPanelGrowStyle.FixedSize,
                Tag = "TracePrimaryInputGridV123"
            };
            primaryFields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46));
            primaryFields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 18));
            primaryFields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 18));
            primaryFields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 18));
            primaryFields.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var serviceFields = new TableLayoutPanel
            {
                Dock = DockStyle.Left, Width = 360, ColumnCount = 3, RowCount = 1,
                Margin = new Padding(0), Padding = new Padding(0), GrowStyle = TableLayoutPanelGrowStyle.FixedSize,
                Tag = "TraceServiceInputGridV123"
            };
            serviceFields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            serviceFields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            serviceFields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            serviceFields.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var actionFields = new FlowLayoutPanel { Dock = DockStyle.Right, Width = 336, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = new Padding(0), Padding = new Padding(0, 7, 0, 5) };
            session.Target = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDown, FlatStyle = FlatStyle.Standard, Text = number == 1 ? "8.8.8.8" : "1.1.1.1" };
            session.Protocol = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Standard, DropDownWidth = 104 };
            session.Protocol.Items.AddRange(new object[] { "ICMP", "TCP", "UDP" }); session.Protocol.SelectedIndex = 0;
            session.Port = NumberField(1, 65535, 443, 1); session.Port.Enabled = false;
            session.PacketSize = NumberField(0, 65500, 32, 8);
            session.MaxHops = NumberField(1, 64, 24, 1); session.Timeout = NumberField(200, 30000, 1000, 100); session.Interval = NumberField(250, 60000, 1000, 250);
            session.Protocol.SelectedIndexChanged += delegate { session.Port.Enabled = session.Cancellation == null; };
            primaryFields.Controls.Add(TraceGridFieldV123("Target", session.Target), 0, 0);
            primaryFields.Controls.Add(TraceGridFieldV123("Max hops", session.MaxHops), 1, 0);
            primaryFields.Controls.Add(TraceGridFieldV123("Timeout (ms)", session.Timeout), 2, 0);
            primaryFields.Controls.Add(TraceGridFieldV123("Interval (ms)", session.Interval, true), 3, 0);
            serviceFields.Controls.Add(TraceGridFieldV123("Protocol", session.Protocol), 0, 0);
            serviceFields.Controls.Add(TraceGridFieldV123("Port (TCP/UDP)", session.Port), 1, 0);
            serviceFields.Controls.Add(TraceGridFieldV123("Packet size", session.PacketSize, true), 2, 0);
            session.Start = ActionButton("START", true, 104); session.Start.Margin = new Padding(0, 1, 8, 1);
            session.Pause = ActionButton("PAUSE", false, 104); session.Pause.Enabled = false; session.Pause.ForeColor = TextMain; session.Pause.Margin = new Padding(0, 1, 8, 1);
            session.Stop = DangerButton("STOP", 104); session.Stop.Enabled = false; session.Stop.Margin = new Padding(0, 1, 8, 1);
            session.Start.Click += async delegate { await RunTraceSessionAsync(session); };
            session.Pause.Click += delegate { ToggleTracePause(session); };
            session.Stop.Click += delegate { RequestTraceSessionStop(session); };
            actionFields.Controls.AddRange(new Control[] { session.Start, session.Pause, session.Stop });
            controls.Controls.Add(primaryFields, 0, 0); controls.Controls.Add(serviceFields, 0, 1); controls.Controls.Add(actionFields, 0, 2);

            var info = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 36, BackColor = Color.FromArgb(248, 250, 252), Padding = new Padding(8, 7, 8, 0), WrapContents = false };
            session.Cycle = new Label { Text = "Cycle: 0", AutoSize = true, ForeColor = TextMuted };
            session.Destination = new Label { Text = "Destination: -", AutoSize = true, ForeColor = TextMuted, Margin = new Padding(22, 0, 0, 0) };
            session.State = new Label { Text = "Target: Waiting", AutoSize = true, ForeColor = Warning, Font = new Font("Segoe UI Semibold", 9), Margin = new Padding(22, 0, 0, 0) };
            session.HopDescriptions = ActionButton("Hop descriptions...", false, 140); session.HopDescriptions.Height = 25; session.HopDescriptions.Margin = new Padding(22, 0, 0, 0); session.HopDescriptions.Click += ShowHopDescriptions;
            info.Controls.AddRange(new Control[] { session.Cycle, session.Destination, session.State, session.HopDescriptions });

            session.Table = CreateTraceTable(); session.Source = new BindingSource { DataSource = session.Table }; session.Grid = DataGrid(); session.Grid.AutoGenerateColumns = false;
            AddGridColumn(session.Grid, "Hop", "Hop", 50); AddGridColumn(session.Grid, "Address", "Address", 125); AddGridColumn(session.Grid, "Hostname", "Hostname", 170); AddGridColumn(session.Grid, "Description", "Description", 175);
            AddGridColumn(session.Grid, "Status", "Status", 82); AddGridColumn(session.Grid, "Last", "LastMs", 70); AddGridColumn(session.Grid, "Best", "BestMs", 70); AddGridColumn(session.Grid, "Average", "AvgMs", 78);
            AddGridColumn(session.Grid, "Worst", "WorstMs", 72); AddGridColumn(session.Grid, "Jitter", "JitterMs", 72); AddGridColumn(session.Grid, "Sent", "Sent", 58); AddGridColumn(session.Grid, "Received", "Received", 68);
            AddGridColumn(session.Grid, "Loss", "LossPct", 67); AddGridColumn(session.Grid, "Route changes", "RouteChanges", 98); AddGridColumn(session.Grid, "Updated", "Updated", 82);
            session.Grid.DataSource = session.Source; session.Grid.CellFormatting += FormatTraceCell; session.Grid.KeyDown += GridCopyShortcut;
            resultCard.Controls.Add(session.Grid); resultCard.Controls.Add(info); resultCard.Controls.Add(controls);
            resultCard.Controls.Add(SectionHeader("Realtime Traceroute - Session " + number, "Continuous system-DNS route polling; TCP/UDP adds a destination service check"));
            split.Panel1.Controls.Add(resultCard);

            var eventCard = Card();
            var filterBar = new TableLayoutPanel { Dock = DockStyle.Top, Height = 45, ColumnCount = 2, Padding = new Padding(0, 5, 0, 6) };
            filterBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); filterBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            filterBar.Controls.Add(new Label { Text = "TRACE EVENT LOG", Dock = DockStyle.Fill, ForeColor = TextMuted, Font = new Font("Segoe UI Semibold", 8.5f), TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
            session.EventFilter = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            session.EventFilter.Items.AddRange(new object[] { "All events", "Route changes", "DNS changes", "Service", "Errors" }); session.EventFilter.SelectedIndex = 0;
            filterBar.Controls.Add(session.EventFilter, 1, 0);
            session.EventTable = CreateTraceEventTable(); session.EventSource = new BindingSource { DataSource = session.EventTable };
            session.EventGrid = DataGrid(); session.EventGrid.AutoGenerateColumns = false; session.EventGrid.ReadOnly = true; session.EventGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            AddGridColumn(session.EventGrid, "Time", "Time", 78); AddGridColumn(session.EventGrid, "Type", "Type", 90); AddGridColumn(session.EventGrid, "Hop", "Hop", 48); AddGridColumn(session.EventGrid, "Message", "Message", 330);
            session.EventGrid.DataSource = session.EventSource; session.EventGrid.KeyDown += GridCopyShortcut;
            session.EventGrid.CellFormatting += delegate(object sender, DataGridViewCellFormattingEventArgs e)
            {
                if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
                string type = Convert.ToString(session.EventGrid.Rows[e.RowIndex].Cells["Type"].Value);
                e.CellStyle.ForeColor = type == "Error" ? Danger : type == "Route" ? Warning : type == "DNS" ? Accent : TextMain;
            };
            session.EventFilter.SelectedIndexChanged += delegate { ApplyTraceEventFilter(session); };
            eventCard.Controls.Add(session.EventGrid); eventCard.Controls.Add(filterBar);
            split.Panel2.Controls.Add(eventCard);
            return session;
        }

        DataTable CreatePingTableV103()
        {
            DataTable table = CreatePingTable();
            table.Columns.Add("Seq", typeof(int)); table.Columns.Add("SourceIp"); table.Columns.Add("Protocol"); table.Columns.Add("Port", typeof(int));
            return table;
        }

        DataTable CreateTraceEventTable()
        {
            var table = new DataTable();
            table.Columns.Add("Time"); table.Columns.Add("Type"); table.Columns.Add("Hop", typeof(int)); table.Columns.Add("Message");
            return table;
        }

        Control CompactLabeledFieldV104(string label, Control input, int width)
        {
            Control framedInput = TraceInputFrameV110(input);
            Control field = LabeledField(label, framedInput);
            field.Dock = DockStyle.None; field.Width = width; field.Height = 58;
            field.Margin = new Padding(0, 0, 8, 0);
            framedInput.Margin = new Padding(0, 2, 0, 2);
            return field;
        }

        Control TraceGridFieldV123(string label, Control input, bool last = false)
        {
            Control framedInput = TraceInputFrameV110(input);
            Control field = LabeledField(label, framedInput);
            field.Dock = DockStyle.Fill;
            field.Margin = new Padding(0, 0, 8, 0);
            field.MinimumSize = new Size(72, 54);
            framedInput.Margin = new Padding(0, 2, 0, 2);
            return field;
        }

        Control TraceInputFrameV110(Control input)
        {
            var frame = new Panel { Dock = DockStyle.Fill, Padding = new Padding(2), Margin = new Padding(0), BackColor = Color.White, Tag = "TraceInputFrame" };
            input.Dock = DockStyle.None; input.Anchor = AnchorStyles.Left | AnchorStyles.Right; input.Margin = new Padding(0); input.BackColor = Color.White;
            var numeric = input as NumericUpDown;
            if (numeric != null) numeric.BorderStyle = BorderStyle.None;
            var text = input as TextBox;
            if (text != null) text.BorderStyle = BorderStyle.None;
            var combo = input as ComboBox;
            if (combo != null)
            {
                combo.FlatStyle = FlatStyle.Flat;
                combo.DrawMode = DrawMode.OwnerDrawFixed;
                combo.DrawItem += delegate(object sender, DrawItemEventArgs e)
                {
                    Color fill = Color.White;
                    bool selected = combo.DroppedDown && (e.State & DrawItemState.Selected) == DrawItemState.Selected;
                    if (selected) fill = Color.FromArgb(219, 234, 254);
                    using (var brush = new SolidBrush(fill)) e.Graphics.FillRectangle(brush, e.Bounds);
                    string value = e.Index >= 0 && e.Index < combo.Items.Count ? Convert.ToString(combo.Items[e.Index]) : combo.Text;
                    Color textColor = combo.Enabled ? TextMain : TextMuted;
                    TextRenderer.DrawText(e.Graphics, value ?? "", combo.Font, e.Bounds, textColor,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                    if ((e.State & DrawItemState.Focus) == DrawItemState.Focus) e.DrawFocusRectangle();
                };
            }
            Action layout = delegate
            {
                input.Left = 3;
                input.Width = Math.Max(1, frame.ClientSize.Width - 6);
                input.Top = Math.Max(2, (frame.ClientSize.Height - input.Height) / 2);
            };
            frame.Resize += delegate { layout(); };
            frame.Paint += delegate(object sender, PaintEventArgs e)
            {
                Color color = input.Focused ? Accent : input.Enabled ? Color.FromArgb(148, 163, 184) : Color.FromArgb(203, 213, 225);
                using (var pen = new Pen(color)) e.Graphics.DrawRectangle(pen, 0, 0, Math.Max(0, frame.Width - 1), Math.Max(0, frame.Height - 1));
            };
            input.Enter += delegate { frame.Invalidate(); };
            input.Leave += delegate { frame.Invalidate(); };
            input.EnabledChanged += delegate { ApplyTraceInputPaletteV122(input, frame); };
            frame.Controls.Add(input);
            ApplyTraceInputPaletteV122(input, frame);
            layout();
            return frame;
        }

        void ApplyTraceInputPaletteV122(Control input, Panel frame)
        {
            Color fill = Color.White;
            frame.BackColor = fill;
            input.BackColor = fill;
            foreach (Control child in input.Controls) child.BackColor = fill;
            frame.Invalidate(); input.Invalidate();
        }

        void UpdatePingAdvancedState()
        {
            if (pingProtocol == null || pingPort == null) return;
            pingPort.Enabled = pingCancellation == null && pingProtocol.Text == "TCP";
        }

        void PopulateSourceAddresses(ComboBox combo)
        {
            combo.Items.Clear(); combo.Items.Add(new SourceAddressOption());
            try
            {
                foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces().Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback))
                {
                    foreach (UnicastIPAddressInformation item in nic.GetIPProperties().UnicastAddresses)
                    {
                        if (item.Address.AddressFamily != AddressFamily.InterNetwork && item.Address.AddressFamily != AddressFamily.InterNetworkV6) continue;
                        if (IPAddress.IsLoopback(item.Address)) continue;
                        combo.Items.Add(new SourceAddressOption { Address = item.Address.ToString(), Label = item.Address + " - " + nic.Name });
                    }
                }
            }
            catch { }
            combo.SelectedIndex = 0;
        }

        string SelectedPingSourceAddress()
        {
            SourceAddressOption option = pingSourceIp == null ? null : pingSourceIp.SelectedItem as SourceAddressOption;
            return option == null ? "" : option.Address;
        }

        void TogglePingPause(object sender, EventArgs e)
        {
            if (pingCancellation == null) return;
            pingPaused = !pingPaused;
            pingPauseButton.Text = pingPaused ? "RESUME" : "PAUSE";
            pingPauseButton.BackColor = pingPaused ? Color.FromArgb(249, 115, 22) : Surface;
            pingPauseButton.ForeColor = pingPaused ? Color.White : TextMain;
            pingPauseButton.FlatAppearance.BorderColor = pingPaused ? Color.FromArgb(249, 115, 22) : Border;
            appStatus.Text = pingPaused ? "Ping monitoring paused" : "Ping monitoring resumed";
            Log("ACTION", "Ping", pingPaused ? "Monitoring paused" : "Monitoring resumed");
        }

        static int ComputePingStaggerMs(int index, int targetCount, int interval)
        {
            if (index <= 0 || targetCount <= 1) return 0;
            int spread = Math.Min(interval, 1000);
            return (int)((long)index * spread / targetCount);
        }

        static int RemainingPollDelayV104(int interval, long elapsedMilliseconds)
        {
            return elapsedMilliseconds >= interval ? 0 : interval - (int)Math.Max(0, elapsedMilliseconds);
        }

        static int MaxOutstandingPollsV105(int timeout, int interval)
        {
            // Enough slots to keep the requested cadence across ordinary timeouts,
            // with a hard ceiling so a large CIDR cannot grow work without bounds.
            int required = (int)Math.Ceiling(Math.Max(1, timeout) / (double)Math.Max(1, interval)) + 2;
            return Math.Max(2, Math.Min(8, required));
        }

        async Task PingLoopV103(TargetSpec target, int interval, int timeout, string customDns, string protocol, int port, int packetSize,
            string selectedSource, int initialStaggerMs, CancellationToken token)
        {
            if (initialStaggerMs > 0) await Task.Delay(initialStaggerMs, token).ConfigureAwait(false);
            var resolution = new PingResolutionCacheV105();
            var pending = new List<Task>();
            int maxOutstanding = MaxOutstandingPollsV105(timeout, interval);
            var cadence = Stopwatch.StartNew();
            long nextDueMs = 0;
            long probeOrder = 0;
            bool wasPaused = false;
            try
            {
                while (!token.IsCancellationRequested)
                {
                    for (int i = pending.Count - 1; i >= 0; i--)
                        if (pending[i].IsCompleted) pending.RemoveAt(i);

                    if (pingPaused)
                    {
                        wasPaused = true;
                        await Task.Delay(80, token).ConfigureAwait(false);
                        continue;
                    }

                    if (wasPaused)
                    {
                        // Resume immediately without trying to replay ticks missed while paused.
                        nextDueMs = cadence.ElapsedMilliseconds;
                        wasPaused = false;
                    }

                    if (pending.Count < maxOutstanding)
                        pending.Add(PingProbeOnceV105Async(target, timeout, customDns, protocol, port, packetSize, selectedSource, resolution, ++probeOrder, token));

                    nextDueMs += interval;
                    long remaining = nextDueMs - cadence.ElapsedMilliseconds;
                    if (remaining > 0)
                        await Task.Delay((int)Math.Min(Int32.MaxValue, remaining), token).ConfigureAwait(false);
                    else
                    {
                        long missed = (-remaining / Math.Max(1, interval)) + 1;
                        nextDueMs += missed * interval;
                        await Task.Yield();
                    }
                }
            }
            catch (OperationCanceledException) { }
            try { await Task.WhenAll(pending.ToArray()).ConfigureAwait(false); }
            catch (OperationCanceledException) { }
            catch { }
            resolution.Gate.Dispose();
        }

        async Task PingProbeOnceV105Async(TargetSpec target, int timeout, string customDns, string protocol, int port, int packetSize,
            string selectedSource, PingResolutionCacheV105 resolution, long order, CancellationToken token)
        {
            string resolved = "";
            var probe = new ServiceProbeResult();
            try
            {
                IPAddress destination = await ResolvePingDestinationV105Async(target.Host, customDns, resolution, token).ConfigureAwait(false);
                resolved = destination.ToString();
                probe = await ProbeServiceAsync(destination, protocol, port, packetSize, timeout, selectedSource, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex) { probe.Detail = FriendlyError(ex); }

            if (token.IsCancellationRequested || pingPaused) return;
            pingUiUpdates.Enqueue(new PingUiUpdate
            {
                Host = target.Host, Resolved = resolved, Up = probe.Reachable,
                Latency = probe.LatencyMs < 0 ? -1 : (long)Math.Round(probe.LatencyMs), Ttl = probe.Ttl,
                Detail = probe.Detail, EventTime = DateTime.Now,
                SourceIp = probe.SourceIp, Protocol = protocol, Port = protocol == "ICMP" ? 0 : port, Order = order
            });
        }

        async Task<IPAddress> ResolvePingDestinationV105Async(string host, string customDns, PingResolutionCacheV105 cache, CancellationToken token)
        {
            IPAddress literal;
            if (IPAddress.TryParse(host, out literal)) return literal;
            if (cache.Destination != null && DateTime.UtcNow < cache.RefreshAfterUtc) return cache.Destination;

            await cache.Gate.WaitAsync(token).ConfigureAwait(false);
            try
            {
                if (cache.Destination != null && DateTime.UtcNow < cache.RefreshAfterUtc) return cache.Destination;
                List<string> answers = await ResolveForward(host, customDns).ConfigureAwait(false);
                if (answers.Count == 0)
                {
                    cache.RefreshAfterUtc = DateTime.UtcNow.AddSeconds(5);
                    throw new InvalidOperationException("DNS resolution failed");
                }
                cache.Destination = IPAddress.Parse(answers[0]);
                cache.RefreshAfterUtc = DateTime.UtcNow.AddSeconds(30);
                return cache.Destination;
            }
            finally { cache.Gate.Release(); }
        }

        async Task<ServiceProbeResult> ProbeServiceAsync(IPAddress destination, string protocol, int port, int packetSize, int timeout, string selectedSource, CancellationToken token)
        {
            if (protocol == "TCP") return await ProbeTcpAsync(destination, port, timeout, selectedSource, token).ConfigureAwait(false);
            if (protocol == "UDP") return await ProbeUdpAsync(destination, port, packetSize, timeout, selectedSource, token).ConfigureAwait(false);
            var result = new ServiceProbeResult();
            var watch = Stopwatch.StartNew();
            using (var ping = new Ping())
            {
                byte[] payload = new byte[Math.Max(0, Math.Min(65500, packetSize))];
                PingReply reply = await ping.SendPingAsync(destination, timeout, payload, new PingOptions(128, true)).ConfigureAwait(false);
                watch.Stop();
                result.Reachable = reply.Status == IPStatus.Success;
                result.LatencyMs = result.Reachable ? (reply.RoundtripTime > 0 ? reply.RoundtripTime : watch.Elapsed.TotalMilliseconds) : -1;
                result.Ttl = result.Reachable && reply.Options != null ? reply.Options.Ttl : 0;
                result.SourceIp = DetectLocalIpForDestination(destination);
                result.Detail = result.Reachable ? "ICMP reply from " + reply.Address : reply.Status.ToString();
                if (!String.IsNullOrWhiteSpace(selectedSource) && !String.Equals(selectedSource, result.SourceIp, StringComparison.OrdinalIgnoreCase))
                    result.Detail += " (Windows ICMP route used source " + result.SourceIp + "; selected source binding applies to TCP/UDP)";
            }
            return result;
        }

        async Task<ServiceProbeResult> ProbeTcpAsync(IPAddress destination, int port, int timeout, string selectedSource, CancellationToken token)
        {
            var result = new ServiceProbeResult();
            using (var socket = new Socket(destination.AddressFamily, SocketType.Stream, ProtocolType.Tcp))
            {
                BindSourceIfRequested(socket, destination.AddressFamily, selectedSource);
                var completion = new TaskCompletionSource<bool>();
                var watch = Stopwatch.StartNew();
                try
                {
                    socket.BeginConnect(new IPEndPoint(destination, port), delegate(IAsyncResult ar)
                    {
                        try { socket.EndConnect(ar); completion.TrySetResult(true); }
                        catch (Exception ex) { completion.TrySetException(ex); }
                    }, null);
                    Task winner = await Task.WhenAny(completion.Task, Task.Delay(timeout, token)).ConfigureAwait(false);
                    token.ThrowIfCancellationRequested();
                    if (winner != completion.Task) { result.Detail = "TCP connection timed out"; return result; }
                    await completion.Task.ConfigureAwait(false);
                    watch.Stop(); result.Reachable = true; result.LatencyMs = watch.Elapsed.TotalMilliseconds;
                    IPEndPoint local = socket.LocalEndPoint as IPEndPoint; result.SourceIp = local == null ? selectedSource : local.Address.ToString();
                    result.Detail = "TCP/" + port + " connected";
                }
                catch (Exception ex) { result.Detail = "TCP/" + port + " " + FriendlyError(ex); }
            }
            return result;
        }

        async Task<ServiceProbeResult> ProbeUdpAsync(IPAddress destination, int port, int packetSize, int timeout, string selectedSource, CancellationToken token)
        {
            var result = new ServiceProbeResult();
            using (var socket = new Socket(destination.AddressFamily, SocketType.Dgram, ProtocolType.Udp))
            {
                BindSourceIfRequested(socket, destination.AddressFamily, selectedSource);
                var completion = new TaskCompletionSource<int>();
                byte[] receive = new byte[2048]; byte[] payload = new byte[Math.Max(1, Math.Min(65500, packetSize))];
                var watch = Stopwatch.StartNew();
                try
                {
                    socket.Connect(new IPEndPoint(destination, port));
                    socket.Send(payload);
                    socket.BeginReceive(receive, 0, receive.Length, SocketFlags.None, delegate(IAsyncResult ar)
                    {
                        try { completion.TrySetResult(socket.EndReceive(ar)); }
                        catch (Exception ex) { completion.TrySetException(ex); }
                    }, null);
                    Task winner = await Task.WhenAny(completion.Task, Task.Delay(timeout, token)).ConfigureAwait(false);
                    token.ThrowIfCancellationRequested();
                    if (winner != completion.Task)
                    {
                        result.Indeterminate = true; result.Detail = "UDP/" + port + " no response (open/filtered is indeterminate)";
                    }
                    else
                    {
                        int bytes = await completion.Task.ConfigureAwait(false); watch.Stop(); result.Reachable = true; result.LatencyMs = watch.Elapsed.TotalMilliseconds;
                        result.Detail = "UDP/" + port + " response received (" + bytes + " bytes)";
                    }
                    IPEndPoint local = socket.LocalEndPoint as IPEndPoint; result.SourceIp = local == null ? selectedSource : local.Address.ToString();
                }
                catch (Exception ex) { result.Detail = "UDP/" + port + " " + FriendlyError(ex); }
            }
            return result;
        }

        static void BindSourceIfRequested(Socket socket, AddressFamily family, string selectedSource)
        {
            if (String.IsNullOrWhiteSpace(selectedSource)) return;
            IPAddress source; if (!IPAddress.TryParse(selectedSource, out source) || source.AddressFamily != family) return;
            socket.Bind(new IPEndPoint(source, 0));
        }

        void UpdatePingRowV103(PingUiUpdate update)
        {
            long latestOrder;
            bool updateCurrentState = update.Order <= 0 || !latestPingOrderV105.TryGetValue(update.Host, out latestOrder) || update.Order >= latestOrder;
            if (update.Order > 0 && updateCurrentState) latestPingOrderV105[update.Host] = update.Order;
            UpdatePingRowCoreV105(update.Host, update.Resolved, update.Up, update.Latency, update.Ttl, update.Detail, update.EventTime, updateCurrentState);
            DataRow row = pingTable.Rows.Find(update.Host); if (row == null) return;
            if (updateCurrentState)
            {
                row["SourceIp"] = update.SourceIp ?? ""; row["Protocol"] = update.Protocol ?? "ICMP"; row["Port"] = update.Port == 0 ? (object)DBNull.Value : update.Port;
                row["Status"] = String.Equals(update.Protocol, "TCP", StringComparison.OrdinalIgnoreCase)
                    ? (update.Up ? "Connected" : "TCP Timeout")
                    : (update.Up ? "ICMP OK" : "Unreachable");
            }
        }

        async Task RunTraceSessionAsync(TraceSessionV103 session)
        {
            if (session.Cancellation != null) return;
            string host = session.Target.Text.Trim(); if (host.Length == 0) return;
            RefreshHopDescriptions(); RememberTraceTargetSession(session, host);
            session.Table.Rows.Clear(); session.EventTable.Rows.Clear(); session.Stats.Clear(); session.LatestCycleByHop.Clear(); session.LastResolvedNames.Clear();
            session.CycleNumber = 0; session.LastAppliedCycle = 0; session.LastState = "";
            session.KnownDestinationHop = 0; session.LastFullProbeCycle = 0; session.ForceFullProbeCycles = 0;
            string protocol = session.Protocol.Text; int port = (int)session.Port.Value; int packetSize = (int)session.PacketSize.Value;
            int maxHops = (int)session.MaxHops.Value; int timeout = (int)session.Timeout.Value; int interval = (int)session.Interval.Value;
            session.Cancellation = new CancellationTokenSource(); CancellationToken token = session.Cancellation.Token;
            SetTraceSessionRunning(session, true); session.State.Text = "Target: Discovering"; session.State.ForeColor = Warning;
            AddTraceEvent(session, "Info", 0, "Trace started - " + protocol + (protocol == "ICMP" ? "" : "/" + port) + ". Route hops use ICMP TTL probes.");
            Log("ACTION", "Traceroute", "Session " + session.Number + " started: " + host + " (" + protocol + ")");
            try
            {
                string destination = host; IPAddress parsed;
                if (!IPAddress.TryParse(destination, out parsed))
                {
                    List<string> answers = await ResolveForward(host, "");
                    if (answers.Count == 0) throw new InvalidOperationException("Unable to resolve trace target.");
                    destination = answers[0]; parsed = IPAddress.Parse(destination);
                    AddTraceEvent(session, "DNS", 0, host + " resolved to " + destination);
                }
                session.Destination.Text = "Destination: " + destination;
                int activeHops = maxHops;
                int maxOutstanding = MaxOutstandingPollsV105(timeout, interval);
                var pending = new List<Task<TraceCycleResultV105>>();
                var cadence = Stopwatch.StartNew();
                long nextDueMs = 0;
                bool wasPaused = false;
                while (!token.IsCancellationRequested)
                {
                    for (int i = 0; i < pending.Count; )
                    {
                        if (!pending[i].IsCompleted) { i++; continue; }
                        Task<TraceCycleResultV105> completed = pending[i]; pending.RemoveAt(i);
                        try
                        {
                            TraceCycleResultV105 result = await completed;
                            int reachedHop = ApplyTraceCycleV105(session, result, protocol, port);
                            if (reachedHop > 0) activeHops = Math.Min(activeHops, reachedHop);
                        }
                        catch (OperationCanceledException) { }
                        catch (Exception ex) { AddTraceEvent(session, "Error", 0, FriendlyError(ex)); }
                    }

                    if (session.Paused)
                    {
                        wasPaused = true;
                        await WaitForTraceWakeV105(pending, 80, token);
                        continue;
                    }

                    if (wasPaused)
                    {
                        nextDueMs = cadence.ElapsedMilliseconds;
                        wasPaused = false;
                    }

                    long nowMs = cadence.ElapsedMilliseconds;
                    if (nowMs >= nextDueMs)
                    {
                        if (pending.Count < maxOutstanding)
                        {
                            int cycle = ++session.CycleNumber;
                            session.Cycle.Text = "Cycle: " + cycle;
                            int[] ttls = BuildAdaptiveTraceTtlsV120(session, cycle, activeHops);
                            pending.Add(ProbeTraceCycleV105Async(cycle, destination, parsed, ttls, timeout, packetSize, protocol, port, token));
                        }
                        nextDueMs += interval;
                        if (nowMs >= nextDueMs)
                        {
                            long missed = ((nowMs - nextDueMs) / Math.Max(1, interval)) + 1;
                            nextDueMs += missed * interval;
                        }
                        continue;
                    }

                    await WaitForTraceWakeV105(pending, (int)Math.Max(1, nextDueMs - nowMs), token);
                }

                try { await Task.WhenAll(pending.ToArray()); }
                catch (OperationCanceledException) { }
                catch { }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                AddTraceEvent(session, "Error", 0, FriendlyError(ex)); Log("ERROR", "Traceroute", "Session " + session.Number + ": " + FriendlyError(ex));
            }
            finally
            {
                CancellationTokenSource completed = session.Cancellation; session.Cancellation = null;
                if (completed != null) completed.Dispose();
                if (!appClosing && !IsDisposed && !Disposing)
                {
                    AddTraceEvent(session, "Info", 0, "Trace stopped"); SetTraceSessionRunning(session, false);
                }
            }
        }

        async Task WaitForTraceWakeV105(List<Task<TraceCycleResultV105>> pending, int delayMs, CancellationToken token)
        {
            Task delay = Task.Delay(Math.Max(1, delayMs), token);
            if (pending.Count == 0) { await delay; return; }
            Task completion = Task.WhenAny(pending.ToArray());
            await Task.WhenAny(delay, completion);
            token.ThrowIfCancellationRequested();
        }

        int[] BuildAdaptiveTraceTtlsV120(TraceSessionV103 session, int cycle, int activeHops)
        {
            activeHops = Math.Max(1, activeHops);
            bool full = session.KnownDestinationHop <= 0 || cycle <= 3 || session.ForceFullProbeCycles > 0
                || cycle - session.LastFullProbeCycle >= 8;
            if (full)
            {
                session.LastFullProbeCycle = cycle;
                if (session.ForceFullProbeCycles > 0) session.ForceFullProbeCycles--;
                return Enumerable.Range(1, activeHops).ToArray();
            }
            int destinationHop = Math.Max(1, Math.Min(activeHops, session.KnownDestinationHop));
            var selected = new HashSet<int> { 1, destinationHop };
            for (int hop = 2; hop < destinationHop; hop++)
                if ((hop + cycle) % 4 == 0) selected.Add(hop);
            foreach (KeyValuePair<int, HopStats> item in session.Stats)
                if (item.Key <= destinationHop && item.Value.RouteChanges > 0) selected.Add(item.Key);
            return selected.OrderBy(value => value).ToArray();
        }

        async Task<TraceCycleResultV105> ProbeTraceCycleV105Async(int cycle, string destination, IPAddress parsed, int[] ttls,
            int timeout, int packetSize, string protocol, int port, CancellationToken token)
        {
            Task<HopProbe>[] tasks = (ttls ?? new int[0])
                .Select(ttl => ProbeHopV103Async(destination, ttl, timeout, packetSize)).ToArray();
            Task<ServiceProbeResult> serviceTask = protocol == "ICMP" ? null : ProbeServiceAsync(parsed, protocol, port, packetSize, timeout, "", token);
            HopProbe[] probes = await Task.WhenAll(tasks).ConfigureAwait(false);
            ServiceProbeResult service = serviceTask == null ? null : await serviceTask.ConfigureAwait(false);
            return new TraceCycleResultV105 { Cycle = cycle, Probes = probes, Service = service };
        }

        int ApplyTraceCycleV105(TraceSessionV103 session, TraceCycleResultV105 result, string protocol, int port)
        {
            TraceGridPositionV110 gridPosition = CaptureTraceGridPositionV110(session.Grid);
            HopProbe[] probes = result.Probes ?? new HopProbe[0];
            int reachedHop = probes.Where(p => p.Reached).Select(p => p.Hop).DefaultIfEmpty(0).Min();
            if (reachedHop > 0)
            {
                session.KnownDestinationHop = reachedHop;
                foreach (DataRow extra in session.Table.Rows.Cast<DataRow>().Where(r => Convert.ToInt32(r["Hop"]) > reachedHop).ToArray()) session.Table.Rows.Remove(extra);
                foreach (int extra in session.Stats.Keys.Where(h => h > reachedHop).ToArray()) session.Stats.Remove(extra);
                foreach (int extra in session.LatestCycleByHop.Keys.Where(h => h > reachedHop).ToArray()) session.LatestCycleByHop.Remove(extra);
            }

            int[] visibleHops = probes.Where(p => reachedHop == 0 || p.Hop <= reachedHop).Select(p => p.Hop).Distinct().ToArray();
            foreach (int hop in visibleHops)
            {
                if (session.Table.Rows.Find(hop) != null) continue;
                DataRow added = session.Table.NewRow(); added["Hop"] = hop; session.Table.Rows.Add(added);
            }

            session.Grid.SuspendLayout();
            session.Source.RaiseListChangedEvents = false;
            var changedHops = new HashSet<int>();
            try
            {
                foreach (HopProbe probe in probes.Where(p => reachedHop == 0 || p.Hop <= reachedHop).OrderBy(p => p.Hop))
                {
                    HopStats stat;
                    if (!session.Stats.TryGetValue(probe.Hop, out stat)) { stat = new HopStats(); session.Stats[probe.Hop] = stat; }
                    int latestCycle;
                    bool isLatest = !session.LatestCycleByHop.TryGetValue(probe.Hop, out latestCycle) || result.Cycle >= latestCycle;
                    stat.Sent++;
                    if (probe.Responded)
                    {
                        stat.Received++; stat.Total += probe.Latency; stat.Best = Math.Min(stat.Best, probe.Latency); stat.Worst = Math.Max(stat.Worst, probe.Latency);
                        if (isLatest)
                        {
                            stat.Last = probe.Latency;
                            if (stat.PreviousLatency >= 0) { stat.JitterTotal += Math.Abs(probe.Latency - stat.PreviousLatency); stat.JitterSamples++; }
                            stat.PreviousLatency = probe.Latency;
                            bool changed = stat.Address.Length > 0 && !String.Equals(stat.Address, probe.Address, StringComparison.OrdinalIgnoreCase);
                            if (changed)
                            {
                                session.ForceFullProbeCycles = Math.Max(session.ForceFullProbeCycles, 3);
                                stat.RouteChanges++; AddTraceEvent(session, "Route", probe.Hop, "Route changed: " + stat.Address + " -> " + probe.Address);
                                Log("WARNING", "Traceroute", "Session " + session.Number + " hop " + probe.Hop + " route changed " + stat.Address + " -> " + probe.Address);
                            }
                            if (!String.Equals(stat.Address, probe.Address, StringComparison.OrdinalIgnoreCase))
                            {
                                stat.Address = probe.Address; stat.Hostname = "";
                                ResolveTraceHopNameV103(session, probe.Hop, probe.Address);
                                QueueTraceProviderLookupV104(session, probe.Hop, probe.Address);
                            }
                        }
                    }
                    if (isLatest) session.LatestCycleByHop[probe.Hop] = result.Cycle;
                    DataRow existing = session.Table.Rows.Find(probe.Hop);
                    string status = isLatest || existing == null ? probe.Status : Convert.ToString(existing["Status"]);
                    UpsertTraceSession(session, probe.Hop, stat, status);
                    changedHops.Add(probe.Hop);
                }
            }
            finally
            {
                session.Source.RaiseListChangedEvents = true;
                foreach (int hop in changedHops) NotifyTraceHopChangedV120(session, hop);
                RestoreTraceGridPositionV110(session.Grid, gridPosition);
                session.Grid.ResumeLayout();
            }

            if (result.Cycle >= session.LastAppliedCycle)
            {
                bool routeReached = probes.Any(p => p.Reached); string state; Color stateColor;
                if (protocol == "ICMP") { state = routeReached ? "Reachable" : "No final reply"; stateColor = routeReached ? Success : Warning; }
                else
                {
                    ServiceProbeResult service = result.Service ?? new ServiceProbeResult { Detail = "No service result" };
                    state = service.Reachable ? protocol + "/" + port + " Reachable" : service.Indeterminate ? protocol + "/" + port + " Indeterminate" : protocol + "/" + port + " Unreachable";
                    stateColor = service.Reachable ? Success : service.Indeterminate ? Warning : Danger;
                    AddTraceEventOnStateChange(session, "Service", 0, state + " - " + service.Detail);
                }
                session.State.Text = "Target: " + state; session.State.ForeColor = stateColor;
                if (protocol == "ICMP") AddTraceEventOnStateChange(session, routeReached ? "Info" : "Error", reachedHop, "Target " + state);
                session.LastAppliedCycle = result.Cycle;
            }
            return reachedHop;
        }

        static void NotifyTraceHopChangedV120(TraceSessionV103 session, int hop)
        {
            if (session == null || session.Source == null) return;
            for (int index = 0; index < session.Source.Count; index++)
            {
                var view = session.Source[index] as DataRowView;
                if (view == null || Convert.ToInt32(view["Hop"]) != hop) continue;
                session.Source.ResetItem(index);
                if (session.Grid != null && index < session.Grid.Rows.Count) session.Grid.InvalidateRow(index);
                break;
            }
        }

        static TraceGridPositionV110 CaptureTraceGridPositionV110(DataGridView grid)
        {
            var position = new TraceGridPositionV110();
            if (grid == null || grid.IsDisposed) return position;
            try { position.FirstDisplayedRow = grid.FirstDisplayedScrollingRowIndex; } catch { }
            try { position.HorizontalOffset = grid.HorizontalScrollingOffset; } catch { }
            try
            {
                if (grid.CurrentCell != null)
                {
                    position.CurrentColumn = grid.Columns[grid.CurrentCell.ColumnIndex].Name;
                    object hop = grid.Rows[grid.CurrentCell.RowIndex].Cells["Hop"].Value;
                    Int32.TryParse(Convert.ToString(hop), out position.CurrentHop);
                }
            }
            catch { }
            return position;
        }

        static void RestoreTraceGridPositionV110(DataGridView grid, TraceGridPositionV110 position)
        {
            if (grid == null || grid.IsDisposed || position == null || grid.Rows.Count == 0) return;
            try
            {
                if (position.CurrentHop >= 0 && grid.Columns.Contains(position.CurrentColumn))
                {
                    DataGridViewRow current = grid.Rows.Cast<DataGridViewRow>()
                        .FirstOrDefault(row => Convert.ToInt32(row.Cells["Hop"].Value) == position.CurrentHop);
                    if (current != null) grid.CurrentCell = current.Cells[position.CurrentColumn];
                }
            }
            catch { }
            try
            {
                int first = Math.Max(0, Math.Min(position.FirstDisplayedRow, grid.Rows.Count - 1));
                if (position.FirstDisplayedRow >= 0) grid.FirstDisplayedScrollingRowIndex = first;
            }
            catch { }
            try { grid.HorizontalScrollingOffset = Math.Max(0, position.HorizontalOffset); } catch { }
        }

        async Task<HopProbe> ProbeHopV103Async(string destination, int hop, int timeout, int packetSize)
        {
            var result = new HopProbe { Hop = hop };
            try
            {
                PingReply reply; var watch = Stopwatch.StartNew(); byte[] payload = new byte[Math.Max(0, Math.Min(65500, packetSize))];
                using (var ping = new Ping()) reply = await ping.SendPingAsync(destination, timeout, payload, new PingOptions(hop, true)).ConfigureAwait(false);
                watch.Stop(); result.IpStatus = reply.Status; result.Address = reply.Address == null ? "" : reply.Address.ToString();
                result.Latency = reply.RoundtripTime > 0 ? reply.RoundtripTime : Math.Max(0.1, watch.Elapsed.TotalMilliseconds);
                result.Reached = reply.Status == IPStatus.Success;
                bool unreachable = reply.Status == IPStatus.DestinationHostUnreachable || reply.Status == IPStatus.DestinationNetworkUnreachable || reply.Status == IPStatus.DestinationPortUnreachable || reply.Status == IPStatus.DestinationProtocolUnreachable;
                result.Responded = result.Reached || reply.Status == IPStatus.TtlExpired || unreachable;
                result.Status = result.Reached ? "Reached" : reply.Status == IPStatus.TtlExpired ? "Reply" : unreachable ? "Unreachable" : reply.Status == IPStatus.TimedOut ? "Timeout" : reply.Status.ToString();
            }
            catch (Exception ex) { result.Status = FriendlyError(ex); }
            return result;
        }

        async void ResolveTraceHopNameV103(TraceSessionV103 session, int hop, string address)
        {
            if (String.IsNullOrWhiteSpace(address)) return;
            try
            {
                string previousCached = "";
                string hostname = "";
                lock (traceLookupCacheLockV120)
                {
                    TraceLookupCacheItemV120 cached;
                    if (traceDnsEntriesV120.TryGetValue(address, out cached))
                    {
                        previousCached = cached == null ? "" : cached.Value ?? "";
                        if (IsTraceLookupFreshV120(cached, TimeSpan.FromMinutes(15))) hostname = previousCached;
                    }
                }
                if (hostname.Length == 0)
                {
                    hostname = await ResolveReverse(IPAddress.Parse(address), "");
                    if (!String.IsNullOrWhiteSpace(hostname))
                    {
                        lock (traceLookupCacheLockV120)
                            traceDnsEntriesV120[address] = new TraceLookupCacheItemV120 { Value = hostname, RetrievedUtc = DateTime.UtcNow };
                        ScheduleTraceLookupCacheSaveV120();
                    }
                }
                ApplyTraceDnsResultV120(session, hop, address, hostname, previousCached);
            }
            catch { }
        }

        void ApplyTraceDnsResultV120(TraceSessionV103 session, int hop, string address, string hostname, string previousCached)
        {
            HopStats stat; if (!session.Stats.TryGetValue(hop, out stat) || !String.Equals(stat.Address, address, StringComparison.OrdinalIgnoreCase)) return;
            string previous; session.LastResolvedNames.TryGetValue(hop, out previous);
            if (String.IsNullOrWhiteSpace(previous)) previous = previousCached;
            stat.Hostname = hostname ?? ""; session.LastResolvedNames[hop] = hostname ?? "";
            DataRow row = session.Table.Rows.Find(hop);
            if (row != null && String.Equals(Convert.ToString(row["Address"]), address, StringComparison.OrdinalIgnoreCase)) row["Hostname"] = hostname ?? "";
            NotifyTraceHopChangedV120(session, hop);
            if (!String.IsNullOrWhiteSpace(previous) && !String.Equals(previous, hostname, StringComparison.OrdinalIgnoreCase))
                AddTraceEvent(session, "DNS", hop, "DNS changed: " + previous + " -> " + hostname);
            else if (!String.IsNullOrWhiteSpace(hostname)) AddTraceEvent(session, "DNS", hop, address + " resolved to " + hostname);
        }

        void QueueTraceProviderLookupV104(TraceSessionV103 session, int hop, string address)
        {
            if (!IsPublicAddressV104(address) || !String.IsNullOrWhiteSpace(GetHopDescription(address))) return;
            lock (traceLookupCacheLockV120)
            {
                TraceLookupCacheItemV120 entry;
                if (traceProviderEntriesV120.TryGetValue(address, out entry))
                {
                    if (IsTraceLookupFreshV120(entry, ProviderCacheTtlV120(entry))) { traceProviderCacheV104[address] = entry.Value; return; }
                    traceProviderCacheV104.Remove(address);
                }
                else if (traceProviderCacheV104.ContainsKey(address)) return;
            }
            if (!traceProviderPendingV104.Add(address)) return;
            ResolveTraceProviderV104(session, hop, address);
        }

        async void ResolveTraceProviderV104(TraceSessionV103 sourceSession, int hop, string address)
        {
            await traceProviderGateV104.WaitAsync();
            string provider = "";
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create("https://ipwho.is/" + Uri.EscapeDataString(address));
                request.UserAgent = AppName + "/" + AppVersion; request.Timeout = 4000; request.ReadWriteTimeout = 4000;
                Task<WebResponse> responseTask = request.GetResponseAsync();
                if (await Task.WhenAny(responseTask, Task.Delay(4200)) != responseTask)
                {
                    request.Abort();
                    throw new TimeoutException("ISP lookup timed out");
                }
                string json;
                using (WebResponse response = await responseTask)
                using (var reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8)) json = await reader.ReadToEndAsync();
                var data = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(json);
                bool success = data != null && data.ContainsKey("success") && Convert.ToBoolean(data["success"], CultureInfo.InvariantCulture);
                var connection = success && data.ContainsKey("connection") ? data["connection"] as Dictionary<string, object> : null;
                string organization = Value(connection, "org").Trim();
                string isp = Value(connection, "isp").Trim();
                string asn = Value(connection, "asn").Trim();
                provider = organization.Length > 0 ? organization : isp;
                if (provider.Length > 0 && asn.Length > 0 && provider.IndexOf(asn, StringComparison.OrdinalIgnoreCase) < 0)
                    provider += " (AS" + asn.TrimStart('A', 'S') + ")";
                if (provider.Length == 0) provider = "Public IP (ISP unavailable)";
            }
            catch (Exception ex)
            {
                provider = "Public IP (ISP unavailable)";
                if (!appClosing && !IsDisposed && !Disposing)
                    AddTraceEvent(sourceSession, "Error", hop, address + " ISP lookup: " + FriendlyError(ex));
            }
            finally
            {
                lock (traceLookupCacheLockV120)
                {
                    traceProviderCacheV104[address] = provider;
                    traceProviderEntriesV120[address] = new TraceLookupCacheItemV120 { Value = provider, RetrievedUtc = DateTime.UtcNow };
                }
                traceProviderPendingV104.Remove(address);
                traceProviderGateV104.Release();
                ScheduleTraceLookupCacheSaveV120();
            }

            if (appClosing || IsDisposed || Disposing) return;
            foreach (TraceSessionV103 session in traceSessionsV103)
            {
                foreach (DataRow row in session.Table.Rows.Cast<DataRow>().Where(r => String.Equals(Convert.ToString(r["Address"]), address, StringComparison.OrdinalIgnoreCase)))
                    if (String.IsNullOrWhiteSpace(GetHopDescription(address)))
                    {
                        row["Description"] = provider;
                        NotifyTraceHopChangedV120(session, Convert.ToInt32(row["Hop"]));
                    }
            }
            if (!provider.StartsWith("Public IP (", StringComparison.OrdinalIgnoreCase))
                AddTraceEvent(sourceSession, "Info", hop, address + " provider: " + provider);
        }

        string GetTraceDescriptionV104(string address)
        {
            string custom = GetHopDescription(address);
            if (!String.IsNullOrWhiteSpace(custom)) return custom;
            if (!IsPublicAddressV104(address)) return "";
            string provider;
            lock (traceLookupCacheLockV120)
                return traceProviderCacheV104.TryGetValue(address, out provider) ? provider : "Looking up ISP...";
        }

        static bool IsPublicAddressV104(string value)
        {
            IPAddress address;
            if (!IPAddress.TryParse(value, out address) || IPAddress.IsLoopback(address)) return false;
            if (address.AddressFamily == AddressFamily.InterNetwork)
            {
                byte[] b = address.GetAddressBytes();
                if (b[0] == 0 || b[0] == 10 || b[0] == 127 || b[0] >= 224) return false;
                if (b[0] == 100 && b[1] >= 64 && b[1] <= 127) return false;
                if (b[0] == 169 && b[1] == 254) return false;
                if (b[0] == 172 && b[1] >= 16 && b[1] <= 31) return false;
                if (b[0] == 192 && b[1] == 168) return false;
                if (b[0] == 192 && b[1] == 0 && (b[2] == 0 || b[2] == 2)) return false;
                if (b[0] == 198 && (b[1] == 18 || b[1] == 19 || (b[1] == 51 && b[2] == 100))) return false;
                if (b[0] == 203 && b[1] == 0 && b[2] == 113) return false;
                return true;
            }
            if (address.AddressFamily == AddressFamily.InterNetworkV6)
            {
                byte[] b = address.GetAddressBytes();
                return !address.IsIPv6LinkLocal && !address.IsIPv6Multicast && !address.IsIPv6SiteLocal && (b[0] & 0xFE) != 0xFC;
            }
            return false;
        }

        void UpsertTraceSession(TraceSessionV103 session, int hop, HopStats stat, string status)
        {
            DataRow row = session.Table.Rows.Find(hop); if (row == null) { row = session.Table.NewRow(); row["Hop"] = hop; session.Table.Rows.Add(row); }
            row["Address"] = stat.Address.Length == 0 ? "*" : stat.Address; row["Hostname"] = stat.Hostname; row["Description"] = GetTraceDescriptionV104(stat.Address); row["Status"] = status;
            row["LastMs"] = stat.Received == 0 ? (object)DBNull.Value : Math.Round(stat.Last, 2); row["BestMs"] = stat.Received == 0 ? (object)DBNull.Value : Math.Round(stat.Best, 2);
            row["AvgMs"] = stat.Received == 0 ? (object)DBNull.Value : Math.Round(stat.Total / stat.Received, 2); row["WorstMs"] = stat.Received == 0 ? (object)DBNull.Value : Math.Round(stat.Worst, 2);
            row["JitterMs"] = stat.JitterSamples == 0 ? (object)DBNull.Value : Math.Round(stat.JitterTotal / stat.JitterSamples, 2);
            row["Sent"] = stat.Sent; row["Received"] = stat.Received; row["LossPct"] = stat.Sent == 0 ? 0d : Math.Round((stat.Sent - stat.Received) * 100d / stat.Sent, 2);
            row["RouteChanges"] = stat.RouteChanges; row["Updated"] = DateTime.Now.ToString("HH:mm:ss");
        }

        void ToggleTracePause(TraceSessionV103 session)
        {
            if (session.Cancellation == null) return;
            session.Paused = !session.Paused; session.Pause.Text = session.Paused ? "RESUME" : "PAUSE";
            session.Pause.BackColor = session.Paused ? Color.FromArgb(249, 115, 22) : Surface;
            session.Pause.ForeColor = session.Paused ? Color.White : TextMain;
            session.Pause.FlatAppearance.BorderColor = session.Paused ? Color.FromArgb(249, 115, 22) : Border;
            AddTraceEvent(session, "Info", 0, session.Paused ? "Trace paused" : "Trace resumed");
        }

        void RequestTraceSessionStop(TraceSessionV103 session)
        {
            if (session.Cancellation == null) return;
            session.Stop.Enabled = false; session.Stop.Text = "STOPPING"; session.Stop.BackColor = Warning; session.Stop.ForeColor = Color.White;
            session.Stop.FlatAppearance.BorderColor = Warning;
            session.Paused = false; session.Cancellation.Cancel();
        }

        void SetTraceSessionRunning(TraceSessionV103 session, bool running)
        {
            SetTabActivity("Traceroute", running);
            session.Start.Enabled = !running; session.Pause.Enabled = running; session.Stop.Enabled = running;
            session.Target.Enabled = !running; session.Protocol.Enabled = !running; session.Port.Enabled = !running;
            session.PacketSize.Enabled = session.MaxHops.Enabled = session.Timeout.Enabled = session.Interval.Enabled = !running;
            session.Page.Text = "Session " + session.Number + (running ? "  [RUNNING]" : "");
            if (running)
            {
                session.Start.Text = "MONITORING"; session.Start.BackColor = Color.FromArgb(220, 252, 231); session.Start.ForeColor = Color.FromArgb(21, 128, 61); session.Start.FlatAppearance.BorderColor = Color.FromArgb(134, 239, 172);
                session.Pause.Text = "PAUSE"; session.Pause.BackColor = Surface; session.Pause.ForeColor = TextMain; session.Pause.FlatAppearance.BorderColor = Border;
                session.Stop.Text = "STOP NOW"; session.Stop.BackColor = Danger; session.Stop.ForeColor = Color.White; session.Stop.FlatAppearance.BorderColor = Danger;
                appStatus.Text = "Traceroute session " + session.Number + " active";
            }
            else
            {
                session.Paused = false; session.Start.Text = "START"; session.Start.BackColor = Accent; session.Start.ForeColor = Color.White;
                session.Start.FlatAppearance.BorderColor = Accent;
                session.Pause.Text = "PAUSE"; session.Pause.BackColor = Surface; session.Pause.ForeColor = TextMain; session.Pause.FlatAppearance.BorderColor = Border;
                session.Stop.Text = "STOP"; session.Stop.BackColor = Surface; session.Stop.ForeColor = Danger;
                session.Stop.FlatAppearance.BorderColor = Color.FromArgb(252, 165, 165);
                appStatus.Text = traceSessionsV103.Any(s => s.Cancellation != null) ? "Traceroute active" : "Ready";
            }
        }

        void RememberTraceTargetSession(TraceSessionV103 session, string target)
        {
            foreach (object item in session.Target.Items) if (String.Equals(Convert.ToString(item), target, StringComparison.OrdinalIgnoreCase)) return;
            session.Target.Items.Insert(0, target); while (session.Target.Items.Count > 20) session.Target.Items.RemoveAt(session.Target.Items.Count - 1);
        }

        void AddTraceEventOnStateChange(TraceSessionV103 session, string type, int hop, string message)
        {
            if (String.Equals(session.LastState, message, StringComparison.Ordinal)) return;
            session.LastState = message; AddTraceEvent(session, type, hop, message);
        }

        void AddTraceEvent(TraceSessionV103 session, string type, int hop, string message)
        {
            if (session.EventTable == null) return;
            session.EventTable.Rows.Add(DateTime.Now.ToString("HH:mm:ss"), type, hop == 0 ? (object)DBNull.Value : hop, message);
            while (session.EventTable.Rows.Count > 1000) session.EventTable.Rows.RemoveAt(0);
        }

        void ApplyTraceEventFilter(TraceSessionV103 session)
        {
            if (session.EventSource == null) return;
            string selected = session.EventFilter.Text;
            session.EventSource.Filter = selected == "Route changes" ? "Type = 'Route'" : selected == "DNS changes" ? "Type = 'DNS'" : selected == "Service" ? "Type = 'Service'" : selected == "Errors" ? "Type = 'Error'" : "";
        }

        void StopV103Sessions()
        {
            pingPaused = false;
            foreach (TraceSessionV103 session in traceSessionsV103)
                if (session.Cancellation != null) try { session.Cancellation.Cancel(); } catch { }
        }

        async Task RefreshNetworkIdentityAsync()
        {
            if (appClosing || IsDisposed || Disposing) return;
            networkIdentityCachePathV103 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppName, "network-identity-v103.json");
            string local = DetectPreferredLocalIp(); localIpStatus.Text = "My Local IP: " + (local.Length == 0 ? "Unavailable" : local);
            localIpStatus.ToolTipText = "Preferred local IPv4 address selected by Windows routing";
            NetworkIdentityCacheV103 cached = LoadNetworkIdentityCacheV103();
            if (cached != null)
            {
                publicIpStatus.Text = "My Public IP: " + cached.PublicIp;
                if ((DateTime.UtcNow - cached.RetrievedUtc).TotalMinutes <= 10) return;
            }
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create("https://api.ipify.org");
                request.UserAgent = AppName + "/" + AppVersion; request.Timeout = 4000; request.ReadWriteTimeout = 4000;
                string publicIp;
                using (WebResponse response = await request.GetResponseAsync())
                using (var reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8)) publicIp = (await reader.ReadToEndAsync()).Trim();
                if (appClosing || IsDisposed || Disposing) return;
                IPAddress parsed; if (!IPAddress.TryParse(publicIp, out parsed)) throw new InvalidOperationException("Public IP service returned an invalid address.");
                publicIpStatus.Text = "My Public IP: " + publicIp; publicIpStatus.ToolTipText = "Current egress public IP";
                SaveNetworkIdentityCacheV103(new NetworkIdentityCacheV103 { PublicIp = publicIp, RetrievedUtc = DateTime.UtcNow });
            }
            catch (Exception ex)
            {
                if (appClosing || IsDisposed || Disposing) return;
                if (cached == null) publicIpStatus.Text = "My Public IP: Unavailable";
                else publicIpStatus.ToolTipText = "Cached value - refresh failed";
                Log("WARNING", "Network identity", "Public IP lookup unavailable: " + FriendlyError(ex));
            }
        }

        static string DetectPreferredLocalIp()
        {
            try
            {
                using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
                {
                    socket.Connect(new IPEndPoint(IPAddress.Parse("1.1.1.1"), 53));
                    IPEndPoint endpoint = socket.LocalEndPoint as IPEndPoint; if (endpoint != null) return endpoint.Address.ToString();
                }
            }
            catch { }
            try
            {
                foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces().Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback))
                    foreach (UnicastIPAddressInformation item in nic.GetIPProperties().UnicastAddresses)
                        if (item.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(item.Address)) return item.Address.ToString();
            }
            catch { }
            return "";
        }

        static string DetectLocalIpForDestination(IPAddress destination)
        {
            try
            {
                using (var socket = new Socket(destination.AddressFamily, SocketType.Dgram, ProtocolType.Udp))
                {
                    socket.Connect(new IPEndPoint(destination, 53));
                    IPEndPoint endpoint = socket.LocalEndPoint as IPEndPoint; if (endpoint != null) return endpoint.Address.ToString();
                }
            }
            catch { }
            return destination.AddressFamily == AddressFamily.InterNetwork ? DetectPreferredLocalIp() : "";
        }

        NetworkIdentityCacheV103 LoadNetworkIdentityCacheV103()
        {
            try
            {
                if (String.IsNullOrWhiteSpace(networkIdentityCachePathV103) || !File.Exists(networkIdentityCachePathV103)) return null;
                NetworkIdentityCacheV103 cache = new JavaScriptSerializer().Deserialize<NetworkIdentityCacheV103>(File.ReadAllText(networkIdentityCachePathV103, Encoding.UTF8));
                return cache == null || String.IsNullOrWhiteSpace(cache.PublicIp) ? null : cache;
            }
            catch { return null; }
        }

        void SaveNetworkIdentityCacheV103(NetworkIdentityCacheV103 cache)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(networkIdentityCachePathV103));
                File.WriteAllText(networkIdentityCachePathV103, new JavaScriptSerializer().Serialize(cache), new UTF8Encoding(false));
            }
            catch { }
        }
    }
}
