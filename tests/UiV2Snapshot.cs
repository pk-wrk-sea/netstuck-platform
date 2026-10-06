using System;
using System.Data;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using NetStuck;

// Sanitized structural captures at the real host DPI; no probe or API is run.
static class UiV2Snapshot
{
    [StructLayout(LayoutKind.Sequential)] struct NativeRect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] struct ComboInfo { public int Size; public NativeRect Item, Button; public int State; public IntPtr Combo, Edit, List; }
    [DllImport("user32.dll")] static extern bool GetComboBoxInfo(IntPtr combo, ref ComboInfo info);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr window, out NativeRect rect);
    [DllImport("user32.dll")] static extern bool PrintWindow(IntPtr window, IntPtr dc, uint flags);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr window);
    static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static object Get(MainForm form, string name) { return typeof(MainForm).GetField(name, Private).GetValue(form); }
    static void Call(MainForm form, string name, params object[] args) { typeof(MainForm).GetMethod(name, Private).Invoke(form, args); }
    static void Pump() { for (int i = 0; i < 12; i++) { Application.DoEvents(); Thread.Sleep(10); } }
    [STAThread]
    static int Main(string[] args)
    {
        if (args.Length < 1 || args.Length > 2) return 2;
        string output = Path.GetFullPath(args[0]);
        string root = Path.Combine(Path.GetTempPath(), "NetStuck-v2-capture-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root); Directory.CreateDirectory(output);
        Environment.SetEnvironmentVariable("NETSTUCK_TEST_ROOT", root);
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        try
        {
            using (var form = new MainForm())
            {
                ((System.Windows.Forms.Timer)Get(form, "clockTimer")).Stop();
                ((ToolStripStatusLabel)Get(form, "clockStatus")).Text = "2000-01-02 03:04:05 UTC+07:00";
                ((ToolStripStatusLabel)Get(form, "timeSourceStatus")).Text = "Time: fixture";
                ((ToolStripStatusLabel)Get(form, "localIpStatus")).Text = "Local IP: 192.0.2.10";
                ((ToolStripStatusLabel)Get(form, "publicIpStatus")).Text = "Public IP: 203.0.113.10";
                ((TextBox)Get(form, "targetInput")).Text = "192.0.2.1 Core gateway\r\n192.0.2.2 Branch edge\r\n198.51.100.10 Example service\r\n192.0.2.20 Access switch";
                ((TextBox)Get(form, "collectorDevices")).Text = "192.0.2.1 Example-router\r\n192.0.2.2 Example-switch";
                ((TextBox)Get(form, "collectorFolderBox")).Text = "Portable output folder";
                ((TextBox)Get(form, "pingDnsServer")).Text = "192.0.2.53";
                ((TextBox)Get(form, "dnsInput")).Text = "example.test\r\n192.0.2.2";
                ((TextBox)Get(form, "dnsServer")).Text = "192.0.2.53";
                ((TextBox)Get(form, "macInput")).Text = "02:00:00:00:00:01";
                ((TextBox)Get(form, "wanInput")).Text = "203.0.113.10";
                ((TextBox)Get(form, "subnetInput")).Text = "192.0.2.0/24";
                int sessionNumber = 0;
                foreach (object session in (System.Collections.IEnumerable)Get(form, "traceSessionsV103"))
                    ((ComboBox)session.GetType().GetField("Target").GetValue(session)).Text = "192.0.2." + (++sessionNumber);
                foreach (Control control in Flat(form))
                    if (control is Label && control.Text.StartsWith("Examples:")) control.Text = "Examples: 192.0.2.1 SW-01  |  198.51.100.0/24";
                var engine = typeof(MainForm).Assembly.GetType("NetStuck.UpdateEngine");
                string versionsJson = "[{\"tag_name\":\"v1.3.5\",\"assets\":[{\"name\":\"NetStuck-v.1.3.5.zip\",\"size\":100,\"browser_download_url\":\"https://github.com/pk-wrk-sea/netstuck-platform/releases/download/v1.3.5/NetStuck-v.1.3.5.zip\"},{\"name\":\"NetStuck-v.1.3.5.zip.sha256.txt\",\"size\":87,\"browser_download_url\":\"https://github.com/pk-wrk-sea/netstuck-platform/releases/download/v1.3.5/NetStuck-v.1.3.5.zip.sha256.txt\"}]}]";
                foreach (object choice in (System.Collections.IEnumerable)engine.GetMethod("PreviousPackages").Invoke(null, new object[] { versionsJson, typeof(MainForm).Assembly.GetName().Version }))
                    ((ComboBox)Get(form, "previousVersionCombo")).Items.Add(choice);
                ((ComboBox)Get(form, "previousVersionCombo")).SelectedIndex = 0;
                var source = (ComboBox)Get(form, "pingSourceIp"); source.Items.Clear(); source.Items.Add("Automatic (Windows route)"); source.SelectedIndex = 0;
                ((DataTable)Get(form, "logTable")).Clear();
                var table = (DataTable)Get(form, "pingTable");
                for (int i = 1; i <= 20; i++)
                {
                    var row = table.NewRow(); row["Seq"] = i; row["Host"] = "192.0.2." + i; row["Description"] = "Example device " + i;
                    row["ResolvedIp"] = "192.0.2." + i; row["SourceIp"] = "192.0.2.10"; row["Protocol"] = "ICMP";
                    row["Status"] = i == 4 ? "Unreachable" : "ICMP OK"; row["LastMs"] = i == 4 ? (object)DBNull.Value : (double)(8 + i);
                    row["AvgMs"] = 12.25; row["MinMs"] = 3.0; row["MaxMs"] = 34.0; row["Sent"] = 100; row["Received"] = i == 4 ? 98 : 100;
                    row["Lost"] = i == 4 ? 2 : 0; row["LossPct"] = i == 4 ? 2.0 : 0.0; row["LastPing"] = new DateTime(2000, 1, 2, 3, 4, 5); table.Rows.Add(row);
                }
                var history = (DataTable)Get(form, "pingHistoryDisplayTable");
                for (int i = 0; i < 8; i++) history.Rows.Add(new DateTime(2000, 1, 2, 3, 4, i), "192.0.2.1", "192.0.2.1", 8L + i, 64, "Succeeded", 100L + i, "ICMP reply from 192.0.2.1");
                ((SplitContainer)Get(form, "pingResultSplit")).Panel2Collapsed = false;
                ((Label)Get(form, "pingHistoryTitle")).Text = "PING HISTORY  |  192.0.2.1  |  newest events at the bottom";
                form.Show(); Pump();
                using (var graphics = form.CreateGraphics()) Console.WriteLine("EVIDENCE capture DPI=" + graphics.DpiX + "; working area=" + Screen.FromControl(form).WorkingArea.Size);
                var tabs = (TabControl)Get(form, "tabs");
                foreach (bool dark in new[] { false, true })
                {
                    Call(form, "SetApplicationTheme", dark);
                    foreach (var size in new[] { new Size(1366, 768), new Size(1440, 900), new Size(1920, 1080), new Size(1100, 700) })
                    {
                        form.Size = size; Pump();
                        foreach (TabPage page in tabs.TabPages)
                        {
                            tabs.SelectedTab = page; Pump();
                            var grid = (DataGridView)Get(form, "pingGrid"); grid.ClearSelection(); if (grid.Rows.Count > 0) grid.Rows[0].Selected = true;
                            using (var bitmap = new Bitmap(form.Width, form.Height))
                            {
                                form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
                                string name = page.Text.Replace(" / ", "-").Replace(" ", "-").ToLowerInvariant();
                                bitmap.Save(Path.Combine(output, name + "-" + (dark ? "dark" : "light") + "-" + size.Width + "x" + size.Height + ".png"));
                            }
                        }
                    }
                }
                if (args.Length == 2 && args[1] == "--native")
                {
                    foreach (bool dark in new[] { false, true })
                    {
                        Call(form, "SetApplicationTheme", dark);
                        foreach (var size in new[] { new Size(1440, 900), new Size(1100, 700) })
                        {
                            form.Size = size; tabs.SelectedIndex = 0; Pump();
                            CapturePopup(form, (ComboBox)Get(form, "themeSelector"), Path.Combine(output, "native-theme-" + (dark ? "dark" : "light") + "-" + size.Width + ".png"));
                            tabs.SelectedIndex = 1; Pump();
                            object primary = ((System.Collections.IList)Get(form, "traceSessionsV103"))[0];
                            CapturePopup(form, (ComboBox)primary.GetType().GetField("Protocol").GetValue(primary), Path.Combine(output, "native-protocol-" + (dark ? "dark" : "light") + "-" + size.Width + ".png"));
                            tabs.SelectedIndex = 7; Pump();
                            CapturePopup(form, (ComboBox)Get(form, "previousVersionCombo"), Path.Combine(output, "native-version-" + (dark ? "dark" : "light") + "-" + size.Width + ".png"));
                        }
                    }
                    Console.WriteLine("PASS native dropdown HWND renders over sanitized form (automated opening; no screen or physical pointer claim)");
                }
                form.Close();
            }
            Console.WriteLine("PASS sanitized V2 structural screenshot matrix"); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally
        {
            Environment.SetEnvironmentVariable("NETSTUCK_TEST_ROOT", null);
            Directory.Delete(root, true);
            if (Directory.Exists(root)) throw new IOException("Owned capture state remains.");
        }
    }
    static System.Collections.Generic.IEnumerable<Control> Flat(Control root) { yield return root; foreach (Control child in root.Controls) foreach (Control nested in Flat(child)) yield return nested; }
    static void CapturePopup(MainForm form, ComboBox combo, string path)
    {
        combo.Focus(); combo.DroppedDown = true; Pump();
        if (!combo.DroppedDown) throw new InvalidOperationException("Native dropdown did not open.");
        // Render only our own form and its visible native list HWND. Reading the
        // desktop is unsafe when another application covers the fixture window.
        var info = new ComboInfo(); info.Size = Marshal.SizeOf(typeof(ComboInfo)); NativeRect rect;
        if (!GetComboBoxInfo(combo.Handle, ref info) || !IsWindowVisible(info.List) || !GetWindowRect(info.List, out rect))
            throw new InvalidOperationException("Native dropdown HWND is unavailable.");
        int width = rect.Right - rect.Left, height = rect.Bottom - rect.Top;
        var location = new Point(rect.Left - form.Left, rect.Top - form.Top);
        if (width <= 0 || height <= 0 || location.X < 0 || location.Y < 0 || location.X + width > form.Width || location.Y + height > form.Height)
            throw new InvalidOperationException("Native dropdown is outside the sanitized fixture canvas.");
        using (var bitmap = new Bitmap(form.Width, form.Height)) using (var popup = new Bitmap(width, height))
        {
            form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
            using (var graphics = Graphics.FromImage(popup))
            {
                IntPtr dc = graphics.GetHdc();
                try { if (!PrintWindow(info.List, dc, 0)) throw new InvalidOperationException("Native dropdown render failed."); }
                finally { graphics.ReleaseHdc(dc); }
            }
            bool varied = false; Color first = popup.GetPixel(0, 0);
            for (int y = 0; y < height && !varied; y++) for (int x = 0; x < width; x++) if (popup.GetPixel(x, y) != first) { varied = true; break; }
            if (!varied) throw new InvalidOperationException("Native dropdown render is blank.");
            using (var graphics = Graphics.FromImage(bitmap)) graphics.DrawImageUnscaled(popup, location);
            bitmap.Save(path);
        }
        combo.DroppedDown = false; Pump();
    }
}
