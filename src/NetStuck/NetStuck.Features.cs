using System;
using System.Collections.Concurrent;
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
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace NetStuck
{
    sealed class AppState
    {
        public int StateVersion { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int Left { get; set; }
        public int Top { get; set; }
        public bool Maximized { get; set; }
        public int SelectedTab { get; set; }
        public string PingTargets { get; set; }
        public int PingInterval { get; set; }
        public int PingTimeout { get; set; }
        public bool PingCustomDns { get; set; }
        public string PingDns { get; set; }
        public bool PingCardsVisible { get; set; }
        public List<string> PingColumns { get; set; }
        public string PingSourceIp { get; set; }
        public bool PingAdvanced { get; set; }
        public string PingProtocol { get; set; }
        public int PingPort { get; set; }
        public int PingPacketSize { get; set; }
        public string TraceTarget { get; set; }
        public List<string> TraceHistory { get; set; }
        public int TraceMaxHops { get; set; }
        public int TraceTimeout { get; set; }
        public int TraceInterval { get; set; }
        public bool TraceContinuous { get; set; }
        public bool TraceResolve { get; set; }
        public bool TraceCustomDns { get; set; }
        public string TraceDns { get; set; }
        public string TraceHopInfo { get; set; }
        public string TraceTarget2 { get; set; }
        public List<string> TraceHistory2 { get; set; }
        public string TraceProtocol1 { get; set; }
        public string TraceProtocol2 { get; set; }
        public int TracePort1 { get; set; }
        public int TracePort2 { get; set; }
        public int TracePacketSize1 { get; set; }
        public int TracePacketSize2 { get; set; }
        public int TraceMaxHops2 { get; set; }
        public int TraceTimeout2 { get; set; }
        public int TraceInterval2 { get; set; }
        public int TraceSelectedSession { get; set; }
        public string DnsInput { get; set; }
        public bool DnsCustom { get; set; }
        public string DnsServer { get; set; }
        public int DnsPollInterval { get; set; }
        public string MacInput { get; set; }
        public string WanInput { get; set; }
        public string SubnetInput { get; set; }
        public string UnitValue { get; set; }
        public string UnitFrom { get; set; }
        public string UnitTo { get; set; }
        public string CollectorProtocol { get; set; }
        public int CollectorPort { get; set; }
        public int CollectorConcurrency { get; set; }
        public string CollectorDevices { get; set; }
        public string CollectorBasic { get; set; }
        public string CollectorCommands { get; set; }
        public string CollectorDeviceType { get; set; }
        public string CollectorAuth1User { get; set; }
        public string CollectorAuth2User { get; set; }
        public bool CollectorUseAuth1 { get; set; }
        public bool CollectorUseAuth2 { get; set; }
        public bool CollectorStrictHostKey { get; set; }
        public bool CollectorContinueError { get; set; }
        public bool CollectorJson { get; set; }
        public string CollectorFolder { get; set; }
        public float Zoom { get; set; }
    }

    sealed class CollectorDevice
    {
        public string Host;
        public string Description;
        public int AuthSlot;
        public int Port;
    }

    sealed class CollectorCredential
    {
        public int Slot;
        public string Name;
        public string User;
        public string Password;
        public string EnableSecret;
    }

    sealed class CollectorSession
    {
        public string Output;
        public string CredentialName;
        public string Username;
        public string CaptureFile;
    }

    sealed class CommandProcessResult
    {
        public int ExitCode;
        public string Output;
        public string Error;
    }

    sealed class CollectorAuthenticationException : Exception
    {
        public CollectorAuthenticationException(string message) : base(message) { }
    }

    sealed class CollectorTransportException : Exception
    {
        public CollectorTransportException(string message) : base(message) { }
    }

    sealed class CollectorPromptResult
    {
        public string Kind;
        public string Text;
    }

    sealed class CollectorCaptureBuffer : IDisposable
    {
        const int TailLimit = 524288;
        readonly object sync = new object();
        readonly StreamWriter writer;
        readonly StringBuilder tail = new StringBuilder();
        long length;

        public CollectorCaptureBuffer(string path)
        {
            writer = new StreamWriter(new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read,
                65536, FileOptions.SequentialScan), new UTF8Encoding(false), 65536);
        }

        public void Append(char[] buffer, int count)
        {
            if (count <= 0) return;
            lock (sync)
            {
                writer.Write(buffer, 0, count);
                tail.Append(buffer, 0, count);
                length += count;
                if (tail.Length > TailLimit) tail.Remove(0, tail.Length - TailLimit);
            }
        }

        public void Append(string value)
        {
            if (String.IsNullOrEmpty(value)) return;
            char[] buffer = value.ToCharArray();
            Append(buffer, buffer.Length);
        }

        public long Length { get { lock (sync) return length; } }

        public string TextFrom(long start)
        {
            lock (sync)
            {
                long tailStart = length - tail.Length;
                int offset = (int)Math.Max(0, Math.Min(tail.Length, start - tailStart));
                return tail.ToString(offset, tail.Length - offset);
            }
        }

        public string TailText { get { lock (sync) return tail.ToString(); } }

        public void Flush() { lock (sync) writer.Flush(); }
        public void Dispose() { lock (sync) writer.Dispose(); }
    }

    sealed class CollectorInteractiveProcess : IDisposable
    {
        readonly Process process;
        readonly CollectorCaptureBuffer capture;
        readonly SemaphoreSlim changed = new SemaphoreSlim(0, Int32.MaxValue);
        readonly Task stdoutPump;
        readonly Task stderrPump;
        bool disposed;

        public CollectorInteractiveProcess(string executable, string arguments, string captureFile)
        {
            capture = new CollectorCaptureBuffer(captureFile);
            var info = new ProcessStartInfo(executable, arguments)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8
            };
            process = new Process { StartInfo = info };
            if (!process.Start()) throw new InvalidOperationException("Could not start the SSH client.");
            stdoutPump = PumpAsync(process.StandardOutput);
            stderrPump = PumpAsync(process.StandardError);
        }

        async Task PumpAsync(StreamReader reader)
        {
            var buffer = new char[2048];
            try
            {
                while (true)
                {
                    int read = await reader.ReadAsync(buffer, 0, buffer.Length);
                    if (read <= 0) break;
                    capture.Append(buffer, read);
                    try { changed.Release(); } catch (ObjectDisposedException) { break; }
                }
            }
            catch (ObjectDisposedException) { }
            catch (IOException) { }
            finally { try { changed.Release(); } catch { } }
        }

        public long Length
        {
            get { return capture.Length; }
        }

        public string TextFrom(long start)
        {
            return capture.TextFrom(start);
        }

        public string AllText
        {
            get { return capture.TailText; }
        }

        public bool HasExited
        {
            get { try { return process.HasExited; } catch { return true; } }
        }

        public int ExitCode
        {
            get { try { return process.HasExited ? process.ExitCode : -1; } catch { return -1; } }
        }

        public void WriteLine(string value)
        {
            if (disposed || process.HasExited) throw new IOException("The SSH session closed before input could be sent.");
            process.StandardInput.WriteLine(value ?? "");
            process.StandardInput.Flush();
        }

        public async Task WaitForChangeAsync(int timeoutMs, CancellationToken token)
        {
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                timeout.CancelAfter(Math.Max(1, timeoutMs));
                try { await changed.WaitAsync(timeout.Token); }
                catch (OperationCanceledException) { token.ThrowIfCancellationRequested(); }
            }
        }

        public async Task<bool> WaitForExitAsync(int timeoutMs, CancellationToken token)
        {
            if (HasExited) return true;
            Task wait = Task.Run(delegate { try { process.WaitForExit(); } catch { } });
            Task completed = await Task.WhenAny(wait, Task.Delay(Math.Max(1, timeoutMs), token));
            if (completed == wait)
            {
                try { await Task.WhenAll(stdoutPump, stderrPump); } catch { }
                capture.Flush();
                return true;
            }
            token.ThrowIfCancellationRequested();
            return false;
        }

        public void CloseInput()
        {
            try { process.StandardInput.Close(); } catch { }
        }

        public void Stop()
        {
            try { if (!process.HasExited) process.Kill(); } catch { }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            Stop();
            try { process.Dispose(); } catch { }
            try { capture.Dispose(); } catch { }
            try { changed.Dispose(); } catch { }
        }
    }

    sealed class CollectorResult
    {
        public string Device;
        public string Hostname;
        public string Status;
        public string Protocol;
        public string Username;
        public DateTime Started;
        public DateTime Finished;
        public string Output;
        public string Error;
        public string File;
    }

    public sealed partial class MainForm
    {
        string statePath;
        ToolStripStatusLabel timeSourceStatus;
        DateTime clockBaseUtc;
        Stopwatch clockElapsed;
        bool clockNtp;
        float zoomScale = 1f;
        readonly Dictionary<Control, float> zoomBaseFonts = new Dictionary<Control, float>();

        ComboBox collectorProtocol;
        ComboBox collectorDeviceType;
        NumericUpDown collectorPort;
        NumericUpDown collectorConcurrency;
        TextBox collectorAuth1User;
        TextBox collectorAuth1Pass;
        TextBox collectorAuth2User;
        TextBox collectorAuth2Pass;
        TextBox collectorAuth1Secret;
        TextBox collectorAuth2Secret;
        CheckBox collectorUseAuth1;
        CheckBox collectorUseAuth2;
        CheckBox collectorShowPasswords;
        CheckBox collectorStrictHostKey;
        CheckBox collectorContinueError;
        TextBox collectorDevices;
        TextBox collectorBasic;
        TextBox collectorCommands;
        CheckBox collectorJson;
        TextBox collectorFolderBox;
        RichTextBox collectorTerminal;
        DataGridView collectorGrid;
        DataTable collectorTable;
        Button collectorStart;
        Button collectorCancel;
        Button collectorExportLog;
        CancellationTokenSource collectorCancellation;
        string collectorPlinkOverride = null;
        readonly ConcurrentQueue<string> collectorTerminalQueueV120 = new ConcurrentQueue<string>();
        System.Windows.Forms.Timer collectorTerminalTimerV120;
        int collectorTerminalFlushScheduledV120;
        int collectorTerminalFlushCountV120;

        void BuildCollectorPage()
        {
            var page = NewPage("Config Collector");
            var outer = new SplitContainer { Dock = DockStyle.Fill, SplitterWidth = 8, SplitterDistance = 530, FixedPanel = FixedPanel.Panel1, BackColor = Canvas };
            outer.Panel1.Padding = new Padding(0, 0, 4, 0); outer.Panel2.Padding = new Padding(4, 0, 0, 0);

            var left = Card();
            var transport = new TableLayoutPanel { Dock = DockStyle.Top, Height = 102, ColumnCount = 4, RowCount = 3, Padding = new Padding(0, 4, 0, 5) };
            transport.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            transport.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 86));
            transport.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 98));
            transport.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));
            collectorProtocol = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            collectorProtocol.Items.AddRange(new object[] { "SSH", "Telnet" }); collectorProtocol.SelectedIndex = 0;
            collectorPort = NumberField(1, 65535, 22, 1);
            collectorConcurrency = NumberField(1, 32, 5, 1);
            collectorJson = new CheckBox { Text = "JSON", Checked = false, AutoSize = true, Anchor = AnchorStyles.Left };
            collectorDeviceType = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            collectorDeviceType.Items.AddRange(new object[] { "Cisco IOS / IOS-XE", "Cisco NX-OS", "Cisco ASA", "Huawei VRP", "Aruba AOS-S / HP ProCurve" });
            collectorDeviceType.SelectedIndex = 0;
            transport.Controls.Add(FieldLabel("Protocol"), 0, 0); transport.Controls.Add(FieldLabel("Port"), 1, 0); transport.Controls.Add(FieldLabel("Parallel"), 2, 0); transport.Controls.Add(FieldLabel("Also export"), 3, 0);
            transport.Controls.Add(collectorProtocol, 0, 1); transport.Controls.Add(collectorPort, 1, 1); transport.Controls.Add(collectorConcurrency, 2, 1); transport.Controls.Add(collectorJson, 3, 1);
            transport.Controls.Add(FieldLabel("Device type"), 0, 2); transport.Controls.Add(collectorDeviceType, 1, 2); transport.SetColumnSpan(collectorDeviceType, 3);
            collectorProtocol.SelectedIndexChanged += delegate { collectorPort.Value = collectorProtocol.Text == "Telnet" ? 23 : 22; };

            var auth = new TableLayoutPanel { Dock = DockStyle.Top, Height = 178, ColumnCount = 4, RowCount = 4, Padding = new Padding(0, 4, 0, 4) };
            auth.RowStyles.Add(new RowStyle(SizeType.Absolute, 64)); auth.RowStyles.Add(new RowStyle(SizeType.Absolute, 31));
            auth.RowStyles.Add(new RowStyle(SizeType.Absolute, 31)); auth.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            auth.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 68)); auth.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            auth.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); auth.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 98));
            var authOptions = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true };
            collectorUseAuth1 = new CheckBox { Text = "Use AUTH1", Checked = true, AutoSize = true };
            collectorUseAuth2 = new CheckBox { Text = "Use AUTH2 fallback", Checked = true, AutoSize = true };
            collectorShowPasswords = new CheckBox { Text = "Show passwords", AutoSize = true };
            collectorStrictHostKey = new CheckBox { Text = "Strict host key", AutoSize = true };
            collectorContinueError = new CheckBox { Text = "Continue command errors", Checked = true, AutoSize = true };
            collectorShowPasswords.CheckedChanged += delegate
            {
                bool hide = !collectorShowPasswords.Checked;
                collectorAuth1Pass.UseSystemPasswordChar = collectorAuth2Pass.UseSystemPasswordChar = hide;
                collectorAuth1Secret.UseSystemPasswordChar = collectorAuth2Secret.UseSystemPasswordChar = hide;
            };
            authOptions.Controls.AddRange(new Control[] { collectorUseAuth1, collectorUseAuth2, collectorShowPasswords, collectorStrictHostKey, collectorContinueError });
            collectorAuth1User = new TextBox { Dock = DockStyle.Fill }; collectorAuth1Pass = new TextBox { Dock = DockStyle.Fill, UseSystemPasswordChar = true };
            collectorAuth2User = new TextBox { Dock = DockStyle.Fill }; collectorAuth2Pass = new TextBox { Dock = DockStyle.Fill, UseSystemPasswordChar = true };
            collectorAuth1Secret = new TextBox { Dock = DockStyle.Fill, UseSystemPasswordChar = true };
            collectorAuth2Secret = new TextBox { Dock = DockStyle.Fill, UseSystemPasswordChar = true };
            Cue(collectorAuth1User, "Username"); Cue(collectorAuth1Pass, "Password"); Cue(collectorAuth1Secret, "Enable secret");
            Cue(collectorAuth2User, "Username"); Cue(collectorAuth2Pass, "Password"); Cue(collectorAuth2Secret, "Enable secret");
            auth.Controls.Add(authOptions, 0, 0); auth.SetColumnSpan(authOptions, 4);
            auth.Controls.Add(new Label { Text = "AUTH1", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI Semibold", 9f) }, 0, 1);
            auth.Controls.Add(collectorAuth1User, 1, 1); auth.Controls.Add(collectorAuth1Pass, 2, 1); auth.Controls.Add(collectorAuth1Secret, 3, 1);
            auth.Controls.Add(new Label { Text = "AUTH2", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI Semibold", 9f) }, 0, 2);
            auth.Controls.Add(collectorAuth2User, 1, 2); auth.Controls.Add(collectorAuth2Pass, 2, 2); auth.Controls.Add(collectorAuth2Secret, 3, 2);
            auth.Controls.Add(new Label { Text = "Both checked = try AUTH1 first, then AUTH2. Passwords/enable secrets stay in memory only.", Dock = DockStyle.Fill, ForeColor = Warning }, 0, 3); auth.SetColumnSpan(auth.GetControlFromPosition(0, 3), 4);

            var commandTabs = new TabControl { Dock = DockStyle.Bottom, Height = 185 };
            var basicTab = new TabPage("Basic commands") { Padding = new Padding(5) };
            collectorBasic = new TextBox { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Both, WordWrap = false, Font = new Font("Consolas", 9f), Text = "terminal length 0" };
            collectorDeviceType.SelectedIndexChanged += delegate
            {
                string current = collectorBasic.Text.Trim();
                if (current.Length == 0 || current == "terminal length 0" || current == "terminal pager 0" || current == "screen-length 0 temporary" || current == "no page")
                    collectorBasic.Text = DefaultCollectorBasic(collectorDeviceType.Text);
            };
            basicTab.Controls.Add(collectorBasic);
            var showTab = new TabPage("Collect commands") { Padding = new Padding(5) };
            collectorCommands = new TextBox { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Both, WordWrap = false, Font = new Font("Consolas", 9f), Text = "show running-config\r\nshow interfaces status\r\nshow version" };
            showTab.Controls.Add(collectorCommands);
            commandTabs.TabPages.Add(basicTab); commandTabs.TabPages.Add(showTab);

            var folderBar = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 44, ColumnCount = 2, Padding = new Padding(0, 4, 0, 4) };
            folderBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); folderBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));
            collectorFolderBox = new TextBox { Dock = DockStyle.Fill, ReadOnly = true, Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "NetStuck Configs") };
            var browse = ActionButton("Browse…", false, 0); browse.Dock = DockStyle.Fill; browse.Click += BrowseCollectorFolder;
            folderBar.Controls.Add(collectorFolderBox, 0, 0); folderBar.Controls.Add(browse, 1, 0);

            var collectorActions = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 52, ColumnCount = 5, Padding = new Padding(0, 5, 0, 5) };
            collectorActions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            collectorActions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));
            collectorActions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
            collectorActions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            collectorActions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 82));
            var ping = ActionButton("Ping all", false, 0); ping.Dock = DockStyle.Fill; ping.Click += PingCollectorDevices;
            var open = ActionButton("Open folder", false, 0); open.Dock = DockStyle.Fill; open.Click += OpenCollectorFolder;
            collectorExportLog = ActionButton("Export errors", false, 0); collectorExportLog.Dock = DockStyle.Fill; collectorExportLog.Enabled = false; collectorExportLog.Click += ExportCollectorLog;
            collectorStart = ActionButton("COLLECT", true, 0); collectorStart.Dock = DockStyle.Fill; collectorStart.Click += StartCollector;
            collectorCancel = DangerButton("STOP", 0); collectorCancel.Dock = DockStyle.Fill; collectorCancel.Enabled = false; collectorCancel.Click += delegate { if (collectorCancellation != null) collectorCancellation.Cancel(); };
            collectorActions.Controls.Add(ping, 0, 0); collectorActions.Controls.Add(open, 1, 0); collectorActions.Controls.Add(collectorExportLog, 2, 0); collectorActions.Controls.Add(collectorStart, 3, 0); collectorActions.Controls.Add(collectorCancel, 4, 0);

            collectorDevices = new TextBox
            {
                Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Both, WordWrap = false,
                Font = new Font("Consolas", 9.5f), Text = "10.100.10.1 Core-SW\r\n10.100.10.2:2222 Branch-SW"
            };
            left.Controls.Add(collectorDevices); left.Controls.Add(collectorActions); left.Controls.Add(folderBar); left.Controls.Add(commandTabs); left.Controls.Add(auth); left.Controls.Add(transport);
            left.Controls.Add(SectionHeader("Remote collection", "SSH/Telnet • AUTH1 → AUTH2 fallback • enable secret • parallel devices"));

            var resultCard = Card();
            var resultSplit = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterWidth = 8, BackColor = Canvas };
            collectorTable = new DataTable();
            collectorTable.Columns.Add("Device"); collectorTable.Columns.Add("Status"); collectorTable.Columns.Add("Protocol"); collectorTable.Columns.Add("Username"); collectorTable.Columns.Add("Hostname"); collectorTable.Columns.Add("File"); collectorTable.Columns.Add("Detail");
            collectorGrid = DataGrid(); collectorGrid.AutoGenerateColumns = false;
            AddGridColumn(collectorGrid, "Device", "Device", 145); AddGridColumn(collectorGrid, "Status", "Status", 95); AddGridColumn(collectorGrid, "Protocol", "Protocol", 75);
            AddGridColumn(collectorGrid, "Hostname", "Hostname", 150); AddGridColumn(collectorGrid, "Saved file", "File", 360); AddGridColumn(collectorGrid, "Detail", "Detail", 260);
            collectorGrid.DataSource = collectorTable; collectorGrid.CellFormatting += FormatCollectorCell; collectorGrid.CellDoubleClick += OpenCollectorResult;
            collectorTerminal = new RichTextBox { Dock = DockStyle.Fill, ReadOnly = true, BackColor = Color.FromArgb(15, 23, 42), ForeColor = Color.FromArgb(226, 232, 240), Font = new Font("Consolas", 9.25f), WordWrap = false };
            collectorTerminalTimerV120 = new System.Windows.Forms.Timer { Interval = 75 };
            collectorTerminalTimerV120.Tick += delegate { DrainCollectorTerminalQueueV120(); };
            collectorTerminalTimerV120.Start();
            resultSplit.Panel1.Controls.Add(collectorGrid); resultSplit.Panel2.Controls.Add(collectorTerminal);
            resultCard.Controls.Add(resultSplit); resultCard.Controls.Add(SectionHeader("Collection review + terminal", "Double-click a completed row to open the captured file"));
            outer.Panel1.Controls.Add(left); outer.Panel2.Controls.Add(resultCard); page.Controls.Add(outer);
            ConfigureSplit(page, outer, 530, 450, 620);
            ConfigureHorizontalSplit(resultCard, resultSplit, 300, 180, 170);
        }

        List<CollectorDevice> ParseCollectorDevices()
        {
            var result = new List<CollectorDevice>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int defaultPort = (int)collectorPort.Value;
            foreach (string raw in (collectorDevices.Text ?? "").Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#") || line.StartsWith(";")) continue;
                int comment = line.IndexOf(" #", StringComparison.Ordinal);
                if (comment >= 0) line = line.Substring(0, comment).Trim();
                string[] parts = Regex.Split(line, @"\s+");
                string hostToken = parts[0];
                string host = hostToken;
                int port = defaultPort;
                Match portMatch = Regex.Match(hostToken, @"^(?<host>.+?)(?:,|:)(?<port>\d{1,5})$");
                if (portMatch.Success)
                {
                    host = portMatch.Groups["host"].Value.Trim('[', ']');
                    int parsed;
                    if (!Int32.TryParse(portMatch.Groups["port"].Value, out parsed) || parsed < 1 || parsed > 65535)
                        throw new InvalidOperationException("Invalid port in device line: " + raw);
                    port = parsed;
                }
                string key = host + ":" + port;
                if (!seen.Add(key)) continue;
                var item = new CollectorDevice { Host = host, Port = port };
                var description = new List<string>();
                foreach (string part in parts.Skip(1))
                {
                    if (String.Equals(part, "auth=1", StringComparison.OrdinalIgnoreCase)) item.AuthSlot = 1;
                    else if (String.Equals(part, "auth=2", StringComparison.OrdinalIgnoreCase)) item.AuthSlot = 2;
                    else description.Add(part);
                }
                item.Description = String.Join(" ", description);
                result.Add(item);
            }
            return result;
        }

        async void PingCollectorDevices(object sender, EventArgs e)
        {
            List<CollectorDevice> devices = ParseCollectorDevices();
            if (devices.Count == 0) { MessageBox.Show(this, "Enter at least one device."); return; }
            collectorTable.Rows.Clear();
            collectorTerminal.Clear();
            foreach (CollectorDevice device in devices)
            {
                bool ok = false; long latency = 0; string detail;
                try
                {
                    using (var p = new Ping())
                    {
                        PingReply reply = await p.SendPingAsync(device.Host, 1500);
                        ok = reply.Status == IPStatus.Success; latency = reply.RoundtripTime; detail = reply.Status.ToString();
                    }
                }
                catch (Exception ex) { detail = FriendlyError(ex); }
                collectorTable.Rows.Add(device.Host, ok ? "Reachable" : "Unreachable", "ICMP", "", device.Description, "", ok ? latency + " ms" : detail);
                AppendTerminal("[" + device.Host + "] " + (ok ? "Reachable " + latency + " ms" : "Unreachable: " + detail));
            }
            UpdateCollectorErrorExportStateV120();
        }

        async void StartCollector(object sender, EventArgs e)
        {
            List<CollectorDevice> devices = ParseCollectorDevices();
            if (devices.Count == 0) { MessageBox.Show(this, "Enter at least one device."); return; }
            if (GetCollectorCredentials(0).Count == 0)
            {
                MessageBox.Show(this, "Enable AUTH1 and/or AUTH2, then enter a username for every enabled profile.");
                return;
            }
            string folder = collectorFolderBox.Text.Trim();
            if (folder.Length == 0) { MessageBox.Show(this, "Choose an output folder."); return; }
            Directory.CreateDirectory(folder);
            collectorTable.Rows.Clear(); collectorTerminal.Clear();
            string discarded; while (collectorTerminalQueueV120.TryDequeue(out discarded)) { }
            Interlocked.Exchange(ref collectorTerminalFlushScheduledV120, 0);
            UpdateCollectorErrorExportStateV120();
            collectorCancellation = new CancellationTokenSource();
            SetTabActivity("Config Collector", true);
            collectorStart.Enabled = false; collectorStart.Text = "● COLLECTING";
            collectorCancel.Enabled = true; collectorCancel.BackColor = Danger; collectorCancel.ForeColor = Color.White;
            string protocol = collectorProtocol.Text;
            int parallel = (int)collectorConcurrency.Value;
            string basic = collectorBasic.Text, commands = collectorCommands.Text;
            bool writeJson = collectorJson.Checked;
            var semaphore = new SemaphoreSlim(parallel, parallel);
            try
            {
                var jobs = devices.Select(async device =>
                {
                    await semaphore.WaitAsync(collectorCancellation.Token);
                    try { return await CollectOneAsync(device, protocol, basic, commands, folder, writeJson, collectorCancellation.Token); }
                    finally { semaphore.Release(); }
                }).ToArray();
                CollectorResult[] results = await Task.WhenAll(jobs);
                Log("ACTION", "Collector", "Finished " + results.Count(r => r.Status == "Completed") + "/" + results.Length + " device(s)");
            }
            catch (OperationCanceledException) { AppendTerminal("[SYSTEM] Collection cancelled."); }
            catch (Exception ex) { AppendTerminal("[ERROR] " + FriendlyError(ex)); Log("ERROR", "Collector", FriendlyError(ex)); }
            finally
            {
                CancellationTokenSource completedCollection = collectorCancellation;
                collectorCancellation = null;
                if (completedCollection != null) completedCollection.Dispose();
                if (!appClosing && !IsDisposed && !Disposing)
                {
                    try
                    {
                        if (IsHandleCreated)
                        {
                            SetTabActivity("Config Collector", false);
                            collectorStart.Enabled = true; collectorStart.Text = "COLLECT";
                            collectorCancel.Enabled = false; collectorCancel.BackColor = Surface; collectorCancel.ForeColor = Danger;
                        }
                    }
                    catch (ObjectDisposedException) { }
                    catch (System.ComponentModel.Win32Exception) { }
                }
            }
        }

        async Task<CollectorResult> CollectOneAsync(CollectorDevice device, string protocol, string basic, string commands, string folder, bool writeJson, CancellationToken token)
        {
            var result = new CollectorResult { Device = device.Host, Hostname = device.Description, Protocol = protocol, Started = DateTime.Now, Status = "Running" };
            AddCollectorRow(result, "Connecting…");
            AppendTerminal("\r\n=== " + device.Host + " (" + protocol + ":" + device.Port + ", " + collectorDeviceType.Text + ") [" + AppVersion + "] ===");
            try
            {
                List<CollectorCredential> credentials = GetCollectorCredentials(device.AuthSlot);
                if (credentials.Count == 0) throw new InvalidOperationException("No enabled credential is available for this device.");
                result.Username = String.Join(" | ", credentials.Select(item => item.User).Distinct(StringComparer.OrdinalIgnoreCase));
                CollectorSession session = await RunCollectorSessionAsync(device, protocol, basic, commands, credentials, folder, token);
                result.Output = session.Output;
                result.Username = session.Username;
                result.Hostname = DetectHostnameFromFileV120(session.CaptureFile, device.Description);
                result.Finished = DateTime.Now;
                string fileName = SafeFileName(device.Host + "_" + (String.IsNullOrWhiteSpace(result.Hostname) ? "device" : result.Hostname) + "_" + result.Finished.ToString("yyyyMMdd-HHmmss") + ".txt");
                result.File = Path.Combine(folder, fileName);
                RedactCollectorFileV120(session.CaptureFile, credentialSecrets: credentials.SelectMany(item => new[] { item.Password, item.EnableSecret }));
                if (File.Exists(result.File)) File.Delete(result.File);
                File.Move(session.CaptureFile, result.File);
                if (writeJson)
                {
                    string jsonFile = Path.ChangeExtension(result.File, ".json");
                    WriteCollectorJsonFromFileV120(jsonFile, result.File, result, device.Port, session.CredentialName);
                }
                result.Status = "Completed";
                UpdateCollectorRow(result, "Saved with " + session.CredentialName);
                AppendTerminal(result.Output + (new FileInfo(result.File).Length > Encoding.UTF8.GetByteCount(result.Output ?? "")
                    ? "\r\n[Preview truncated; full streamed capture is in the saved file.]" : ""));
                AppendTerminal("[" + device.Host + "] SAVED with " + session.CredentialName + ": " + result.File);
            }
            catch (OperationCanceledException) { result.Status = "Cancelled"; result.Error = "Cancelled"; UpdateCollectorRow(result, result.Error); throw; }
            catch (Exception ex)
            {
                result.Finished = DateTime.Now; result.Status = "Failed";
                string detail = FriendlyError(ex), category = ClassifyCollectorFailureV120(detail);
                result.Error = category == "Other error" ? detail : category + ": " + detail;
                UpdateCollectorRow(result, result.Error); AppendTerminal("[" + device.Host + "] FAILED: " + result.Error);
            }
            return result;
        }

        List<CollectorCredential> GetCollectorCredentials(int forcedSlot)
        {
            var result = new List<CollectorCredential>();
            if ((forcedSlot == 0 || forcedSlot == 1) && collectorUseAuth1.Checked && !String.IsNullOrWhiteSpace(collectorAuth1User.Text))
                result.Add(new CollectorCredential
                {
                    Slot = 1, Name = "AUTH1", User = NormalizeCollectorUsername(collectorAuth1User.Text),
                    Password = collectorAuth1Pass.Text, EnableSecret = collectorAuth1Secret.Text
                });
            if ((forcedSlot == 0 || forcedSlot == 2) && collectorUseAuth2.Checked && !String.IsNullOrWhiteSpace(collectorAuth2User.Text))
                result.Add(new CollectorCredential
                {
                    Slot = 2, Name = "AUTH2", User = NormalizeCollectorUsername(collectorAuth2User.Text),
                    Password = collectorAuth2Pass.Text, EnableSecret = collectorAuth2Secret.Text
                });
            return result;
        }

        static string NormalizeCollectorUsername(string value)
        {
            string username = (value ?? "").Trim();
            if (username.IndexOf('\\') >= 0) username = Regex.Replace(username, @"\\+", "\\");
            return username;
        }

        async Task<CollectorSession> RunCollectorSessionAsync(CollectorDevice device, string protocol, string basic, string commands,
            List<CollectorCredential> credentials, string folder, CancellationToken token)
        {
            var failures = new List<string>();
            string captureFile = Path.Combine(folder, ".netstuck-" + SafeFileName(device.Host) + "-" + Guid.NewGuid().ToString("N") + ".tmp");
            foreach (CollectorCredential credential in credentials)
            {
                token.ThrowIfCancellationRequested();
                AppendTerminal("[" + device.Host + "] Trying " + credential.Name + "...");
                AppendTerminal("[" + device.Host + "] " + credential.Name + " username prepared with " +
                    credential.User.Count(character => character == '\\') + " literal backslash character(s).");
                try
                {
                    string commandScript = String.Equals(protocol, "Telnet", StringComparison.OrdinalIgnoreCase)
                        ? BuildCollectorCommandScript(basic, commands, credential.EnableSecret)
                        : JoinCommands(basic, commands);
                    string output = String.Equals(protocol, "Telnet", StringComparison.OrdinalIgnoreCase)
                        ? await RunTelnetToFileV120(device.Host, device.Port, credential.User, credential.Password, commandScript, captureFile, token)
                        : await RunSshCollectorToFileV120(device.Host, device.Port, credential, commandScript, captureFile, token);
                    if (LooksLikeAuthenticationFailure(output))
                        throw new CollectorAuthenticationException("Remote device rejected " + credential.Name + ".");
                    if (!collectorContinueError.Checked && LooksLikeCommandFailure(output))
                        throw new InvalidOperationException("The device reported a command error. Enable Continue command errors to save partial output.");
                    output = RedactCollectorSecret(CleanCollectorTerminal(output), credential.Password);
                    output = RedactCollectorSecret(output, credential.EnableSecret);
                    return new CollectorSession { Output = output, CredentialName = credential.Name, Username = credential.User, CaptureFile = captureFile };
                }
                catch (OperationCanceledException) { try { if (File.Exists(captureFile)) File.Delete(captureFile); } catch { } throw; }
                catch (CollectorAuthenticationException ex)
                {
                    string message = FriendlyError(ex);
                    failures.Add(credential.Name + ": " + message);
                    AppendTerminal("[" + device.Host + "] " + credential.Name + " failed: " + message);
                }
                catch (Exception ex)
                {
                    string message = FriendlyError(ex);
                    failures.Add(credential.Name + ": " + message);
                    AppendTerminal("[" + device.Host + "] " + credential.Name + " failed: " + message);
                    try { if (File.Exists(captureFile)) File.Delete(captureFile); } catch { }
                    throw new InvalidOperationException(credential.Name + " failed before credential fallback: " + message, ex);
                }
            }
            try { if (File.Exists(captureFile)) File.Delete(captureFile); } catch { }
            throw new InvalidOperationException("All selected credentials failed. " + String.Join(" | ", failures));
        }

        static string BuildCollectorCommandScript(string basic, string commands, string enableSecret)
        {
            var lines = new List<string>();
            if (!String.IsNullOrWhiteSpace(enableSecret))
            {
                lines.Add("enable");
                lines.Add(enableSecret);
            }
            lines.AddRange(SplitCollectorCommands(basic));
            lines.AddRange(SplitCollectorCommands(commands));
            return String.Join("\r\n", lines);
        }

        static IEnumerable<string> SplitCollectorCommands(string commands)
        {
            return (commands ?? "").Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(value => value.Trim()).Where(value => value.Length > 0 && !value.StartsWith("#") && !value.StartsWith(";"));
        }

        static string DefaultCollectorBasic(string deviceType)
        {
            if ((deviceType ?? "").IndexOf("ASA", StringComparison.OrdinalIgnoreCase) >= 0) return "terminal pager 0";
            if ((deviceType ?? "").IndexOf("Huawei", StringComparison.OrdinalIgnoreCase) >= 0) return "screen-length 0 temporary";
            if ((deviceType ?? "").IndexOf("Aruba", StringComparison.OrdinalIgnoreCase) >= 0 || (deviceType ?? "").IndexOf("ProCurve", StringComparison.OrdinalIgnoreCase) >= 0) return "no page";
            return "terminal length 0";
        }

        async Task<string> RunSshCollectorAsync(string host, int port, CollectorCredential credential, string commands, CancellationToken token)
        {
            string captureFile = Path.Combine(Path.GetTempPath(), "NetStuck-ssh-test-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                await RunSshCollectorToFileV120(host, port, credential, commands, captureFile, token);
                return File.ReadAllText(captureFile, Encoding.UTF8);
            }
            finally { try { if (File.Exists(captureFile)) File.Delete(captureFile); } catch { } }
        }

        async Task<string> RunSshCollectorToFileV120(string host, int port, CollectorCredential credential, string commands, string captureFile, CancellationToken token)
        {
            string bundledPlink = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "plink.exe");
            string installedPlink = @"C:\Program Files\PuTTY\plink.exe";
            string plink = !String.IsNullOrWhiteSpace(collectorPlinkOverride) ? collectorPlinkOverride :
                (File.Exists(bundledPlink) ? bundledPlink : installedPlink);
            if (!File.Exists(plink)) throw new FileNotFoundException("PuTTY plink.exe is required for password SSH. Reinstall NetStuck with its tools folder.", plink);
            if ((credential.Password ?? "").IndexOfAny(new[] { '\r', '\n' }) >= 0)
                throw new InvalidOperationException("SSH passwords containing a line break are not supported.");
            if ((credential.EnableSecret ?? "").IndexOfAny(new[] { '\r', '\n' }) >= 0)
                throw new InvalidOperationException("Enable secrets containing a line break are not supported.");

            string hostKeyArgument = await ResolveCollectorHostKeyArgumentAsync(plink, host, port, token);
            CollectorTransportException transportFailure = null;
            try
            {
                return await RunSshCollectorInteractiveAttemptAsync(plink, host, port, hostKeyArgument, credential, commands, captureFile, token);
            }
            catch (CollectorTransportException ex)
            {
                transportFailure = ex;
            }
            if (!IsRetryableSshTransportFailure(transportFailure.Message)) throw transportFailure;
            AppendTerminal("[" + host + "] Transient SSH transport failure; retrying once...");
            await Task.Delay(500, token);
            return await RunSshCollectorInteractiveAttemptAsync(plink, host, port, hostKeyArgument, credential, commands, captureFile, token);
        }

        async Task<string> ResolveCollectorHostKeyArgumentAsync(string plink, string host, int port, CancellationToken token)
        {
            string arguments = "-batch -ssh -no-antispoof -noagent -P " + port + " " + QuoteArg(host);
            CommandProcessResult check = await RunPlinkWithTransportRetryAsync(plink, arguments, "", host, token);
            string combined = CombineProcessOutput(check);
            if (IsUnknownHostKey(combined))
            {
                Match fingerprint = Regex.Match(combined, @"SHA256:[A-Za-z0-9+/=]+", RegexOptions.IgnoreCase);
                if (collectorStrictHostKey.Checked)
                    throw new InvalidOperationException("SSH host key is not trusted. Cache and verify the device key with PuTTY, or turn off Strict host key for a one-session fingerprint trust.");
                if (!fingerprint.Success)
                    throw new InvalidOperationException("SSH host key is not cached and its SHA256 fingerprint could not be read.");
                AppendTerminal("[" + host + "] Trusting the reported SSH host key for this session: " + fingerprint.Value);
                return " -hostkey " + QuoteArg(fingerprint.Value);
            }
            if (check.ExitCode != 0 && LooksLikeSshTransportFailure(combined))
                throw new CollectorTransportException(combined);
            return "";
        }

        async Task<string> RunSshCollectorInteractiveAttemptAsync(string plink, string host, int port, string hostKeyArgument,
            CollectorCredential credential, string commands, string captureFile, CancellationToken token)
        {
            string arguments = "-ssh -t -no-antispoof -noagent -P " + port + " -l " + QuoteArg(credential.User) +
                hostKeyArgument + " " + QuoteArg(host);
            using (var session = new CollectorInteractiveProcess(plink, arguments, captureFile))
            {
                try
                {
                    long cursor = 0;
                    CollectorPromptResult state = await WaitForCollectorPromptAsync(session, cursor, 20000, true, null, token);
                    if (state.Kind == "password")
                    {
                        if (String.IsNullOrEmpty(credential.Password))
                            throw new CollectorAuthenticationException("The device requested a password but this authentication profile has no password.");
                        cursor = session.Length;
                        session.WriteLine(credential.Password);
                        state = await WaitForCollectorPromptAsync(session, cursor, 20000, false, null, token);
                    }
                    EnsureCollectorPrompt(state, host);

                    string recentPrompt = CleanCollectorTerminal(state.Text);
                    string expectedPrompt = ExtractCollectorPrompt(recentPrompt);
                    if (!String.IsNullOrWhiteSpace(credential.EnableSecret) && !Regex.IsMatch(recentPrompt, @"#\s*$"))
                    {
                        cursor = session.Length;
                        session.WriteLine("enable");
                        state = await WaitForCollectorPromptAsync(session, cursor, 15000, true, null, token);
                        if (state.Kind == "password")
                        {
                            cursor = session.Length;
                            session.WriteLine(credential.EnableSecret);
                            state = await WaitForCollectorPromptAsync(session, cursor, 15000, false, null, token);
                        }
                        EnsureCollectorPrompt(state, host);
                        if (!Regex.IsMatch(CleanCollectorTerminal(state.Text), @"#\s*$"))
                            throw new CollectorAuthenticationException("Enable secret was rejected or privileged mode was not entered.");
                        expectedPrompt = ExtractCollectorPrompt(state.Text);
                    }

                    foreach (string command in SplitCollectorCommands(commands))
                    {
                        token.ThrowIfCancellationRequested();
                        cursor = session.Length;
                        session.WriteLine(command);
                        state = await WaitForCollectorPromptAsync(session, cursor, 120000, false, expectedPrompt, token);
                        EnsureCollectorPrompt(state, host);
                    }

                    try { session.WriteLine("exit"); } catch { }
                    session.CloseInput();
                    if (!await session.WaitForExitAsync(2500, token)) session.Stop();
                    string output = CleanCollectorTerminal(session.AllText).Trim();
                    output = RedactCollectorSecret(output, credential.Password);
                    output = RedactCollectorSecret(output, credential.EnableSecret);
                    return output;
                }
                catch (TimeoutException ex)
                {
                    throw new CollectorTransportException(ex.Message);
                }
                catch (IOException ex)
                {
                    string output = CleanCollectorTerminal(session.AllText);
                    if (LooksLikeAuthenticationFailure(output))
                        throw new CollectorAuthenticationException("Remote device rejected the supplied username or password.");
                    throw new CollectorTransportException(String.IsNullOrWhiteSpace(output) ? ex.Message : output);
                }
            }
        }

        async Task<CollectorPromptResult> WaitForCollectorPromptAsync(CollectorInteractiveProcess session, long start,
            int timeoutMs, bool allowPasswordPrompt, string expectedPrompt, CancellationToken token)
        {
            Stopwatch watch = Stopwatch.StartNew();
            while (watch.ElapsedMilliseconds < timeoutMs)
            {
                token.ThrowIfCancellationRequested();
                string text = CleanCollectorTerminal(session.TextFrom(start));
                if (LooksLikeAuthenticationFailure(text)) return new CollectorPromptResult { Kind = "authentication", Text = text };
                if (IsUnknownHostKey(text)) return new CollectorPromptResult { Kind = "hostkey", Text = text };
                if (Regex.IsMatch(text, @"(?im)(verification\s*code|one[- ]time|otp|security\s*token|passcode)[^\r\n:]*:\s*$"))
                    return new CollectorPromptResult { Kind = "mfa", Text = text };
                if (Regex.IsMatch(text, @"(?im)(?:password|password for [^\r\n]+)[^\r\n:]*:\s*$"))
                    return new CollectorPromptResult { Kind = allowPasswordPrompt ? "password" : "authentication", Text = text };
                if (Regex.IsMatch(text, @"(?im)(\[confirm\]|continue\?|\(y/n\)|\[yes/no\])\s*$"))
                    return new CollectorPromptResult { Kind = "confirmation", Text = text };
                bool hasPrompt = !String.IsNullOrWhiteSpace(expectedPrompt)
                    ? Regex.IsMatch(text, @"(?m)(?:^|\s)" + Regex.Escape(expectedPrompt) + @"\s*$")
                    : Regex.IsMatch(text, @"(?m)(?:^|\s)(?:[A-Za-z0-9_.:/()@<>-]{1,160}[#>]|\[[^\r\n\]]{1,160}\])\s*$");
                if (hasPrompt)
                {
                    long stableLength = session.Length;
                    await session.WaitForChangeAsync(60, token);
                    if (session.Length == stableLength)
                        return new CollectorPromptResult { Kind = "prompt", Text = text };
                    continue;
                }
                if (session.HasExited)
                {
                    await Task.Delay(30, token);
                    text = CleanCollectorTerminal(session.TextFrom(start));
                    if (LooksLikeAuthenticationFailure(text)) return new CollectorPromptResult { Kind = "authentication", Text = text };
                    return new CollectorPromptResult { Kind = "exit", Text = text };
                }
                int remaining = Math.Max(1, timeoutMs - (int)watch.ElapsedMilliseconds);
                await session.WaitForChangeAsync(Math.Min(250, remaining), token);
            }
            throw new TimeoutException("SSH prompt was not received within " + (timeoutMs / 1000) + " seconds.");
        }

        static string ExtractCollectorPrompt(string text)
        {
            Match prompt = Regex.Match(CleanCollectorTerminal(text), @"(?m)([A-Za-z0-9_.:/()@<>-]{1,160}[#>]|\[[^\r\n\]]{1,160}\])\s*$");
            return prompt.Success ? prompt.Groups[1].Value : "";
        }

        static void EnsureCollectorPrompt(CollectorPromptResult state, string host)
        {
            if (state != null && state.Kind == "prompt") return;
            string detail = state == null ? "No response." : (state.Text ?? "").Trim();
            if (state != null && state.Kind == "authentication")
                throw new CollectorAuthenticationException("Remote device rejected the supplied username, password, or enable secret.");
            if (state != null && state.Kind == "mfa")
                throw new CollectorAuthenticationException("The server requested MFA/OTP, which cannot be answered by an unattended collector profile.");
            if (state != null && state.Kind == "confirmation")
                throw new InvalidOperationException("A command requested interactive confirmation. Run a non-interactive show command instead.");
            if (state != null && state.Kind == "hostkey")
                throw new InvalidOperationException("SSH host key validation failed for " + host + ".");
            if (LooksLikeSshTransportFailure(detail)) throw new CollectorTransportException(detail);
            throw new CollectorTransportException(String.IsNullOrWhiteSpace(detail) ? "SSH session closed before the device prompt was received." : detail);
        }

        static string CleanCollectorTerminal(string value)
        {
            string clean = Regex.Replace(value ?? "", "\x1B\\[[0-?]*[ -/]*[@-~]", "");
            clean = Regex.Replace(clean, @"[^\r\n]\x08", "");
            return clean.Replace("\0", "");
        }

        static string RedactCollectorSecret(string value, string secret)
        {
            if (String.IsNullOrEmpty(secret)) return value ?? "";
            return (value ?? "").Replace(secret, "[REDACTED]");
        }

        async Task<CommandProcessResult> RunPlinkWithTransportRetryAsync(string plink, string arguments, string stdin,
            string host, CancellationToken token)
        {
            CommandProcessResult process = await RunCommandProcessResult(plink, arguments, stdin, token);
            string combined = CombineProcessOutput(process);
            if (process.ExitCode != 0 && IsTransientSshTransportFailure(combined))
            {
                AppendTerminal("[" + host + "] Transient SSH transport failure; retrying once...");
                await Task.Delay(500, token);
                process = await RunCommandProcessResult(plink, arguments, stdin, token);
            }
            return process;
        }

        static bool IsUnknownHostKey(string error)
        {
            return Regex.IsMatch(error ?? "", @"(?i)(host key is not cached|host key is not known|no guarantee that the server is the computer)");
        }

        static bool IsKeyboardInteractiveBatchFailure(string error)
        {
            return (error ?? "").IndexOf("Cannot answer interactive prompts in batch mode", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static bool IsTransientSshTransportFailure(string error)
        {
            return Regex.IsMatch(error ?? "", @"(?i)(software caused connection abort|connection reset by peer|network error:\s*connection timed out)");
        }

        static bool IsRetryableSshTransportFailure(string error)
        {
            return Regex.IsMatch(error ?? "", @"(?i)(software caused connection abort|connection reset by peer|connection timed out|prompt was not received|closed before the device prompt|connection unexpectedly closed)");
        }

        static bool LooksLikeSshTransportFailure(string error)
        {
            return Regex.IsMatch(error ?? "", @"(?i)(network error|connection refused|connection timed out|connection reset|connection abort|host does not exist|unable to open connection|connection unexpectedly closed|remote side unexpectedly closed|maximum (?:number of )?(?:vty )?(?:lines|sessions|connections)|vty (?:line|limit)|server refused to (?:start a shell|allocate pty))");
        }

        static string ClassifyCollectorFailureV120(string detail)
        {
            if (LooksLikeAuthenticationFailure(detail)) return "Authentication rejected";
            if (Regex.IsMatch(detail ?? "", @"(?i)(maximum (?:number of )?(?:vty )?(?:lines|sessions|connections)|vty (?:line|limit)|server refused to (?:start a shell|allocate pty))"))
                return "Device VTY/session limit";
            if (LooksLikeSshTransportFailure(detail)) return "Transport error";
            return "Other error";
        }

        static bool LooksLikeAuthenticationFailure(string text)
        {
            return Regex.IsMatch(text ?? "", @"(?i)(access denied|authentication failed|unable to authenticate|login incorrect|permission denied|wrong password|no supported authentication)");
        }

        static bool LooksLikeCommandFailure(string text)
        {
            return Regex.IsMatch(text ?? "", @"(?im)^\s*(?:%\s*)?(invalid input|unknown command|unrecognized command|error:|syntax error|incomplete command|ambiguous command)");
        }

        static string CombineProcessOutput(CommandProcessResult process)
        {
            string output = (process.Output ?? "").Trim();
            string error = (process.Error ?? "").Trim();
            if (error.Length == 0) return output;
            return (output + (output.Length == 0 ? "" : "\r\n") + "[STDERR]\r\n" + error).Trim();
        }

        async Task<string> RunCommandProcess(string executable, string arguments, string stdin, CancellationToken token)
        {
            CommandProcessResult result = await RunCommandProcessResult(executable, arguments, stdin, token);
            string combined = CombineProcessOutput(result);
            if (result.ExitCode != 0 && String.IsNullOrWhiteSpace(result.Output))
                throw new InvalidOperationException(combined.Length == 0 ? "Remote client exited with code " + result.ExitCode : combined);
            return combined;
        }

        async Task<CommandProcessResult> RunCommandProcessResult(string executable, string arguments, string stdin, CancellationToken token)
        {
            var info = new ProcessStartInfo(executable, arguments)
            {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true,
                RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = Encoding.UTF8
            };
            using (var process = new Process { StartInfo = info })
            {
                process.Start();
                process.StandardInput.Write(stdin); process.StandardInput.Flush(); process.StandardInput.Close();
                Task<string> output = process.StandardOutput.ReadToEndAsync();
                Task<string> error = process.StandardError.ReadToEndAsync();
                Task wait = Task.Run(delegate { process.WaitForExit(); });
                Task completed = await Task.WhenAny(wait, Task.Delay(120000, token));
                if (completed != wait)
                {
                    try { process.Kill(); } catch { }
                    token.ThrowIfCancellationRequested();
                    throw new TimeoutException("Remote session exceeded 120 seconds.");
                }
                string stdout = await output, stderr = await error;
                return new CommandProcessResult { ExitCode = process.ExitCode, Output = stdout, Error = stderr };
            }
        }

        async Task<string> RunTelnetAsync(string host, int port, string user, string pass, string commands, CancellationToken token)
        {
            string captureFile = Path.Combine(Path.GetTempPath(), "NetStuck-telnet-test-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                await RunTelnetToFileV120(host, port, user, pass, commands, captureFile, token);
                return File.ReadAllText(captureFile, Encoding.UTF8);
            }
            finally { try { if (File.Exists(captureFile)) File.Delete(captureFile); } catch { } }
        }

        async Task<string> RunTelnetToFileV120(string host, int port, string user, string pass, string commands,
            string captureFile, CancellationToken token)
        {
            using (var client = new TcpClient())
            {
                Task connect = client.ConnectAsync(host, port);
                if (await Task.WhenAny(connect, Task.Delay(10000, token)) != connect) throw new TimeoutException("Telnet connect timeout.");
                using (NetworkStream stream = client.GetStream())
                using (var output = new CollectorCaptureBuffer(captureFile))
                {
                    stream.ReadTimeout = 1000; stream.WriteTimeout = 5000;
                    await ReadTelnetBurst(stream, output, 700, token);
                    await WriteTelnet(stream, user + "\r\n", token); await ReadTelnetBurst(stream, output, 500, token);
                    await WriteTelnet(stream, pass + "\r\n", token); await ReadTelnetUntilPrompt(stream, output, 10000, token);
                    if (LooksLikeAuthenticationFailure(output.TailText)) { output.Flush(); return output.TailText; }
                    foreach (string command in commands.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        token.ThrowIfCancellationRequested();
                        await WriteTelnet(stream, command.Trim() + "\r\n", token);
                        await ReadTelnetUntilPrompt(stream, output, 60000, token);
                    }
                    await WriteTelnet(stream, "exit\r\n", token);
                    await ReadTelnetBurst(stream, output, 500, token);
                    output.Flush();
                    return output.TailText;
                }
            }
        }

        static async Task ReadTelnetUntilPrompt(NetworkStream stream, CollectorCaptureBuffer output, int maxMs, CancellationToken token)
        {
            byte[] buffer = new byte[8192];
            Stopwatch watch = Stopwatch.StartNew();
            DateTime lastData = DateTime.UtcNow;
            long initialLength = output.Length;
            while (watch.ElapsedMilliseconds < maxMs)
            {
                token.ThrowIfCancellationRequested();
                if (stream.DataAvailable)
                {
                    int read = await stream.ReadAsync(buffer, 0, buffer.Length, token);
                    if (read <= 0) break;
                    output.Append(StripTelnetNegotiation(Encoding.UTF8.GetString(buffer, 0, read)));
                    lastData = DateTime.UtcNow;
                    string recent = output.TextFrom(Math.Max(initialLength, output.Length - 400));
                    if (Regex.IsMatch(recent, @"(?m)[^\r\n]{0,120}[#>]\s*$")) return;
                    if (LooksLikeAuthenticationFailure(recent)) return;
                }
                else
                {
                    if (output.Length > initialLength && (DateTime.UtcNow - lastData).TotalMilliseconds >= 900) return;
                    await Task.Delay(35, token);
                }
            }
            if (output.Length == initialLength) throw new TimeoutException("Remote prompt was not received within " + (maxMs / 1000) + " seconds.");
        }

        static async Task ReadTelnetBurst(NetworkStream stream, CollectorCaptureBuffer output, int idleMs, CancellationToken token)
        {
            byte[] buffer = new byte[8192];
            DateTime until = DateTime.UtcNow.AddMilliseconds(idleMs);
            while (DateTime.UtcNow < until)
            {
                token.ThrowIfCancellationRequested();
                if (stream.DataAvailable)
                {
                    int read = await stream.ReadAsync(buffer, 0, buffer.Length, token);
                    if (read <= 0) break;
                    output.Append(StripTelnetNegotiation(Encoding.UTF8.GetString(buffer, 0, read)));
                    until = DateTime.UtcNow.AddMilliseconds(idleMs);
                }
                else await Task.Delay(40, token);
            }
        }

        static async Task WriteTelnet(NetworkStream stream, string text, CancellationToken token)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(text);
            await stream.WriteAsync(bytes, 0, bytes.Length, token); await stream.FlushAsync(token);
        }

        static string StripTelnetNegotiation(string value)
        {
            return Regex.Replace(value ?? "", "\u00ff[\u0000-\u00ff]{1,2}", "");
        }

        static string JoinCommands(string first, string second)
        {
            return String.Join("\r\n", new[] { first ?? "", second ?? "" }.Where(s => !String.IsNullOrWhiteSpace(s)));
        }

        static string DetectHostname(string output, string fallback)
        {
            Match m = Regex.Match(output ?? "", @"(?im)^\s*hostname\s+([A-Za-z0-9_.-]+)");
            if (m.Success) return m.Groups[1].Value;
            m = Regex.Match(output ?? "", @"(?m)^([A-Za-z0-9_.-]+)[#>]\s*$");
            return m.Success ? m.Groups[1].Value : fallback;
        }

        static string DetectHostnameFromFileV120(string file, string fallback)
        {
            try
            {
                using (var reader = new StreamReader(file, Encoding.UTF8, true, 65536))
                {
                    string line;
                    string prompt = "";
                    while ((line = reader.ReadLine()) != null)
                    {
                        Match hostname = Regex.Match(CleanCollectorTerminal(line), @"(?i)^\s*hostname\s+([A-Za-z0-9_.-]+)");
                        if (hostname.Success) return hostname.Groups[1].Value;
                        Match candidate = Regex.Match(CleanCollectorTerminal(line), @"^([A-Za-z0-9_.-]+)[#>]\s*$");
                        if (candidate.Success) prompt = candidate.Groups[1].Value;
                    }
                    if (prompt.Length > 0) return prompt;
                }
            }
            catch { }
            return fallback;
        }

        static void RedactCollectorFileV120(string file, IEnumerable<string> credentialSecrets)
        {
            string temporary = file + ".clean";
            string[] secrets = (credentialSecrets ?? Enumerable.Empty<string>()).Where(value => !String.IsNullOrEmpty(value)).Distinct().ToArray();
            using (var reader = new StreamReader(file, Encoding.UTF8, true, 65536))
            using (var writer = new StreamWriter(temporary, false, new UTF8Encoding(false), 65536))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    line = CleanCollectorTerminal(line);
                    foreach (string secret in secrets) line = RedactCollectorSecret(line, secret);
                    writer.WriteLine(line);
                }
            }
            File.Delete(file);
            File.Move(temporary, file);
        }

        void WriteCollectorJsonFromFileV120(string jsonFile, string outputFile, CollectorResult result, int port, string credentialName)
        {
            var metadata = new Dictionary<string, object>
            {
                { "device", result.Device }, { "hostname", result.Hostname }, { "protocol", result.Protocol },
                { "port", port }, { "deviceType", collectorDeviceType.Text }, { "authentication", credentialName },
                { "username", result.Username }, { "started", result.Started }, { "finished", result.Finished }
            };
            string prefix = new JavaScriptSerializer().Serialize(metadata);
            using (var writer = new StreamWriter(jsonFile, false, new UTF8Encoding(false), 65536))
            using (var reader = new StreamReader(outputFile, Encoding.UTF8, true, 65536))
            {
                writer.Write(prefix.Substring(0, prefix.Length - 1));
                writer.Write(",\"output\":\"");
                var buffer = new char[32768];
                int read;
                while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
                {
                    for (int i = 0; i < read; i++)
                    {
                        char value = buffer[i];
                        switch (value)
                        {
                            case '\"': writer.Write("\\\""); break;
                            case '\\': writer.Write("\\\\"); break;
                            case '\b': writer.Write("\\b"); break;
                            case '\f': writer.Write("\\f"); break;
                            case '\n': writer.Write("\\n"); break;
                            case '\r': writer.Write("\\r"); break;
                            case '\t': writer.Write("\\t"); break;
                            default:
                                if (value < 32) writer.Write("\\u" + ((int)value).ToString("x4"));
                                else writer.Write(value);
                                break;
                        }
                    }
                }
                writer.Write("\"}");
            }
        }

        static string SafeFileName(string value)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) value = value.Replace(c, '_');
            return value;
        }

        static string QuoteArg(string value)
        {
            value = value ?? "";
            var quoted = new StringBuilder("\"");
            int slashCount = 0;
            foreach (char character in value)
            {
                if (character == '\\')
                {
                    slashCount++;
                    continue;
                }
                if (character == '"')
                {
                    quoted.Append('\\', slashCount * 2 + 1);
                    quoted.Append('"');
                    slashCount = 0;
                    continue;
                }
                if (slashCount > 0)
                {
                    quoted.Append('\\', slashCount);
                    slashCount = 0;
                }
                quoted.Append(character);
            }
            if (slashCount > 0) quoted.Append('\\', slashCount * 2);
            quoted.Append('"');
            return quoted.ToString();
        }

        void AddCollectorRow(CollectorResult result, string detail)
        {
            if (appClosing || IsDisposed || Disposing) return;
            if (InvokeRequired) { try { BeginInvoke(new Action<CollectorResult, string>(AddCollectorRow), result, detail); } catch { } return; }
            collectorTable.Rows.Add(result.Device, result.Status, result.Protocol, result.Username, result.Hostname, result.File, detail);
            UpdateCollectorErrorExportStateV120();
        }

        void UpdateCollectorRow(CollectorResult result, string detail)
        {
            if (appClosing || IsDisposed || Disposing) return;
            if (InvokeRequired) { try { BeginInvoke(new Action<CollectorResult, string>(UpdateCollectorRow), result, detail); } catch { } return; }
            DataRow row = collectorTable.Rows.Cast<DataRow>().FirstOrDefault(r => String.Equals(Convert.ToString(r["Device"]), result.Device, StringComparison.OrdinalIgnoreCase));
            if (row == null) { AddCollectorRow(result, detail); return; }
            row["Status"] = result.Status; row["Protocol"] = result.Protocol; row["Username"] = result.Username ?? ""; row["Hostname"] = result.Hostname; row["File"] = result.File; row["Detail"] = detail;
            UpdateCollectorErrorExportStateV120();
        }

        void AppendTerminal(string text)
        {
            if (appClosing || IsDisposed || Disposing) return;
            collectorTerminalQueueV120.Enqueue(text ?? "");
            if (Interlocked.Exchange(ref collectorTerminalFlushScheduledV120, 1) != 0) return;
            try { BeginInvoke(new Action(DrainCollectorTerminalQueueV120)); }
            catch { Interlocked.Exchange(ref collectorTerminalFlushScheduledV120, 0); }
        }

        void DrainCollectorTerminalQueueV120()
        {
            if (appClosing || IsDisposed || Disposing || collectorTerminal == null) return;
            if (InvokeRequired) { try { BeginInvoke(new Action(DrainCollectorTerminalQueueV120)); } catch { } return; }
            Interlocked.Exchange(ref collectorTerminalFlushScheduledV120, 0);
            var batch = new StringBuilder();
            string line;
            int count = 0;
            while (count < 256 && batch.Length < 131072 && collectorTerminalQueueV120.TryDequeue(out line))
            {
                if (batch.Length > 0 || collectorTerminal.TextLength > 0) batch.Append("\r\n");
                batch.Append(line);
                count++;
            }
            if (batch.Length == 0) return;
            const int terminalLimit = 2000000;
            bool followTail = collectorTerminal.SelectionStart >= Math.Max(0, collectorTerminal.TextLength - 512);
            collectorTerminal.SuspendLayout();
            collectorTerminal.AppendText(batch.ToString());
            if (collectorTerminal.TextLength > terminalLimit)
            {
                collectorTerminal.SelectionStart = 0;
                collectorTerminal.SelectionLength = collectorTerminal.TextLength - terminalLimit;
                collectorTerminal.SelectedText = "";
            }
            if (followTail)
            {
                collectorTerminal.SelectionStart = collectorTerminal.TextLength;
                if (collectorTerminal.Visible) collectorTerminal.ScrollToCaret();
            }
            collectorTerminal.ResumeLayout();
            collectorTerminalFlushCountV120++;
            if (!collectorTerminalQueueV120.IsEmpty && Interlocked.Exchange(ref collectorTerminalFlushScheduledV120, 1) == 0)
                try { BeginInvoke(new Action(DrainCollectorTerminalQueueV120)); } catch { }
        }

        void FormatCollectorCell(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || collectorGrid.Columns[e.ColumnIndex].DataPropertyName != "Status") return;
            string value = Convert.ToString(e.Value);
            e.CellStyle.Font = gridBoldFont;
            e.CellStyle.ForeColor = value == "Completed" || value == "Reachable" ? Success : value == "Running" ? Warning : Danger;
        }

        void OpenCollectorResult(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            string file = Convert.ToString(collectorGrid.Rows[e.RowIndex].Cells["File"].Value);
            if (File.Exists(file)) Process.Start(new ProcessStartInfo(file) { UseShellExecute = true });
        }

        static bool IsCollectorErrorStatusV120(string status)
        {
            return String.Equals(status, "Failed", StringComparison.OrdinalIgnoreCase)
                || String.Equals(status, "Unreachable", StringComparison.OrdinalIgnoreCase)
                || String.Equals(status, "Cancelled", StringComparison.OrdinalIgnoreCase);
        }

        void UpdateCollectorErrorExportStateV120()
        {
            if (collectorExportLog == null || collectorTable == null) return;
            collectorExportLog.Enabled = collectorTable.Rows.Cast<DataRow>()
                .Any(row => IsCollectorErrorStatusV120(Convert.ToString(row["Status"])));
        }

        string BuildCollectorErrorCsvV120()
        {
            var csv = new StringBuilder();
            csv.AppendLine("IP,Status,Protocol,Username,Detail");
            if (collectorTable != null)
            {
                foreach (DataRow row in collectorTable.Rows.Cast<DataRow>()
                    .Where(row => IsCollectorErrorStatusV120(Convert.ToString(row["Status"]))))
                {
                    csv.AppendLine(String.Join(",", new[] { "Device", "Status", "Protocol", "Username", "Detail" }
                        .Select(column => CollectorCsvCellV120(row[column]))));
                }
            }
            return csv.ToString();
        }

        static string CollectorCsvCellV120(object value)
        {
            string text = Convert.ToString(value).Replace("\r", " ").Replace("\n", " ");
            return "\"" + text.Replace("\"", "\"\"") + "\"";
        }

        void ExportCollectorLog(object sender, EventArgs e)
        {
            string content = BuildCollectorErrorCsvV120();
            using (var dialog = new SaveFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                FileName = "NetStuck-Collector-Errors-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".csv",
                InitialDirectory = Directory.Exists(collectorFolderBox.Text) ? collectorFolderBox.Text : ""
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                File.WriteAllText(dialog.FileName, content, new UTF8Encoding(false));
                appStatus.Text = "Collector error CSV exported";
                Log("ACTION", "Collector", "Error CSV exported: " + dialog.FileName);
            }
        }

        void BrowseCollectorFolder(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog { Description = "Choose the folder for captured configurations", SelectedPath = collectorFolderBox.Text })
                if (dialog.ShowDialog(this) == DialogResult.OK) collectorFolderBox.Text = dialog.SelectedPath;
        }

        void OpenCollectorFolder(object sender, EventArgs e)
        {
            string folder = collectorFolderBox.Text.Trim();
            if (folder.Length == 0) return;
            Directory.CreateDirectory(folder);
            Process.Start(new ProcessStartInfo("explorer.exe", QuoteArg(folder)) { UseShellExecute = true });
        }

        void SelectPingHistory(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            object value = pingGrid.Rows[e.RowIndex].Cells["Host"].Value;
            string host = Convert.ToString(value);
            if (host.Length == 0) return;
            selectedPingHistoryHost = host;
            pingHistorySource.RaiseListChangedEvents = false;
            pingHistoryDisplayTable.BeginLoadData();
            try
            {
                pingHistoryDisplayTable.Rows.Clear();
                foreach (DataRow row in pingHistoryTable.Rows.Cast<DataRow>()
                    .Where(r => String.Equals(Convert.ToString(r["Host"]), host, StringComparison.OrdinalIgnoreCase)))
                    pingHistoryDisplayTable.Rows.Add((object[])row.ItemArray.Clone());
            }
            finally
            {
                pingHistoryDisplayTable.EndLoadData();
                pingHistorySource.RaiseListChangedEvents = true;
                pingHistorySource.ResetBindings(false);
            }
            pingHistoryTitle.Text = "PING HISTORY  •  " + host + "  •  newest events at the bottom";
            if (pingHistoryGrid.Rows.Count > 0) pingHistoryGrid.FirstDisplayedScrollingRowIndex = pingHistoryGrid.Rows.Count - 1;
        }

        void TogglePingCards(Button button)
        {
            bool show = pingRoot.RowStyles[0].Height < 1;
            pingRoot.RowStyles[0].Height = show ? 64 : 0;
            button.Text = show ? "Hide cards" : "Show cards";
        }

        void ShowPingColumnChooser(object sender, EventArgs e)
        {
            using (var dialog = new Form { Text = "Realtime result columns", Width = 365, Height = 520, StartPosition = FormStartPosition.CenterParent, BackColor = Canvas, Font = Font, MinimizeBox = false, MaximizeBox = false, ShowInTaskbar = false })
            {
                var list = new CheckedListBox { Dock = DockStyle.Fill, CheckOnClick = true, BorderStyle = BorderStyle.FixedSingle };
                foreach (DataGridViewColumn column in pingGrid.Columns) list.Items.Add(column.HeaderText, column.Visible);
                var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 50, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(6) };
                var ok = ActionButton("Apply", true, 90); ok.DialogResult = DialogResult.OK;
                var all = ActionButton("Select all", false, 90); all.Click += delegate { for (int i = 0; i < list.Items.Count; i++) list.SetItemChecked(i, true); };
                bar.Controls.Add(ok); bar.Controls.Add(all); dialog.Controls.Add(list); dialog.Controls.Add(bar); dialog.AcceptButton = ok;
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    if (list.CheckedItems.Count == 0) { MessageBox.Show(this, "Keep at least one column visible."); return; }
                    for (int i = 0; i < pingGrid.Columns.Count; i++) pingGrid.Columns[i].Visible = list.GetItemChecked(i);
                }
            }
        }

        void RememberTraceTarget(string host)
        {
            if (String.IsNullOrWhiteSpace(host)) return;
            int existing = traceTarget.FindStringExact(host);
            if (existing >= 0) traceTarget.Items.RemoveAt(existing);
            traceTarget.Items.Insert(0, host);
            while (traceTarget.Items.Count > 30) traceTarget.Items.RemoveAt(traceTarget.Items.Count - 1);
            traceTarget.Text = host;
        }

        void ConfigureEqualSplit(TabPage page, SplitContainer split, int panel1Min, int panel2Min)
        {
            bool configured = false;
            LayoutEventHandler handler = null;
            handler = delegate
            {
                int width = split.ClientSize.Width;
                if (configured || width < panel1Min + panel2Min + split.SplitterWidth) return;
                split.Panel1MinSize = panel1Min; split.Panel2MinSize = panel2Min;
                split.SplitterDistance = (width - split.SplitterWidth) / 2;
                configured = true; page.Layout -= handler;
            };
            page.Layout += handler;
        }

        async Task SynchronizeClockAsync()
        {
            timeSourceStatus.Text = "Time: syncing NTP…";
            foreach (string server in new[] { "time.cloudflare.com", "pool.ntp.org", "time.google.com" })
            {
                try
                {
                    DateTime utc = await QueryNtpAsync(server, 2200);
                    clockBaseUtc = utc; clockElapsed = Stopwatch.StartNew(); clockNtp = true;
                    timeSourceStatus.Text = "Time: NTP • " + server;
                    UpdateClockDisplay(); Log("INFO", "Clock", "Synchronized with " + server); return;
                }
                catch { }
            }
            clockBaseUtc = DateTime.UtcNow; clockElapsed = Stopwatch.StartNew(); clockNtp = false;
            timeSourceStatus.Text = "Time: Local fallback";
            UpdateClockDisplay(); Log("WARNING", "Clock", "Internet NTP unavailable; using Windows local clock");
        }

        static async Task<DateTime> QueryNtpAsync(string host, int timeoutMs)
        {
            IPAddress[] addresses = await Dns.GetHostAddressesAsync(host);
            IPAddress address = addresses.First(a => a.AddressFamily == AddressFamily.InterNetwork);
            using (var udp = new UdpClient(AddressFamily.InterNetwork))
            {
                udp.Connect(new IPEndPoint(address, 123));
                byte[] request = new byte[48]; request[0] = 0x1B;
                await udp.SendAsync(request, request.Length);
                Task<UdpReceiveResult> receive = udp.ReceiveAsync();
                if (await Task.WhenAny(receive, Task.Delay(timeoutMs)) != receive) throw new TimeoutException("NTP timeout");
                byte[] data = (await receive).Buffer;
                if (data.Length < 48) throw new InvalidDataException("Invalid NTP response");
                ulong seconds = ((ulong)data[40] << 24) | ((ulong)data[41] << 16) | ((ulong)data[42] << 8) | data[43];
                ulong fraction = ((ulong)data[44] << 24) | ((ulong)data[45] << 16) | ((ulong)data[46] << 8) | data[47];
                DateTime epoch = new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                return epoch.AddSeconds(seconds + fraction / 4294967296d);
            }
        }

        void UpdateClockDisplay()
        {
            DateTime utc = clockElapsed == null ? DateTime.UtcNow : clockBaseUtc.Add(clockElapsed.Elapsed);
            clockStatus.Text = utc.ToLocalTime().ToString("yyyy-MM-dd  HH:mm:ss") + (clockNtp ? " ICT" : "");
        }

        void EnableCtrlWheelZoom(Control root)
        {
            RegisterZoomControl(root);
            foreach (Control child in root.Controls) EnableCtrlWheelZoom(child);
        }

        void RegisterZoomControl(Control control)
        {
            if (!(control is TextBox) && !(control is RichTextBox) && !(control is DataGridView)) return;
            if (!zoomBaseFonts.ContainsKey(control)) zoomBaseFonts[control] = control.Font.Size / Math.Max(0.1f, zoomScale);
            control.MouseWheel += ZoomMouseWheel;
        }

        void ZoomMouseWheel(object sender, MouseEventArgs e)
        {
            if ((ModifierKeys & Keys.Control) != Keys.Control) return;
            zoomScale = Math.Max(0.7f, Math.Min(1.8f, zoomScale + (e.Delta > 0 ? 0.1f : -0.1f)));
            ApplyZoom();
            appStatus.Text = "Zoom " + Math.Round(zoomScale * 100) + "%";
        }

        void ApplyZoom()
        {
            foreach (KeyValuePair<Control, float> pair in zoomBaseFonts.ToArray())
            {
                if (pair.Key.IsDisposed) continue;
                pair.Key.Font = new Font(pair.Key.Font.FontFamily, Math.Max(7f, pair.Value * zoomScale), pair.Key.Font.Style);
                var grid = pair.Key as DataGridView;
                if (grid != null)
                {
                    grid.RowTemplate.Height = Math.Max(24, (int)Math.Round(32 * zoomScale));
                    grid.ColumnHeadersHeight = Math.Max(28, (int)Math.Round(36 * zoomScale));
                    grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", Math.Max(7f, 9f * zoomScale));
                }
            }
        }

        void LoadAppState()
        {
            try
            {
                if (!File.Exists(statePath)) return;
                AppState s = new JavaScriptSerializer().Deserialize<AppState>(File.ReadAllText(statePath, Encoding.UTF8));
                if (s == null) return;
                if (s.Width >= MinimumSize.Width && s.Height >= MinimumSize.Height) Size = new Size(s.Width, s.Height);
                Rectangle screens = SystemInformation.VirtualScreen;
                if (screens.Contains(new Point(s.Left + 40, s.Top + 40))) Location = new Point(s.Left, s.Top);
                targetInput.Text = s.PingTargets ?? targetInput.Text;
                pingInterval.Value = Clamp(s.PingInterval, pingInterval.Minimum, pingInterval.Maximum);
                pingTimeout.Value = Clamp(s.PingTimeout, pingTimeout.Minimum, pingTimeout.Maximum);
                pingUseCustomDns.Checked = s.PingCustomDns; pingDnsServer.Text = s.PingDns ?? pingDnsServer.Text;
                if (s.PingColumns != null && s.PingColumns.Count > 0)
                    foreach (DataGridViewColumn column in pingGrid.Columns)
                        column.Visible = s.PingColumns.Contains(column.Name) || (s.StateVersion < 3 && new[] { "Seq", "SourceIp", "Protocol", "Port" }.Contains(column.Name));
                pingRoot.RowStyles[0].Height = s.PingCardsVisible ? 64 : 0;
                if (s.StateVersion >= 3)
                {
                    SourceAddressOption savedSource = pingSourceIp.Items.Cast<object>().OfType<SourceAddressOption>().FirstOrDefault(x => String.Equals(x.Address, s.PingSourceIp, StringComparison.OrdinalIgnoreCase));
                    if (savedSource != null) pingSourceIp.SelectedItem = savedSource;
                    if (pingProtocol.Items.Contains(s.PingProtocol)) pingProtocol.SelectedItem = s.PingProtocol;
                    if (s.PingPort > 0) pingPort.Value = Clamp(s.PingPort, pingPort.Minimum, pingPort.Maximum);
                    if (s.PingPacketSize >= 0) pingPacketSize.Value = Clamp(s.PingPacketSize, pingPacketSize.Minimum, pingPacketSize.Maximum);
                }
                traceTarget.Items.Clear();
                if (s.TraceHistory != null) foreach (string item in s.TraceHistory) traceTarget.Items.Add(item);
                traceTarget.Text = String.IsNullOrWhiteSpace(s.TraceTarget) ? "8.8.8.8" : s.TraceTarget;
                traceMaxHops.Value = Clamp(s.TraceMaxHops, traceMaxHops.Minimum, traceMaxHops.Maximum);
                traceTimeout.Value = Clamp(s.TraceTimeout, traceTimeout.Minimum, traceTimeout.Maximum);
                traceInterval.Value = Clamp(s.TraceInterval, traceInterval.Minimum, traceInterval.Maximum);
                traceContinuous.Checked = s.TraceContinuous; traceResolveNames.Checked = s.TraceResolve;
                traceUseCustomDns.Checked = s.TraceCustomDns; traceDnsServer.Text = s.TraceDns ?? traceDnsServer.Text;
                traceHopInfoText = s.TraceHopInfo ?? "";
                if (s.StateVersion >= 3 && traceSessionsV103.Count >= 2)
                {
                    TraceSessionV103 one = traceSessionsV103[0], two = traceSessionsV103[1];
                    if (one.Protocol.Items.Contains(s.TraceProtocol1)) one.Protocol.SelectedItem = s.TraceProtocol1;
                    if (two.Protocol.Items.Contains(s.TraceProtocol2)) two.Protocol.SelectedItem = s.TraceProtocol2;
                    if (s.TracePort1 > 0) one.Port.Value = Clamp(s.TracePort1, one.Port.Minimum, one.Port.Maximum);
                    if (s.TracePort2 > 0) two.Port.Value = Clamp(s.TracePort2, two.Port.Minimum, two.Port.Maximum);
                    if (s.TracePacketSize1 >= 0) one.PacketSize.Value = Clamp(s.TracePacketSize1, one.PacketSize.Minimum, one.PacketSize.Maximum);
                    if (s.TracePacketSize2 >= 0) two.PacketSize.Value = Clamp(s.TracePacketSize2, two.PacketSize.Minimum, two.PacketSize.Maximum);
                    two.Target.Items.Clear(); if (s.TraceHistory2 != null) foreach (string item in s.TraceHistory2) two.Target.Items.Add(item);
                    two.Target.Text = String.IsNullOrWhiteSpace(s.TraceTarget2) ? "1.1.1.1" : s.TraceTarget2;
                    if (s.TraceMaxHops2 > 0) two.MaxHops.Value = Clamp(s.TraceMaxHops2, two.MaxHops.Minimum, two.MaxHops.Maximum);
                    if (s.TraceTimeout2 > 0) two.Timeout.Value = Clamp(s.TraceTimeout2, two.Timeout.Minimum, two.Timeout.Maximum);
                    if (s.TraceInterval2 > 0) two.Interval.Value = Clamp(s.TraceInterval2, two.Interval.Minimum, two.Interval.Maximum);
                    if (s.TraceSelectedSession >= 0 && s.TraceSelectedSession < traceSessionTabs.TabCount) traceSessionTabs.SelectedIndex = s.TraceSelectedSession;
                }
                dnsInput.Text = s.DnsInput ?? dnsInput.Text; dnsUseCustom.Checked = s.DnsCustom; dnsServer.Text = s.DnsServer ?? dnsServer.Text;
                if (s.DnsPollInterval > 0) dnsPollInterval.Value = Clamp(s.DnsPollInterval, dnsPollInterval.Minimum, dnsPollInterval.Maximum);
                macInput.Text = s.MacInput ?? macInput.Text; wanInput.Text = s.WanInput ?? wanInput.Text;
                subnetInput.Text = s.SubnetInput ?? subnetInput.Text; unitValue.Text = s.UnitValue ?? unitValue.Text;
                if (unitFrom.Items.Contains(s.UnitFrom)) unitFrom.SelectedItem = s.UnitFrom;
                if (unitTo.Items.Contains(s.UnitTo)) unitTo.SelectedItem = s.UnitTo;
                if (collectorProtocol.Items.Contains(s.CollectorProtocol)) collectorProtocol.SelectedItem = s.CollectorProtocol;
                if (collectorDeviceType.Items.Contains(s.CollectorDeviceType)) collectorDeviceType.SelectedItem = s.CollectorDeviceType;
                collectorPort.Value = Clamp(s.CollectorPort, collectorPort.Minimum, collectorPort.Maximum);
                collectorConcurrency.Value = Clamp(s.CollectorConcurrency, collectorConcurrency.Minimum, collectorConcurrency.Maximum);
                collectorDevices.Text = s.CollectorDevices ?? collectorDevices.Text; collectorBasic.Text = s.CollectorBasic ?? collectorBasic.Text; collectorCommands.Text = s.CollectorCommands ?? collectorCommands.Text;
                collectorAuth1User.Text = s.CollectorAuth1User ?? ""; collectorAuth2User.Text = s.CollectorAuth2User ?? ""; collectorJson.Checked = s.CollectorJson;
                if (s.StateVersion >= 2)
                {
                    collectorUseAuth1.Checked = s.CollectorUseAuth1; collectorUseAuth2.Checked = s.CollectorUseAuth2;
                    collectorStrictHostKey.Checked = s.CollectorStrictHostKey; collectorContinueError.Checked = s.CollectorContinueError;
                }
                if (!String.IsNullOrWhiteSpace(s.CollectorFolder)) collectorFolderBox.Text = s.CollectorFolder;
                zoomScale = s.Zoom >= 0.7f && s.Zoom <= 1.8f ? s.Zoom : 1f;
                int selectedTab = s.SelectedTab;
                if (s.StateVersion < 5)
                {
                    // v1.1.0 removed Log Sanitizer at the old index 5.
                    if (selectedTab == 5) selectedTab = 4;
                    else if (selectedTab > 5) selectedTab--;
                }
                if (selectedTab >= 0 && selectedTab < tabs.TabCount) tabs.SelectedIndex = selectedTab;
                if (s.Maximized) WindowState = FormWindowState.Maximized;
            }
            catch (Exception ex) { Log("WARNING", "State", "Could not restore previous state: " + FriendlyError(ex)); }
        }

        void SaveAppState()
        {
            try
            {
                Rectangle bounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
                var s = new AppState
                {
                    StateVersion = 6,
                    Width = bounds.Width, Height = bounds.Height, Left = bounds.Left, Top = bounds.Top, Maximized = WindowState == FormWindowState.Maximized,
                    SelectedTab = tabs.SelectedIndex, PingTargets = targetInput.Text, PingInterval = (int)pingInterval.Value, PingTimeout = (int)pingTimeout.Value,
                    PingCustomDns = pingUseCustomDns.Checked, PingDns = pingDnsServer.Text, PingCardsVisible = pingRoot.RowStyles[0].Height > 0,
                    PingColumns = pingGrid.Columns.Cast<DataGridViewColumn>().Where(c => c.Visible).Select(c => c.Name).ToList(),
                    PingSourceIp = SelectedPingSourceAddress(), PingAdvanced = true, PingProtocol = pingProtocol.Text,
                    PingPort = (int)pingPort.Value, PingPacketSize = (int)pingPacketSize.Value,
                    TraceTarget = traceTarget.Text, TraceHistory = traceTarget.Items.Cast<object>().Select(Convert.ToString).ToList(),
                    TraceMaxHops = (int)traceMaxHops.Value, TraceTimeout = (int)traceTimeout.Value, TraceInterval = (int)traceInterval.Value,
                    TraceContinuous = traceContinuous.Checked, TraceResolve = traceResolveNames.Checked, TraceCustomDns = traceUseCustomDns.Checked, TraceDns = traceDnsServer.Text,
                    TraceHopInfo = traceHopInfoText,
                    TraceTarget2 = traceSessionsV103.Count > 1 ? traceSessionsV103[1].Target.Text : "",
                    TraceHistory2 = traceSessionsV103.Count > 1 ? traceSessionsV103[1].Target.Items.Cast<object>().Select(Convert.ToString).ToList() : new List<string>(),
                    TraceProtocol1 = traceSessionsV103.Count > 0 ? traceSessionsV103[0].Protocol.Text : "ICMP",
                    TraceProtocol2 = traceSessionsV103.Count > 1 ? traceSessionsV103[1].Protocol.Text : "ICMP",
                    TracePort1 = traceSessionsV103.Count > 0 ? (int)traceSessionsV103[0].Port.Value : 443,
                    TracePort2 = traceSessionsV103.Count > 1 ? (int)traceSessionsV103[1].Port.Value : 443,
                    TracePacketSize1 = traceSessionsV103.Count > 0 ? (int)traceSessionsV103[0].PacketSize.Value : 32,
                    TracePacketSize2 = traceSessionsV103.Count > 1 ? (int)traceSessionsV103[1].PacketSize.Value : 32,
                    TraceMaxHops2 = traceSessionsV103.Count > 1 ? (int)traceSessionsV103[1].MaxHops.Value : 24,
                    TraceTimeout2 = traceSessionsV103.Count > 1 ? (int)traceSessionsV103[1].Timeout.Value : 1000,
                    TraceInterval2 = traceSessionsV103.Count > 1 ? (int)traceSessionsV103[1].Interval.Value : 1000,
                    TraceSelectedSession = traceSessionTabs == null ? 0 : traceSessionTabs.SelectedIndex,
                    DnsInput = dnsInput.Text, DnsCustom = dnsUseCustom.Checked, DnsServer = dnsServer.Text, DnsPollInterval = (int)dnsPollInterval.Value, MacInput = macInput.Text, WanInput = wanInput.Text,
                    SubnetInput = subnetInput.Text, UnitValue = unitValue.Text, UnitFrom = Convert.ToString(unitFrom.SelectedItem), UnitTo = Convert.ToString(unitTo.SelectedItem),
                    CollectorProtocol = collectorProtocol.Text, CollectorDeviceType = collectorDeviceType.Text, CollectorPort = (int)collectorPort.Value, CollectorConcurrency = (int)collectorConcurrency.Value,
                    CollectorDevices = collectorDevices.Text, CollectorBasic = collectorBasic.Text, CollectorCommands = collectorCommands.Text,
                    CollectorAuth1User = collectorAuth1User.Text, CollectorAuth2User = collectorAuth2User.Text,
                    CollectorUseAuth1 = collectorUseAuth1.Checked, CollectorUseAuth2 = collectorUseAuth2.Checked,
                    CollectorStrictHostKey = collectorStrictHostKey.Checked, CollectorContinueError = collectorContinueError.Checked,
                    CollectorJson = collectorJson.Checked, CollectorFolder = collectorFolderBox.Text,
                    Zoom = zoomScale
                };
                Directory.CreateDirectory(Path.GetDirectoryName(statePath));
                File.WriteAllText(statePath, new JavaScriptSerializer().Serialize(s), new UTF8Encoding(false));
            }
            catch { }
        }
    }
}
