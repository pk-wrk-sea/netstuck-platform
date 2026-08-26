using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using NetStuck;

static class UiV103ActiveSnapshot
{
    [STAThread]
    static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        string state = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "NetStuck-v110-active-snapshot-state.json");
        try { if (System.IO.File.Exists(state)) System.IO.File.Delete(state); } catch { }
        Environment.SetEnvironmentVariable("NETSTUCK_TEST_STATE_PATH", state);
        using (var form = new MainForm())
        {
            int requestedWidth = 1460;
            if (args.Length > 3) Int32.TryParse(args[3], out requestedWidth);
            form.Width = Math.Max(1100, requestedWidth); form.Height = 900; form.Show(); Pump(250);
            TabControl mainTabs = Flat(form).OfType<TabControl>().First(t => t.TabPages.Count >= 8);

            mainTabs.SelectedTab = mainTabs.TabPages.Cast<TabPage>().First(t => t.Text == "Traceroute"); Pump(100);
            TabControl sessionTabs = (TabControl)Get(form, "traceSessionTabs"); sessionTabs.SelectedIndex = 0;
            object session = ((IEnumerable)Get(form, "traceSessionsV103")).Cast<object>().First();
            ((ComboBox)Field(session, "Target")).Text = "127.0.0.1";
            ((NumericUpDown)Field(session, "Timeout")).Value = 250;
            ((NumericUpDown)Field(session, "Interval")).Value = 250;
            if (args.Length > 2)
            {
                ((ComboBox)Field(session, "Protocol")).DroppedDown = true; Pump(100);
                Save(form, args[2]);
                ((ComboBox)Field(session, "Protocol")).DroppedDown = false;
            }
            ((Button)Field(session, "Start")).PerformClick(); Pump(1400);
            Save(form, args[0]);
            ((Button)Field(session, "Stop")).PerformClick(); Pump(400);

            mainTabs.SelectedTab = mainTabs.TabPages.Cast<TabPage>().First(t => t.Text == "Live Ping"); Pump(100);
            ((TextBox)Get(form, "targetInput")).Text = "127.0.0.1 Localhost\r\n203.0.113.1 Expected-timeout";
            ((NumericUpDown)Get(form, "pingTimeout")).Value = 250;
            ((NumericUpDown)Get(form, "pingInterval")).Value = 250;
            ((ComboBox)Get(form, "pingProtocol")).SelectedItem = "ICMP";
            ((Button)Get(form, "pingStartButton")).PerformClick(); Pump(1500);
            DataGridView grid = (DataGridView)Get(form, "pingGrid");
            foreach (string columnName in new[] { "Seq", "SourceIp", "Protocol", "Port" }) grid.Columns[columnName].Visible = true;
            if (grid.Rows.Count > 0)
            {
                grid.Rows[0].Selected = true;
                Invoke(form, "SelectPingHistory", grid, new DataGridViewCellEventArgs(0, 0));
                Pump(100);
            }
            Save(form, args[1]);
            ((Button)Get(form, "pingStopButton")).PerformClick(); Pump(350);
            form.Close();
        }
        try { if (System.IO.File.Exists(state)) System.IO.File.Delete(state); } catch { }
        Environment.SetEnvironmentVariable("NETSTUCK_TEST_STATE_PATH", null);
    }

    static void Save(Form form, string path)
    {
        using (var image = new Bitmap(form.Width, form.Height))
        {
            form.DrawToBitmap(image, new Rectangle(0, 0, image.Width, image.Height));
            image.Save(path, ImageFormat.Png);
        }
    }

    static object Get(object target, string name)
    {
        FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null) throw new MissingFieldException(name);
        return field.GetValue(target);
    }

    static object Field(object target, string name)
    {
        return target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(target);
    }

    static object Invoke(object target, string name, params object[] args)
    {
        return target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
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
        while (watch.ElapsedMilliseconds < milliseconds) { Application.DoEvents(); Thread.Sleep(10); }
    }
}
