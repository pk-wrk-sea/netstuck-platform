using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using NetStuck;

static class UiFoundationSnapshot
{
    const string FixedClockText = "2000-01-02  03:04:05 ICT";
    const string FixedTimeSourceText = "Time: fixed capture fixture";
    static readonly BindingFlags InstanceMembers = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    sealed class Scenario
    {
        public string Name;
        public string TargetPage;
        public string ExpectedState;
        public string ForbiddenState;
        public Size Resolution;
        public string[] VisibleControls;
        public string FocusControl;
        public Action<MainForm> Prepare;
        public Action<MainForm> Assert;
    }

    [STAThread]
    static int Main(string[] args)
    {
        if (args.Length != 1)
        {
            Console.Error.WriteLine("CAPTURE FAIL arguments: expected one output directory argument.");
            return 2;
        }

        string output = Path.GetFullPath(args[0]);
        string fault = Environment.GetEnvironmentVariable("NETSTUCK_CAPTURE_FAULT") ?? "";
        string testRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "NetStuck-ui-capture-" + Guid.NewGuid().ToString("N")));
        string activeScenario = "startup";
        Exception primaryFailure = null;
        Exception cleanupFailure = null;
        var semanticPasses = new List<string>();

        try
        {
            Directory.CreateDirectory(output);
            if (Directory.GetFiles(output, "*.png", SearchOption.AllDirectories).Length != 0)
                throw new InvalidOperationException("Capture output must not contain prior PNG evidence.");

            Directory.CreateDirectory(testRoot);
            Environment.SetEnvironmentVariable("NETSTUCK_TEST_ROOT", testRoot);
            Environment.SetEnvironmentVariable("NETSTUCK_TEST_STATE_PATH", null);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            foreach (Scenario scenario in BuildScenarios(fault))
            {
                activeScenario = scenario.Name;
                semanticPasses.Add(CaptureScenario(output, scenario));
            }
            foreach (string semanticPass in semanticPasses) Console.WriteLine(semanticPass);
        }
        catch (Exception ex)
        {
            primaryFailure = Unwrap(ex);
            Console.Error.WriteLine("SEMANTIC FAIL " + activeScenario + " [" + primaryFailure.GetType().Name + "]: "
                + SafeMessage(primaryFailure.Message, output, testRoot));
        }
        finally
        {
            Environment.SetEnvironmentVariable("NETSTUCK_TEST_ROOT", null);
            Environment.SetEnvironmentVariable("NETSTUCK_TEST_STATE_PATH", null);
            try
            {
                DeleteOwnedTemporaryRoot(testRoot);
                if (String.Equals(fault, "cleanup", StringComparison.OrdinalIgnoreCase))
                    throw new IOException("Injected cleanup failure for verification.");
            }
            catch (Exception ex)
            {
                cleanupFailure = Unwrap(ex);
                Console.Error.WriteLine("CLEANUP FAIL capture-test-root [" + cleanupFailure.GetType().Name + "]: "
                    + SafeMessage(cleanupFailure.Message, output, testRoot));
            }
        }

        if (primaryFailure != null && cleanupFailure != null)
            Console.Error.WriteLine("CAPTURE FAIL combined: semantic generation and cleanup both failed.");
        return primaryFailure == null && cleanupFailure == null ? 0 : 1;
    }

    static IEnumerable<Scenario> BuildScenarios(string fault)
    {
        yield return ShellScenario("main-shell-1100x900", new Size(1100, 900));
        yield return ShellScenario("main-shell-1460x900", new Size(1460, 900));
        yield return CalculatorSuccessScenario("pilot-calculators-1100x700", new Size(1100, 700), false, fault);
        yield return CalculatorSuccessScenario("pilot-calculators-1100x900", new Size(1100, 900), true, fault);
        yield return CalculatorSuccessScenario("pilot-calculators-1460x900", new Size(1460, 900), true, fault);
        yield return CalculatorValidationScenario();
        yield return EventLogEmptyScenario("pilot-event-log-empty-1100x700", new Size(1100, 700));
        yield return EventLogEmptyScenario("pilot-event-log-empty-1100x900", new Size(1100, 900));
        yield return EventLogFilteredEmptyScenario();
    }

    static Scenario ShellScenario(string name, Size size)
    {
        return new Scenario
        {
            Name = name,
            TargetPage = "Updates",
            ExpectedState = "ShellReady",
            ForbiddenState = "DynamicClock",
            Resolution = size,
            VisibleControls = new[] { "tabs", "clockStatus", "localIpStatus", "publicIpStatus" },
            FocusControl = null,
            Prepare = delegate(MainForm form) { },
            Assert = delegate(MainForm form)
            {
                Ensure(((ToolStripStatusLabel)Get(form, "clockStatus")).Text == FixedClockText, "fixed application clock was not applied");
                Ensure(((ToolStripStatusLabel)Get(form, "timeSourceStatus")).Text == FixedTimeSourceText, "fixed time source was not applied");
                Ensure(((ToolStripStatusLabel)Get(form, "localIpStatus")).Text == "My Local IP: 192.0.2.10", "local identity is not sanitized");
                Ensure(((ToolStripStatusLabel)Get(form, "publicIpStatus")).Text == "My Public IP: 203.0.113.10", "public identity is not sanitized");
            }
        };
    }

    static Scenario CalculatorSuccessScenario(string name, Size size, bool focusAction, string fault)
    {
        return new Scenario
        {
            Name = name,
            TargetPage = "Calculators",
            ExpectedState = "Success",
            ForbiddenState = "Idle",
            Resolution = size,
            VisibleControls = new[] { "subnetInput", "subnetCalculateButton", "subnetStatePresenter", "subnetOutput", "unitValue", "unitConvertButton", "unitStatePresenter", "unitOutput" },
            FocusControl = focusAction ? "subnetCalculateButton" : null,
            Prepare = delegate(MainForm form)
            {
                TextBox subnetInput = (TextBox)Get(form, "subnetInput");
                TextBox unitValue = (TextBox)Get(form, "unitValue");
                Button calculate = (Button)Get(form, "subnetCalculateButton");
                Button convert = (Button)Get(form, "unitConvertButton");
                Ensure(StateName(Get(form, "subnetStatePresenter")) == "Idle", "subnet pre-state is not Idle");
                Ensure(StateName(Get(form, "unitStatePresenter")) == "Idle", "unit pre-state is not Idle");
                Ensure(String.IsNullOrEmpty(((TextBox)Get(form, "subnetOutput")).Text), "subnet pre-result is not empty");
                Ensure(((Label)Get(form, "unitOutput")).Text == "Result: —", "unit pre-result is not the expected placeholder");
                Ensure(calculate.Visible && calculate.Enabled, "Calculate action is not visible and enabled");
                Ensure(convert.Visible && convert.Enabled, "Convert action is not visible and enabled");
                subnetInput.Text = "192.0.2.10/24";
                unitValue.Text = "1000";
                Ensure(subnetInput.Text == "192.0.2.10/24" && unitValue.Text == "1000", "calculator inputs were not populated");
                Pump(50);
                if (!String.Equals(fault, "calculator-idle", StringComparison.OrdinalIgnoreCase))
                {
                    calculate.PerformClick();
                    convert.PerformClick();
                }
                Pump(80);
                if (focusAction) calculate.Focus();
            },
            Assert = AssertCalculatorSuccess
        };
    }

    static void AssertCalculatorSuccess(MainForm form)
    {
        string subnetState = StateName(Get(form, "subnetStatePresenter"));
        string unitState = StateName(Get(form, "unitStatePresenter"));
        string subnetOutput = ((TextBox)Get(form, "subnetOutput")).Text;
        string unitOutput = ((Label)Get(form, "unitOutput")).Text;
        Ensure(subnetState == "Success", "subnet state is " + subnetState + " instead of Success");
        Ensure(unitState == "Success", "unit state is " + unitState + " instead of Success");
        Ensure(subnetState != "Idle" && unitState != "Idle", "Calculator success evidence remained Idle");
        Ensure(subnetOutput.Contains("Address          192.0.2.10"), "subnet address result is missing");
        Ensure(subnetOutput.Contains("Network          192.0.2.0"), "subnet network result is missing");
        Ensure(subnetOutput.Contains("Broadcast        192.0.2.255"), "subnet broadcast result is missing");
        Ensure(subnetOutput.IndexOf("Invalid input", StringComparison.OrdinalIgnoreCase) < 0, "valid subnet scenario contains a validation error");
        Ensure(unitOutput == "Result: 1 Gbit", "unit result is not the expected calculated value");
        Ensure(unitOutput.IndexOf("invalid", StringComparison.OrdinalIgnoreCase) < 0, "valid unit scenario contains a validation error");
    }

    static Scenario CalculatorValidationScenario()
    {
        return new Scenario
        {
            Name = "pilot-calculators-validation-1100x900",
            TargetPage = "Calculators",
            ExpectedState = "ValidationFailure",
            ForbiddenState = "Success",
            Resolution = new Size(1100, 900),
            VisibleControls = new[] { "subnetInput", "subnetCalculateButton", "subnetStatePresenter", "subnetOutput" },
            FocusControl = "subnetInput",
            Prepare = delegate(MainForm form)
            {
                Ensure(StateName(Get(form, "subnetStatePresenter")) == "Idle", "validation pre-state is not Idle");
                TextBox input = (TextBox)Get(form, "subnetInput");
                input.Text = "invalid-example";
                ((Button)Get(form, "subnetCalculateButton")).PerformClick();
                Pump(80);
            },
            Assert = delegate(MainForm form)
            {
                Ensure(StateName(Get(form, "subnetStatePresenter")) == "ValidationFailure", "invalid input did not reach ValidationFailure");
                Ensure(((TextBox)Get(form, "subnetOutput")).Text.Contains("Invalid input"), "validation result text is missing");
                Ensure(((TextBox)Get(form, "subnetInput")).Focused, "invalid subnet input did not retain focus");
            }
        };
    }

    static Scenario EventLogEmptyScenario(string name, Size size)
    {
        return new Scenario
        {
            Name = name,
            TargetPage = "Event Log",
            ExpectedState = "Empty",
            ForbiddenState = "FilteredEmpty",
            Resolution = size,
            VisibleControls = new[] { "logSearch", "logLevel", "logStatePresenter", "logGrid" },
            FocusControl = null,
            Prepare = delegate(MainForm form)
            {
                DataTable table = (DataTable)Get(form, "logTable");
                table.Rows.Clear();
                Invoke(form, "UpdateLogStatePresenter");
                Pump(50);
            },
            Assert = delegate(MainForm form)
            {
                Ensure(StateName(Get(form, "logStatePresenter")) == "Empty", "Event Log did not reach Empty");
                Ensure(((Control)Get(form, "logStatePresenter")).Visible, "Event Log empty presenter is hidden");
                Ensure(((BindingSource)Get(form, "logSource")).Count == 0, "Event Log empty scenario still has visible rows");
            }
        };
    }

    static Scenario EventLogFilteredEmptyScenario()
    {
        return new Scenario
        {
            Name = "pilot-event-log-filtered-empty-1460x900",
            TargetPage = "Event Log",
            ExpectedState = "FilteredEmpty",
            ForbiddenState = "Empty",
            Resolution = new Size(1460, 900),
            VisibleControls = new[] { "logSearch", "logLevel", "logStatePresenter", "logGrid" },
            FocusControl = null,
            Prepare = delegate(MainForm form)
            {
                DataTable table = (DataTable)Get(form, "logTable");
                table.Rows.Clear();
                table.Rows.Add(new DateTime(2000, 1, 2, 12, 0, 0), "INFO", "Synthetic fixture", "Safe local UI-foundation event");
                ((TextBox)Get(form, "logSearch")).Text = "no-matching-event";
                Pump(80);
            },
            Assert = delegate(MainForm form)
            {
                Ensure(StateName(Get(form, "logStatePresenter")) == "FilteredEmpty", "Event Log did not reach FilteredEmpty");
                Ensure(((Control)Get(form, "logStatePresenter")).Visible, "Event Log filtered-empty presenter is hidden");
                Ensure(((DataTable)Get(form, "logTable")).Rows.Count == 1, "filtered fixture row is missing");
                Ensure(((BindingSource)Get(form, "logSource")).Count == 0, "filtered fixture still has a visible row");
            }
        };
    }

    static string CaptureScenario(string output, Scenario scenario)
    {
        string semanticPass;
        using (var form = new MainForm())
        {
            form.StartPosition = FormStartPosition.Manual;
            form.Location = Point.Empty;
            form.Show();
            Pump(160);
            Sanitize(form);
            FreezeDynamicValues(form);
            SelectPage(form, scenario.TargetPage);
            form.Size = scenario.Resolution;
            Pump(140);
            AssertPageActive(form, scenario.TargetPage);
            scenario.Prepare(form);
            Pump(50);
            AssertPageActive(form, scenario.TargetPage);
            AssertVisibleControls(form, scenario.VisibleControls);
            scenario.Assert(form);
            EstablishDeterministicViewport(form, scenario);
            WaitForStableViewport(form, scenario, 2500);

            using (var warmup = new Bitmap(scenario.Resolution.Width, scenario.Resolution.Height))
                form.DrawToBitmap(warmup, new Rectangle(Point.Empty, warmup.Size));
            EstablishDeterministicViewport(form, scenario);
            WaitForStableViewport(form, scenario, 2500);
            AssertPageActive(form, scenario.TargetPage);
            AssertVisibleControls(form, scenario.VisibleControls);
            scenario.Assert(form);

            string path = Path.Combine(output, scenario.Name + ".png");
            using (var bitmap = new Bitmap(scenario.Resolution.Width, scenario.Resolution.Height))
            {
                form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                bitmap.Save(path, ImageFormat.Png);
            }
            Ensure(File.Exists(path), "scenario PNG was not written");
            semanticPass = "SEMANTIC PASS " + scenario.Name + " surface=" + scenario.TargetPage
                + " state=" + scenario.ExpectedState + " forbidden=" + scenario.ForbiddenState
                + " size=" + scenario.Resolution.Width + "x" + scenario.Resolution.Height;
            form.Close();
        }
        return semanticPass;
    }

    static void EstablishDeterministicViewport(MainForm form, Scenario scenario)
    {
        form.Size = scenario.Resolution;
        form.PerformLayout();
        foreach (Control control in Flat(form))
        {
            control.PerformLayout();
            ScrollableControl scrollable = control as ScrollableControl;
            if (scrollable != null && scrollable.AutoScroll) scrollable.AutoScrollPosition = Point.Empty;

            TextBox text = control as TextBox;
            if (text != null && text.Multiline)
            {
                text.Select(0, 0);
                text.ScrollToCaret();
            }

            DataGridView grid = control as DataGridView;
            if (grid != null && grid.Visible)
            {
                grid.ClearSelection();
                grid.CurrentCell = null;
                if (grid.Rows.Count > 0) grid.FirstDisplayedScrollingRowIndex = 0;
                grid.HorizontalScrollingOffset = 0;
            }
        }

        if (String.IsNullOrEmpty(scenario.FocusControl))
        {
            form.ActiveControl = null;
        }
        else
        {
            Control focus = Get(form, scenario.FocusControl) as Control;
            Ensure(focus != null && focus.CanFocus && focus.Focus(), "expected capture focus could not be established: " + scenario.FocusControl);
        }
        form.Update();
    }

    static void WaitForStableViewport(MainForm form, Scenario scenario, int timeoutMs)
    {
        string previous = null;
        int stablePasses = 0;
        Stopwatch watch = Stopwatch.StartNew();
        while (watch.ElapsedMilliseconds < timeoutMs)
        {
            bool postedMessageObserved = false;
            form.BeginInvoke(new Action(delegate { postedMessageObserved = true; }));
            while (!postedMessageObserved && watch.ElapsedMilliseconds < timeoutMs) Application.DoEvents();
            EstablishDeterministicViewport(form, scenario);
            Application.DoEvents();
            string current = GetViewportSignature(form);
            if (String.Equals(previous, current, StringComparison.Ordinal)) stablePasses++;
            else { previous = current; stablePasses = 1; }
            if (stablePasses >= 4) return;
            Thread.Sleep(10);
        }
        throw new InvalidOperationException("capture viewport did not reach a stable observable state: " + scenario.Name);
    }

    static string GetViewportSignature(Control root)
    {
        var signature = new StringBuilder();
        foreach (Control control in new[] { root }.Concat(Flat(root)))
        {
            Rectangle bounds = control.Bounds;
            signature.Append(control.GetType().FullName).Append('|').Append(control.Name).Append('|')
                .Append(bounds.X).Append(',').Append(bounds.Y).Append(',').Append(bounds.Width).Append(',').Append(bounds.Height).Append('|')
                .Append(control.ClientSize.Width).Append(',').Append(control.ClientSize.Height).Append('|')
                .Append(control.Visible ? '1' : '0').Append(control.Enabled ? '1' : '0').Append(control.Focused ? '1' : '0');
            ScrollableControl scrollable = control as ScrollableControl;
            if (scrollable != null)
                signature.Append("|A:").Append(scrollable.AutoScrollPosition.X).Append(',').Append(scrollable.AutoScrollPosition.Y);
            TextBox text = control as TextBox;
            if (text != null)
                signature.Append("|T:").Append((int)text.ScrollBars).Append(',').Append(text.SelectionStart).Append(',').Append(text.SelectionLength);
            DataGridView grid = control as DataGridView;
            if (grid != null)
            {
                int first = grid.Visible && grid.Rows.Count > 0 ? grid.FirstDisplayedScrollingRowIndex : -1;
                int horizontalOffset = grid.Visible ? grid.HorizontalScrollingOffset : -1;
                signature.Append("|G:").Append(first).Append(',').Append(horizontalOffset).Append(',').Append(grid.Rows.Count)
                    .Append(',').Append(grid.CurrentCell == null ? -1 : grid.CurrentCell.RowIndex);
            }
            ScrollBar bar = control as ScrollBar;
            if (bar != null) signature.Append("|S:").Append(bar.Value).Append(',').Append(bar.Minimum).Append(',').Append(bar.Maximum).Append(',').Append(bar.LargeChange);
            signature.AppendLine();
        }
        return signature.ToString();
    }

    static void SelectPage(MainForm form, string pageName)
    {
        TabControl tabs = (TabControl)Get(form, "tabs");
        TabPage page = tabs.TabPages.Cast<TabPage>().FirstOrDefault(candidate => candidate.Text == pageName);
        Ensure(page != null, "target page does not exist: " + pageName);
        tabs.SelectedTab = page;
        Pump(100);
    }

    static void AssertPageActive(MainForm form, string pageName)
    {
        TabControl tabs = (TabControl)Get(form, "tabs");
        Ensure(tabs.SelectedTab != null && tabs.SelectedTab.Text == pageName, "target page is not selected: " + pageName);
        Ensure(tabs.SelectedTab.Visible, "target page is not visible: " + pageName);
    }

    static void AssertVisibleControls(MainForm form, IEnumerable<string> names)
    {
        foreach (string name in names)
        {
            object value = Get(form, name);
            Control control = value as Control;
            if (control != null)
            {
                Ensure(control.Visible && control.Width > 0 && control.Height > 0, "required control is not visible: " + name);
                continue;
            }
            ToolStripItem item = value as ToolStripItem;
            Ensure(item != null && item.Available, "required status item is not available: " + name);
        }
    }

    static void Sanitize(MainForm form)
    {
        ((TextBox)Get(form, "targetInput")).Text = "192.0.2.10 Synthetic target";
        ((TextBox)Get(form, "dnsInput")).Text = "example.test Synthetic DNS fixture";
        ((TextBox)Get(form, "macInput")).Text = "02:00:00:00:00:01";
        ((TextBox)Get(form, "wanInput")).Text = "203.0.113.10";
        ((TextBox)Get(form, "collectorAuth1User")).Text = "synthetic-user";
        ((TextBox)Get(form, "collectorAuth2User")).Text = "";
        ((TextBox)Get(form, "collectorAuth1Pass")).Text = "";
        ((TextBox)Get(form, "collectorAuth2Pass")).Text = "";
        ((TextBox)Get(form, "collectorAuth1Secret")).Text = "";
        ((TextBox)Get(form, "collectorAuth2Secret")).Text = "";
        ((TextBox)Get(form, "collectorDevices")).Text = "192.0.2.20 Synthetic-Device";
        ((TextBox)Get(form, "collectorFolderBox")).Text = @"C:\NetStuck-Synthetic\Configs";
        ((ToolStripStatusLabel)Get(form, "localIpStatus")).Text = "My Local IP: 192.0.2.10";
        ((ToolStripStatusLabel)Get(form, "publicIpStatus")).Text = "My Public IP: 203.0.113.10";
    }

    static void FreezeDynamicValues(MainForm form)
    {
        var timer = (System.Windows.Forms.Timer)Get(form, "clockTimer");
        timer.Stop();
        timer.Dispose();
        ((ToolStripStatusLabel)Get(form, "timeSourceStatus")).Text = FixedTimeSourceText;
        ((ToolStripStatusLabel)Get(form, "clockStatus")).Text = FixedClockText;
    }

    static string StateName(object presenter)
    {
        return Convert.ToString(Property(presenter, "State"));
    }

    static void Ensure(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    static object Get(object target, string name)
    {
        FieldInfo field = target.GetType().GetField(name, InstanceMembers);
        if (field == null) throw new MissingFieldException(name);
        return field.GetValue(target);
    }

    static object Property(object target, string name)
    {
        PropertyInfo property = target.GetType().GetProperty(name, InstanceMembers);
        if (property == null) throw new MissingMemberException(name);
        return property.GetValue(target, null);
    }

    static object Invoke(object target, string name, params object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(name, InstanceMembers);
        if (method == null) throw new MissingMethodException(name);
        return method.Invoke(target, args);
    }

    static Exception Unwrap(Exception ex)
    {
        var invocation = ex as TargetInvocationException;
        return invocation != null && invocation.InnerException != null ? invocation.InnerException : ex;
    }

    static string SafeMessage(string message, string output, string testRoot)
    {
        string safe = message ?? "";
        if (!String.IsNullOrEmpty(output)) safe = safe.Replace(output, "<OUTPUT>");
        if (!String.IsNullOrEmpty(testRoot)) safe = safe.Replace(testRoot, "<TEST_ROOT>");
        safe = safe.Replace(Path.GetTempPath(), "<TEMP>" + Path.DirectorySeparatorChar);
        return safe.Replace('\r', ' ').Replace('\n', ' ').Trim();
    }

    static void DeleteOwnedTemporaryRoot(string path)
    {
        string full = Path.GetFullPath(path);
        string temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!full.StartsWith(temp, StringComparison.OrdinalIgnoreCase)
            || !Path.GetFileName(full).StartsWith("NetStuck-ui-capture-", StringComparison.Ordinal))
            throw new InvalidOperationException("Refusing to clean a directory that is not an owned capture root.");
        if (Directory.Exists(full)) Directory.Delete(full, true);
        if (Directory.Exists(full)) throw new IOException("Owned capture root still exists after cleanup.");
    }

    static IEnumerable<Control> Flat(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (Control nested in Flat(child)) yield return nested;
        }
    }

    static void Pump(int milliseconds)
    {
        Stopwatch watch = Stopwatch.StartNew();
        while (watch.ElapsedMilliseconds < milliseconds)
        {
            Application.DoEvents();
            Thread.Sleep(10);
        }
    }
}
