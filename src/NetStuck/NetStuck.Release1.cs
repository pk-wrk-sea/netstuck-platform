using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace NetStuck
{
    sealed class DnsProbeResult
    {
        public TargetSpec Query;
        public string Type;
        public string Status;
        public string Answer;
        public double LatencyMs;
    }

    sealed class PingUiUpdate
    {
        public string Host;
        public string Resolved;
        public string SourceIp;
        public string Protocol;
        public int Port;
        public bool Up;
        public long Latency;
        public int Ttl;
        public string Detail;
        public DateTime EventTime;
        public long Order;
    }

    public sealed partial class MainForm
    {
        Font gridBoldFont;
        readonly Dictionary<string, string> traceHopDescriptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        DateTime lastPingMetricsUpdate = DateTime.MinValue;
        string selectedPingHistoryHost = "";
        readonly ConcurrentQueue<PingUiUpdate> pingUiUpdates = new ConcurrentQueue<PingUiUpdate>();
        System.Windows.Forms.Timer pingUiTimer;
        DateTime lastPingBindingReset = DateTime.MinValue;
        DateTime lastPingGridInvalidate = DateTime.MinValue;
        bool stopPingUiTimerAfterDrain;
        volatile bool appClosing;
        readonly Dictionary<string, long> latestPingOrderV105 = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

        void ConfigureFastGrid(DataGridView grid)
        {
            try
            {
                typeof(DataGridView).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(grid, true, null);
            }
            catch { }
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
        }

        void MakeGridEditorSelectable(object sender, DataGridViewEditingControlShowingEventArgs e)
        {
            var editor = e.Control as TextBox;
            if (editor == null) return;
            editor.ReadOnly = true;
            editor.ShortcutsEnabled = true;
            editor.BorderStyle = BorderStyle.None;
            editor.Cursor = Cursors.IBeam;
        }

        void SetTabActivity(string pageName, bool active)
        {
            TabPage page;
            if (!pagesByName.TryGetValue(pageName, out page)) return;
            int count;
            if (!tabActivityCounts.TryGetValue(pageName, out count)) count = 0;
            count = active ? count + 1 : Math.Max(0, count - 1);
            tabActivityCounts[pageName] = count;
            page.Text = count > 0 ? "\u25CF  " + pageName : pageName;
            page.ToolTipText = count > 0 ? pageName + " is running" : "";
            tabs.Invalidate();
        }

        void DrawMainTab(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= tabs.TabPages.Count) return;
            TabPage page = tabs.TabPages[e.Index];
            bool active = page.Text.StartsWith("\u25CF", StringComparison.Ordinal);
            string caption = active ? page.Text.Substring(1).TrimStart() : page.Text;
            Rectangle bounds = e.Bounds;
            bool selected = e.Index == tabs.SelectedIndex;
            using (var background = new SolidBrush(selected ? Surface : Canvas)) e.Graphics.FillRectangle(background, bounds);
            int textX = bounds.Left + 9;
            if (active)
            {
                int dotSize = 8;
                using (var brush = new SolidBrush(Success))
                    e.Graphics.FillEllipse(brush, bounds.Left + 8, bounds.Top + (bounds.Height - dotSize) / 2, dotSize, dotSize);
                textX += 14;
            }
            TextRenderer.DrawText(e.Graphics, caption, tabs.Font,
                new Rectangle(textX, bounds.Top + 1, Math.Max(1, bounds.Right - textX - 5), bounds.Height - 2),
                selected ? TextMain : TextMuted, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            if (selected)
            {
                using (var pen = new Pen(Accent, 2f)) e.Graphics.DrawLine(pen, bounds.Left + 2, bounds.Bottom - 2, bounds.Right - 2, bounds.Bottom - 2);
            }
        }

        static string FormatMsValue(object value)
        {
            return Convert.ToDouble(value).ToString("0.##") + " ms";
        }

        static bool IsSuccessfulPingStatus(string status)
        {
            return status == "Reachable" || status == "ICMP OK" || status == "Connected";
        }

        static bool IsFailedPingStatus(string status)
        {
            return status == "Unreachable" || status == "TCP Timeout";
        }

        void DrainPingUpdates()
        {
            if (appClosing || IsDisposed || Disposing || pingTable == null) return;
            PingUiUpdate update;
            int processed = 0;
            int queued = pingUiUpdates.Count;
            int batchLimit = queued > 2048 ? 256 : queued > 512 ? 128 : 64;
            pingGrid.SuspendLayout();
            pingSource.RaiseListChangedEvents = false;
            try
            {
                while (!appClosing && !IsDisposed && !Disposing && processed < batchLimit && pingUiUpdates.TryDequeue(out update))
                {
                    UpdatePingRowV103(update);
                    processed++;
                }
            }
            finally
            {
                pingSource.RaiseListChangedEvents = true;
            }
            if (processed > 0)
            {
                bool needsRebind = !String.IsNullOrWhiteSpace(pingSource.Filter) || !String.IsNullOrWhiteSpace(pingSource.Sort);
                if (needsRebind && (DateTime.UtcNow - lastPingBindingReset).TotalMilliseconds >= 500)
                {
                    lastPingBindingReset = DateTime.UtcNow;
                    pingSource.ResetBindings(false);
                }
            }
            pingGrid.ResumeLayout();
            if (processed > 0 && (DateTime.UtcNow - lastPingGridInvalidate).TotalMilliseconds >= 100)
            {
                lastPingGridInvalidate = DateTime.UtcNow;
                pingGrid.Invalidate();
            }
        }

        void ShowHopDescriptions(object sender, EventArgs e)
        {
            using (var dialog = new Form
            {
                Text = "Traceroute hop descriptions", Width = 620, Height = 520,
                StartPosition = FormStartPosition.CenterParent, BackColor = Canvas, Font = Font,
                MinimizeBox = false, MaximizeBox = false, ShowInTaskbar = false
            })
            {
                var hint = new Label
                {
                    Dock = DockStyle.Top, Height = 58, ForeColor = TextMuted,
                    Text = "One mapping per line: 10.10.10.10 Firewall-User-Inside\r\nThe description appears automatically whenever that hop is detected.",
                    Padding = new Padding(12, 10, 12, 4)
                };
                var input = new TextBox
                {
                    Dock = DockStyle.Fill, Multiline = true, AcceptsTab = true, WordWrap = false,
                    ScrollBars = ScrollBars.Both, Font = new Font("Consolas", 10f),
                    Text = traceHopInfoText ?? ""
                };
                var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 52, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8) };
                var save = ActionButton("Save mappings", true, 120); save.DialogResult = DialogResult.OK;
                var cancel = ActionButton("Cancel", false, 90); cancel.DialogResult = DialogResult.Cancel;
                bar.Controls.Add(save); bar.Controls.Add(cancel);
                dialog.Controls.Add(input); dialog.Controls.Add(hint); dialog.Controls.Add(bar);
                dialog.AcceptButton = save; dialog.CancelButton = cancel;
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                traceHopInfoText = input.Text;
                RefreshHopDescriptions();
                foreach (TraceSessionV103 session in traceSessionsV103)
                    foreach (DataRow row in session.Table.Rows)
                        row["Description"] = GetTraceDescriptionV104(Convert.ToString(row["Address"]));
                SaveAppState();
            }
        }

        void RefreshHopDescriptions()
        {
            traceHopDescriptions.Clear();
            foreach (TargetSpec item in NetOpsCore.ParseTargets(traceHopInfoText ?? ""))
                if (!String.IsNullOrWhiteSpace(item.Description)) traceHopDescriptions[item.Host] = item.Description;
        }

        string GetHopDescription(string address)
        {
            string description;
            return !String.IsNullOrWhiteSpace(address) && traceHopDescriptions.TryGetValue(address, out description) ? description : "";
        }

        DataTable CreateDnsMonitorTable()
        {
            var t = new DataTable();
            t.Columns.Add("Query"); t.Columns.Add("Description"); t.Columns.Add("Type"); t.Columns.Add("Status"); t.Columns.Add("Answer");
            t.Columns.Add("LatencyMs", typeof(double)); t.Columns.Add("MinMs", typeof(double)); t.Columns.Add("AvgMs", typeof(double)); t.Columns.Add("MaxMs", typeof(double));
            t.Columns.Add("TotalMs", typeof(double)); t.Columns.Add("PollCount", typeof(int)); t.Columns.Add("Server"); t.Columns.Add("Updated");
            t.PrimaryKey = new[] { t.Columns["Query"] };
            return t;
        }

        async void ResolveDnsOnce(object sender, EventArgs e)
        {
            if (dnsCancellation != null) return;
            List<TargetSpec> queries;
            string server;
            if (!TryGetDnsInputs(out queries, out server)) return;
            SetDnsRunning(true, false);
            try { await RunDnsCycle(queries, server, true, CancellationToken.None); }
            finally { if (!appClosing && !IsDisposed && !Disposing) SetDnsRunning(false, false); }
        }

        async void StartDnsPolling(object sender, EventArgs e)
        {
            if (dnsCancellation != null) return;
            List<TargetSpec> queries;
            string server;
            if (!TryGetDnsInputs(out queries, out server)) return;
            dnsCancellation = new CancellationTokenSource();
            SetDnsRunning(true, true);
            bool first = true;
            try
            {
                while (!dnsCancellation.IsCancellationRequested)
                {
                    Stopwatch cycle = Stopwatch.StartNew();
                    await RunDnsCycle(queries, server, first, dnsCancellation.Token);
                    first = false;
                    int delay = Math.Max(0, (int)dnsPollInterval.Value - (int)cycle.ElapsedMilliseconds);
                    await Task.Delay(delay, dnsCancellation.Token);
                }
            }
            catch (OperationCanceledException) { }
            finally
            {
                dnsCancellation.Dispose(); dnsCancellation = null;
                if (!appClosing && !IsDisposed && !Disposing)
                {
                    SetDnsRunning(false, true);
                    Log("INFO", "DNS", "DNS polling stopped");
                }
            }
        }

        void StopDnsPolling(object sender, EventArgs e)
        {
            if (dnsCancellation == null) return;
            dnsStopButton.Enabled = false; dnsStopButton.Text = "Stopping…";
            dnsCancellation.Cancel();
        }

        bool TryGetDnsInputs(out List<TargetSpec> queries, out string server)
        {
            queries = NetOpsCore.ParseTargets(dnsInput.Text);
            server = dnsUseCustom.Checked ? dnsServer.Text.Trim() : "";
            if (queries.Count == 0) { MessageBox.Show(this, "Enter at least one domain name or IP address."); return false; }
            if (dnsUseCustom.Checked && !IsValidIp(server)) { MessageBox.Show(this, "Custom DNS must be a valid IP address."); return false; }
            return true;
        }

        void SetDnsRunning(bool running, bool polling)
        {
            SetTabActivity("DNS Resolver", running);
            dnsInput.ReadOnly = running; dnsUseCustom.Enabled = !running; dnsServer.Enabled = !running && dnsUseCustom.Checked; dnsPollInterval.Enabled = !running;
            dnsResolveButton.Enabled = !running; dnsPollButton.Enabled = !running; dnsStopButton.Enabled = running && polling;
            dnsPollButton.Text = running && polling ? "● POLLING" : "Start polling";
            dnsStopButton.Text = "STOP";
            appStatus.Text = running ? (polling ? "DNS polling active" : "Resolving DNS…") : "Ready";
        }

        async Task RunDnsCycle(List<TargetSpec> queries, string server, bool clear, CancellationToken token)
        {
            if (clear) dnsTable.Rows.Clear();
            string serverName = server.Length > 0 ? server : "System DNS";
            foreach (TargetSpec query in queries)
            {
                DataRow row = dnsTable.Rows.Find(query.Host);
                if (row == null)
                {
                    row = dnsTable.NewRow();
                    row["Query"] = query.Host; row["Description"] = query.Description; row["PollCount"] = 0; row["TotalMs"] = 0d;
                    dnsTable.Rows.Add(row);
                }
                row["Type"] = IsValidIp(query.Host) ? "PTR" : "A / AAAA";
                row["Status"] = "Searching"; row["Answer"] = "Query in progress…"; row["Server"] = serverName; row["Updated"] = DateTime.Now.ToString("HH:mm:ss");
            }
            dnsGrid.Invalidate();
            Task<DnsProbeResult>[] jobs = queries.Select(q => ProbeDnsAsync(q, server, token)).ToArray();
            DnsProbeResult[] results = await Task.WhenAll(jobs);
            foreach (DnsProbeResult result in results)
            {
                DataRow row = dnsTable.Rows.Find(result.Query.Host);
                if (row == null) continue;
                int count = Convert.ToInt32(row["PollCount"]) + 1;
                double total = Convert.ToDouble(row["TotalMs"]) + result.LatencyMs;
                double min = row.IsNull("MinMs") ? result.LatencyMs : Math.Min(Convert.ToDouble(row["MinMs"]), result.LatencyMs);
                double max = row.IsNull("MaxMs") ? result.LatencyMs : Math.Max(Convert.ToDouble(row["MaxMs"]), result.LatencyMs);
                row["Type"] = result.Type; row["Status"] = result.Status; row["Answer"] = result.Answer;
                row["LatencyMs"] = Math.Round(result.LatencyMs, 2); row["MinMs"] = Math.Round(min, 2); row["MaxMs"] = Math.Round(max, 2);
                row["TotalMs"] = total; row["AvgMs"] = Math.Round(total / count, 2); row["PollCount"] = count; row["Updated"] = DateTime.Now.ToString("HH:mm:ss");
            }
            dnsGrid.Invalidate();
            Log("INFO", "DNS", "Resolved " + queries.Count + " query(s)" + (server.Length > 0 ? " via " + server : ""));
        }

        async Task<DnsProbeResult> ProbeDnsAsync(TargetSpec query, string server, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            Stopwatch watch = Stopwatch.StartNew();
            var result = new DnsProbeResult { Query = query, Type = IsValidIp(query.Host) ? "PTR" : "A / AAAA" };
            try
            {
                IPAddress ip;
                if (IPAddress.TryParse(query.Host, out ip))
                {
                    string answer = await ResolveReverse(ip, server);
                    result.Status = answer.Length > 0 ? "OK" : "NOT FOUND";
                    result.Answer = answer.Length > 0 ? answer : "No PTR record";
                }
                else
                {
                    List<string> answers = await ResolveForward(query.Host, server);
                    result.Status = answers.Count > 0 ? "OK" : "NOT FOUND";
                    result.Answer = answers.Count > 0 ? String.Join(", ", answers) : "No answer";
                }
            }
            catch (Exception ex) { result.Status = "ERROR"; result.Answer = FriendlyError(ex); }
            watch.Stop(); result.LatencyMs = Math.Max(0.01, watch.Elapsed.TotalMilliseconds);
            return result;
        }

        void FormatDnsCell(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0) return;
            string property = dnsGrid.Columns[e.ColumnIndex].DataPropertyName;
            if ((property == "LatencyMs" || property == "MinMs" || property == "AvgMs" || property == "MaxMs") && e.Value != null && e.Value != DBNull.Value)
                e.Value = FormatMsValue(e.Value);
            if (property == "Status")
            {
                string status = Convert.ToString(e.Value);
                e.CellStyle.Font = gridBoldFont;
                e.CellStyle.ForeColor = status == "OK" ? Success : status == "Searching" ? Warning : status == "NOT FOUND" ? Warning : Danger;
            }
        }
    }
}
