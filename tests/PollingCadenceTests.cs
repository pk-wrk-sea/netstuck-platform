using System;
using System.Collections;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using NetStuck;

static class PollingCadenceTests
{
    static int failures;

    [STAThread]
    static int Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        string state = Path.Combine(Path.GetTempPath(), "NetStuck-polling-cadence-test.json");
        if (File.Exists(state)) File.Delete(state);
        Environment.SetEnvironmentVariable("NETSTUCK_TEST_STATE_PATH", state);
        using (var form = new MainForm())
        {
            form.Width = 1460; form.Height = 900; form.Show(); Pump(300);

            int max250 = Convert.ToInt32(Invoke(form, "MaxOutstandingPollsV105", 1000, 250));
            int max1000 = Convert.ToInt32(Invoke(form, "MaxOutstandingPollsV105", 1000, 1000));
            Check("poll overlap is bounded", max250 == 6 && max1000 == 3, max250 + "/" + max1000 + " slots");

            TabControl tabs = Flat(form).OfType<TabControl>().First(t => t.TabPages.Count >= 8);
            tabs.SelectedTab = tabs.TabPages.Cast<TabPage>().First(t => t.Text == "Live Ping"); Pump(80);
            int ping250 = MeasurePing(form, 250, 3400);
            int ping1000 = MeasurePing(form, 1000, 3400);
            Check("Live Ping 250 ms cadence is materially faster than 1000 ms during timeout", ping250 >= ping1000 * 2, ping250 + " vs " + ping1000 + " completions");

            tabs.SelectedTab = tabs.TabPages.Cast<TabPage>().First(t => t.Text == "Traceroute"); Pump(80);
            int trace250 = MeasureTrace(form, 250, 3400);
            int trace1000 = MeasureTrace(form, 1000, 3400);
            Check("Traceroute 250 ms cadence is materially faster than 1000 ms during timeout", trace250 >= Math.Max(2, trace1000 * 2), trace250 + " vs " + trace1000 + " samples");
            form.Close();
        }
        try { if (File.Exists(state)) File.Delete(state); } catch { }
        Environment.SetEnvironmentVariable("NETSTUCK_TEST_STATE_PATH", null);
        Console.WriteLine("Failures: " + failures);
        int exitCode = failures == 0 ? 0 : 1;
        Console.Out.Flush();
        // Rapid System.Net.NetworkInformation.Ping teardown can fault inside
        // the legacy CLR several seconds after every task and Form has closed
        // on Windows Server 2025 runners. All owned resources are already
        // cleaned above; exit explicitly so that native finalizer race cannot
        // overwrite the completed suite's verified exit code.
        Environment.Exit(exitCode);
        return exitCode;
    }

    static int MeasurePing(MainForm form, int interval, int duration)
    {
        ((TextBox)Get(form, "targetInput")).Text = "192.0.2.1 RFC5737-timeout";
        ((NumericUpDown)Get(form, "pingInterval")).Value = interval;
        ((NumericUpDown)Get(form, "pingTimeout")).Value = 1000;
        ((ComboBox)Get(form, "pingProtocol")).SelectedItem = "ICMP";
        ((Button)Get(form, "pingStartButton")).PerformClick(); Pump(duration);
        int sent = Convert.ToInt32(((DataTable)Get(form, "pingTable")).Rows[0]["Sent"]);
        ((Button)Get(form, "pingStopButton")).PerformClick();
        PumpUntil(delegate { return Get(form, "pingCancellation") == null; }, 2500);
        return sent;
    }

    static int MeasureTrace(MainForm form, int interval, int duration)
    {
        object session = ((IEnumerable)Get(form, "traceSessionsV103")).Cast<object>().First();
        ((TabControl)Get(form, "traceSessionTabs")).SelectedTab = (TabPage)Field(session, "Page");
        Pump(50);
        ((ComboBox)Field(session, "Target")).Text = "192.0.2.1";
        ((NumericUpDown)Field(session, "MaxHops")).Value = 1;
        ((NumericUpDown)Field(session, "Interval")).Value = interval;
        ((NumericUpDown)Field(session, "Timeout")).Value = 1000;
        ((ComboBox)Field(session, "Protocol")).SelectedItem = "ICMP";
        Button start = (Button)Field(session, "Start");
        start.PerformClick(); Pump(duration);
        DataTable table = (DataTable)Field(session, "Table");
        int sent = table.Rows.Count == 0 ? 0 : Convert.ToInt32(table.Rows[0]["Sent"]);
        if (sent == 0)
        {
            DataTable events = (DataTable)Field(session, "EventTable");
            Console.WriteLine("TRACE DEBUG interval=" + interval + " cycles=" + Field(session, "CycleNumber") + " events=" +
                String.Join(" | ", events.Rows.Cast<DataRow>().Select(r => Convert.ToString(r["Type"]) + ":" + Convert.ToString(r["Message"])).ToArray()));
        }
        ((Button)Field(session, "Stop")).PerformClick();
        PumpUntil(delegate { return Field(session, "Cancellation") == null && start.Enabled; }, 2500);
        return sent;
    }

    static void Check(string name, bool ok, string detail)
    {
        Console.WriteLine((ok ? "PASS " : "FAIL ") + name + " - " + detail);
        if (!ok) failures++;
    }

    static object Get(object target, string name)
    {
        FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null) throw new MissingFieldException(name);
        return field.GetValue(target);
    }

    static void Set(object target, string name, object value)
    {
        FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null) throw new MissingFieldException(name);
        field.SetValue(target, value);
    }

    static object Field(object target, string name)
    {
        FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (field == null) throw new MissingFieldException(name);
        return field.GetValue(target);
    }

    static object Invoke(object target, string name, params object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic);
        if (method == null) throw new MissingMethodException(name);
        return method.Invoke(method.IsStatic ? null : target, args);
    }

    static System.Collections.Generic.IEnumerable<Control> Flat(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (Control nested in Flat(child)) yield return nested;
        }
    }

    static void Pump(int milliseconds)
    {
        var watch = Stopwatch.StartNew();
        while (watch.ElapsedMilliseconds < milliseconds) { Application.DoEvents(); Thread.Sleep(5); }
    }

    static void PumpUntil(Func<bool> condition, int timeout)
    {
        var watch = Stopwatch.StartNew();
        while (!condition() && watch.ElapsedMilliseconds < timeout) { Application.DoEvents(); Thread.Sleep(10); }
    }
}
