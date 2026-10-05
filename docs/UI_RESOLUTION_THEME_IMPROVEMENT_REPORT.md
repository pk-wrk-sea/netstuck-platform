# UI resolution and theme improvement — 2026-10-05

## Outcome and scope

Audited the eight active pages, shell, cards, labels, action groups, text/search inputs, dropdowns, result grids, terminal and custom dialogs. Applied layout/theme repairs to the active WinForms builders. Application version remains 1.3.3; portable distribution and the existing system-DPI compatibility boundary remain unchanged.

The initial checkout was clean at `54a972175039812409ee92105da527ccbb707e11`. All captures and tests use isolated, disposable state and synthetic inputs. No operator state or collector captures are included in this report.

## Findings and repairs

| Area | Evidence / best-practice assessment | Implemented behavior |
| --- | --- | --- |
| All page canvases | A common 620-pixel content minimum (820 for Collector) introduced page scrolling even when substantial content could fit. The scrollbar reserve also created avoidable overflow. | Per-page content floors, available-client sizing and zero wrapper margins. Scrolling remains a fallback on smaller workspaces. |
| Live Ping inputs | Start/Pause/Stop were below a tall input stack at 1100×700. | Operations remain at the bottom of the visible card; settings/target content scroll independently. |
| Live Ping results | Search depended on placeholder text; history lost usable height at the minimum size. | Persistent search label and accessible name; compact toolbar and separate minimum allocations for the result/history panes. Both grids retain at least 90 pixels in tested geometry. |
| Config Collector | At minimum size, the single action row squeezed its first button and pushed collection controls below the viewport. Authentication labels disappeared after entering text. | Two balanced action rows outside the input scroller. Persistent credential column labels and AUTH-specific accessible names; password masking and fallback behavior are preserved. |
| Traceroute | Fixed-height, non-wrapping information line risked hiding related status/actions. | Information wraps within the card. Two sessions and the existing grouped protocol/port/packet inputs remain intact. |
| Section headers/cards | Fixed description positions and a 900-pixel maximum assumed a wide parent. | Headers measure the available width and expand for wrapped descriptions; narrow Thai/English content is covered by regression checks. Decorative card borders remain quiet; action/input boundaries remain stronger. |
| MAC / WAN Lookup | Existing grouped input/action and results layout remained usable in inspected sizes. | Shared header, action, theme, fallback and text-zoom repairs apply; no lookup workflow changes. |
| DNS Resolver | Existing input/results grouping remained operable after removing the oversized common canvas floor. | Shared responsive/theme improvements; polling controls and cadence remain unchanged. |
| Calculators | Existing pilot action roles, validation and field grouping were suitable. | Shared responsive canvas/header/theme fixes; pilot contracts retained. |
| Event Log | Existing filter/copy/clear pilot structure was suitable. | Shared geometry, contrast and existing/new-row zoom improvements; batched logging retained. |
| Updates | Existing check/install hierarchy remained suitable and visible. | Shared styles, header and fallback improvements. No release/download/install action was invoked during visual review. |
| Light/Dark | Blue-heavy dark surfaces, weak light semantic text and inconsistent disabled actions reduced comfort/readability. | Neutral charcoal dark surfaces, softer light canvas, stronger muted/semantic text, consistent disabled styling, quiet status strip, explicit hover/pressed colors. |
| Dropdowns | Owner-drawn item height did not follow enlarged fonts; long items could truncate in the popup. | Item height follows the font; popup width measures item text and is capped to the working area. Selection remains unchanged. |
| Custom dialogs | Save-list prompt used absolute coordinates; fixed button bars/hop hint heights could clip enlarged content. | Save prompt uses content-sized rows. Custom dialogs use a DPI baseline and working-area clamp; action bars and hop hints size to content; column lists support horizontal scrolling. |
| Ctrl+wheel / restored zoom | Existing bound rows retained old heights; restored zoom divided the baseline before applying it. Font replacement did not own/dispose created fonts. | Baselines are stored before restored zoom; existing/new rows and headers fit enlarged text. Zoom-created fonts are tracked and disposed. |

## Design basis

Use container layout for related field/action groups, measure text where wrapping is required, and reserve scrolling for content that cannot fit. This follows Microsoft's [TableLayoutPanel guidance](https://learn.microsoft.com/en-us/dotnet/desktop/winforms/controls/best-practices-for-the-tablelayoutpanel-control) without rebuilding the entire application around nested tables.

Theme changes preserve hierarchy, semantic status and readable foreground/background combinations. The reviewed text, grid header/selection and primary-action combinations pass 4.5:1 contrast checks in both themes; this is a measured subset, not an accessibility certification of every OS-drawn pixel. See Microsoft's [theming guidance](https://learn.microsoft.com/en-us/windows/apps/develop/ui/theming) and [contrast-theme guidance](https://learn.microsoft.com/en-us/windows/apps/design/accessibility/high-contrast-themes).

## Validation evidence

The test host and native review fixture use the production `app.manifest` for system-DPI awareness. The observed desktop is 1920×1080 with a 1920×1032 working area; process DPI is 96. Geometry tests run with autoscaling enabled. Legacy suites may deliberately disable autoscaling for their own constrained-geometry cases; their output is not high-DPI acceptance.

Native review covered Light/Dark Live Ping, the Light theme popup, the Dark Traceroute protocol popup, hop-description and column dialogs, and Config Collector's terminal/input scroller. The Collector actions stay visible after scrolling down to commands and the output folder. Related popup text is readable and does not cover the adjacent protocol/port fields. Captures are saved in `artifacts/ui-resolution/native/`. The remaining page/size/theme combinations were inspected through the structural bitmap set and regression geometry, rather than being recorded as native interactive runs.

Final regression runs of `scripts/Test-NetStuck.ps1 -SoakSeconds 10`:

| Host | Passed / discovered | Failed / skipped / infrastructure failures | Required suites |
| --- | --- | --- | --- |
| Windows PowerShell 5.1.26100.3624 | 423 / 423 | 0 / 0 / 0 | 12 / 12 |
| PowerShell 7.6.5 | 423 / 423 | 0 / 0 / 0 | 12 / 12 |

Both compilation/build stages passed on each host. This includes 91 new UI layout/theme checks, the existing 130-check unchanged-behavior corpus, Collector integration and independent Traceroute lifecycle coverage. Final summaries and full output are `artifacts/ui-resolution/test-summary-ps51.json`, `test-summary-ps7.json`, `full-ps51.log` and `full-ps7.log`. Earlier diagnostic/intermediate runs are retained separately and are not used as final acceptance.

| Fresh measurement | PowerShell 5.1 | PowerShell 7 |
| --- | --- | --- |
| Warm UI startup | 1,381 ms | 1,396 ms |
| Worst UI dispatch under /24 load | 44 ms | 17 ms |
| Worst dual-Traceroute UI dispatch | 58 ms | 68 ms |
| Working set in performance suite | 86 MB | 80 MB |
| Live Ping: 250 ms vs 1000 ms, timeout 1000 ms | 11 vs 3 completions | 11 vs 3 completions |
| Traceroute: 250 ms vs 1000 ms, timeout 1000 ms | 14 vs 4 samples | 14 vs 4 samples |
| Soak measured duration / worst dispatch / memory growth | 11.418 s / 41 ms / 16 MB | 11.480 s / 17 ms / 11 MB |

These are local measurements, not performance promises for other machines. Final production source-input fingerprint is `a40106c8f905fd33ddfcd9dbdee9ad0514994f3739f3bed7ff5730fff3e38bfc`; build version is `1.3.3.0`. No production host, update installation, release packaging or publication was performed.

`scripts/Capture-UiFoundations.ps1 -DeterminismRuns 5` passed: all nine scenario hashes match across all five runs, with semantic assertions and PNG/privacy checks passing. The validated screenshot set was promoted to `docs/ui-foundations/screenshots/`; full output is `artifacts/ui-resolution/capture-closure.log`. An additional 32 page captures cover eight pages × two window sizes × two themes in `artifacts/ui-resolution/final-structural/`.

Artifacts are under ignored `artifacts/ui-resolution/`. Bitmap page captures are structural evidence only: native dropdown text and RichTextBox contents require interactive inspection. Microsoft's [DrawToBitmap documentation](https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.control.drawtobitmap?view=netframework-4.8.1) documents container/rendering limitations and RichTextBox border-only rendering. The capture harness does not disable autoscaling, but a 96-DPI capture still cannot prove another DPI setting.

## Display acceptance boundary

| Scenario | Status |
| --- | --- |
| 1460×900 and 1100×700, Light/Dark, all eight pages | Structural checks and visual capture review on the 96-DPI host. Native review coverage is described above. |
| 1024×600 constrained-workspace fallback | Existing Maintenance suite checks all grids and Traceroute input containment; scrolling is intentionally allowed. |
| App text zoom 100–180%, existing/new rows, persisted 160% | Focused regression coverage; no cumulative font scaling. |
| Real Windows Scale 125%, 150%, 200% | **UNVERIFIED** — this session has a 96-DPI desktop. |
| Different-DPI monitor move, docking, RDP reconnect | **UNVERIFIED** — no matching display setup exercised. Existing system-DPI bitmap scaling can remain blurry after a monitor move. |
| Windows accessibility text size, High Contrast, screen reader | **UNVERIFIED** — code retains system-color paths, but these modes were not activated during this task. |

Native window chrome, scrollbar/thumb rendering and some checkbox/combo borders still follow Windows. A future per-monitor rendering change needs explicit framework/runtime support evidence and a real mixed-DPI test matrix.
