using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using NetStuck;

// Geometry is measured in the test host's real DPI. This is not evidence for
// unvisited Windows scaling settings, native popups or monitor transitions.
static class UiLayoutTests
{
    static int failed;
    static float baseGridFont;
    static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Check(string name, bool ok) { Console.WriteLine((ok ? "PASS " : "FAIL ") + name); if (!ok) failed++; }
    static object Get(MainForm form, string name) { return typeof(MainForm).GetField(name, Private).GetValue(form); }
    static object Call(MainForm form, string name, params object[] args) { return typeof(MainForm).GetMethod(name, Private).Invoke(form, args); }
    static void Set(MainForm form, string name, object value) { typeof(MainForm).GetField(name, Private).SetValue(form, value); }
    static IEnumerable<Control> Flat(Control root)
    { yield return root; foreach (Control child in root.Controls) foreach (Control nested in Flat(child)) yield return nested; }
    static void Pump() { for (int i = 0; i < 5; i++) { Application.DoEvents(); Thread.Sleep(10); } }
    static double Channel(byte value) { double v = value / 255.0; return v <= 0.04045 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4); }
    static double Light(Color c) { return 0.2126 * Channel(c.R) + 0.7152 * Channel(c.G) + 0.0722 * Channel(c.B); }
    static double Contrast(Color a, Color b) { double x = Light(a), y = Light(b); return (Math.Max(x, y) + 0.05) / (Math.Min(x, y) + 0.05); }
    static bool Inside(Control child, Control host)
    { return host.ClientRectangle.Contains(host.RectangleToClient(child.RectangleToScreen(child.ClientRectangle))); }

    [STAThread]
    static int Main()
    {
        string root = Path.Combine(Path.GetTempPath(), "NetStuck-ui-layout-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root); Environment.SetEnvironmentVariable("NETSTUCK_TEST_ROOT", root);
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        try
        {
            using (var form = new MainForm())
            {
                form.Show(); Pump();
                using (var g = form.CreateGraphics()) Console.WriteLine("EVIDENCE real test-host DPI=" + g.DpiX);
                var tabs = (TabControl)Get(form, "tabs");
                baseGridFont = ((DataGridView)Get(form, "pingGrid")).Font.Size;
                foreach (bool dark in new[] { false, true })
                {
                    Call(form, "SetApplicationTheme", dark); Pump();
                    foreach (var size in new[] { new Size(1460, 900), new Size(1100, 700) })
                    {
                        form.Size = size; Pump();
                        foreach (TabPage page in tabs.TabPages)
                        {
                            tabs.SelectedTab = page; Pump();
                            string context = (dark ? "Dark " : "Light ") + size.Width + " " + page.Text;
                            Check(context + " result grids retain usable geometry", Flat(page).OfType<DataGridView>().Where(g => g.Visible).All(g => g.Height >= 90 && g.Width >= 100));
                            var buttons = Flat(page).OfType<Button>().Where(b => b.Visible).ToArray();
                            var clipped = buttons.Where(b => TextRenderer.MeasureText(b.Text, b.Font).Width > b.ClientSize.Width || !b.Parent.ClientRectangle.Contains(b.Bounds)).ToArray();
                            if (clipped.Length > 0) Console.WriteLine("EVIDENCE clipped actions " + context + ": " + String.Join(", ", clipped.Select(b => b.Text + " " + b.Bounds + " parent=" + b.Parent.ClientSize)));
                            Check(context + " action labels and bounds fit", clipped.Length == 0);
                            if (page.Text == "Live Ping" || page.Text == "Config Collector")
                            {
                                var viewport = Flat(page).OfType<Panel>().Single(p => p.Name == "responsiveViewport");
                                string name = page.Text == "Live Ping" ? "pingOperationActions" : "collectorOperationActions";
                                var actions = Flat(page).Single(c => c.Name == name);
                                bool visible = Flat(actions).OfType<Button>().All(b => Inside(b, viewport));
                                if (!visible) Console.WriteLine("EVIDENCE action bar=" + viewport.RectangleToClient(actions.RectangleToScreen(actions.ClientRectangle)) + " viewport=" + viewport.ClientSize);
                                Check(context + " primary actions visible without input scrolling", visible);
                            }
                        }
                    }
                    tabs.SelectedIndex = 0; Pump();
                    var grid = (DataGridView)Get(form, "pingGrid");
                    Check((dark ? "Dark" : "Light") + " body and muted text contrast", Contrast(form.ForeColor, form.BackColor) >= 4.5 && Flat(form).OfType<Label>().Where(l => l.Visible && l.Text.Length > 0 && l.ForeColor.A == 255).All(l => Contrast(l.ForeColor, l.BackColor) >= 4.5));
                    Check((dark ? "Dark" : "Light") + " grid header and selection contrast", Contrast(grid.ColumnHeadersDefaultCellStyle.ForeColor, grid.ColumnHeadersDefaultCellStyle.BackColor) >= 4.5 && Contrast(grid.DefaultCellStyle.SelectionForeColor, grid.DefaultCellStyle.SelectionBackColor) >= 4.5);
                    var start = (Button)Get(form, "pingStartButton");
                    Check((dark ? "Dark" : "Light") + " enabled primary button contrast", Contrast(start.ForeColor, start.BackColor) >= 4.5);
                    using (var cancellation = new CancellationTokenSource())
                    {
                        Set(form, "pingCancellation", cancellation); Call(form, "SetPingRunning", true); Call(form, "TogglePingPause", null, EventArgs.Empty);
                        var resume = (Button)Get(form, "pingPauseButton"); var stop = (Button)Get(form, "pingStopButton");
                        Check((dark ? "Dark" : "Light") + " paused and stop actions remain readable", resume.Text == "RESUME" && Contrast(resume.ForeColor, resume.BackColor) >= 4.5 && Contrast(stop.ForeColor, stop.BackColor) >= 4.5);
                        Set(form, "pingCancellation", null); Call(form, "SetPingRunning", false);
                    }
                }
                Call(form, "SetApplicationTheme", false); Pump();
                Color lightBack = form.BackColor, lightText = form.ForeColor;
                for (int i = 0; i < 10; i++) { Call(form, "SetApplicationTheme", true); Call(form, "SetApplicationTheme", false); }
                Check("repeated theme changes restore light surface and text", form.BackColor == lightBack && form.ForeColor == lightText);
                var combo = (ComboBox)Get(form, "pingStatusFilter");
                combo.SelectedIndex = 2; combo.Font = new Font(combo.Font.FontFamily, 16f); Call(form, "UpdateComboMetrics", combo);
                Check("enlarged dropdown items fit font and preserve selection", combo.ItemHeight >= combo.Font.Height + 2 && combo.SelectedIndex == 2);
                combo.Items.Add("Long diagnostic status description for a readable popup"); Call(form, "UpdateComboMetrics", combo);
                Check("dropdown expands to fit long item within working area", combo.DropDownWidth >= TextRenderer.MeasureText(combo.Items[combo.Items.Count - 1].ToString(), combo.Font).Width && combo.DropDownWidth <= Screen.FromControl(combo).WorkingArea.Width);
                using (var header = (Control)Call(form, "SectionHeader", "Diagnostic settings", "ข้อความภาษาไทยสำหรับตรวจสอบการตัดข้อความ และคำอธิบายที่ยาว should wrap without covering the next control."))
                {
                    form.Controls.Add(header); header.Dock = DockStyle.None; header.Width = 280; Pump();
                    Check("narrow section header wraps long Thai and English subtitle", header.Controls.Cast<Control>().All(c => c.Right <= header.ClientSize.Width && c.Bottom <= header.ClientSize.Height) && header.Height > 55);
                    header.Width = 900; Pump();
                    Check("wide section header contracts after wrapping", header.Height < 100 && header.Controls.Cast<Control>().All(c => c.Right <= header.ClientSize.Width && c.Bottom <= header.ClientSize.Height));
                    form.Controls.Remove(header);
                }
                combo.SelectedIndex = 0;
                var table = (DataTable)Get(form, "pingTable");
                for (int i = 0; i < 20; i++) { var row = table.NewRow(); row["Seq"] = i + 1; row["Host"] = "fixture-" + i; table.Rows.Add(row); }
                var ping = (DataGridView)Get(form, "pingGrid"); tabs.SelectedIndex = 0; Pump();
                ping.ClearSelection(); ping.Rows[3].Selected = true;
                Set(form, "zoomScale", 1.8f); Call(form, "ApplyZoom"); Pump();
                Check("zoom resizes existing bound rows for readable text", ping.Rows.Count == 20 && ping.Rows.Cast<DataGridViewRow>().All(r => r.Height >= TextRenderer.MeasureText("Ag", ping.Font).Height + 8));
                var later = table.NewRow(); later["Seq"] = 21; later["Host"] = "fixture-later"; table.Rows.Add(later); Pump();
                Check("rows arriving after zoom use the new row height", ping.Rows[20].Height == ping.RowTemplate.Height);
                Check("zoom preserves selected result row", ping.Rows[3].Selected);
                Set(form, "zoomScale", 1f); Call(form, "ApplyZoom"); Pump();
                Check("zoom roundtrip restores default row geometry without drift", Math.Abs(ping.Font.Size - baseGridFont) < 0.01f && ping.Rows.Cast<DataGridViewRow>().All(r => r.Height == 32) && ping.ColumnHeadersHeight == 36);
                Set(form, "zoomScale", 1.6f); Call(form, "ApplyZoom"); Call(form, "SetApplicationTheme", true); Call(form, "SaveAppState"); form.Close();
            }
            using (var restored = new MainForm())
            {
                Check("saved zoom is applied once to restored controls", Math.Abs(((DataGridView)Get(restored, "pingGrid")).Font.Size - baseGridFont * 1.6f) < 0.02f);
                Check("saved dark theme restores without changing state schema", ((ComboBox)Get(restored, "themeSelector")).SelectedIndex == 1 && restored.BackColor.R < 70);
                Call(restored, "SetApplicationTheme", false);
            }
        }
        catch (Exception ex) { Check("unexpected UI layout exception " + ex, false); }
        finally
        {
            Environment.SetEnvironmentVariable("NETSTUCK_TEST_ROOT", null);
            try { Directory.Delete(root, true); if (Directory.Exists(root)) throw new IOException("Owned state remains"); }
            catch { Check("owned temporary state cleanup", false); }
        }
        Console.WriteLine("Failures: " + failed); return failed == 0 ? 0 : 1;
    }
}
