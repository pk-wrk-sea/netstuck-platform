# Changelog

All notable changes are recorded here. Versions use `vMAJOR.MINOR.PATCH` in Git tags and `v.MAJOR.MINOR.PATCH` in the legacy application UI.

## v2.0.0 — Sidebar workspace and version recovery, 2026-10-06

- Introduce sidebar navigation, a shared page header, Home and Settings while preserving all eight tools and persisted page indices.
- Separate Ping target/probe panels, pin operations, retain result/history grids and expose secondary actions through More at narrow widths.
- Add optional event detail, target text-file loading, shared preferences and theme-aware navigation icons/focus.
- Preserve readable Light/Dark colors and reapply Windows system colors when appearance preferences change.
- Fit resized/restored windows to the current working area, with DPI-scaled minimums and scroll-safe content.
- Add explicit GitHub downgrade selection, including archived v1.3.1, exact executable/manifest verification, installed identity checks and rollback backups.
- Preserve polling cadence, stale-result protection, two Traceroute sessions, batching and password-free Collector transport/state.
- Add 460 V2 UI and 27 recovery checks, sanitized captures and focused test profiles. Physical DPI/mixed-monitor/High Contrast/screen-reader acceptance remains separate.

## v1.3.5 — Responsive UI with safe Traceroute Stop, 2026-10-05

- Includes the responsive layout, Light/Dark, text-zoom and snapshot improvements prepared for the unpublished v1.3.4 candidate.
- Restore the WinForms UI synchronization context before awaiting Traceroute Stop; timeout labels remain on the UI thread even when the caller has no synchronization context.
- Prevent an old Stop timeout from changing a replaced run or disposed session page.
- Add a deterministic regression for the off-thread timeout-state update; increase the required lifecycle suite to 32 and the complete runner to 424 checks.
- Preserve the failed v1.3.4 tag and its release-run evidence. The failed run produced no release assets; publication uses the repaired v1.3.5 candidate.

## v1.3.4 — Unpublished UI candidate, 2026-10-05

- Keep Live Ping and Config Collector operations visible outside scrollable settings; size page workspaces from the available client area.
- Add persistent search/credential labels, wrapped card descriptions, content-sized dialog actions and font-aware dropdown sizing.
- Use neutral dark surfaces, clearer muted/semantic text and consistent enabled/disabled action contrast in Light/Dark.
- Correct restored text zoom and resize existing/new table rows; dispose owned zoom fonts.
- Add 91 focused layout/theme regression checks across eight pages, both themes and normal/minimum sizes; preserve existing polling, dual Traceroute and Collector behavior.
- Run fresh local regression, deterministic capture, Windows CI and packaged startup verification for this release. Real Windows Scale 125/150/200%, mixed-DPI and accessibility-mode acceptance remain unverified; see the release report.
- Stage automated release uploads as drafts and verify packaged startup; published assets cannot be overwritten by a workflow rerun.
- Wait for rendered pixels as well as geometry before snapshot capture; retain the strict five-run screenshot comparison.
- Release packaging stopped in a Traceroute Stop timeout on Windows CI; this candidate was not published.

## v1.3.3 — Light and Dark themes, 2026-09-23

- Added a Light / Dark selector in the application header with a locally persisted preference. Older state files default to Light.
- Added theme-aware foreground/background palettes for text, semantic statuses, result grids, selection, input controls, owner-drawn dropdowns, tabs and application dialogs.
- Preserve filled action-button contrast and redraw disabled action labels for the dark palette; keep the Collector terminal in its existing readable dark colors.
- Theme switches recolor existing controls without rebuilding forms or rebinding result data. Native Windows file/message dialogs and system chrome retain Windows rendering.
- Tests, runtime smoke, screenshots and CI are not run, continuing the owner's no-test instruction. This is a build-only release awaiting owner acceptance.

## v1.3.2 — Owner update trial, 2026-09-23

- Provides a newer version for exercising Update now from the local 1.3.1 installation.
- Retains 1.3.1 behavior; updates version metadata and release notes only.
- No regression, smoke, runtime or updater tests were run for 1.3.2, at the owner's explicit request. Build/package integrity checks are not test acceptance.
- Published as a normal Latest release for compatibility with the 1.3.1 stable-only updater. This does not imply completed manual UI/DPI acceptance.

## v1.3.1 — Local candidate (not published)

- Added the selected NS circuit logo and a multi-resolution Windows icon.
- Added column drag guidelines and independent, persisted Traceroute column visibility.
- Recalculate splitter limits on resize; preserve accessible result canvases using workspace scrollbars on constrained displays; clamp restored windows to the current monitor.
- Corrected clipped header logo and Traceroute input arrow bounds; declared system DPI awareness.
- Added optional daily GitHub checks, Check for updates, last-check time, new-version indicator and Update now.
- Update now verifies the stable release ZIP, checksum, exact file inventory, per-file manifest and executable version, then replaces the portable files after exit and restarts. Keeps a verified backup and durable recovery record.
- Serialize atomic JSON writes with backup recovery; restore in-memory profiles after failed saves; bound MAC/WAN requests and cancel them at close; display the actual timezone offset.
- Added 40 focused maintenance checks without removing the prior 292 checks. Current execution evidence is in the v1.3.1 preparation report.

## v1.3.0 — Published 2026-08-28

- Added shared UI tokens, action roles, accessibility helpers and semantic state presentation for the application shell, Calculators and Event Log pilots.
- Added persistent input labels and clearer accessible control identity without changing the established WinForms interaction model.
- Added deterministic UI capture, privacy, rollback and build/package provenance verification infrastructure.
- Corrected Traceroute lifecycle ownership, UI-thread dispatch, stop/restart gating and stale completion handling.
- Preserved two independent Traceroute sessions, fixed polling cadence, Config Collector authentication order and credential-free process arguments/state/logs.
- Recorded physical-pointer/native-dropdown and 125% DPI acceptance. Remaining manual gates were owner-waived for this release only; see the original acceptance report.
- Published as GitHub Release `v1.3.0`; the original preparation and acceptance reports remain historical evidence.

## v1.2.3 — Baseline

- Replaced fixed-width Traceroute flow rows with deterministic, adjacent grid columns.
- Aligned Target, Max Hops, Timeout and Interval on the first row.
- Aligned Protocol, Port and Packet Size on the second row so the Protocol dropdown cannot cover timing inputs.
- Preserved dedicated Start/Pause/Stop actions and narrow-window safeguards.
- Made SplitContainer startup deterministic on constrained desktops so Traceroute and Collector inputs do not collapse into the default split.
- Made Collector stdin BOM-free under UTF-8 Windows consoles while keeping passwords out of process arguments.
- Deferred the shared result-grid font cleanup until after control teardown to prevent an intermittent Windows shutdown access fault.
- Added close-state guards for startup NTP/Public-IP tasks and isolated those external calls from deterministic UI/performance tests.
- Stabilized the completed cadence harness against a Windows Server 2025 CLR/native Ping finalizer race.
- Repository preparation: added reproducible build/test/package scripts, CI, maintenance documentation and an AI skill.
- Repository hardening: removed an unused legacy SSH method that passed passwords through process arguments and replaced organization-specific assembly metadata. Active collector behavior and application version remain unchanged.

## v1.2.2

- Prevented Traceroute timing inputs and action buttons from overlapping at narrow widths or high DPI.
- Standardized enabled and disabled Traceroute input backgrounds.
- Corrected minimum splitter sizing.

## v1.2.0–v1.2.1

- Added changed-row Traceroute updates, adaptive TTL polling and persistent ISP/DNS caches.
- Added batched Collector terminal output and streaming large-config capture.
- Added Collector error-only CSV export and completed removal of Log Sanitizer.
- Refined Traceroute control-panel layout.

## v1.0.0–v1.1.0

- Established Live Ping, Traceroute, DNS, MAC/WAN, Calculators and Config Collector workflows.
- Added profiles, persistent UI state, dual authentication, export logs, copyable grids and performance safeguards.

Detailed historical notes remain in `docs/releases/v1.2.3/` and in the application Updates tab.
