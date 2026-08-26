using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using NetStuck;

static class PerformanceTests
{
    static int failures;

    static void Check(string name, bool condition, string detail)
    {
        Console.WriteLine((condition ? "PASS " : "FAIL ") + name + " — " + detail);
        if (!condition) failures++;
    }

    [STAThread]
    static int Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        string state = Path.Combine(Path.GetTempPath(), "NetStuck-performance-test.json");
        if (File.Exists(state)) File.Delete(state);
        Environment.SetEnvironmentVariable("NETSTUCK_TEST_STATE_PATH", state);
        var startup = Stopwatch.StartNew();
        using (var form = new MainForm())
        {
            form.Width = 1460;
            form.Height = 900;
            form.Show();
            long ignoredDispatch;
            Pump(400, out ignoredDispatch);
            startup.Stop();
            Check("warm UI startup", startup.ElapsedMilliseconds < 2500, startup.ElapsedMilliseconds + " ms");

            var grids = Flat(form).OfType<DataGridView>().ToList();
            bool allBuffered = grids.All(IsDoubleBuffered);
            Check("all result grids are double buffered", allBuffered, grids.Count + " grids");

            var tabs = Flat(form).OfType<TabControl>().First(t => t.TabPages.Cast<TabPage>().Any(p => p.Text == "Live Ping"));
            tabs.SelectedTab = tabs.TabPages.Cast<TabPage>().First(p => p.Text == "Live Ping");
            Pump(100, out ignoredDispatch);
            ((TextBox)Get(form, "targetInput")).Text = "127.0.0.0/24 Loopback stress";
            ((NumericUpDown)Get(form, "pingInterval")).Value = 250;
            ((NumericUpDown)Get(form, "pingTimeout")).Value = 250;
            ((Button)Get(form, "pingStartButton")).PerformClick();

            long maxDispatchMs;
            Pump(2400, out maxDispatchMs);
            var table = (DataTable)Get(form, "pingTable");
            long sent = table.Rows.Cast<DataRow>().Sum(row => Convert.ToInt64(row["Sent"]));
            Check("/24 creates the full host set", table.Rows.Count >= 250, table.Rows.Count + " rows");
            Check("/24 monitoring produces realtime samples", sent >= 250, sent + " probes");
            Check("UI event dispatch remains responsive under /24 load", maxDispatchMs < 350, "worst dispatch " + maxDispatchMs + " ms");

            ((Button)Get(form, "pingStopButton")).PerformClick();
            Pump(900, out ignoredDispatch);
            object queue = Get(form, "pingUiUpdates");
            DateTime drainDeadline = DateTime.UtcNow.AddSeconds(5);
            while ((Get(form, "pingCancellation") != null || Convert.ToInt32(queue.GetType().GetProperty("Count").GetValue(queue, null)) > 0)
                && DateTime.UtcNow < drainDeadline)
                Pump(50, out ignoredDispatch);
            int queued = Convert.ToInt32(queue.GetType().GetProperty("Count").GetValue(queue, null));
            Check("batched UI queue drains cleanly", queued == 0 && Get(form, "pingCancellation") == null, queued + " pending");

            tabs.SelectedTab = tabs.TabPages.Cast<TabPage>().First(p => p.Text == "Traceroute");
            Pump(100, out ignoredDispatch);
            var traceSessions = ((System.Collections.IEnumerable)Get(form, "traceSessionsV103")).Cast<object>().Take(2).ToList();
            var traceSessionTabs = (TabControl)Get(form, "traceSessionTabs");
            foreach (object session in traceSessions)
            {
                traceSessionTabs.SelectedTab = (TabPage)Field(session, "Page");
                Pump(60, out ignoredDispatch);
                ((ComboBox)Field(session, "Target")).Text = "127.0.0.1";
                ((NumericUpDown)Field(session, "Interval")).Value = 250;
                ((NumericUpDown)Field(session, "Timeout")).Value = 250;
                ((Button)Field(session, "Start")).PerformClick();
            }
            long traceDispatchMs;
            Pump(1800, out traceDispatchMs);
            int traceRows = traceSessions.Sum(s => ((DataTable)Field(s, "Table")).Rows.Count);
            int traceEvents = traceSessions.Sum(s => ((DataTable)Field(s, "EventTable")).Rows.Count);
            Check("two traceroute sessions update concurrently", traceRows == 2, traceRows + " destination rows");
            Check("dual traceroute UI remains responsive", traceDispatchMs < 350, "worst dispatch " + traceDispatchMs + " ms");
            Check("traceroute event history remains bounded", traceEvents > 0 && traceEvents <= 2000, traceEvents + " events");
            foreach (object session in traceSessions) ((Button)Field(session, "Stop")).PerformClick();
            Pump(600, out ignoredDispatch);

            long workingSetMb = Process.GetCurrentProcess().WorkingSet64 / 1024 / 1024;
            Check("working set remains bounded", workingSetMb < 350, workingSetMb + " MB");
            form.Close();
        }

        try { if (File.Exists(state)) File.Delete(state); } catch { }
        Environment.SetEnvironmentVariable("NETSTUCK_TEST_STATE_PATH", null);

        Console.WriteLine("Failures: " + failures);
        return failures == 0 ? 0 : 1;
    }

    static bool IsDoubleBuffered(DataGridView grid)
    {
        PropertyInfo property = typeof(DataGridView).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);
        return property != null && Convert.ToBoolean(property.GetValue(grid, null));
    }

    static object Get(object target, string name)
    {
        FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null) throw new MissingFieldException(name);
        return field.GetValue(target);
    }

    static object Field(object target, string name)
    {
        FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (field == null) throw new MissingFieldException(name);
        return field.GetValue(target);
    }

    static IEnumerable<Control> Flat(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (Control nested in Flat(child)) yield return nested;
        }
    }

    static void Pump(int milliseconds, out long maxDispatchMs)
    {
        maxDispatchMs = 0;
        var total = Stopwatch.StartNew();
        while (total.ElapsedMilliseconds < milliseconds)
        {
            var dispatch = Stopwatch.StartNew();
            Application.DoEvents();
            dispatch.Stop();
            maxDispatchMs = Math.Max(maxDispatchMs, dispatch.ElapsedMilliseconds);
            Thread.Sleep(5);
        }
    }
}
