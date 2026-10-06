using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using NetStuck;

static class UiV2Tests
{
    static int failed;
    static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static object Get(MainForm form, string name) { return typeof(MainForm).GetField(name, Private).GetValue(form); }
    static object Call(MainForm form, string name, params object[] args) { return typeof(MainForm).GetMethod(name, Private).Invoke(form, args); }
    static IEnumerable<Control> Flat(Control root) { yield return root; foreach (Control child in root.Controls) foreach (Control nested in Flat(child)) yield return nested; }
    static void Pump() { for (int i = 0; i < 3; i++) { Application.DoEvents(); Thread.Sleep(10); } }
    static void Check(string name, bool ok) { Console.WriteLine((ok ? "PASS " : "FAIL ") + name); if (!ok) failed++; }
    static bool Inside(Control child, Control host) { return host.ClientRectangle.Contains(host.RectangleToClient(child.RectangleToScreen(child.ClientRectangle))); }
    [STAThread]
    static int Main()
    {
        string root = Path.Combine(Path.GetTempPath(), "NetStuck-v2-ui-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        Environment.SetEnvironmentVariable("NETSTUCK_TEST_ROOT", root); Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        try
        {
            var form = new MainForm();
            using (form)
            {
                form.Show(); Pump();
                var tabs = (TabControl)Get(form, "tabs");
                var nav = (IDictionary)Get(form, "navigationV2");
                string[] original = { "Live Ping", "Traceroute", "DNS Resolver", "MAC / WAN Lookup", "Calculators", "Config Collector", "Event Log", "Updates" };
                Check("V2 preserves all eight original page identities and state indices", original.Select((name, i) => tabs.TabPages[i].Text == name).All(value => value));
                Check("Home and Settings are reachable additional destinations", tabs.TabPages[8].Text == "Home" && tabs.TabPages[9].Text == "Settings");
                Check("sidebar navigation IDs are unique and cover every page", nav.Count == tabs.TabCount && nav.Count == 10);
                Size expectedMinimum;
                using (var graphics = form.CreateGraphics())
                {
                    Rectangle work = Screen.FromControl(form).WorkingArea;
                    expectedMinimum = new Size(Math.Min((int)Math.Round(1100 * graphics.DpiX / 96.0), work.Width),
                        Math.Min((int)Math.Round(700 * graphics.DpiY / 96.0), work.Height));
                }
                Check("production keeps DPI autoscaling and caps its scaled nominal minimum to the actual working area", form.AutoScaleMode == AutoScaleMode.Dpi && form.MinimumSize == expectedMinimum);
                Check("V2 assembly version is 2.0.0.0", typeof(MainForm).Assembly.GetName().Version == new Version(2, 0, 0, 0));
                foreach (DictionaryEntry entry in nav)
                {
                    var button = (Button)entry.Value; button.PerformClick(); Pump();
                    Check("sidebar action reaches " + entry.Key, tabs.SelectedTab.Text == (string)entry.Key && button.AccessibleDescription.Contains("Selected"));
                    Check("navigation has native keyboard role and named focus target " + entry.Key, button.TabStop && button.AccessibilityObject.Role == AccessibleRole.PushButton && !String.IsNullOrWhiteSpace(button.AccessibleName));
                }
                Check("sidebar tab order has no duplicate indices", nav.Values.Cast<Button>().Select(b => b.TabIndex).Distinct().Count() == nav.Count);
                using (var graphics = form.CreateGraphics()) Console.WriteLine("EVIDENCE V2 layout host DPI=" + graphics.DpiX + "; working area=" + Screen.FromControl(form).WorkingArea.Size);
                foreach (bool dark in new[] { false, true })
                {
                    Call(form, "SetApplicationTheme", dark);
                    foreach (var size in new[] { new Size(1100, 700), new Size(1024, 768), new Size(1280, 720), new Size(1366, 768), new Size(1440, 900), new Size(1920, 1080), new Size(2560, 1440) })
                    {
                        form.Size = size; Pump();
                        string dimensions = size.Width + "x" + size.Height + " observed=" + form.Width + "x" + form.Height;
                        foreach (TabPage page in tabs.TabPages)
                        {
                            tabs.SelectedTab = page; Pump(); string context = (dark ? "Dark " : "Light ") + dimensions + " " + page.Text;
                            var controls = Flat(page).ToArray();
                            var buttons = controls.OfType<Button>().Where(b => b.Visible).ToArray();
                            var clipped = buttons.Where(b => b.Width <= 0 || b.Height <= 0 || !b.Parent.ClientRectangle.Contains(b.Bounds) || TextRenderer.MeasureText(b.Text, b.Font).Width > b.ClientSize.Width).ToArray();
                            if (clipped.Length > 0) Console.WriteLine("EVIDENCE " + context + " clipped=" + String.Join(", ", clipped.Select(b => b.Text + " " + b.Bounds)));
                            Check(context + " action geometry and text remain usable", clipped.Length == 0);
                            Check(context + " grids retain usable geometry and columns", controls.OfType<DataGridView>().Where(g => g.Visible).All(g => g.Width >= 100 && g.Height >= 90 && g.Columns.Cast<DataGridViewColumn>().Any(c => c.Visible)));
                            Check(context + " interactive controls have accessible names", controls.Where(c => c is Button || c is TextBox || c is ComboBox || c is NumericUpDown || c is CheckBox || c is DataGridView || c is RichTextBox).All(c => !String.IsNullOrWhiteSpace(c.AccessibleName)));
                        }
                    }
                }
                form.Size = new Size(1100, 700); Call(form, "NavigateV2", "Live Ping"); Pump();
                var more = Flat(tabs.SelectedTab).OfType<Button>().Single(b => b.Name == "pingResultsMore");
                Check("minimum-width result toolbar exposes secondary actions through More", more.Visible && Flat(tabs.SelectedTab).OfType<Button>().Where(b => b.Text == "Copy" || b.Text == "Export CSV").All(b => b.Visible));
                var viewport = Flat(tabs.SelectedTab).OfType<Panel>().Single(p => p.Name == "responsiveViewport");
                Check("Ping Start Pause Stop stay visible outside the input scroller", new[] { "pingStartButton", "pingPauseButton", "pingStopButton" }.All(name => Inside((Control)Get(form, name), viewport)));
                var inputViewport = Flat(tabs.SelectedTab).OfType<Panel>().Single(p => p.Name == "inputViewport");
                inputViewport.ScrollControlIntoView((Control)Get(form, "pingDnsServer")); Pump();
                Check("minimum-height Ping settings are scroll reachable", Inside((Control)Get(form, "pingDnsServer"), inputViewport));
                Call(form, "NavigateV2", "Config Collector"); Pump();
                viewport = Flat(tabs.SelectedTab).OfType<Panel>().Single(p => p.Name == "responsiveViewport");
                Check("Collector Collect and Stop stay visible outside the input scroller", Inside((Control)Get(form, "collectorStart"), viewport) && Inside((Control)Get(form, "collectorCancel"), viewport));
                Call(form, "NavigateV2", "Live Ping"); form.Size = new Size(1440, 900); Pump();
                var table = (DataTable)Get(form, "pingTable");
                for (int i = 0; i < 3; i++) { var row = table.NewRow(); row["Seq"] = i + 1; row["Host"] = "fixture-" + i; row["Status"] = "ICMP OK"; table.Rows.Add(row); }
                var grid = (DataGridView)Get(form, "pingGrid"); Pump(); grid.ClearSelection(); grid.Rows[1].Selected = true; grid.Columns["Host"].Width = 190;
                for (int i = 0; i < 4; i++) { Call(form, "SetApplicationTheme", false); Call(form, "SetApplicationTheme", true); }
                Check("theme changes preserve result selection binding and user column width", grid.Rows[1].Selected && grid.Rows.Count == 3 && grid.Columns["Host"].Width == 190);
                ((TextBox)Get(form, "pingSearch")).Text = "fixture-1"; Pump(); Check("V2 results search retains the existing filter behavior", ((BindingSource)Get(form, "pingSource")).Count == 1);
                ((TextBox)Get(form, "pingSearch")).Clear(); Pump();
                var history = (DataTable)Get(form, "pingHistoryDisplayTable");
                var historyGrid = (DataGridView)Get(form, "pingHistoryGrid");
                var detail = (TextBox)Get(form, "pingHistoryDetailV2");
                history.Rows.Add(new DateTime(2000, 1, 2), "192.0.2.1", "192.0.2.1", 8L, 64, "Succeeded", 1L, "Selected synthetic event");
                Pump(); historyGrid.CurrentCell = historyGrid.Rows[0].Cells[0]; Pump();
                Check("Ping detail follows the selected history event", detail.Text == "Selected synthetic event");
                history.Clear(); Pump();
                Check("clearing Ping history removes stale event detail", detail.Text == "Select a history row to view its detail.");
                Call(form, "NavigateV2", "Settings"); Pump();
                var zoom = (NumericUpDown)Get(form, "settingsZoomV2"); zoom.Value = 120; Pump();
                Check("Settings zoom updates the existing result font behavior", Math.Abs((float)Get(form, "zoomScale") - 1.2f) < 0.01f);
                zoom.Value = 100;
                typeof(Form).GetMethod("OnKeyDown", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(form, new object[] { new KeyEventArgs(Keys.Control | Keys.Shift | Keys.U) }); Pump();
                Check("version recovery shortcut opens Updates directly", tabs.SelectedTab.Text == "Updates");
                Check("downgrade requires an explicit version selection", !((Button)Get(form, "downgradeButton")).Enabled);
                Check("version recovery selector is constrained to listed choices", ((ComboBox)Get(form, "previousVersionCombo")).DropDownStyle == ComboBoxStyle.DropDownList);
                form.Close();
            }
            Check("V2 disposal releases presentation resources and themed controls", form.IsDisposed && (bool)Get(form, "presentationDisposedV2") && ((IDictionary)Get(form, "themeControls")).Count == 0);
        }
        catch (Exception ex) { Check("unexpected V2 UI exception " + ex, false); }
        finally { Environment.SetEnvironmentVariable("NETSTUCK_TEST_ROOT", null); try { Directory.Delete(root, true); Check("V2 UI temporary state removed", !Directory.Exists(root)); } catch { Check("V2 UI cleanup", false); } }
        Console.WriteLine("Failures: " + failed); return failed == 0 ? 0 : 1;
    }
}
