using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace NetStuck
{
    // All writers serialize through one gate, including the timer and shutdown.
    internal static class AtomicJson
    {
        static readonly object Gate = new object();
        public static void Write(string path, object value)
        {
            lock (Gate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
                string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    byte[] bytes = new UTF8Encoding(false).GetBytes(new JavaScriptSerializer().Serialize(value));
                    using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
                    if (File.Exists(path))
                    {
                        bool valid = false;
                        try { valid = new JavaScriptSerializer().DeserializeObject(File.ReadAllText(path)) != null; } catch { }
                        File.Replace(temporary, path, valid ? path + ".bak" : null);
                    }
                    else File.Move(temporary, path);
                }
                finally { if (File.Exists(temporary)) File.Delete(temporary); }
            }
        }

        public static T Read<T>(string path) where T : class
        {
            lock (Gate)
            {
                Exception failure = null;
                foreach (string candidate in new[] { path, path + ".bak" })
                {
                    if (!File.Exists(candidate)) continue;
                    try
                    {
                        T value = new JavaScriptSerializer().Deserialize<T>(File.ReadAllText(candidate, Encoding.UTF8));
                        if (value == null) throw new InvalidDataException("Empty saved data.");
                        return value;
                    }
                    catch (Exception ex) { failure = ex; }
                }
                if (failure != null) throw new InvalidDataException("Saved data and backup could not be read.", failure);
                return null;
            }
        }
    }

    // The native header supplies resizing/reordering; this adds a full-height guide.
    internal sealed class GuidedGrid : DataGridView
    {
        int guide = -1;
        bool headerDrag;
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            headerDrag = e.Button == MouseButtons.Left && e.Y >= 0 && e.Y <= ColumnHeadersHeight;
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (headerDrag && e.Button == MouseButtons.Left)
            { guide = Math.Max(0, Math.Min(ClientSize.Width - 1, e.X)); Invalidate(); }
        }
        protected override void OnMouseUp(MouseEventArgs e)
        { base.OnMouseUp(e); headerDrag = false; guide = -1; Invalidate(); }
        protected override void OnMouseCaptureChanged(EventArgs e)
        { base.OnMouseCaptureChanged(e); if (!Capture) { headerDrag = false; guide = -1; Invalidate(); } }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (guide < 0) return;
            using (var pen = new Pen(Color.FromArgb(0, 120, 230), 2))
            { pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash; e.Graphics.DrawLine(pen, guide, 0, guide, ClientSize.Height); }
        }
    }

    public sealed partial class MainForm
    {
        readonly CancellationTokenSource maintenanceCancellation = new CancellationTokenSource();
        bool fittingDesktop;
        bool stateSaveSucceeded = true;

        static string ClockText(DateTime utc, TimeZoneInfo zone)
        {
            DateTime local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zone);
            TimeSpan offset = zone.GetUtcOffset(utc);
            return local.ToString("yyyy-MM-dd  HH:mm:ss") + " UTC" + (offset < TimeSpan.Zero ? "-" : "+") + offset.Duration().ToString(@"hh\:mm");
        }

        void InitializeResponsiveLayout()
        {
            // Dense input cards scroll independently; results use the available height.
            foreach (TabPage page in tabs.TabPages)
            {
                int width = page.Text == "Config Collector" ? 840 : page.Text == "Traceroute" ? 820 : 800;
                int height = page.Text == "Live Ping" ? 460 : page.Text == "Traceroute" ? 560 : page.Text == "Calculators" ? 460 : 360;
                ProtectPageCanvas(page, width, height);
            }
            tabs.Multiline = false;
            Shown += delegate { FitDesktop(); };
            LocationChanged += delegate { if (Visible) FitDesktop(); };
            SizeChanged += delegate { if (Visible) FitDesktop(); };
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged += DesktopSettingsChanged;
            FormClosed += delegate { Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= DesktopSettingsChanged; };
        }

        void DesktopSettingsChanged(object sender, EventArgs e)
        {
            if (!appClosing && IsHandleCreated && !IsDisposed)
                try { BeginInvoke((Action)FitDesktop); } catch (InvalidOperationException) { }
        }

        void FitDesktop()
        {
            if (fittingDesktop || IsDisposed) return;
            fittingDesktop = true;
            try
            {
                Rectangle work = Screen.FromControl(this).WorkingArea;
                MinimumSize = new Size(Math.Min(LayoutPixels(1100), work.Width), Math.Min(LayoutPixels(700), work.Height));
                if (WindowState == FormWindowState.Normal)
                {
                    Size = new Size(Math.Min(Width, work.Width), Math.Min(Height, work.Height));
                    Location = new Point(Math.Max(work.Left, Math.Min(Left, work.Right - Width)), Math.Max(work.Top, Math.Min(Top, work.Bottom - Height)));
                }
            }
            finally { fittingDesktop = false; }
        }

        void ProtectPageCanvas(TabPage page, int minimumWidth, int minimumHeight)
        {
            var children = page.Controls.Cast<Control>().ToArray();
            var viewport = new Panel { Name = "responsiveViewport", Dock = DockStyle.Fill, AutoScroll = true, Margin = new Padding(0) };
            var canvas = new Panel { Name = "responsiveCanvas", Location = Point.Empty, Margin = new Padding(0) };
            page.Controls.Clear();
            canvas.Controls.AddRange(children);
            viewport.Controls.Add(canvas); page.Controls.Add(viewport);
            Action arrange = delegate
            {
                int minimumW = LayoutPixels(minimumWidth), minimumH = LayoutPixels(minimumHeight);
                int availableW = viewport.ClientSize.Width, availableH = viewport.ClientSize.Height;
                // ClientSize already excludes visible scrollbars. Reserve space only
                // when overflow requires a new one, instead of creating overflow itself.
                if (minimumH > availableH && !viewport.VerticalScroll.Visible) availableW -= SystemInformation.VerticalScrollBarWidth;
                if (minimumW > availableW && !viewport.HorizontalScroll.Visible) availableH -= SystemInformation.HorizontalScrollBarHeight;
                int w = Math.Max(minimumW, availableW), h = Math.Max(minimumH, availableH);
                Size next = new Size(w, h);
                if (canvas.Size != next) canvas.Size = next;
            };
            viewport.SizeChanged += delegate { arrange(); };
            viewport.FontChanged += delegate { arrange(); };
            arrange();
        }

        int LayoutPixels(int logicalPixels)
        {
            if (AutoScaleMode == AutoScaleMode.None) return logicalPixels;
            using (var graphics = CreateGraphics()) return Math.Max(1, (int)Math.Round(logicalPixels * graphics.DpiX / 96f));
        }

        void ProtectInputCard(Control card, Control pinnedActions, int minimumHeight)
        {
            var children = card.Controls.Cast<Control>().Where(c => c != pinnedActions).ToArray();
            var viewport = new Panel { Name = "inputViewport", Dock = DockStyle.Fill, AutoScroll = true, BackColor = Surface, Margin = new Padding(0) };
            var canvas = new Panel { Name = "inputCanvas", Location = Point.Empty, BackColor = Surface, Margin = new Padding(0) };
            foreach (Control child in children) card.Controls.Remove(child);
            canvas.Controls.AddRange(children);
            viewport.Controls.Add(canvas);
            card.Controls.Add(viewport);
            viewport.BringToFront();
            Action arrange = delegate
            {
                int height = Math.Max(LayoutPixels(minimumHeight), viewport.ClientSize.Height);
                int width = viewport.ClientSize.Width;
                if (height > viewport.ClientSize.Height && !viewport.VerticalScroll.Visible) width -= SystemInformation.VerticalScrollBarWidth;
                canvas.Size = new Size(Math.Max(1, width), height);
            };
            viewport.SizeChanged += delegate { arrange(); };
            viewport.FontChanged += delegate { arrange(); };
            arrange();
        }

        void ConfigureResponsiveSplit(SplitContainer split, int desired, int firstMin, int secondMin)
        {
            bool arranging = false, initialized = false;
            double ratio = 0.5;
            int fixedDistance = desired;
            Action arrange = delegate
            {
                if (arranging || split.IsDisposed) return;
                int available = (split.Orientation == Orientation.Vertical ? split.ClientSize.Width : split.ClientSize.Height) - split.SplitterWidth;
                if (available <= 2) return;
                arranging = true;
                try
                {
                    int first = LayoutPixels(firstMin), second = LayoutPixels(secondMin);
                    int a = Math.Min(first, available / 2), b = Math.Min(second, available / 2);
                    if (available >= first + second) { a = first; b = second; }
                    split.Panel1MinSize = 0; split.Panel2MinSize = 0;
                    int distance = split.FixedPanel == FixedPanel.Panel1 && desired >= 0 ? LayoutPixels(fixedDistance)
                        : initialized ? (int)(available * ratio) : (desired < 0 ? available / 2 : LayoutPixels(desired));
                    split.SplitterDistance = Math.Max(a, Math.Min(available - b, distance));
                    split.Panel1MinSize = a; split.Panel2MinSize = b;
                    if (!initialized && available >= first + second) { initialized = true; ratio = split.SplitterDistance / (double)available; }
                }
                finally { arranging = false; }
            };
            split.SizeChanged += delegate { arrange(); };
            split.SplitterMoved += delegate
            {
                if (arranging || !initialized) return;
                int available = (split.Orientation == Orientation.Vertical ? split.Width : split.Height) - split.SplitterWidth;
                if (available > 0) ratio = split.SplitterDistance / (double)available;
                if (split.FixedPanel == FixedPanel.Panel1 && desired >= 0)
                    fixedDistance = (int)Math.Round(split.SplitterDistance * 96.0 / (AutoScaleMode == AutoScaleMode.None ? 96 : CreateGraphicsDpiV2()));
            };
            arrange();
        }

        void ShowColumnChooser(DataGridView grid, string title)
        {
            using (var dialog = new Form { Text = title, Size = new Size(365, 520), StartPosition = FormStartPosition.CenterParent, Font = Font, MinimizeBox = false, MaximizeBox = false, ShowInTaskbar = false })
            {
                var list = new CheckedListBox { Dock = DockStyle.Fill, CheckOnClick = true, HorizontalScrollbar = true, AccessibleName = "Visible columns" };
                var columns = grid.Columns.Cast<DataGridViewColumn>().OrderBy(c => c.DisplayIndex).ToArray();
                foreach (var column in columns) list.Items.Add(column.HeaderText, column.Visible);
                var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8) };
                var ok = ActionButton("Apply", true, 90);
                ok.Click += delegate
                {
                    if (list.CheckedItems.Count == 0) { MessageBox.Show(dialog, "Keep at least one column visible."); return; }
                    grid.CurrentCell = null;
                    for (int i = 0; i < columns.Length; i++) columns[i].Visible = list.GetItemChecked(i);
                    dialog.DialogResult = DialogResult.OK;
                };
                var all = ActionButton("Select all", false, 100);
                all.Click += delegate { for (int i = 0; i < list.Items.Count; i++) list.SetItemChecked(i, true); };
                bar.Controls.Add(ok); bar.Controls.Add(all); dialog.Controls.Add(list); dialog.Controls.Add(bar);
                dialog.AcceptButton = ok;
                ApplyTheme(dialog);
                dialog.ShowDialog(this);
            }
        }

        void PrepareDialog(Form dialog)
        {
            dialog.AutoScaleDimensions = new SizeF(96, 96);
            dialog.AutoScaleMode = AutoScaleMode.Dpi;
            dialog.Shown += delegate
            {
                Rectangle work = Screen.FromControl(dialog).WorkingArea;
                dialog.Size = new Size(Math.Min(dialog.Width, work.Width), Math.Min(dialog.Height, work.Height));
                dialog.Location = new Point(Math.Max(work.Left, Math.Min(dialog.Left, work.Right - dialog.Width)), Math.Max(work.Top, Math.Min(dialog.Top, work.Bottom - dialog.Height)));
            };
        }

        static void RestoreColumns(DataGridView grid, List<string> visible)
        {
            if (visible == null || !grid.Columns.Cast<DataGridViewColumn>().Any(c => visible.Contains(c.Name))) return;
            grid.CurrentCell = null;
            foreach (DataGridViewColumn column in grid.Columns) column.Visible = visible.Contains(column.Name);
        }

        async Task<string> DownloadLookupAsync(WebClient web, string address)
        {
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(maintenanceCancellation.Token))
            {
                timeout.CancelAfter(10000);
                using (timeout.Token.Register(web.CancelAsync))
                {
                    timeout.Token.ThrowIfCancellationRequested();
                    string result = await web.DownloadStringTaskAsync(address);
                    timeout.Token.ThrowIfCancellationRequested();
                    return result;
                }
            }
        }
    }
}
