# V2 design system

Use `UiTokens` for established semantic resources and `V2Design` for active V2 dimensions. Do not introduce page-specific global margins or repeated independent color palettes.

| Role | Resource / value at 96 DPI |
| --- | --- |
| Spacing XS / S / M / L / XL | 4 / 8 / 12 / 16 / 24 |
| Sidebar | 184 logical pixels; content panes use scrolling on constrained displays |
| Shared page header | 84 |
| Page margin / section gap / field gap / toolbar gap | 12 / 12 / 6 / 8 |
| Native action height / navigation row / icon | 34 / 38 / 20 |
| Ping configuration / action width | 340 / 96 |
| App title / page title / section title | Segoe UI 13 bold / 22 bold / 11.5 semibold |
| Body / caption / data editor | Segoe UI 9.25 / 8.5 / Consolas 9 |
| Metrics | Existing semantic metric labels, optional visibility |

Semantic text/background resources cover primary/muted text, surface/subtle surface, canvas, border, focus, accent, success, warning, error and selection. Light uses quiet near-white surfaces; Dark uses neutral charcoal surfaces with pale text, green/amber/red status text and matching selection. High Contrast uses Windows system colors and native focus/state cues. Disabled action rendering follows the shared theme layer. Native frames, scrollbars and some disabled-input chrome follow Windows.

Use one primary action per operation: Start, Collect or Calculate. Pause and secondary data commands stay neutral. Delete/Clear are distinguishable destructive commands; an active Stop follows its existing running-state convention. Status always contains readable text; color does not carry the only meaning. Numeric result columns align right; columns retain their deliberate widths and horizontal scrolling.

Use container layout for related groups and Dock/Anchor for a single flexible content area. Keep the hierarchy shallow where practical; follow [Microsoft's TableLayoutPanel guidance](https://learn.microsoft.com/en-us/dotnet/desktop/winforms/controls/best-practices-for-the-tablelayoutpanel-control). Main workflows use measured/wrapped captions and scrolling instead of screen-specific coordinates. Long primary labels remain visible; secondary descriptions may wrap or use accessible ellipsis.

Interactive controls need meaningful accessible names, native roles, keyboard activation and visible focus. Existing explicit pilot field order is preserved. Sensitive field values never become accessibility labels. Theme/zoom/navigation changes recolor or lay out existing controls and preserve bindings, selection and operator column choices.
