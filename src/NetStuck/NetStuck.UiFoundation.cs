using System;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace NetStuck
{
    enum UiActionRole
    {
        Primary,
        Secondary,
        Destructive
    }

    enum UiSemanticState
    {
        Idle,
        Success,
        Empty,
        FilteredEmpty,
        ValidationFailure
    }

    sealed class UiStateDefinition
    {
        public readonly string Marker;
        public readonly string DefaultTitle;
        public readonly Color Foreground;
        public readonly Color Background;
        public readonly AccessibleRole AccessibleRole;

        public UiStateDefinition(string marker, string defaultTitle, Color foreground, Color background,
            AccessibleRole accessibleRole)
        {
            Marker = marker;
            DefaultTitle = defaultTitle;
            Foreground = foreground;
            Background = background;
            AccessibleRole = accessibleRole;
        }
    }

    static class UiTokens
    {
        public const int SpaceXs = 4;
        public const int SpaceSm = 8;
        public const int SpaceMd = 12;
        public const int SpaceLg = 16;

        public const int PageMargin = SpaceMd;
        public const int AppHeaderHeight = 72;
        public const int SectionHeaderHeight = 55;
        public const int DenseControlHeight = 34;
        public const int SplitterWidth = 8;
        public const int IconLarge = 40;
        public const int BorderWidth = 1;
        public const int StandardFieldMinWidth = 160;

        public const string FontFamily = "Segoe UI";
        public const string MonospaceFontFamily = "Consolas";
        public const float BodyFontSize = 9.25f;
        public const float CaptionFontSize = 8.5f;
        public const float ActionFontSize = 9f;
        public const float SectionTitleFontSize = 11.5f;
        public const float AppTitleFontSize = 17f;
        public const float ResultFontSize = 15f;

        public static Color Surface { get { return SystemInformation.HighContrast ? SystemColors.Window : Color.White; } }
        public static Color SurfaceSubtle { get { return SystemInformation.HighContrast ? SystemColors.Window : Color.FromArgb(248, 250, 252); } }
        public static Color Text { get { return SystemInformation.HighContrast ? SystemColors.WindowText : Color.FromArgb(30, 41, 59); } }
        public static Color MutedText { get { return SystemInformation.HighContrast ? SystemColors.WindowText : Color.FromArgb(71, 85, 105); } }
        public static Color Border { get { return SystemInformation.HighContrast ? SystemColors.WindowText : Color.FromArgb(100, 116, 139); } }
        public static Color Focus { get { return SystemInformation.HighContrast ? SystemColors.Highlight : Color.FromArgb(29, 78, 216); } }
        public static Color Info { get { return SystemInformation.HighContrast ? SystemColors.WindowText : Color.FromArgb(29, 78, 216); } }
        public static Color Success { get { return SystemInformation.HighContrast ? SystemColors.WindowText : Color.FromArgb(22, 101, 52); } }
        public static Color Error { get { return SystemInformation.HighContrast ? SystemColors.WindowText : Color.FromArgb(185, 28, 28); } }
        public static Color Destructive { get { return Error; } }
        public static Color InfoSurface { get { return SystemInformation.HighContrast ? SystemColors.Window : Color.FromArgb(239, 246, 255); } }
        public static Color SuccessSurface { get { return SystemInformation.HighContrast ? SystemColors.Window : Color.FromArgb(240, 253, 244); } }
        public static Color ErrorSurface { get { return SystemInformation.HighContrast ? SystemColors.Window : Color.FromArgb(254, 242, 242); } }
        public static Color HoverSurface { get { return SystemInformation.HighContrast ? SystemColors.Highlight : Color.FromArgb(226, 232, 240); } }
        public static Color PressedSurface { get { return SystemInformation.HighContrast ? SystemColors.Highlight : Color.FromArgb(203, 213, 225); } }

        public static UiStateDefinition State(UiSemanticState state)
        {
            switch (state)
            {
                case UiSemanticState.Idle:
                    return new UiStateDefinition("[i]", "Ready", Info, InfoSurface, AccessibleRole.StatusBar);
                case UiSemanticState.Success:
                    return new UiStateDefinition("[OK]", "Complete", Success, SuccessSurface, AccessibleRole.StatusBar);
                case UiSemanticState.Empty:
                    return new UiStateDefinition("[-]", "No data", MutedText, SurfaceSubtle, AccessibleRole.StatusBar);
                case UiSemanticState.FilteredEmpty:
                    return new UiStateDefinition("[-]", "No matching results", MutedText, SurfaceSubtle, AccessibleRole.StatusBar);
                case UiSemanticState.ValidationFailure:
                    return new UiStateDefinition("[!]", "Check the input", Error, ErrorSurface, AccessibleRole.Alert);
                default:
                    throw new ArgumentOutOfRangeException("state");
            }
        }
    }

    static class UiAccessibility
    {
        static readonly Regex DecorativePrefix = new Regex(@"^[\s\u25CF\u25B6\u25A0]+", RegexOptions.Compiled);

        public static void Configure(Control control, string stableName, string accessibleName, string accessibleDescription, int tabIndex)
        {
            if (control == null) throw new ArgumentNullException("control");
            if (!String.IsNullOrWhiteSpace(stableName)) control.Name = stableName;
            control.AccessibleName = accessibleName ?? "";
            control.AccessibleDescription = accessibleDescription ?? "";
            if (tabIndex >= 0) control.TabIndex = tabIndex;
        }

        public static void Configure(ToolStripItem item, string accessibleName, string accessibleDescription)
        {
            if (item == null) throw new ArgumentNullException("item");
            item.AccessibleName = accessibleName ?? "";
            item.AccessibleDescription = accessibleDescription ?? "";
        }

        public static Label AssociateLabel(string text, Control input, string stableName,
            string accessibleName, string accessibleDescription, int tabIndex)
        {
            if (input == null) throw new ArgumentNullException("input");
            Configure(input, stableName, accessibleName, accessibleDescription, tabIndex);
            return new Label
            {
                Text = text,
                AutoSize = true,
                UseMnemonic = true,
                ForeColor = UiTokens.MutedText,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, UiTokens.SpaceXs, UiTokens.SpaceXs, UiTokens.SpaceXs),
                AccessibleName = accessibleName + " label",
                AccessibleDescription = "Label for " + accessibleName,
                TabStop = false
            };
        }

        public static string ActionName(string text)
        {
            string value = (text ?? "").Replace("&", "").Trim();
            value = DecorativePrefix.Replace(value, "");
            return value.Length == 0 ? "Action" : value;
        }
    }

    static class UiFoundation
    {
        const string ActionTagPrefix = "ui-action:";

        public static void ConfigureActionButton(Button button, UiActionRole role)
        {
            if (button == null) throw new ArgumentNullException("button");
            bool alreadyConfigured = Convert.ToString(button.Tag).StartsWith(ActionTagPrefix, StringComparison.Ordinal);
            button.Tag = ActionTagPrefix + role.ToString();
            button.Height = UiTokens.DenseControlHeight;
            button.FlatStyle = FlatStyle.Flat;
            button.Font = new Font(UiTokens.FontFamily, UiTokens.ActionFontSize, FontStyle.Bold);
            button.Cursor = Cursors.Hand;
            button.Padding = new Padding(UiTokens.SpaceMd, 0, UiTokens.SpaceMd, 0);
            button.FlatAppearance.BorderSize = UiTokens.BorderWidth;
            ApplyActionColors(button, role);
            if (String.IsNullOrWhiteSpace(button.AccessibleName)) button.AccessibleName = UiAccessibility.ActionName(button.Text);
            button.AccessibleDescription = ActionDescription(role);
            if (!alreadyConfigured)
            {
                button.GotFocus += delegate { button.Invalidate(); };
                button.LostFocus += delegate { button.Invalidate(); };
                button.Paint += delegate(object sender, PaintEventArgs e)
                {
                    if (!button.Focused) return;
                    Rectangle focus = Rectangle.Inflate(button.ClientRectangle, -UiTokens.SpaceXs, -UiTokens.SpaceXs);
                    if (focus.Width > 0 && focus.Height > 0)
                        ControlPaint.DrawFocusRectangle(e.Graphics, focus, UiTokens.Focus, button.BackColor);
                };
            }
        }

        static void ApplyActionColors(Button button, UiActionRole role)
        {
            if (SystemInformation.HighContrast)
            {
                button.BackColor = SystemColors.Control;
                button.ForeColor = SystemColors.ControlText;
                button.FlatAppearance.BorderColor = SystemColors.WindowText;
                button.FlatAppearance.MouseOverBackColor = SystemColors.Highlight;
                button.FlatAppearance.MouseDownBackColor = SystemColors.Highlight;
                return;
            }

            if (role == UiActionRole.Primary)
            {
                button.BackColor = UiTokens.Info;
                button.ForeColor = Color.White;
                button.FlatAppearance.BorderColor = UiTokens.Info;
                button.FlatAppearance.MouseOverBackColor = Color.FromArgb(30, 64, 175);
                button.FlatAppearance.MouseDownBackColor = Color.FromArgb(30, 58, 138);
            }
            else if (role == UiActionRole.Destructive)
            {
                button.BackColor = UiTokens.Surface;
                button.ForeColor = UiTokens.Destructive;
                button.FlatAppearance.BorderColor = UiTokens.Destructive;
                button.FlatAppearance.MouseOverBackColor = UiTokens.ErrorSurface;
                button.FlatAppearance.MouseDownBackColor = Color.FromArgb(254, 226, 226);
            }
            else
            {
                button.BackColor = UiTokens.Surface;
                button.ForeColor = UiTokens.Text;
                button.FlatAppearance.BorderColor = UiTokens.Border;
                button.FlatAppearance.MouseOverBackColor = UiTokens.HoverSurface;
                button.FlatAppearance.MouseDownBackColor = UiTokens.PressedSurface;
            }
        }

        static string ActionDescription(UiActionRole role)
        {
            if (role == UiActionRole.Primary) return "Primary action for this section.";
            if (role == UiActionRole.Destructive) return "Destructive action that removes saved or displayed data.";
            return "Secondary action for this section.";
        }

        public static void ConfigureReadOnlyResultGrid(DataGridView grid, string stableName,
            string accessibleName, string accessibleDescription, int tabIndex)
        {
            UiAccessibility.Configure(grid, stableName, accessibleName, accessibleDescription, tabIndex);
            grid.ReadOnly = true;
            grid.EditMode = DataGridViewEditMode.EditProgrammatically;
            grid.AccessibleRole = AccessibleRole.Table;
            foreach (DataGridViewColumn column in grid.Columns) column.ReadOnly = true;
        }
    }

    sealed class UiStatePresenter : Panel
    {
        readonly Label markerLabel;
        readonly Label titleLabel;
        readonly Label detailLabel;
        UiSemanticState currentState;
        string currentTitle;
        string currentDetail;
        UiSemanticState? lastAnnouncedState;
        string lastAnnouncedTitle;
        string lastAnnouncedDetail;
        int announcementVersion;

        public UiStatePresenter(string stableName, string accessibleName)
        {
            Name = stableName;
            AccessibleName = accessibleName;
            AccessibleRole = AccessibleRole.StatusBar;
            Dock = DockStyle.Top;
            Height = 72;
            Padding = new Padding(UiTokens.SpaceMd, UiTokens.SpaceSm, UiTokens.SpaceMd, UiTokens.SpaceSm);
            Margin = new Padding(0, UiTokens.SpaceSm, 0, UiTokens.SpaceSm);
            TabStop = false;

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = new Padding(0), Padding = new Padding(0) };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            markerLabel = new Label { AutoSize = true, Font = new Font(UiTokens.FontFamily, UiTokens.ActionFontSize, FontStyle.Bold), Margin = new Padding(0, 5, UiTokens.SpaceSm, 0), TabStop = false };
            var text = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = new Padding(0), Padding = new Padding(0) };
            text.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
            text.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            titleLabel = new Label { Dock = DockStyle.Fill, Font = new Font(UiTokens.FontFamily, UiTokens.ActionFontSize, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true, TabStop = false };
            detailLabel = new Label { Dock = DockStyle.Fill, Font = new Font(UiTokens.FontFamily, UiTokens.CaptionFontSize), TextAlign = ContentAlignment.TopLeft, AutoEllipsis = true, TabStop = false };
            text.Controls.Add(titleLabel, 0, 0);
            text.Controls.Add(detailLabel, 0, 1);
            layout.Controls.Add(markerLabel, 0, 0);
            layout.Controls.Add(text, 1, 0);
            Controls.Add(layout);
            SetState(UiSemanticState.Idle, null, null, false);
        }

        public UiSemanticState State { get { return currentState; } }
        public string TitleText { get { return titleLabel.Text; } }
        public string DetailText { get { return detailLabel.Text; } }
        public int AnnouncementVersion { get { return announcementVersion; } }

        public void SetState(UiSemanticState state, string title, string detail, bool announce)
        {
            currentState = state;
            currentTitle = title;
            currentDetail = detail;
            UiStateDefinition definition = UiTokens.State(state);
            string effectiveTitle = String.IsNullOrWhiteSpace(title) ? definition.DefaultTitle : title.Trim();
            string effectiveDetail = detail == null ? "" : detail.Trim();
            BackColor = definition.Background;
            ForeColor = definition.Foreground;
            AccessibleRole = definition.AccessibleRole;
            markerLabel.Text = definition.Marker;
            markerLabel.ForeColor = definition.Foreground;
            titleLabel.Text = effectiveTitle;
            titleLabel.ForeColor = definition.Foreground;
            detailLabel.Text = effectiveDetail;
            detailLabel.ForeColor = UiTokens.Text;
            AccessibleName = definition.Marker + " " + effectiveTitle;
            AccessibleDescription = effectiveDetail;
            bool changedAnnouncement = !lastAnnouncedState.HasValue || lastAnnouncedState.Value != state
                || !String.Equals(lastAnnouncedTitle, effectiveTitle, StringComparison.Ordinal)
                || !String.Equals(lastAnnouncedDetail, effectiveDetail, StringComparison.Ordinal);
            if (announce && changedAnnouncement && IsHandleCreated)
            {
                AccessibilityNotifyClients(AccessibleEvents.NameChange, -1);
                lastAnnouncedState = state;
                lastAnnouncedTitle = effectiveTitle;
                lastAnnouncedDetail = effectiveDetail;
                announcementVersion++;
            }
        }

        protected override void OnSystemColorsChanged(EventArgs e)
        {
            base.OnSystemColorsChanged(e);
            SetState(currentState, currentTitle, currentDetail, false);
        }
    }
}
