# Changelog

All notable changes are recorded here. Versions use `vMAJOR.MINOR.PATCH` in Git tags and `v.MAJOR.MINOR.PATCH` in the legacy application UI.

## v1.3.0 — Candidate (not released)

- Added shared UI tokens, action roles, accessibility helpers and semantic state presentation for the application shell, Calculators and Event Log pilots.
- Added persistent input labels and clearer accessible control identity without changing the established WinForms interaction model.
- Added deterministic UI capture, privacy, rollback and build/package provenance verification infrastructure.
- Corrected Traceroute lifecycle ownership, UI-thread dispatch, stop/restart gating and stale completion handling.
- Preserved two independent Traceroute sessions, fixed polling cadence, Config Collector authentication order and credential-free process arguments/state/logs.
- Recorded physical-pointer and native-dropdown merge acceptance; 125%, 150% and 200% DPI, High Contrast and screen-reader release gates remain open.
- Prepared a local candidate only; no `v1.3.0` tag or GitHub Release exists yet.

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
