using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using NetStuck;

static class FeatureTests
{
    static int failures;
    static void Check(string name, bool ok)
    {
        Console.WriteLine((ok ? "PASS " : "FAIL ") + name);
        if (!ok) failures++;
    }

    [STAThread]
    static int Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        string state = Path.Combine(Environment.CurrentDirectory, "feature-state-test.json");
        if (File.Exists(state)) File.Delete(state);
        Environment.SetEnvironmentVariable("NETSTUCK_TEST_STATE_PATH", state);
        var startupWatch = Stopwatch.StartNew();
        using (var form = new MainForm())
        {
            Set(form, "statePath", state);
            // Keep geometry assertions deterministic on headless CI runners.
            // Production remains DPI-aware; this suite validates the declared
            // 1460x900 and 1100x700 logical layouts independent of host DPI.
            form.AutoScaleMode = AutoScaleMode.None;
            form.StartPosition = FormStartPosition.Manual;
            form.Location = Point.Empty;
            form.Width = 1460; form.Height = 900; form.Show();
            form.Width = 1460; form.Height = 900; Pump(700);
            startupWatch.Stop();
            var controls = Flat(form).ToList();
            Check("UI first display stays responsive", startupWatch.ElapsedMilliseconds < 3000);
            var tabs = controls.OfType<TabControl>().First(t => t.TabPages.Count >= 8);
            Check("window title is only NetStuck", form.Text == "NetStuck");
            Check("Config Collector remains and Log Sanitizer menu is removed", tabs.TabPages.Cast<TabPage>().Any(t => t.Text == "Config Collector")
                && !tabs.TabPages.Cast<TabPage>().Any(t => t.Text == "Log Sanitizer"));
            Check("v.1.2.3 Updates menu exists", tabs.TabPages.Cast<TabPage>().Any(t => t.Text == "Updates")
                && controls.OfType<TextBox>().Any(t => t.ReadOnly && t.Text.Contains("NetStuck v.1.2.3 (Current)") && t.Text.Contains("NetStuck v.1.2.2")));
            Check("WinMTR text removed", !controls.Any(c => c.Text.IndexOf("WinMTR", StringComparison.OrdinalIgnoreCase) >= 0));
            StatusStrip globalStatus = controls.OfType<StatusStrip>().First();
            Check("global status shows local and public IP", globalStatus.Items.Cast<ToolStripItem>().Any(i => i.Text.StartsWith("My Local IP:"))
                && globalStatus.Items.Cast<ToolStripItem>().Any(i => i.Text.StartsWith("My Public IP:")));
            int localIpIndex = globalStatus.Items.Cast<ToolStripItem>().ToList().FindIndex(i => i.Text.StartsWith("My Local IP:"));
            int publicIpIndex = globalStatus.Items.Cast<ToolStripItem>().ToList().FindIndex(i => i.Text.StartsWith("My Public IP:"));
            Check("network identity is separated at the bottom right", localIpIndex >= globalStatus.Items.Count - 4 && publicIpIndex > localIpIndex
                && globalStatus.Items.OfType<ToolStripSeparator>().Count() >= 2);

            foreach (string pageName in new[] { "MAC / WAN Lookup", "Calculators" })
            {
                tabs.SelectedTab = tabs.TabPages.Cast<TabPage>().First(t => t.Text == pageName); Pump(150);
                SplitContainer split = Flat(tabs.SelectedTab).OfType<SplitContainer>().First();
                double ratio = split.SplitterDistance / (double)Math.Max(1, split.ClientSize.Width - split.SplitterWidth);
                Check(pageName + " opens 50/50", ratio > 0.46 && ratio < 0.54);
            }
            TextBox reference = controls.OfType<TextBox>().FirstOrDefault(t => t.ReadOnly && t.Text.Contains("QUICK REFERENCE"));
            Check("calculator includes /16 and /17", reference != null && reference.Text.Contains("/16") && reference.Text.Contains("/17"));
            Check("lookup execute buttons use compact widths", ((Button)Get(form, "macLookupButton")).Width <= 200 && ((Button)Get(form, "wanLookupButton")).Width <= 170);
            Button converterButton = controls.OfType<Button>().FirstOrDefault(b => b.Text == "Convert");
            Check("calculator Convert button is compact", converterButton != null && converterButton.Width <= 160 && converterButton.Height <= 42);

            tabs.SelectedTab = tabs.TabPages.Cast<TabPage>().First(t => t.Text == "Live Ping"); Pump(100);
            var target = (TextBox)Get(form, "targetInput"); target.Text = "127.0.0.1 Localhost";
            ((NumericUpDown)Get(form, "pingInterval")).Value = 250;
            var continuousHistory = (BindingSource)Get(form, "pingHistorySource");
            Check("history starts hidden until row click", continuousHistory.Count == 0);
            ((Button)Get(form, "pingStartButton")).PerformClick(); Pump(1300);
            Button pingPause = (Button)Get(form, "pingPauseButton");
            Button pingStart = (Button)Get(form, "pingStartButton");
            Button pingStop = (Button)Get(form, "pingStopButton");
            Check("Live Ping action buttons have equal stable widths", pingStart.Width == pingPause.Width && pingPause.Width == pingStop.Width);
            Check("all Live Ping buttons match START width", Flat(tabs.SelectedTab).OfType<Button>().All(b => b.Width == pingStart.Width));
            Check("Live Ping running button states are explicit", pingStart.Text == "MONITORING" && pingPause.Text == "PAUSE" && pingStop.Text == "STOP NOW");
            Check("ICMP success status is protocol specific", Convert.ToString(((DataTable)Get(form, "pingTable")).Rows[0]["Status"]) == "ICMP OK");
            pingPause.PerformClick(); Pump(350);
            long pausedSamples = ((DataTable)Get(form, "pingTable")).Rows.Cast<DataRow>().Sum(r => Convert.ToInt64(r["Sent"]));
            Pump(350);
            long pausedSamplesAfter = ((DataTable)Get(form, "pingTable")).Rows.Cast<DataRow>().Sum(r => Convert.ToInt64(r["Sent"]));
            Check("Live Ping pause freezes new polling", pingPause.Text == "RESUME" && pausedSamplesAfter == pausedSamples);
            pingPause.PerformClick(); Pump(350);
            Check("Live Ping resumes after pause", pingPause.Text == "PAUSE" && ((DataTable)Get(form, "pingTable")).Rows.Cast<DataRow>().Sum(r => Convert.ToInt64(r["Sent"])) > pausedSamplesAfter);
            Check("active Live Ping tab uses owner-drawn running symbol", tabs.DrawMode == TabDrawMode.OwnerDrawFixed && tabs.SelectedTab.Text.StartsWith("\u25CF"));
            ((Button)Get(form, "pingStopButton")).PerformClick(); Pump(600);
            Check("Live Ping running symbol clears after stop", tabs.SelectedTab.Text == "Live Ping");
            var historyTable = (DataTable)Get(form, "pingHistoryTable");
            Check("ping probes retained in history table", historyTable.Rows.Count > 0);
            Invoke(form, "SelectPingHistory", null, new DataGridViewCellEventArgs(0, 0)); Pump(100);
            Check("click target reveals only its history", continuousHistory.Count > 0 && continuousHistory.Cast<DataRowView>().All(v => Convert.ToString(v["Host"]) == "127.0.0.1"));
            var historyGrid = (DataGridView)Get(form, "pingHistoryGrid");
            for (int i = 0; i < 45; i++)
                Invoke(form, "UpdatePingRow", "127.0.0.1", "127.0.0.1", true, 1L, 128, "Synthetic scroll test", DateTime.Now.AddMilliseconds(i));
            historyGrid.FirstDisplayedScrollingRowIndex = 0;
            Invoke(form, "UpdatePingRow", "127.0.0.1", "127.0.0.1", true, 1L, 128, "New sample", DateTime.Now);
            Pump(100);
            Check("ping history refresh preserves manual scroll", historyGrid.FirstDisplayedScrollingRowIndex == 0);
            ((Button)Get(form, "pingStartButton")).PerformClick();
            Check("new monitoring session resets ping history", historyTable.Rows.Count == 0 && continuousHistory.Count == 0);
            ((Button)Get(form, "pingStopButton")).PerformClick(); Pump(500);
            var pingRoot = (TableLayoutPanel)Get(form, "pingRoot");
            Check("status cards compact", pingRoot.RowStyles[0].Height <= 64);
            Check("custom column action exists", controls.OfType<Button>().Any(b => b.Text == "Columns"));
            Check("ping history clear-log removed", !Flat(tabs.SelectedTab).OfType<Button>().Any(b => b.Text == "Clear log"));
            ComboBox pingProtocol = (ComboBox)Get(form, "pingProtocol");
            NumericUpDown pingPort = (NumericUpDown)Get(form, "pingPort");
            Check("Live Ping uses direct ICMP TCP selection", Get(form, "pingSourceIp") != null
                && pingProtocol.Items.Cast<object>().Select(Convert.ToString).SequenceEqual(new[] { "ICMP", "TCP" })
                && !Flat(tabs.SelectedTab).OfType<CheckBox>().Any(c => c.Text.IndexOf("Advanced", StringComparison.OrdinalIgnoreCase) >= 0));
            Check("Live Ping packet size is a main input", Get(form, "pingPacketSize") is NumericUpDown);
            pingProtocol.SelectedItem = "ICMP"; Pump(50); bool icmpPortDisabled = !pingPort.Enabled;
            pingProtocol.SelectedItem = "TCP"; Pump(50);
            Check("custom port enables only for TCP", icmpPortDisabled && pingPort.Enabled);
            Check("fixed cadence overlap is bounded", Convert.ToInt32(Invoke(form, "MaxOutstandingPollsV105", 1000, 250)) == 6
                && Convert.ToInt32(Invoke(form, "MaxOutstandingPollsV105", 1000, 1000)) == 3);
            Check("Live Ping result begins with sequence and uses full-row selection", ((DataGridView)Get(form, "pingGrid")).Columns[0].Name == "Seq"
                && ((DataGridView)Get(form, "pingGrid")).SelectionMode == DataGridViewSelectionMode.FullRowSelect);
            var tcpListener = new TcpListener(IPAddress.Loopback, 0); tcpListener.Start();
            int tcpProbePort = ((IPEndPoint)tcpListener.LocalEndpoint).Port;
            Task tcpAccept = Task.Run(delegate { using (TcpClient client = tcpListener.AcceptTcpClient()) { } tcpListener.Stop(); });
            Task tcpProbe = (Task)Invoke(form, "ProbeTcpAsync", IPAddress.Loopback, tcpProbePort, 1500, "", CancellationToken.None);
            while (!tcpProbe.IsCompleted) Pump(25);
            object tcpProbeResult = tcpProbe.GetType().GetProperty("Result").GetValue(tcpProbe, null);
            Check("advanced TCP probe validates the selected service port", Convert.ToBoolean(tcpProbeResult.GetType().GetField("Reachable").GetValue(tcpProbeResult)));
            tcpAccept.Wait(1500);
            Type pingUpdateType = typeof(MainForm).Assembly.GetType("NetStuck.PingUiUpdate");
            object tcpUiUpdate = Activator.CreateInstance(pingUpdateType);
            foreach (var pair in new Dictionary<string, object> {
                { "Host", "127.0.0.1" }, { "Resolved", "127.0.0.1" }, { "Up", true }, { "Latency", 2L }, { "Ttl", 0 },
                { "Detail", "TCP connected" }, { "EventTime", DateTime.Now }, { "SourceIp", "127.0.0.1" }, { "Protocol", "TCP" }, { "Port", tcpProbePort }
            }) pingUpdateType.GetField(pair.Key).SetValue(tcpUiUpdate, pair.Value);
            Invoke(form, "UpdatePingRowV103", tcpUiUpdate);
            Check("TCP success status is Connected", Convert.ToString(((DataTable)Get(form, "pingTable")).Rows[0]["Status"]) == "Connected");

            var udpServer = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
            int udpProbePort = ((IPEndPoint)udpServer.Client.LocalEndPoint).Port;
            Task udpReply = Task.Run(delegate
            {
                IPEndPoint senderEndpoint = new IPEndPoint(IPAddress.Any, 0);
                byte[] request = udpServer.Receive(ref senderEndpoint);
                udpServer.Send(request, request.Length, senderEndpoint); udpServer.Close();
            });
            Task udpProbe = (Task)Invoke(form, "ProbeUdpAsync", IPAddress.Loopback, udpProbePort, 32, 1500, "", CancellationToken.None);
            while (!udpProbe.IsCompleted) Pump(25);
            object udpProbeResult = udpProbe.GetType().GetProperty("Result").GetValue(udpProbe, null);
            Check("advanced UDP probe requires and measures a real response", Convert.ToBoolean(udpProbeResult.GetType().GetField("Reachable").GetValue(udpProbeResult)));
            udpReply.Wait(1500);

            tabs.SelectedTab = tabs.TabPages.Cast<TabPage>().First(t => t.Text == "Traceroute"); Pump(100);
            TabControl traceSessions = Flat(tabs.SelectedTab).OfType<TabControl>().First(t => t.TabPages.Count == 2);
            Check("Traceroute has exactly two independent session tabs", traceSessions.TabPages.Count == 2);
            traceSessions.SelectedIndex = 0; Pump(100);
            Check("Traceroute removes visible custom DNS and manual continuous/resolve options", !Flat(tabs.SelectedTab).OfType<CheckBox>().Any(c =>
                c.Text.IndexOf("Custom DNS", StringComparison.OrdinalIgnoreCase) >= 0 || c.Text.IndexOf("Continuous", StringComparison.OrdinalIgnoreCase) >= 0 || c.Text.IndexOf("Resolve", StringComparison.OrdinalIgnoreCase) >= 0));
            var traceTarget = (ComboBox)Get(form, "traceTarget"); traceTarget.Text = "127.0.0.1";
            Set(form, "traceHopInfoText", "127.0.0.1 Local-Loopback-Hop");
            ((NumericUpDown)Get(form, "traceInterval")).Value = 250;
            ((Button)Get(form, "traceStartButton")).PerformClick(); Pump(1800);
            var traceTable = (DataTable)Get(form, "traceTable");
            Check("traceroute loopback stops at target", traceTable.Rows.Count == 1 && Convert.ToInt32(traceTable.Rows[0]["Hop"]) == 1);
            Check("traceroute latency measured", traceTable.Rows.Count == 1 && traceTable.Rows[0]["LastMs"] != DBNull.Value && Convert.ToDouble(traceTable.Rows[0]["LastMs"]) > 0);
            Check("hop description mapping appears in result", traceTable.Rows.Count == 1 && Convert.ToString(traceTable.Rows[0]["Description"]) == "Local-Loopback-Hop");
            Check("traceroute has Description column", ((DataGridView)Get(form, "traceGrid")).Columns.Contains("Description"));
            Check("trace target remembered in dropdown", traceTarget.Items.Cast<object>().Any(x => Convert.ToString(x) == "127.0.0.1"));
            object traceSessionList = Get(form, "traceSessionsV103");
            object primarySession = ((System.Collections.IEnumerable)traceSessionList).Cast<object>().First();
            DataTable traceEvents = (DataTable)primarySession.GetType().GetField("EventTable").GetValue(primarySession);
            Button tracePause = (Button)primarySession.GetType().GetField("Pause").GetValue(primarySession);
            tracePause.PerformClick(); Pump(300);
            int traceSentPaused = traceTable.Rows.Cast<DataRow>().Sum(r => Convert.ToInt32(r["Sent"]));
            Pump(350);
            Check("Traceroute pause freezes session polling", tracePause.Text == "RESUME" && traceTable.Rows.Cast<DataRow>().Sum(r => Convert.ToInt32(r["Sent"])) == traceSentPaused);
            tracePause.PerformClick(); Pump(300);
            Check("Traceroute Event Log records lifecycle events", traceEvents.Rows.Count > 0 && traceEvents.Rows.Cast<DataRow>().Any(r => Convert.ToString(r["Type"]) == "Info"));
            Check("Traceroute Event Log is filterable and resizable", primarySession.GetType().GetField("EventFilter").GetValue(primarySession) is ComboBox
                && Flat(traceSessions.TabPages[0]).OfType<SplitContainer>().Any(s => s.Orientation == Orientation.Vertical));
            Check("Traceroute supports ICMP TCP UDP and packet size", ((ComboBox)primarySession.GetType().GetField("Protocol").GetValue(primarySession)).Items.Count == 3
                && primarySession.GetType().GetField("PacketSize").GetValue(primarySession) is NumericUpDown);
            Button traceStartState = (Button)primarySession.GetType().GetField("Start").GetValue(primarySession);
            Button traceStopState = (Button)primarySession.GetType().GetField("Stop").GetValue(primarySession);
            ComboBox traceSessionTarget = (ComboBox)primarySession.GetType().GetField("Target").GetValue(primarySession);
            ComboBox traceProtocol = (ComboBox)primarySession.GetType().GetField("Protocol").GetValue(primarySession);
            NumericUpDown tracePort = (NumericUpDown)primarySession.GetType().GetField("Port").GetValue(primarySession);
            NumericUpDown tracePacketSize = (NumericUpDown)primarySession.GetType().GetField("PacketSize").GetValue(primarySession);
            NumericUpDown traceMaxHops = (NumericUpDown)primarySession.GetType().GetField("MaxHops").GetValue(primarySession);
            NumericUpDown traceTimeout = (NumericUpDown)primarySession.GetType().GetField("Timeout").GetValue(primarySession);
            NumericUpDown traceInterval = (NumericUpDown)primarySession.GetType().GetField("Interval").GetValue(primarySession);
            Check("Traceroute action buttons have equal stable widths", traceStartState.Width == tracePause.Width && tracePause.Width == traceStopState.Width);
            TableLayoutPanel traceControlPanel = Flat(traceSessions.TabPages[0]).OfType<TableLayoutPanel>()
                .FirstOrDefault(panel => Convert.ToString(panel.Tag) == "TraceControlPanelV123");
            Check("Traceroute uses one bordered responsive control panel", traceControlPanel != null && traceControlPanel.RowCount == 3
                && traceControlPanel.BackColor == Color.FromArgb(248, 250, 252) && traceProtocol.DropDownWidth >= 100);
            TableLayoutPanel timingPanel = Flat(traceSessions.TabPages[0]).OfType<TableLayoutPanel>()
                .FirstOrDefault(panel => Convert.ToString(panel.Tag) == "TracePrimaryInputGridV123");
            TableLayoutPanel servicePanel = Flat(traceSessions.TabPages[0]).OfType<TableLayoutPanel>()
                .FirstOrDefault(panel => Convert.ToString(panel.Tag) == "TraceServiceInputGridV123");
            Check("Traceroute actions use a separate non-overlapping row", traceStartState.Parent.Parent == traceControlPanel
                && !timingPanel.Bounds.IntersectsWith(traceStartState.Parent.Bounds));
            Control[] firstRowFields = new Control[] { traceSessionTarget.Parent.Parent, traceMaxHops.Parent.Parent, traceTimeout.Parent.Parent, traceInterval.Parent.Parent };
            Control[] secondRowFields = new Control[] { traceProtocol.Parent.Parent, tracePort.Parent.Parent, tracePacketSize.Parent.Parent };
            Check("Traceroute inputs are adjacent and never overlap", timingPanel != null && servicePanel != null
                && AdjacentWithoutOverlap(firstRowFields) && AdjacentWithoutOverlap(secondRowFields)
                && timingPanel.Bottom <= servicePanel.Top);
            Check("Traceroute protocol dropdown has no timing input below it", ScreenBounds(traceProtocol.Parent.Parent).Top >= ScreenBounds(traceMaxHops.Parent.Parent).Bottom
                && ScreenBounds(traceProtocol.Parent.Parent).Top >= ScreenBounds(traceTimeout.Parent.Parent).Bottom
                && ScreenBounds(traceProtocol.Parent.Parent).Top >= ScreenBounds(traceInterval.Parent.Parent).Bottom);
            Check("Traceroute Protocol Port and Packet Size have complete border frames", new Control[] { traceProtocol, tracePort, tracePacketSize }.All(input =>
                input.Parent != null && Convert.ToString(input.Parent.Tag) == "TraceInputFrame" && input.Parent.Padding.All == 2
                && input.Left >= 2 && input.Top >= 2 && input.Right <= input.Parent.ClientSize.Width - 2 && input.Bottom <= input.Parent.ClientSize.Height - 2));
            Check("Traceroute input backgrounds remain consistent while disabled", new Control[] { traceProtocol, tracePort, tracePacketSize }.All(input =>
                input.BackColor == Color.White && input.Parent.BackColor == Color.White));
            form.Width = 1100; Pump(150);
            SplitContainer narrowTraceSplit = Flat(traceSessions.TabPages[0]).OfType<SplitContainer>().First(split => split.Orientation == Orientation.Vertical);
            Check("Traceroute remains separated without overlap at minimum window width", narrowTraceSplit.Panel1.ClientSize.Width >= 690
                && !timingPanel.Bounds.IntersectsWith(traceStartState.Parent.Bounds));
            form.Width = 1460; Pump(150);
            Check("Traceroute Protocol Port and Packet Size fields are balanced", traceProtocol.Parent.Parent.Width == tracePort.Parent.Parent.Width
                && tracePort.Parent.Parent.Width == tracePacketSize.Parent.Parent.Width);
            Check("Traceroute running button states are explicit", traceStartState.Text == "MONITORING" && tracePause.Text == "PAUSE" && traceStopState.Text == "STOP NOW");
            Check("public-hop detection excludes private ranges", Convert.ToBoolean(Invoke(form, "IsPublicAddressV104", "8.8.8.8"))
                && !Convert.ToBoolean(Invoke(form, "IsPublicAddressV104", "10.10.10.10")));
            var providerCache = (IDictionary<string, string>)Get(form, "traceProviderCacheV104");
            providerCache["8.8.8.8"] = "Google LLC (AS15169)";
            Check("public-hop provider populates Description", Convert.ToString(Invoke(form, "GetTraceDescriptionV104", "8.8.8.8")).Contains("Google LLC"));
            int[] initialAdaptive = (int[])Invoke(form, "BuildAdaptiveTraceTtlsV120", primarySession, 1, 24);
            primarySession.GetType().GetField("KnownDestinationHop").SetValue(primarySession, 12);
            primarySession.GetType().GetField("LastFullProbeCycle").SetValue(primarySession, 3);
            int[] stableAdaptive = (int[])Invoke(form, "BuildAdaptiveTraceTtlsV120", primarySession, 4, 12);
            Check("adaptive TTL polling keeps destination while reducing stable probes", initialAdaptive.Length == 24
                && stableAdaptive.Contains(12) && stableAdaptive.Length < 12);
            var traceGrid = (DataGridView)Get(form, "traceGrid");
            traceGrid.CurrentCell = traceGrid.Rows[0].Cells["Address"];
            traceGrid.BeginEdit(true);
            var selectableEditor = traceGrid.EditingControl as TextBox;
            if (selectableEditor != null) selectableEditor.Select(0, Math.Min(3, selectableEditor.TextLength));
            Check("result-grid text is mouse selectable and copyable", selectableEditor != null && selectableEditor.ReadOnly && selectableEditor.SelectionLength > 0);
            traceGrid.EndEdit();
            ((Button)primarySession.GetType().GetField("Stop").GetValue(primarySession)).PerformClick(); Pump(500);
            for (int hop = 2; hop <= 30; hop++)
            {
                DataRow row = traceTable.NewRow(); row["Hop"] = hop; row["Address"] = "10.0.0." + hop; row["Status"] = "Reply"; traceTable.Rows.Add(row);
            }
            Pump(100);
            traceGrid.CurrentCell = traceGrid.Rows[0].Cells["Address"];
            traceGrid.FirstDisplayedScrollingRowIndex = 10;
            traceGrid.HorizontalScrollingOffset = 80;
            object traceGridPosition = Invoke(form, "CaptureTraceGridPositionV110", traceGrid);
            ((BindingSource)primarySession.GetType().GetField("Source").GetValue(primarySession)).ResetBindings(false);
            Invoke(form, "RestoreTraceGridPositionV110", traceGrid, traceGridPosition);
            Check("Traceroute polling refresh preserves manual vertical and horizontal scroll", traceGrid.FirstDisplayedScrollingRowIndex == 10
                && traceGrid.HorizontalScrollingOffset == 80);

            tabs.SelectedTab = tabs.TabPages.Cast<TabPage>().First(t => t.Text == "DNS Resolver"); Pump(100);
            Check("DNS action text is centered", ((Button)Get(form, "dnsResolveButton")).TextAlign == ContentAlignment.MiddleCenter
                && ((Button)Get(form, "dnsPollButton")).TextAlign == ContentAlignment.MiddleCenter
                && ((Button)Get(form, "dnsResolveButton")).Height <= 42);
            ((TextBox)Get(form, "dnsInput")).Text = "localhost Local forward\r\n127.0.0.1 Local reverse";
            Invoke(form, "ResolveDnsOnce", null, EventArgs.Empty);
            var dnsTable = (DataTable)Get(form, "dnsTable");
            Check("DNS rows appear immediately while searching", dnsTable.Rows.Count == 2);
            PumpUntil(delegate { return dnsTable.Rows.Count == 2 && dnsTable.Rows.Cast<DataRow>().All(r => Convert.ToString(r["Status"]) != "Searching"); }, 5000);
            Check("DNS forward and reverse resolve", dnsTable.Rows.Cast<DataRow>().Any(r => Convert.ToString(r["Type"]) == "PTR" && Convert.ToString(r["Status"]) == "OK")
                && dnsTable.Rows.Cast<DataRow>().Any(r => Convert.ToString(r["Type"]) == "A / AAAA" && Convert.ToString(r["Status"]) == "OK"));
            Check("DNS query latency recorded", dnsTable.Rows.Cast<DataRow>().All(r => r["LatencyMs"] != DBNull.Value && Convert.ToDouble(r["LatencyMs"]) > 0));
            ((NumericUpDown)Get(form, "dnsPollInterval")).Value = 250;
            Invoke(form, "StartDnsPolling", null, EventArgs.Empty);
            Pump(1000);
            Check("DNS continuous polling accumulates samples", dnsTable.Rows.Cast<DataRow>().All(r => Convert.ToInt32(r["PollCount"]) >= 2));
            Invoke(form, "StopDnsPolling", null, EventArgs.Empty); Pump(350);

            tabs.SelectedTab = tabs.TabPages.Cast<TabPage>().First(t => t.Text == "MAC / WAN Lookup"); Pump(100);
            var vendorCache = (IDictionary<string, string>)Get(form, "macVendorCache");
            vendorCache["001122"] = "NetStuck Test Vendor";
            ((TextBox)Get(form, "macInput")).Text = "0011.2233.4455\r\n00:11:22:AA:BB:CC\r\n02-00-00-00-00-01\r\n01:00:5E:00:00:01";
            Invoke(form, "LookupMac", null, EventArgs.Empty); Pump(150);
            var macTable = (DataTable)Get(form, "macTable");
            Check("MAC lookup reuses one cached OUI without API errors", macTable.Rows.Count == 4
                && macTable.Rows.Cast<DataRow>().Count(r => Convert.ToString(r["Status"]) == "CACHED") == 2
                && !macTable.Rows.Cast<DataRow>().Any(r => Convert.ToString(r["Status"]) == "ERROR"));
            Check("local and multicast MACs avoid vendor API", macTable.Rows.Cast<DataRow>().Any(r => Convert.ToString(r["Status"]) == "LOCAL")
                && macTable.Rows.Cast<DataRow>().Any(r => Convert.ToString(r["Status"]) == "SKIPPED"));

            tabs.SelectedTab = tabs.TabPages.Cast<TabPage>().First(t => t.Text == "Config Collector"); Pump(150);
            SplitContainer collectorSplit = Flat(tabs.SelectedTab).OfType<SplitContainer>().OrderByDescending(s => s.ClientSize.Width).First();
            Check("collector config panel usable", collectorSplit.SplitterDistance >= 420);
            Check("SSH and Telnet available", ((ComboBox)Get(form, "collectorProtocol")).Items.Count == 2);
            Check("collector supports two auth slots", Get(form, "collectorAuth1Pass") != null && Get(form, "collectorAuth2Pass") != null);
            ((TextBox)Get(form, "collectorAuth1User")).Text = "first-user";
            ((TextBox)Get(form, "collectorAuth2User")).Text = "second-user";
            ((CheckBox)Get(form, "collectorUseAuth1")).Checked = true;
            ((CheckBox)Get(form, "collectorUseAuth2")).Checked = true;
            var orderedCredentials = ((System.Collections.IEnumerable)Invoke(form, "GetCollectorCredentials", 0)).Cast<object>().ToList();
            Check("collector credential fallback is AUTH1 then AUTH2", orderedCredentials.Count == 2
                && Convert.ToString(orderedCredentials[0].GetType().GetField("Name").GetValue(orderedCredentials[0])) == "AUTH1"
                && Convert.ToString(orderedCredentials[1].GetType().GetField("Name").GetValue(orderedCredentials[1])) == "AUTH2");
            ((TextBox)Get(form, "collectorDevices")).Text = "10.0.0.1:2222 Edge auth=2";
            var parsedDevices = ((System.Collections.IEnumerable)Invoke(form, "ParseCollectorDevices")).Cast<object>().ToList();
            Check("collector supports per-device port and auth override", parsedDevices.Count == 1
                && Convert.ToInt32(parsedDevices[0].GetType().GetField("Port").GetValue(parsedDevices[0])) == 2222
                && Convert.ToInt32(parsedDevices[0].GetType().GetField("AuthSlot").GetValue(parsedDevices[0])) == 2);
            Check("collector credentials are wide enough", ((TextBox)Get(form, "collectorAuth1Pass")).Width >= 125 && ((TextBox)Get(form, "collectorAuth2Pass")).Width >= 125);
            ((CheckBox)Get(form, "collectorShowPasswords")).Checked = true;
            Check("collector show-password control works", !((TextBox)Get(form, "collectorAuth1Pass")).UseSystemPasswordChar && !((TextBox)Get(form, "collectorAuth2Pass")).UseSystemPasswordChar);
            Check("collector preserves one domain-user backslash", Convert.ToString(Invoke(form, "QuoteArg", "company\\myname")) == "\"company\\myname\"");
            ((TextBox)Get(form, "collectorAuth1User")).Text = "company\\\\myname";
            ((TextBox)Get(form, "collectorAuth1Pass")).Text = "testpass";
            ((CheckBox)Get(form, "collectorUseAuth1")).Checked = true;
            ((CheckBox)Get(form, "collectorUseAuth2")).Checked = false;
            var sshCredentials = ((System.Collections.IEnumerable)Invoke(form, "GetCollectorCredentials", 0)).Cast<object>().ToList();
            string normalizedSshUser = Convert.ToString(sshCredentials[0].GetType().GetField("User").GetValue(sshCredentials[0]));
            Check("collector normalizes copied double separator to one literal backslash", normalizedSshUser == "company\\myname"
                && normalizedSshUser.Count(c => c == '\\') == 1);
            Set(form, "collectorPlinkOverride", Path.Combine(Environment.CurrentDirectory, "FakePlink.exe"));
            ((CheckBox)Get(form, "collectorStrictHostKey")).Checked = false;
            string authCountMarker = Path.Combine(Environment.CurrentDirectory, "fake-plink-auth-count.txt");
            if (File.Exists(authCountMarker)) File.Delete(authCountMarker);
            var sshTask = (Task<string>)Invoke(form, "RunSshCollectorAsync", "127.0.0.1", 22, sshCredentials[0], "show version", CancellationToken.None);
            while (!sshTask.IsCompleted) Pump(50);
            Check("collector uses one literal domain separator and keeps password out of argv", sshTask.Result.Contains("USER_BACKSLASHES=1")
                && sshTask.Result.Contains("KEYBOARD_INTERACTIVE_FALLBACK=OK"));
            Check("collector waits for the SSH prompt before each command", sshTask.Result.Contains("PROMPT_AWARE_COMMAND=OK"));
            Check("collector performs only one real authentication attempt per credential", File.Exists(authCountMarker) && File.ReadAllText(authCountMarker) == "1");
            string largeCapture = Path.Combine(Path.GetTempPath(), "NetStuck-large-capture-" + Guid.NewGuid().ToString("N") + ".tmp");
            long memoryBeforeLarge = Process.GetCurrentProcess().WorkingSet64;
            var largeTask = (Task<string>)Invoke(form, "RunSshCollectorToFileV120", "127.0.0.1", 22, sshCredentials[0], "show large", largeCapture, CancellationToken.None);
            while (!largeTask.IsCompleted) Pump(50);
            long memoryLargeGrowth = Process.GetCurrentProcess().WorkingSet64 - memoryBeforeLarge;
            Check("large SSH config streams to disk with bounded memory preview", File.Exists(largeCapture) && new FileInfo(largeCapture).Length > 2000000
                && largeTask.Result.Length <= 550000 && memoryLargeGrowth < 100L * 1024 * 1024);
            try { if (File.Exists(largeCapture)) File.Delete(largeCapture); } catch { }
            if (File.Exists(authCountMarker)) File.Delete(authCountMarker);
            string retryMarker = Path.Combine(Environment.CurrentDirectory, "fake-plink-transport-retry.marker");
            if (File.Exists(retryMarker)) File.Delete(retryMarker);
            var transportRetryTask = (Task)Invoke(form, "RunPlinkWithTransportRetryAsync",
                Path.Combine(Environment.CurrentDirectory, "FakePlink.exe"), "transport-retry", "", "127.0.0.1", CancellationToken.None);
            while (!transportRetryTask.IsCompleted) Pump(50);
            object transportResult = transportRetryTask.GetType().GetProperty("Result").GetValue(transportRetryTask, null);
            string transportOutput = Convert.ToString(transportResult.GetType().GetField("Output").GetValue(transportResult));
            Check("collector retries a transient SSH connection abort once", transportOutput.Contains("TRANSIENT_RETRY=OK"));
            Set(form, "collectorPlinkOverride", null);
            int mockPort;
            Task mockServer = StartMockTelnet(out mockPort);
            var telnetTask = (Task<string>)Invoke(form, "RunTelnetAsync", "127.0.0.1", mockPort, "testuser", "testpass", "show version", CancellationToken.None);
            while (!telnetTask.IsCompleted) Pump(50);
            mockServer.Wait();
            Check("collector Telnet transport integration", telnetTask.Result.Contains("MOCK CONFIG OUTPUT"));
            int fallbackPort;
            Task fallbackServer = StartMockCollectorFallback(out fallbackPort);
            string collectorOutputFolder = Path.Combine(Path.GetTempPath(), "NetStuck-Collector-Test-" + Guid.NewGuid().ToString("N"));
            ((ComboBox)Get(form, "collectorProtocol")).SelectedItem = "Telnet";
            ((NumericUpDown)Get(form, "collectorPort")).Value = fallbackPort;
            ((TextBox)Get(form, "collectorDevices")).Text = "127.0.0.1 Fallback-Test";
            ((TextBox)Get(form, "collectorBasic")).Text = "";
            ((TextBox)Get(form, "collectorCommands")).Text = "show version";
            ((TextBox)Get(form, "collectorAuth1User")).Text = "bad-user";
            ((TextBox)Get(form, "collectorAuth1Pass")).Text = "bad-password";
            ((TextBox)Get(form, "collectorAuth2User")).Text = "good-user";
            ((TextBox)Get(form, "collectorAuth2Pass")).Text = "good-password";
            ((CheckBox)Get(form, "collectorUseAuth1")).Checked = true;
            ((CheckBox)Get(form, "collectorUseAuth2")).Checked = true;
            ((TextBox)Get(form, "collectorFolderBox")).Text = collectorOutputFolder;
            ((Button)Get(form, "collectorStart")).PerformClick();
            Check("Config Collector tab shows a running symbol", tabs.SelectedTab.Text.StartsWith("\u25CF"));
            var collectorTable = (DataTable)Get(form, "collectorTable");
            PumpUntil(delegate { return collectorTable.Rows.Count == 1 && Convert.ToString(collectorTable.Rows[0]["Status"]) != "Running"; }, 30000);
            fallbackServer.Wait(2000);
            Check("collector falls back from AUTH1 to AUTH2 end-to-end", collectorTable.Rows.Count == 1
                && Convert.ToString(collectorTable.Rows[0]["Status"]) == "Completed"
                && Convert.ToString(collectorTable.Rows[0]["Detail"]).Contains("AUTH2"));
            PumpUntil(delegate { return Get(form, "collectorCancellation") == null; }, 5000);
            collectorTable.Rows.Add("10.0.0.9", "Failed", "SSH", "company\\engineer", "", "", "Authentication failed for CSV test");
            collectorTable.Rows.Add("10.0.0.10", "Completed", "SSH", "company\\engineer", "", "saved.txt", "Saved");
            ((RichTextBox)Get(form, "collectorTerminal")).AppendText("\r\n[10.0.0.9] FAILED: Authentication failed for diagnostic test");
            Invoke(form, "UpdateCollectorErrorExportStateV120");
            string collectorCsv = Convert.ToString(Invoke(form, "BuildCollectorErrorCsvV120"));
            Check("collector error CSV contains only requested fields and failed rows", ((Button)Get(form, "collectorExportLog")).Enabled
                && collectorCsv.StartsWith("IP,Status,Protocol,Username,Detail") && collectorCsv.Contains("10.0.0.9")
                && collectorCsv.Contains("company\\engineer") && collectorCsv.Contains("Authentication failed for CSV test")
                && !collectorCsv.Contains("10.0.0.10") && !collectorCsv.Contains("testpass"));
            int flushBefore = Convert.ToInt32(Get(form, "collectorTerminalFlushCountV120"));
            for (int i = 0; i < 1000; i++) Invoke(form, "AppendTerminal", "batch-line-" + i);
            Pump(300);
            int flushAfter = Convert.ToInt32(Get(form, "collectorTerminalFlushCountV120"));
            Check("collector terminal batches concurrent output", flushAfter - flushBefore < 40
                && ((RichTextBox)Get(form, "collectorTerminal")).Text.Contains("batch-line-999"));
            try { if (Directory.Exists(collectorOutputFolder)) Directory.Delete(collectorOutputFolder, true); } catch { }
            ((TextBox)Get(form, "collectorAuth1Pass")).Text = "DO_NOT_SAVE_THIS";
            ((TextBox)Get(form, "collectorAuth2Pass")).Text = "ALSO_SECRET";
            ((TextBox)Get(form, "collectorAuth1Secret")).Text = "ENABLE_SECRET";
            Invoke(form, "SaveAppState");
            string savedState = File.ReadAllText(state);
            Check("collector passwords never persisted", !savedState.Contains("DO_NOT_SAVE_THIS") && !savedState.Contains("ALSO_SECRET") && !savedState.Contains("ENABLE_SECRET"));
            Check("v.1.2.3 state schema is persisted", savedState.Contains("\"StateVersion\":6"));

            IDictionary providerEntries = (IDictionary)Get(form, "traceProviderEntriesV120");
            IDictionary dnsEntries = (IDictionary)Get(form, "traceDnsEntriesV120");
            Type cacheItemType = typeof(MainForm).Assembly.GetType("NetStuck.TraceLookupCacheItemV120");
            object providerItem = Activator.CreateInstance(cacheItemType);
            cacheItemType.GetProperty("Value").SetValue(providerItem, "Persistent Test ISP", null);
            cacheItemType.GetProperty("RetrievedUtc").SetValue(providerItem, DateTime.UtcNow, null);
            object dnsItem = Activator.CreateInstance(cacheItemType);
            cacheItemType.GetProperty("Value").SetValue(dnsItem, "cached-dns.test", null);
            cacheItemType.GetProperty("RetrievedUtc").SetValue(dnsItem, DateTime.UtcNow, null);
            providerEntries["8.8.4.4"] = providerItem; dnsEntries["8.8.4.4"] = dnsItem;
            Invoke(form, "SaveTraceLookupCacheV120");
            providerEntries.Clear(); dnsEntries.Clear(); providerCache.Clear();
            Invoke(form, "InitializeTraceLookupCacheV120");
            Check("ISP and DNS cache persists with TTL metadata", providerCache.ContainsKey("8.8.4.4")
                && Convert.ToString(providerCache["8.8.4.4"]) == "Persistent Test ISP" && dnsEntries.Contains("8.8.4.4"));

            float before = ((TextBox)Get(form, "collectorDevices")).Font.Size;
            Set(form, "zoomScale", 1.2f); Invoke(form, "ApplyZoom");
            Check("zoom changes result/input fonts", ((TextBox)Get(form, "collectorDevices")).Font.Size > before);

            ToolStripStatusLabel timeSource = (ToolStripStatusLabel)Get(form, "timeSourceStatus");
            Check("NTP first with local fallback", timeSource.Text.Contains("NTP") || timeSource.Text.Contains("Local fallback") || timeSource.Text.Contains("syncing"));
            form.Close();
        }
        if (File.Exists(state)) File.Delete(state);
        string traceCache = Path.Combine(Path.GetDirectoryName(state), "trace-lookups.json");
        if (File.Exists(traceCache)) File.Delete(traceCache);
        Environment.SetEnvironmentVariable("NETSTUCK_TEST_STATE_PATH", null);
        Console.WriteLine("Failures: " + failures);
        return failures == 0 ? 0 : 1;
    }

    static object Get(object target, string name)
    {
        FieldInfo f = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        if (f == null) throw new MissingFieldException(name);
        return f.GetValue(target);
    }

    static void Set(object target, string name, object value)
    {
        FieldInfo f = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        if (f == null) throw new MissingFieldException(name);
        f.SetValue(target, value);
    }

    static object Invoke(object target, string name, params object[] args)
    {
        MethodInfo m = target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic);
        if (m == null) throw new MissingMethodException(name);
        return m.Invoke(target, args);
    }

    static IEnumerable<Control> Flat(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (Control nested in Flat(child)) yield return nested;
        }
    }

    static bool AdjacentWithoutOverlap(Control[] fields)
    {
        if (fields == null || fields.Length < 2 || fields.Any(field => field == null)) return false;
        float dpi = 96f;
        try { using (Graphics graphics = fields[0].CreateGraphics()) dpi = graphics.DpiX; } catch { }
        int maximumGap = Math.Max(12, (int)Math.Ceiling(12f * dpi / 96f));
        Rectangle previous = ScreenBounds(fields[0]);
        for (int i = 1; i < fields.Length; i++)
        {
            Rectangle current = ScreenBounds(fields[i]);
            if (previous.Right > current.Left) return false;
            if (current.Left - previous.Right > maximumGap) return false;
            previous = current;
        }
        return true;
    }

    static Rectangle ScreenBounds(Control control)
    {
        return new Rectangle(control.PointToScreen(Point.Empty), control.Size);
    }

    static void Pump(int milliseconds)
    {
        DateTime until = DateTime.UtcNow.AddMilliseconds(milliseconds);
        while (DateTime.UtcNow < until) { Application.DoEvents(); Thread.Sleep(15); }
    }

    static void PumpUntil(Func<bool> condition, int timeoutMs)
    {
        DateTime until = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!condition() && DateTime.UtcNow < until) { Application.DoEvents(); Thread.Sleep(15); }
    }

    static Task StartMockTelnet(out int port)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        port = ((IPEndPoint)listener.LocalEndpoint).Port;
        return Task.Run(delegate
        {
            try
            {
                using (TcpClient client = listener.AcceptTcpClient())
                using (NetworkStream stream = client.GetStream())
                using (var reader = new StreamReader(stream, Encoding.ASCII))
                using (var writer = new StreamWriter(stream, Encoding.ASCII) { AutoFlush = true, NewLine = "\r\n" })
                {
                    writer.Write("Username: "); reader.ReadLine();
                    writer.Write("Password: "); reader.ReadLine();
                    writer.WriteLine("MOCK-SW#");
                    string command;
                    while ((command = reader.ReadLine()) != null)
                    {
                        if (command.Trim().Equals("exit", StringComparison.OrdinalIgnoreCase)) { writer.WriteLine("Bye"); break; }
                        writer.WriteLine("MOCK CONFIG OUTPUT for " + command.Trim());
                        writer.WriteLine("MOCK-SW#");
                    }
                }
            }
            finally { listener.Stop(); }
        });
    }

    static Task StartMockCollectorFallback(out int port)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        port = ((IPEndPoint)listener.LocalEndpoint).Port;
        return Task.Run(delegate
        {
            try
            {
                for (int attempt = 0; attempt < 2; attempt++)
                {
                    using (TcpClient client = listener.AcceptTcpClient())
                    using (NetworkStream stream = client.GetStream())
                    using (var reader = new StreamReader(stream, Encoding.ASCII))
                    using (var writer = new StreamWriter(stream, Encoding.ASCII) { AutoFlush = true, NewLine = "\r\n" })
                    {
                        writer.Write("Username: ");
                        string user = reader.ReadLine();
                        writer.Write("Password: ");
                        reader.ReadLine();
                        if (!String.Equals(user, "good-user", StringComparison.Ordinal))
                        {
                            writer.WriteLine("Login incorrect");
                            continue;
                        }
                        writer.WriteLine("MOCK-SW#");
                        string command;
                        while ((command = reader.ReadLine()) != null)
                        {
                            if (command.Trim().Equals("exit", StringComparison.OrdinalIgnoreCase)) { writer.WriteLine("Bye"); break; }
                            writer.WriteLine("MOCK CONFIG OUTPUT for " + command.Trim());
                            writer.WriteLine("MOCK-SW#");
                        }
                    }
                }
            }
            finally { listener.Stop(); }
        });
    }
}
