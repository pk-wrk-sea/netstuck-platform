using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using NetStuck;

static class TracerouteLifecycleTests
{
    static readonly BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static int failures;
    static int assertions;
    static int passes;
    static int uiThread;

    sealed class ControlledProbeGate
    {
        readonly object sync = new object();
        readonly List<TaskCompletionSource<bool>> pending = new List<TaskCompletionSource<bool>>();
        public int Started;
        public int Completed;

        public Task Invoke(int hop, int timeout, CancellationToken token)
        {
            var completion = new TaskCompletionSource<bool>();
            lock (sync) { pending.Add(completion); Started++; }
            return completion.Task.ContinueWith(task =>
            {
                lock (sync) Completed++;
                return task.GetAwaiter().GetResult();
            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }

        public void ReleaseAll()
        {
            TaskCompletionSource<bool>[] copy;
            lock (sync) { copy = pending.ToArray(); pending.Clear(); }
            foreach (TaskCompletionSource<bool> completion in copy) completion.TrySetResult(true);
        }

        public void FaultAll()
        {
            TaskCompletionSource<bool>[] copy;
            lock (sync) { copy = pending.ToArray(); pending.Clear(); }
            foreach (TaskCompletionSource<bool> completion in copy)
                completion.TrySetException(new InvalidOperationException("Injected deterministic probe fault."));
        }
    }

    sealed class RunSnapshot
    {
        public int InFlight;
        public int Started;
        public int Observed;
        public int Faulted;
        public int ActiveCallbacks;
        public int StartedAfterStop;
        public bool DrainTimedOut;
        public bool Completed;
    }

    sealed class Harness
    {
        public string Root;
        public MainForm Form;
        public object Session;
        public Button Start;
        public Button Pause;
        public Button Stop;
        public NumericUpDown Interval;
        public NumericUpDown Timeout;
        public NumericUpDown MaxHops;
        public ComboBox Target;
        public DataTable Rows;
        public DataTable Events;
        public readonly List<int> MutationThreads = new List<int>();

        public int MutationCount
        {
            get { lock (MutationThreads) return MutationThreads.Count; }
        }
    }

    [STAThread]
    static int Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        uiThread = Thread.CurrentThread.ManagedThreadId;

        int targetedCycles = ParseTargetedCycles(args);
        if (targetedCycles > 0)
        {
            int completed = 0;
            for (int cycle = 1; cycle <= targetedCycles; cycle++)
            {
                string detail;
                bool ok = RunTargetedCycle(out detail);
                Check("Traceroute targeted lifecycle cycle " + cycle.ToString("D2"), ok, detail);
                if (ok) completed++;
            }
            Console.WriteLine("Targeted lifecycle cycles: " + completed + "/" + targetedCycles);
        }
        else
        {
            RunNormalAndZeroPendingCase();
            RunSingleDelayedProbeCase();
            RunMultipleDelayedProbeCase();
            RunFaultedProbeCase();
            RunDrainTimeoutCase();
            RunObsoleteAndRestartCase();
            RunPageDisposeCase();
            RunActiveCloseCase();
        }

        Console.WriteLine("Lifecycle assertions: " + passes + "/" + assertions);
        Console.WriteLine("Failures: " + failures);
        return failures == 0 ? 0 : 1;
    }

    static int ParseTargetedCycles(string[] args)
    {
        if (args == null || args.Length == 0) return 0;
        int cycles;
        if (args.Length != 2 || args[0] != "--cycles" || !Int32.TryParse(args[1], out cycles) || cycles < 1 || cycles > 200)
            throw new ArgumentException("Usage: TracerouteLifecycleTests.exe [--cycles 1..200]");
        return cycles;
    }

    static void RunNormalAndZeroPendingCase()
    {
        Harness harness = null;
        try
        {
            Func<int, int, CancellationToken, Task> gate = delegate { return Task.FromResult(true); };
            harness = CreateHarness(gate, 1, 1000, 200);
            harness.Start.PerformClick();
            object run = WaitForRun(harness);
            bool populated = PumpUntil(delegate { return harness.Rows.Rows.Count == 1 && harness.Events.Rows.Count >= 2; }, 3000);
            bool zeroPendingBeforeStop = PumpUntil(delegate { return Snapshot(run).InFlight == 0; }, 1500);
            harness.Stop.PerformClick();
            bool stopped = WaitStopped(harness, 2500);
            RunSnapshot snapshot = Snapshot(run);
            Check("Traceroute normal completion binds both route and event tables", populated,
                "rows=" + harness.Rows.Rows.Count + "; events=" + harness.Events.Rows.Count);
            Check("Traceroute bound-table mutations stay on the UI thread", MutationThreadsAreUiOnly(harness),
                "ui=" + uiThread + "; observed=" + String.Join(",", harness.MutationThreads.Distinct().OrderBy(value => value)));
            Check("Traceroute Stop with zero pending probes completes", zeroPendingBeforeStop && stopped,
                "zero-before=" + zeroPendingBeforeStop + "; stopped=" + stopped);
            Check("Traceroute zero-pending run releases and observes every owned task", IsQuiescent(snapshot), SnapshotDetail(snapshot));
            Check("Traceroute stopped run has no late table mutation", NoLateMutation(harness, 150), "mutations=" + harness.MutationCount);
        }
        catch (Exception ex) { Check("Traceroute normal/zero-pending case completes without exception", false, Safe(ex)); }
        finally { CheckCleanup("normal-zero", harness); }
    }

    static void RunSingleDelayedProbeCase()
    {
        Harness harness = null;
        try
        {
            var gate = new ControlledProbeGate();
            harness = CreateHarness(gate.Invoke, 1, 1000, 200);
            harness.Start.PerformClick();
            object run = WaitForRun(harness);
            bool started = PumpUntil(delegate { return gate.Started == 1; }, 1500);
            harness.Stop.PerformClick();
            bool stoppingBoundary = started && !harness.Start.Enabled && Object.ReferenceEquals(Field(harness.Session, "ActiveRun"), run);
            gate.ReleaseAll();
            bool stopped = WaitStopped(harness, 2500);
            RunSnapshot snapshot = Snapshot(run);
            Check("Traceroute Stop awaits one delayed non-cancellable probe", stoppingBoundary && stopped,
                "gate=" + gate.Started + "/" + gate.Completed + "; stopped=" + stopped);
            Check("Traceroute delayed-probe drain observes all task terminals", IsQuiescent(snapshot) && !snapshot.DrainTimedOut, SnapshotDetail(snapshot));
            Check("Traceroute delayed completion cannot mutate after Stop", NoLateMutation(harness, 150), "mutations=" + harness.MutationCount);
        }
        catch (Exception ex) { Check("Traceroute one-delayed-probe case completes without exception", false, Safe(ex)); }
        finally { CheckCleanup("one-delayed", harness); }
    }

    static void RunMultipleDelayedProbeCase()
    {
        Harness harness = null;
        try
        {
            var gate = new ControlledProbeGate();
            harness = CreateHarness(gate.Invoke, 4, 1000, 200);
            harness.Start.PerformClick();
            object run = WaitForRun(harness);
            bool allStarted = PumpUntil(delegate { return gate.Started == 4; }, 1500);
            harness.Stop.PerformClick();
            gate.ReleaseAll();
            bool stopped = WaitStopped(harness, 2500);
            RunSnapshot snapshot = Snapshot(run);
            Check("Traceroute Stop drains multiple delayed probes from one cycle", allStarted && stopped && gate.Completed == 4,
                "gate=" + gate.Started + "/" + gate.Completed);
            Check("Traceroute multi-probe run has no unobserved task or callback", IsQuiescent(snapshot), SnapshotDetail(snapshot));
        }
        catch (Exception ex) { Check("Traceroute multiple-delayed-probe case completes without exception", false, Safe(ex)); }
        finally { CheckCleanup("multiple-delayed", harness); }
    }

    static void RunFaultedProbeCase()
    {
        Harness harness = null;
        try
        {
            var gate = new ControlledProbeGate();
            harness = CreateHarness(gate.Invoke, 1, 1000, 200);
            harness.Start.PerformClick();
            object run = WaitForRun(harness);
            bool started = PumpUntil(delegate { return gate.Started == 1; }, 1500);
            harness.Stop.PerformClick();
            gate.FaultAll();
            bool stopped = WaitStopped(harness, 2500);
            RunSnapshot snapshot = Snapshot(run);
            Check("Traceroute probe fault during Stop is observed", started && stopped && snapshot.Faulted >= 1, SnapshotDetail(snapshot));
            Check("Traceroute faulted run reaches complete quiescence", IsQuiescent(snapshot), SnapshotDetail(snapshot));
        }
        catch (Exception ex) { Check("Traceroute faulted-probe case completes without exception", false, Safe(ex)); }
        finally { CheckCleanup("faulted", harness); }
    }

    static void RunDrainTimeoutCase()
    {
        Harness harness = null;
        try
        {
            var gate = new ControlledProbeGate();
            harness = CreateHarness(gate.Invoke, 1, 1000, 200);
            harness.Start.PerformClick();
            object run = WaitForRun(harness);
            bool started = PumpUntil(delegate { return gate.Started == 1; }, 1500);
            harness.Stop.PerformClick();
            bool timeoutReported = PumpUntil(delegate { return Snapshot(run).DrainTimedOut; }, 1800);
            RunSnapshot timedOut = Snapshot(run);
            bool restartStillBlocked = Field(harness.Session, "ActiveRun") != null && !harness.Start.Enabled
                && timedOut.InFlight > 0 && !timedOut.Completed;
            Check("Traceroute drain timeout reports incomplete quiescence", started && timeoutReported,
                SnapshotDetail(timedOut));
            Check("Traceroute drain timeout cannot produce a false stopped state", restartStillBlocked,
                "active=" + (Field(harness.Session, "ActiveRun") != null) + "; start-enabled=" + harness.Start.Enabled);
            gate.ReleaseAll();
            bool stopped = WaitStopped(harness, 2500);
            RunSnapshot recovered = Snapshot(run);
            Check("Traceroute timed-out drain continues observing until terminal completion", stopped && IsQuiescent(recovered),
                SnapshotDetail(recovered));
        }
        catch (Exception ex) { Check("Traceroute drain-timeout case completes without exception", false, Safe(ex)); }
        finally { CheckCleanup("drain-timeout", harness); }
    }

    static void RunObsoleteAndRestartCase()
    {
        Harness harness = null;
        try
        {
            var firstGate = new ControlledProbeGate();
            harness = CreateHarness(firstGate.Invoke, 1, 1000, 200);
            harness.Start.PerformClick();
            object firstRun = WaitForRun(harness);
            bool firstStarted = PumpUntil(delegate { return firstGate.Started == 1; }, 1500);
            harness.Stop.PerformClick();
            harness.Start.PerformClick();
            bool restartBlocked = firstStarted && Object.ReferenceEquals(Field(harness.Session, "ActiveRun"), firstRun) && firstGate.Started == 1;
            firstGate.ReleaseAll();
            bool firstStopped = WaitStopped(harness, 2500);
            RunSnapshot firstSnapshot = Snapshot(firstRun);

            Func<int, int, CancellationToken, Task> secondGate = delegate { return Task.FromResult(true); };
            Set(harness.Form, "traceProbeGateV123", secondGate);
            harness.Start.PerformClick();
            object secondRun = WaitForRun(harness);
            bool secondPopulated = PumpUntil(delegate { return harness.Rows.Rows.Count == 1; }, 2500);
            PumpUntil(delegate { return Snapshot(secondRun).InFlight == 0; }, 1500);
            harness.Stop.PerformClick();
            bool secondStopped = WaitStopped(harness, 2500);
            Check("Traceroute rapid Stop to Start is blocked until old run quiesces", restartBlocked && firstStopped,
                "blocked=" + restartBlocked + "; first-stopped=" + firstStopped);
            Check("Traceroute new run starts only after prior Stop completes", secondPopulated && secondStopped && !Object.ReferenceEquals(firstRun, secondRun),
                "new-run=" + !Object.ReferenceEquals(firstRun, secondRun));
            Check("Traceroute obsolete run owns no task or callback after restart", IsQuiescent(firstSnapshot) && firstSnapshot.StartedAfterStop == 0,
                SnapshotDetail(firstSnapshot));
        }
        catch (Exception ex) { Check("Traceroute obsolete/restart case completes without exception", false, Safe(ex)); }
        finally { CheckCleanup("obsolete-restart", harness); }
    }

    static void RunPageDisposeCase()
    {
        Harness harness = null;
        try
        {
            var gate = new ControlledProbeGate();
            harness = CreateHarness(gate.Invoke, 1, 1000, 200);
            harness.Start.PerformClick();
            object run = WaitForRun(harness);
            bool started = PumpUntil(delegate { return gate.Started == 1; }, 1500);
            harness.Stop.PerformClick();
            TabPage page = (TabPage)Field(harness.Session, "Page");
            page.Dispose();
            int mutationsAtDispose = harness.MutationCount;
            gate.ReleaseAll();
            bool completed = PumpUntil(delegate { return RunCompleted(run); }, 2500);
            RunSnapshot snapshot = Snapshot(run);
            Check("Traceroute page disposal during probe completes without late mutation", started && completed && harness.MutationCount == mutationsAtDispose,
                "before=" + mutationsAtDispose + "; after=" + harness.MutationCount);
            Check("Traceroute disposed-page run drains all owned work", IsQuiescent(snapshot), SnapshotDetail(snapshot));
        }
        catch (Exception ex) { Check("Traceroute page-dispose case completes without exception", false, Safe(ex)); }
        finally { CheckCleanup("page-dispose", harness); }
    }

    static void RunActiveCloseCase()
    {
        Harness harness = null;
        try
        {
            var gate = new ControlledProbeGate();
            harness = CreateHarness(gate.Invoke, 1, 1000, 200);
            harness.Start.PerformClick();
            object run = WaitForRun(harness);
            bool started = PumpUntil(delegate { return gate.Started == 1; }, 1500);
            int beforeClose = harness.MutationCount;
            harness.Form.Close();
            int afterClose = harness.MutationCount;
            gate.ReleaseAll();
            bool completed = PumpUntil(delegate { return RunCompleted(run); }, 2500);
            PumpFor(120);
            RunSnapshot snapshot = Snapshot(run);
            Check("Traceroute active form close allows pending probe to terminate", started && completed, "completed=" + completed);
            Check("Traceroute close during probe produces no post-close table mutation", beforeClose == afterClose && afterClose == harness.MutationCount,
                "before=" + beforeClose + "; after=" + harness.MutationCount);
            Check("Traceroute closed run observes and releases all owned tasks", IsQuiescent(snapshot), SnapshotDetail(snapshot));
        }
        catch (Exception ex) { Check("Traceroute active-close case completes without exception", false, Safe(ex)); }
        finally { CheckCleanup("active-close", harness); }
    }

    static bool RunTargetedCycle(out string detail)
    {
        Harness harness = null;
        bool lifecycle = false;
        string lifecycleDetail = "not started";
        try
        {
            var gate = new ControlledProbeGate();
            harness = CreateHarness(gate.Invoke, 1, 1000, 200);
            harness.Start.PerformClick();
            object run = WaitForRun(harness);
            bool started = PumpUntil(delegate { return gate.Started == 1; }, 1500);
            harness.Stop.PerformClick();
            gate.ReleaseAll();
            bool stopped = WaitStopped(harness, 2500);
            RunSnapshot snapshot = Snapshot(run);
            lifecycle = started && stopped && gate.Completed == 1 && IsQuiescent(snapshot)
                && MutationThreadsAreUiOnly(harness) && NoLateMutation(harness, 40);
            lifecycleDetail = "gate=" + gate.Started + "/" + gate.Completed + "; " + SnapshotDetail(snapshot);
        }
        catch (Exception ex) { lifecycleDetail = Safe(ex); }
        bool cleanup = Cleanup(harness, out detail);
        detail = lifecycleDetail + "; cleanup=" + cleanup + "; " + detail;
        return lifecycle && cleanup;
    }

    static Harness CreateHarness(Func<int, int, CancellationToken, Task> gate, int maxHops, int interval, int timeout)
    {
        string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "NetStuck-trace-lifecycle-" + Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(root);
        Environment.SetEnvironmentVariable("NETSTUCK_TEST_ROOT", root);
        Environment.SetEnvironmentVariable("NETSTUCK_TEST_STATE_PATH", null);
        var harness = new Harness { Root = root };
        try
        {
            harness.Form = new MainForm();
            Set(harness.Form, "traceProbeGateV123", gate);
            harness.Form.AutoScaleMode = AutoScaleMode.None;
            harness.Form.StartPosition = FormStartPosition.Manual;
            harness.Form.Width = 1100;
            harness.Form.Height = 700;
            harness.Form.Show();
            PumpFor(80);

            TabControl mainTabs = Flat(harness.Form).OfType<TabControl>().First(control => control.TabPages.Count >= 8);
            mainTabs.SelectedTab = mainTabs.TabPages.Cast<TabPage>().First(page => page.Text == "Traceroute");
            PumpFor(30);
            TabControl sessionTabs = Flat(mainTabs.SelectedTab).OfType<TabControl>().First(control => control.TabPages.Count == 2);
            sessionTabs.SelectedIndex = 0;
            PumpFor(20);

            harness.Session = ((IEnumerable)Get(harness.Form, "traceSessionsV103")).Cast<object>().First();
            harness.Target = (ComboBox)Field(harness.Session, "Target");
            harness.Interval = (NumericUpDown)Field(harness.Session, "Interval");
            harness.Timeout = (NumericUpDown)Field(harness.Session, "Timeout");
            harness.MaxHops = (NumericUpDown)Field(harness.Session, "MaxHops");
            harness.Start = (Button)Field(harness.Session, "Start");
            harness.Pause = (Button)Field(harness.Session, "Pause");
            harness.Stop = (Button)Field(harness.Session, "Stop");
            harness.Rows = (DataTable)Field(harness.Session, "Table");
            harness.Events = (DataTable)Field(harness.Session, "EventTable");
            harness.Rows.RowChanged += delegate { lock (harness.MutationThreads) harness.MutationThreads.Add(Thread.CurrentThread.ManagedThreadId); };
            harness.Events.RowChanged += delegate { lock (harness.MutationThreads) harness.MutationThreads.Add(Thread.CurrentThread.ManagedThreadId); };
            harness.Target.Text = "192.0.2.10";
            harness.Interval.Value = interval;
            harness.Timeout.Value = timeout;
            harness.MaxHops.Value = maxHops;

            string[] isolatedPaths = { "statePath", "profilePath", "macCachePath", "traceLookupCachePathV120" };
            foreach (string pathField in isolatedPaths)
            {
                string value = Path.GetFullPath(Convert.ToString(Get(harness.Form, pathField)));
                if (!value.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(pathField + " escaped the owned test root.");
            }
            return harness;
        }
        catch
        {
            string cleanupDetail;
            Cleanup(harness, out cleanupDetail);
            throw;
        }
    }

    static object WaitForRun(Harness harness)
    {
        object run = null;
        if (!PumpUntil(delegate { run = Field(harness.Session, "ActiveRun"); return run != null; }, 1500))
            throw new TimeoutException("Traceroute run did not become active.");
        return run;
    }

    static bool WaitStopped(Harness harness, int timeoutMs)
    {
        return PumpUntil(delegate
        {
            return Field(harness.Session, "ActiveRun") == null && Field(harness.Session, "Cancellation") == null
                && harness.Start.Enabled && !harness.Pause.Enabled && !harness.Stop.Enabled;
        }, timeoutMs);
    }

    static bool NoLateMutation(Harness harness, int milliseconds)
    {
        int before = harness.MutationCount;
        PumpFor(milliseconds);
        return harness.MutationCount == before;
    }

    static bool MutationThreadsAreUiOnly(Harness harness)
    {
        int[] threads;
        lock (harness.MutationThreads) threads = harness.MutationThreads.Distinct().ToArray();
        return threads.Length == 1 && threads[0] == uiThread;
    }

    static RunSnapshot Snapshot(object run)
    {
        object sync = Field(run, "SyncRoot");
        lock (sync)
        {
            return new RunSnapshot
            {
                InFlight = ((IEnumerable)Field(run, "InFlightTasks")).Cast<object>().Count(),
                Started = Convert.ToInt32(Field(run, "StartedTasks")),
                Observed = Convert.ToInt32(Field(run, "ObservedTasks")),
                Faulted = Convert.ToInt32(Field(run, "FaultedTasks")),
                ActiveCallbacks = Convert.ToInt32(Field(run, "ActiveCallbacks")),
                StartedAfterStop = Convert.ToInt32(Field(run, "StartedAfterStop")),
                DrainTimedOut = Convert.ToBoolean(Field(run, "DrainTimedOut")),
                Completed = RunCompleted(run)
            };
        }
    }

    static bool RunCompleted(object run)
    {
        object completion = Field(run, "Completed");
        return ((Task)Property(completion, "Task")).IsCompleted;
    }

    static bool IsQuiescent(RunSnapshot snapshot)
    {
        return snapshot.InFlight == 0 && snapshot.ActiveCallbacks == 0 && snapshot.Started == snapshot.Observed
            && snapshot.StartedAfterStop == 0 && snapshot.Completed;
    }

    static string SnapshotDetail(RunSnapshot snapshot)
    {
        return "started=" + snapshot.Started + "; observed=" + snapshot.Observed + "; pending=" + snapshot.InFlight
            + "; callbacks=" + snapshot.ActiveCallbacks + "; faulted=" + snapshot.Faulted
            + "; after-stop=" + snapshot.StartedAfterStop + "; timeout=" + snapshot.DrainTimedOut + "; completed=" + snapshot.Completed;
    }

    static void CheckCleanup(string name, Harness harness)
    {
        string detail;
        Check("Traceroute " + name + " owns and cleans form/state/task residue", Cleanup(harness, out detail), detail);
    }

    static bool Cleanup(Harness harness, out string detail)
    {
        var errors = new List<string>();
        string root = harness == null ? null : harness.Root;
        try
        {
            if (harness != null && harness.Form != null && !harness.Form.IsDisposed)
            {
                harness.Form.Close();
                PumpFor(80);
                harness.Form.Dispose();
            }
        }
        catch (Exception ex) { errors.Add("form=" + ex.GetType().Name); }
        Environment.SetEnvironmentVariable("NETSTUCK_TEST_ROOT", null);
        Environment.SetEnvironmentVariable("NETSTUCK_TEST_STATE_PATH", null);
        try
        {
            if (!String.IsNullOrEmpty(root))
            {
                string full = Path.GetFullPath(root);
                string temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (!full.StartsWith(temp, StringComparison.OrdinalIgnoreCase)
                    || !Path.GetFileName(full).StartsWith("NetStuck-trace-lifecycle-", StringComparison.Ordinal))
                    throw new InvalidOperationException("owned lifecycle root validation failed");
                if (Directory.Exists(full)) Directory.Delete(full, true);
                if (Directory.Exists(full)) throw new IOException("owned lifecycle root still exists");
            }
        }
        catch (Exception ex) { errors.Add("state=" + ex.GetType().Name); }
        bool formGone = harness == null || harness.Form == null || harness.Form.IsDisposed;
        bool noOwnedWindow = harness == null || harness.Form == null || !Application.OpenForms.Cast<Form>().Contains(harness.Form);
        if (!formGone || !noOwnedWindow) errors.Add("window=residue");
        detail = errors.Count == 0 ? "owned root removed; window disposed; no harness task residue" : String.Join(",", errors);
        return errors.Count == 0;
    }

    static void Check(string name, bool ok, string detail)
    {
        assertions++;
        if (ok) passes++; else failures++;
        Console.WriteLine((ok ? "PASS " : "FAIL ") + name + " - " + detail);
    }

    static bool PumpUntil(Func<bool> condition, int timeoutMs)
    {
        Stopwatch watch = Stopwatch.StartNew();
        while (watch.ElapsedMilliseconds < timeoutMs)
        {
            Application.DoEvents();
            if (condition()) return true;
            Thread.Sleep(5);
        }
        Application.DoEvents();
        return condition();
    }

    static void PumpFor(int milliseconds)
    {
        Stopwatch watch = Stopwatch.StartNew();
        while (watch.ElapsedMilliseconds < milliseconds)
        {
            Application.DoEvents();
            Thread.Sleep(5);
        }
    }

    static object Get(object target, string name)
    {
        FieldInfo field = target.GetType().GetField(name, Members);
        if (field == null) throw new MissingFieldException(target.GetType().FullName, name);
        return field.GetValue(target);
    }

    static object Field(object target, string name)
    {
        FieldInfo field = target.GetType().GetField(name, Members);
        if (field == null) throw new MissingFieldException(target.GetType().FullName, name);
        return field.GetValue(target);
    }

    static object Property(object target, string name)
    {
        PropertyInfo property = target.GetType().GetProperty(name, Members);
        if (property == null) throw new MissingMemberException(target.GetType().FullName, name);
        return property.GetValue(target, null);
    }

    static void Set(object target, string name, object value)
    {
        FieldInfo field = target.GetType().GetField(name, Members);
        if (field == null) throw new MissingFieldException(target.GetType().FullName, name);
        field.SetValue(target, value);
    }

    static IEnumerable<Control> Flat(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (Control nested in Flat(child)) yield return nested;
        }
    }

    static string Safe(Exception ex)
    {
        Exception current = ex is TargetInvocationException && ex.InnerException != null ? ex.InnerException : ex;
        return current.GetType().Name + ": " + (current.Message ?? "").Replace('\r', ' ').Replace('\n', ' ');
    }
}
