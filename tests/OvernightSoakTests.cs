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

static class OvernightSoakTests
{
    static int failures;

    [STAThread]
    static int Main(string[] args)
    {
        int seconds = 8 * 60 * 60;
        int option = Array.IndexOf(args, "--seconds");
        if (option >= 0 && option + 1 < args.Length) Int32.TryParse(args[option + 1], out seconds);
        seconds = Math.Max(10, seconds);
        string state = Path.Combine(Path.GetTempPath(), "NetStuck-soak-" + Guid.NewGuid().ToString("N") + ".json");
        Environment.SetEnvironmentVariable("NETSTUCK_TEST_STATE_PATH", state);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        using (var form = new MainForm())
        {
            form.Width = 1460; form.Height = 900; form.Show(); Pump(500, ref ignoredDispatch);
            long startMemory = Process.GetCurrentProcess().WorkingSet64;

            ((TextBox)Get(form, "targetInput")).Text = "127.0.0.1 Local-success\r\n192.0.2.1 Packet-loss";
            ((NumericUpDown)Get(form, "pingInterval")).Value = 250;
            ((NumericUpDown)Get(form, "pingTimeout")).Value = 500;
            ((Button)Get(form, "pingStartButton")).PerformClick();

            object traceSession = ((IEnumerable)Get(form, "traceSessionsV103")).Cast<object>().Last();
            var watch = Stopwatch.StartNew();
            long worstDispatch = 0;
            int syntheticCycle = 0;
            while (watch.Elapsed < TimeSpan.FromSeconds(seconds))
            {
                long dispatch = 0;
                Pump(250, ref dispatch); worstDispatch = Math.Max(worstDispatch, dispatch);
                if (watch.ElapsedMilliseconds / 1000 <= syntheticCycle) continue;
                syntheticCycle++;
                string address = syntheticCycle % 2 == 0 ? "10.20.30.1" : "10.20.30.2";
                ApplySyntheticTrace(form, traceSession, syntheticCycle, address);
                Invoke(form, "ApplyTraceDnsResultV120", traceSession, 1, address,
                    syntheticCycle % 2 == 0 ? "core-a.test" : "core-b.test", "");
                for (int i = 0; i < 64; i++) Invoke(form, "AppendTerminal", "[SOAK] device-" + i + " update " + syntheticCycle);
                Invoke(form, "ClassifyCollectorFailureV120", "Access denied by TACACS+");
                Invoke(form, "ClassifyCollectorFailureV120", "Maximum number of VTY sessions reached");
            }

            ((Button)Get(form, "pingStopButton")).PerformClick(); Pump(1200, ref ignoredDispatch);
            Invoke(form, "DrainCollectorTerminalQueueV120");
            DataTable ping = (DataTable)Get(form, "pingTable");
            DataTable events = (DataTable)Field(traceSession, "EventTable");
            long memoryGrowth = Process.GetCurrentProcess().WorkingSet64 - startMemory;
            int pendingTerminal = Convert.ToInt32(Get(form, "collectorTerminalQueueV120").GetType().GetProperty("Count").GetValue(Get(form, "collectorTerminalQueueV120"), null));

            Check("packet-loss and success targets keep polling", ping.Rows.Count == 2 && ping.Rows.Cast<DataRow>().All(row => Convert.ToInt32(row["Sent"]) >= 4));
            Check("route-change events survive long polling", events.Rows.Cast<DataRow>().Any(row => Convert.ToString(row["Type"]) == "Route"));
            Check("DNS-change events survive long polling", events.Rows.Cast<DataRow>().Any(row => Convert.ToString(row["Type"]) == "DNS"));
            Check("TACACS rejection is classified", Convert.ToString(Invoke(form, "ClassifyCollectorFailureV120", "Access denied by TACACS+")) == "Authentication rejected");
            Check("VTY limit is classified", Convert.ToString(Invoke(form, "ClassifyCollectorFailureV120", "Maximum number of VTY sessions reached")) == "Device VTY/session limit");
            Check("UI stays responsive", worstDispatch < 500);
            Check("memory growth stays bounded", memoryGrowth < 256L * 1024 * 1024);
            Check("collector terminal queue drains", pendingTerminal == 0 && ((RichTextBox)Get(form, "collectorTerminal")).TextLength <= 2000000);
            Console.WriteLine("Duration: " + watch.Elapsed + "; worst dispatch: " + worstDispatch + " ms; memory growth: " + (memoryGrowth / 1024 / 1024) + " MB");
            form.Close();
        }

        try { if (File.Exists(state)) File.Delete(state); } catch { }
        try { string cache = Path.Combine(Path.GetDirectoryName(state), "trace-lookups.json"); if (File.Exists(cache)) File.Delete(cache); } catch { }
        Environment.SetEnvironmentVariable("NETSTUCK_TEST_STATE_PATH", null);
        Console.WriteLine("Failures: " + failures);
        return failures == 0 ? 0 : 1;
    }

    static long ignoredDispatch;

    static void ApplySyntheticTrace(MainForm form, object session, int cycle, string address)
    {
        Assembly assembly = typeof(MainForm).Assembly;
        Type probeType = assembly.GetType("NetStuck.HopProbe");
        object probe = Activator.CreateInstance(probeType);
        SetField(probe, "Hop", 1); SetField(probe, "Address", address); SetField(probe, "Status", "Reply");
        SetField(probe, "Responded", true); SetField(probe, "Reached", false); SetField(probe, "Latency", 2.5 + cycle % 4);
        Array probes = Array.CreateInstance(probeType, 1); probes.SetValue(probe, 0);
        Type cycleType = assembly.GetType("NetStuck.TraceCycleResultV105");
        object result = Activator.CreateInstance(cycleType);
        SetField(result, "Cycle", cycle); SetField(result, "Probes", probes);
        Invoke(form, "ApplyTraceCycleV105", session, result, "ICMP", 0);
    }

    static void Check(string name, bool ok)
    {
        Console.WriteLine((ok ? "PASS " : "FAIL ") + name);
        if (!ok) failures++;
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

    static void SetField(object target, string name, object value)
    {
        FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        field.SetValue(target, value);
    }

    static object Invoke(object target, string name, params object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic);
        if (method == null) throw new MissingMethodException(name);
        return method.Invoke(method.IsStatic ? null : target, args);
    }

    static void Pump(int milliseconds, ref long maxDispatch)
    {
        var watch = Stopwatch.StartNew();
        while (watch.ElapsedMilliseconds < milliseconds)
        {
            var dispatch = Stopwatch.StartNew(); Application.DoEvents(); dispatch.Stop();
            maxDispatch = Math.Max(maxDispatch, dispatch.ElapsedMilliseconds);
            Thread.Sleep(5);
        }
    }
}
