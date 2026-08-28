using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using NetStuck;

static class UiFoundationTests
{
    static int failures;
    static readonly BindingFlags AllStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static readonly BindingFlags AllInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    static void Check(string name, bool ok)
    {
        Console.WriteLine((ok ? "PASS " : "FAIL ") + name);
        if (!ok) failures++;
    }

    [STAThread]
    static int Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        Assembly assembly = typeof(MainForm).Assembly;
        Type tokens = assembly.GetType("NetStuck.UiTokens", true);
        Type stateType = assembly.GetType("NetStuck.UiSemanticState", true);
        Type presenterType = assembly.GetType("NetStuck.UiStatePresenter", true);

        CheckTokenCatalog(tokens);
        CheckStateCatalog(tokens, stateType, presenterType);

        CheckCleanupFailurePropagation();

        string testRoot = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "NetStuck-ui-foundation-" + Guid.NewGuid().ToString("N"));
        testRoot = System.IO.Path.GetFullPath(testRoot);
        System.IO.Directory.CreateDirectory(testRoot);
        Environment.SetEnvironmentVariable("NETSTUCK_TEST_ROOT", testRoot);
        Environment.SetEnvironmentVariable("NETSTUCK_TEST_STATE_PATH", null);
        Exception primaryError = null;
        Exception cleanupError = null;
        try
        {
            using (var form = new MainForm())
            {
                form.AutoScaleMode = AutoScaleMode.None;
                form.StartPosition = FormStartPosition.Manual;
                form.Location = Point.Empty;
                form.Width = 1460;
                form.Height = 900;
                form.Show();
                Pump(250);

                Check("window construction smoke", form.IsHandleCreated && !form.IsDisposed && form.Text == "NetStuck");
                CheckSafeTestRoot(form, testRoot);
                CheckShell(form);
                CheckCalculators(form);
                CheckEventLog(form, stateType);
                CheckActionRoles(form);
                CheckCredentialMetadata(form);
                CheckLayouts(form);
                CheckMissingResources(form);
                form.Close();
            }
        }
        catch (Exception ex)
        {
            primaryError = ex;
            failures++;
            Console.WriteLine("FAIL UI foundation primary execution: " + ex.GetType().Name + ": " + ex.Message);
        }
        finally
        {
            Environment.SetEnvironmentVariable("NETSTUCK_TEST_ROOT", null);
            try
            {
                DeleteOwnedTestRoot(testRoot, delegate { System.IO.Directory.Delete(testRoot, true); });
            }
            catch (Exception ex)
            {
                cleanupError = ex;
                failures++;
                Console.WriteLine("FAIL UI foundation cleanup: " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        Check("primary and cleanup diagnostics remain independently observable", primaryError == null && cleanupError == null);

        Console.WriteLine("Failures: " + failures);
        return failures == 0 ? 0 : 1;
    }

    static void CheckCleanupFailurePropagation()
    {
        string probe = System.IO.Path.GetFullPath(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "NetStuck-ui-foundation-cleanup-probe-" + Guid.NewGuid().ToString("N")));
        System.IO.Directory.CreateDirectory(probe);
        bool propagated = false;
        try
        {
            DeleteOwnedTestRoot(probe, delegate { throw new System.IO.IOException("Synthetic cleanup failure."); });
        }
        catch (System.IO.IOException ex)
        {
            propagated = ex.Message.IndexOf("Synthetic cleanup failure", StringComparison.Ordinal) >= 0;
        }
        Check("deliberate cleanup failure propagates to the caller", propagated);

        try
        {
            DeleteOwnedTestRoot(probe, delegate { System.IO.Directory.Delete(probe, true); });
            Check("cleanup propagation fixture is actually removed", !System.IO.Directory.Exists(probe));
        }
        catch (Exception ex)
        {
            failures++;
            Console.WriteLine("FAIL cleanup propagation fixture removal: " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    static void DeleteOwnedTestRoot(string path, Action delete)
    {
        string full = System.IO.Path.GetFullPath(path);
        string tempRoot = System.IO.Path.GetFullPath(System.IO.Path.GetTempPath()).TrimEnd(System.IO.Path.DirectorySeparatorChar)
            + System.IO.Path.DirectorySeparatorChar;
        string leaf = System.IO.Path.GetFileName(full);
        if (!full.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase)
            || !leaf.StartsWith("NetStuck-ui-foundation-", StringComparison.Ordinal))
            throw new InvalidOperationException("Refusing to clean an unowned UI foundation path: " + leaf);
        if (System.IO.Directory.Exists(full)) delete();
        if (System.IO.Directory.Exists(full))
            throw new System.IO.IOException("Owned UI foundation test directory still exists after cleanup: " + leaf);
    }

    static void CheckTokenCatalog(Type tokens)
    {
        string[] requiredConstants =
        {
            "SpaceXs", "SpaceSm", "SpaceMd", "SpaceLg", "PageMargin", "AppHeaderHeight",
            "SectionHeaderHeight", "DenseControlHeight", "SplitterWidth", "IconLarge", "BorderWidth",
            "StandardFieldMinWidth", "FontFamily", "MonospaceFontFamily", "BodyFontSize", "CaptionFontSize",
            "ActionFontSize", "SectionTitleFontSize", "AppTitleFontSize", "ResultFontSize"
        };
        bool constantsPresent = requiredConstants.All(name => tokens.GetField(name, AllStatic) != null);
        Check("pilot-owned UI token keys are registered", constantsPresent);

        string[] removedDeferredConstants =
        {
            "SpaceXl", "DialogMargin", "SectionGap", "DialogControlHeight", "GridHeaderHeight", "GridRowHeight",
            "DialogFooterHeight", "IconSmall", "IconMedium", "FocusWidth", "CornerRadius", "NumericFieldMinWidth",
            "StandardFieldPreferredWidth", "WideFieldPreferredWidth"
        };
        Check("deferred or unused token keys are absent", removedDeferredConstants.All(name => tokens.GetField(name, AllStatic) == null));

        int xs = ConstantInt(tokens, "SpaceXs");
        int sm = ConstantInt(tokens, "SpaceSm");
        int md = ConstantInt(tokens, "SpaceMd");
        int lg = ConstantInt(tokens, "SpaceLg");
        Check("spacing scale is ordered and positive", xs > 0 && xs < sm && sm < md && md < lg);
        Check("control and layout tokens are valid", ConstantInt(tokens, "DenseControlHeight") >= 34
            && ConstantInt(tokens, "SplitterWidth") >= 6 && ConstantInt(tokens, "BorderWidth") == 1
            && ConstantInt(tokens, "StandardFieldMinWidth") >= 160);

        string[] colors =
        {
            "Surface", "SurfaceSubtle", "Text", "MutedText", "Border", "Focus", "Info",
            "Success", "Error", "Destructive", "InfoSurface", "SuccessSurface", "ErrorSurface", "HoverSurface", "PressedSurface"
        };
        Check("pilot-owned semantic color resources are complete", colors.All(name => tokens.GetProperty(name, AllStatic) != null && StaticColor(tokens, name) != Color.Empty));
        string[] removedDeferredColors = { "Canvas", "Warning", "WarningSurface", "Selection", "SelectionText", "DisabledSurface", "DisabledText" };
        Check("deferred or unused color resources are absent", removedDeferredColors.All(name => tokens.GetProperty(name, AllStatic) == null));

        Color surface = StaticColor(tokens, "Surface");
        bool textContrast = new[] { "Text", "MutedText", "Info", "Success", "Error" }
            .All(name => Contrast(StaticColor(tokens, name), surface) >= 4.5d);
        Check("normal-theme semantic text colors meet 4.5 to 1", SystemInformation.HighContrast || textContrast);
        Check("focus and required borders meet 3 to 1", SystemInformation.HighContrast
            || (Contrast(StaticColor(tokens, "Focus"), surface) >= 3d && Contrast(StaticColor(tokens, "Border"), surface) >= 3d));
    }

    static void CheckStateCatalog(Type tokens, Type stateType, Type presenterType)
    {
        string[] requiredStates =
        {
            "Idle", "Success", "Empty", "FilteredEmpty", "ValidationFailure"
        };
        string[] actual = Enum.GetNames(stateType);
        Check("pilot-owned semantic state catalog is exact", requiredStates.All(actual.Contains) && actual.Length == requiredStates.Length);
        string[] removedDeferredStates = { "Running", "Loading", "Cancelling", "Warning", "Error", "Unavailable" };
        Check("deferred or unused semantic states are absent", removedDeferredStates.All(name => !actual.Contains(name)));

        MethodInfo stateMethod = tokens.GetMethod("State", AllStatic);
        bool definitionsComplete = true;
        foreach (object state in Enum.GetValues(stateType))
        {
            object definition = stateMethod.Invoke(null, new[] { state });
            definitionsComplete &= !String.IsNullOrWhiteSpace(Convert.ToString(Field(definition, "Marker")));
            definitionsComplete &= !String.IsNullOrWhiteSpace(Convert.ToString(Field(definition, "DefaultTitle")));
            definitionsComplete &= (Color)Field(definition, "Foreground") != Color.Empty;
            definitionsComplete &= (Color)Field(definition, "Background") != Color.Empty;
        }
        Check("every semantic state has text and non-color marker", definitionsComplete);

        Type definitionType = presenterType.Assembly.GetType("NetStuck.UiStateDefinition", true);
        Check("unused progress definition fields are absent", new[] { "ShowsProgress", "ProgressStyle" }
            .All(name => definitionType.GetField(name, AllInstance) == null));
        Check("unused determinate progress API is absent", presenterType.GetMethods(AllInstance)
            .All(method => method.Name != "SetDeterminateProgress"));
        Check("removed action-enablement fields are absent", new[] { "PrimaryActionEnabled", "SecondaryActionEnabled", "CancelActionEnabled" }
            .All(name => definitionType.GetField(name, AllInstance) == null));

        using (var host = new Form())
        using (Control presenter = (Control)Activator.CreateInstance(presenterType, new object[] { "testStatePresenter", "Test state" }))
        {
            host.Controls.Add(presenter);
            host.Show();
            Pump(30);
            MethodInfo setState = presenterType.GetMethods(AllInstance).Single(method => method.Name == "SetState" && method.DeclaringType == presenterType);
            foreach (object state in Enum.GetValues(stateType))
                setState.Invoke(presenter, new object[] { state, null, "Synthetic state detail.", false });
            Check("state presenter renders every registered state", !String.IsNullOrWhiteSpace(presenter.AccessibleName)
                && !String.IsNullOrWhiteSpace(presenter.AccessibleDescription)
                && Flat(presenter).OfType<Label>().Any(label => label.Text.StartsWith("[", StringComparison.Ordinal)));

            string longEnglish = "A deliberately long operational message remains available to assistive technology even when the visible detail needs to wrap or ellipsize at the minimum supported window width.";
            setState.Invoke(presenter, new object[] { Enum.Parse(stateType, "FilteredEmpty"), "Long-text check", longEnglish, false });
            Check("long English state text remains accessible", presenter.AccessibleDescription.Contains(longEnglish));

            string longThai = "ข้อความสถานะภาษาไทยแบบยาวยังคงอ่านได้ผ่านข้อมูลการช่วยการเข้าถึง และวาดผลลัพธ์ได้โดยไม่ทำให้ส่วนติดต่อผู้ใช้ล้มเหลว";
            setState.Invoke(presenter, new object[] { Enum.Parse(stateType, "ValidationFailure"), "ทดสอบข้อความยาว", longThai, false });
            using (Bitmap bitmap = new Bitmap(Math.Max(1, presenter.Width), Math.Max(1, presenter.Height)))
                presenter.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
            Check("long Thai state text renders and remains accessible", presenter.AccessibleDescription.Contains(longThai));

            Check("state presenter has no test-only progress control", !Flat(presenter).OfType<ProgressBar>().Any());

            object emptyState = Enum.Parse(stateType, "Empty");
            setState.Invoke(presenter, new object[] { emptyState, "No data", "One.", true });
            int firstAnnouncement = Convert.ToInt32(Property(presenter, "AnnouncementVersion"));
            setState.Invoke(presenter, new object[] { emptyState, "No data", "One.", true });
            int duplicateAnnouncement = Convert.ToInt32(Property(presenter, "AnnouncementVersion"));
            setState.Invoke(presenter, new object[] { emptyState, "No data", "Two.", true });
            int changedDetailAnnouncement = Convert.ToInt32(Property(presenter, "AnnouncementVersion"));
            setState.Invoke(presenter, new object[] { Enum.Parse(stateType, "Success"), "Complete", "Two.", true });
            int changedStateAnnouncement = Convert.ToInt32(Property(presenter, "AnnouncementVersion"));
            Check("exact duplicate state announcement is suppressed", firstAnnouncement > 0 && duplicateAnnouncement == firstAnnouncement);
            Check("detail and state transitions each announce once", changedDetailAnnouncement == firstAnnouncement + 1
                && changedStateAnnouncement == changedDetailAnnouncement + 1);
            host.Close();
        }
    }

    static void CheckSafeTestRoot(MainForm form, string root)
    {
        string prefix = root.TrimEnd(System.IO.Path.DirectorySeparatorChar) + System.IO.Path.DirectorySeparatorChar;
        string[] fields = { "statePath", "profilePath", "macCachePath", "traceLookupCachePathV120" };
        bool isolated = fields.All(name => System.IO.Path.GetFullPath(Convert.ToString(Get(form, name))).StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        Check("test root isolates state profiles and caches", isolated);
        Invoke(form, "SaveAppState");
        Check("test-root state write stays inside temporary root", System.IO.File.Exists(System.IO.Path.Combine(root, "state.json")));
    }

    static void CheckShell(MainForm form)
    {
        List<Control> controls = Flat(form).ToList();
        Control header = controls.FirstOrDefault(control => control.Name == "applicationHeader");
        TabControl tabs = (TabControl)Get(form, "tabs");
        StatusStrip status = controls.OfType<StatusStrip>().First();
        Check("application shell adopts foundation layout", header != null && Flat(header).OfType<TableLayoutPanel>().Any(panel => Convert.ToString(panel.Tag) == "UiFoundationShell"));
        Check("shell navigation and status have accessible names", !String.IsNullOrWhiteSpace(tabs.AccessibleName)
            && !String.IsNullOrWhiteSpace(tabs.AccessibleDescription)
            && !String.IsNullOrWhiteSpace(status.AccessibleName)
            && status.Items.Cast<ToolStripItem>().Where(item => !(item is ToolStripSeparator)).All(item => !String.IsNullOrWhiteSpace(item.AccessibleName)
                || (item is ToolStripStatusLabel && ((ToolStripStatusLabel)item).Spring)));
        Check("all top-level pages have stable accessible names", tabs.TabPages.Cast<TabPage>().All(page => !String.IsNullOrWhiteSpace(page.Name)
            && !String.IsNullOrWhiteSpace(page.AccessibleName)));
        Check("shell tab order begins with primary navigation", tabs.TabIndex == 0 && tabs.TabStop);
    }

    static void CheckCalculators(MainForm form)
    {
        TabControl tabs = (TabControl)Get(form, "tabs");
        TabPage page = tabs.TabPages.Cast<TabPage>().First(tab => tab.Text == "Calculators");
        TextBox subnetInput = (TextBox)Get(form, "subnetInput");
        Button calculate = (Button)Get(form, "subnetCalculateButton");
        TextBox subnetOutput = (TextBox)Get(form, "subnetOutput");
        TextBox unitValue = (TextBox)Get(form, "unitValue");
        ComboBox unitFrom = (ComboBox)Get(form, "unitFrom");
        ComboBox unitTo = (ComboBox)Get(form, "unitTo");
        Button convert = (Button)Get(form, "unitConvertButton");
        object subnetPresenter = Get(form, "subnetStatePresenter");
        object unitPresenter = Get(form, "unitStatePresenter");

        tabs.SelectedTab = tabs.TabPages.Cast<TabPage>().First(tab => tab.Text == "Updates");
        Pump(50);
        subnetInput.Text = "192.0.2.10/24";
        unitValue.Text = "1000";
        bool hiddenSubnetAction = TryPerformVisibleClick(calculate);
        bool hiddenUnitAction = TryPerformVisibleClick(convert);
        Check("semantic harness refuses calculator actions on a hidden tab", !hiddenSubnetAction && !hiddenUnitAction
            && Convert.ToString(Property(subnetPresenter, "State")) == "Idle"
            && Convert.ToString(Property(unitPresenter, "State")) == "Idle"
            && String.IsNullOrWhiteSpace(subnetOutput.Text)
            && ((Label)Get(form, "unitOutput")).Text == "Result: \u2014");

        tabs.SelectedTab = page;
        Pump(80);
        List<Control> controls = Flat(page).ToList();
        Control[] interactive = controls.Where(IsInteractive).ToArray();
        Check("calculator pilot has no unnamed interactive control", interactive.All(control => !String.IsNullOrWhiteSpace(control.AccessibleName)));
        Check("calculator accessible names are unique in context", UniqueAccessibleNames(interactive));
        Check("calculator has persistent From and To labels", controls.OfType<Label>().Any(label => label.Text == "&From")
            && controls.OfType<Label>().Any(label => label.Text == "&To"));

        Check("calculator tab order follows visual task order", subnetInput.TabIndex == 0 && calculate.TabIndex == 1 && subnetOutput.TabIndex == 2
            && unitValue.TabIndex == 0 && unitFrom.TabIndex == 1 && unitTo.TabIndex == 2 && convert.TabIndex == 3);

        subnetInput.Focus();
        Pump(25);
        subnetInput.Parent.SelectNextControl(subnetInput, true, true, true, true);
        Pump(25);
        Check("calculator keyboard focus smoke", calculate.Focused);

        subnetInput.Text = "not-an-address";
        calculate.PerformClick();
        Check("subnet validation is inline and returns focus", Convert.ToString(Property(subnetPresenter, "State")) == "ValidationFailure"
            && subnetInput.Focused && ((Control)subnetPresenter).AccessibleRole == AccessibleRole.Alert);
        subnetInput.Text = "192.0.2.10/24";
        calculate.PerformClick();
        Check("visible subnet action reaches exact success state", Convert.ToString(Property(subnetPresenter, "State")) == "Success"
            && subnetOutput.Text.Contains("Address          192.0.2.10")
            && subnetOutput.Text.Contains("Network          192.0.2.0")
            && subnetOutput.Text.Contains("Broadcast        192.0.2.255")
            && Convert.ToString(Property(subnetPresenter, "TitleText")) == "Subnet calculated");

        int mouseEventCount = 0;
        calculate.MouseClick += delegate { mouseEventCount++; };
        typeof(Control).GetMethod("OnMouseClick", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(calculate, new object[] { new MouseEventArgs(MouseButtons.Left, 1, calculate.Width / 2, calculate.Height / 2, 0) });
        Check("calculator mouse-event routing smoke", mouseEventCount == 1 && calculate.Enabled);

        unitValue.Text = "not-a-number";
        convert.PerformClick();
        Check("unit validation is inline and returns focus", Convert.ToString(Property(unitPresenter, "State")) == "ValidationFailure" && unitValue.Focused);
        unitValue.Text = "1000";
        unitFrom.SelectedItem = "Mbit";
        unitTo.SelectedItem = "Gbit";
        convert.PerformClick();
        Check("visible unit action reaches exact success result", Convert.ToString(Property(unitPresenter, "State")) == "Success"
            && Convert.ToString(Property(unitPresenter, "TitleText")) == "Conversion complete"
            && ((Label)Get(form, "unitOutput")).Text == "Result: 1 Gbit");
    }

    static void CheckEventLog(MainForm form, Type stateType)
    {
        TabControl tabs = (TabControl)Get(form, "tabs");
        TabPage page = tabs.TabPages.Cast<TabPage>().First(tab => tab.Text == "Event Log");
        tabs.SelectedTab = page;
        Pump(80);
        List<Control> controls = Flat(page).ToList();
        Control[] interactive = controls.Where(IsInteractive).ToArray();
        Check("event-log pilot has no unnamed interactive control", interactive.All(control => !String.IsNullOrWhiteSpace(control.AccessibleName)));
        Check("event-log accessible names are unique in context", UniqueAccessibleNames(interactive));
        Check("event-log search and level filters have persistent labels", controls.OfType<Label>().Any(label => label.Text == "&Search")
            && controls.OfType<Label>().Any(label => label.Text == "&Level"));

        DataGridView grid = (DataGridView)Get(form, "logGrid");
        Check("event-log result grid is explicitly read only", grid.ReadOnly && grid.EditMode == DataGridViewEditMode.EditProgrammatically
            && grid.Columns.Cast<DataGridViewColumn>().All(column => column.ReadOnly));
        Check("read-only grid retains operations", grid.AllowUserToOrderColumns && grid.AllowUserToResizeColumns
            && grid.ClipboardCopyMode == DataGridViewClipboardCopyMode.EnableAlwaysIncludeHeaderText
            && grid.Columns.Cast<DataGridViewColumn>().All(column => column.SortMode == DataGridViewColumnSortMode.Automatic));

        DataTable table = (DataTable)Get(form, "logTable");
        TextBox search = (TextBox)Get(form, "logSearch");
        object presenter = Get(form, "logStatePresenter");
        table.Rows.Clear();
        Invoke(form, "UpdateLogStatePresenter");
        Check("event-log empty state is explicit", ((Control)presenter).Visible && Convert.ToString(Property(presenter, "State")) == "Empty"
            && Convert.ToString(Property(presenter, "TitleText")).Contains("No events"));
        table.Rows.Add(new DateTime(2000, 1, 2, 3, 4, 5), "INFO", "Synthetic", "Safe foundation fixture");
        Pump(30);
        int beforeFiltered = Convert.ToInt32(Property(presenter, "AnnouncementVersion"));
        search.Text = "no-match-foundation-fixture";
        Pump(30);
        int afterFiltered = Convert.ToInt32(Property(presenter, "AnnouncementVersion"));
        Check("event-log filtered-empty state is explicit", ((Control)presenter).Visible && Convert.ToString(Property(presenter, "State")) == "FilteredEmpty"
            && Convert.ToString(Property(presenter, "DetailText")).Contains("1 available"));
        Check("event-log filtered-empty transition announces exactly once", afterFiltered == beforeFiltered + 1);

        Invoke(form, "UpdateLogStatePresenter");
        int afterRedundantUpdate = Convert.ToInt32(Property(presenter, "AnnouncementVersion"));
        table.Rows.Add(new DateTime(2000, 1, 2, 3, 4, 6), "INFO", "Synthetic", "Another safe fixture");
        Pump(30);
        int afterNonMatchingRow = Convert.ToInt32(Property(presenter, "AnnouncementVersion"));
        Check("event-log redundant updates do not repeat live announcements", afterRedundantUpdate == afterFiltered
            && afterNonMatchingRow == afterFiltered);

        search.Text = "";
        Pump(30);
        int afterPopulated = Convert.ToInt32(Property(presenter, "AnnouncementVersion"));
        Check("event-log populated transition announces once and yields to data", afterPopulated == afterFiltered + 1
            && !((Control)presenter).Visible && grid.Rows.Count == 2 && Convert.ToString(Property(presenter, "State")) == "Success");
    }

    static void CheckActionRoles(MainForm form)
    {
        Button calculate = (Button)Get(form, "subnetCalculateButton");
        Button convert = (Button)Get(form, "unitConvertButton");
        Button export = (Button)Get(form, "logExportButton");
        Button clear = (Button)Get(form, "logClearButton");
        Button pingStart = (Button)Get(form, "pingStartButton");
        Button pingStop = (Button)Get(form, "pingStopButton");
        Button traceStart = (Button)Get(form, "traceStartButton");
        Check("primary secondary and destructive roles are distinct", Convert.ToString(calculate.Tag) == "ui-action:Primary"
            && Convert.ToString(convert.Tag) == "ui-action:Primary"
            && Convert.ToString(export.Tag) == "ui-action:Secondary"
            && Convert.ToString(clear.Tag) == "ui-action:Destructive");
        Check("non-pilot operation actions retain baseline visual ownership", !Convert.ToString(pingStart.Tag).StartsWith("ui-action:", StringComparison.Ordinal)
            && !Convert.ToString(pingStop.Tag).StartsWith("ui-action:", StringComparison.Ordinal)
            && !Convert.ToString(traceStart.Tag).StartsWith("ui-action:", StringComparison.Ordinal)
            && pingStart.Height == 34 && traceStart.Height == 34);

        DataGridView pingGrid = (DataGridView)Get(form, "pingGrid");
        DataGridView traceGrid = (DataGridView)Get(form, "traceGrid");
        Check("non-pilot grids retain baseline visual ownership", pingGrid.RowTemplate.Height == 32
            && traceGrid.RowTemplate.Height == 32 && pingGrid.ColumnHeadersHeight == 36 && traceGrid.ColumnHeadersHeight == 36
            && pingGrid.DefaultCellStyle.SelectionBackColor == Color.FromArgb(219, 234, 254)
            && traceGrid.DefaultCellStyle.SelectionBackColor == Color.FromArgb(219, 234, 254));

        TabControl tabs = (TabControl)Get(form, "tabs");
        tabs.SelectedTab = tabs.TabPages.Cast<TabPage>().First(tab => tab.Text == "Calculators");
        Pump(25);
        calculate.Focus();
        Pump(25);
        using (var bitmap = new Bitmap(Math.Max(1, calculate.Width), Math.Max(1, calculate.Height)))
            calculate.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
        Check("focused action renders without error", calculate.Focused);
    }

    static void CheckCredentialMetadata(MainForm form)
    {
        const string sentinel = "UIA_SENTINEL_42";
        ((TextBox)Get(form, "collectorAuth1Pass")).Text = sentinel;
        ((TextBox)Get(form, "collectorAuth2Pass")).Text = sentinel;
        bool leaked = Flat(form).Any(control => Contains(control.AccessibleName, sentinel) || Contains(control.AccessibleDescription, sentinel));
        leaked |= Flat(form).OfType<ToolStrip>().SelectMany(strip => strip.Items.Cast<ToolStripItem>())
            .Any(item => Contains(item.AccessibleName, sentinel) || Contains(item.AccessibleDescription, sentinel));
        Check("credential values never enter accessibility metadata", !leaked);
    }

    static void CheckLayouts(MainForm form)
    {
        TabControl tabs = (TabControl)Get(form, "tabs");
        TabPage calculators = tabs.TabPages.Cast<TabPage>().First(tab => tab.Text == "Calculators");
        TabPage eventLog = tabs.TabPages.Cast<TabPage>().First(tab => tab.Text == "Event Log");
        foreach (Size size in new[] { new Size(1100, 700), new Size(1100, 900), new Size(1460, 900) })
        {
            form.Size = size;
            tabs.SelectedTab = calculators;
            Pump(90);
            TextBox subnet = (TextBox)Get(form, "subnetInput");
            Button calculate = (Button)Get(form, "subnetCalculateButton");
            ComboBox from = (ComboBox)Get(form, "unitFrom");
            ComboBox to = (ComboBox)Get(form, "unitTo");
            bool calculatorsSafe = OnScreen(calculators, subnet) && OnScreen(calculators, calculate) && OnScreen(calculators, from) && OnScreen(calculators, to)
                && !ScreenBounds(subnet).IntersectsWith(ScreenBounds(calculate))
                && !ScreenBounds(from).IntersectsWith(ScreenBounds(to));
            Check("calculator layout safe at " + size.Width + "x" + size.Height, calculatorsSafe);

            tabs.SelectedTab = eventLog;
            Pump(90);
            Control[] toolbarControls = { (Control)Get(form, "logSearch"), (Control)Get(form, "logLevel"), (Control)Get(form, "logExportButton"), (Control)Get(form, "logClearButton") };
            bool logSafe = toolbarControls.All(control => OnScreen(eventLog, control)) && AdjacentWithoutOverlap(toolbarControls);
            Check("event-log layout safe at " + size.Width + "x" + size.Height, logSafe);
        }
    }

    static void CheckMissingResources(MainForm form)
    {
        PictureBox logo = Flat(form).OfType<PictureBox>().FirstOrDefault(box => box.Name == "applicationLogo");
        Check("application icon resources are present", form.Icon != null && logo != null && logo.Image != null);
    }

    static int ConstantInt(Type type, string name)
    {
        return Convert.ToInt32(type.GetField(name, AllStatic).GetRawConstantValue());
    }

    static Color StaticColor(Type type, string name)
    {
        return (Color)type.GetProperty(name, AllStatic).GetValue(null, null);
    }

    static double Contrast(Color first, Color second)
    {
        double lighter = Math.Max(Luminance(first), Luminance(second));
        double darker = Math.Min(Luminance(first), Luminance(second));
        return (lighter + 0.05d) / (darker + 0.05d);
    }

    static double Luminance(Color color)
    {
        double r = Linear(color.R / 255d);
        double g = Linear(color.G / 255d);
        double b = Linear(color.B / 255d);
        return 0.2126d * r + 0.7152d * g + 0.0722d * b;
    }

    static double Linear(double value)
    {
        return value <= 0.03928d ? value / 12.92d : Math.Pow((value + 0.055d) / 1.055d, 2.4d);
    }

    static object Get(object target, string name)
    {
        FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null) throw new MissingFieldException(name);
        return field.GetValue(target);
    }

    static object Field(object target, string name)
    {
        FieldInfo field = target.GetType().GetField(name, AllInstance);
        if (field == null) throw new MissingFieldException(name);
        return field.GetValue(target);
    }

    static object Property(object target, string name)
    {
        PropertyInfo property = target.GetType().GetProperty(name, AllInstance);
        if (property == null) throw new MissingMemberException(name);
        return property.GetValue(target, null);
    }

    static object Invoke(object target, string name, params object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic);
        if (method == null) throw new MissingMethodException(name);
        return method.Invoke(target, args);
    }

    static IEnumerable<Control> Flat(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (Control nested in Flat(child)) yield return nested;
        }
    }

    static bool IsInteractive(Control control)
    {
        return control is TextBox || control is RichTextBox || control is ComboBox || control is NumericUpDown
            || control is CheckBox || control is RadioButton || control is Button || control is DataGridView;
    }

    static bool UniqueAccessibleNames(IEnumerable<Control> controls)
    {
        string[] names = controls.Select(control => control.AccessibleName ?? "").Where(name => name.Length > 0).ToArray();
        return names.Length == names.Distinct(StringComparer.OrdinalIgnoreCase).Count();
    }

    static bool Contains(string value, string needle)
    {
        return !String.IsNullOrEmpty(value) && value.IndexOf(needle, StringComparison.Ordinal) >= 0;
    }

    static bool OnScreen(Control host, Control child)
    {
        Rectangle hostBounds = ScreenBounds(host);
        Rectangle childBounds = ScreenBounds(child);
        return child.Visible && child.Width > 0 && child.Height > 0
            && hostBounds.Contains(childBounds.Left, childBounds.Top)
            && hostBounds.Contains(Math.Max(childBounds.Left, childBounds.Right - 1), Math.Max(childBounds.Top, childBounds.Bottom - 1));
    }

    static bool AdjacentWithoutOverlap(Control[] controls)
    {
        Rectangle previous = ScreenBounds(controls[0]);
        for (int i = 1; i < controls.Length; i++)
        {
            Rectangle current = ScreenBounds(controls[i]);
            if (previous.Right > current.Left || current.Left - previous.Right > 16) return false;
            previous = current;
        }
        return true;
    }

    static Rectangle ScreenBounds(Control control)
    {
        return new Rectangle(control.PointToScreen(Point.Empty), control.Size);
    }

    static bool TryPerformVisibleClick(Button button)
    {
        if (button == null || !button.Visible || !button.Enabled) return false;
        button.PerformClick();
        return true;
    }

    static void Pump(int milliseconds)
    {
        DateTime until = DateTime.UtcNow.AddMilliseconds(milliseconds);
        while (DateTime.UtcNow < until) { Application.DoEvents(); Thread.Sleep(10); }
    }
}
